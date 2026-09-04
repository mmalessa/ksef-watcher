namespace KsefWatcher.InvoiceWatching.ValueObjects;

/// <summary>
/// The window handed to <c>IInvoiceListProvider</c> (docs/08_invoice_watching_value_objects.md).
/// <c>From</c> is the exclusive lower bound of the previous HWM.
/// </summary>
public sealed record FetchWindow
{
    public DateTimeOffset From { get; }
    public DateTimeOffset To { get; }

    public FetchWindow(DateTimeOffset from, DateTimeOffset to)
    {
        if (from.Offset != TimeSpan.Zero || to.Offset != TimeSpan.Zero)
        {
            throw new ArgumentException("FetchWindow bounds must be UTC.");
        }

        if (from >= to)
        {
            throw new ArgumentException("From must be earlier than To.");
        }

        // A window may legitimately span > 100 days (e.g. catch-up after a long downtime) — the
        // KSeF API's 100-day-per-request limit is the provider's concern to split around
        // (KsefAccessService), not this VO's to reject (docs/08_invoice_watching_value_objects.md).
        From = from;
        To = to;
    }
}
