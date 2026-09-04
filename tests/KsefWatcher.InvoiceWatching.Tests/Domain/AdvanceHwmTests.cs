using KsefWatcher.InvoiceWatching.Domain;
using KsefWatcher.InvoiceWatching.Domain.Events;
using KsefWatcher.InvoiceWatching.ValueObjects;
using Xunit;

namespace KsefWatcher.InvoiceWatching.Tests.Domain;

public class AdvanceHwmTests
{
    private static SubjectId AnySubjectId => new("5260001246");
    private static FetchWindow AnyWindow => new(DateTimeOffset.UtcNow.AddHours(-1), DateTimeOffset.UtcNow);

    [Fact]
    public void Throws_WhenNoWindowIsPending()
    {
        var sut = new SubjectWatch(AnySubjectId, new HashSet<InvoiceReference>(), new Hwm(DateTimeOffset.UtcNow));

        Assert.Throws<InvalidOperationException>(() => sut.AdvanceHwm());
    }

    [Fact]
    public void Throws_WhenNotEveryWindowRefIsNotifiedYet()
    {
        var refA = new InvoiceReference("A-1");
        var sut = new SubjectWatch(AnySubjectId, new HashSet<InvoiceReference>(), new Hwm(DateTimeOffset.UtcNow.AddHours(-1)));
        sut.Detect(AnyWindow, new FetchedWindow(new HashSet<InvoiceReference> { refA }, [], new Hwm(DateTimeOffset.UtcNow)));
        // refA never marked notified.

        Assert.Throws<InvalidOperationException>(() => sut.AdvanceHwm());
    }

    [Fact]
    public void AdvancesLastHwm_ClearsPendingWindow_AndRaisesCursorAdvanced_WhenEveryRefIsNotified()
    {
        var refA = new InvoiceReference("A-1");
        var newHwm = new Hwm(DateTimeOffset.UtcNow);
        var sut = new SubjectWatch(AnySubjectId, new HashSet<InvoiceReference>(), new Hwm(DateTimeOffset.UtcNow.AddHours(-1)));
        sut.Detect(AnyWindow, new FetchedWindow(new HashSet<InvoiceReference> { refA }, [], newHwm));
        sut.MarkNotified(new HashSet<InvoiceReference> { refA });

        sut.AdvanceHwm();

        Assert.Equal(newHwm, sut.LastHwm);
        Assert.Null(sut.PendingWindow);
        var raised = Assert.Single(sut.DomainEvents.OfType<CursorAdvanced>());
        Assert.Equal(newHwm, raised.LastHwm);
    }
}
