# Step 6 — Organise

**Status:** ⏳ drafted

## Reality check

Solo open-source project, no real organization behind it — so a real Team Topologies session does not apply. The hypothetical team-ownership table from the earlier draft was cut as low-value for a solo project (decision — see conversation log); what remains is the part that survives solo development and *does* shape the code.

## Conway's-law takeaways that apply even solo

In an open-source project, "teams" are effectively **contributor clusters** around PRs — and the module boundaries decide whether a contribution stays local or leaks across the domain. The boundaries must survive as **code/module boundaries** so that:

1. A contributor adding a Slack notifier touches *only* Notification Delivery — never Watching. (The `Notifier` interface is the seam; PG-4.)
2. A contributor fixing KSeF session handling touches *only* KSeF Access — Watching must not know sessions exist.
3. The cursor/registry logic (Watching) is testable with **faked** boundaries (list provider + notifier fakes) — no KSeF sandbox, no Discord webhook needed in tests. This is the single most valuable consequence of this step for a solo project.

These three points become the practical acceptance criteria for Step 9's architecture decisions.