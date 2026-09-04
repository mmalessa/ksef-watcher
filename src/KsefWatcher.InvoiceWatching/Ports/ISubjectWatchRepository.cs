using KsefWatcher.InvoiceWatching.Domain;
using KsefWatcher.InvoiceWatching.ValueObjects;

namespace KsefWatcher.InvoiceWatching.Ports;

/// <summary>
/// The context's only persistence seam (docs/08_invoice_watching_aggregates.md). Implemented by
/// Host's SQLite adapter (docs/09_architecture.md) — this project never sees the concrete store.
/// </summary>
public interface ISubjectWatchRepository
{
    /// <summary>Returns a fresh instance; persistent state loaded, <c>pendingWindow</c> always empty (transient by design).</summary>
    Task<SubjectWatch> LoadAsync(SubjectId subjectId, CancellationToken cancellationToken);

    /// <summary>Persists notifiedRefs + lastHwm atomically.</summary>
    Task SaveAsync(SubjectWatch subject, CancellationToken cancellationToken);
}
