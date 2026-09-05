using KsefWatcher.Host.Scheduling;
using KsefWatcher.Host.Tests.TestDoubles;
using KsefWatcher.InvoiceWatching.Application;
using KsefWatcher.InvoiceWatching.Domain;
using KsefWatcher.InvoiceWatching.ValueObjects;
using KsefWatcher.SubjectConfiguration;
using Microsoft.Extensions.Logging;
using Xunit;

namespace KsefWatcher.Host.Tests.Scheduling;

public class PollingBackgroundServiceTests
{
    private static ConfigWatcher NewConfigWatcher(int intervalMinutes = 60) =>
        ConfigWatcher.Start(new ConfigLoader(new EnvironmentVariables()), $"""
            version: 1
            environment: test
            intervalMinutes: {intervalMinutes}
            subjects: []
            """);

    private static PollCycle NewNeverExercisedPollCycle() => new(
        new NeverExercisedSubjectWatchRepository(),
        new NeverExercisedInvoiceListProvider(),
        new NeverExercisedNotifier(),
        new NeverExercisedDelay());

    private static ConfigWatcher NewConfigWatcherWithSubject(string nip, int intervalMinutes = 60) =>
        ConfigWatcher.Start(new ConfigLoader(new EnvironmentVariables()), $"""
            version: 1
            environment: test
            intervalMinutes: {intervalMinutes}
            subjects:
              - nip: "{nip}"
                intervalOffset: 0
                ksefToken: "token"
                channels:
                  - type: discord
                    token: "bot-token"
                    channelId: "111111111111111111"
            """);

    private static DetectedInvoice AnyInvoice(string ksefNumber) =>
        new(new InvoiceReference(ksefNumber), "FV/1", 100m, 123m, "PLN", "1111111111", "Contractor");

    private static SubjectConfig AnySubject(string nip = "5260001246", int intervalOffset = 0) => new()
    {
        Nip = nip,
        IntervalOffset = intervalOffset,
        KsefToken = "token",
        Channels = [new ChannelConfig { Type = "discord", Token = "bot-token", ChannelId = "111111111111111111" }],
    };

    [Fact]
    public void StartSubject_LogsIntervalAndNextSyncTime()
    {
        const int intervalMinutes = 60;
        var now = DateTimeOffset.Parse("2026-01-01T00:00:00Z");
        var logger = new FakeLogger<PollingBackgroundService>();
        var sut = new PollingBackgroundService(NewConfigWatcher(intervalMinutes), NewNeverExercisedPollCycle(), logger, new FixedTimeProvider(now));
        var subject = AnySubject(intervalOffset: 15);
        var expectedNextSync = now + TimeSpan.FromMinutes(subject.IntervalOffset);

        sut.StartSubject(subject);

        Assert.Contains(logger.Entries, e =>
            e.Level == LogLevel.Information &&
            e.Message.Contains(subject.Nip) &&
            e.Message.Contains(intervalMinutes.ToString()) &&
            e.Message.Contains(expectedNextSync.ToString("u")));
    }

    [Fact]
    public async Task PollNowAsync_BaselineCycle_LogsBaselineEstablished()
    {
        const string nip = "5260001246";
        var hwm = new Hwm(DateTimeOffset.Parse("2026-01-01T00:00:00Z"));
        var sw = new SubjectWatch(new SubjectId(nip), new HashSet<InvoiceReference>(), lastHwm: null);
        var repository = new FakeSubjectWatchRepository(sw);
        var provider = new FakeInvoiceListProvider(new FetchedWindow(new HashSet<InvoiceReference>(), [], hwm));
        var pollCycle = new PollCycle(repository, provider, new FakeNotifier(new DeliveryResult.Confirmed()), new FakeDelay());
        var logger = new FakeLogger<PollingBackgroundService>();
        var sut = new PollingBackgroundService(NewConfigWatcherWithSubject(nip), pollCycle, logger, TimeProvider.System);

        await sut.PollNowAsync(nip, CancellationToken.None);

        Assert.Contains(logger.Entries, e =>
            e.Level == LogLevel.Information &&
            e.Message.Contains(nip) &&
            e.Message.Contains("baseline", StringComparison.OrdinalIgnoreCase) &&
            e.Message.Contains(hwm.Utc.ToString("u")));
    }

    [Fact]
    public async Task PollNowAsync_NormalCycle_LogsFetchedDetectedNotifiedAndNewHwm()
    {
        const string nip = "5260001246";
        var previousHwm = new Hwm(DateTimeOffset.Parse("2026-01-01T00:00:00Z"));
        var newHwm = new Hwm(DateTimeOffset.Parse("2026-01-01T01:00:00Z"));
        var invoice = AnyInvoice("A-1");
        var sw = new SubjectWatch(new SubjectId(nip), new HashSet<InvoiceReference>(), previousHwm);
        var repository = new FakeSubjectWatchRepository(sw);
        var provider = new FakeInvoiceListProvider(new FetchedWindow(new HashSet<InvoiceReference> { invoice.Ref }, [invoice], newHwm));
        var pollCycle = new PollCycle(repository, provider, new FakeNotifier(new DeliveryResult.Confirmed()), new FakeDelay());
        var logger = new FakeLogger<PollingBackgroundService>();
        var sut = new PollingBackgroundService(NewConfigWatcherWithSubject(nip), pollCycle, logger, TimeProvider.System);

        await sut.PollNowAsync(nip, CancellationToken.None);

        Assert.Contains(logger.Entries, e =>
            e.Level == LogLevel.Information &&
            e.Message.Contains(nip) &&
            e.Message.Contains("fetched 1") &&
            e.Message.Contains("1 new") &&
            e.Message.Contains("1 notified") &&
            e.Message.Contains(newHwm.Utc.ToString("u")));
    }

    [Fact]
    public async Task PollNowAsync_DeliveryFails_LogsWarning_HwmNotAdvanced()
    {
        const string nip = "5260001246";
        var previousHwm = new Hwm(DateTimeOffset.Parse("2026-01-01T00:00:00Z"));
        var invoice = AnyInvoice("A-1");
        var sw = new SubjectWatch(new SubjectId(nip), new HashSet<InvoiceReference>(), previousHwm);
        var repository = new FakeSubjectWatchRepository(sw);
        var provider = new FakeInvoiceListProvider(new FetchedWindow(new HashSet<InvoiceReference> { invoice.Ref }, [invoice], new Hwm(DateTimeOffset.Parse("2026-01-01T01:00:00Z"))));
        var pollCycle = new PollCycle(repository, provider, new FakeNotifier(new DeliveryResult.Failed(DeliveryResult.FailureKind.Permanent)), new FakeDelay());
        var logger = new FakeLogger<PollingBackgroundService>();
        var sut = new PollingBackgroundService(NewConfigWatcherWithSubject(nip), pollCycle, logger, TimeProvider.System);

        await sut.PollNowAsync(nip, CancellationToken.None);

        Assert.Contains(logger.Entries, e =>
            e.Level == LogLevel.Warning &&
            e.Message.Contains(nip) &&
            e.Message.Contains("not advanced"));
    }
}
