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
        KsefSession? session = null;
        try
        {
            try
            {
                session = await client.OpenSessionAsync(credentials, cancellationToken);
            }
            catch (KsefRateLimitedException ex)
            {
                throw LogAndBuildRateLimitedFailure(subjectId, ex);
            }
            catch (KsefAuthFailedException ex)
            {
                throw LogAndBuildAuthFailure(subjectId, ex);
            }

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
                        throw LogAndBuildRateLimitedFailure(subjectId, ex);
                    }
                    catch (KsefAuthFailedException ex)
                    {
                        throw LogAndBuildAuthFailure(subjectId, ex);
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
            if (session is not null)
            {
                await client.CloseSessionAsync(session, cancellationToken);
            }
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

    private PollFailureException LogAndBuildRateLimitedFailure(SubjectId subjectId, KsefRateLimitedException ex)
    {
        _logger.LogWarning("SubjectPollFailed for {SubjectId}: rate limited, retry after {RetryAfter} (I-8).", subjectId, ex.RetryAfter);
        return new PollFailureException(new PollFailure.RateLimited(ex.RetryAfter));
    }

    /// <summary>OQ-18: permanent, never self-heals — logged Error on every poll (no suppression),
    /// so the operator sees it recur until config.yaml's token is fixed.</summary>
    private PollFailureException LogAndBuildAuthFailure(SubjectId subjectId, KsefAuthFailedException ex)
    {
        _logger.LogError("SubjectPollFailed for {SubjectId}: auth rejected (I-8, OQ-18): {Reason}", subjectId, ex.Message);
        return new PollFailureException(new PollFailure.AuthFailure());
    }

    private static DetectedInvoice Translate(KsefInvoiceSummary raw) =>
        new(new InvoiceReference(raw.KsefNumber), raw.InvoiceNumber, raw.NetAmount, raw.GrossAmount, raw.Currency, raw.IssuerNip, raw.IssuerName);
}
