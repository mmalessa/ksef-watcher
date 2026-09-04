# Step 7b — Define: Invoice Watching (Bounded Context Canvas)

**Status:** ⏳ drafted · **Classification:** Core Domain

| Field | Value |
|---|---|
| **Name** | Invoice Watching |
| **Purpose** | Decide, for each subject, which received invoices are *new* (not yet notified) and orchestrate their notification; never miss one (PG-2). |
| **Strategic classification** | Core. Evolution: custom-built (the product's heart; not buyable). |
| **Domain roles** | Decision maker & orchestrator of the poll cycle; owner of the notified-invoice registry (cursor). |

## Inbound communication

| From | What arrives | Pattern |
|---|---|---|
| Scheduler (timer, driven by Subject Configuration intervals) | `PollSubject(subjectId)` — triggers the cycle; the window is Watching's own state, not a parameter from the scheduler | internal command |
| KSeF Access | `InvoiceListFetched(subjectId, items, hwm)` — ACL-clean window of received-invoice references + the fetch's HWM for the cursor decision | Customer–Supplier (customer side) |
| Notification Delivery | `DeliveryConfirmed(refs)` / `DeliveryFailed(refs, reason)` | Customer–Supplier (customer side) |

## Outbound communication

| To | What leaves | Pattern |
|---|---|---|
| Notification Delivery | `SendNotification(channel, payload[refNo, invoiceNo, grossAmount, issuerNip, issuerName?])` — `issuerName?` present iff the simplified list returned it (OQ-1, resolved) | Customer–Supplier + Published Language (`Notifier` interface) |

No other context may read or write the registry; it is internal state (invariant below).

## Ubiquitous language

| Term | Meaning |
|---|---|
| **Subject** | A company (NIP) whose KSeF inbox is being watched. |
| **Invoice reference (refNo)** | KSeF number uniquely identifying a received invoice; the registry key. |
| **Notified-invoice registry (cursor)** | Per-subject set of invoice references already notified (high-water semantics). |
| **Detection** | Diffing a fetched window against the registry; output: unseen references. |
| **Fetch window** | The date range requested from KSeF each poll: `From = lastHwm` (the subject's persisted high-water mark — see HWM cursor below), `To = now`, in **snapshot mode** (`DateType = PermanentStorage`, `RestrictToPermanentStorageHwmDate = true`). The window is the *fetch* mechanism; the registry is the *detection* mechanism. |
| **HWM cursor (`lastHwm`)** | Per-subject persisted high-water mark = `PermanentStorageHwmDate` from the last window in which **all references were notified** (not merely fetched — see I-23). Authoritative "data-committed-up-to" point from KSeF itself (official incremental-retrieval pattern from the C# client: continuation point = HWM of the previous window). Replaces clock-based cursor start; a still-in-flight invoice (accepted, not yet in permanent storage) is simply below the new HWM's coverage and will be fetched in a later window — by design, not by margin. |
| **Catch-up** | First polls after downtime detecting a backlog — same code path as normal detection (Scenario B). |
| **Send-before-mark** | Ordering rule: notification delivered *before* registry update (at-least-once). |
| **Baseline** | First poll of a subject with no state (`lastHwm = null`): one narrow fetch, no notifications, `lastHwm` set — only later arrivals are notified (I-18). |
| **Dormant registry** | State (registry + `lastHwm`) of a subject removed from config: retained untouched, resumes as-is on re-add. |

## Business decisions (invariants)

1. **I-1 No loss:** an invoice reference is marked notified *only after* delivery confirmation — never before.
2. **I-2 No skip on failure:** a failed delivery leaves the cursor untouched; the reference will be re-detected and re-sent with backoff (OQ-4, resolved) — duplicate possible, loss impossible.
3. **I-3 Per-subject isolation:** one subject's failure (KSeF, notifier) never blocks another subject's cycle.
4. **I-4 Registry survives restarts:** cursor semantics break if state is volatile (A3) — constrains persistence choice in Step 8/9.
5. **I-5 Monotonic cursor:** the registry only grows (append notified refs); no removal of "old" refs without an explicit, justified retention policy (OQ-8).
6. **I-18 Baseline on empty registry:** a subject's first poll ever (no registry, `lastHwm = null`) establishes the baseline in **one minimal fetch**: fetch with a narrow window (e.g. the last poll interval, not "from the beginning" — that would hit the 100-day/10 000-result API limits on old inboxes), send nothing, and set `lastHwm = hwm` of that fetch. From then on the normal HWM-cursor flow applies; only invoices arriving after onboarding are notified. Rationale: notification-only product — the operator cares about *new arrivals*, not history (Long-term vision, `01_understand.md`). Note: with the HWM cursor (I-23) the baseline needs no registry entries at all — the cursor itself carries "everything before `lastHwm` is settled"; the registry only accumulates refs from the first real window onward (I-5's "append-only" remains untouched).
7. **I-19 Registry survives subject removal:** removing a subject from config stops polling but never deletes its registry; re-adding resumes from existing state (no baseline re-run, no historical flood).
8. **I-20 Hot reload never loses state:** config reload (add/remove/change) never resets, deletes or skips any subject's registry; it only starts/stops timers and passes new parameters.
9. **I-23 Window-fetch + registry-diff, HWM cursor:** each poll fetches invoices **by date window** (`From = lastHwm`, `To = now`, snapshot mode `RestrictToPermanentStorageHwmDate = true`, iterated through all pages until `HasMore = false`), detects new ones by **diffing against the registry** (dedupe by `KsefNumber`). **The HWM cursor follows the registry, not the fetch:** `lastHwm` is advanced only once *every reference from the window is marked notified* (I-1) — never right after fetch. A crash between fetch and send therefore re-fetches the same window and re-detects (duplicate at worst, never a loss — HS-3b applies to both registry and HWM). Division of roles: the **HWM cursor** bounds fetch completeness (a still-in-flight invoice is below HWM and arrives in a later window — by design; the safety-margin concept is thereby retired), the **registry** guarantees detection semantics (duplicates on window boundaries and after crashes are filtered by refs, PG-2-safe). If `IsTruncated` (10 000-result limit hit) — continue from the truncation point before advancing the window. Windows longer than the API's 100-day limit are split into sub-windows.

## Assumptions

- **KSeF query API guarantees — verified** against the official C# client (`ksef-client-csharp`, `IInvoiceDownloadClient.QueryInvoiceMetadataAsync` → `POST /v2/invoices/query/metadata`): the request carries a **date-range filter** (`DateRange { DateType: Issue | Invoicing | PermanentStorage, From, To?, RestrictToPermanentStorageHwmDate? }`), **pagination** (`pageOffset`, `pageSize` 10–250, response `HasMore`), and the response includes **`PermanentStorageHwmDate`** (API-provided high-water mark of committed data; snapshot mode `RestrictToPermanentStorageHwmDate = true` makes it stable and guaranteed — official incremental-retrieval pattern). Received invoices = `SubjectType.Subject2` (buyer role). Boundaries to respect: date window **≤ 100 days (UTC)**, max **10 000 results** per query (`IsTruncated`, with `LastPermanentStorageDate` as continuation). *(was "complete snapshot, unverified" — superseded; see OQ-5 resolution below)*
- Invoice references (`KsefNumber`) are stable and unique within a subject's KSeF inbox; the "received" date = `AcquisitionDate` (date of assigning the KSeF number — MF definition), assigned once and never changes.

## Open questions

- **OQ-5** *Resolved.* The KSeF simplified list supports **date-range filtering** (`DateRange` in the request), **pagination** (`pageOffset`/`pageSize`, response `HasMore`), and returns **`PermanentStorageHwmDate`** — which we adopt as the cursor start (I-23, HWM mode). Combined with stable `AcquisitionDate` (KSeF-number assignment date, never changes), cursor-diffing is implementable correctly: fetch by window from `lastHwm`, iterate all pages, dedupe by ref. This unblocked Step 8 aggregate design. *Note:* the official C# client's incremental-retrieval E2E test documents this exact pattern (continuation point = HWM) and states explicitly that **HWM minimizes but does not eliminate duplicates** — hence the registry stays.
- **OQ-8** Registry retention: keep all notified refs forever vs prune by age/count. *Eased by the OQ-5 resolution:* the HWM cursor (I-23) bounds the fetch window, so pruning refs **older than the window span** would be *safe* today — but "keep forever" remains the default (rows are tiny; deleting state is the only irreversible operation in the system — Simplicity). Revisit only if a real subject produces six-digit ref counts.