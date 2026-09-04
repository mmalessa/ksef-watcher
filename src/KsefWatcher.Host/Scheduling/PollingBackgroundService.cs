using System.Collections.Concurrent;
using KsefWatcher.InvoiceWatching.Application;
using KsefWatcher.InvoiceWatching.ValueObjects;
using KsefWatcher.SubjectConfiguration;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;

namespace KsefWatcher.Host.Scheduling;

/// <summary>
/// Owns one <see cref="Timer"/> per subject, firing <see cref="PollCycle.RunAsync"/> at
/// <c>boot + offset</c> then every configured interval (A9, docs/09_architecture.md "Scheduler").
/// <see cref="ConfigReloadCoordinator"/> drives <see cref="StartSubject"/>/<see cref="StopSubject"/>/
/// <see cref="RescheduleSubject"/> on hot reload.
/// </summary>
public sealed class PollingBackgroundService(
    ConfigWatcher configWatcher,
    PollCycle pollCycle,
    ILogger<PollingBackgroundService> logger) : BackgroundService
{
    private readonly ConcurrentDictionary<string, Timer> _timers = new();
    private CancellationToken _stoppingToken;

    protected override Task ExecuteAsync(CancellationToken stoppingToken)
    {
        _stoppingToken = stoppingToken;

        foreach (var subject in configWatcher.Current.Subjects)
        {
            StartSubject(subject);
        }

        // Timers keep firing independently of this method's lifetime — nothing to await here.
        return Task.CompletedTask;
    }

    public void StartSubject(SubjectConfig subject)
    {
        var subjectId = new SubjectId(subject.Nip);
        var interval = TimeSpan.FromMinutes(subject.IntervalMinutes);
        var offset = PollOffsetCalculator.ComputeOffset(subjectId, interval);

        var timer = new Timer(_ => Fire(subject.Nip), null, offset, interval);
        _timers[subject.Nip] = timer;
    }

    public void StopSubject(string nip)
    {
        if (_timers.TryRemove(nip, out var timer))
        {
            timer.Dispose();
        }
    }

    public void RescheduleSubject(SubjectConfig subject)
    {
        StopSubject(subject.Nip);
        StartSubject(subject);
    }

    private void Fire(string nip) => _ = RunPollSafelyAsync(nip);

    private async Task RunPollSafelyAsync(string nip)
    {
        try
        {
            // Read live, not the subject captured at StartSubject time — a hot reload may have
            // changed the token/channel/amountDisplay without touching the interval (09_architecture.md).
            var subject = configWatcher.Current.Subjects.FirstOrDefault(s => s.Nip == nip);
            if (subject is null)
            {
                return; // removed between scheduling and firing; ConfigReloadCoordinator already stopped this timer
            }

            var channelConfig = subject.Channels[0]; // V1: exactly one channel per subject (OQ-12)
            var channel = new ChannelRef(channelConfig.Type, channelConfig.WebhookUrl ?? string.Empty);
            var amountDisplay = string.Equals(subject.AmountDisplay, "netto", StringComparison.OrdinalIgnoreCase)
                ? AmountDisplay.Netto
                : AmountDisplay.Brutto; // default brutto, docs/08_subject_configuration_tactical_model.md
            var interval = TimeSpan.FromMinutes(subject.IntervalMinutes);

            await pollCycle.RunAsync(new SubjectId(nip), channel, amountDisplay, interval, _stoppingToken);
        }
        catch (Exception ex)
        {
            // I-3: one subject's failure must never affect another's cycle or crash the host.
            logger.LogError(ex, "Poll cycle failed for subject {Nip}.", nip);
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
