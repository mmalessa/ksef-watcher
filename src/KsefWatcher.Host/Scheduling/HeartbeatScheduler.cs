using System.Collections.Concurrent;
using KsefWatcher.InvoiceWatching.Ports;
using KsefWatcher.InvoiceWatching.ValueObjects;
using KsefWatcher.SubjectConfiguration;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;

namespace KsefWatcher.Host.Scheduling;

/// <summary>
/// One <see cref="Timer"/> per subject, firing the daily watchdog pulse (OQ-7a/7b) through
/// <see cref="INotifier.SendHeartbeatAsync"/> — a second caller of the port, independent of
/// <see cref="PollingBackgroundService"/>. The fire time depends only on subject identity
/// (<see cref="HeartbeatSchedule"/>), not the poll interval, so config reloads never need to
/// reschedule an existing subject's heartbeat — only add/remove on subject add/removal.
/// </summary>
public sealed class HeartbeatScheduler(
    ConfigWatcher configWatcher,
    INotifier notifier,
    TimeProvider timeProvider,
    ILogger<HeartbeatScheduler> logger) : BackgroundService
{
    private readonly ConcurrentDictionary<string, Timer> _timers = new();

    protected override Task ExecuteAsync(CancellationToken stoppingToken)
    {
        foreach (var subject in configWatcher.Current.Subjects)
        {
            StartSubject(subject);
        }

        return Task.CompletedTask;
    }

    public void StartSubject(SubjectConfig subject)
    {
        var subjectId = new SubjectId(subject.Nip);
        var now = timeProvider.GetUtcNow();
        var dueTime = HeartbeatSchedule.ComputeNextFireTime(subjectId, now) - now;

        var timer = new Timer(_ => Fire(subject.Nip), null, dueTime, TimeSpan.FromDays(1));
        _timers[subject.Nip] = timer;
    }

    public void StopSubject(string nip)
    {
        if (_timers.TryRemove(nip, out var timer))
        {
            timer.Dispose();
        }
    }

    private void Fire(string nip) => _ = SendHeartbeatSafelyAsync(nip);

    private async Task SendHeartbeatSafelyAsync(string nip)
    {
        try
        {
            var subject = configWatcher.Current.Subjects.FirstOrDefault(s => s.Nip == nip);
            if (subject is null)
            {
                return; // removed between scheduling and firing; ConfigReloadCoordinator already stopped this timer
            }

            var channelConfig = subject.Channels[0]; // V1: exactly one channel per subject (OQ-12)
            var channel = new ChannelRef(channelConfig.Type, channelConfig.ChannelId ?? string.Empty, channelConfig.Token);
            var asOf = DateOnly.FromDateTime(timeProvider.GetUtcNow().UtcDateTime);

            await notifier.SendHeartbeatAsync(channel, asOf, CancellationToken.None);
        }
        catch (Exception ex)
        {
            // Same isolation principle as PollingBackgroundService (I-3): never let one subject's
            // heartbeat failure affect another's, or crash the host.
            logger.LogError(ex, "Heartbeat failed for subject {Nip}.", nip);
        }
    }

    public override void Dispose()
    {
        foreach (var timer in _timers.Values)
        {
            timer.Dispose();
        }

        base.Dispose();
    }
}
