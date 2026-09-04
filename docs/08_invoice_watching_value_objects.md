# Step 8c — Code: Invoice Watching — Value Objects

**Status:** ✅ reviewed & accepted

All value objects are **immutable**; equality is structural (by value). C#-oriented sketches are indicative — final shape belongs to implementation.

## Value objects

| VO | Shape (indicative C#) | Rules | Notes |
|---|---|---|---|
| `SubjectId` | `record SubjectId(string Nip)` | non-empty; validated NIP format (checksum per I-13) | Identity of the aggregate. Also the key for poll-offset derivation (A9) and per-subject KSeF rate budget (I-21). |
| `InvoiceReference` | `record InvoiceReference(string KsefNumber)` | non-empty; `OrdinalIgnoreCase` equality (matching the C# client's E2E dedup convention) | The registry key. ACL-translated from KSeF's `KsefNumber` — no other KSeF shape enters the context. |
| `Hwm` | `record Hwm(DateTimeOffset Utc)` | UTC only | `PermanentStorageHwmDate` translated by KSeF Access. Monotonic within a subject: `AdvanceHwm` only moves it forward (time doesn't flow backwards in snapshot mode). |
| `FetchWindow` | `record FetchWindow(DateTimeOffset From, DateTimeOffset To)` | `From < To`, both UTC; `To − From ≤ 100 days` (API limit); `From` is the *exclusive* lower bound of the previous HWM (the HWM semantics: data committed *up to and including* the mark) | The window handed to `IInvoiceListProvider`. Splitting into sub-windows for spans > 100 days is the provider's/Cycle's mechanical concern, not the VO's. |
| `DetectedInvoice` | `record DetectedInvoice(InvoiceReference Ref, string InvoiceNumber, decimal NetAmount, decimal GrossAmount, string Currency, string IssuerNip, string? IssuerName)` | all present except `IssuerName?` (iff the simplified list returned it, OQ-1); amounts + currency straight from the list (OQ-16 resolved) | The notification payload for one invoice (I-12: whole structured record to the notifier, not extracted scalars). **Presentation-agnostic:** carries both amounts; the netto-vs-brutto *display* choice is a per-subject config parameter consumed at render time — never part of the payload. |
| `PendingWindow` | `record PendingWindow(FetchWindow Window, IReadOnlySet<InvoiceReference> Refs, Hwm Hwm)` | — | **Aggregate-internal, transient** (08b): the window currently being processed. Never persisted — see I-4/I-23 rationale. |
| `FetchedWindow` | `record FetchedWindow(IReadOnlySet<InvoiceReference> Refs, IReadOnlyList<DetectedInvoice> Detected, Hwm Hwm)` | — | The port's return shape (08d) — **Published Language of the IW↔KSeF Access contract**; constructed by KSeF Access (its canvas calls it the translated result), consumed here. |

## Entities — none (justified)

Checked against the entity test (*has identity with a lifecycle*): nothing qualifies. `InvoiceReference` entries have no lifecycle (present / not yet present — that is it); the only identity in the context is the aggregate root's `SubjectId`. See the overview's "Deliberate absences".