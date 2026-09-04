namespace KsefWatcher.InvoiceWatching.ValueObjects;

/// <summary>
/// The port's return shape (docs/08_invoice_watching_value_objects.md) — Published Language of
/// the Invoice Watching ↔ KSeF Access contract; constructed by KSeF Access, consumed here.
/// </summary>
public sealed record FetchedWindow(
    IReadOnlySet<InvoiceReference> Refs,
    IReadOnlyList<DetectedInvoice> Detected,
    Hwm Hwm);
