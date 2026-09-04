# Step 7c — Define: KSeF Access (Bounded Context Canvas)

**Status:** ✅ reviewed & accepted (steps 1–7 pass) · **Classification:** Supporting (ACL)

| Field | Value |
|---|---|
| **Name** | KSeF Access |
| **Purpose** | Provide the rest of the system a clean, reliable capability: *"give me the current simplified list of invoices received by subject X"* — hiding all KSeF session/auth/payload complexity. |
| **Strategic classification** | Supporting. Evolution: commodity integration, but robustness-critical (the product is blind without it). |
| **Domain roles** | Anti-corruption layer in front of a government external system; list provider. |

## Inbound communication

| From | What arrives | Pattern |
|---|---|---|
| Invoice Watching (orchestrator) | `FetchInvoiceList(subjectId, window: {From, To})` — the window is a parameter, KSeF Access holds no cursor state of its own | Customer–Supplier (supplier side) |
| Subject Configuration | Credentials, NIP, KSeF environment (test/prod) per subject | Published Language (read-only) |

## Outbound communication

| To | What leaves | Pattern |
|---|---|---|
| Invoice Watching | `InvoiceListFetched(subjectId, items, hwm)` — internal representation, no raw KSeF shapes; `hwm` = translated `PermanentStorageHwmDate` of this fetch, passed through for the caller's cursor decision | Customer–Supplier |
| (log/monitoring) | `SubjectPollFailed(subjectId, reason)` | event for operator visibility |

**ACL rule:** KSeF concepts (session tokens, error codes, payload field names) must not appear outside this context. Translation happens here: KSeF JSON → `DetectedInvoice { refNo, invoiceNo, netAmount, grossAmount, currency, issuerNip, issuerName? }` — the translated item carries **only what the simplified list itself returns**; per-invoice detail calls are outside the product boundary (OQ-1/OQ-10, resolved). `issuerName?` is included *if and only if* the simplified list returns it — never fetched separately.

## Ubiquitous language

| Term | Meaning |
|---|---|
| **KSeF session** | Authenticated context for API calls (HS-2); lifecycle owned here. **Fresh session per poll:** open, fetch, close (OQ-2, resolved — A8). |
| **Simplified list** | KSeF API 2.0 endpoint returning received invoices without full XML (A1) — `POST /v2/invoices/query/metadata`, `SubjectType = Subject2` (buyer role), snapshot mode, paginated. |
| **Fetch** | One authenticate + fetch cycle for one subject, executing the window passed by the caller (the *poll* is the caller-side cycle; this context performs its KSeF leg). |
| **Translated list item** | Context-internal representation of one received invoice (see ACL rule). |

## Business decisions (invariants)

1. **I-6 Faithful translation:** every invoice present in KSeF's response appears in `InvoiceListFetched`, and the fetched `hwm` is passed through unchanged; nothing invented, nothing dropped.
2. **I-7 Session is invisible:** callers never see or manage tokens; re-auth is this context's problem.
3. **I-8 Fail loudly:** KSeF errors surface as `SubjectPollFailed` with a reason — never as an empty successful list (empty list ≠ failure; conflating them would silently skip detection).

## Assumptions

- **Fresh session per poll** (open → fetch → close, no reuse — OQ-2 resolved, A8); a direct consequence of the hourly cadence. *(Kept as an assumption on purpose: if the cadence ever drops below a few minutes, revisit — reuse may become worthwhile.)*
- **Rate limits — verified** (official *Limity żądań API*, A7): counted **per pair (context + IP)**, so each subject has its own budgets. Simplified list (`POST /invoices/query/metadata`): 8 req/s · 16 req/min · **20 req/h** — the bottleneck; session open/close: 30 req/min · **120 req/h**. At I-13a's minimum interval (15 min) one subject consumes ≤ 4 list calls/h — well within budget. The interval bound (not runtime throttling) is the enforcement mechanism (deliberate simplicity — if 429s appear in practice, handle `Retry-After` and revisit in Step 9).
- **429 handling:** KSeF returns 429 with a `Retry-After` header (dynamic block duration); repeated violations extend the block. `SubjectPollFailed` (I-8) is the domain-level surface; honoring `Retry-After` is this context's mechanics.
- Credentials per subject, from config (A5).

## Open questions

- **OQ-9** *Resolved.* KSeF environment switching: **global file-level default + per-subject override** — `defaultEnvironment` in `config.yaml` (default value: `test`), a subject may set `environment: test | prod` explicitly. Rationale: new subjects inherit the safe default (hard to hit production by accident); switching a subject to prod is a conscious, visible act.
  **V1 wiring constraint (found while building `KsefClientAdapter`, docs/09_architecture.md):** the vendored client's `IAuthCoordinator`/`IInvoiceDownloadClient` are obtained per-environment (`IKSeFClientFactory.KSeFClient(environment)`), but `KsefClientAdapter` is wired with **one fixed instance** for the whole daemon process — it does not resolve environment per subject at call time. So although the *schema* still allows a per-subject `environment` override (kept for forwards-compatibility, no breaking change if this is revisited), **mixing test and prod subjects in one running daemon is not actually supported in V1** — every subject is served against whatever single environment the Host process was wired for. A subject whose resolved `environment` differs from the daemon's would silently authenticate against the wrong one. Operators must run one daemon instance per KSeF environment until this is revisited (fix: `KsefClientAdapter` resolving per-call via `IKSeFClientFactory`/`IKSeFFactoryCryptographyServices`, keyed by `SubjectCredentials.Environment` — deliberately deferred, not implemented).