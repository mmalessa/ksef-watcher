using KsefWatcher.InvoiceWatching.Domain;
using KsefWatcher.InvoiceWatching.Ports;
using KsefWatcher.InvoiceWatching.ValueObjects;

namespace KsefWatcher.Host.Tests.TestDoubles;

/// <summary>
/// Fakes for PollCycle's dependencies, used only to satisfy PollingBackgroundServiceTests'
/// constructor requirements — these tests exercise StartSubject's synchronous scheduling/logging,
/// never an actual poll cycle, so every member throws if it's ever really called.
/// </summary>
public sealed class NeverExercisedSubjectWatchRepository : ISubjectWatchRepository
{
    public Task<SubjectWatch> LoadAsync(SubjectId subjectId, CancellationToken cancellationToken) => throw new NotSupportedException();
    public Task SaveAsync(SubjectWatch subject, CancellationToken cancellationToken) => throw new NotSupportedException();
}

public sealed class NeverExercisedInvoiceListProvider : IInvoiceListProvider
{
    public Task<FetchedWindow> FetchWindowedListAsync(SubjectId subjectId, FetchWindow window, CancellationToken cancellationToken) => throw new NotSupportedException();
}

public sealed class NeverExercisedNotifier : INotifier
{
    public Task<DeliveryResult> SendAsync(ChannelRef channel, DetectedInvoice invoice, AmountDisplay amountDisplay, CancellationToken cancellationToken) => throw new NotSupportedException();
    public Task<DeliveryResult> SendHeartbeatAsync(ChannelRef channel, DateOnly asOf, CancellationToken cancellationToken) => throw new NotSupportedException();
}

public sealed class NeverExercisedDelay : IDelay
{
    public Task WaitAsync(TimeSpan duration, CancellationToken cancellationToken) => throw new NotSupportedException();
}

/// <summary>Functional fakes — for tests that exercise an actual poll cycle end to end (e.g. its logging).</summary>
public sealed class FakeSubjectWatchRepository(SubjectWatch seed) : ISubjectWatchRepository
{
    public Task<SubjectWatch> LoadAsync(SubjectId subjectId, CancellationToken cancellationToken) => Task.FromResult(seed);
    public Task SaveAsync(SubjectWatch subject, CancellationToken cancellationToken) => Task.CompletedTask;
}

public sealed class FakeInvoiceListProvider(FetchedWindow result) : IInvoiceListProvider
{
    public Task<FetchedWindow> FetchWindowedListAsync(SubjectId subjectId, FetchWindow window, CancellationToken cancellationToken) => Task.FromResult(result);
}

public sealed class FakeNotifier(DeliveryResult result) : INotifier
{
    public Task<DeliveryResult> SendAsync(ChannelRef channel, DetectedInvoice invoice, AmountDisplay amountDisplay, CancellationToken cancellationToken) => Task.FromResult(result);
    public Task<DeliveryResult> SendHeartbeatAsync(ChannelRef channel, DateOnly asOf, CancellationToken cancellationToken) => Task.FromResult(result);
}

public sealed class FakeDelay : IDelay
{
    public Task WaitAsync(TimeSpan duration, CancellationToken cancellationToken) => Task.CompletedTask;
}
