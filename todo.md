# TODO

Wyniki porządnego code review (2026-09-05, poziom `medium`) — każde zgłoszenie zweryfikowane
ręcznie w kodzie przed zapisaniem tutaj, nie tylko przyjęte z raportu narzędzia. Pogrupowane wg
powagi.

## Krytyczne

- ~~Nieprawidłowy YAML podczas hot-reloadu wywala cały daemon.~~ **Zamknięte (2026-09-05).**
  `ConfigLoader.Load` łapie teraz `YamlDotNet.Core.YamlException` wokół
  `Deserializer.Deserialize<ConfigFile>(yaml)` i zwraca `ConfigLoadResult.Failure(["yaml: could
  not be parsed (...)"])` zamiast pozwalać wyjątkowi uciec — składniowo zepsuty YAML przechodzi
  teraz przez ten sam, już istniejący, ścieżkę `ConfigWatcher.Reload`'s `Failure` (log Error,
  `ReloadRejected`, zachowanie ostatniego poprawnego configu, I-16). Zweryfikowane na trzech
  poziomach: `ConfigLoader.Load` bezpośrednio, `ConfigWatcher.Reload` (żeby potwierdzić, że
  istniejąca obsługa `Failure` faktycznie łapie ten przypadek), i `ConfigFileWatcher.HandleFileChanged`
  (rzeczywista ścieżka zgłoszonego problemu — realny plik na dysku z literówką w YAML podczas
  edycji na żywo).

- ~~Pole `environment` w configu nigdy nie jest walidowane.~~ **Zamknięte (2026-09-05).**
  `ConfigLoader.ValidateSubject` sprawdza teraz `subject.Environment` (po dziedziczeniu z
  `DefaultEnvironment`, więc literówka w `defaultEnvironment` na poziomie pliku też zostaje
  złapana) względem nowej listy `SupportedEnvironments` (`test`/`demo`/`prod`,
  case-insensitive — dopasowane do tolerancji `KsefClientAdapter.ParseEnvironment`, żeby nie
  zacząć odrzucać configów, które dotąd działały poprawnie przez np. `environment: PROD`).
  Zweryfikowano case-insensitivity przez chwilowe jej wyłączenie i potwierdzenie prawdziwego RED
  — nie był to przypadkowy zielony test.

## Ważne

- ~~Rate limit przy otwieraniu sesji omija klasyfikację I-8.~~ **Zamknięte (2026-09-05).**
  `client.OpenSessionAsync(...)` w `KsefAccessService.FetchWindowedListAsync` jest teraz wewnątrz
  try/catch analogicznego do tego przy zapytaniu o strony — `KsefRateLimitedException` z etapu
  otwierania sesji dostaje ten sam log Warning i `PollFailureException(PollFailure.RateLimited)`.
  Duplikację logiki (log + budowa wyjątku, teraz w dwóch miejscach) wydzielono do
  `LogAndBuildRateLimitedFailure`. Sesja (`session`) stała się `KsefSession?` — `finally` zamyka
  ją tylko jeśli faktycznie została otwarta (rate limit na etapie otwierania nie ma czego
  zamykać). Nowe testy: klasyfikacja + zachowany `RetryAfter`, log Warning, i potwierdzenie że
  `CloseSessionAsync` NIE jest wołane gdy sesja nigdy nie powstała.

- ~~`PollFailure.AuthFailure` nigdy nie jest konstruowany.~~ **Zamknięte (2026-09-05, częściowo —
  patrz `Network` niżej.)** Nowy `KsefAuthFailedException` (`KsefWatcher.KsefAccess`) — w
  `KsefClientAdapter` łapane jest teraz `KSeF.Client.Core.Exceptions.KsefApiException` z
  `StatusCode` 401 (Unauthorized) lub 403 (Forbidden) w obu miejscach (`OpenSessionAsync`,
  `QueryReceivedInvoicesAsync`; oba HTTP-kody mapują się na `KsefApiException` w bibliotece
  wendorowanej, zweryfikowane w źródle — nie ma osobnego typu per status). `KsefAccessService`
  łapie ten wyjątek symetrycznie w obu miejscach, loguje Error (zgodnie z tabelą logowania w
  `docs/09_architecture.md` i OQ-18 "every poll re-classifies and logs loudly") i rzuca
  `PollFailureException(PollFailure.AuthFailure())`.
  **`PollFailure.Network` świadomie pominięty w tym przejściu** — w przeciwieństwie do `AuthFailure`
  (dobrze zdefiniowany: konkretny status HTTP), klasyfikacja "network" wymagałaby łapania
  szerokiego, rozmytego zbioru wyjątków transportowych (`HttpRequestException`, wygasły
  `TaskCanceledException` z timeoutu itd.) — a to ostatnie wymaga starannego odróżnienia od
  legalnego anulowania przez `cancellationToken` przy zamykaniu hosta, żeby nie zaklasyfikować
  zwykłego shutdownu jako awarii sieci. Nie chciałem tego robić pod presją, bez osobnego
  namysłu — zostawione jako osobna, mniejsza pozycja.
  **Do zrobienia (Network):** zdefiniować dokładnie, które wyjątki liczą się jako "Network",
  z uwzględnieniem `cancellationToken.IsCancellationRequested` jako wyjątku od klasyfikacji.

- ~~Brak zabezpieczenia przed nakładającymi się cyklami pollowania dla tego samego subjecta.~~
  **Zamknięte (2026-09-05).** Nowa `InFlightGate` (`KsefWatcher.Host.Scheduling`) — cienki wrapper
  nad `ConcurrentDictionary<string, byte>.TryAdd`/`TryRemove`, per-key "w trakcie" guard.
  `PollingBackgroundService.Fire` woła `TryEnter(nip)` przed odpaleniem cyklu; jeśli poprzedni
  cykl dla tego subjecta jeszcze trwa, cichy skip zamieniono na `LogWarning` i cichy powrót —
  następny zaplanowany tick i tak go podejmie. `RunPollSafelyAsync`'s `finally` woła `Exit(nip)`,
  więc zwolnienie następuje niezależnie od sukcesu/porażki cyklu. Sama logika `InFlightGate` w
  pełni pokryta 4 testami jednostkowymi (pierwsze wejście, blokada przy powtórnym, niezależność
  różnych kluczy, zwolnienie po `Exit`); okablowanie w `PollingBackgroundService` to cienki klej
  (3 linijki) zweryfikowany przeglądem kodu, nie osobnym testem — analogicznie do
  `ConfigFileWatcher`'s realnego `FileSystemWatcher`.

## Do rozważenia (niższy priorytet)

- ~~`SubjectWatch.AdvanceHwm` nie sprawdza monotoniczności HWM.~~ **Zamknięte (2026-09-05).**
  `AdvanceHwm` rzuca teraz `InvalidOperationException` gdy nowy HWM jest wcześniejszy niż bieżący
  `LastHwm` (ścisłe `<`, nie `<=` — równy HWM jest dozwolony, np. puste okno bez postępu w czasie).
  Guard pomijany gdy `LastHwm is null` (brak wcześniejszego HWM do naruszenia — teoretyczny
  przypadek, w praktyce niedostępny przez normalny przepływ `PollCycle`). Granicę `<` vs `<=`
  zweryfikowano przez chwilową zamianę i potwierdzenie prawdziwego RED — nie przypadkowy zielony
  test. Wyjątek propaguje przez `PollCycle` do generycznego catch-alla w
  `PollingBackgroundService` (Error log) — cykl przerywa się bez przesunięcia kursora, następny
  poll ponawia to samo okno (bezpieczne wg I-23).

- ~~`DiscordNotifier` zarejestrowany jako typed `HttpClient`, ale przypięty jako singleton.~~
  **Zamknięte (2026-09-05).** `DiscordNotifier` przyjmuje teraz `IHttpClientFactory` zamiast
  wstrzykniętego `HttpClient` i woła `CreateClient(nameof(DiscordNotifier))` **przy każdym
  wywołaniu `SendAsync`**, nie raz przy konstrukcji — mimo że `DiscordNotifier`/`IChannelSender`
  nadal jest singletonem (nic nie stoi na przeszkodzie), `IHttpClientFactory` faktycznie dostaje
  szansę rotować handler między wywołaniami, tak jak został do tego zaprojektowany. `Program.cs`:
  `AddHttpClient<DiscordNotifier>()` (typed client) zamieniony na zwykły nazwany
  `AddHttpClient(nameof(DiscordNotifier))`. Nowy test potwierdza dwa niezależne wywołania
  `CreateClient` dla dwóch wywołań `SendAsync` (nie cache'owane między nimi) — pozostałe 4
  istniejące testy zaadaptowane do nowego kształtu konstruktora bez zmiany asercji.
