using System.Security.Cryptography;

namespace SharpTimer.Core.Scrambles;

public sealed class ThreeByThreeScrambleGenerator
{
    public const int DefaultLength = 25;

    private static readonly ScrambleMove[] Moves =
    [
        new("U", 0),
        new("D", 0),
        new("R", 1),
        new("L", 1),
        new("F", 2),
        new("B", 2)
    ];

    private static readonly string[] Suffixes = ["", "2", "'"];

    public string Generate(int length = DefaultLength)
    {
        return Generate(length, RandomNumberGenerator.GetInt32);
    }

    public string Generate(int length, Random random)
    {
        ArgumentNullException.ThrowIfNull(random);

        return Generate(length, random.Next);
    }

    private static string Generate(int length, Func<int, int> next)
    {
        if (length <= 0)
        {
            throw new ArgumentOutOfRangeException(nameof(length), length, "Scramble length must be positive.");
        }

        var tokens = new string[length];
        var previousAxis = -1;

        for (var index = 0; index < tokens.Length; index++)
        {
            ScrambleMove move;
            do
            {
                move = Moves[next(Moves.Length)];
            }
            while (move.Axis == previousAxis);

            previousAxis = move.Axis;
            tokens[index] = move.Face + Suffixes[next(Suffixes.Length)];
        }

        return string.Join(' ', tokens);
    }

    private readonly record struct ScrambleMove(string Face, int Axis);
}
