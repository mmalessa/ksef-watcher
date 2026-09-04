# Step 8g — Code: Subject Configuration — Tactical Design (deliberately none)

**Status:** ✅ reviewed & accepted

**Decision: no tactical model for this context.** Subject Configuration is Generic plumbing (Step 4): a file format, a schema and validation rules. It has no domain behaviour, no invariants over time, no aggregates, no domain events — its entire "domain" is the **config file schema** (Published Language, Step 7), which belongs to implementation:

- **Schema + types** (`ConfigFile`, `SubjectConfig {Nip, IntervalMinutes, KsefToken, Environment, Channels, AmountDisplay}`, `ChannelConfig`) → Step 9 (parsing, binding, defaults).
- **Validation rules** (I-13, I-13a ≥ 15 min, I-14 no-secret-logging, I-15 schema version) → Step 9 (a validator component; pure functions over the parsed file).
- **Hot reload machinery** (file watcher, I-16 keep-last-valid, I-17 atomic reload) → Step 9 (runtime component).

This mirrors the Step-6 acceptance criteria: configuration must stay a *parameter source* other contexts read — if it ever "needs" an aggregate or a domain service, that is a signal the context grew domain logic it must not have (Step 4: "never let it grow domain logic").

What **does** get fixed here, tactically, is the schema sketch (indicative — final names in Step 9):

```yaml
version: 1                        # I-15: explicit schema version
defaultEnvironment: test          # OQ-9: file-level default (safe); per-subject override below
subjects:
  - nip: "1234567890"
    intervalMinutes: 60           # I-13a: >= 15
    ksefToken: "..."              # I-14: never logged
    environment: test             # OQ-9: optional per-subject override (test | prod)
    amountDisplay: brutto         # OQ-16: brutto | netto (default brutto)
    channels:
      - type: discord
        webhookUrl: "https://..."
```

- `subjects[].nip` is the `SubjectId` source (also: poll-offset derivation A9, per-subject rate budget I-21).
- `intervalMinutes` → `Interval` (in minutes, default 60 — OQ-19).
- `amountDisplay` per subject (OQ-16); V1 assumes exactly **one** channel per subject (OQ-12 placeholder for fan-out).
- Search paths (A12): `./config.yaml` → `/etc/ksef-watcher/config.yaml`.