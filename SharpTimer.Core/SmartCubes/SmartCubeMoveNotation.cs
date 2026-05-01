namespace SharpTimer.Core.SmartCubes;

public static class SmartCubeMoveNotation
{
    public static IReadOnlyList<string> ParseSequence(string? sequence)
    {
        if (string.IsNullOrWhiteSpace(sequence))
        {
            return Array.Empty<string>();
        }

        return sequence
            .ReplaceLineEndings(" ")
            .Split(' ', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
            .Select(Normalize)
            .ToArray();
    }

    public static string Normalize(string move)
    {
        var normalized = move.Trim();
        if (normalized.Length is < 1 or > 2 || !"URFDLB".Contains(normalized[0]))
        {
            throw new ArgumentException($"Unsupported 3x3 move: {move}", nameof(move));
        }

        if (normalized.Length == 2 && normalized[1] is not ('2' or '\''))
        {
            throw new ArgumentException($"Unsupported 3x3 move suffix: {move}", nameof(move));
        }

        return normalized;
    }

    public static string Invert(string move)
    {
        var normalized = Normalize(move);
        return normalized.Length == 1
            ? normalized + "'"
            : normalized[1] == '\''
                ? normalized[0].ToString()
                : normalized;
    }

    public static void AppendCombined(IList<string> moves, string move)
    {
        var normalized = Normalize(move);
        if (moves.Count == 0 || moves[^1][0] != normalized[0])
        {
            moves.Add(normalized);
            return;
        }

        var mergedPower = (GetPower(moves[^1]) + GetPower(normalized)) % 4;
        moves.RemoveAt(moves.Count - 1);
        if (mergedPower != 0)
        {
            moves.Add(normalized[0] + GetSuffix(mergedPower));
        }
    }

    private static int GetPower(string move)
    {
        return move.Length == 1
            ? 1
            : move[1] == '2'
                ? 2
                : 3;
    }

    private static string GetSuffix(int power)
    {
        return power switch
        {
            2 => "2",
            3 => "'",
            _ => string.Empty
        };
    }
}
