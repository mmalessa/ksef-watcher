# Step 7d — Define: Notification Delivery (Bounded Context Canvas)

**Status:** ✅ reviewed & accepted (steps 1–7 pass) · **Classification:** Supporting (pluggable)

| Field | Value |
|---|---|
| **Name** | Notification Delivery |
| **Purpose** | Deliver notification payloads to a subject's configured channel(s) via internet messengers (Discord first), reporting a truthful delivery result. |
| **Strategic classification** | Supporting, deliberately extensible (PG-4: adding a messenger must not touch domain logic). |
| **Domain roles** | Channel adapter host; owner of the `Notifier` interface (Published Language); executor of delivery retries. |

## Inbound communication

| From | What arrives | Pattern |
|---|---|---|
| Invoice Watching | `SendNotification(channel, NotificationPayload)` | Customer–Supplier (supplier side) + Published Language |
| Subject Configuration | Channel definitions (Discord webhook URL / bot config, …) | Published Language (read-only) |

## Outbound communication

| To | What leaves | Pattern |
|---|---|---|
| Invoice Watching | `DeliveryConfirmed` / `DeliveryFailed(reason)` | Customer–Supplier |
| External messengers | Messenger-specific API calls (webhook/message) | ACL per notifier implementation |

## The `Notifier` interface (Published Language, deliberately sketched)

```text
Notifier.send(channel: ChannelRef, payload: NotificationPayload) -> DeliveryResult
```

- `NotificationPayload` is a *structured* record (`DetectedInvoice`: refNo, invoiceNo, netAmount, grossAmount, currency, issuerNip, issuerName?), **not** pre-rendered text — each notifier renders for its medium. Surface rule: payload = only what the simplified list returns; `issuerName?` present iff the list provides it, never fetched per-invoice (OQ-1, resolved). **Presentation rule (OQ-16, resolved):** the payload carries both amounts + currency and is presentation-agnostic; which amount is displayed comes from the per-subject config (`amountDisplay: brutto | netto`, default `brutto`); the rendered message contains only factual invoice info and the amount — no advisory texts ("pay today…").
- Pluggable-implementation lesson applied: the interface carries the *whole payload object*, not extracted scalars, so new notifiers can render differently without changing the call signature.

## Ubiquitous language

| Term | Meaning |
|---|---|
| **Channel** | A configured notification target (e.g. a Discord webhook); belongs to a subject's config. |
| **Notifier** | An adapter implementation for one messenger family. |
| **Payload** | Structured data about detected invoices, rendered per channel. |
| **Delivery result** | Confirmed / Failed(retryable) / Failed(permanent). |

## Business decisions (invariants)

1. **I-9 Truthful results:** `DeliveryConfirmed` is returned only on real messenger acknowledgement — optimism here breaks I-1 upstream.
2. **I-10 Classify truthfully; retry lives with the caller:** *(refined by OQ-17, option c)* Notification Delivery performs **one delivery attempt per call** and classifies the result: Confirmed / Failed(retryable) / Failed(permanent). The in-cycle backoff loop (3 attempts, 5s→20s→60s) and the unbounded next-poll retry live in the cycle service (the caller) — never inside ND, so the restart-proof guarantee never depends on an in-memory loop. On failure, refs are NOT marked notified upstream — never (OQ-4).
3. **I-11 Permanent failure surfaces:** a permanently broken channel (revoked webhook) must not be silently retried forever — it is reported (log in V1; escalation mechanism open — OQ-7b, possibly a heartbeat watchdog per OQ-7a). Note the interplay with I-10/OQ-4: retryable failures retry forever (no loss, PG-2), so "permanent" classification (e.g. webhook revoked) is what stops the loop — and it must be reported, not silent.
4. **I-12 One payload, many renderings:** all required notification content (Step 1 decision) is available to every notifier; no notifier-specific fields in Watching.
5. **I-22 One message per invoice:** each detected invoice is sent as its own notification message — no digest/aggregation mode (OQ-6, resolved; closes HS-4). Applies uniformly to normal polls and catch-up batches; batch size only affects *how many* messages are sent (sequentially, with a small delay — OQ-11), never their form.

## Assumptions

- Discord webhook is the only V1 notifier.
- One channel per subject in V1 (multi-channel fan-out later — OQ-12).

## Open questions

- **OQ-7a** Watchdog heartbeat: a periodic (e.g. daily) *"no new invoices"* message to the subject's channel; its *absence* signals that daemon or channel is broken (inverted liveness check — operator's proposal). Open points: cadence/wording, per-subject vs per-daemon scope, and whether it replaces or complements OQ-7b. Note: a heartbeat exercises the *whole delivery path* (channel health + daemon liveness) — it is self-checking, unlike a failure alert that can silently fail itself.
- **OQ-7b** Permanent channel failure (webhook revoked): how does the operator learn? Logs only (V1 default) vs operator fallback channel. Watch-out: the alerting path can itself fail — see OQ-7a's advantage.
- **OQ-11** Discord rate limits under catch-up bursts (N invoices → N messages, per I-22 sent sequentially) — backoff strategy needed? Default: sequential send with small delay.
- **OQ-12** Multi-channel support per subject (fan-out to e.g. Discord + e-mail): config language and delivery semantics (all must confirm? best-effort?). Out of V1 scope, kept as a language placeholder.