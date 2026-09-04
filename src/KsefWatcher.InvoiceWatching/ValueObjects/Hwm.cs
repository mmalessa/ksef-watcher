namespace KsefWatcher.InvoiceWatching.ValueObjects;

/// <summary>
/// The HWM cursor (docs/08_invoice_watching_value_objects.md) — translated <c>PermanentStorageHwmDate</c>.
/// Monotonic within a subject: only <c>SubjectWatch.AdvanceHwm</c> moves it, and only forward.
/// </summary>
public sealed record Hwm(DateTimeOffset Utc)
{
    public DateTimeOffset Utc { get; } = Utc.Offset == TimeSpan.Zero
        ? Utc
        : throw new ArgumentException("Hwm must be UTC.", nameof(Utc));
}
