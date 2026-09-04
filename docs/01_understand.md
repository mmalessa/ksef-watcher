# Step 1 — Understand

**Status:** ✅ reviewed & accepted (steps 1–7 pass)

## Project vision

**Problem.** Polish companies receive invoices through KSeF (Krajowy System e-Faktur, the National e-Invoice System). Invoices land in the company's KSeF inbox regardless of whether anyone is actively watching. In practice, people learn about a new invoice only when they log in manually — days late. This delays booking, payment runs, and cash-flow awareness.

**Solution.** A self-hosted, open-source Linux daemon that periodically checks the simplified list of invoices received by each configured subject, detects newly arrived invoices, and pushes a notification to an internet messenger (Discord first, more notifiers later). The user's mental model: *"tell me about every invoice the moment it appears, without me checking KSeF."*

## Users & actors

| Actor | Type | Description |
|-------|------|-------------|
| Subject owner / accountant | human (primary) | Configures subjects (company NIPs), receives notifications in a messenger; acts on invoices (booking, payment). |
| Daemon operator | human (primary; often same person) | Runs the daemon on a server (systemd), maintains the config file, monitors liveness. |
| Notified channel members | human (indirect) | People in the Discord channel/team who see notifications without configuring anything. |
| KSeF (MF) | external system | Authoritative source of received invoices; exposes session-based API 2.0. |
| Messengers (Discord, …) | external system | Receive notification payloads via their APIs/webhooks. |

## Product goals

1. **PG-1 Timeliness:** a new invoice in KSeF is notified within roughly one polling interval (per-subject configurable, in minutes; default 60 min per A7).
2. **PG-2 Reliability over silence:** a lost notification is unacceptable; a duplicate notification is acceptable.
3. **PG-3 Zero-effort operation:** config-file driven with hot reload, runs unattended for months (systemd daemon).
4. **PG-4 Extensibility of channels:** adding a notifier (Slack, Teams, e-mail…) must not touch domain logic.

## Design assumptions

- **A1** KSeF API 2.0 is the single source of truth for "invoices received"; the daemon never parses invoice XML (FA(3)) — only the simplified list.
- **A2** A subject's notification target is a channel (Discord channel/webhook), not an individual person.
- **A3** State is local and minimal: a per-subject registry of already-notified invoice references (cursor semantics).
- **A4** Sending first, marking later — at-least-once notifications (duplicates OK, losses not). *(confirmed — OQ-4 resolved: on delivery failure, do NOT mark as notified; retry with backoff until confirmed)*
- **A5** Config is an operator-edited file, **hot-reloaded** — the daemon watches the file and applies changes without restart (OQ-3, resolved). Invalid file at reload ⇒ keep last valid config and log loudly; fail-fast applies only at startup.
- **A6** One daemon process serves all configured subjects (no per-subject processes).
- **A7** KSeF rate limits — **verified against official docs** (KSeF API 2.0, *Limity żądań API*, CIRFMF/ksef-api, 2025-11-22): limits are counted **per pair (context + IP)** — each subject (NIP context) has its own budget. Our poll's bottleneck, the simplified-list endpoint (`POST /invoices/query/metadata`): **8 req/s · 16 req/min · 20 req/h**; auth/session endpoints: 10 req/s · 30 req/min · **120 req/h**. Consequences: the poll budget is **per subject, not global**; MF recommends a polling interval **≥ 15 minutes** (hard validation bound — I-13a); default 60 min kept.
- **A8** KSeF session lifecycle: **fresh session per poll** — open, fetch, close. No long-lived sessions, no renewal logic (OQ-2, resolved; at hourly cadence reuse is worthless and expiry-mid-poll disappears by design).
- **A9** Poll spreading: per-subject polling is **offset deterministically within the interval window** — offset derived from subject identity (e.g. `hash(NIP) mod interval`), stable across restarts and hot reloads; a subject's first poll fires at `boot + offset`, not immediately at boot. Rationale (softened after verifying A7): with per-subject budgets this is *politeness and load smoothing*, not a hard survival requirement — it still avoids bursting shared per-IP public endpoints (e.g. `/auth/challenge`, 60 req/s per IP) and keeps the daemon's own outbound traffic even.
- **A10** Implementation stack: **C# (.NET)**, built on the **official KSeF client** `CIRFMF/ksef-client-csharp` (KSeF Access will wrap it as its ACL). *(operator decision, `prompt.md`)*
- **A11** KSeF authentication: **token generated in the KSeF system** (+ NIP where required) — not certificates. *(operator decision; details of the token flow belong to KSeF Access / Step 8–9)*
- **A12** Config format: **YAML** (`config.yaml`), searched in order: (1) the binary's directory (`./config.yaml`), (2) `/etc/ksef-watcher/config.yaml`. *(operator decision — resolves OQ-14)*
- **A13** Test environments: KSeF sandbox endpoints (`api-test.ksef.mf.gov.pl`, `api-demo.ksef.mf.gov.pl`) are for integration testing only — never test against production. *(operator constraint)*

## Project values

- **Simplicity** — minimal state, plain config file, single binary feel.
- **Reliability** — catch-up after downtime, never silently lose an invoice notification.
- **Openness** — open source, self-hosted, easy to audit (credentials stay on the operator's machine).

## Long-term vision

The watcher remains, permanently, a **single-purpose notification tool**: tell the subject's channel that a new invoice arrived in KSeF. It will never manage invoices — no viewing, downloading, parsing, booking, archiving, payments, no invoice-management UI/API. This is a product boundary, not a backlog item ("out of scope" here means *not ever*, not "maybe later"). The only intended growth dimensions: more notifier channels (community-contributed adapters are the natural form of growth), more subjects per daemon, and robustness of the single flow. The data surface is fixed to the KSeF simplified list — no per-invoice enrichment calls (OQ-1/OQ-10, resolved).

## Open questions

All Step-1 open questions are resolved (OQ-1, OQ-2, OQ-3, OQ-4). Remaining open questions live in the step-7 context canvases (OQ-5 … OQ-15). KSeF rate limits are now **verified** (A7 — official *Limity żądań API* doc); the per-subject poll budget (I-21) inherits that verification. Note: MF states limits are **dynamic** and may be adjusted — re-verify if 429s appear despite compliant intervals.