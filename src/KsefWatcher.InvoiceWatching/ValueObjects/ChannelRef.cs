namespace KsefWatcher.InvoiceWatching.ValueObjects;

/// <summary>
/// Identifies a configured notification channel (type + already-resolved target reference,
/// e.g. a Discord webhook URL) — docs/08_notification_delivery_tactical_model.md.
/// Lives here (not in Notification Delivery) because it is part of the <c>INotifier</c> port
/// signature, and Invoice Watching owns that port (docs/09_architecture.md, "Clarifications").
/// Resolved from validated config by the Host composition root — never read from config by
/// Notification Delivery itself.
/// </summary>
public sealed record ChannelRef(string Type, string Target);
