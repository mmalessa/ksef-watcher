namespace KsefWatcher.InvoiceWatching.Ports;

/// <summary>
/// Abstraction over waiting, so <c>PollCycle</c>'s hybrid retry backoff (OQ-17c) and inter-message
/// delay (OQ-11) are deterministically testable without real sleeps. Implemented by Host with
/// <c>Task.Delay</c> in production.
/// </summary>
public interface IDelay
{
    Task WaitAsync(TimeSpan duration, CancellationToken cancellationToken);
}
