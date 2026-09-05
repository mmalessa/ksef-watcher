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
    public void Throws_WhenPendingWindowHwmIsBeforeCurrentLastHwm()
    {
        var currentHwm = new Hwm(DateTimeOffset.UtcNow);
        var earlierHwm = new Hwm(DateTimeOffset.UtcNow.AddMinutes(-30));
        var sut = new SubjectWatch(AnySubjectId, new HashSet<InvoiceReference>(), currentHwm);
        sut.Detect(AnyWindow, new FetchedWindow(new HashSet<InvoiceReference>(), [], earlierHwm));

        Assert.Throws<InvalidOperationException>(() => sut.AdvanceHwm());
    }

    [Fact]
    public void DoesNotThrow_WhenPendingWindowHwmEqualsCurrentLastHwm()
    {
        var sameHwm = new Hwm(DateTimeOffset.UtcNow);
        var sut = new SubjectWatch(AnySubjectId, new HashSet<InvoiceReference>(), sameHwm);
        sut.Detect(AnyWindow, new FetchedWindow(new HashSet<InvoiceReference>(), [], sameHwm));

        var exception = Record.Exception(() => sut.AdvanceHwm());

        Assert.Null(exception);
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
