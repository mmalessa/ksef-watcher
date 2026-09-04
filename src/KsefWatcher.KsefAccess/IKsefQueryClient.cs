namespace KsefWatcher.KsefAccess;

/// <summary>
/// The ACL seam: the only interface between <see cref="KsefAccessService"/> and the outside world.
/// A future <c>KsefClientAdapter</c> implements this by wrapping the official
/// <c>CIRFMF/ksef-client-csharp</c> package (docs/08_ksef_access_tactical_model.md) — that package
/// is not wired in yet (docs/09_integration_contracts.md), so this interface lets
/// <see cref="KsefAccessService"/>'s orchestration logic be built and tested now, independently.
/// </summary>
public interface IKsefQueryClient
{
    /// <summary>Fresh session per poll (A8) — auth: token + NIP (A11).</summary>
    Task<KsefSession> OpenSessionAsync(SubjectCredentials credentials, CancellationToken cancellationToken);

    /// <summary><c>SubjectType.Subject2</c> (received invoices), snapshot mode, one page at <paramref name="pageOffset"/>.</summary>
    Task<KsefQueryPage> QueryReceivedInvoicesAsync(KsefSession session, DateTimeOffset from, DateTimeOffset to, int pageOffset, CancellationToken cancellationToken);

    Task CloseSessionAsync(KsefSession session, CancellationToken cancellationToken);
}

public sealed record KsefSession(string Token, string Environment = "test");

/// <summary>One page of the raw KSeF response — pre-ACL-translation shape.</summary>
public sealed record KsefQueryPage(
    IReadOnlyList<KsefInvoiceSummary> Invoices,
    bool HasMore,
    bool IsTruncated,
    DateTimeOffset? PermanentStorageHwmDate);

/// <summary>Raw KSeF invoice summary — never crosses out of KsefAccess unchanged (ACL rule).</summary>
public sealed record KsefInvoiceSummary(
    string KsefNumber,
    string InvoiceNumber,
    decimal NetAmount,
    decimal GrossAmount,
    string Currency,
    string IssuerNip,
    string? IssuerName);
