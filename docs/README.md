# KSeF Watcher — DDD Modelling Roadmap

Lightweight pass over [DDD Starter Modelling Process](https://github.com/ddd-crew/ddd-starter-modelling-process) (steps 1–7, light) for **ksef-watcher**: a Linux daemon that periodically checks the simplified list of invoices received by configured subjects (companies) via KSeF (Polish National e-Invoice System, API 2.0) and sends a notification when a new invoice arrives (notifier interface, Discord first).

## Working agreement

- Status ≠ "written". A step is `✅` only after explicit user review & acceptance.
- The process is non-linear: later steps may reopen earlier ones.
- Uncertain ⇒ Open Question in the document, not a guess.
- Artifact language: **English** (conversation may be Polish).
- Scope of this pass: steps 1–7 (light). Tactical design (8) and architecture (9) come later.

## Status legend

`⏸` not started · `⏳` drafted · `✅` reviewed & accepted · `⚠️` substantially complete, open questions left

## Steps

| # | Step | Key question | Artifact(s) | Status |
|---|------|--------------|-------------|--------|
| 1 | Understand | Why does this exist and for whom? | [01_understand.md](01_understand.md) | ⏳ |
| 2 | Discover | Which events and processes form the domain? | [02_discover_big_picture.md](02_discover_big_picture.md) · [02_discover_process_level.md](02_discover_process_level.md) | ⏳ |
| 3 | Decompose | Where are the natural seams? | [03_decompose_subdomains.md](03_decompose_subdomains.md) | ⏳ |
| 4 | Strategize | What differentiates, what is plumbing? | [04_strategize_core_domain_chart.md](04_strategize_core_domain_chart.md) | ⏳ |
| 5 | Connect | How do scenarios flow across boundaries? | [05_connect_message_flows.md](05_connect_message_flows.md) | ⏳ |
| 6 | Organise | Which team would own each context? | [06_organise.md](06_organise.md) | ⏳ |
| 7 | Define | What is each context responsible for? | [07_define_context_map.md](07_define_context_map.md) · [07_define_invoice_watching.md](07_define_invoice_watching.md) · [07_define_notification_delivery.md](07_define_notification_delivery.md) · [07_define_ksef_access.md](07_define_ksef_access.md) · [07_define_subject_configuration.md](07_define_subject_configuration.md) | ⏳ |
| 8 | Code | Aggregates, entities, events inside contexts? | — | ⏸ |
| 9 | Architecture | How does the model map to implementation? | — | ⏸ |

## Decisions log (this pass)

| Decision | Value |
|----------|-------|
| Artifact language | English |
| Docs directory | `./docs` |
| Scope | Steps 1–7, light ("simplified big picture") |
| Product shape | Open source, multi-tenant (multiple subjects per daemon), self-hosted |
| Product boundary | Notification-only — never invoice management (viewing/parsing/booking/payments) |
| Data source | KSeF 2.0 simplified list of received invoices |
| Notification content | KSeF reference number, issuer invoice number, gross amount, issuer NIP (+ issuer name iff the simplified list returns it); **no per-invoice API calls** (OQ-1/OQ-10 resolved) |
| Persisted state | Per subject: registry of already-notified invoice refs + `lastHwm` HWM cursor — minimal (A3, I-23) |
| Downtime behaviour | Catch-up from cursor (`lastHwm` persists; the first poll simply fetches a wider window) |
| Configuration | Config file with **hot reload**: auto-detected file change, validate, apply without restart (OQ-3 resolved) |
| Hot reload safety | Invalid reload ⇒ keep last valid config + loud log (I-16); fail-fast only at startup (I-13) |
| Subject onboarding | Baseline: first poll marks current list as notified, no historical flood (I-18) |
| Subject removal | Registry retained (dormant); re-add resumes, no baseline re-run (I-19, OQ-15) |
| Polling interval | Per subject, **in minutes**, default 60 (OQ-2 resolved) |
| KSeF rate limits | **Verified** (official *Limity żądań API*): per pair (context + IP); simplified list 8 req/s · 16 req/min · 20 req/h (bottleneck); session endpoints 120 req/h |
| Min interval | **≥ 15 min per subject** (MF recommendation, verified) — hard validation bound (I-13a); default 60 min |
| Poll spreading | **Deterministic poll offset per subject** (`hash(NIP) mod interval`) — load smoothing/politeness (A9); first poll at boot + offset |
| Poll budget | **Per subject** (each NIP context has own 20 req/h), **no global fleet cap** (I-21, corrected after docs verification) |
| KSeF session | **Fresh session per poll** — open, fetch, close; no reuse/renewal (OQ-2 resolved, A8) |
| Delivery semantics | At-least-once (send-before-mark; duplicate notification acceptable, lost is not) — A4/OQ-4 resolved: on failure NOT marked notified, retry with backoff |
| Notification form | **One message per invoice** — no digest mode, normal polls and catch-up uniform (OQ-6 resolved, I-22) |
| Silent-daemon risk | Open: watchdog heartbeat ("no new invoices" daily, absence = alarm) OQ-7a vs permanent-failure escalation OQ-7b — candidate direction: heartbeat |
| Detection mechanism | **HWM-cursor window-fetch + registry-diff** (OQ-5 resolved, I-23): window `From = lastHwm → now` in snapshot mode, paginate all pages, dedupe by `KsefNumber` vs notified-refs registry; `lastHwm` advances only when the whole window is notified (send-before-mark applies to the cursor, not just the registry) — pattern from official C# client's incremental-retrieval E2E |
| KSeF query API facts | `SubjectType=Subject2` = received invoices; date window ≤ 100 days UTC; pageSize 10–250; `HasMore`/`IsTruncated` (10k/query, `LastPermanentStorageDate` continuation); `PermanentStorageHwmDate` in response; HWM minimizes but does not eliminate duplicates (registry stays) |
| Baseline mechanics (I-18, updated for HWM) | New subject (`lastHwm = null`): one narrow fetch, no notifications, `lastHwm` set — registry not populated by baseline; only post-onboarding arrivals are notified |
| Implementation stack | **C# (.NET)** on the official `CIRFMF/ksef-client-csharp` client (KSeF Access wraps it as ACL) — A10 |
| KSeF auth | Token generated in KSeF (+ NIP where required), not certificates — A11 |
| Config format | **YAML** `config.yaml`; search paths: binary dir, then `/etc/ksef-watcher/` (A12; OQ-14 resolved) |
| Test environments | KSeF sandboxes only (`api-test`, `api-demo`), never production — A13 |