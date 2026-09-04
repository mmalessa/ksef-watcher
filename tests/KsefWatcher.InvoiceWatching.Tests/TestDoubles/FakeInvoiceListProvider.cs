using KsefWatcher.InvoiceWatching.Ports;
using KsefWatcher.InvoiceWatching.ValueObjects;

namespace KsefWatcher.InvoiceWatching.Tests.TestDoubles;

public sealed class FakeInvoiceListProvider(FetchedWindow result) : IInvoiceListProvider
{
    public List<(SubjectId SubjectId, FetchWindow Window)> Calls { get; } = [];

    public Task<FetchedWindow> FetchWindowedListAsync(SubjectId subjectId, FetchWindow window, CancellationToken cancellationToken)
    {
        Calls.Add((subjectId, window));
        return Task.FromResult(result);
    }
}
