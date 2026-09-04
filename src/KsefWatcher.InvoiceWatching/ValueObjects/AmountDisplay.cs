namespace KsefWatcher.InvoiceWatching.ValueObjects;

/// <summary>
/// Per-subject config parameter (OQ-16, docs/08_notification_delivery_tactical_model.md):
/// which amount <c>DetectedInvoice</c> renders as. Default is <see cref="Brutto"/>
/// (docs/08_subject_configuration_tactical_model.md).
/// </summary>
public enum AmountDisplay
{
    Brutto,
    Netto,
}
