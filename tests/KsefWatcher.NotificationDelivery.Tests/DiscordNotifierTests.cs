using System.Net;
using KsefWatcher.InvoiceWatching.ValueObjects;
using KsefWatcher.NotificationDelivery.Notifiers;
using KsefWatcher.NotificationDelivery.Tests.TestDoubles;
using Xunit;

namespace KsefWatcher.NotificationDelivery.Tests;

public class DiscordNotifierTests
{
    private static ChannelRef AnyChannel => new("discord", "123456789012345678", "bot-token-abc");

    [Fact]
    public void ChannelType_IsDiscord()
    {
        var sut = new DiscordNotifier(new FakeHttpClientFactory(() => new HttpClient(new FakeHttpMessageHandler(_ => new HttpResponseMessage(HttpStatusCode.NoContent)))));

        Assert.Equal("discord", sut.ChannelType);
    }

    [Fact]
    public async Task SendAsync_ResolvesAFreshClientPerCall_ViaHttpClientFactory()
    {
        var factory = new FakeHttpClientFactory(() => new HttpClient(new FakeHttpMessageHandler(_ => new HttpResponseMessage(HttpStatusCode.NoContent))));
        var sut = new DiscordNotifier(factory);

        await sut.SendAsync(AnyChannel, "first", CancellationToken.None);
        await sut.SendAsync(AnyChannel, "second", CancellationToken.None);

        Assert.Equal(2, factory.RequestedNames.Count); // not cached across calls — lets IHttpClientFactory rotate handlers
    }

    [Fact]
    public async Task Success_PostsToChannelTarget_WithMessageAsJsonContent_ReturnsAcknowledged()
    {
        var handler = new FakeHttpMessageHandler(_ => new HttpResponseMessage(HttpStatusCode.NoContent));
        var sut = new DiscordNotifier(new FakeHttpClientFactory(() => new HttpClient(handler)));

        var outcome = await sut.SendAsync(AnyChannel, "New invoice received", CancellationToken.None);

        Assert.IsType<ChannelSendOutcome.Acknowledged>(outcome);
        Assert.Equal($"https://discord.com/api/v10/channels/{AnyChannel.Target}/messages", handler.LastRequest!.RequestUri!.ToString());
        Assert.Equal("Bot", handler.LastRequest.Headers.Authorization?.Scheme);
        Assert.Equal(AnyChannel.Credential, handler.LastRequest.Headers.Authorization?.Parameter);
        Assert.Contains("New invoice received", handler.LastRequestBody);
    }

    [Fact]
    public async Task WebhookMode_PostsDirectlyToTargetUrl_WithoutAuthorizationHeader_ReturnsAcknowledged()
    {
        var webhookChannel = new ChannelRef("discord", "https://discord.com/api/webhooks/123/abc", Credential: null);
        var handler = new FakeHttpMessageHandler(_ => new HttpResponseMessage(HttpStatusCode.NoContent));
        var sut = new DiscordNotifier(new FakeHttpClientFactory(() => new HttpClient(handler)));

        var outcome = await sut.SendAsync(webhookChannel, "New invoice received", CancellationToken.None);

        Assert.IsType<ChannelSendOutcome.Acknowledged>(outcome);
        Assert.Equal(webhookChannel.Target, handler.LastRequest!.RequestUri!.ToString());
        Assert.Null(handler.LastRequest.Headers.Authorization);
        Assert.Contains("New invoice received", handler.LastRequestBody);
    }

    [Theory]
    [InlineData(HttpStatusCode.TooManyRequests)]
    [InlineData(HttpStatusCode.NotFound)]
    [InlineData(HttpStatusCode.InternalServerError)]
    public async Task NonSuccessStatusCode_ReturnsHttpFailure_WithTheStatusCode(HttpStatusCode statusCode)
    {
        var handler = new FakeHttpMessageHandler(_ => new HttpResponseMessage(statusCode));
        var sut = new DiscordNotifier(new FakeHttpClientFactory(() => new HttpClient(handler)));

        var outcome = await sut.SendAsync(AnyChannel, "message", CancellationToken.None);

        var failure = Assert.IsType<ChannelSendOutcome.HttpFailure>(outcome);
        Assert.Equal((int)statusCode, failure.StatusCode);
    }

    [Fact]
    public async Task ConnectionRefused_ReturnsTransportFailure()
    {
        var handler = new FakeHttpMessageHandler(_ => throw new HttpRequestException("Connection refused"));
        var sut = new DiscordNotifier(new FakeHttpClientFactory(() => new HttpClient(handler)));

        var outcome = await sut.SendAsync(AnyChannel, "message", CancellationToken.None);

        Assert.IsType<ChannelSendOutcome.TransportFailure>(outcome);
    }

    [Fact]
    public async Task Timeout_ReturnsTransportFailure()
    {
        var handler = new FakeHttpMessageHandler(_ => throw new TaskCanceledException("Timed out", new TimeoutException()));
        var sut = new DiscordNotifier(new FakeHttpClientFactory(() => new HttpClient(handler)));

        var outcome = await sut.SendAsync(AnyChannel, "message", CancellationToken.None);

        Assert.IsType<ChannelSendOutcome.TransportFailure>(outcome);
    }
}
