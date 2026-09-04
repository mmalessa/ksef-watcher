using KsefWatcher.InvoiceWatching.ValueObjects;
using KsefWatcher.NotificationDelivery;

namespace KsefWatcher.NotificationDelivery.Tests.TestDoubles;

public sealed class FakeChannelSender(string channelType, ChannelSendOutcome outcome) : IChannelSender
{
    public string ChannelType { get; } = channelType;
    public List<(ChannelRef Channel, string Message)> Calls { get; } = [];

    public Task<ChannelSendOutcome> SendAsync(ChannelRef channel, string message, CancellationToken cancellationToken)
    {
        Calls.Add((channel, message));
        return Task.FromResult(outcome);
    }
}
