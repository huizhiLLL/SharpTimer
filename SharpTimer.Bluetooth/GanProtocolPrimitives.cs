namespace SharpTimer.Bluetooth;

internal sealed class GanBitReader
{
    private readonly string _bits;

    public GanBitReader(IReadOnlyList<byte> message)
    {
        _bits = string.Concat(message.Select(item => Convert.ToString(item, 2).PadLeft(8, '0')));
    }

    public int Get(int startBit, int bitLength, bool littleEndian = false)
    {
        if (bitLength <= 8)
        {
            return Convert.ToInt32(_bits.Substring(startBit, bitLength), 2);
        }

        if (bitLength is not (16 or 32))
        {
            throw new ArgumentOutOfRangeException(nameof(bitLength), bitLength, "Unsupported bit word length.");
        }

        var bytes = new byte[bitLength / 8];
        for (var index = 0; index < bytes.Length; index++)
        {
            bytes[index] = Convert.ToByte(_bits.Substring(startBit + index * 8, 8), 2);
        }

        if (littleEndian)
        {
            Array.Reverse(bytes);
        }

        return bitLength == 16
            ? bytes[0] << 8 | bytes[1]
            : bytes[0] << 24 | bytes[1] << 16 | bytes[2] << 8 | bytes[3];
    }
}

internal static class GanFaceletConverter
{
    private static readonly int[][] CornerFacelets =
    {
        new[] { 8, 9, 20 },
        new[] { 6, 18, 38 },
        new[] { 0, 36, 47 },
        new[] { 2, 45, 11 },
        new[] { 29, 26, 15 },
        new[] { 27, 44, 24 },
        new[] { 33, 53, 42 },
        new[] { 35, 17, 51 }
    };

    private static readonly int[][] EdgeFacelets =
    {
        new[] { 5, 10 },
        new[] { 7, 19 },
        new[] { 3, 37 },
        new[] { 1, 46 },
        new[] { 32, 16 },
        new[] { 28, 25 },
        new[] { 30, 43 },
        new[] { 34, 52 },
        new[] { 23, 12 },
        new[] { 21, 41 },
        new[] { 50, 39 },
        new[] { 48, 14 }
    };

    public static string ToFacelets(IReadOnlyList<int> cp, IReadOnlyList<int> co, IReadOnlyList<int> ep, IReadOnlyList<int> eo)
    {
        var perm = Enumerable.Range(0, 54).ToArray();
        for (var corner = 0; corner < 8; corner++)
        {
            var cubie = cp[corner];
            var orientation = co[corner];
            for (var n = 0; n < 3; n++)
            {
                perm[CornerFacelets[corner][(n + orientation) % 3]] = CornerFacelets[cubie][n];
            }
        }

        for (var edge = 0; edge < 12; edge++)
        {
            var cubie = ep[edge];
            var orientation = eo[edge];
            for (var n = 0; n < 2; n++)
            {
                perm[EdgeFacelets[edge][(n + orientation) % 2]] = EdgeFacelets[cubie][n];
            }
        }

        const string faces = "URFDLB";
        return new string(perm.Select(index => faces[index / 9]).ToArray());
    }
}
