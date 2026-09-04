using KsefWatcher.InvoiceWatching.ValueObjects;

namespace KsefWatcher.InvoiceWatching.Ports;

/// <summary>
/// Owned by Invoice Watching, implemented by Notification Delivery
/// (docs/08_invoice_watching_domain_services.md; ownership clarified in docs/09_architecture.md).
/// Carries the whole <see cref="DetectedInvoice"/> record, not extracted scalars, so a future
/// notifier can render more of it without a signature change (pluggable-service lesson).
/// </summary>
public interface INotifier
{
    Task<DeliveryResult> SendAsync(ChannelRef channel, DetectedInvoice invoice, AmountDisplay amountDisplay, CancellationToken cancellationToken);

    /// <summary>
    /// The daily watchdog pulse (OQ-7a/7b, docs/09_architecture.md "Scheduler") — a second caller
    /// of this same port, not an invoice notification. A missing expected pulse is the alarm.
    /// </summary>
    Task<DeliveryResult> SendHeartbeatAsync(ChannelRef channel, DateOnly asOf, CancellationToken cancellationToken);
}
