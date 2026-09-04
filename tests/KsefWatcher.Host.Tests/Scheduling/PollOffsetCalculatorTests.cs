using KsefWatcher.Host.Scheduling;
using KsefWatcher.InvoiceWatching.ValueObjects;
using Xunit;

namespace KsefWatcher.Host.Tests.Scheduling;

public class PollOffsetCalculatorTests
{
    [Theory]
    [InlineData("5260001246", 60)]
    [InlineData("9999999999", 15)]
    [InlineData("1111111111", 1440)]
    public void ComputeOffset_IsWithinZeroToInterval(string nip, int intervalMinutes)
    {
        var interval = TimeSpan.FromMinutes(intervalMinutes);

        var offset = PollOffsetCalculator.ComputeOffset(new SubjectId(nip), interval);

        Assert.True(offset >= TimeSpan.Zero);
        Assert.True(offset < interval);
    }

    [Fact]
    public void ComputeOffset_IsDeterministic_SameInputsAlwaysGiveSameOffset()
    {
        var subjectId = new SubjectId("5260001246");
        var interval = TimeSpan.FromMinutes(60);

        var first = PollOffsetCalculator.ComputeOffset(subjectId, interval);
        var second = PollOffsetCalculator.ComputeOffset(subjectId, interval);

        Assert.Equal(first, second);
    }

    [Fact]
    public void ComputeOffset_DifferentSubjects_GenerallyGiveDifferentOffsets()
    {
        var interval = TimeSpan.FromMinutes(60);

        var offsetA = PollOffsetCalculator.ComputeOffset(new SubjectId("5260001246"), interval);
        var offsetB = PollOffsetCalculator.ComputeOffset(new SubjectId("9999999999"), interval);

        Assert.NotEqual(offsetA, offsetB);
    }
}
