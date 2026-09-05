using System.Net;
using KSeF.Client.Core.Exceptions;
using KSeF.Client.Core.Interfaces;
using KSeF.Client.Core.Interfaces.Clients;
using KSeF.Client.Core.Interfaces.Services;
using KSeF.Client.Core.Models.Authorization;
using KSeF.Client.Core.Models.Invoices;
using Environment = KSeF.Client.ClientFactory.Environment;

namespace KsefWatcher.KsefAccess;

/// <summary>
/// The single implementation of <see cref="IKsefQueryClient"/> wrapping the official
/// <c>ksef-client-csharp</c> (vendor/README.md; docs/08_ksef_access_tactical_model.md). The only
/// file that may reference its types (ACL enforcement point, docs/09_integration_contracts.md).
/// </summary>
/// <remarks>
/// Per-subject <c>environment</c> (OQ-9): each dependency is resolved per call from
/// <see cref="SubjectCredentials.Environment"/> (open) / <see cref="KsefSession.Environment"/>
/// (query), via factories rather than fixed instances — one daemon process can therefore serve
/// subjects across test/demo/prod simultaneously. Host's composition root supplies the factories,
/// built from the library's own <c>IKSeFClientFactory</c>/<c>IKSeFFactoryCryptographyServices</c>
/// (already internally cached per environment — see their implementations).
/// </remarks>
public sealed class KsefClientAdapter(
    Func<Environment, IAuthCoordinator> authCoordinatorFactory,
    Func<Environment, Task<ICryptographyService>> cryptographyServiceFactory,
    Func<Environment, IInvoiceDownloadClient> invoiceDownloadClientFactory) : IKsefQueryClient
{
    private const int PageSize = 250; // verified max page size (A7)

    public async Task<KsefSession> OpenSessionAsync(SubjectCredentials credentials, CancellationToken cancellationToken)
    {
        var environment = ParseEnvironment(credentials.Environment);
        var authCoordinator = authCoordinatorFactory(environment);
        var cryptographyService = await cryptographyServiceFactory(environment);

        try
        {
            var result = await authCoordinator.AuthKsefTokenAsync(
                AuthenticationTokenContextIdentifierType.Nip,
                credentials.Nip,
                credentials.Token,
                cryptographyService,
                cancellationToken: cancellationToken);

            return new KsefSession(result.AccessToken.Token, credentials.Environment);
        }
        catch (KsefRateLimitException ex)
        {
            throw new KsefRateLimitedException(ex.RecommendedDelay);
        }
        catch (KsefApiException ex) when (ex.StatusCode is HttpStatusCode.Unauthorized or HttpStatusCode.Forbidden)
        {
            throw new KsefAuthFailedException(ex.Message);
        }
    }

    public async Task<KsefQueryPage> QueryReceivedInvoicesAsync(KsefSession session, DateTimeOffset from, DateTimeOffset to, int pageOffset, CancellationToken cancellationToken)
    {
        var invoiceDownloadClient = invoiceDownloadClientFactory(ParseEnvironment(session.Environment));

        var filters = new InvoiceQueryFilters
        {
            SubjectType = InvoiceSubjectType.Subject2, // received invoices (verified API fact)
            DateRange = new DateRange
            {
                DateType = DateType.PermanentStorage,
                From = from,
                To = to,
                RestrictToPermanentStorageHwmDate = true, // snapshot mode, I-23
            },
        };

        PagedInvoiceResponse response;
        try
        {
            response = await invoiceDownloadClient.QueryInvoiceMetadataAsync(
                filters, session.Token, pageOffset, PageSize, cancellationToken: cancellationToken);
        }
        catch (KsefRateLimitException ex)
        {
            throw new KsefRateLimitedException(ex.RecommendedDelay);
        }
        catch (KsefApiException ex) when (ex.StatusCode is HttpStatusCode.Unauthorized or HttpStatusCode.Forbidden)
        {
            throw new KsefAuthFailedException(ex.Message);
        }

        var items = (response.Invoices ?? []).Select(Translate).ToList();
        return new KsefQueryPage(items, response.HasMore, response.IsTruncated, response.PermanentStorageHwmDate);
    }

    private static KsefInvoiceSummary Translate(InvoiceSummary raw) =>
        new(raw.KsefNumber, raw.InvoiceNumber, raw.NetAmount, raw.GrossAmount, raw.Currency, raw.Seller?.Nip ?? "", raw.Seller?.Name);

    /// <summary>No server-side "close" call exists for a lightweight accessToken (verified against ksef-client-csharp) — nothing to release.</summary>
    public Task CloseSessionAsync(KsefSession session, CancellationToken cancellationToken) => Task.CompletedTask;

    private static Environment ParseEnvironment(string name) => name.ToLowerInvariant() switch
    {
        "prod" => Environment.Prod,
        "demo" => Environment.Demo,
        _ => Environment.Test, // safe default (OQ-9)
    };
}
