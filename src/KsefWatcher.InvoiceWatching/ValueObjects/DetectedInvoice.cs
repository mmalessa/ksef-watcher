namespace KsefWatcher.InvoiceWatching.ValueObjects;

/// <summary>
/// The notification payload for one invoice (docs/08_invoice_watching_value_objects.md, I-12).
/// Presentation-agnostic: carries both amounts; the netto-vs-brutto display choice is a
/// per-subject config parameter (<see cref="AmountDisplay"/>) consumed at render time —
/// never part of this payload. Same shape crosses the KSeF Access → Invoice Watching boundary
/// unchanged (docs/08_ksef_access_tactical_model.md) — this is the shared Published-Language type.
/// </summary>
public sealed record DetectedInvoice(
    InvoiceReference Ref,
    string InvoiceNumber,
    decimal NetAmount,
    decimal GrossAmount,
    string Currency,
    string IssuerNip,
    string? IssuerName);
