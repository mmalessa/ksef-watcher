namespace KsefWatcher.InvoiceWatching.Domain.Events;

/// <summary>
/// Marker for events raised by <see cref="SubjectWatch"/>. Domain events stay inside this
/// context (docs/08_invoice_watching_domain_model.md, "Deliberate absences: integration events —
/// none") — nothing here is published across a context boundary.
/// </summary>
public interface IDomainEvent;
