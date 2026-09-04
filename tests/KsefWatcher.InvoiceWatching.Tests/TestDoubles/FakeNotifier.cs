using KsefWatcher.InvoiceWatching.Ports;
using KsefWatcher.InvoiceWatching.ValueObjects;

namespace KsefWatcher.InvoiceWatching.Tests.TestDoubles;

/// <summary><paramref name="resultForAttempt"/>: attempt is 1-based, reset per invoice.</summary>
public sealed class FakeNotifier(Func<DetectedInvoice, int, DeliveryResult> resultForAttempt) : INotifier
{
    private readonly Dictionary<InvoiceReference, int> _attemptsByRef = [];

    public List<(ChannelRef Channel, DetectedInvoice Invoice, AmountDisplay AmountDisplay)> Calls { get; } = [];

    public Task<DeliveryResult> SendAsync(ChannelRef channel, DetectedInvoice invoice, AmountDisplay amountDisplay, CancellationToken cancellationToken)
    {
        Calls.Add((channel, invoice, amountDisplay));

        _attemptsByRef.TryGetValue(invoice.Ref, out var previousAttempts);
        var attempt = previousAttempts + 1;
        _attemptsByRef[invoice.Ref] = attempt;

        return Task.FromResult(resultForAttempt(invoice, attempt));
    }

    public Task<DeliveryResult> SendHeartbeatAsync(ChannelRef channel, DateOnly asOf, CancellationToken cancellationToken) =>
        throw new NotSupportedException("PollCycle never sends heartbeats — that's HeartbeatScheduler's job.");
}
