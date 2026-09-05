using KsefWatcher.SubjectConfiguration;

namespace KsefWatcher.Host.Scheduling;

/// <summary>
/// The scheduling decisions a config reload implies (docs/05_connect_message_flows.md, Scenario D):
/// added subjects need a new timer (baseline runs naturally on its first poll, I-18); removed
/// subjects need their timer stopped and state reset (I-19); a subject needs its timer
/// rescheduled when either the shared <see cref="ConfigFile.IntervalMinutes"/> changed (affects
/// every subject) or its own <see cref="SubjectConfig.IntervalOffset"/> changed. Any other field
/// change (token, channel, amountDisplay) needs no scheduling action — PollCycle reads current
/// config fresh every poll, so it just takes effect next cycle.
/// </summary>
public sealed record ConfigReloadPlan(
    IReadOnlyList<SubjectConfig> Added,
    IReadOnlyList<SubjectConfig> Removed,
    IReadOnlyList<SubjectConfig> Rescheduled);

public static class ConfigReloadPlanner
{
    public static ConfigReloadPlan Plan(ConfigFile previous, ConfigFile current)
    {
        var previousByNip = previous.Subjects.ToDictionary(s => s.Nip);
        var currentByNip = current.Subjects.ToDictionary(s => s.Nip);

        var added = current.Subjects.Where(s => !previousByNip.ContainsKey(s.Nip)).ToList();
        var removed = previous.Subjects.Where(s => !currentByNip.ContainsKey(s.Nip)).ToList();

        var globalIntervalChanged = previous.IntervalMinutes != current.IntervalMinutes;

        var rescheduled = current.Subjects
            .Where(s => previousByNip.TryGetValue(s.Nip, out var old) &&
                        (globalIntervalChanged || old.IntervalOffset != s.IntervalOffset))
            .ToList();

        return new ConfigReloadPlan(added, removed, rescheduled);
    }
}
