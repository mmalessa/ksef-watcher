using KsefWatcher.InvoiceWatching.ValueObjects;
using KsefWatcher.KsefAccess;
using KsefWatcher.KsefAccess.Tests.TestDoubles;
using Microsoft.Extensions.Logging;
using Xunit;

namespace KsefWatcher.KsefAccess.Tests;

public class KsefAccessServiceTests
{
    private static SubjectId AnySubjectId => new("5260001246");
    private static SubjectCredentials AnyCredentials => new("5260001246", "token-abc", "test");
    private static FetchWindow AnyWindow => new(DateTimeOffset.Parse("2026-01-01T00:00:00Z"), DateTimeOffset.Parse("2026-01-01T01:00:00Z"));

    [Fact]
    public async Task SinglePage_TranslatesInvoiceAndHwm_OpensAndClosesSession()
    {
        var raw = new KsefInvoiceSummary("KSEF-1", "FV/1", 100m, 123m, "PLN", "1111111111", "Contractor Sp. z o.o.");
        var hwm = DateTimeOffset.Parse("2026-01-01T00:59:00Z");
        var page = new KsefQueryPage([raw], HasMore: false, IsTruncated: false, PermanentStorageHwmDate: hwm);
        var client = new FakeKsefQueryClient([page]);
        var credentialsStore = new FakeCredentialsStore(AnyCredentials);
        var sut = new KsefAccessService(client, credentialsStore);

        var result = await sut.FetchWindowedListAsync(AnySubjectId, AnyWindow, CancellationToken.None);

        Assert.Equal(hwm, result.Hwm.Utc);
        var invoice = Assert.Single(result.Detected);
        Assert.Equal("KSEF-1", invoice.Ref.KsefNumber);
        Assert.Equal("FV/1", invoice.InvoiceNumber);
        Assert.Equal(100m, invoice.NetAmount);
        Assert.Equal(123m, invoice.GrossAmount);
        Assert.Equal("PLN", invoice.Currency);
        Assert.Equal("1111111111", invoice.IssuerNip);
        Assert.Equal("Contractor Sp. z o.o.", invoice.IssuerName);
        Assert.Contains(invoice.Ref, result.Refs);

        Assert.Single(client.OpenSessionCalls);
        Assert.Equal(AnyCredentials, client.OpenSessionCalls[0]);
        Assert.Single(client.CloseSessionCalls);
    }

    [Fact]
    public async Task MultiplePages_PaginatesUntilHasMoreFalse_UsingWindowOnEveryPage()
    {
        var invoiceA = new KsefInvoiceSummary("KSEF-1", "FV/1", 100m, 123m, "PLN", "1111111111", null);
        var invoiceB = new KsefInvoiceSummary("KSEF-2", "FV/2", 200m, 246m, "PLN", "1111111111", null);
        var hwm = DateTimeOffset.Parse("2026-01-01T00:59:00Z");
        var pageOne = new KsefQueryPage([invoiceA], HasMore: true, IsTruncated: false, PermanentStorageHwmDate: null);
        var pageTwo = new KsefQueryPage([invoiceB], HasMore: false, IsTruncated: false, PermanentStorageHwmDate: hwm);
        var client = new FakeKsefQueryClient([pageOne, pageTwo]);
        var sut = new KsefAccessService(client, new FakeCredentialsStore(AnyCredentials));
        var window = AnyWindow;

        var result = await sut.FetchWindowedListAsync(AnySubjectId, window, CancellationToken.None);

        Assert.Equal(2, client.QueryCalls.Count);
        Assert.Equal((window.From, window.To, 0), client.QueryCalls[0]);
        Assert.Equal((window.From, window.To, 1), client.QueryCalls[1]);
        Assert.Equal(2, result.Detected.Count);
        Assert.Equal(hwm, result.Hwm.Utc);
        Assert.Single(client.CloseSessionCalls);
    }

    [Fact]
    public async Task SpanExceeding100Days_SplitsIntoSubWindows_AggregatesResults_UsesLastSubWindowHwm()
    {
        var from = DateTimeOffset.Parse("2026-01-01T00:00:00Z");
        var to = from.AddDays(250);
        var window = new FetchWindow(from, to);

        var invoiceA = new KsefInvoiceSummary("KSEF-1", "FV/1", 100m, 123m, "PLN", "1111111111", null);
        var invoiceB = new KsefInvoiceSummary("KSEF-2", "FV/2", 200m, 246m, "PLN", "1111111111", null);
        var invoiceC = new KsefInvoiceSummary("KSEF-3", "FV/3", 300m, 369m, "PLN", "1111111111", null);
        var hwm1 = from.AddDays(100);
        var hwm2 = from.AddDays(200);
        var hwm3 = from.AddDays(250);
        var pages = new[]
        {
            new KsefQueryPage([invoiceA], HasMore: false, IsTruncated: false, PermanentStorageHwmDate: hwm1),
            new KsefQueryPage([invoiceB], HasMore: false, IsTruncated: false, PermanentStorageHwmDate: hwm2),
            new KsefQueryPage([invoiceC], HasMore: false, IsTruncated: false, PermanentStorageHwmDate: hwm3),
        };
        var client = new FakeKsefQueryClient(pages);
        var sut = new KsefAccessService(client, new FakeCredentialsStore(AnyCredentials));

        var result = await sut.FetchWindowedListAsync(AnySubjectId, window, CancellationToken.None);

        Assert.Equal(3, client.QueryCalls.Count);
        Assert.Equal((from, from.AddDays(100), 0), client.QueryCalls[0]);
        Assert.Equal((from.AddDays(100), from.AddDays(200), 0), client.QueryCalls[1]);
        Assert.Equal((from.AddDays(200), to, 0), client.QueryCalls[2]);
        Assert.Equal(3, result.Detected.Count);
        Assert.Equal(hwm3, result.Hwm.Utc);
        Assert.Single(client.CloseSessionCalls);
    }

    [Fact]
    public async Task SpanExceeding100Days_TruncationOnSecondSubWindow_FailsLoudly_StillClosesSession()
    {
        var from = DateTimeOffset.Parse("2026-01-01T00:00:00Z");
        var to = from.AddDays(150);
        var window = new FetchWindow(from, to);

        var invoiceA = new KsefInvoiceSummary("KSEF-1", "FV/1", 100m, 123m, "PLN", "1111111111", null);
        var pages = new[]
        {
            new KsefQueryPage([invoiceA], HasMore: false, IsTruncated: false, PermanentStorageHwmDate: from.AddDays(100)),
            new KsefQueryPage([invoiceA], HasMore: false, IsTruncated: true, PermanentStorageHwmDate: from.AddDays(150)),
        };
        var client = new FakeKsefQueryClient(pages);
        var sut = new KsefAccessService(client, new FakeCredentialsStore(AnyCredentials));

        var ex = await Assert.ThrowsAsync<PollFailureException>(() =>
            sut.FetchWindowedListAsync(AnySubjectId, window, CancellationToken.None));

        Assert.IsType<PollFailure.ApiError>(ex.Reason);
        Assert.Equal(2, client.QueryCalls.Count);
        Assert.Single(client.CloseSessionCalls);
    }

    [Fact]
    public async Task MissingPermanentStorageHwmDate_FailsLoudly_StillClosesSession()
    {
        var raw = new KsefInvoiceSummary("KSEF-1", "FV/1", 100m, 123m, "PLN", "1111111111", null);
        var page = new KsefQueryPage([raw], HasMore: false, IsTruncated: false, PermanentStorageHwmDate: null);
        var client = new FakeKsefQueryClient([page]);
        var sut = new KsefAccessService(client, new FakeCredentialsStore(AnyCredentials));

        var ex = await Assert.ThrowsAsync<PollFailureException>(() =>
            sut.FetchWindowedListAsync(AnySubjectId, AnyWindow, CancellationToken.None));

        Assert.IsType<PollFailure.ApiError>(ex.Reason);
        Assert.Single(client.CloseSessionCalls);
    }

    [Fact]
    public async Task IsTruncated_FailsLoudly_InsteadOfSilentlyDroppingResults()
    {
        var raw = new KsefInvoiceSummary("KSEF-1", "FV/1", 100m, 123m, "PLN", "1111111111", null);
        var page = new KsefQueryPage([raw], HasMore: false, IsTruncated: true, PermanentStorageHwmDate: DateTimeOffset.UtcNow);
        var client = new FakeKsefQueryClient([page]);
        var sut = new KsefAccessService(client, new FakeCredentialsStore(AnyCredentials));

        var ex = await Assert.ThrowsAsync<PollFailureException>(() =>
            sut.FetchWindowedListAsync(AnySubjectId, AnyWindow, CancellationToken.None));

        Assert.IsType<PollFailure.ApiError>(ex.Reason);
        Assert.Single(client.CloseSessionCalls);
    }

    [Fact]
    public async Task RateLimited_ClassifiesAsPollFailure_PreservesRetryAfter_StillClosesSession()
    {
        var retryAfter = TimeSpan.FromSeconds(42);
        var client = new FakeKsefQueryClient([], new KsefRateLimitedException(retryAfter));
        var sut = new KsefAccessService(client, new FakeCredentialsStore(AnyCredentials));

        var ex = await Assert.ThrowsAsync<PollFailureException>(() =>
            sut.FetchWindowedListAsync(AnySubjectId, AnyWindow, CancellationToken.None));

        var reason = Assert.IsType<PollFailure.RateLimited>(ex.Reason);
        Assert.Equal(retryAfter, reason.RetryAfter);
        Assert.Single(client.CloseSessionCalls);
    }

    [Fact]
    public async Task RateLimited_LogsWarning()
    {
        var client = new FakeKsefQueryClient([], new KsefRateLimitedException(TimeSpan.FromSeconds(42)));
        var logger = new FakeLogger<KsefAccessService>();
        var sut = new KsefAccessService(client, new FakeCredentialsStore(AnyCredentials), logger);

        await Assert.ThrowsAsync<PollFailureException>(() =>
            sut.FetchWindowedListAsync(AnySubjectId, AnyWindow, CancellationToken.None));

        var entry = Assert.Single(logger.Entries);
        Assert.Equal(LogLevel.Warning, entry.Level);
    }

    [Fact]
    public async Task IsTruncated_LogsError()
    {
        var raw = new KsefInvoiceSummary("KSEF-1", "FV/1", 100m, 123m, "PLN", "1111111111", null);
        var page = new KsefQueryPage([raw], HasMore: false, IsTruncated: true, PermanentStorageHwmDate: DateTimeOffset.UtcNow);
        var client = new FakeKsefQueryClient([page]);
        var logger = new FakeLogger<KsefAccessService>();
        var sut = new KsefAccessService(client, new FakeCredentialsStore(AnyCredentials), logger);

        await Assert.ThrowsAsync<PollFailureException>(() =>
            sut.FetchWindowedListAsync(AnySubjectId, AnyWindow, CancellationToken.None));

        var entry = Assert.Single(logger.Entries);
        Assert.Equal(LogLevel.Error, entry.Level);
    }

    [Fact]
    public async Task MissingPermanentStorageHwmDate_LogsError()
    {
        var raw = new KsefInvoiceSummary("KSEF-1", "FV/1", 100m, 123m, "PLN", "1111111111", null);
        var page = new KsefQueryPage([raw], HasMore: false, IsTruncated: false, PermanentStorageHwmDate: null);
        var client = new FakeKsefQueryClient([page]);
        var logger = new FakeLogger<KsefAccessService>();
        var sut = new KsefAccessService(client, new FakeCredentialsStore(AnyCredentials), logger);

        await Assert.ThrowsAsync<PollFailureException>(() =>
            sut.FetchWindowedListAsync(AnySubjectId, AnyWindow, CancellationToken.None));

        var entry = Assert.Single(logger.Entries);
        Assert.Equal(LogLevel.Error, entry.Level);
    }
}
