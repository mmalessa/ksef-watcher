using KsefWatcher.InvoiceWatching.Application;
using KsefWatcher.InvoiceWatching.Domain;
using KsefWatcher.InvoiceWatching.Tests.TestDoubles;
using KsefWatcher.InvoiceWatching.ValueObjects;
using Xunit;

namespace KsefWatcher.InvoiceWatching.Tests.Application;

public class PollCycle_FailureTests
{
    private static SubjectId AnySubjectId => new("5260001246");
    private static readonly ChannelRef AnyChannel = new("discord", "https://example.invalid/webhook");
    private static readonly TimeSpan AnyInterval = TimeSpan.FromMinutes(60);
    private static readonly Hwm PreviousHwm = new(DateTimeOffset.Parse("2026-01-01T00:00:00Z"));
    private static readonly Hwm FetchedHwm = new(DateTimeOffset.Parse("2026-01-01T01:00:00Z"));

    private static DetectedInvoice AnyInvoice(string ksefNumber) =>
        new(new InvoiceReference(ksefNumber), "FV/1", 100m, 123m, "PLN", "1111111111", "Contractor");

    [Fact]
    public async Task PermanentFailure_StopsAfterOneAttempt_DoesNotAdvanceHwm_DoesNotMark()
    {
        var invoice = AnyInvoice("A-1");
        var sw = new SubjectWatch(AnySubjectId, new HashSet<InvoiceReference>(), PreviousHwm);
        var repository = new FakeSubjectWatchRepository(sw);
        var provider = new FakeInvoiceListProvider(new FetchedWindow(new HashSet<InvoiceReference> { invoice.Ref }, [invoice], FetchedHwm));
        var notifier = new FakeNotifier((_, _) => new DeliveryResult.Failed(DeliveryResult.FailureKind.Permanent));
        var delay = new FakeDelay();
        var sut = new PollCycle(repository, provider, notifier, delay);

        await sut.RunAsync(AnySubjectId, AnyChannel, AmountDisplay.Brutto, AnyInterval, CancellationToken.None);

        Assert.Single(notifier.Calls);
        Assert.Empty(delay.Requested);
        Assert.DoesNotContain(invoice.Ref, sw.NotifiedRefs);
        Assert.Equal(PreviousHwm, sw.LastHwm);
    }

    [Fact]
    public async Task PermanentFailure_ReturnsOutcomeWithNullHwm_AndZeroNotified()
    {
        var invoice = AnyInvoice("A-1");
        var sw = new SubjectWatch(AnySubjectId, new HashSet<InvoiceReference>(), PreviousHwm);
        var repository = new FakeSubjectWatchRepository(sw);
        var provider = new FakeInvoiceListProvider(new FetchedWindow(new HashSet<InvoiceReference> { invoice.Ref }, [invoice], FetchedHwm));
        var notifier = new FakeNotifier((_, _) => new DeliveryResult.Failed(DeliveryResult.FailureKind.Permanent));
        var delay = new FakeDelay();
        var sut = new PollCycle(repository, provider, notifier, delay);

        var outcome = await sut.RunAsync(AnySubjectId, AnyChannel, AmountDisplay.Brutto, AnyInterval, CancellationToken.None);

        Assert.False(outcome.IsBaseline);
        Assert.Equal(1, outcome.FetchedCount);
        Assert.Equal(1, outcome.DetectedCount);
        Assert.Equal(0, outcome.NotifiedCount);
        Assert.Null(outcome.Hwm);
    }

    [Fact]
    public async Task RetryableFailure_ExhaustsThreeAttemptsWithBackoff_ThenStopsWithoutAdvancingHwm()
    {
        var invoice = AnyInvoice("A-1");
        var sw = new SubjectWatch(AnySubjectId, new HashSet<InvoiceReference>(), PreviousHwm);
        var repository = new FakeSubjectWatchRepository(sw);
        var provider = new FakeInvoiceListProvider(new FetchedWindow(new HashSet<InvoiceReference> { invoice.Ref }, [invoice], FetchedHwm));
        var notifier = new FakeNotifier((_, _) => new DeliveryResult.Failed(DeliveryResult.FailureKind.Retryable));
        var delay = new FakeDelay();
        var sut = new PollCycle(repository, provider, notifier, delay);

        await sut.RunAsync(AnySubjectId, AnyChannel, AmountDisplay.Brutto, AnyInterval, CancellationToken.None);

        Assert.Equal(3, notifier.Calls.Count);
        Assert.Equal([TimeSpan.FromSeconds(5), TimeSpan.FromSeconds(20)], delay.Requested);
        Assert.DoesNotContain(invoice.Ref, sw.NotifiedRefs);
        Assert.Equal(PreviousHwm, sw.LastHwm);
    }

    [Fact]
    public async Task RetryableFailure_SucceedsOnSecondAttempt_ContinuesCycleNormally()
    {
        var invoice = AnyInvoice("A-1");
        var sw = new SubjectWatch(AnySubjectId, new HashSet<InvoiceReference>(), PreviousHwm);
        var repository = new FakeSubjectWatchRepository(sw);
        var provider = new FakeInvoiceListProvider(new FetchedWindow(new HashSet<InvoiceReference> { invoice.Ref }, [invoice], FetchedHwm));
        var notifier = new FakeNotifier((_, attempt) => attempt == 1
            ? new DeliveryResult.Failed(DeliveryResult.FailureKind.Retryable)
            : new DeliveryResult.Confirmed());
        var delay = new FakeDelay();
        var sut = new PollCycle(repository, provider, notifier, delay);

        await sut.RunAsync(AnySubjectId, AnyChannel, AmountDisplay.Brutto, AnyInterval, CancellationToken.None);

        Assert.Equal(2, notifier.Calls.Count);
        Assert.Equal([TimeSpan.FromSeconds(5)], delay.Requested);
        Assert.Contains(invoice.Ref, sw.NotifiedRefs);
        Assert.Equal(FetchedHwm, sw.LastHwm);
    }

    [Fact]
    public async Task SecondInvoiceFailsPermanently_FirstStaysMarked_ButHwmDoesNotAdvance()
    {
        var invoiceA = AnyInvoice("A-1");
        var invoiceB = AnyInvoice("A-2");
        var refs = new HashSet<InvoiceReference> { invoiceA.Ref, invoiceB.Ref };
        var sw = new SubjectWatch(AnySubjectId, new HashSet<InvoiceReference>(), PreviousHwm);
        var repository = new FakeSubjectWatchRepository(sw);
        var provider = new FakeInvoiceListProvider(new FetchedWindow(refs, [invoiceA, invoiceB], FetchedHwm));
        var notifier = new FakeNotifier((invoice, _) => invoice.Ref.Equals(invoiceA.Ref)
            ? new DeliveryResult.Confirmed()
            : new DeliveryResult.Failed(DeliveryResult.FailureKind.Permanent));
        var delay = new FakeDelay();
        var sut = new PollCycle(repository, provider, notifier, delay);

        await sut.RunAsync(AnySubjectId, AnyChannel, AmountDisplay.Brutto, AnyInterval, CancellationToken.None);

        Assert.Contains(invoiceA.Ref, sw.NotifiedRefs);
        Assert.DoesNotContain(invoiceB.Ref, sw.NotifiedRefs);
        Assert.Equal(PreviousHwm, sw.LastHwm);
        Assert.NotNull(sw.PendingWindow);
    }
}
