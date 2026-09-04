using KsefWatcher.InvoiceWatching.ValueObjects;
using Xunit;

namespace KsefWatcher.InvoiceWatching.Tests;

public class SubjectIdTests
{
    [Fact]
    public void Constructor_StoresNip()
    {
        var subjectId = new SubjectId("5260001246");

        Assert.Equal("5260001246", subjectId.Nip);
    }

    [Theory]
    [InlineData("")]
    [InlineData("   ")]
    public void Constructor_RejectsEmptyNip(string invalidNip)
    {
        Assert.Throws<ArgumentException>(() => new SubjectId(invalidNip));
    }

    [Fact]
    public void Constructor_RejectsNipWithInvalidChecksum()
    {
        // 1234567890: well-formed (10 digits) but the checksum digit doesn't match (I-13).
        Assert.Throws<ArgumentException>(() => new SubjectId("1234567890"));
    }

    [Theory]
    [InlineData("111111111")] // 9 digits — one short; would-be checksum (mod 11 = 1) doesn't hit the invalid-modulus shortcut
    [InlineData("52600012460")] // 5260001246 (valid) plus a spurious trailing digit
    public void Constructor_RejectsNipWithWrongLength(string wrongLengthNip)
    {
        Assert.Throws<ArgumentException>(() => new SubjectId(wrongLengthNip));
    }

    [Fact]
    public void Constructor_RejectsNipWithNonDigitCharacters()
    {
        Assert.Throws<ArgumentException>(() => new SubjectId("526000124A"));
    }
}
