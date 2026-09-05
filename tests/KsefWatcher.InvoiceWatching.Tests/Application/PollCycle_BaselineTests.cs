using KsefWatcher.InvoiceWatching.Application;
using KsefWatcher.InvoiceWatching.Domain;
using KsefWatcher.InvoiceWatching.Tests.TestDoubles;
using KsefWatcher.InvoiceWatching.ValueObjects;
using Xunit;

namespace KsefWatcher.InvoiceWatching.Tests.Application;

public class PollCycle_BaselineTests
{
    private static SubjectId AnySubjectId => new("5260001246");
    private static readonly ChannelRef AnyChannel = new("discord", "https://example.invalid/webhook");

    private static DetectedInvoice AnyInvoice(string ksefNumber) =>
        new(new InvoiceReference(ksefNumber), "FV/1", 100m, 123m, "PLN", "1111111111", "Contractor");

    [Fact]
    public async Task Baseline_FetchesNarrowWindowEndingNow_ConfirmsBaseline_SavesWithoutSending()
    {
        var now = DateTimeOffset.Parse("2026-01-01T12:00:00Z");
        var interval = TimeSpan.FromMinutes(60);
        var baselineHwm = new Hwm(now);
        var sw = new SubjectWatch(AnySubjectId, new HashSet<InvoiceReference>(), lastHwm: null);
        var repository = new FakeSubjectWatchRepository(sw);
        var provider = new FakeInvoiceListProvider(new FetchedWindow(new HashSet<InvoiceReference>(), [], baselineHwm));
        var notifier = new FakeNotifier((_, _) => new DeliveryResult.Confirmed());
        var delay = new FakeDelay();
        var sut = new PollCycle(repository, provider, notifier, delay, new FixedTimeProvider(now));

        await sut.RunAsync(AnySubjectId, AnyChannel, AmountDisplay.Brutto, interval, CancellationToken.None);

        var call = Assert.Single(provider.Calls);
        Assert.Equal(now - interval, call.Window.From);
        Assert.Equal(now, call.Window.To);

        Assert.Equal(baselineHwm, sw.LastHwm);
        Assert.Single(repository.SaveCalls);
        Assert.Empty(notifier.Calls);
    }

    [Fact]
    public async Task Baseline_ReturnsOutcomeDescribingBaselineEstablished()
    {
        var now = DateTimeOffset.Parse("2026-01-01T12:00:00Z");
        var interval = TimeSpan.FromMinutes(60);
        var baselineHwm = new Hwm(now);
        var invoice = AnyInvoice("A-1"); // already existed before this subject's first poll (I-18)
        var sw = new SubjectWatch(AnySubjectId, new HashSet<InvoiceReference>(), lastHwm: null);
        var repository = new FakeSubjectWatchRepository(sw);
        var provider = new FakeInvoiceListProvider(new FetchedWindow(new HashSet<InvoiceReference> { invoice.Ref }, [invoice], baselineHwm));
        var notifier = new FakeNotifier((_, _) => new DeliveryResult.Confirmed());
        var delay = new FakeDelay();
        var sut = new PollCycle(repository, provider, notifier, delay, new FixedTimeProvider(now));

        var outcome = await sut.RunAsync(AnySubjectId, AnyChannel, AmountDisplay.Brutto, interval, CancellationToken.None);

        Assert.True(outcome.IsBaseline);
        Assert.Equal(1, outcome.FetchedCount);
        Assert.Equal(0, outcome.DetectedCount);
        Assert.Equal(0, outcome.NotifiedCount);
        Assert.Equal(baselineHwm, outcome.Hwm);
    }
}
