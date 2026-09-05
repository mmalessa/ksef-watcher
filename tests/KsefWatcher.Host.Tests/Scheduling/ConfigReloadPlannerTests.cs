using KsefWatcher.Host.Scheduling;
using KsefWatcher.SubjectConfiguration;
using Xunit;

namespace KsefWatcher.Host.Tests.Scheduling;

public class ConfigReloadPlannerTests
{
    private static SubjectConfig Subject(string nip, int intervalOffset = 0) =>
        new() { Nip = nip, IntervalOffset = intervalOffset, KsefToken = "token" };

    private static ConfigFile Config(int intervalMinutes, params SubjectConfig[] subjects) =>
        new() { IntervalMinutes = intervalMinutes, Subjects = subjects.ToList() };

    [Fact]
    public void Plan_DetectsAddedSubject()
    {
        var previous = Config(60, Subject("1111111111"));
        var current = Config(60, Subject("1111111111"), Subject("2222222222"));

        var plan = ConfigReloadPlanner.Plan(previous, current);

        var added = Assert.Single(plan.Added);
        Assert.Equal("2222222222", added.Nip);
        Assert.Empty(plan.Removed);
        Assert.Empty(plan.Rescheduled);
    }

    [Fact]
    public void Plan_DetectsRemovedSubject()
    {
        var previous = Config(60, Subject("1111111111"), Subject("2222222222"));
        var current = Config(60, Subject("1111111111"));

        var plan = ConfigReloadPlanner.Plan(previous, current);

        var removed = Assert.Single(plan.Removed);
        Assert.Equal("2222222222", removed.Nip);
        Assert.Empty(plan.Added);
        Assert.Empty(plan.Rescheduled);
    }

    [Fact]
    public void Plan_DetectsSubjectOwnIntervalOffsetChange()
    {
        var previous = Config(60, Subject("1111111111", intervalOffset: 10));
        var current = Config(60, Subject("1111111111", intervalOffset: 20));

        var plan = ConfigReloadPlanner.Plan(previous, current);

        var rescheduled = Assert.Single(plan.Rescheduled);
        Assert.Equal("1111111111", rescheduled.Nip);
        Assert.Equal(20, rescheduled.IntervalOffset);
        Assert.Empty(plan.Added);
        Assert.Empty(plan.Removed);
    }

    [Fact]
    public void Plan_GlobalIntervalChange_ReschedulesEveryExistingSubject()
    {
        var previous = Config(60, Subject("1111111111"), Subject("2222222222"));
        var current = Config(30, Subject("1111111111"), Subject("2222222222"));

        var plan = ConfigReloadPlanner.Plan(previous, current);

        Assert.Equal(["1111111111", "2222222222"], plan.Rescheduled.Select(s => s.Nip));
        Assert.Empty(plan.Added);
        Assert.Empty(plan.Removed);
    }

    [Fact]
    public void Plan_GlobalIntervalChange_DoesNotAlsoRescheduleAddedSubject()
    {
        var previous = Config(60, Subject("1111111111"));
        var current = Config(30, Subject("1111111111"), Subject("2222222222"));

        var plan = ConfigReloadPlanner.Plan(previous, current);

        Assert.Equal(["2222222222"], plan.Added.Select(s => s.Nip));
        Assert.Equal(["1111111111"], plan.Rescheduled.Select(s => s.Nip));
    }

    [Fact]
    public void Plan_UnchangedSubject_AppearsInNoBucket()
    {
        var previous = Config(60, Subject("1111111111", intervalOffset: 5));
        var current = Config(60, Subject("1111111111", intervalOffset: 5));

        var plan = ConfigReloadPlanner.Plan(previous, current);

        Assert.Empty(plan.Added);
        Assert.Empty(plan.Removed);
        Assert.Empty(plan.Rescheduled);
    }

    [Fact]
    public void Plan_HandlesAddedRemovedAndRescheduled_Simultaneously()
    {
        var previous = Config(
            60,
            Subject("1111111111"), // stays unchanged
            Subject("2222222222"), // removed
            Subject("3333333333", intervalOffset: 10)); // offset changes
        var current = Config(
            60,
            Subject("1111111111"),
            Subject("3333333333", intervalOffset: 20),
            Subject("4444444444")); // added

        var plan = ConfigReloadPlanner.Plan(previous, current);

        Assert.Equal(["4444444444"], plan.Added.Select(s => s.Nip));
        Assert.Equal(["2222222222"], plan.Removed.Select(s => s.Nip));
        Assert.Equal(["3333333333"], plan.Rescheduled.Select(s => s.Nip));
    }
}
