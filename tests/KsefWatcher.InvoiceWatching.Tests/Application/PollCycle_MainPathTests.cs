using KsefWatcher.InvoiceWatching.Application;
using KsefWatcher.InvoiceWatching.Domain;
using KsefWatcher.InvoiceWatching.Tests.TestDoubles;
using KsefWatcher.InvoiceWatching.ValueObjects;
using Xunit;

namespace KsefWatcher.InvoiceWatching.Tests.Application;

public class PollCycle_MainPathTests
{
    private static SubjectId AnySubjectId => new("5260001246");
    private static readonly ChannelRef AnyChannel = new("discord", "https://example.invalid/webhook");
    private static readonly TimeSpan AnyInterval = TimeSpan.FromMinutes(60);

    [Fact]
    public async Task NothingNew_AdvancesHwm_WithoutCallingNotifier()
    {
        var previousHwm = new Hwm(DateTimeOffset.Parse("2026-01-01T00:00:00Z"));
        var newHwm = new Hwm(DateTimeOffset.Parse("2026-01-01T01:00:00Z"));
        var sw = new SubjectWatch(AnySubjectId, new HashSet<InvoiceReference>(), previousHwm);
        var repository = new FakeSubjectWatchRepository(sw);
        var provider = new FakeInvoiceListProvider(new FetchedWindow(new HashSet<InvoiceReference>(), [], newHwm));
        var notifier = new FakeNotifier((_, _) => new DeliveryResult.Confirmed());
        var delay = new FakeDelay();
        var sut = new PollCycle(repository, provider, notifier, delay);

        await sut.RunAsync(AnySubjectId, AnyChannel, AmountDisplay.Brutto, AnyInterval, CancellationToken.None);

        Assert.Equal(newHwm, sw.LastHwm);
        Assert.Null(sw.PendingWindow);
        Assert.Empty(notifier.Calls);
    }
}
