using KsefWatcher.InvoiceWatching.ValueObjects;

namespace KsefWatcher.NotificationDelivery;

/// <summary>
/// Renders <see cref="DetectedInvoice"/> + <see cref="AmountDisplay"/> into medium-native text
/// (docs/08_notification_delivery_tactical_model.md). Rule (OQ-16): factual invoice info + the
/// chosen amount + currency only — no advisory texts.
/// </summary>
public static class NotificationRenderer
{
    public static string Render(DetectedInvoice invoice, AmountDisplay amountDisplay)
    {
        var issuer = string.IsNullOrEmpty(invoice.IssuerName) ? $"NIP {invoice.IssuerNip}" : invoice.IssuerName;
        var amount = amountDisplay == AmountDisplay.Netto ? invoice.NetAmount : invoice.GrossAmount;

        return $"""
            New invoice received
            Issuer: {issuer}
            Invoice no.: {invoice.InvoiceNumber}
            KSeF no.: {invoice.Ref.KsefNumber}
            Amount: {amount} {invoice.Currency}
            """;
    }

    /// <summary>
    /// The daily watchdog pulse (OQ-7a/7b): says nothing new on purpose — it's a liveness pulse,
    /// not a report. A missing expected pulse is the alarm, not its content.
    /// </summary>
    public static string RenderHeartbeat(DateOnly asOf) =>
        $"No new invoices (as of {asOf:yyyy-MM-dd})";
}
