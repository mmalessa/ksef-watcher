namespace KsefWatcher.KsefAccess;

/// <summary>
/// Classification surfaced to logs as SubjectPollFailed (I-8, docs/08_ksef_access_tactical_model.md).
/// AuthFailure is permanent (OQ-18): never self-heals; every poll re-classifies and logs loudly.
/// </summary>
public abstract record PollFailure
{
    public sealed record RateLimited(TimeSpan RetryAfter) : PollFailure;
    public sealed record AuthFailure : PollFailure;
    public sealed record ApiError(string Reason) : PollFailure;
    public sealed record Network(string Reason) : PollFailure;
}
