using System.Diagnostics;
using KsefWatcher.InvoiceWatching.Domain.Events;
using KsefWatcher.InvoiceWatching.Ports;
using KsefWatcher.InvoiceWatching.ValueObjects;

namespace KsefWatcher.InvoiceWatching.Application;

/// <summary>
/// Orchestrates one poll for one subject (docs/08_invoice_watching_domain_services.md). Owns no
/// state — all state lives in <c>SubjectWatch</c>. Built test-first against fakes for
/// <see cref="IInvoiceListProvider"/>, <see cref="INotifier"/> and <see cref="IDelay"/>
/// (docs/06_organise.md) — see <c>tests/KsefWatcher.InvoiceWatching.Tests/Application</c>.
/// </summary>
public sealed class PollCycle
{
    private static readonly TimeSpan InterMessageDelay = TimeSpan.FromSeconds(3); // OQ-11
    private const int MaxAttempts = 3; // OQ-17c

    // OQ-17c's wording ("3 attempts, backoff 5s→20s→60s") is one delay short of unambiguous for a
    // 3-attempt cap (3 attempts have only 2 gaps between them). Read literally as a hard 3-attempt
    // cap: backoff applies between attempts 1→2 and 2→3, so 60s is defined for traceability but
    // never actually awaited at MaxAttempts=3 — it activates on its own if that cap is ever raised.
    private static readonly TimeSpan[] RetryBackoffs = [TimeSpan.FromSeconds(5), TimeSpan.FromSeconds(20), TimeSpan.FromSeconds(60)];

    private readonly ISubjectWatchRepository _repository;
    private readonly IInvoiceListProvider _provider;
    private readonly INotifier _notifier;
    private readonly IDelay _delay;
    private readonly TimeProvider _timeProvider;

    public PollCycle(ISubjectWatchRepository repository, IInvoiceListProvider provider, INotifier notifier, IDelay delay, TimeProvider? timeProvider = null)
    {
        _repository = repository;
        _provider = provider;
        _notifier = notifier;
        _delay = delay;
        _timeProvider = timeProvider ?? TimeProvider.System;
    }

    public async Task<PollOutcome> RunAsync(SubjectId subjectId, ChannelRef channel, AmountDisplay amountDisplay, TimeSpan configuredInterval, CancellationToken cancellationToken)
    {
        var sw = await _repository.LoadAsync(subjectId, cancellationToken);

        if (sw.LastHwm is null)
        {
            var now = _timeProvider.GetUtcNow();
            var narrowWindow = new FetchWindow(now - configuredInterval, now);
            var baselineFetch = await _provider.FetchWindowedListAsync(subjectId, narrowWindow, cancellationToken);
            sw.ConfirmBaseline(baselineFetch.Hwm);
            await _repository.SaveAsync(sw, cancellationToken);
            return new PollOutcome(IsBaseline: true, FetchedCount: baselineFetch.Refs.Count, DetectedCount: 0, NotifiedCount: 0, Hwm: baselineFetch.Hwm);
        }

        var window = sw.PlanFetch();
        var fetched = await _provider.FetchWindowedListAsync(subjectId, window, cancellationToken);
        sw.Detect(window, fetched);

        var unseenRefs = sw.DomainEvents.OfType<NewInvoicesDetected>().SingleOrDefault()?.UnseenRefs
            ?? new HashSet<InvoiceReference>();
        var toSend = fetched.Detected.Where(invoice => unseenRefs.Contains(invoice.Ref)).ToList();

        for (var i = 0; i < toSend.Count; i++)
        {
            var invoice = toSend[i];
            var result = await SendWithBackoffAsync(channel, invoice, amountDisplay, cancellationToken);
            if (result is not DeliveryResult.Confirmed)
            {
                // Failed(Permanent): no point retrying further (I-11). Failed(Retryable) exhausted:
                // the next scheduled poll re-plans this window (OQ-17c). Either way, the cursor
                // must not advance past an un-notified ref (I-1) — stop the whole cycle here.
                return new PollOutcome(IsBaseline: false, FetchedCount: fetched.Refs.Count, DetectedCount: unseenRefs.Count, NotifiedCount: i, Hwm: null);
            }

            sw.MarkNotified(new HashSet<InvoiceReference> { invoice.Ref });
            await _repository.SaveAsync(sw, cancellationToken);

            var isLast = i == toSend.Count - 1;
            if (!isLast)
            {
                await _delay.WaitAsync(InterMessageDelay, cancellationToken);
            }
        }

        sw.AdvanceHwm();
        await _repository.SaveAsync(sw, cancellationToken);

        return new PollOutcome(IsBaseline: false, FetchedCount: fetched.Refs.Count, DetectedCount: unseenRefs.Count, NotifiedCount: toSend.Count, Hwm: sw.LastHwm);
    }

    /// <summary>
    /// Hybrid retry (OQ-17c): up to <see cref="MaxAttempts"/> attempts with backoff between them.
    /// Stops immediately on Failed(Permanent) — no point retrying a revoked webhook (I-11).
    /// </summary>
    private async Task<DeliveryResult> SendWithBackoffAsync(ChannelRef channel, DetectedInvoice invoice, AmountDisplay amountDisplay, CancellationToken cancellationToken)
    {
        for (var attempt = 1; attempt <= MaxAttempts; attempt++)
        {
            var result = await _notifier.SendAsync(channel, invoice, amountDisplay, cancellationToken);
            if (result is DeliveryResult.Confirmed or DeliveryResult.Failed(DeliveryResult.FailureKind.Permanent))
            {
                return result;
            }

            if (attempt < MaxAttempts)
            {
                await _delay.WaitAsync(RetryBackoffs[attempt - 1], cancellationToken);
            }
            else
            {
                return result;
            }
        }

        throw new UnreachableException();
    }
}
