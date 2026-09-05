using System.Net;
using KSeF.Client.Core.Exceptions;
using KSeF.Client.Core.Interfaces;
using KSeF.Client.Core.Interfaces.Clients;
using KSeF.Client.Core.Interfaces.Services;
using KSeF.Client.Core.Models.Authorization;
using KSeF.Client.Core.Models.Invoices;
using KsefWatcher.KsefAccess.Tests.TestDoubles;
using Xunit;
using Environment = KSeF.Client.ClientFactory.Environment;

namespace KsefWatcher.KsefAccess.Tests;

public class KsefClientAdapterTests
{
    private static SubjectCredentials AnyCredentials => new("1234567890", "ksef-token-abc", "test");

    /// <summary>Wraps fixed fakes in environment-ignoring factories — for tests unconcerned with
    /// per-environment resolution (that's <see cref="OpenSessionAsync_ResolvesAuthCoordinatorAndCryptographyService_ForCredentialsEnvironment"/>'s job).</summary>
    private static KsefClientAdapter NewSut(IAuthCoordinator authCoordinator, ICryptographyService cryptographyService, IInvoiceDownloadClient invoiceDownloadClient) =>
        new(_ => authCoordinator, _ => Task.FromResult(cryptographyService), _ => invoiceDownloadClient);

    [Fact]
    public async Task OpenSessionAsync_AuthenticatesWithKsefToken_ReturnsSessionWrappingAccessToken()
    {
        var authCoordinator = new FakeAuthCoordinator((contextType, contextValue, tokenKsef, encryptionMethod) =>
            new AuthenticationOperationStatusResponse
            {
                AccessToken = new TokenInfo { Token = "resolved-access-token" },
                RefreshToken = new TokenInfo { Token = "refresh-token" },
            });
        var sut = NewSut(authCoordinator, new FakeCryptographyService(), new FakeInvoiceDownloadClient((_, _, _, _) => throw new NotSupportedException()));

        var session = await sut.OpenSessionAsync(AnyCredentials, CancellationToken.None);

        Assert.Equal("resolved-access-token", session.Token);
        var call = Assert.Single(authCoordinator.AuthKsefTokenCalls);
        Assert.Equal(AuthenticationTokenContextIdentifierType.Nip, call.ContextType);
        Assert.Equal("1234567890", call.ContextValue);
        Assert.Equal("ksef-token-abc", call.TokenKsef);
    }

    [Fact]
    public async Task OpenSessionAsync_UsesRsaEncryptionMethod()
    {
        // The real KSeF sandbox issues an RSA certificate for KsefTokenEncryption, not ECDSA
        // (verified against a real poll cycle) — the vendor library's own default (ECDsa) fails
        // with "Nie znaleziono klucza ECDSA." against that certificate.
        var authCoordinator = new FakeAuthCoordinator((_, _, _, _) =>
            new AuthenticationOperationStatusResponse { AccessToken = new TokenInfo { Token = "access-token" } });
        var sut = NewSut(authCoordinator, new FakeCryptographyService(), new FakeInvoiceDownloadClient((_, _, _, _) => throw new NotSupportedException()));

        await sut.OpenSessionAsync(AnyCredentials, CancellationToken.None);

        var call = Assert.Single(authCoordinator.AuthKsefTokenCalls);
        Assert.Equal(EncryptionMethodEnum.Rsa, call.EncryptionMethod);
    }

    [Fact]
    public async Task OpenSessionAsync_RateLimited_ClassifiesAsOurRateLimitedException_PreservingRecommendedDelay()
    {
        var authCoordinator = new FakeAuthCoordinator((_, _, _, _) =>
            throw new KsefRateLimitException("Too many requests", retryAfterSeconds: 42));
        var sut = NewSut(authCoordinator, new FakeCryptographyService(), new FakeInvoiceDownloadClient((_, _, _, _) => throw new NotSupportedException()));

        var ex = await Assert.ThrowsAsync<KsefRateLimitedException>(() =>
            sut.OpenSessionAsync(AnyCredentials, CancellationToken.None));

        Assert.Equal(TimeSpan.FromSeconds(42), ex.RetryAfter);
    }

    private static FakeAuthCoordinator AlwaysAuthenticated => new((_, _, _, _) =>
        new AuthenticationOperationStatusResponse { AccessToken = new TokenInfo { Token = "access-token" } });

    [Fact]
    public async Task QueryReceivedInvoicesAsync_BuildsCorrectFilters_TranslatesResponse()
    {
        var from = DateTimeOffset.Parse("2026-01-01T00:00:00Z");
        var to = DateTimeOffset.Parse("2026-01-01T01:00:00Z");
        var hwm = DateTimeOffset.Parse("2026-01-01T00:59:00Z");
        var rawInvoice = new InvoiceSummary
        {
            KsefNumber = "KSEF-1",
            InvoiceNumber = "FV/1",
            NetAmount = 100m,
            GrossAmount = 123m,
            Currency = "PLN",
            Seller = new Seller { Nip = "1111111111", Name = "Contractor Sp. z o.o." },
        };
        var response = new PagedInvoiceResponse
        {
            HasMore = true,
            IsTruncated = false,
            PermanentStorageHwmDate = hwm,
            Invoices = [rawInvoice],
        };
        var invoiceDownloadClient = new FakeInvoiceDownloadClient((_, _, _, _) => response);
        var sut = NewSut(AlwaysAuthenticated, new FakeCryptographyService(), invoiceDownloadClient);
        var session = await sut.OpenSessionAsync(AnyCredentials, CancellationToken.None);

        var page = await sut.QueryReceivedInvoicesAsync(session, from, to, pageOffset: 2, CancellationToken.None);

        var call = Assert.Single(invoiceDownloadClient.QueryCalls);
        Assert.Equal("access-token", call.AccessToken);
        Assert.Equal(2, call.PageOffset);
        Assert.Equal(InvoiceSubjectType.Subject2, call.Filters.SubjectType);
        Assert.Equal(DateType.PermanentStorage, call.Filters.DateRange.DateType);
        Assert.Equal(from, call.Filters.DateRange.From);
        Assert.Equal(to, call.Filters.DateRange.To);
        Assert.True(call.Filters.DateRange.RestrictToPermanentStorageHwmDate);

        Assert.True(page.HasMore);
        Assert.False(page.IsTruncated);
        Assert.Equal(hwm, page.PermanentStorageHwmDate);
        var translated = Assert.Single(page.Invoices);
        Assert.Equal("KSEF-1", translated.KsefNumber);
        Assert.Equal("FV/1", translated.InvoiceNumber);
        Assert.Equal(100m, translated.NetAmount);
        Assert.Equal(123m, translated.GrossAmount);
        Assert.Equal("PLN", translated.Currency);
        Assert.Equal("1111111111", translated.IssuerNip);
        Assert.Equal("Contractor Sp. z o.o.", translated.IssuerName);
    }

    [Fact]
    public async Task QueryReceivedInvoicesAsync_RateLimited_ClassifiesAsOurRateLimitedException()
    {
        var invoiceDownloadClient = new FakeInvoiceDownloadClient((_, _, _, _) =>
            throw new KsefRateLimitException("Too many requests", retryAfterSeconds: 7));
        var sut = NewSut(AlwaysAuthenticated, new FakeCryptographyService(), invoiceDownloadClient);
        var session = await sut.OpenSessionAsync(AnyCredentials, CancellationToken.None);

        var ex = await Assert.ThrowsAsync<KsefRateLimitedException>(() =>
            sut.QueryReceivedInvoicesAsync(session, DateTimeOffset.UtcNow.AddHours(-1), DateTimeOffset.UtcNow, 0, CancellationToken.None));

        Assert.Equal(TimeSpan.FromSeconds(7), ex.RetryAfter);
    }

    [Fact]
    public async Task QueryReceivedInvoicesAsync_MissingSeller_TranslatesToEmptyNipAndNullName()
    {
        var response = new PagedInvoiceResponse
        {
            HasMore = false,
            IsTruncated = false,
            PermanentStorageHwmDate = DateTimeOffset.UtcNow,
            Invoices = [new InvoiceSummary { KsefNumber = "KSEF-1", InvoiceNumber = "FV/1", Currency = "PLN", Seller = null }],
        };
        var sut = NewSut(AlwaysAuthenticated, new FakeCryptographyService(), new FakeInvoiceDownloadClient((_, _, _, _) => response));
        var session = await sut.OpenSessionAsync(AnyCredentials, CancellationToken.None);

        var page = await sut.QueryReceivedInvoicesAsync(session, DateTimeOffset.UtcNow.AddHours(-1), DateTimeOffset.UtcNow, 0, CancellationToken.None);

        var translated = Assert.Single(page.Invoices);
        Assert.Equal(string.Empty, translated.IssuerNip);
        Assert.Null(translated.IssuerName);
    }

    [Fact]
    public async Task OpenSessionAsync_ResolvesAuthCoordinatorAndCryptographyService_ForCredentialsEnvironment()
    {
        var requestedAuthEnvironments = new List<Environment>();
        var requestedCryptoEnvironments = new List<Environment>();
        var sut = new KsefClientAdapter(
            env => { requestedAuthEnvironments.Add(env); return AlwaysAuthenticated; },
            env => { requestedCryptoEnvironments.Add(env); return Task.FromResult<ICryptographyService>(new FakeCryptographyService()); },
            _ => new FakeInvoiceDownloadClient((_, _, _, _) => throw new NotSupportedException()));
        var credentials = new SubjectCredentials("1234567890", "ksef-token-abc", "prod");

        await sut.OpenSessionAsync(credentials, CancellationToken.None);

        Assert.Equal(Environment.Prod, Assert.Single(requestedAuthEnvironments));
        Assert.Equal(Environment.Prod, Assert.Single(requestedCryptoEnvironments));
    }

    [Fact]
    public async Task QueryReceivedInvoicesAsync_ResolvesInvoiceDownloadClient_ForSessionEnvironment()
    {
        var response = new PagedInvoiceResponse { HasMore = false, IsTruncated = false, PermanentStorageHwmDate = DateTimeOffset.UtcNow, Invoices = [] };
        var invoiceDownloadClient = new FakeInvoiceDownloadClient((_, _, _, _) => response);
        var requestedEnvironments = new List<Environment>();
        var sut = new KsefClientAdapter(
            _ => AlwaysAuthenticated,
            _ => Task.FromResult<ICryptographyService>(new FakeCryptographyService()),
            env => { requestedEnvironments.Add(env); return invoiceDownloadClient; });
        var session = await sut.OpenSessionAsync(new SubjectCredentials("1234567890", "ksef-token-abc", "demo"), CancellationToken.None);

        await sut.QueryReceivedInvoicesAsync(session, DateTimeOffset.UtcNow.AddHours(-1), DateTimeOffset.UtcNow, 0, CancellationToken.None);

        Assert.Equal(Environment.Demo, Assert.Single(requestedEnvironments));
    }

    [Fact]
    public async Task OpenSessionAsync_UnrecognizedEnvironment_DefaultsToTest()
    {
        var requestedEnvironments = new List<Environment>();
        var sut = new KsefClientAdapter(
            env => { requestedEnvironments.Add(env); return AlwaysAuthenticated; },
            _ => Task.FromResult<ICryptographyService>(new FakeCryptographyService()),
            _ => new FakeInvoiceDownloadClient((_, _, _, _) => throw new NotSupportedException()));
        var credentials = new SubjectCredentials("1234567890", "ksef-token-abc", "not-a-real-environment");

        await sut.OpenSessionAsync(credentials, CancellationToken.None);

        Assert.Equal(Environment.Test, Assert.Single(requestedEnvironments));
    }

    [Theory]
    [InlineData(HttpStatusCode.Unauthorized)]
    [InlineData(HttpStatusCode.Forbidden)]
    public async Task OpenSessionAsync_AuthRejected_ThrowsKsefAuthFailedException(HttpStatusCode statusCode)
    {
        var authCoordinator = new FakeAuthCoordinator((_, _, _, _) =>
            throw new KsefApiException("Token rejected", statusCode));
        var sut = NewSut(authCoordinator, new FakeCryptographyService(), new FakeInvoiceDownloadClient((_, _, _, _) => throw new NotSupportedException()));

        await Assert.ThrowsAsync<KsefAuthFailedException>(() =>
            sut.OpenSessionAsync(AnyCredentials, CancellationToken.None));
    }

    [Theory]
    [InlineData(HttpStatusCode.Unauthorized)]
    [InlineData(HttpStatusCode.Forbidden)]
    public async Task QueryReceivedInvoicesAsync_AuthRejected_ThrowsKsefAuthFailedException(HttpStatusCode statusCode)
    {
        var invoiceDownloadClient = new FakeInvoiceDownloadClient((_, _, _, _) =>
            throw new KsefApiException("Token rejected", statusCode));
        var sut = NewSut(AlwaysAuthenticated, new FakeCryptographyService(), invoiceDownloadClient);
        var session = await sut.OpenSessionAsync(AnyCredentials, CancellationToken.None);

        await Assert.ThrowsAsync<KsefAuthFailedException>(() =>
            sut.QueryReceivedInvoicesAsync(session, DateTimeOffset.UtcNow.AddHours(-1), DateTimeOffset.UtcNow, 0, CancellationToken.None));
    }

    [Fact]
    public async Task CloseSessionAsync_CompletesWithoutError()
    {
        var sut = NewSut(AlwaysAuthenticated, new FakeCryptographyService(), new FakeInvoiceDownloadClient((_, _, _, _) => throw new NotSupportedException()));
        var session = await sut.OpenSessionAsync(AnyCredentials, CancellationToken.None);

        var exception = await Record.ExceptionAsync(() => sut.CloseSessionAsync(session, CancellationToken.None));

        Assert.Null(exception);
    }
}
