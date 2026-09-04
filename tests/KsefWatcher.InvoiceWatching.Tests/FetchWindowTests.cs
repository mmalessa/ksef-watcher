using KsefWatcher.InvoiceWatching.ValueObjects;
using Xunit;

namespace KsefWatcher.InvoiceWatching.Tests;

public class FetchWindowTests
{
    [Fact]
    public void Constructor_RejectsFromNotEarlierThanTo()
    {
        var now = DateTimeOffset.UtcNow;

        Assert.Throws<ArgumentException>(() => new FetchWindow(now, now));
        Assert.Throws<ArgumentException>(() => new FetchWindow(now, now.AddDays(-1)));
    }

    [Fact]
    public void Constructor_RejectsNonUtcBounds()
    {
        var localFrom = new DateTimeOffset(2026, 1, 1, 0, 0, 0, TimeSpan.FromHours(2));
        var to = DateTimeOffset.UtcNow;

        Assert.Throws<ArgumentException>(() => new FetchWindow(localFrom, to));
    }

    [Fact]
    public void Constructor_AcceptsValidUtcRange()
    {
        var from = DateTimeOffset.UtcNow.AddDays(-1);
        var to = DateTimeOffset.UtcNow;

        var window = new FetchWindow(from, to);

        Assert.Equal(from, window.From);
        Assert.Equal(to, window.To);
    }
}
