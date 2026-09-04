using KsefWatcher.Host.Persistence;
using KsefWatcher.Host.Tests.TestDoubles;
using KsefWatcher.InvoiceWatching.Domain;
using KsefWatcher.InvoiceWatching.ValueObjects;
using Xunit;

namespace KsefWatcher.Host.Tests.Persistence;

public class SqliteSubjectWatchRepositoryTests : IDisposable
{
    private readonly TempSqliteFile _dbFile = new();
    private readonly SqliteSubjectWatchRepository _sut;

    public SqliteSubjectWatchRepositoryTests()
    {
        _sut = new SqliteSubjectWatchRepository(_dbFile.ConnectionString);
        _sut.EnsureSchemaAsync(CancellationToken.None).GetAwaiter().GetResult();
    }

    public void Dispose() => _dbFile.Dispose();

    [Fact]
    public async Task Load_UnknownSubject_ReturnsFreshNotOnboardedState()
    {
        var subjectId = new SubjectId("5260001246");

        var loaded = await _sut.LoadAsync(subjectId, CancellationToken.None);

        Assert.Equal(subjectId, loaded.SubjectId);
        Assert.Null(loaded.LastHwm);
        Assert.Empty(loaded.NotifiedRefs);
        Assert.Null(loaded.PendingWindow);
    }

    [Fact]
    public async Task SaveThenLoad_RoundTripsHwmAndNotifiedRefs()
    {
        var subjectId = new SubjectId("5260001246");
        var hwm = new Hwm(DateTimeOffset.Parse("2026-01-01T12:34:56.7890000Z"));
        var refs = new HashSet<InvoiceReference> { new("A-1"), new("A-2") };
        var subject = new SubjectWatch(subjectId, refs, hwm);

        await _sut.SaveAsync(subject, CancellationToken.None);
        var loaded = await _sut.LoadAsync(subjectId, CancellationToken.None);

        Assert.Equal(hwm, loaded.LastHwm);
        Assert.Equal(refs, loaded.NotifiedRefs);
    }

    [Fact]
    public async Task Save_TwiceWithOverlappingRefs_IsIdempotent_DoesNotThrow()
    {
        var subjectId = new SubjectId("5260001246");
        var hwm = new Hwm(DateTimeOffset.Parse("2026-01-01T00:00:00Z"));
        var refs = new HashSet<InvoiceReference> { new("A-1") };

        await _sut.SaveAsync(new SubjectWatch(subjectId, refs, hwm), CancellationToken.None);
        var exception = await Record.ExceptionAsync(() =>
            _sut.SaveAsync(new SubjectWatch(subjectId, refs, hwm), CancellationToken.None));
        var loaded = await _sut.LoadAsync(subjectId, CancellationToken.None);

        Assert.Null(exception);
        Assert.Single(loaded.NotifiedRefs);
    }

    [Fact]
    public async Task DifferentSubjects_AreIsolated()
    {
        var subjectA = new SubjectId("1111111111");
        var subjectB = new SubjectId("2222222222");
        var hwmA = new Hwm(DateTimeOffset.Parse("2026-01-01T00:00:00Z"));
        var hwmB = new Hwm(DateTimeOffset.Parse("2026-02-01T00:00:00Z"));

        await _sut.SaveAsync(new SubjectWatch(subjectA, new HashSet<InvoiceReference> { new("A-1") }, hwmA), CancellationToken.None);
        await _sut.SaveAsync(new SubjectWatch(subjectB, new HashSet<InvoiceReference> { new("B-1") }, hwmB), CancellationToken.None);

        var loadedA = await _sut.LoadAsync(subjectA, CancellationToken.None);
        var loadedB = await _sut.LoadAsync(subjectB, CancellationToken.None);

        Assert.Equal(hwmA, loadedA.LastHwm);
        Assert.Equal(hwmB, loadedB.LastHwm);
        Assert.Equal(new InvoiceReference("A-1"), Assert.Single(loadedA.NotifiedRefs));
        Assert.Equal(new InvoiceReference("B-1"), Assert.Single(loadedB.NotifiedRefs));
    }

    [Fact]
    public async Task SuccessiveSaves_AccumulateNotifiedRefs_AndAdvanceHwm()
    {
        var subjectId = new SubjectId("5260001246");
        var firstHwm = new Hwm(DateTimeOffset.Parse("2026-01-01T00:00:00Z"));
        var secondHwm = new Hwm(DateTimeOffset.Parse("2026-01-01T01:00:00Z"));

        await _sut.SaveAsync(new SubjectWatch(subjectId, new HashSet<InvoiceReference> { new("A-1") }, firstHwm), CancellationToken.None);
        var afterFirst = await _sut.LoadAsync(subjectId, CancellationToken.None);
        await _sut.SaveAsync(new SubjectWatch(subjectId, new HashSet<InvoiceReference>(afterFirst.NotifiedRefs) { new("A-2") }, secondHwm), CancellationToken.None);

        var loaded = await _sut.LoadAsync(subjectId, CancellationToken.None);

        Assert.Equal(secondHwm, loaded.LastHwm);
        Assert.Equal(new HashSet<InvoiceReference> { new("A-1"), new("A-2") }, loaded.NotifiedRefs);
    }

    [Fact]
    public async Task DeleteAsync_ResetsSubjectToFreshNotOnboardedState()
    {
        // I-19: removing a subject from config deliberately resets its state; re-adding starts
        // a fresh baseline (I-18) — this is the mechanism the Host's ConfigReloadCoordinator uses.
        var subjectId = new SubjectId("5260001246");
        var hwm = new Hwm(DateTimeOffset.Parse("2026-01-01T00:00:00Z"));
        await _sut.SaveAsync(new SubjectWatch(subjectId, new HashSet<InvoiceReference> { new("A-1") }, hwm), CancellationToken.None);

        await _sut.DeleteAsync(subjectId, CancellationToken.None);
        var loaded = await _sut.LoadAsync(subjectId, CancellationToken.None);

        Assert.Null(loaded.LastHwm);
        Assert.Empty(loaded.NotifiedRefs);
    }

    [Fact]
    public async Task DeleteAsync_DoesNotAffectOtherSubjects()
    {
        var subjectA = new SubjectId("1111111111");
        var subjectB = new SubjectId("2222222222");
        var hwm = new Hwm(DateTimeOffset.Parse("2026-01-01T00:00:00Z"));
        await _sut.SaveAsync(new SubjectWatch(subjectA, new HashSet<InvoiceReference> { new("A-1") }, hwm), CancellationToken.None);
        await _sut.SaveAsync(new SubjectWatch(subjectB, new HashSet<InvoiceReference> { new("B-1") }, hwm), CancellationToken.None);

        await _sut.DeleteAsync(subjectA, CancellationToken.None);
        var loadedB = await _sut.LoadAsync(subjectB, CancellationToken.None);

        Assert.Equal(hwm, loadedB.LastHwm);
        Assert.Single(loadedB.NotifiedRefs);
    }
}
