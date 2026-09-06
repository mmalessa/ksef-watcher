# KSeF Watcher

A self-hosted, open-source Linux daemon that watches the KSeF (Krajowy System e-Faktur — the Polish National e-Invoice System) inbox of configured companies and pushes a notification to an internet messenger the moment a new invoice arrives — so nobody has to log in and check manually.

> Tell me about every invoice the moment it appears, without me checking KSeF.

## What it does

- Periodically polls the **simplified list of invoices received** (KSeF API 2.0) for each configured subject, on a shared interval (with a per-subject offset so they don't all poll at once).
- Detects new invoices (HWM-cursor window fetch + registry diff — no invoice is ever missed).
- Sends a notification with the invoice essentials: KSeF reference number, issuer invoice number, gross amount, issuer NIP (one message per invoice).
- **Discord first**, more messengers later — notifiers are pluggable and community contributions are the natural way to grow.

## Design principles

- **Notification-only, permanently.** It will never manage invoices — no viewing, parsing, booking or payments. It is a doorbell, not an ERP.
- **Never lose a notification** (at-least-once delivery: a duplicate is acceptable, a loss is not). Downtime is handled by catch-up from the persisted cursor.
- **Zero-effort operation.** Plain YAML config file with hot reload; runs unattended as a systemd service.
- **Respects the KSeF API.** Rate limits, minimum polling interval, the official incremental-retrieval pattern.

## Example configuration

```yaml
version: 1
environment: test            # test | demo | prod — one environment for the whole daemon
intervalMinutes: 60          # shared by every subject, min 15 (MF recommendation)
# databasePath: /var/lib/ksef-watcher/state.db  # optional, defaults to state.db next to this file
subjects:
  - nip: "1234567890"
    intervalOffset: 0        # minutes into the shared window this subject polls at
    ksefToken: "..."         # token generated in KSeF
    channels:
      - type: discord        # or "logs" — writes to the daemon's log, handy while testing
        webhookUrl: "${DISCORD_WEBHOOK_URL}"   # or token + channelId (bot); webhook takes priority if both are set
```

The config file defaults to `/etc/ksef-watcher/config.yaml`; pass `--config <path>` to use a
different location. A fully-commented starting point is at [`config.yaml.dist`](config.yaml.dist)
— copy it and fill in your subjects.

A subject's first-ever poll only establishes its HWM cursor and sends nothing (any invoice already
in KSeF at that point is by design never notified — only invoices arriving afterwards are). To make
a subject re-run that first poll (e.g. while testing), forget its state with:

```sh
ksef-watcher --config <path> --reset-hwm <nip>
```

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
another target with `make publish PUBLISH_RID=linux-arm64`. Run it with `--config <path>` or drop
a `config.yaml` at `/etc/ksef-watcher/config.yaml` (see above).

## Status

**Early design phase** — the domain is being modelled with a lightweight DDD process before implementation starts. Read the [design documentation](docs/README.md) (bounded contexts, context map, domain events, invariants and decisions log) to see where it is heading.

Stack (decided): **C# / .NET**, built on the [official KSeF client](https://github.com/CIRFMF/ksef-client-csharp).

## Ubuntu notes
- create user `ksef-watcher`
- copy `bin/ksef-watcher` to `/opt/ksef-watcher/bin/ksef-watcher` (and set owner to ksef-watcher)
- copy (and modify) `config.yaml.dist` to `/etc/ksef-watcher/config.yaml`  (and set owner to ksef-watcher)
- create directory `/var/lib/ksef-watcher`  (and set owner to ksef-watcher)
- copy `ubuntu/ksef-watcher.service` to `/etc/systemd/system/ksef-watcher.service`
- run `sudo systemctl daemon-reload`
- run `sudo systemctl enable ksef-watcher.service`
- run `sudo systemctl start ksef-watcher.service`
- check `sudo systemctl status ksef-watcher.service` and logs: `journalctl -u ksef-watcher.service -b -f`

## Documentation

- [DDD modelling roadmap & decisions log](docs/README.md)
- [Step 1 — Understand (vision, goals, assumptions)](docs/01_understand.md)
- [Step 7 — Bounded contexts & context map](docs/07_define_context_map.md)