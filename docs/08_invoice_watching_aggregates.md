# Step 8b — Code: Invoice Watching — Aggregate

**Status:** ✅ reviewed & accepted

## Aggregate: `SubjectWatch`

One instance per subject (identified by `SubjectId`). It is the *entire* persistent state of the context — the unit that enforces every correctness invariant of the product.

### State

| Field | Type | Persistence | Meaning |
|---|---|---|---|
| `subjectId` | `SubjectId` | persistent | Identity — which company's inbox this watches (NIP). |
| `notifiedRefs` | `IReadOnlySet<InvoiceReference>` | persistent | The registry: refs already notified (I-5: append-only). |
| `lastHwm` | `Hwm?` | persistent | HWM cursor; `null` ⇒ not onboarded yet (baseline pending, I-18). |
| `pendingWindow` | `PendingWindow?` | **transient** | The currently-processing window: `{window: FetchWindow, refs: IReadOnlySet<InvoiceReference>, hwm: Hwm}`. Deliberately *not* persisted: if the process dies mid-window, the next poll re-fetches the same `lastHwm`-anchored window — duplicates at worst, never a loss (I-23, HS-3b). |

### Commands

| Command | Guard / transition | Emits |
|---|---|---|
| `ConfirmBaseline(hwm)` | `lastHwm == null` (else no-op). Sends nothing, stores nothing in `notifiedRefs` — sets `lastHwm = hwm`. | `SubjectOnboarded` |
| `PlanFetch()` → `FetchWindow` | Requires `lastHwm != null` (caller ensures baseline happened). Returns `window = {from = lastHwm, to = now}`; the caller (PollCycle) hands it to KSeF Access and calls back with the result. | — (query-like command) |
| `Detect(window, fetched)` | Requires `pendingWindow == null`. Computes `unseenRefs = fetched.refs.except(notifiedRefs)` internally — the registry is never writable from outside. Stashes `pendingWindow = {window, refs: fetched.refs, hwm: fetched.hwm}`. Emits `NewInvoicesDetected(unseenRefs)` if non-empty. **If nothing is new, the window stays pending with zero unseen** — the cycle calls `AdvanceHwm()` anyway (guard passes: every fetched ref is already notified), so `lastHwm` catches up. This is what recovers the cursor after a crash between `MarkNotified` and `AdvanceHwm` (re-planned window re-fetches already-marked refs, detects nothing, advances — no stall, no duplicates). | `NewInvoicesDetected` |
| `MarkNotified(refs)` | Requires `pendingWindow != null`; `refs ⊆ pendingWindow.refs ∖ notifiedRefs`. Appends to `notifiedRefs` (I-5). Emits `InvoicesNotified(refs)`. | `InvoicesNotified` |
| `AdvanceHwm()` | Requires `pendingWindow != null` **and** `pendingWindow.refs ⊆ notifiedRefs` (every window ref notified — I-23). Sets `lastHwm = pendingWindow.hwm`, clears `pendingWindow`, emits `CursorAdvanced`. | `CursorAdvanced` |

*Deliberate sequencing note:* `MarkNotified` and `AdvanceHwm` are **two transitions**, not one — per-invoice confirmations may arrive incrementally (I-22: one message per invoice, sequential sends). `AdvanceHwm` fires once per window, after the last confirmation.

### Invariants enforced (mapping to Step-7 canvas)

| Invariant | How the aggregate enforces it |
|---|---|
| **I-1 No loss** | Only `MarkNotified` appends refs, and only for refs the current window actually fetched (guard on `pendingWindow`); `AdvanceHwm` refuses while any window ref is un-notified. |
| **I-2 No skip on failure** | A `DeliveryFailed` maps to **no command at all** — nothing is persisted (`pendingWindow` is transient and vanishes with the cycle's `Load` on the next poll). Since `lastHwm` is unmoved, the next poll re-plans the same window and re-detects the un-notified refs (after the caller's in-cycle backoff is exhausted — OQ-17c). |
| **I-3 Per-subject isolation** | One aggregate instance per subject; failures never touch another instance. |
| **I-4 Registry survives restarts** | `notifiedRefs` + `lastHwm` are the persistent half of the state; `pendingWindow` is transient by design (see above). |
| **I-5 Monotonic cursor** | `MarkNotified` is append-only; no command removes refs. |
| **I-18 Baseline** | `ConfirmBaseline` is the only setter of a first `lastHwm`; it populates no refs. |
| **I-19/20 Reset-on-removal / safe reload** | Aggregate instances are keyed by `SubjectId`; a config-removal **reset** is a deliberate domain operation (repository delete + timer stop — OQ-15, I-19); a reload changes only timers/parameters otherwise (I-20). |
| **I-23 HWM follows the registry** | `AdvanceHwm`'s guard (`pendingWindow.refs ⊆ notifiedRefs`) is the invariant in code terms. |

### References to other aggregates

None. Other contexts are reached through ports (`IInvoiceListProvider`, `INotifier`) owned by this context's application layer — the aggregate itself holds no external references (not even by ID: `SubjectId` is its own identity).

### Repository contract (tactical boundary)

```text
ISubjectWatchRepository
    Load(subjectId: SubjectId) -> SubjectWatch        # returns a fresh instance; persistent state loaded,
                                                      # pendingWindow always empty (transient by design)
    Save(subject: SubjectWatch)                        # persists notifiedRefs + lastHwm atomically
```

- `Save` is idempotent-safe by construction: state only grows via `MarkNotified`/`AdvanceHwm`, so a crash between send and save re-plans the same window (I-23 crash semantics) — no write-ahead coordination with the notifier is required.
- The repository is the context's **only** persistence seam (Step 9 picks the concrete store; the aggregate never sees it).

### Consistency boundaries

- **Transactionality:** `MarkNotified(refs)` must persist `notifiedRefs` atomically with the emission decision of `InvoicesNotified` (send-before-mark ordering is the *caller's* duty in the retry loop — the aggregate just guards against double-appends: re-marking a ref already in `notifiedRefs` is a no-op, making redelivery idempotent, consistent with the denormalized-counter lesson: contributions are keyed by source ref, `+=` style drift is impossible by construction).
- **Concurrency:** one `SubjectWatch` instance processes at most one window at a time (`pendingWindow != null` guard); the scheduler must not start a second poll for a subject whose window is pending (per-subject single-flight — Step 9 scheduling detail).