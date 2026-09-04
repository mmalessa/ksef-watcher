using KsefWatcher.InvoiceWatching.ValueObjects;

namespace KsefWatcher.InvoiceWatching.Ports;

/// <summary>
/// Owned by Invoice Watching, implemented by KSeF Access (docs/08_invoice_watching_domain_services.md).
/// Window-in, windowed-result: the provider holds no cursor state of its own.
/// </summary>
public interface IInvoiceListProvider
{
    Task<FetchedWindow> FetchWindowedListAsync(SubjectId subjectId, FetchWindow window, CancellationToken cancellationToken);
}
