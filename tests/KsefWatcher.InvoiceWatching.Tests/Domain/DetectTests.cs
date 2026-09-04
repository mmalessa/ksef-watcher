using KsefWatcher.InvoiceWatching.Domain;
using KsefWatcher.InvoiceWatching.Domain.Events;
using KsefWatcher.InvoiceWatching.ValueObjects;
using Xunit;

namespace KsefWatcher.InvoiceWatching.Tests.Domain;

public class DetectTests
{
    private static SubjectId AnySubjectId => new("5260001246");
    private static Hwm AnyHwm => new(DateTimeOffset.UtcNow);
    private static FetchWindow AnyWindow => new(DateTimeOffset.UtcNow.AddHours(-1), DateTimeOffset.UtcNow);

    private static SubjectWatch OnboardedWatch(IReadOnlySet<InvoiceReference>? notifiedRefs = null) =>
        new(AnySubjectId, notifiedRefs ?? new HashSet<InvoiceReference>(), new Hwm(DateTimeOffset.UtcNow.AddHours(-1)));

    [Fact]
    public void Throws_WhenAWindowIsAlreadyPending()
    {
        var sut = OnboardedWatch();
        var refs = new HashSet<InvoiceReference> { new("A-1") };
        sut.Detect(AnyWindow, new FetchedWindow(refs, [], AnyHwm));

        Assert.Throws<InvalidOperationException>(() => sut.Detect(AnyWindow, new FetchedWindow(refs, [], AnyHwm)));
    }

    [Fact]
    public void StashesPendingWindow_WithFetchedRefsAndHwm()
    {
        var sut = OnboardedWatch();
        var refs = new HashSet<InvoiceReference> { new("A-1"), new("A-2") };
        var hwm = AnyHwm;
        var window = AnyWindow;

        sut.Detect(window, new FetchedWindow(refs, [], hwm));

        Assert.NotNull(sut.PendingWindow);
        Assert.Equal(window, sut.PendingWindow!.Window);
        Assert.Equal(refs, sut.PendingWindow!.Refs);
        Assert.Equal(hwm, sut.PendingWindow!.Hwm);
    }

    [Fact]
    public void RaisesNewInvoicesDetected_WithRefsNotAlreadyNotified()
    {
        var alreadyNotified = new InvoiceReference("A-1");
        var unseen = new InvoiceReference("A-2");
        var sut = OnboardedWatch(new HashSet<InvoiceReference> { alreadyNotified });
        var fetchedRefs = new HashSet<InvoiceReference> { alreadyNotified, unseen };

        sut.Detect(AnyWindow, new FetchedWindow(fetchedRefs, [], AnyHwm));

        var raised = Assert.Single(sut.DomainEvents);
        var detected = Assert.IsType<NewInvoicesDetected>(raised);
        Assert.Equal(new HashSet<InvoiceReference> { unseen }, detected.UnseenRefs);
    }

    [Fact]
    public void DoesNotRaiseNewInvoicesDetected_WhenNothingUnseen()
    {
        var alreadyNotified = new InvoiceReference("A-1");
        var sut = OnboardedWatch(new HashSet<InvoiceReference> { alreadyNotified });

        sut.Detect(AnyWindow, new FetchedWindow(new HashSet<InvoiceReference> { alreadyNotified }, [], AnyHwm));

        Assert.Empty(sut.DomainEvents);
        Assert.NotNull(sut.PendingWindow);
    }
}
