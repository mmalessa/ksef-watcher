using KsefWatcher.InvoiceWatching.ValueObjects;

namespace KsefWatcher.InvoiceWatching.Domain.Events;

/// <summary>Raised when a fetched window contains refs not present in the registry (docs/08_invoice_watching_domain_model.md).</summary>
public sealed record NewInvoicesDetected(SubjectId SubjectId, IReadOnlySet<InvoiceReference> UnseenRefs) : IDomainEvent;
