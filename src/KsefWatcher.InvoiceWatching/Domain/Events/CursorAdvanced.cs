using KsefWatcher.InvoiceWatching.ValueObjects;

namespace KsefWatcher.InvoiceWatching.Domain.Events;

/// <summary>Raised when lastHwm advances because the whole window was notified (I-23, docs/08_invoice_watching_domain_model.md).</summary>
public sealed record CursorAdvanced(SubjectId SubjectId, Hwm LastHwm) : IDomainEvent;
