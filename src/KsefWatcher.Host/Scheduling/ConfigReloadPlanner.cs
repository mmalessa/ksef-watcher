using KsefWatcher.SubjectConfiguration;

namespace KsefWatcher.Host.Scheduling;

/// <summary>
/// The scheduling decisions a config reload implies (docs/05_connect_message_flows.md, Scenario D):
/// added subjects need a new timer (baseline runs naturally on its first poll, I-18); removed
/// subjects need their timer stopped and state reset (I-19); subjects whose interval changed need
/// their offset recomputed and timer rescheduled (A9). Any other field change (token, channel,
/// amountDisplay, environment) needs no scheduling action — PollCycle reads current config fresh
/// every poll, so it just takes effect next cycle.
/// </summary>
public sealed record ConfigReloadPlan(
    IReadOnlyList<SubjectConfig> Added,
    IReadOnlyList<SubjectConfig> Removed,
    IReadOnlyList<SubjectConfig> IntervalChanged);

public static class ConfigReloadPlanner
{
    public static ConfigReloadPlan Plan(IReadOnlyList<SubjectConfig> previous, IReadOnlyList<SubjectConfig> current)
    {
        var previousByNip = previous.ToDictionary(s => s.Nip);
        var currentByNip = current.ToDictionary(s => s.Nip);

        var added = current.Where(s => !previousByNip.ContainsKey(s.Nip)).ToList();
        var removed = previous.Where(s => !currentByNip.ContainsKey(s.Nip)).ToList();
        var intervalChanged = current
            .Where(s => previousByNip.TryGetValue(s.Nip, out var old) && old.IntervalMinutes != s.IntervalMinutes)
            .ToList();

        return new ConfigReloadPlan(added, removed, intervalChanged);
    }
}
