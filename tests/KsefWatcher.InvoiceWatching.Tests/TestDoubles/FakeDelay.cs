using KsefWatcher.InvoiceWatching.Ports;

namespace KsefWatcher.InvoiceWatching.Tests.TestDoubles;

/// <summary>Records requested wait durations but never actually sleeps — keeps retry/backoff tests fast.</summary>
public sealed class FakeDelay : IDelay
{
    public List<TimeSpan> Requested { get; } = [];

    public Task WaitAsync(TimeSpan duration, CancellationToken cancellationToken)
    {
        Requested.Add(duration);
        return Task.CompletedTask;
    }
}
