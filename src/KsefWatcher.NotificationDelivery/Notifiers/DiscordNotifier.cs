using System.Net.Http.Headers;
using System.Text;
using System.Text.Json;
using KsefWatcher.InvoiceWatching.ValueObjects;

namespace KsefWatcher.NotificationDelivery.Notifiers;

/// <summary>
/// Thin ACL over the Discord Bot API and webhooks (docs/08_notification_delivery_tactical_model.md)
/// — the only place Discord-specific concepts appear. V1's only notifier. Receives an
/// already-rendered message (<see cref="NotificationRenderer"/> runs in <c>DeliveryService</c>,
/// upstream) — this class only knows how to post text to a channel (bot token + channel ID, or a
/// webhook URL when <see cref="ChannelRef.Credential"/> is null) and classify the raw transport outcome.
/// </summary>
public sealed class DiscordNotifier(IHttpClientFactory httpClientFactory) : IChannelSender
{
    public string ChannelType => "discord";

    public async Task<ChannelSendOutcome> SendAsync(ChannelRef channel, string message, CancellationToken cancellationToken)
    {
        var payload = JsonSerializer.Serialize(new { content = message });
        var isWebhook = channel.Credential is null; // webhook URL embeds its own auth; bot mode always has a token (ConfigLoader requires it)
        var url = isWebhook ? channel.Target : $"https://discord.com/api/v10/channels/{channel.Target}/messages";
        using var request = new HttpRequestMessage(HttpMethod.Post, url)
        {
            Content = new StringContent(payload, Encoding.UTF8, "application/json"),
        };
        if (!isWebhook)
        {
            request.Headers.Authorization = new AuthenticationHeaderValue("Bot", channel.Credential);
        }

        try
        {
            // A fresh client per call (not cached on this class) — this class is a long-lived
            // singleton, and only calling CreateClient() per use lets IHttpClientFactory actually
            // rotate the underlying handler (stale DNS, dead connections) as designed.
            var httpClient = httpClientFactory.CreateClient(nameof(DiscordNotifier));
            var response = await httpClient.SendAsync(request, cancellationToken);

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
