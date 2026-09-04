using KsefWatcher.InvoiceWatching.ValueObjects;

namespace KsefWatcher.InvoiceWatching.Domain.Events;

/// <summary>Raised when a baseline is confirmed for a subject with no prior state (I-18, docs/08_invoice_watching_domain_model.md).</summary>
public sealed record SubjectOnboarded(SubjectId SubjectId, Hwm BaselineHwm) : IDomainEvent;
