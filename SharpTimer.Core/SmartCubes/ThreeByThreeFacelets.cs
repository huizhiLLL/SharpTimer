using System.Collections.ObjectModel;

namespace SharpTimer.Core.SmartCubes;

public static class ThreeByThreeFacelets
{
    public const string Solved = "UUUUUUUUURRRRRRRRRFFFFFFFFFDDDDDDDDDLLLLLLLLLBBBBBBBBB";

    private static readonly IReadOnlyList<Sticker> IndexToSticker = CreateIndexToSticker();

    private static readonly IReadOnlyDictionary<Sticker, int> StickerToIndex =
        new ReadOnlyDictionary<Sticker, int>(IndexToSticker
            .Select((sticker, index) => new { sticker, index })
            .ToDictionary(item => item.sticker, item => item.index));

    public static bool IsValidState(string facelets)
    {
        return !string.IsNullOrWhiteSpace(facelets)
            && facelets.Length == 54
            && facelets.All(facelet => "URFDLB".Contains(facelet));
    }

    public static bool IsSolved(string facelets)
    {
        if (!IsValidState(facelets))
        {
            return false;
        }

        for (var face = 0; face < 6; face++)
        {
            var start = face * 9;
            var center = facelets[start + 4];
            for (var index = 0; index < 9; index++)
            {
                if (facelets[start + index] != center)
                {
                    return false;
                }
            }
        }

        return true;
    }

    public static bool IsSolvedIgnoringRotation(string facelets)
    {
        return IsSameStateIgnoringRotation(facelets, Solved);
    }

    public static bool IsSameStateIgnoringRotation(string state, string target)
    {
        if (!IsValidState(state) || !IsValidState(target))
        {
            return false;
        }

        if (state == target)
        {
            return true;
        }

        return GetOrientationVariants(target).Contains(state);
    }

    public static IReadOnlyList<string> GetOrientationVariants(string facelets)
    {
        if (!IsValidState(facelets))
        {
            return Array.Empty<string>();
        }

        var seen = new HashSet<string>(StringComparer.Ordinal);
        var variants = new List<string>();
        var queue = new Queue<string>();
        queue.Enqueue(facelets);

        while (queue.Count > 0)
        {
            var current = queue.Dequeue();
            if (!seen.Add(current))
            {
                continue;
            }

            variants.Add(current);
            queue.Enqueue(RotateWholeCube(current, 0));
            queue.Enqueue(RotateWholeCube(current, 1));
            queue.Enqueue(RotateWholeCube(current, 2));
        }

        return variants;
    }

    public static string ApplyScramble(string scramble)
    {
        var state = Solved;
        foreach (var move in SmartCubeMoveNotation.ParseSequence(scramble))
        {
            state = ApplyMove(state, move);
        }

        return state;
    }

    public static string ApplyMove(string facelets, string move)
    {
        if (!IsValidState(facelets))
        {
            throw new ArgumentException("Facelets must be a 54-character URFDLB state.", nameof(facelets));
        }

        var normalizedMove = SmartCubeMoveNotation.Normalize(move);
        var face = normalizedMove[0];
        var turns = normalizedMove.EndsWith("2", StringComparison.Ordinal)
            ? 2
            : normalizedMove.EndsWith("'", StringComparison.Ordinal)
                ? 3
                : 1;

        var next = facelets;
        for (var i = 0; i < turns; i++)
        {
            next = ApplyQuarterTurn(next, face);
        }

        return next;
    }

    private static string ApplyQuarterTurn(string facelets, char face)
    {
        var next = facelets.ToCharArray();
        for (var index = 0; index < IndexToSticker.Count; index++)
        {
            var sticker = IndexToSticker[index];
            if (!IsInLayer(sticker.Position, face))
            {
                continue;
            }

            var rotated = Rotate(sticker, face);
            next[StickerToIndex[rotated]] = facelets[index];
        }

        return new string(next);
    }

    private static string RotateWholeCube(string facelets, int axis)
    {
        var next = facelets.ToCharArray();
        for (var index = 0; index < IndexToSticker.Count; index++)
        {
            var sticker = IndexToSticker[index];
            var rotated = axis switch
            {
                0 => new Sticker(RotateX(sticker.Position, clockwise: true), RotateX(sticker.Normal, clockwise: true)),
                1 => new Sticker(RotateY(sticker.Position, clockwise: true), RotateY(sticker.Normal, clockwise: true)),
                _ => new Sticker(RotateZ(sticker.Position, clockwise: false), RotateZ(sticker.Normal, clockwise: false))
            };

            next[StickerToIndex[rotated]] = facelets[index];
        }

        return new string(next);
    }

    private static bool IsInLayer(Vector3 position, char face)
    {
        return face switch
        {
            'U' => position.Y == 1,
            'R' => position.X == 1,
            'F' => position.Z == 1,
            'D' => position.Y == -1,
            'L' => position.X == -1,
            'B' => position.Z == -1,
            _ => false
        };
    }

    private static Sticker Rotate(Sticker sticker, char face)
    {
        return face switch
        {
            'U' => new Sticker(RotateY(sticker.Position, clockwise: false), RotateY(sticker.Normal, clockwise: false)),
            'D' => new Sticker(RotateY(sticker.Position, clockwise: true), RotateY(sticker.Normal, clockwise: true)),
            'R' => new Sticker(RotateX(sticker.Position, clockwise: false), RotateX(sticker.Normal, clockwise: false)),
            'L' => new Sticker(RotateX(sticker.Position, clockwise: true), RotateX(sticker.Normal, clockwise: true)),
            'F' => new Sticker(RotateZ(sticker.Position, clockwise: true), RotateZ(sticker.Normal, clockwise: true)),
            'B' => new Sticker(RotateZ(sticker.Position, clockwise: false), RotateZ(sticker.Normal, clockwise: false)),
            _ => sticker
        };
    }

    private static Vector3 RotateX(Vector3 vector, bool clockwise)
    {
        return clockwise
            ? new Vector3(vector.X, -vector.Z, vector.Y)
            : new Vector3(vector.X, vector.Z, -vector.Y);
    }

    private static Vector3 RotateY(Vector3 vector, bool clockwise)
    {
        return clockwise
            ? new Vector3(vector.Z, vector.Y, -vector.X)
            : new Vector3(-vector.Z, vector.Y, vector.X);
    }

    private static Vector3 RotateZ(Vector3 vector, bool clockwise)
    {
        return clockwise
            ? new Vector3(vector.Y, -vector.X, vector.Z)
            : new Vector3(-vector.Y, vector.X, vector.Z);
    }

    private static IReadOnlyList<Sticker> CreateIndexToSticker()
    {
        var stickers = new List<Sticker>(54);

        AddFace(stickers, new Vector3(0, 1, 0), row => new Vector3(-1 + row.Column, 1, -1 + row.Row));
        AddFace(stickers, new Vector3(1, 0, 0), row => new Vector3(1, 1 - row.Row, 1 - row.Column));
        AddFace(stickers, new Vector3(0, 0, 1), row => new Vector3(-1 + row.Column, 1 - row.Row, 1));
        AddFace(stickers, new Vector3(0, -1, 0), row => new Vector3(-1 + row.Column, -1, 1 - row.Row));
        AddFace(stickers, new Vector3(-1, 0, 0), row => new Vector3(-1, 1 - row.Row, -1 + row.Column));
        AddFace(stickers, new Vector3(0, 0, -1), row => new Vector3(1 - row.Column, 1 - row.Row, -1));

        return stickers;
    }

    private static void AddFace(List<Sticker> stickers, Vector3 normal, Func<(int Row, int Column), Vector3> positionFactory)
    {
        for (var row = 0; row < 3; row++)
        {
            for (var column = 0; column < 3; column++)
            {
                stickers.Add(new Sticker(positionFactory((row, column)), normal));
            }
        }
    }

    private readonly record struct Sticker(Vector3 Position, Vector3 Normal);

    private readonly record struct Vector3(int X, int Y, int Z);
}
