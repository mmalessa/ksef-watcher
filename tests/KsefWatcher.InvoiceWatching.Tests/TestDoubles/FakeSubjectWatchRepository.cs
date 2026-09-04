using KsefWatcher.InvoiceWatching.Domain;
using KsefWatcher.InvoiceWatching.Ports;
using KsefWatcher.InvoiceWatching.ValueObjects;

namespace KsefWatcher.InvoiceWatching.Tests.TestDoubles;

/// <summary>In-memory fake — no KSeF sandbox, no real store needed to test PollCycle (docs/06_organise.md).</summary>
public sealed class FakeSubjectWatchRepository(SubjectWatch seed) : ISubjectWatchRepository
{
    public List<(Hwm? LastHwm, IReadOnlySet<InvoiceReference> NotifiedRefs)> SaveCalls { get; } = [];

    public Task<SubjectWatch> LoadAsync(SubjectId subjectId, CancellationToken cancellationToken) =>
        Task.FromResult(seed);

    public Task SaveAsync(SubjectWatch subject, CancellationToken cancellationToken)
    {
        SaveCalls.Add((subject.LastHwm, subject.NotifiedRefs));
        return Task.CompletedTask;
    }
}
