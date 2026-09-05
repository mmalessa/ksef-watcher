namespace KsefWatcher.InvoiceWatching.ValueObjects;

/// <summary>
/// Telemetry for one <c>PollCycle.RunAsync</c> call — Host logs from this, the core domain never
/// takes a logging dependency (docs/06_organise.md: Invoice Watching stays zero-PackageReference).
/// </summary>
/// <param name="IsBaseline">True when this poll established the subject's first HWM (I-18) — no invoice can be new on this cycle by design.</param>
/// <param name="FetchedCount">Total invoices returned by the window fetch, new or already known.</param>
/// <param name="DetectedCount">Of those, how many were unseen (I-23) — 0 for a baseline cycle.</param>
/// <param name="NotifiedCount">How many were actually delivered before the cycle stopped (may be less than <see cref="DetectedCount"/> on a delivery failure).</param>
/// <param name="Hwm">The new HWM if it moved this cycle (baseline confirm or full-window cursor advance); null if the cycle stopped early before advancing.</param>
public sealed record PollOutcome(bool IsBaseline, int FetchedCount, int DetectedCount, int NotifiedCount, Hwm? Hwm);
