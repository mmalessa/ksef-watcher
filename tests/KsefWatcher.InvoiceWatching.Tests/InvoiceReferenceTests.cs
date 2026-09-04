using KsefWatcher.InvoiceWatching.ValueObjects;
using Xunit;

namespace KsefWatcher.InvoiceWatching.Tests;

public class InvoiceReferenceTests
{
    [Fact]
    public void Equality_IsCaseInsensitive()
    {
        var lower = new InvoiceReference("abc-123");
        var upper = new InvoiceReference("ABC-123");

        Assert.Equal(lower, upper);
        Assert.Equal(lower.GetHashCode(), upper.GetHashCode());
    }

    [Fact]
    public void Equality_DistinguishesDifferentNumbers()
    {
        var a = new InvoiceReference("abc-123");
        var b = new InvoiceReference("abc-124");

        Assert.NotEqual(a, b);
    }

    [Fact]
    public void Constructor_RejectsEmptyKsefNumber()
    {
        Assert.Throws<ArgumentException>(() => new InvoiceReference(""));
    }
}
