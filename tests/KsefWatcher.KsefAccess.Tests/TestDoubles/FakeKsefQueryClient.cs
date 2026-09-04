using KsefWatcher.KsefAccess;

namespace KsefWatcher.KsefAccess.Tests.TestDoubles;

/// <summary>
/// Records calls; returns <paramref name="pages"/> in order, one per <c>QueryReceivedInvoicesAsync</c>
/// call, unless <paramref name="queryException"/> is set — then it throws on the call at
/// <paramref name="throwOnPageIndex"/> (0-based) instead of returning that page.
/// </summary>
public sealed class FakeKsefQueryClient(
    IReadOnlyList<KsefQueryPage> pages,
    Exception? queryException = null,
    int throwOnPageIndex = 0) : IKsefQueryClient
{
    public List<SubjectCredentials> OpenSessionCalls { get; } = [];
    public List<(DateTimeOffset From, DateTimeOffset To, int PageOffset)> QueryCalls { get; } = [];
    public List<KsefSession> CloseSessionCalls { get; } = [];

    private int _queryCallCount;

    public Task<KsefSession> OpenSessionAsync(SubjectCredentials credentials, CancellationToken cancellationToken)
    {
        OpenSessionCalls.Add(credentials);
        return Task.FromResult(new KsefSession("fake-token"));
    }

    public Task<KsefQueryPage> QueryReceivedInvoicesAsync(KsefSession session, DateTimeOffset from, DateTimeOffset to, int pageOffset, CancellationToken cancellationToken)
    {
        QueryCalls.Add((from, to, pageOffset));
        var callIndex = _queryCallCount++;

        if (queryException is not null && callIndex == throwOnPageIndex)
        {
            throw queryException;
        }

        return Task.FromResult(pages[callIndex]);
    }

    public Task CloseSessionAsync(KsefSession session, CancellationToken cancellationToken)
    {
        CloseSessionCalls.Add(session);
        return Task.CompletedTask;
    }
}
