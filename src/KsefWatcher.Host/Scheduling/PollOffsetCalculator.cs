using System.Security.Cryptography;
using System.Text;
using KsefWatcher.InvoiceWatching.ValueObjects;

namespace KsefWatcher.Host.Scheduling;

/// <summary>
/// A9: deterministic position of a subject's poll within its interval window — offset =
/// hash(NIP) mod interval, stable across restarts and hot reloads (docs/09_architecture.md).
/// Load-smoothing so simultaneous per-subject intervals spread into a smooth stream instead of
/// bursting shared per-IP endpoints.
/// </summary>
public static class PollOffsetCalculator
{
    public static TimeSpan ComputeOffset(SubjectId subjectId, TimeSpan interval)
    {
        // SHA-256, not string.GetHashCode() — .NET randomizes string hash codes per process by
        // default, which would break "stable across restarts" (A9's whole point).
        var hash = SHA256.HashData(Encoding.UTF8.GetBytes(subjectId.Nip));
        var value = BitConverter.ToUInt64(hash, 0);
        var offsetTicks = (long)(value % (ulong)interval.Ticks);
        return TimeSpan.FromTicks(offsetTicks);
    }
}
