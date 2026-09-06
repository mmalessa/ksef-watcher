using KsefWatcher.InvoiceWatching.ValueObjects;
using KsefWatcher.SubjectConfiguration;

namespace KsefWatcher.Host.Scheduling;

/// <summary>
/// Maps a validated <see cref="ChannelConfig"/> onto the <see cref="ChannelRef"/> port signature.
/// Shared by <see cref="PollingBackgroundService"/> and <see cref="HeartbeatScheduler"/> so the
/// webhook-priority rule (webhook wins over bot token/channel ID when both are configured) lives
/// in exactly one place.
/// </summary>
public static class ChannelConfigExtensions
{
    public static ChannelRef ToChannelRef(this ChannelConfig config) =>
        string.IsNullOrWhiteSpace(config.WebhookUrl)
            ? new ChannelRef(config.Type, config.ChannelId ?? string.Empty, config.Token)
            : new ChannelRef(config.Type, config.WebhookUrl, Credential: null);
}
