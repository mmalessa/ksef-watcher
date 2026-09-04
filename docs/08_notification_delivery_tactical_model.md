# Step 8f — Code: Notification Delivery — Tactical Model

**Status:** ✅ reviewed & accepted

Supporting context (pluggable). One responsibility (Step 7): deliver one payload to one channel via the configured notifier, and classify the result truthfully. The tactical model is the `INotifier` port implementation skeleton + the Discord adapter.

## Building blocks

| Block | Kind | Notes |
|---|---|---|
| `ChannelRef` | value object | Identifies a configured channel (type + target reference, e.g. `discord-webhook <url>`) — resolved from validated config by the cycle service; ND receives it as a parameter (no config reads inside a send). |
| `AmountDisplay` | value object | `Brutto \| Netto` — per-subject config parameter (OQ-16); passed to `Send`, consumed at render time. |
| `DeliveryResult` | value object | `Confirmed \| Failed(reason: Retryable \| Permanent)` — the whole contract back to Invoice Watching (I-10: classify truthfully). |
| `NotificationRenderer` | domain service | Per-notifier rendering of `DetectedInvoice` + `AmountDisplay` → medium-native message. **Rule (OQ-16): factual invoice info + the chosen amount + currency only — no advisory texts.** |
| `Notifier` implementations | adapters | V1: `DiscordNotifier` (webhook). Each adapter is a thin ACL over its messenger API — the only place messenger concepts appear. |
| `DeliveryService` | domain service | The implementation of Invoice Watching's port `INotifier`: resolves the notifier for `ChannelRef.Type`, delegates one attempt, maps transport errors → `DeliveryResult` classification. **Single attempt — no retry loop here (OQ-17c: retry lives with the caller).** |

## Failure classification (the core decision surface)

| Situation | Classification | Rationale |
|---|---|---|
| Messenger acknowledged (Discord webhook → 204) | `Confirmed` | I-9: only real acknowledgement confirms. |
| HTTP 5xx / timeout / connection refused | `Failed(Retryable)` | Transient outage — the caller's backoff + next-poll re-plan handle it (OQ-17c). |
| HTTP 429 (Discord rate limit) | `Failed(Retryable)` | The caller's backoff (5s→20s→60s) absorbs it; sequential sends with a small delay make this rare (OQ-11 default). |
| HTTP 4xx — *not* 429 (webhook revoked/deleted, bad URL) | `Failed(Permanent)` | No point retrying — surfaced loudly (I-11, OQ-7b); operator fixes config, hot reload re-enables. |
| Malformed channel (unknown type at runtime) | `Failed(Permanent)` | Should be impossible post-validation (I-13); treated as permanent, logged loudly — fail loudly beats silent skip (I-8 spirit). |

## `NotificationRenderer` — what the message contains (Discord V1)

```text
New invoice received
Issuer: {IssuerName | "NIP " + IssuerNip}
Invoice no.: {InvoiceNumber}
KSeF no.: {Ref}
Amount: {GrossAmount | NetAmount per AmountDisplay} {Currency}
```

- Exactly the fields the payload carries — no lookups, no enrichment (OQ-1); no advisory/action texts, per the OQ-16 rendering rule.
- `IssuerName` omitted iff absent in the payload; the NIP line always present (identification guarantee — OQ-1 surface).

## Deliberate absences (justified)

- **Aggregates / entities — none.** Stateless delivery; no invariants over time (a message is either delivered-and-acknowledged or not — there is nothing to keep consistent between calls).
- **Domain events — none.** `DeliveryConfirmed/DeliveryFailed` are *return values of a Customer–Supplier call*, not events (Step 7); the cycle's events (`InvoicesNotified`) belong to Invoice Watching.
- **Read models — none.** Nothing is queried.

## Open questions (carried, none new)

- **OQ-7a/7b resolved — heartbeat adopted in V1:** a daily per-subject *"no new invoices (as of {date})"* pulse sent through the normal `INotifier` path (a new *caller* of the port — the scheduler — not a change to this tactical model); a missing expected pulse is the alarm, and it replaces a dedicated fallback channel for V1. Loud `Failed(Permanent)` logs remain the fast local signal (I-11).
- **OQ-11 resolved:** sequential sends with a fixed 3 s delay live in the *cycle's batch loop* (caller side), not inside ND — ≤ 20 msg/min, safely under the webhook's ~30/min; a stray 429 falls into the OQ-17c backoff. Hardcoded V1.