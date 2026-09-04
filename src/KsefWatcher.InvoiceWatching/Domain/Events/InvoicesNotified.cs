using KsefWatcher.InvoiceWatching.ValueObjects;

namespace KsefWatcher.InvoiceWatching.Domain.Events;

/// <summary>Raised when delivery is confirmed for a batch of refs (docs/08_invoice_watching_domain_model.md).</summary>
public sealed record InvoicesNotified(SubjectId SubjectId, IReadOnlySet<InvoiceReference> ConfirmedRefs) : IDomainEvent;
