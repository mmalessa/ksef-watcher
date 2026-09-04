using KSeF.Client.Core.Interfaces;
using KSeF.Client.Core.Interfaces.Services;
using KSeF.Client.Core.Models.Authorization;

namespace KsefWatcher.KsefAccess.Tests.TestDoubles;

public sealed class FakeAuthCoordinator(
    Func<AuthenticationTokenContextIdentifierType, string, string, EncryptionMethodEnum, AuthenticationOperationStatusResponse> authKsefToken)
    : IAuthCoordinator
{
    public List<(AuthenticationTokenContextIdentifierType ContextType, string ContextValue, string TokenKsef, ICryptographyService CryptographyService, EncryptionMethodEnum EncryptionMethod)> AuthKsefTokenCalls { get; } = [];

    public Task<AuthenticationOperationStatusResponse> AuthKsefTokenAsync(
        AuthenticationTokenContextIdentifierType contextIdentifierType,
        string contextIdentifierValue,
        string tokenKsef,
        ICryptographyService cryptographyService,
        EncryptionMethodEnum encryptionMethod = EncryptionMethodEnum.ECDsa,
        AuthenticationTokenAuthorizationPolicy? authorizationPolicy = default,
        CancellationToken cancellationToken = default)
    {
        AuthKsefTokenCalls.Add((contextIdentifierType, contextIdentifierValue, tokenKsef, cryptographyService, encryptionMethod));
        return Task.FromResult(authKsefToken(contextIdentifierType, contextIdentifierValue, tokenKsef, encryptionMethod));
    }

    public Task<AuthenticationOperationStatusResponse> AuthAsync(
        AuthenticationTokenContextIdentifierType contextIdentifierType,
        string contextIdentifierValue,
        AuthenticationTokenSubjectIdentifierTypeEnum identifierType,
        Func<string, Task<string>> xmlSigner,
        AuthenticationTokenAuthorizationPolicy? authorizationPolicy = default,
        bool verifyCertificateChain = false,
        CancellationToken cancellationToken = default) =>
        throw new NotSupportedException("Not used by KsefClientAdapter (A11: KSeF-token auth, not XAdES certificates).");

    public Task<TokenInfo> RefreshAccessTokenAsync(string refreshToken, CancellationToken cancellationToken = default) =>
        throw new NotSupportedException("Not used by KsefClientAdapter (A8: fresh session per poll, no token refresh).");
}
