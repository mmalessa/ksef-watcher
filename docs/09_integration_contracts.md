# Step 9b — Architecture: Integration Contracts

**Status:** ✅ reviewed & accepted

**Decision: no integration-event contract machinery for this system.** Mirrors the style of `08_subject_configuration_tactical_model.md` ("deliberately none, justified") rather than inventing schemas nobody needs.

## Why this artifact stays thin

`08_invoice_watching_domain_model.md` already ruled out integration events: this is a single-process monolith (`09_architecture.md`), so cross-context communication is in-process calls against C#-typed ports (`IInvoiceListProvider`, `INotifier`, `ISubjectWatchRepository`). Those interfaces **are** the contracts, and they're versioned by the compiler — a breaking change to a port signature is a build error at every call site, which is a stronger and simpler guarantee than any JSON Schema/Avro registry could give for an in-process call. A schema-registry-style artifact exists to protect against *independently deployed* producers/consumers drifting apart; that problem doesn't exist here (one binary, one build).

## What "contracts" actually exist here

Not integration events — two **external HTTP APIs**, each with exactly one producer and one consumer, both outside this codebase's control:

| Boundary | Producer | Consumer | Contract | Stability mechanism |
|---|---|---|---|---|
| KSeF API 2.0 | Ministry of Finance (KSeF) | `KsefWatcher.KsefAccess` | Whatever the official `CIRFMF/ksef-client-csharp` models (`IInvoiceDownloadClient.QueryInvoiceMetadataAsync`, `IAuthCoordinator.AuthKsefTokenAsync`, response shapes) | The client is **vendored** (`vendor/ksef-client-csharp`, `ProjectReference`, not a NuGet package — it's published to GitHub Packages, which would need a PAT to restore; the public git repo needs none). **Pin an exact commit/tag** on clone (`vendor/README.md`) instead of tracking `main`; bump deliberately, re-verify against the sandbox environments (`api-test`/`api-demo`, A13) before shipping a bump. `KsefClientAdapter` is the ACL enforcement point (only file referencing the vendored types); its own translation (`InvoiceSummary → DetectedInvoice`, `KsefRateLimitException → PollFailure.RateLimited`) is the only "schema" this project maintains itself. |
| Discord webhook API | Discord | `KsefWatcher.NotificationDelivery`'s `DiscordNotifier` | Discord's public webhook JSON payload — their API, not ours to version | No schema file: it's a small, stable, publicly documented payload. A breaking change on Discord's side surfaces as `Failed(Permanent)` (I-11) or a deserialization exception — same failure path already specified, nothing new to design. |

Both boundaries are already ACL points (`08_ksef_access_tactical_model.md`, `08_notification_delivery_tactical_model.md`) — this document doesn't add a second layer of contract enforcement on top of the ACL that already exists; it just names where responsibility for "did the external API change" lives.

## Validation

No JSON Schema / Avro / Protobuf, no validate-before-publish / validate-after-receive pipeline. A malformed or unexpected external response fails **implicitly** (deserialization throws) and is caught at each ACL's existing failure-classification point:

- KSeF Access → `PollFailure(ApiError)` (I-8, fail loudly — already specified, not new).
- Notification Delivery → `Failed(Permanent)` or a caught exception logged loudly (I-11 — already specified).

A dedicated schema-validation layer would protect against a producer/consumer drifting apart *silently and successfully* — irrelevant when the actual failure mode (an external government or SaaS API changing shape) already surfaces as a hard error through paths this system already treats as first-class (loud failure, not silent corruption).

## Extensibility check

Adding a second notifier (Slack, e-mail, …) or a second external data source needs **no new integration-contract artifact** — the port/ACL pattern (`08_invoice_watching_domain_services.md`) already covers it: a new adapter implements the existing port, the compiler enforces the interface, and this document's reasoning ("one producer, one consumer, external API, ACL already exists") applies unchanged.

## Future trigger to revisit (not an open question now)

If this system ever splits into more than one OS process (flagged as the redesign trigger in `09_architecture.md`'s "Cross-context communication" section), *this* is the document that would then need outbox/inbox patterns and real integration-event schemas — because at that point there would be more than one producer/consumer pair per contract, and "the compiler enforces it" stops being true. Deliberately not designed now (Simplicity) — there is no such split planned.
