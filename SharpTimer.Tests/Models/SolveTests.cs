using SharpTimer.Core.Models;

namespace SharpTimer.Tests.Models;

public sealed class SolveTests
{
    [Fact]
    public void EffectiveDuration_AddsTwoSeconds_WhenPenaltyIsPlusTwo()
    {
        var solve = new Solve
        {
            Duration = TimeSpan.FromSeconds(10),
            Penalty = Penalty.PlusTwo
        };

        Assert.Equal(TimeSpan.FromSeconds(12), solve.EffectiveDuration);
    }

    [Fact]
    public void EffectiveDuration_ReturnsNull_WhenPenaltyIsDnf()
    {
        var solve = new Solve
        {
            Duration = TimeSpan.FromSeconds(10),
            Penalty = Penalty.Dnf
        };

        Assert.Null(solve.EffectiveDuration);
    }
}
