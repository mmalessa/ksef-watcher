# KSeF Watcher — DDD Modelling Roadmap

Pass over [DDD Starter Modelling Process](https://github.com/ddd-crew/ddd-starter-modelling-process) (steps 1–7, light, plus full tactical design in step 8) for **ksef-watcher**: a Linux daemon that periodically checks the simplified list of invoices received by configured subjects (companies) via KSeF (Polish National e-Invoice System, API 2.0) and sends a notification when a new invoice arrives (notifier interface, Discord first).

## Working agreement

- Status ≠ "written". A step is `✅` only after explicit user review & acceptance.
- The process is non-linear: later steps may reopen earlier ones.
- Uncertain ⇒ Open Question in the document, not a guess.
- Artifact language: **English** (conversation may be Polish).
- Scope of this pass: steps 1–7 (light), plus full tactical design (step 8). Architecture (9) comes later.

## Status legend

`⏸` not started · `⏳` drafted · `✅` reviewed & accepted · `⚠️` substantially complete, open questions left

## Steps

| # | Step | Key question | Artifact(s) | Status |
|---|------|--------------|-------------|--------|
| 1 | Understand | Why does this exist and for whom? | [01_understand.md](01_understand.md) | ✅ |
| 2 | Discover | Which events and processes form the domain? | [02_discover_big_picture.md](02_discover_big_picture.md) · [02_discover_process_level.md](02_discover_process_level.md) | ✅ |
| 3 | Decompose | Where are the natural seams? | [03_decompose_subdomains.md](03_decompose_subdomains.md) | ✅ |
| 4 | Strategize | What differentiates, what is plumbing? | [04_strategize_core_domain_chart.md](04_strategize_core_domain_chart.md) | ✅ |
| 5 | Connect | How do scenarios flow across boundaries? | [05_connect_message_flows.md](05_connect_message_flows.md) | ✅ |
| 6 | Organise | Which team would own each context? | [06_organise.md](06_organise.md) | ✅ |
| 7 | Define | What is each context responsible for? | [07_define_context_map.md](07_define_context_map.md) · [07_define_invoice_watching.md](07_define_invoice_watching.md) · [07_define_notification_delivery.md](07_define_notification_delivery.md) · [07_define_ksef_access.md](07_define_ksef_access.md) · [07_define_subject_configuration.md](07_define_subject_configuration.md) | ✅ |
| 8 | Code | Aggregates, entities, events inside contexts? | **Invoice Watching:** [domain model](08_invoice_watching_domain_model.md) · [aggregate](08_invoice_watching_aggregates.md) · [value objects](08_invoice_watching_value_objects.md) · [domain services](08_invoice_watching_domain_services.md) · **KSeF Access:** [tactical model](08_ksef_access_tactical_model.md) · **Notification Delivery:** [tactical model](08_notification_delivery_tactical_model.md) · **Subject Configuration:** [no tactical model (justified)](08_subject_configuration_tactical_model.md) | ✅ |
| 9 | Architecture | How does the model map to implementation? | — | ⏸ |

## Decisions log (this pass)

| Decision | Value |
|----------|-------|
| Artifact language | English |
| Docs directory | `./docs` |
| Scope | Steps 1–7 (light, "simplified big picture") + full tactical design (step 8); architecture (step 9) not started |
| Product shape | Open source, multi-tenant (multiple subjects per daemon), self-hosted |
| Product boundary | Notification-only — never invoice management (viewing/parsing/booking/payments) |
| Data source | KSeF 2.0 simplified list of received invoices |
| Notification content | KSeF reference number, issuer invoice number, net amount + gross amount + currency, issuer NIP (+ issuer name iff the simplified list returns it); **no per-invoice API calls** (OQ-1/OQ-10 resolved); which amount is *displayed* is a per-subject setting (OQ-16, see below) |
| Persisted state | Per subject: registry of already-notified invoice refs + `lastHwm` HWM cursor — minimal (A3, I-23) |
| Registry retention | **Resolved (OQ-8): keep forever** — no retention mechanism, no parameter (rows ~40B; deletion is the only irreversible op — Simplicity); future giant ⇒ index the store, don't prune |
| Downtime behaviour | Catch-up from cursor (`lastHwm` persists; the first poll simply fetches a wider window) |
| Configuration | Config file with **hot reload**: auto-detected file change, validate, apply without restart (OQ-3 resolved) |
| Hot reload safety | Invalid reload ⇒ keep last valid config + loud log (I-16); fail-fast only at startup (I-13) |
| Subject onboarding | Baseline: first poll marks current list as notified, no historical flood (I-18) |
| Subject removal | **Resolved (OQ-15): baseline afresh on re-add** — removal deliberately resets state (I-19, updated — supersedes "resume from dormant state"); absence-period notifications are relinquished by the removal; PG-2 scoped to "while configured and watched" |
| Polling interval | Per subject, **in minutes**, default 60 (OQ-19 resolved) |
| KSeF rate limits | **Verified** (official *Limity żądań API*): per pair (context + IP); simplified list 8 req/s · 16 req/min · 20 req/h (bottleneck); session endpoints 120 req/h |
| Min interval | **≥ 15 min per subject** (MF recommendation, verified) — hard validation bound (I-13a); default 60 min |
| Poll spreading | **Deterministic poll offset per subject** (`hash(NIP) mod interval`) — load smoothing/politeness (A9); first poll at boot + offset |
| Poll budget | **Per subject** (each NIP context has own 20 req/h), **no global fleet cap** (I-21, corrected after docs verification) |
| KSeF session | **Fresh session per poll** — open, fetch, close; no reuse/renewal (OQ-2 resolved, A8) |
| Delivery semantics | At-least-once (send-before-mark; duplicate notification acceptable, lost is not) — A4/OQ-4 resolved: on failure NOT marked notified; **hybrid retry** (OQ-17, option c): in-cycle backoff 5s→20s→60s (max 3 attempts), then next poll's window re-plan = unbounded, restart-proof retry |
| Notification form | **One message per invoice** — no digest mode, normal polls and catch-up uniform (OQ-6 resolved, I-22) |
| Silent-daemon risk | **Resolved (OQ-7a/7b): watchdog heartbeat per subject** — daily "no new invoices (as of {date})" pulse through the normal delivery path; a missing expected pulse is the alarm; replaces a dedicated operator fallback channel in V1; per-subject pulse time derived from poll offset; scheduling → Step 9 |
| Detection mechanism | **HWM-cursor window-fetch + registry-diff** (OQ-5 resolved, I-23): window `From = lastHwm → now` in snapshot mode, paginate all pages, dedupe by `KsefNumber` vs notified-refs registry; `lastHwm` advances only when the whole window is notified (send-before-mark applies to the cursor, not just the registry) — pattern from official C# client's incremental-retrieval E2E |
| KSeF query API facts | `SubjectType=Subject2` = received invoices; date window ≤ 100 days UTC; pageSize 10–250; `HasMore`/`IsTruncated` (10k/query, `LastPermanentStorageDate` continuation); `PermanentStorageHwmDate` in response; HWM minimizes but does not eliminate duplicates (registry stays) |
| Baseline mechanics (I-18, updated for HWM) | New subject (`lastHwm = null`): one narrow fetch, no notifications, `lastHwm` set — registry not populated by baseline; only post-onboarding arrivals are notified |
| Implementation stack | **C# (.NET)** on the official `CIRFMF/ksef-client-csharp` client (KSeF Access wraps it as ACL) — A10 |
| KSeF auth | Token generated in KSeF (+ NIP where required), not certificates — A11 |
| Config format | **YAML** `config.yaml`; search paths: binary dir, then `/etc/ksef-watcher/` (A12; OQ-14 resolved) |
| Test environments | KSeF sandboxes only (`api-test`, `api-demo`) for integration testing, never production data — A13; **environment config (OQ-9 resolved): file-level `defaultEnvironment` (default `test`) + per-subject override** — prod is always an explicit act |
| Step 8 — aggregate shape | One `SubjectWatch` aggregate per subject: `notifiedRefs` + `lastHwm` persistent, `pendingWindow` **transient** (crash ⇒ same window re-planned: duplicate at worst, never loss); commands: `ConfirmBaseline`, `PlanFetch`, `Detect`, `MarkNotified`, `AdvanceHwm` |
| Step 8 — ports | `IInvoiceListProvider` (window-in → windowed result, no provider-side cursor) + `INotifier` (whole `DetectedInvoice` per message); `PollCycle` orchestrates and owns no state |
| Step 8 — deliberate absences | No entities (nothing has a lifecycle), no read models (detection must see command-mutated state), no integration events (in-process contracts in a monolith); domain events stay inside the context |
| Notification content — currency? | **Resolved (OQ-16):** payload carries `netAmount` + `grossAmount` + `currency` (all from the simplified list); **which amount is displayed** is a per-subject config setting (`amountDisplay: brutto \| netto`, default `brutto`), consumed at render time; message = factual invoice info + amount only, no advisory texts |
| Retry split | **Resolved (OQ-17: option c, hybrid):** ND attempts once + classifies; cycle service holds the in-cycle backoff (3 attempts, 5s→20s→60s, hardcoded V1); unbounded retry = next-poll re-plan, emergent from the HWM cursor — I-10 refined accordingly ("retry lives with the caller") |
| Step 8 — KSeF Access | Stateless ACL service: window-in → paginated snapshot query (`Subject2`, pageSize 250, all `HasMore` pages, `hwm` mandatory) → fresh session per fetch, always closed; `IsTruncated` fails loudly (I-8); 429 → `RateLimited(retryAfter)`, no cursor move |
| Step 8 — Notification Delivery | Single-attempt `DeliveryService` (retry lives with the caller, OQ-17c) + failure classification table (5xx/timeout/429 → retryable; 4xx≠429 → permanent, loud); renderer rule: factual fields + amount per `AmountDisplay`, no advisory texts (OQ-16) |
| Step 8 — Subject Configuration | **No tactical model (deliberate, justified)** — schema/validation/reload machinery are Step 9 components; schema sketch fixed (version, per-subject fields incl. `amountDisplay`) |
| Auth failure classification | **Resolved (OQ-18): permanent** — loud log every poll, cursor stays, timers keep ticking (no hidden stop-state); hot reload resumes after the config fix (I-19); consistent with ND's revoked-webhook pattern |
| Discord burst throttle | **Resolved (OQ-11):** fixed 3 s delay between messages in the cycle's batch (≤20/min, under the ~30/min webhook limit); stray 429 → existing OQ-17c backoff; hardcoded V1 |
| Multi-channel fan-out | **Resolved (OQ-12): not in V1** — exactly one channel per subject (validated); reopen on demand, semantics designed then; `channels: []` list placeholder in schema avoids future breaking change |
| Credentials storage | **Resolved (OQ-13): both** — literal token or `${ENV_VAR}` in config; loader resolves `${...}` from environment; defaults documented as literal + `chmod 600` + dedicated owner; I-14 (never logged) covers both forms |