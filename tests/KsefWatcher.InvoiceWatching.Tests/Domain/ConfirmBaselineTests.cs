using KsefWatcher.InvoiceWatching.Domain;
using KsefWatcher.InvoiceWatching.Domain.Events;
using KsefWatcher.InvoiceWatching.ValueObjects;
using Xunit;

namespace KsefWatcher.InvoiceWatching.Tests.Domain;

public class ConfirmBaselineTests
{
    private static SubjectId AnySubjectId => new("5260001246");

    [Fact]
    public void SetsLastHwm_WhenNotYetOnboarded()
    {
        var sut = new SubjectWatch(AnySubjectId, new HashSet<InvoiceReference>(), lastHwm: null);
        var hwm = new Hwm(DateTimeOffset.UtcNow);

        sut.ConfirmBaseline(hwm);

        Assert.Equal(hwm, sut.LastHwm);
    }

    [Fact]
    public void DoesNotPopulateNotifiedRefs()
    {
        var sut = new SubjectWatch(AnySubjectId, new HashSet<InvoiceReference>(), lastHwm: null);

        sut.ConfirmBaseline(new Hwm(DateTimeOffset.UtcNow));

        Assert.Empty(sut.NotifiedRefs);
    }

    [Fact]
    public void RaisesSubjectOnboarded_WithBaselineHwm()
    {
        var sut = new SubjectWatch(AnySubjectId, new HashSet<InvoiceReference>(), lastHwm: null);
        var hwm = new Hwm(DateTimeOffset.UtcNow);

        sut.ConfirmBaseline(hwm);

        var raised = Assert.Single(sut.DomainEvents);
        var onboarded = Assert.IsType<SubjectOnboarded>(raised);
        Assert.Equal(sut.SubjectId, onboarded.SubjectId);
        Assert.Equal(hwm, onboarded.BaselineHwm);
    }

    [Fact]
    public void IsNoOp_WhenAlreadyOnboarded()
    {
        var originalHwm = new Hwm(DateTimeOffset.UtcNow.AddDays(-1));
        var sut = new SubjectWatch(AnySubjectId, new HashSet<InvoiceReference>(), originalHwm);

        sut.ConfirmBaseline(new Hwm(DateTimeOffset.UtcNow));

        Assert.Equal(originalHwm, sut.LastHwm);
        Assert.Empty(sut.DomainEvents);
    }
}
