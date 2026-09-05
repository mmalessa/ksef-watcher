# KSeF Watcher

A self-hosted, open-source Linux daemon that watches the KSeF (Krajowy System e-Faktur — the Polish National e-Invoice System) inbox of configured companies and pushes a notification to an internet messenger the moment a new invoice arrives — so nobody has to log in and check manually.

> Tell me about every invoice the moment it appears, without me checking KSeF.

## What it does

- Periodically polls the **simplified list of invoices received** (KSeF API 2.0) for each configured subject, on a per-subject interval.
- Detects new invoices (HWM-cursor window fetch + registry diff — no invoice is ever missed).
- Sends a notification with the invoice essentials: KSeF reference number, issuer invoice number, gross amount, issuer NIP (one message per invoice).
- **Discord first**, more messengers later — notifiers are pluggable and community contributions are the natural way to grow.

## Design principles

- **Notification-only, permanently.** It will never manage invoices — no viewing, parsing, booking or payments. It is a doorbell, not an ERP.
- **Never lose a notification** (at-least-once delivery: a duplicate is acceptable, a loss is not). Downtime is handled by catch-up from the persisted cursor.
- **Zero-effort operation.** Plain YAML config file with hot reload; runs unattended as a systemd service.
- **Respects the KSeF API.** Rate limits, minimum polling interval, the official incremental-retrieval pattern.

## Example configuration (indicative — schema not final yet)

```yaml
subjects:
  - nip: "1234567890"
    intervalMinutes: 60        # per subject, min 15 (MF recommendation)
    ksefToken: "..."           # token generated in KSeF
    environment: test          # or prod
    channels:
      - type: discord
        webhookUrl: "https://discord.com/api/webhooks/..."
```

The config file is searched in the binary's directory (`./config.yaml`) and then in `/etc/ksef-watcher/config.yaml`. A fully-commented starting point is at [`config.yaml.dist`](config.yaml.dist) — copy it to `config.yaml` and fill in your subjects.

## Building

Requires only Docker — builds and tests run inside the pinned .NET 8 SDK image via the `Makefile`,
no local .NET SDK needed:

```sh
make init    # one-time: fetches the vendored KSeF client at its pinned commit and patches it
make build
make test
```

`make init` is required before the first build — the official KSeF client (`ksef-client-csharp`)
is vendored rather than pulled from NuGet (see `vendor/README.md` for why) and isn't committed to
this repo.

### Producing a deployable binary

```sh
make publish
```

Produces a single self-contained executable at `./bin/ksef-watcher` — no .NET runtime needed on
the target machine, nothing else to copy alongside it. Defaults to `linux-x64`; cross-compile for
another target with `make publish PUBLISH_RID=linux-arm64`. Drop it wherever `config.yaml` will
live (see above) and run it directly.

## Status

**Early design phase** — the domain is being modelled with a lightweight DDD process before implementation starts. Read the [design documentation](docs/README.md) (bounded contexts, context map, domain events, invariants and decisions log) to see where it is heading.

Stack (decided): **C# / .NET**, built on the [official KSeF client](https://github.com/CIRFMF/ksef-client-csharp).

## Documentation

- [DDD modelling roadmap & decisions log](docs/README.md)
- [Step 1 — Understand (vision, goals, assumptions)](docs/01_understand.md)
- [Step 7 — Bounded contexts & context map](docs/07_define_context_map.md)