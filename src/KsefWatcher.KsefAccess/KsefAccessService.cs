using KsefWatcher.InvoiceWatching.Ports;
using KsefWatcher.InvoiceWatching.ValueObjects;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;

namespace KsefWatcher.KsefAccess;

/// <summary>
/// The single implementation of <see cref="IInvoiceListProvider"/> (docs/08_ksef_access_tactical_model.md).
/// Orchestrates: session open → query (all pages) → session close → translate. Owns no cursor state.
/// </summary>
/// <remarks>Built test-first against <see cref="IKsefQueryClient"/> fakes — the real adapter
/// wrapping the official ksef-client-csharp package is separate future work (docs/09_integration_contracts.md).</remarks>
public sealed class KsefAccessService(
    IKsefQueryClient client,
    ICredentialsStore credentialsStore,
    ILogger<KsefAccessService>? logger = null) : IInvoiceListProvider
{
    private const int MaxWindowSpanDays = 100; // KSeF API limit, verified (docs/07_define_invoice_watching.md)

    private readonly ILogger<KsefAccessService> _logger = logger ?? NullLogger<KsefAccessService>.Instance;

    public async Task<FetchedWindow> FetchWindowedListAsync(SubjectId subjectId, FetchWindow window, CancellationToken cancellationToken)
    {
        var credentials = credentialsStore.Current(subjectId);
        var session = await client.OpenSessionAsync(credentials, cancellationToken);
        try
        {
            var items = new List<DetectedInvoice>();
            DateTimeOffset? hwmUtc = null;

            foreach (var (subFrom, subTo) in SplitIntoSubWindows(window))
            {
                var pages = new List<KsefQueryPage>();
                KsefQueryPage page;
                do
                {
                    try
                    {
                        page = await client.QueryReceivedInvoicesAsync(session, subFrom, subTo, pages.Count, cancellationToken);
                    }
                    catch (KsefRateLimitedException ex)
                    {
                        _logger.LogWarning("SubjectPollFailed for {SubjectId}: rate limited, retry after {RetryAfter} (I-8).", subjectId, ex.RetryAfter);
                        throw new PollFailureException(new PollFailure.RateLimited(ex.RetryAfter));
                    }

                    pages.Add(page);
                } while (page.HasMore);

                if (pages.Any(p => p.IsTruncated))
                {
                    const string reason = "Result truncated (IsTruncated) — exceeds product assumptions for a legal window (I-8).";
                    _logger.LogError("SubjectPollFailed for {SubjectId}: {Reason}", subjectId, reason);
                    throw new PollFailureException(new PollFailure.ApiError(reason));
                }

                var lastPage = pages[^1];
                if (lastPage.PermanentStorageHwmDate is not { } subWindowHwm)
                {
                    const string reason = "Snapshot-mode response missing PermanentStorageHwmDate (I-6).";
                    _logger.LogError("SubjectPollFailed for {SubjectId}: {Reason}", subjectId, reason);
                    throw new PollFailureException(new PollFailure.ApiError(reason));
                }

                hwmUtc = subWindowHwm;
                items.AddRange(pages.SelectMany(p => p.Invoices).Select(Translate));
            }

            var refs = new HashSet<InvoiceReference>(items.Select(i => i.Ref));

            return new FetchedWindow(refs, items, new Hwm(hwmUtc!.Value));
        }
        finally
        {
            await client.CloseSessionAsync(session, cancellationToken);
        }
    }

    /// <summary>Splits into ≤100-day chunks (KSeF API limit) — a same-or-under-limit window yields
    /// itself unchanged as the sole chunk.</summary>
    private static IEnumerable<(DateTimeOffset From, DateTimeOffset To)> SplitIntoSubWindows(FetchWindow window)
    {
        var chunkFrom = window.From;
        while (chunkFrom < window.To)
        {
            var chunkTo = chunkFrom.AddDays(MaxWindowSpanDays);
            if (chunkTo > window.To)
            {
                chunkTo = window.To;
            }

            yield return (chunkFrom, chunkTo);
            chunkFrom = chunkTo;
        }
    }

    private static DetectedInvoice Translate(KsefInvoiceSummary raw) =>
        new(new InvoiceReference(raw.KsefNumber), raw.InvoiceNumber, raw.NetAmount, raw.GrossAmount, raw.Currency, raw.IssuerNip, raw.IssuerName);
}
