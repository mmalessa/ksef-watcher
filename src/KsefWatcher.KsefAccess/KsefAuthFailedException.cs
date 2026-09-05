namespace KsefWatcher.KsefAccess;

/// <summary>Thrown by <see cref="IKsefQueryClient"/> on HTTP 401/403 — a revoked, expired or
/// mistyped KSeF token (OQ-18: permanent, never self-heals).</summary>
public sealed class KsefAuthFailedException(string reason) : Exception(reason);
