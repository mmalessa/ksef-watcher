using KsefWatcher.Host.Persistence;
using KsefWatcher.InvoiceWatching.ValueObjects;
using KsefWatcher.SubjectConfiguration;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;

namespace KsefWatcher.Host.Scheduling;

/// <summary>
/// Reacts to <see cref="ConfigWatcher.Reloaded"/> (docs/05_connect_message_flows.md, Scenario D):
/// diffs the whole config (<see cref="ConfigReloadPlanner"/>) and drives
/// <see cref="PollingBackgroundService"/> and <see cref="HeartbeatScheduler"/> plus the deliberate
/// state reset on removal (I-19). Heartbeat timing doesn't depend on the poll interval
/// (<see cref="HeartbeatSchedule"/>), so it only needs add/remove, never reschedule.
/// </summary>
public sealed class ConfigReloadCoordinator(
    ConfigWatcher configWatcher,
    PollingBackgroundService pollingService,
    HeartbeatScheduler heartbeatScheduler,
    SqliteSubjectWatchRepository repository,
    ILogger<ConfigReloadCoordinator> logger) : IHostedService
{
    private ConfigFile _previousConfig = new();

    public Task StartAsync(CancellationToken cancellationToken)
    {
        _previousConfig = configWatcher.Current;
        configWatcher.Reloaded += OnReloaded;
        return Task.CompletedTask;
    }

    public Task StopAsync(CancellationToken cancellationToken)
    {
        configWatcher.Reloaded -= OnReloaded;
        return Task.CompletedTask;
    }

    private void OnReloaded(ConfigFile newConfig)
    {
        var plan = ConfigReloadPlanner.Plan(_previousConfig, newConfig);
        _previousConfig = newConfig;

        logger.LogInformation(
            "Config reloaded: {Total} subject(s) configured ({Added} added, {Removed} removed, {Rescheduled} rescheduled).",
            newConfig.Subjects.Count, plan.Added.Count, plan.Removed.Count, plan.Rescheduled.Count);

        foreach (var subject in plan.Added)
        {
            pollingService.StartSubject(subject); // baseline runs naturally on its first poll (I-18)
            heartbeatScheduler.StartSubject(subject);
        }

        foreach (var subject in plan.Removed)
        {
            logger.LogInformation("Subject {Nip}: removed from config, no longer watched.", subject.Nip);
            pollingService.StopSubject(subject.Nip);
            heartbeatScheduler.StopSubject(subject.Nip);
            _ = DeleteRemovedSubjectStateSafelyAsync(subject.Nip);
        }

        foreach (var subject in plan.Rescheduled)
        {
            pollingService.RescheduleSubject(subject); // logs new next-sync time
        }
    }

    private async Task DeleteRemovedSubjectStateSafelyAsync(string nip)
    {
        try
        {
            await repository.DeleteAsync(new SubjectId(nip), CancellationToken.None);
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Failed to reset state for removed subject {Nip} (I-19).", nip);
        }
    }
}
