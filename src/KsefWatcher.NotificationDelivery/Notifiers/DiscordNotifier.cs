using System.Text;
using System.Text.Json;
using KsefWatcher.InvoiceWatching.ValueObjects;

namespace KsefWatcher.NotificationDelivery.Notifiers;

/// <summary>
/// Thin ACL over the Discord webhook API (docs/08_notification_delivery_tactical_model.md) — the
/// only place Discord-specific concepts appear. V1's only notifier. Receives an already-rendered
/// message (<see cref="NotificationRenderer"/> runs in <c>DeliveryService</c>, upstream) — this
/// class only knows how to post text to a webhook and classify the raw transport outcome.
/// </summary>
public sealed class DiscordNotifier(HttpClient httpClient) : IChannelSender
{
    public string ChannelType => "discord";

    public async Task<ChannelSendOutcome> SendAsync(ChannelRef channel, string message, CancellationToken cancellationToken)
    {
        var payload = JsonSerializer.Serialize(new { content = message });
        using var body = new StringContent(payload, Encoding.UTF8, "application/json");

        try
        {
            var response = await httpClient.PostAsync(channel.Target, body, cancellationToken);

            return response.IsSuccessStatusCode
                ? new ChannelSendOutcome.Acknowledged()
                : new ChannelSendOutcome.HttpFailure((int)response.StatusCode);
        }
        catch (HttpRequestException)
        {
            return new ChannelSendOutcome.TransportFailure();
        }
        catch (TaskCanceledException) when (!cancellationToken.IsCancellationRequested)
        {
            // HttpClient surfaces a timeout as TaskCanceledException when the caller didn't
            // request cancellation itself — distinct from the caller's own CancellationToken firing.
            return new ChannelSendOutcome.TransportFailure();
        }
    }
}
