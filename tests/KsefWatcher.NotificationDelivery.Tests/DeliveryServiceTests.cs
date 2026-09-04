using KsefWatcher.InvoiceWatching.ValueObjects;
using KsefWatcher.NotificationDelivery;
using KsefWatcher.NotificationDelivery.Tests.TestDoubles;
using Microsoft.Extensions.Logging;
using Xunit;

namespace KsefWatcher.NotificationDelivery.Tests;

public class DeliveryServiceTests
{
    private static readonly ChannelRef DiscordChannel = new("discord", "https://example.invalid/webhook");

    private static DetectedInvoice AnyInvoice() =>
        new(new InvoiceReference("KSEF-1"), "FV/1", 100m, 123m, "PLN", "1111111111", "Contractor");

    [Fact]
    public async Task Acknowledged_ReturnsConfirmed_SendsRenderedMessageToMatchingSender()
    {
        var sender = new FakeChannelSender("discord", new ChannelSendOutcome.Acknowledged());
        var sut = new DeliveryService([sender]);

        var result = await sut.SendAsync(DiscordChannel, AnyInvoice(), AmountDisplay.Brutto, CancellationToken.None);

        Assert.IsType<DeliveryResult.Confirmed>(result);
        var call = Assert.Single(sender.Calls);
        Assert.Equal(DiscordChannel, call.Channel);
        Assert.Contains("123", call.Message);
    }

    [Theory]
    [InlineData(429)]
    [InlineData(500)]
    [InlineData(503)]
    public async Task HttpFailure_429Or5xx_IsClassifiedRetryable(int statusCode)
    {
        var sender = new FakeChannelSender("discord", new ChannelSendOutcome.HttpFailure(statusCode));
        var sut = new DeliveryService([sender]);

        var result = await sut.SendAsync(DiscordChannel, AnyInvoice(), AmountDisplay.Brutto, CancellationToken.None);

        var failed = Assert.IsType<DeliveryResult.Failed>(result);
        Assert.Equal(DeliveryResult.FailureKind.Retryable, failed.Kind);
    }

    [Theory]
    [InlineData(404)]
    [InlineData(401)]
    public async Task HttpFailure_4xxNot429_IsClassifiedPermanent(int statusCode)
    {
        var sender = new FakeChannelSender("discord", new ChannelSendOutcome.HttpFailure(statusCode));
        var sut = new DeliveryService([sender]);

        var result = await sut.SendAsync(DiscordChannel, AnyInvoice(), AmountDisplay.Brutto, CancellationToken.None);

        var failed = Assert.IsType<DeliveryResult.Failed>(result);
        Assert.Equal(DeliveryResult.FailureKind.Permanent, failed.Kind);
    }

    [Fact]
    public async Task HttpFailure_4xxNot429_LogsError()
    {
        var sender = new FakeChannelSender("discord", new ChannelSendOutcome.HttpFailure(404));
        var logger = new FakeLogger<DeliveryService>();
        var sut = new DeliveryService([sender], logger);

        await sut.SendAsync(DiscordChannel, AnyInvoice(), AmountDisplay.Brutto, CancellationToken.None);

        var entry = Assert.Single(logger.Entries);
        Assert.Equal(LogLevel.Error, entry.Level);
    }

    [Fact]
    public async Task TransportFailure_IsClassifiedRetryable()
    {
        var sender = new FakeChannelSender("discord", new ChannelSendOutcome.TransportFailure());
        var sut = new DeliveryService([sender]);

        var result = await sut.SendAsync(DiscordChannel, AnyInvoice(), AmountDisplay.Brutto, CancellationToken.None);

        var failed = Assert.IsType<DeliveryResult.Failed>(result);
        Assert.Equal(DeliveryResult.FailureKind.Retryable, failed.Kind);
    }

    [Fact]
    public async Task UnknownChannelType_IsClassifiedPermanent_WithoutCallingAnySender()
    {
        var sender = new FakeChannelSender("discord", new ChannelSendOutcome.Acknowledged());
        var sut = new DeliveryService([sender]);
        var unknownChannel = new ChannelRef("slack", "https://example.invalid/slack");

        var result = await sut.SendAsync(unknownChannel, AnyInvoice(), AmountDisplay.Brutto, CancellationToken.None);

        var failed = Assert.IsType<DeliveryResult.Failed>(result);
        Assert.Equal(DeliveryResult.FailureKind.Permanent, failed.Kind);
        Assert.Empty(sender.Calls);
    }

    [Fact]
    public async Task UnknownChannelType_LogsError()
    {
        var sender = new FakeChannelSender("discord", new ChannelSendOutcome.Acknowledged());
        var logger = new FakeLogger<DeliveryService>();
        var sut = new DeliveryService([sender], logger);
        var unknownChannel = new ChannelRef("slack", "https://example.invalid/slack");

        await sut.SendAsync(unknownChannel, AnyInvoice(), AmountDisplay.Brutto, CancellationToken.None);

        var entry = Assert.Single(logger.Entries);
        Assert.Equal(LogLevel.Error, entry.Level);
    }

    [Fact]
    public async Task SendHeartbeatAsync_SendsRenderedHeartbeatMessage_ToMatchingSender()
    {
        var sender = new FakeChannelSender("discord", new ChannelSendOutcome.Acknowledged());
        var sut = new DeliveryService([sender]);
        var asOf = new DateOnly(2026, 1, 15);

        var result = await sut.SendHeartbeatAsync(DiscordChannel, asOf, CancellationToken.None);

        Assert.IsType<DeliveryResult.Confirmed>(result);
        var call = Assert.Single(sender.Calls);
        Assert.Equal(DiscordChannel, call.Channel);
        Assert.Contains("2026-01-15", call.Message);
    }
}
