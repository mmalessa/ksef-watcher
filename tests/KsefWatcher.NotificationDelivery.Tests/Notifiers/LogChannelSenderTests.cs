using KsefWatcher.InvoiceWatching.ValueObjects;
using KsefWatcher.NotificationDelivery.Notifiers;
using KsefWatcher.NotificationDelivery.Tests.TestDoubles;
using Microsoft.Extensions.Logging;
using Xunit;

namespace KsefWatcher.NotificationDelivery.Tests.Notifiers;

public class LogChannelSenderTests
{
    private static ChannelRef AnyChannel => new("logs", string.Empty);

    [Fact]
    public void ChannelType_IsLogs()
    {
        var sut = new LogChannelSender(new FakeLogger<LogChannelSender>());

        Assert.Equal("logs", sut.ChannelType);
    }

    [Fact]
    public async Task SendAsync_LogsTheMessage_ReturnsAcknowledged()
    {
        var logger = new FakeLogger<LogChannelSender>();
        var sut = new LogChannelSender(logger);

        var outcome = await sut.SendAsync(AnyChannel, "New invoice received", CancellationToken.None);

        Assert.IsType<ChannelSendOutcome.Acknowledged>(outcome);
        Assert.Contains(logger.Entries, e => e.Level == LogLevel.Information && e.Message.Contains("New invoice received"));
    }
}
