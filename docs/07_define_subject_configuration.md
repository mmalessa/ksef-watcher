# Step 7e — Define: Subject Configuration (Bounded Context Canvas)

**Status:** ✅ reviewed & accepted (steps 1–7 pass) · **Classification:** Generic (thin)

| Field | Value |
|---|---|
| **Name** | Subject Configuration |
| **Purpose** | Own the language and validation of the daemon's configuration: which subjects to watch, how often, with which credentials and channels; watch the file and **hot-reload** changes without restart (OQ-3, resolved). |
| **Strategic classification** | Generic. Evolution: commodity (file format + validation); must never grow domain logic (Step 4). |
| **Domain roles** | Published-Language provider; boot-time gatekeeper (invalid config ⇒ refuse to start); runtime watcher of the config file (hot reload). |

## Inbound communication

| From | What arrives | Pattern |
|---|---|---|
| Operator (human) | The config file — **YAML** (`config.yaml`), searched in order: the binary's directory (`./config.yaml`), then `/etc/ksef-watcher/config.yaml` (A12, resolves OQ-14) | manual artifact |
| Daemon startup | Load & validate request | internal |
| File watcher | Config file changed on disk | internal trigger (auto-reload, OQ-3; mechanism → Step 9) |

## Outbound communication

| To | What leaves | Pattern |
|---|---|---|
| All contexts | Validated, typed configuration: subject list (NIP, KSeF env, credentials ref, interval), channel list (type, target) | Published Language (read-only) |

Configuration data is *read-side input* everywhere (Step 5 conclusion): no context mutates it at runtime; changes arrive via **hot reload** — the daemon watches the file, validates, and republishes (Scenario D in `05_connect_message_flows.md`).

## Ubiquitous language

| Term | Meaning |
|---|---|
| **Subject** | A watched company: NIP + KSeF credentials + interval + channel binding. |
| **Interval** | Per-subject polling period (Step 1 decision: per-subject), **expressed in minutes**; default 60 (OQ-19, resolved — A7). |
| **Poll offset** | Deterministic position of a subject's poll within its interval window: derived from subject identity (`hash(NIP) mod interval`), stable across restarts and reloads — the mechanism that spreads simultaneous intervals into a smooth stream (A9). |
| **Poll budget** | Upper bound on KSeF polls — **per subject, not global**: KSeF counts limits per pair (context + IP), so each subject (NIP context) has its own 20 req/h budget on the simplified-list endpoint (A7, verified). Enforced as a minimum-interval rule (I-13a): interval ≥ 15 min ⇒ ≤ 4 list calls/hour, well within budget together with session calls (120 req/h). |
| **Channel** | A notification target descriptor (e.g. `discord-webhook: <url>`). |
| **Amount display** | Per-subject notification rendering parameter: which amount the message shows — `brutto` (default) or `netto` (OQ-16, resolved). A display setting, not data: the payload always carries both amounts + currency; this parameter is consumed by Notification Delivery at render time. |
| **Environment** | KSeF test vs production: **file-level `defaultEnvironment` (default `test`) + per-subject override** (OQ-9, resolved) — new subjects inherit the safe default; going prod is explicit. |

*(Terminology note: "Subject" and "Channel" are shared with other contexts' canvases deliberately — this context is the definition source; others consume.)*

## Business decisions (invariants)

1. **I-13 Fail-fast at startup:** invalid config (bad NIP, missing credentials, non-positive interval, unknown channel type) ⇒ no startup, precise error. A daemon that starts half-configured silently breaks PG-2/PG-3.
2. **I-13a Minimum interval:** validation **rejects intervals below 15 minutes** — MF explicitly recommends the cyclic sync interval not be shorter than 15 min per subject (verified, A7). At 15 min a subject consumes ≤ 4 list calls/h + 4 session-open/close pairs/h, safely inside its per-subject budgets (20 req/h metadata, 120 req/h session endpoints).
3. **I-13b Maximum interval:** validation **rejects intervals above 10080 minutes (7 days)** — operator decision, no KSeF-side driver (unlike I-13a). Guards against a config typo (e.g. an accidental extra zero) silently producing a near-dormant subject, and keeps every subject checked at least weekly regardless of misconfiguration — a looser bound than PG-1's normal-case timeliness goal, which the default (60 min) already serves.
4. **I-14 Credentials never logged:** validation errors must not echo secrets (Project Values: security/openness).
5. **I-15 Schema is the contract:** the file schema version is explicit; breaking changes bump it.
6. **I-16 Keep-last-valid on reload:** an invalid file detected on hot reload ⇒ keep the last valid configuration in effect and log the error loudly; never partially apply a file. (Fail-fast I-13 applies only to the initial startup, not to reloads.)
7. **I-17 Full-file atomic reload:** a reload always applies the *entire* validated file (all subjects), never per-entry merges from a broken file.
8. **I-21 Per-subject poll budget:** the poll budget is **per subject (NIP context), not global** — KSeF counts rate limits per pair (context + IP), and each subject has its own endpoint budgets (A7, verified). Consequently: (a) the fleet size is *not* budget-limited (a daemon with 100 subjects is fine if each respects I-13a); (b) there is **no global cap** in validation; (c) poll offsets (A9) remain as load-smoothing politeness. *Correction of the earlier draft:* the previous global "~10 polls/h across all subjects" bound was based on an unverified estimate and was wrong about how KSeF counts limits — superseded by this per-subject reading. Watch-out: MF monitors *patterns of circumvention* (e.g. systematically using many IPs for one context) — irrelevant to us (one daemon, one IP, many contexts), but noted so nobody "optimizes" into it.

## Assumptions

- Operator-edited file (A5); auto-reload via file watching (decision: automatic, no signal required — OQ-3 resolved).
- **Credentials (OQ-13, resolved): both** — the schema accepts a literal token or an `${ENV_VAR}` reference; the config loader resolves `${...}` from the environment, treats anything else as the literal value. Defaults documented as literal + `chmod 600` + dedicated owner; env-var refs for operators with no-secrets-in-config policies. I-14 (never logged) covers both forms — logs never echo the raw token nor the resolved value; a validation error references the field path only.

## Open questions

- **OQ-15** *Resolved: baseline afresh on re-add* — removing a subject from config deliberately resets its state (I-19, updated); re-adding runs a fresh baseline (I-18). *Supersedes the earlier "resume from retained state" default.* Removal is a conscious operator choice: the absence period's notifications are relinquished by that choice.
- **OQ-14** Config file format (YAML vs TOML) — **resolved:** YAML, `config.yaml`, search paths per A12 (`prompt.md` operator decision).
- **OQ-13** Credentials — **resolved: both** (literal or `${ENV_VAR}`, loader resolves; see Assumptions above).