using KSeF.Client.Core.Interfaces.Clients;
using KSeF.Client.Core.Models;
using KSeF.Client.Core.Models.Invoices;

namespace KsefWatcher.KsefAccess.Tests.TestDoubles;

public sealed class FakeInvoiceDownloadClient(Func<InvoiceQueryFilters, string, int?, int?, PagedInvoiceResponse> queryInvoiceMetadata)
    : IInvoiceDownloadClient
{
    public List<(InvoiceQueryFilters Filters, string AccessToken, int? PageOffset, int? PageSize)> QueryCalls { get; } = [];

    public Task<PagedInvoiceResponse> QueryInvoiceMetadataAsync(
        InvoiceQueryFilters requestPayload,
        string accessToken,
        int? pageOffset = null,
        int? pageSize = null,
        SortOrder sortOrder = SortOrder.Asc,
        CancellationToken cancellationToken = default)
    {
        QueryCalls.Add((requestPayload, accessToken, pageOffset, pageSize));
        return Task.FromResult(queryInvoiceMetadata(requestPayload, accessToken, pageOffset, pageSize));
    }

    private static NotSupportedException NotNeeded() => new("Not exercised by KsefClientAdapter's query path.");

    public Task<string> GetInvoiceAsync(string ksefNumber, string accessToken, CancellationToken cancellationToken = default) => throw NotNeeded();

    [Obsolete]
    public Task<OperationResponse> ExportInvoicesAsync(InvoiceExportRequest requestPayload, string accessToken, bool includeMetadata = true, CancellationToken cancellationToken = default) => throw NotNeeded();

    public Task<OperationResponse> ExportInvoicesAsync(InvoiceExportRequest requestPayload, string accessToken, CancellationToken cancellationToken = default) => throw NotNeeded();

    public Task<InvoiceExportStatusResponse> GetInvoiceExportStatusAsync(string referenceNumber, string accessToken, CancellationToken cancellationToken = default) => throw NotNeeded();
}
