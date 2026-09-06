using System.Collections.Concurrent;
using KsefWatcher.InvoiceWatching.Application;
using KsefWatcher.InvoiceWatching.ValueObjects;
using KsefWatcher.SubjectConfiguration;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;

namespace KsefWatcher.Host.Scheduling;

/// <summary>
/// Owns one <see cref="Timer"/> per subject, firing <see cref="PollCycle.RunAsync"/> at
/// <c>start + subject's configured offset</c> then every <see cref="SubjectConfiguration.ConfigFile.IntervalMinutes"/>
/// (shared by all subjects; per-subject <see cref="SubjectConfig.IntervalOffset"/> spreads them
/// across that shared window — both operator-configured, not auto-computed).
/// <see cref="ConfigReloadCoordinator"/> drives <see cref="StartSubject"/>/<see cref="StopSubject"/>/
/// <see cref="RescheduleSubject"/> on hot reload.
/// </summary>
public sealed class PollingBackgroundService(
    ConfigWatcher configWatcher,
    PollCycle pollCycle,
    ILogger<PollingBackgroundService> logger,
    TimeProvider timeProvider) : BackgroundService
{
    private readonly ConcurrentDictionary<string, Timer> _timers = new();
    private readonly InFlightGate _inFlightGate = new();
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
        var intervalMinutes = configWatcher.Current.IntervalMinutes;
        var interval = TimeSpan.FromMinutes(intervalMinutes);
        var offset = TimeSpan.FromMinutes(subject.IntervalOffset);
        var nextSyncAtUtc = timeProvider.GetUtcNow() + offset;

        logger.LogInformation(
            "Subject {Nip}: watching every {IntervalMinutes} min, next sync at {NextSyncAtUtc:u}.",
            subject.Nip, intervalMinutes, nextSyncAtUtc);

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

    private void Fire(string nip)
    {
        if (!_inFlightGate.TryEnter(nip))
        {
            // I-1: a poll that outruns its interval must not overlap with the next one for the
            // same subject — the next scheduled tick picks it up once this cycle finishes.
            logger.LogWarning("Skipping poll for subject {Nip}: previous cycle is still running.", nip);
            return;
        }

        _ = RunPollSafelyAsync(nip);
    }

    private async Task RunPollSafelyAsync(string nip)
    {
        try
        {
            await PollNowAsync(nip, _stoppingToken);
        }
        catch (Exception ex)
        {
            // I-3: one subject's failure must never affect another's cycle or crash the host.
            logger.LogError(ex, "Poll cycle failed for subject {Nip}.", nip);
        }
        finally
        {
            _inFlightGate.Exit(nip);
        }
    }

    /// <summary>Runs one poll cycle for <paramref name="nip"/> and logs what it did — extracted from <see cref="RunPollSafelyAsync"/> so it's directly testable without a real Timer.</summary>
    public async Task PollNowAsync(string nip, CancellationToken cancellationToken)
    {
        // Read live, not the subject captured at StartSubject time — a hot reload may have
        // changed the token/channel/amountDisplay without touching the interval (09_architecture.md).
        var subject = configWatcher.Current.Subjects.FirstOrDefault(s => s.Nip == nip);
        if (subject is null)
        {
            return; // removed between scheduling and firing; ConfigReloadCoordinator already stopped this timer
        }

        var channel = subject.Channels[0].ToChannelRef(); // V1: exactly one channel per subject (OQ-12)
        var amountDisplay = string.Equals(subject.AmountDisplay, "netto", StringComparison.OrdinalIgnoreCase)
            ? AmountDisplay.Netto
            : AmountDisplay.Brutto; // default brutto, docs/08_subject_configuration_tactical_model.md
        var interval = TimeSpan.FromMinutes(configWatcher.Current.IntervalMinutes);

        var outcome = await pollCycle.RunAsync(new SubjectId(nip), channel, amountDisplay, interval, cancellationToken);
        LogOutcome(nip, outcome);
    }

    private void LogOutcome(string nip, PollOutcome outcome)
    {
        if (outcome.IsBaseline)
        {
            logger.LogInformation(
                "Subject {Nip}: first poll — baseline established at HWM {Hwm:u}; pre-existing invoices are not notified (I-18), only ones arriving from now on will be.",
                nip, outcome.Hwm!.Utc);
            return;
        }

        if (outcome.Hwm is null)
        {
            logger.LogWarning(
                "Subject {Nip}: fetched {FetchedCount}, {DetectedCount} new, only {NotifiedCount} notified before stopping — delivery failed, HWM not advanced, will retry next cycle.",
                nip, outcome.FetchedCount, outcome.DetectedCount, outcome.NotifiedCount);
            return;
        }

        logger.LogInformation(
            "Subject {Nip}: fetched {FetchedCount}, {DetectedCount} new, {NotifiedCount} notified. HWM advanced to {Hwm:u}.",
            nip, outcome.FetchedCount, outcome.DetectedCount, outcome.NotifiedCount, outcome.Hwm.Utc);
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
