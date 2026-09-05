using KsefWatcher.InvoiceWatching.ValueObjects;
using Microsoft.Extensions.Logging;

namespace KsefWatcher.NotificationDelivery.Notifiers;

/// <summary>
/// Dev/testing-only channel (todo.md): writes the rendered notification to the daemon's own log
/// instead of a real messenger, so a subject can be watched without a Discord webhook configured.
/// Always acknowledges — there is no transport to fail.
/// </summary>
public sealed class LogChannelSender(ILogger<LogChannelSender> logger) : IChannelSender
{
    public string ChannelType => "logs";

    public Task<ChannelSendOutcome> SendAsync(ChannelRef channel, string message, CancellationToken cancellationToken)
    {
        logger.LogInformation("[logs channel] {Message}", message);
        return Task.FromResult<ChannelSendOutcome>(new ChannelSendOutcome.Acknowledged());
    }
}
