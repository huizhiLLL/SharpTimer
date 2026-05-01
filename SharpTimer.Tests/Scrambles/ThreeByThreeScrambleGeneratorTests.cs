using SharpTimer.Core.Scrambles;
using System.Text.RegularExpressions;

namespace SharpTimer.Tests.Scrambles;

public sealed class ThreeByThreeScrambleGeneratorTests
{
    private static readonly Regex MovePattern = new("^[UDRLFB](2|'|)?$", RegexOptions.Compiled);

    [Fact]
    public void Generate_ReturnsTwentyFiveWcaNotationMoves()
    {
        var generator = new ThreeByThreeScrambleGenerator();

        var scramble = generator.Generate(ThreeByThreeScrambleGenerator.DefaultLength, new Random(123));

        var moves = scramble.Split(' ');
        Assert.Equal(25, moves.Length);
        Assert.All(moves, move => Assert.Matches(MovePattern, move));
    }

    [Fact]
    public void Generate_DoesNotRepeatAxisConsecutively()
    {
        var generator = new ThreeByThreeScrambleGenerator();

        var scramble = generator.Generate(100, new Random(456));

        var axes = scramble.Split(' ').Select(GetAxis).ToArray();
        for (var index = 1; index < axes.Length; index++)
        {
            Assert.NotEqual(axes[index - 1], axes[index]);
        }
    }

    [Fact]
    public void Generate_ThrowsWhenLengthIsNotPositive()
    {
        var generator = new ThreeByThreeScrambleGenerator();

        Assert.Throws<ArgumentOutOfRangeException>(() => generator.Generate(0));
    }

    private static int GetAxis(string move)
    {
        return move[0] switch
        {
            'U' or 'D' => 0,
            'R' or 'L' => 1,
            'F' or 'B' => 2,
            _ => throw new ArgumentOutOfRangeException(nameof(move), move, "Unknown face.")
        };
    }
}
