using KsefWatcher.Host.Scheduling;
using KsefWatcher.SubjectConfiguration;
using Xunit;

namespace KsefWatcher.Host.Tests.Scheduling;

public class ConfigReloadPlannerTests
{
    private static SubjectConfig Subject(string nip, int intervalMinutes = 60) =>
        new() { Nip = nip, IntervalMinutes = intervalMinutes, KsefToken = "token" };

    [Fact]
    public void Plan_DetectsAddedSubject()
    {
        var previous = new List<SubjectConfig> { Subject("1111111111") };
        var current = new List<SubjectConfig> { Subject("1111111111"), Subject("2222222222") };

        var plan = ConfigReloadPlanner.Plan(previous, current);

        var added = Assert.Single(plan.Added);
        Assert.Equal("2222222222", added.Nip);
        Assert.Empty(plan.Removed);
        Assert.Empty(plan.IntervalChanged);
    }

    [Fact]
    public void Plan_DetectsRemovedSubject()
    {
        var previous = new List<SubjectConfig> { Subject("1111111111"), Subject("2222222222") };
        var current = new List<SubjectConfig> { Subject("1111111111") };

        var plan = ConfigReloadPlanner.Plan(previous, current);

        var removed = Assert.Single(plan.Removed);
        Assert.Equal("2222222222", removed.Nip);
        Assert.Empty(plan.Added);
        Assert.Empty(plan.IntervalChanged);
    }

    [Fact]
    public void Plan_DetectsIntervalChange()
    {
        var previous = new List<SubjectConfig> { Subject("1111111111", intervalMinutes: 60) };
        var current = new List<SubjectConfig> { Subject("1111111111", intervalMinutes: 30) };

        var plan = ConfigReloadPlanner.Plan(previous, current);

        var changed = Assert.Single(plan.IntervalChanged);
        Assert.Equal("1111111111", changed.Nip);
        Assert.Equal(30, changed.IntervalMinutes);
        Assert.Empty(plan.Added);
        Assert.Empty(plan.Removed);
    }

    [Fact]
    public void Plan_UnchangedSubject_AppearsInNoBucket()
    {
        var previous = new List<SubjectConfig> { Subject("1111111111", intervalMinutes: 60) };
        var current = new List<SubjectConfig> { Subject("1111111111", intervalMinutes: 60) };

        var plan = ConfigReloadPlanner.Plan(previous, current);

        Assert.Empty(plan.Added);
        Assert.Empty(plan.Removed);
        Assert.Empty(plan.IntervalChanged);
    }

    [Fact]
    public void Plan_HandlesAddedRemovedAndChanged_Simultaneously()
    {
        var previous = new List<SubjectConfig>
        {
            Subject("1111111111", intervalMinutes: 60), // stays unchanged
            Subject("2222222222", intervalMinutes: 60), // removed
            Subject("3333333333", intervalMinutes: 60), // interval changes
        };
        var current = new List<SubjectConfig>
        {
            Subject("1111111111", intervalMinutes: 60),
            Subject("3333333333", intervalMinutes: 30),
            Subject("4444444444", intervalMinutes: 60), // added
        };

        var plan = ConfigReloadPlanner.Plan(previous, current);

        Assert.Equal(["4444444444"], plan.Added.Select(s => s.Nip));
        Assert.Equal(["2222222222"], plan.Removed.Select(s => s.Nip));
        Assert.Equal(["3333333333"], plan.IntervalChanged.Select(s => s.Nip));
    }
}
