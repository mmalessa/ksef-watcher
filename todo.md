## Zamknięte (2026-09-05)

- `databasePath` w config.yaml — opcjonalna ścieżka do `state.db`, konfigurowalna zamiast na sztywno obok config.yaml.
- Channel `type: logs` — notyfikacje idą w logi zamiast na komunikator (do testów/developerki), bez wymaganego tokenu/channelId.
- Autoryzacja Discorda przez bota: `channels[].token` (bot token) + `channels[].channelId` zastąpiły `webhookUrl`; oba pola wspierają `${VAR}` tak jak `ksefToken` (np. `${DISCORD_TOKEN}` / `${DISCORD_CHANNEL}`).
- Diagnoza "faktura w KSeF, ale nic się nie dzieje": to był pierwszy poll dla subjecta (I-18 baseline) — po cichu nie wysyła nic. Dodano `ksef-watcher --config <path> --reset-hwm <nip>` do zapominania stanu subjecta na potrzeby testów.
- Dodano logowanie w `PollingBackgroundService`/`PollCycle` (przez nowy `PollOutcome`): czy to baseline, ile faktur pobrano, ile nowych, ile powiadomień wysłano, kiedy HWM się przesuwa — wcześniej `PollCycle` nic nie logował.
- `--help`/`-h` w pliku binarnym — wypisuje usage i dostępne opcje, exit 0, nie wymaga obecności config.yaml.
