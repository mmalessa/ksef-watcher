# Step 8e — Code: KSeF Access — Tactical Model

**Status:** ✅ reviewed & accepted

Supporting context (ACL). One responsibility (Step 7): *window-in → windowed-result*, hiding all KSeF session/auth/payload complexity. The tactical model is deliberately thin — the value here is robustness and faithful translation, not domain richness (Step 4).

## Building blocks

| Block | Kind | Notes |
|---|---|---|
| `InvoiceListItem` | value object | Context-internal translation of one KSeF `InvoiceSummary`: `{Ref, InvoiceNumber, NetAmount, GrossAmount, Currency, IssuerNip, IssuerName?}` — exactly the simplified-list surface (OQ-1), nothing more. |
| `FetchWindow` | value object | Shared shape with Invoice Watching (same record type crosses as the parameter — it *is* the contract, Published Language of the port). |
| `FetchedWindow` | value object | `{refs, detected: IReadOnlyList<InvoiceListItem>, hwm}` — the windowed result handed back. |
| `SubjectCredentials` | value object | `{Nip, Token, Environment}` — read from validated config; never logged (I-14). |
| `KsefAccessService` | domain service | The single implementation of Invoice Watching's port `IInvoiceListProvider`. Orchestrates: session open → query (all pages) → session close → translate. Owns **no cursor state** (window is a parameter). |
| `KsefClientAdapter` | infrastructure port | Thin wrapper over the official `ksef-client-csharp` (`IInvoiceDownloadClient.QueryInvoiceMetadataAsync`). The **only** place where C#-client types appear; everything above sees watcher-internal shapes (ACL enforcement point). |
| `PollFailure` | value object | `{subjectId, reason: RateLimited(retryAfter) | AuthFailure(permanent) | ApiError | Network}` — classification surfaced to logs as `SubjectPollFailed` (I-8). **AuthFailure is permanent** (OQ-18, resolved): an expired/revoked/mistyped token never self-heals — every poll re-classifies and logs loudly, the cursor stays (I-2: nothing lost), timers keep ticking (no hidden stop-state to sync with config); the operator fixes `config.yaml` and hot reload resumes polling (I-19). Consistent with ND's revoked-webhook classification (same pattern: operator intervention required). |

## `KsefAccessService.FetchWindowedList` — flow

```text
FetchWindowedList(subjectId, window):
    creds = configStore.Current(subjectId)                 # validated config, read-only
    session = client.OpenSession(creds)                   # fresh session per poll (A8) — auth: token + NIP (A11)
    try:
        pages = [ query(subject2, window, snapshot, pageSize=250, offset=0) ]   # Subject2 = received
        while pages.last.HasMore: pages += query(..., offset += 1)               # paginate to the end
        # IsTruncated (10k/query) cannot happen for legal windows:
        #   window span ≤ 100 days AND baseline/interval keep result sets far below 10k;
        #   a subject receiving >10k invoices per window would break the whole product's
        #   assumptions — surfaced as PollFailure(ApiError) if it ever occurs (fail loudly, I-8)
        items = pages.flatMap(p => p.Invoices).map(translate)                    # KSeF → InvoiceListItem
        hwm   = pages.last.PermanentStorageHwmDate ?? error(I-6: hwm mandatory in snapshot mode)
        return FetchedWindow(items, hwm)
    finally:
        client.CloseSession(session)                      # always close (A8)
```

**Key mechanics (traceability):**

| Behaviour | Source |
|---|---|
| Window as parameter; no cursor state held | Step 7 contract, I-23 |
| `SubjectType.Subject2` (buyer role) | verified API fact |
| Snapshot mode `RestrictToPermanentStorageHwmDate = true`; `hwm` mandatory | I-23, official incremental pattern |
| `pageSize = 250` (max allowed), iterate `HasMore` | verified API fact (10–250) |
| `IsTruncated` → fail loudly, not silent truncation | I-8, I-6 (nothing dropped) |
| Fresh session per fetch, always closed (finally) | A8 |
| 429 → `PollFailure.RateLimited(retryAfter)` — honors `Retry-After`, cycle aborts (no cursor move) | A7 verified, I-8 |
| Translation carries only the list surface; `issuerName?` iff present | OQ-1, I-6 |

## Deliberate absences (justified)

- **Aggregates — none.** No invariants over time: the context is a stateless translator/orchestrator between a caller's window and KSeF. Its "state" (session) lives and dies within a single call.
- **Domain events — none published.** `SubjectPollFailed` is an *outbound log/monitoring surface* (Step 7), not an event other contexts subscribe to; the domain events of the poll cycle (`NewInvoicesDetected`, …) belong to Invoice Watching.
- **Entities — none.** Nothing has a lifecycle here; everything is a value.

## Open questions

- *(OQ-18 resolved — see `PollFailure` above: auth failure is permanent; poll cadence continues, loud log every interval, hot reload resumes after the operator fixes the token.)*