using KsefWatcher.Host.Scheduling;
using Xunit;

namespace KsefWatcher.Host.Tests.Scheduling;

public class InFlightGateTests
{
    [Fact]
    public void TryEnter_FirstCallForKey_ReturnsTrue()
    {
        var gate = new InFlightGate();

        var entered = gate.TryEnter("subject-a");

        Assert.True(entered);
    }

    [Fact]
    public void TryEnter_WhileAlreadyEnteredForSameKey_ReturnsFalse()
    {
        var gate = new InFlightGate();
        gate.TryEnter("subject-a");

        var enteredAgain = gate.TryEnter("subject-a");

        Assert.False(enteredAgain);
    }

    [Fact]
    public void TryEnter_ForDifferentKey_ReturnsTrue_EvenWhileFirstKeyStillEntered()
    {
        var gate = new InFlightGate();
        gate.TryEnter("subject-a");

        var enteredOther = gate.TryEnter("subject-b");

        Assert.True(enteredOther);
    }

    [Fact]
    public void TryEnter_AfterExit_ReturnsTrueAgain()
    {
        var gate = new InFlightGate();
        gate.TryEnter("subject-a");
        gate.Exit("subject-a");

        var enteredAgain = gate.TryEnter("subject-a");

        Assert.True(enteredAgain);
    }
}
