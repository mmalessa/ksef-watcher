# Step 8d — Code: Invoice Watching — Domain Services (ports)

**Status:** ⏳ drafted

## Why domain services here

The poll cycle's logic spans the aggregate *and* two external-world ports; DDD puts such orchestration in a domain/application service so the aggregate stays free of infrastructure concerns. The service owns **no state** — all state lives in `SubjectWatch`.

## Ports (owned by this context; implemented elsewhere)

```text
IInvoiceListProvider                                  // implemented by KSeF Access (ACL behind it)
    FetchWindowedList(subjectId: SubjectId, window: FetchWindow)
        -> FetchedWindow { refs: IReadOnlySet<InvoiceReference>,
                           detected: IReadOnlyList<DetectedInvoice>,
                           hwm: Hwm }

INotifier                                             // implemented by Notification Delivery
    Send(channel: ChannelRef, invoice: DetectedInvoice,
          amountDisplay: AmountDisplay)               // Brutto | Netto — per-subject config (OQ-16)
        -> DeliveryResult                             // Confirmed | Failed(retryable) | Failed(permanent)
```

- Signatures carry **whole value objects / aggregates-relevant records**, not extracted scalars (pluggable-interface lesson) — a future notifier or provider can use more of the record without a signature change.
- `IInvoiceListProvider.FetchWindowedList` is *window-in, windowed-result*: the provider holds no cursor state (Step 7 contract — window is a parameter, `hwm` comes back with the data).
- The ports are the **only** legal way out of this context; no KSeF or messenger concept appears past them (ACL enforcement point, mirroring the Step 7 boundary rules).

## The cycle service: `PollCycle` (orchestrates one poll for one subject)

```text
PollCycle.Run(subjectId, channel, amountDisplay, provider, notifier):
    sw = repository.Load(subjectId)

    if sw.lastHwm == null:                            # baseline (I-18)
        narrow = window(now − configuredInterval, now)          # one minimal fetch
        fetched = provider.FetchWindowedList(subjectId, narrow)
        sw.ConfirmBaseline(fetched.hwm)
        repository.Save(sw); return                               # nothing sent

    window = sw.PlanFetch()                          # from = lastHwm (excl.), to = now
    fetched = provider.FetchWindowedList(subjectId, window)      # all pages, snapshot mode
    sw.Detect(window, fetched)                       # pendingWindow stashed; NewInvoicesDetected
                                                     # (if nothing new: zero unseen — falls through
                                                     #  to AdvanceHwm below, cursor catches up)

    for each invoice in fetched.detected where ref unseen:      # I-22: one message per invoice
        result = send-with-backoff: notifier.Send(channel, invoice, amountDisplay)
                 # hybrid retry (OQ-17, option c): up to 3 attempts, backoff 5s → 20s → 60s
                 #   Confirmed             -> sw.MarkNotified([invoice.Ref]); save; continue batch
                 #   Failed(retryable) exhausted -> end cycle now — refs already confirmed in this
                 #                              batch stay marked (saved incrementally); the cursor
                 #                              does not advance (window incomplete). Next poll re-plans
                 #                              the same window: only the un-notified refs re-detect.
                 #   Failed(permanent)  -> log loudly (I-11/OQ-7b); end cycle — cursor stays (no loss)

    sw.AdvanceHwm()                                  # guard: whole window notified (I-23)
    repository.Save(sw)
```

**Key decisions encoded above (traceability):**

| Behaviour | Source |
|---|---|
| Baseline = narrow window, no sends, only HWM set | I-18 (updated for HWM in the 1–7 review) |
| Window from `lastHwm` (exclusive), snapshot, all pages | I-23, A7-verified API facts |
| One `Send` per detected invoice, sequential | I-22, OQ-6, OQ-11 default |
| Hybrid retry: up to 3 attempts in-cycle (backoff 5s → 20s → 60s, hardcoded V1), then the next poll's window re-plan **is** the unbounded retry (OQ-17, resolved: option c) | OQ-4, I-2, PG-1 |
| Permanent failure stops the batch, nothing marked, surfaced loudly | I-11, PG-2 |
| `AdvanceHwm` only after whole window notified | I-23 (the crash-safety fix from the consistency review) |
| Provider/notifier hold no cursor state; window passed explicitly | Step 7 contracts (KSeF Access canvas, context map) |

**Retry semantics (OQ-17 — resolved: option c, hybrid):** each send gets a **short in-cycle backoff loop** — up to **3 attempts** with **5s → 20s → 60s** waits (hardcoded in V1; make configurable only if practice demands it — Simplicity) — which catches momentary messenger hiccups within seconds. If the attempts exhaust, the cycle ends with no state change (`pendingWindow` transient, `lastHwm` unmoved), and the *next scheduled poll* re-plans the same `lastHwm`-anchored window — the **unbounded, restart-proof retry is emergent from the HWM cursor** (this foundation exists regardless; the in-cycle loop is a responsiveness layer on top). Permanent failure skips the loop (no point retrying a revoked webhook) and surfaces loudly. *Reconciliation of I-10:* Step 7's "retry lives in ND" is refined — ND classifies the failure; the in-cycle loop and the poll-cadence retry live in the cycle service (the caller), so "retry is not upstream of the aggregate" holds and the restart-proof guarantee never depends on an in-memory loop.