namespace KsefWatcher.KsefAccess;

/// <summary>
/// Carries a classified <see cref="PollFailure"/> (I-8) out of <see cref="KsefAccessService"/>.
/// Propagates through PollCycle unhandled (docs/09_architecture.md) — caught and logged as
/// SubjectPollFailed at the Host/BackgroundService boundary, not inside the Core.
/// </summary>
public sealed class PollFailureException(PollFailure reason) : Exception
{
    public PollFailure Reason { get; } = reason;
}
