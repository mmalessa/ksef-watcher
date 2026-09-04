namespace KsefWatcher.InvoiceWatching.ValueObjects;

/// <summary>
/// The whole contract back to Invoice Watching (I-10, docs/08_notification_delivery_tactical_model.md):
/// classify truthfully, never optimistically (I-9).
/// </summary>
public abstract record DeliveryResult
{
    public sealed record Confirmed : DeliveryResult;

    public sealed record Failed(FailureKind Kind) : DeliveryResult;

    public enum FailureKind
    {
        Retryable,
        Permanent,
    }
}
