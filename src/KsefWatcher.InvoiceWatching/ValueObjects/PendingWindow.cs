namespace KsefWatcher.InvoiceWatching.ValueObjects;

/// <summary>
/// Aggregate-internal, transient (docs/08_invoice_watching_aggregates.md, docs/08_invoice_watching_value_objects.md):
/// the window currently being processed. Never persisted — see I-4/I-23 rationale.
/// </summary>
public sealed record PendingWindow(
    FetchWindow Window,
    IReadOnlySet<InvoiceReference> Refs,
    Hwm Hwm);
