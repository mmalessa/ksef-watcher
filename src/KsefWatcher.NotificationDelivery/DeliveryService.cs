using System.Diagnostics;
using KsefWatcher.InvoiceWatching.Ports;
using KsefWatcher.InvoiceWatching.ValueObjects;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;

namespace KsefWatcher.NotificationDelivery;

/// <summary>
/// Implementation of <see cref="INotifier"/> (docs/08_notification_delivery_tactical_model.md).
/// Resolves the sender for <c>ChannelRef.Type</c>, delegates one attempt, maps the transport
/// outcome to a <see cref="DeliveryResult"/>. Single attempt — no retry loop here (OQ-17c: retry
/// lives with the caller).
/// </summary>
public sealed class DeliveryService(IEnumerable<IChannelSender> senders, ILogger<DeliveryService>? logger = null) : INotifier
{
    private readonly Dictionary<string, IChannelSender> _sendersByType = senders.ToDictionary(s => s.ChannelType);
    private readonly ILogger<DeliveryService> _logger = logger ?? NullLogger<DeliveryService>.Instance;

    public Task<DeliveryResult> SendAsync(ChannelRef channel, DetectedInvoice invoice, AmountDisplay amountDisplay, CancellationToken cancellationToken) =>
        ClassifyAndSendAsync(channel, NotificationRenderer.Render(invoice, amountDisplay), cancellationToken);

    public Task<DeliveryResult> SendHeartbeatAsync(ChannelRef channel, DateOnly asOf, CancellationToken cancellationToken) =>
        ClassifyAndSendAsync(channel, NotificationRenderer.RenderHeartbeat(asOf), cancellationToken);

    private async Task<DeliveryResult> ClassifyAndSendAsync(ChannelRef channel, string message, CancellationToken cancellationToken)
    {
        // Malformed/unknown channel type: should be impossible post-validation (I-13) — treated
        // as permanent and never retried, rather than throwing (fail loudly beats silent skip, I-8 spirit).
        if (!_sendersByType.TryGetValue(channel.Type, out var sender))
        {
            return LogPermanentFailure(channel.Type, "no sender registered");
        }

        var outcome = await sender.SendAsync(channel, message, cancellationToken);

        return outcome switch
        {
            ChannelSendOutcome.Acknowledged => new DeliveryResult.Confirmed(),
            ChannelSendOutcome.HttpFailure(var code) when code is 429 or >= 500 and < 600 =>
                new DeliveryResult.Failed(DeliveryResult.FailureKind.Retryable),
            ChannelSendOutcome.HttpFailure(var code) => LogPermanentFailure(channel.Type, $"HTTP {code}"),
            ChannelSendOutcome.TransportFailure => new DeliveryResult.Failed(DeliveryResult.FailureKind.Retryable),
            _ => throw new UnreachableException(),
        };
    }

    private DeliveryResult.Failed LogPermanentFailure(string channelType, string reason)
    {
        _logger.LogError("Permanent delivery failure for channel type {ChannelType}: {Reason} (I-11).", channelType, reason);
        return new DeliveryResult.Failed(DeliveryResult.FailureKind.Permanent);
    }
}
