using KsefWatcher.Host.Scheduling;
using KsefWatcher.InvoiceWatching.ValueObjects;
using Xunit;

namespace KsefWatcher.Host.Tests.Scheduling;

public class HeartbeatScheduleTests
{
    private static readonly SubjectId AnySubjectId = new("5260001246");

    [Fact]
    public void ComputeNextFireTime_WhenTodaysFireTimeIsStillAhead_ReturnsToday()
    {
        var midnightToday = new DateTimeOffset(2026, 1, 15, 0, 0, 0, TimeSpan.Zero);
        var fireTimeToday = HeartbeatSchedule.ComputeNextFireTime(AnySubjectId, midnightToday);

        // Asking again a moment before that computed time should still yield the same today's slot.
        var justBefore = fireTimeToday.AddSeconds(-1);
        var result = HeartbeatSchedule.ComputeNextFireTime(AnySubjectId, justBefore);

        Assert.Equal(fireTimeToday, result);
        Assert.Equal(new DateTimeOffset(2026, 1, 15, 0, 0, 0, TimeSpan.Zero).Date, result.Date);
    }

    [Fact]
    public void ComputeNextFireTime_WhenTodaysFireTimeAlreadyPassed_ReturnsTomorrowAtTheSameTimeOfDay()
    {
        var midnightToday = new DateTimeOffset(2026, 1, 15, 0, 0, 0, TimeSpan.Zero);
        var fireTimeToday = HeartbeatSchedule.ComputeNextFireTime(AnySubjectId, midnightToday);

        var justAfter = fireTimeToday.AddSeconds(1);
        var result = HeartbeatSchedule.ComputeNextFireTime(AnySubjectId, justAfter);

        Assert.Equal(fireTimeToday.AddDays(1), result);
    }

    [Fact]
    public void ComputeNextFireTime_WhenNowIsExactlyTheFireTime_ReturnsTomorrow_NotImmediateRefire()
    {
        var midnightToday = new DateTimeOffset(2026, 1, 15, 0, 0, 0, TimeSpan.Zero);
        var fireTimeToday = HeartbeatSchedule.ComputeNextFireTime(AnySubjectId, midnightToday);

        var result = HeartbeatSchedule.ComputeNextFireTime(AnySubjectId, fireTimeToday);

        Assert.Equal(fireTimeToday.AddDays(1), result);
    }
}
