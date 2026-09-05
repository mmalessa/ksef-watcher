using System.Collections.Concurrent;

namespace KsefWatcher.Host.Scheduling;

/// <summary>
/// Per-key "in progress" guard — prevents two overlapping runs for the same key (I-1: a poll
/// cycle that outruns its configured interval must not start a second, concurrent cycle for the
/// same subject against the same <c>SubjectWatch</c> row).
/// </summary>
public sealed class InFlightGate
{
    private readonly ConcurrentDictionary<string, byte> _inFlight = new();

    public bool TryEnter(string key) => _inFlight.TryAdd(key, 0);

    public void Exit(string key) => _inFlight.TryRemove(key, out _);
}
