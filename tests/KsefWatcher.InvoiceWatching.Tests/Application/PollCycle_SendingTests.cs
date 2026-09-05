using KsefWatcher.InvoiceWatching.Application;
using KsefWatcher.InvoiceWatching.Domain;
using KsefWatcher.InvoiceWatching.Tests.TestDoubles;
using KsefWatcher.InvoiceWatching.ValueObjects;
using Xunit;

namespace KsefWatcher.InvoiceWatching.Tests.Application;

public class PollCycle_SendingTests
{
    private static SubjectId AnySubjectId => new("5260001246");
    private static readonly ChannelRef AnyChannel = new("discord", "https://example.invalid/webhook");
    private static readonly TimeSpan AnyInterval = TimeSpan.FromMinutes(60);

    private static DetectedInvoice AnyInvoice(string ksefNumber) =>
        new(new InvoiceReference(ksefNumber), "FV/1", 100m, 123m, "PLN", "1111111111", "Contractor");

    [Fact]
    public async Task OneUnseenInvoice_Confirmed_SendsMarksAndAdvancesHwm()
    {
        var invoice = AnyInvoice("A-1");
        var newHwm = new Hwm(DateTimeOffset.Parse("2026-01-01T01:00:00Z"));
        var sw = new SubjectWatch(AnySubjectId, new HashSet<InvoiceReference>(), new Hwm(DateTimeOffset.Parse("2026-01-01T00:00:00Z")));
        var repository = new FakeSubjectWatchRepository(sw);
        var provider = new FakeInvoiceListProvider(new FetchedWindow(new HashSet<InvoiceReference> { invoice.Ref }, [invoice], newHwm));
        var notifier = new FakeNotifier((_, _) => new DeliveryResult.Confirmed());
        var delay = new FakeDelay();
        var sut = new PollCycle(repository, provider, notifier, delay);

        await sut.RunAsync(AnySubjectId, AnyChannel, AmountDisplay.Netto, AnyInterval, CancellationToken.None);

        var call = Assert.Single(notifier.Calls);
        Assert.Equal(AnyChannel, call.Channel);
        Assert.Equal(invoice, call.Invoice);
        Assert.Equal(AmountDisplay.Netto, call.AmountDisplay);

        Assert.Contains(invoice.Ref, sw.NotifiedRefs);
        Assert.Equal(newHwm, sw.LastHwm);
        Assert.Null(sw.PendingWindow);
    }

    [Fact]
    public async Task OneUnseenInvoice_Confirmed_ReturnsOutcomeWithCountsAndNewHwm()
    {
        var invoice = AnyInvoice("A-1");
        var newHwm = new Hwm(DateTimeOffset.Parse("2026-01-01T01:00:00Z"));
        var sw = new SubjectWatch(AnySubjectId, new HashSet<InvoiceReference>(), new Hwm(DateTimeOffset.Parse("2026-01-01T00:00:00Z")));
        var repository = new FakeSubjectWatchRepository(sw);
        var provider = new FakeInvoiceListProvider(new FetchedWindow(new HashSet<InvoiceReference> { invoice.Ref }, [invoice], newHwm));
        var notifier = new FakeNotifier((_, _) => new DeliveryResult.Confirmed());
        var delay = new FakeDelay();
        var sut = new PollCycle(repository, provider, notifier, delay);

        var outcome = await sut.RunAsync(AnySubjectId, AnyChannel, AmountDisplay.Netto, AnyInterval, CancellationToken.None);

        Assert.False(outcome.IsBaseline);
        Assert.Equal(1, outcome.FetchedCount);
        Assert.Equal(1, outcome.DetectedCount);
        Assert.Equal(1, outcome.NotifiedCount);
        Assert.Equal(newHwm, outcome.Hwm);
    }

    [Fact]
    public async Task MultipleUnseenInvoices_SendsSequentiallyWithThreeSecondDelayBetween_NotAfterLast()
    {
        var invoiceA = AnyInvoice("A-1");
        var invoiceB = AnyInvoice("A-2");
        var refs = new HashSet<InvoiceReference> { invoiceA.Ref, invoiceB.Ref };
        var sw = new SubjectWatch(AnySubjectId, new HashSet<InvoiceReference>(), new Hwm(DateTimeOffset.UtcNow.AddHours(-1)));
        var repository = new FakeSubjectWatchRepository(sw);
        var provider = new FakeInvoiceListProvider(new FetchedWindow(refs, [invoiceA, invoiceB], new Hwm(DateTimeOffset.UtcNow)));
        var notifier = new FakeNotifier((_, _) => new DeliveryResult.Confirmed());
        var delay = new FakeDelay();
        var sut = new PollCycle(repository, provider, notifier, delay);

        await sut.RunAsync(AnySubjectId, AnyChannel, AmountDisplay.Brutto, AnyInterval, CancellationToken.None);

        Assert.Equal([invoiceA, invoiceB], notifier.Calls.Select(c => c.Invoice));
        Assert.Equal([TimeSpan.FromSeconds(3)], delay.Requested);
    }
}
