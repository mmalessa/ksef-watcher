# TODO

Rzeczy pozostawione świadomie poza zakresem dotychczasowej pracy (implementacja + TDD kroków
1–9 procesu DDD, patrz `docs/`), zebrane w jednym miejscu. Pogrupowane wg pilności/rodzaju.

## Luki behawioralne (kod działa, ale niezgodnie z udokumentowanym zamiarem)

- ~~"Głośne logi" z dokumentacji nie są nigdzie faktycznie zaimplementowane.~~ **Zamknięte
  (2026-09-05).** `KsefAccessService` loguje `SubjectPollFailed` przy klasyfikacji
  (`Warning` dla `RateLimited`, `Error` dla `ApiError`, I-8); `DeliveryService` loguje `Error`
  przy każdej klasyfikacji do `Failed(Permanent)` (I-11); `ConfigWatcher.Reload` loguje `Error`
  przy odrzuceniu configu, niezależnie od tego czy ktoś subskrybuje `ReloadRejected` (I-16) —
  dokładnie zgodnie z tabelą "Where" w `docs/09_architecture.md`. `PollingBackgroundService`'s
  generyczny catch-all `logger.LogError(ex, "Poll cycle failed...")` **zostaje bez zmian** —
  to osobna, szersza siatka bezpieczeństwa (I-3: żaden wyjątek nie może zabić hosta), nie
  duplikat klasyfikacji, która już dzieje się niżej w `KsefAccessService`.
  `Program.cs` łączy `ConfigWatcher` z bootstrapowym `ILoggerFactory` (konsola) tworzonym przed
  `Host.CreateApplicationBuilder`, bo `ConfigWatcher.Start` musi zadziałać zanim kontener DI
  istnieje.

- ~~Brak walidacji "dokładnie jeden kanał na subject" (OQ-12).~~ **Zamknięte (2026-09-05).**
  `ConfigLoader.ValidateSubject` sprawdza teraz `subject.Channels.Count != 1` i dodaje
  `subjects[i].channels: must have exactly one entry (OQ-12), had {n}.` — subject z zerem lub
  wieloma kanałami dostaje czytelny błąd konfiguracji przy starcie (I-13 fail-fast) zamiast
  `IndexOutOfRangeException`-a przy pierwszym pollu albo cichego ignorowania kanałów poza
  pierwszym.

## Brakujące funkcje o znanym miejscu w architekturze

- ~~KsefClientAdapter: jedno środowisko KSeF na cały daemon, nie per subject.~~
  **Zamknięte (2026-09-05).** `KsefClientAdapter` przyjmuje teraz trzy fabryki
  (`Func<Environment, IAuthCoordinator>`, `Func<Environment, Task<ICryptographyService>>`,
  `Func<Environment, IInvoiceDownloadClient>`) zamiast stałych instancji — środowisko rozwiązywane
  per wywołanie z `SubjectCredentials.Environment` (open) / nowego `KsefSession.Environment`
  (query, dodane pole z domyślną wartością `"test"` żeby nie ruszać niepowiązanych testów). Jeden
  proces daemona obsługuje teraz subjecty na test/demo/prod jednocześnie. `Program.cs` buduje
  fabryki z `IKSeFClientFactory`/`IKSeFFactoryCryptographyServices` (biblioteka cache'uje je już
  wewnętrznie per środowisko — sprawdzone w źródle) — usunięto stare stałe rejestracje
  `IKSeFClient`/`IAuthCoordinator`/`IInvoiceDownloadClient`/`ICryptographyService` razem z
  towarzyszącym im eager-warmupem dla jednego środowiska (i tak miał sens tylko w starym modelu).
  6 istniejących testów zaadaptowano do nowego kształtu konstruktora (fakes owinięte w
  `_ => fake`), bez zmiany ich asercji — plus 3 nowe testy na resolution per środowisko. Po drodze
  złapano trzeci z rzędu fałszywie-zielony test w tej sesji (nowe testy dla query-side resolution
  i default-environment przechodziły od razu, bo obie ścieżki zaimplementowano razem z pierwszym
  testem) — naprawiono przez chwilowe zepsucie implementacji, potwierdzenie prawdziwego RED,
  przywrócenie.

- ~~Realny `FileSystemWatcher` w Hoście.~~ **Zamknięte (2026-09-05).** Nowy
  `KsefWatcher.Host.Configuration.ConfigFileWatcher` (`IHostedService`) obserwuje `config.yaml` i
  woła `ConfigWatcher.Reload` przy zmianie. Split na testowalność: `HandleFileChanged()` (odczyt +
  reload) jest zwykłą metodą testowaną przez fake `IConfigFileReader` (3 testy: udany reload,
  cichy powrót przy `IOException` bez utraty ostatniego poprawnego configu, `LogWarning` przy tym
  samym); realne okablowanie `System.IO.FileSystemWatcher` w `StartAsync`/`StopAsync` to cienki,
  nieautomatyzowany klej. **Zweryfikowane ręcznie**, nie tylko założone: mini-program w
  bind-mountowanym katalogu (dokładnie taki setup jak `make build`/`make test`), zapis przez
  atomic temp-file+rename (tak jak vim/większość edytorów na Linuksie) — realne zdarzenie inotify
  zaobserwowane, config faktycznie się przeładował. Świadomie **bez debounce**: powtórny
  `Reload` identyczną treścią jest nieszkodliwy (I-17), a próba przetestowania debounce'a
  istniejącym `IDelay`/`FakeDelay` okazała się bezcelowa — `FakeDelay` rozstrzyga się od razu, więc
  nie da się nim deterministycznie symulować okna czasowego do koalescencji zdarzeń.
  Zarejestrowany w `Program.cs` jako kolejny `IHostedService`.

- ~~NIP checksum.~~ **Zamknięte (2026-09-05).** Zarówno `SubjectId` (throw `ArgumentException`
  z "invalid checksum (I-13)") jak i `ConfigLoader.ValidateSubject` (błąd
  `subjects[i].nip: invalid checksum (I-13).`) sprawdzają teraz sumę kontrolną NIP-u (10 cyfr,
  wagi 6,5,7,2,3,4,5,6,7 mod 11, checksum==10 → zawsze nieprawidłowy) — algorytm zduplikowany
  w obu miejscach celowo (`InvoiceWatching` i `SubjectConfiguration` nie mają wspólnej referencji
  projektowej, więc nowy "shared kernel" tylko dla 10 linii kodu byłby przedwczesną abstrakcją).
  Wymagało to zmiany fixture'ów testowych w ~18 plikach: placeholder `"1234567890"` **nie
  przechodzi** sumy kontrolnej, więc wszędzie tam, gdzie reprezentował poprawny NIP, zamieniono go
  na `"5260001246"` (realny, publicznie znany przykładowy poprawny NIP); powtarzające się cyfry
  (`"1111111111"`, `"2222222222"`, `"9999999999"`) zostały bez zmian — z tego akurat algorytmu
  wychodzą jako poprawne (suma wag mod 11 = 1, więc `d*1 mod 11 = d` zawsze zgadza się z ostatnią
  cyfrą). Po drodze złapano i poprawiono jeden fałszywy-zielony test (dobór "123456789" jako
  przypadku "za krótki NIP" przypadkiem trafiał w ten sam modulus==10, który i tak zawsze jest
  nieprawidłowy, więc test przechodził dzięki `&&` short-circuit, nie dzięki sprawdzeniu długości).

- ~~Okna zapytań KSeF > 100 dni.~~ **Zamknięte (2026-09-05).** Zgodnie z
  `docs/08_invoice_watching_value_objects.md` ("splitting... is the provider's/Cycle's mechanical
  concern, not the VO's") — `FetchWindow` sam w sobie zostaje bez zmian (nie waliduje/nie odrzuca
  długich okien, to nadal poprawny stan domenowy po długim przestoju), a dzielenie dzieje się
  wewnątrz `KsefAccessService.FetchWindowedListAsync`: nowa `SplitIntoSubWindows` tnie na kawałki
  ≤100 dni, dla każdego kawałka odpala istniejącą już pętlę paginacji (z resetem `pageOffset` do
  zera per pod-okno), agreguje wykryte faktury ze wszystkich pod-okien, i bierze HWM z
  *ostatniego* (najpóźniejszego) pod-okna jako wynikowy. Okno ≤100 dni (typowy przypadek co pollu)
  produkuje dokładnie jedno pod-okno identyczne z oryginałem — zero zmiany zachowania w typowym
  przypadku, potwierdzone przez pozostanie zielonym wszystkich istniejących testów bez zmian.
  Nie dodano deduplikacji na granicy pod-okien (potencjalny rzadki duplikat przy fakturze
  dokładnie na granicy czasowej) — README już wprost toleruje duplikaty ("a duplicate is
  acceptable, a loss is not"), więc dodatkowa złożoność nie była uzasadniona.

## Świadome uproszczenia V1 (nie bugi, ale warto pamiętać)

- **Honorowanie `Retry-After` przy 429.** `KsefRateLimitedException`/`PollFailure.RateLimited`
  niosą `RetryAfter`, ale nic go dziś nie odczytuje — kolejna próba to zwykły, następny
  zaplanowany poll (per `docs/08_ksef_access_tactical_model.md`: "the interval bound... is the
  enforcement mechanism... if 429s appear in practice, handle Retry-After and revisit").
- **`RetryBackoffs[2]` (60s) w `PollCycle` jest strukturalnie nieosiągalny** przy `MaxAttempts=3`
  (3 próby dają tylko 2 odstępy: 5s, 20s). Opisane komentarzem w kodzie, nigdy niepotwierdzone
  z użytkownikiem czy zamysł był inny (np. 4 próby). Do wyjaśnienia, jeśli kiedyś ma znaczenie.
- **Wendorowany `ksef-client-csharp` śledzi `main`, nie konkretny commit/tag.** Obecnie
  zaklonowany na commicie `04f01c1c7834336a3aef1804149cd5bcbd883a3e` (2026-08-27), ale
  `vendor/README.md`'s polecenie klonujące tego nie pinuje — kolejny `git clone` może pobrać
  inną wersję. `docs/09_integration_contracts.md` wprost zaleca pinowanie ("bump deliberately").
  **Do zrobienia:** dodać `git checkout <commit>` do `vendor/README.md`'s instrukcji.

## Do zweryfikowania przed realnym użyciem

- **Nigdy nie przetestowano pełnego cyklu pollowania przeciw prawdziwemu KSeF sandboxowi**
  (autentykacja + realne zapytanie o listę faktur) — smoke test potwierdził tylko start aplikacji
  i warmup `CryptographyService` (realne 200 z `api-test.ksef.mf.gov.pl`). Do tego potrzebny
  prawdziwy token KSeF wygenerowany w sandboxie.
- **Poziomy logowania frameworka są głośne domyślnie** (smoke test pokazał `info:`-owe logi
  `System.Net.Http.HttpClient` na każde żądanie) — do rozważenia `appsettings.json` z
  przyciszeniem kategorii przed produkcyjnym użyciem.
- **Obserwowany jednorazowy flaky test w `KsefWatcher.SubjectConfiguration.Tests` pod `make test`
  na całym rozwiązaniu** (2026-09-05) — `YamlDotNet.Serialization.ObjectFactories.DefaultObjectFactory`
  wyjątek w trakcie deserializacji (`Dictionary.set_Item`/`GetStateMethods`), najpewniej race w
  YamlDotNet-owym cache'u refleksji przy równoległym uruchamianiu projektów testowych przez
  `dotnet test` na całej solucji. Nie odtworzyło się przy izolowanym uruchomieniu tego projektu
  ani przy drugim pełnym `make test`. Nie prześledzone głębiej — jeśli wróci i zacznie się
  powtarzać, sprawdzić `[CollectionDefinition(DisableParallelization = true)]` dla tego projektu.

## Poza zakresem tej pracy (nietykane, brak decyzji czy w ogóle potrzebne)

- Packaging/wdrożenie: plik jednostki systemd (`A6` zakłada systemd, ale nie ma `.service`).
- CI (np. GitHub Actions uruchamiający `make test` na push/PR) — 109 testów dziś uruchamia się
  tylko ręcznie przez `make test`.
- Health-check/metryki (Prometheus czy podobne) — nigdzie niewspomniane w dokumentacji jako
  wymaganie, ale typowe dla "runs unattended for months" (PG-3).
- Główny `README.md` repo (nie `docs/README.md`) — nieaktualizowany pod kątem nowego
  `Makefile`/`vendor/`-owego flow budowania dla kogoś, kto klonuje projekt od zera.
