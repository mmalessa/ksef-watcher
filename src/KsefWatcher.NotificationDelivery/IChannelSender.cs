using KsefWatcher.InvoiceWatching.ValueObjects;

namespace KsefWatcher.NotificationDelivery;

/// <summary>
/// The ACL seam for one messenger family (docs/08_notification_delivery_tactical_model.md:
/// "Each adapter is a thin ACL over its messenger API"). <c>DiscordNotifier</c> implements this
/// over the Discord Bot API or a webhook, resolved by <see cref="DeliveryService"/> per <c>ChannelRef.Type</c>.
/// </summary>
public interface IChannelSender
{
    /// <summary>Matches <c>ChannelRef.Type</c> (e.g. "discord").</summary>
    string ChannelType { get; }

    Task<ChannelSendOutcome> SendAsync(ChannelRef channel, string message, CancellationToken cancellationToken);
}

/// <summary>Raw transport outcome — pre-classification (see the failure table in docs/08_notification_delivery_tactical_model.md).</summary>
public abstract record ChannelSendOutcome
{
    /// <summary>Messenger acknowledged the message (I-9: only real acknowledgement confirms).</summary>
    public sealed record Acknowledged : ChannelSendOutcome;

    /// <summary>An HTTP response was received but wasn't a success.</summary>
    public sealed record HttpFailure(int StatusCode) : ChannelSendOutcome;

    /// <summary>No HTTP response at all — timeout or connection refused.</summary>
    public sealed record TransportFailure : ChannelSendOutcome;
}
