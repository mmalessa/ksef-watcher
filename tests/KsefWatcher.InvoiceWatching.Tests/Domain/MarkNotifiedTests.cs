using KsefWatcher.InvoiceWatching.Domain;
using KsefWatcher.InvoiceWatching.Domain.Events;
using KsefWatcher.InvoiceWatching.ValueObjects;
using Xunit;

namespace KsefWatcher.InvoiceWatching.Tests.Domain;

public class MarkNotifiedTests
{
    private static SubjectId AnySubjectId => new("5260001246");
    private static Hwm AnyHwm => new(DateTimeOffset.UtcNow);
    private static FetchWindow AnyWindow => new(DateTimeOffset.UtcNow.AddHours(-1), DateTimeOffset.UtcNow);

    private static SubjectWatch WithPendingWindow(params InvoiceReference[] fetchedRefs)
    {
        var sut = new SubjectWatch(AnySubjectId, new HashSet<InvoiceReference>(), new Hwm(DateTimeOffset.UtcNow.AddHours(-1)));
        sut.Detect(AnyWindow, new FetchedWindow(new HashSet<InvoiceReference>(fetchedRefs), [], AnyHwm));
        return sut;
    }

    [Fact]
    public void Throws_WhenNoWindowIsPending()
    {
        var sut = new SubjectWatch(AnySubjectId, new HashSet<InvoiceReference>(), new Hwm(DateTimeOffset.UtcNow));

        Assert.Throws<InvalidOperationException>(() => sut.MarkNotified(new HashSet<InvoiceReference> { new("A-1") }));
    }

    [Fact]
    public void Throws_WhenRefIsNotPartOfThePendingWindow()
    {
        var sut = WithPendingWindow(new InvoiceReference("A-1"));

        Assert.Throws<ArgumentException>(() => sut.MarkNotified(new HashSet<InvoiceReference> { new("NOT-IN-WINDOW") }));
    }

    [Fact]
    public void AppendsRefsToNotifiedRefs()
    {
        var refA = new InvoiceReference("A-1");
        var refB = new InvoiceReference("A-2");
        var sut = WithPendingWindow(refA, refB);

        sut.MarkNotified(new HashSet<InvoiceReference> { refA });

        Assert.Contains(refA, sut.NotifiedRefs);
        Assert.DoesNotContain(refB, sut.NotifiedRefs);
    }

    [Fact]
    public void RaisesInvoicesNotified_WithMarkedRefs()
    {
        var refA = new InvoiceReference("A-1");
        var sut = WithPendingWindow(refA);

        sut.MarkNotified(new HashSet<InvoiceReference> { refA });

        var raised = Assert.Single(sut.DomainEvents.OfType<InvoicesNotified>());
        Assert.Equal(new HashSet<InvoiceReference> { refA }, raised.ConfirmedRefs);
    }

    [Fact]
    public void ReMarkingAnAlreadyNotifiedRef_IsIdempotent()
    {
        var refA = new InvoiceReference("A-1");
        var sut = WithPendingWindow(refA);

        sut.MarkNotified(new HashSet<InvoiceReference> { refA });
        sut.MarkNotified(new HashSet<InvoiceReference> { refA });

        Assert.Single(sut.NotifiedRefs);
    }
}
