using SharpTimer.Core.SmartCubes;

namespace SharpTimer.Tests.SmartCubes;

public sealed class ThreeByThreeFaceletsTests
{
    [Theory]
    [InlineData("U")]
    [InlineData("R")]
    [InlineData("F")]
    [InlineData("D")]
    [InlineData("L")]
    [InlineData("B")]
    public void ApplyMove_FourQuarterTurns_ReturnsSolved(string move)
    {
        var state = ThreeByThreeFacelets.Solved;

        for (var i = 0; i < 4; i++)
        {
            state = ThreeByThreeFacelets.ApplyMove(state, move);
        }

        Assert.Equal(ThreeByThreeFacelets.Solved, state);
    }

    [Theory]
    [InlineData("U", "UUUUUUUUUBBBRRRRRRRRRFFFFFFDDDDDDDDDFFFLLLLLLLLLBBBBBB")]
    [InlineData("R", "UUFUUFUUFRRRRRRRRRFFDFFDFFDDDBDDBDDBLLLLLLLLLUBBUBBUBB")]
    [InlineData("F", "UUUUUULLLURRURRURRFFFFFFFFFRRRDDDDDDLLDLLDLLDBBBBBBBBB")]
    [InlineData("D", "UUUUUUUUURRRRRRFFFFFFFFFLLLDDDDDDDDDLLLLLLBBBBBBBBBRRR")]
    [InlineData("L", "BUUBUUBUURRRRRRRRRUFFUFFUFFFDDFDDFDDLLLLLLLLLBBDBBDBBD")]
    [InlineData("B", "RRRUUUUUURRDRRDRRDFFFFFFFFFDDDDDDLLLULLULLULLBBBBBBBBB")]
    public void ApplyMove_MatchesDctimerMin2PhaseFacelets(string move, string expected)
    {
        Assert.Equal(expected, ThreeByThreeFacelets.ApplyMove(ThreeByThreeFacelets.Solved, move));
    }

    [Fact]
    public void GetOrientationVariants_ReturnsAllWholeCubeRotations()
    {
        var variants = ThreeByThreeFacelets.GetOrientationVariants(ThreeByThreeFacelets.Solved);

        Assert.Equal(24, variants.Count);
        Assert.All(variants, variant => Assert.True(ThreeByThreeFacelets.IsSolvedIgnoringRotation(variant)));
    }

    [Fact]
    public void ApplyMove_Inverse_ReturnsSolved()
    {
        var state = ThreeByThreeFacelets.ApplyMove(ThreeByThreeFacelets.Solved, "R");
        state = ThreeByThreeFacelets.ApplyMove(state, "R'");

        Assert.Equal(ThreeByThreeFacelets.Solved, state);
    }

    [Fact]
    public void ApplyScramble_MatchesSequentialMoves()
    {
        var sequential = ThreeByThreeFacelets.ApplyMove(ThreeByThreeFacelets.Solved, "R");
        sequential = ThreeByThreeFacelets.ApplyMove(sequential, "U");
        sequential = ThreeByThreeFacelets.ApplyMove(sequential, "R'");

        Assert.Equal(sequential, ThreeByThreeFacelets.ApplyScramble("R U R'"));
    }
}
