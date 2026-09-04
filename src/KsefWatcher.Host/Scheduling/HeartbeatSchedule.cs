using KsefWatcher.InvoiceWatching.ValueObjects;

namespace KsefWatcher.Host.Scheduling;

/// <summary>
/// The daily watchdog pulse's fixed per-subject time of day (OQ-7a/7b, docs/05_connect_message_flows.md):
/// reuses <see cref="PollOffsetCalculator"/>'s A9-style spreading — same hash(NIP), mod a full day
/// instead of the poll interval — so heartbeats spread across 24h rather than clustering at midnight.
/// </summary>
public static class HeartbeatSchedule
{
    /// <summary><paramref name="nowUtc"/> must be UTC.</summary>
    public static DateTimeOffset ComputeNextFireTime(SubjectId subjectId, DateTimeOffset nowUtc)
    {
        var offset = PollOffsetCalculator.ComputeOffset(subjectId, TimeSpan.FromDays(1));
        var todayFireTime = new DateTimeOffset(nowUtc.UtcDateTime.Date, TimeSpan.Zero) + offset;
        return todayFireTime > nowUtc ? todayFireTime : todayFireTime.AddDays(1);
    }
}
