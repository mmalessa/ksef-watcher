namespace KsefWatcher.KsefAccess;

/// <summary>Thrown by <see cref="IKsefQueryClient"/> on HTTP 429 (A7 verified rate limits).</summary>
public sealed class KsefRateLimitedException(TimeSpan retryAfter) : Exception
{
    public TimeSpan RetryAfter { get; } = retryAfter;
}
