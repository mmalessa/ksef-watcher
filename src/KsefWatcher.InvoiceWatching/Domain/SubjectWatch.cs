using KsefWatcher.InvoiceWatching.Domain.Events;
using KsefWatcher.InvoiceWatching.ValueObjects;

namespace KsefWatcher.InvoiceWatching.Domain;

/// <summary>
/// One instance per subject — the entire persistent state of the Core Domain
/// (docs/08_invoice_watching_aggregates.md). Enforces every correctness invariant of the product.
/// </summary>
/// <remarks>
/// Command bodies are built test-first (docs/09_architecture.md's testability criterion) — see
/// <c>tests/KsefWatcher.InvoiceWatching.Tests/Domain</c> for the behavior each one enforces.
/// </remarks>
public sealed class SubjectWatch
{
    private readonly List<IDomainEvent> _domainEvents = [];
    private readonly TimeProvider _timeProvider;

    public SubjectId SubjectId { get; }
    public IReadOnlySet<InvoiceReference> NotifiedRefs { get; private set; }
    public Hwm? LastHwm { get; private set; }
    public PendingWindow? PendingWindow { get; private set; }
    public IReadOnlyList<IDomainEvent> DomainEvents => _domainEvents;

    public SubjectWatch(SubjectId subjectId, IReadOnlySet<InvoiceReference> notifiedRefs, Hwm? lastHwm, TimeProvider? timeProvider = null)
    {
        SubjectId = subjectId;
        NotifiedRefs = notifiedRefs;
        LastHwm = lastHwm;
        _timeProvider = timeProvider ?? TimeProvider.System;
    }

    /// <summary>I-18: baseline for a subject with no prior state. No-op if already onboarded.</summary>
    public void ConfirmBaseline(Hwm hwm)
    {
        if (LastHwm is not null)
        {
            return;
        }

        LastHwm = hwm;
        _domainEvents.Add(new SubjectOnboarded(SubjectId, hwm));
    }

    /// <summary>Returns {from = lastHwm, to = now}; requires LastHwm != null.</summary>
    public FetchWindow PlanFetch()
    {
        if (LastHwm is null)
        {
            throw new InvalidOperationException("Cannot plan a fetch before the baseline is confirmed (I-18).");
        }

        return new FetchWindow(LastHwm.Utc, _timeProvider.GetUtcNow());
    }

    /// <summary>I-23: diffs the fetched window against the registry; stashes PendingWindow.</summary>
    public void Detect(FetchWindow window, FetchedWindow fetched)
    {
        if (PendingWindow is not null)
        {
            throw new InvalidOperationException("Cannot start a new window while one is still pending.");
        }

        PendingWindow = new PendingWindow(window, fetched.Refs, fetched.Hwm);

        var unseenRefs = new HashSet<InvoiceReference>(fetched.Refs);
        unseenRefs.ExceptWith(NotifiedRefs);

        if (unseenRefs.Count > 0)
        {
            _domainEvents.Add(new NewInvoicesDetected(SubjectId, unseenRefs));
        }
    }

    /// <summary>I-1/I-5: append-only; idempotent re-marking is a no-op.</summary>
    public void MarkNotified(IReadOnlySet<InvoiceReference> refs)
    {
        if (PendingWindow is null)
        {
            throw new InvalidOperationException("Cannot mark refs as notified without a pending window.");
        }

        if (!refs.IsSubsetOf(PendingWindow.Refs))
        {
            throw new ArgumentException("All refs must belong to the current pending window.", nameof(refs));
        }

        var updated = new HashSet<InvoiceReference>(NotifiedRefs);
        updated.UnionWith(refs);
        NotifiedRefs = updated;

        _domainEvents.Add(new InvoicesNotified(SubjectId, refs));
    }

    /// <summary>I-23: only once every window ref is notified.</summary>
    public void AdvanceHwm()
    {
        if (PendingWindow is null)
        {
            throw new InvalidOperationException("Cannot advance the HWM without a pending window.");
        }

        if (!PendingWindow.Refs.IsSubsetOf(NotifiedRefs))
        {
            throw new InvalidOperationException("Cannot advance the HWM until every ref in the pending window is notified (I-23).");
        }

        var newHwm = PendingWindow.Hwm;
        LastHwm = newHwm;
        PendingWindow = null;
        _domainEvents.Add(new CursorAdvanced(SubjectId, newHwm));
    }
}
