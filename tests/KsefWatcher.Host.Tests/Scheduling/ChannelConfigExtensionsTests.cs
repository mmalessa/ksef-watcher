using KsefWatcher.Host.Scheduling;
using KsefWatcher.SubjectConfiguration;
using Xunit;

namespace KsefWatcher.Host.Tests.Scheduling;

public class ChannelConfigExtensionsTests
{
    [Fact]
    public void WithoutWebhookUrl_UsesChannelIdAsTargetAndTokenAsCredential()
    {
        var config = new ChannelConfig { Type = "discord", Token = "bot-token", ChannelId = "111111111111111111" };

        var channel = config.ToChannelRef();

        Assert.Equal("discord", channel.Type);
        Assert.Equal("111111111111111111", channel.Target);
        Assert.Equal("bot-token", channel.Credential);
    }

    [Fact]
    public void WithWebhookUrl_UsesWebhookUrlAsTargetAndNullCredential()
    {
        var config = new ChannelConfig { Type = "discord", WebhookUrl = "https://discord.com/api/webhooks/123/abc" };

        var channel = config.ToChannelRef();

        Assert.Equal("discord", channel.Type);
        Assert.Equal("https://discord.com/api/webhooks/123/abc", channel.Target);
        Assert.Null(channel.Credential);
    }

    [Fact]
    public void WithBothWebhookUrlAndTokenChannelId_WebhookTakesPriority()
    {
        var config = new ChannelConfig
        {
            Type = "discord",
            Token = "bot-token",
            ChannelId = "111111111111111111",
            WebhookUrl = "https://discord.com/api/webhooks/123/abc",
        };

        var channel = config.ToChannelRef();

        Assert.Equal("https://discord.com/api/webhooks/123/abc", channel.Target);
        Assert.Null(channel.Credential);
    }
}
