namespace KsefWatcher.InvoiceWatching.Tests.TestDoubles;

public sealed class FixedTimeProvider(DateTimeOffset now) : TimeProvider
{
    public override DateTimeOffset GetUtcNow() => now;
}
