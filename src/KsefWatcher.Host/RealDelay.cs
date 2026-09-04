using KsefWatcher.InvoiceWatching.Ports;

namespace KsefWatcher.Host;

/// <summary>Production <see cref="IDelay"/> — real wall-clock waits for PollCycle's backoff/inter-message delay.</summary>
public sealed class RealDelay : IDelay
{
    public Task WaitAsync(TimeSpan duration, CancellationToken cancellationToken) =>
        Task.Delay(duration, cancellationToken);
}
