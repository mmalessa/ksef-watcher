using KsefWatcher.InvoiceWatching.Domain;
using KsefWatcher.InvoiceWatching.Tests.TestDoubles;
using KsefWatcher.InvoiceWatching.ValueObjects;
using Xunit;

namespace KsefWatcher.InvoiceWatching.Tests.Domain;

public class PlanFetchTests
{
    private static SubjectId AnySubjectId => new("5260001246");

    [Fact]
    public void Throws_WhenNotYetOnboarded()
    {
        var sut = new SubjectWatch(AnySubjectId, new HashSet<InvoiceReference>(), lastHwm: null);

        Assert.Throws<InvalidOperationException>(() => sut.PlanFetch());
    }

    [Fact]
    public void ReturnsWindow_FromLastHwmToNow()
    {
        var lastHwm = new Hwm(DateTimeOffset.Parse("2026-01-01T00:00:00Z"));
        var now = DateTimeOffset.Parse("2026-01-01T01:00:00Z");
        var sut = new SubjectWatch(AnySubjectId, new HashSet<InvoiceReference>(), lastHwm, new FixedTimeProvider(now));

        var window = sut.PlanFetch();

        Assert.Equal(lastHwm.Utc, window.From);
        Assert.Equal(now, window.To);
    }
}
