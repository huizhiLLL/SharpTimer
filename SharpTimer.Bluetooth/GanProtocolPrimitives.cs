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

internal static class GanPacketValidator
{
    public static bool IsValidGen2Packet(IReadOnlyList<byte> packet)
    {
        if (packet.Count < 16)
        {
            return false;
        }

        try
        {
            var reader = new GanBitReader(packet);
            var type = reader.Get(0, 4);
            if (type is not (1 or 2 or 4 or 5 or 9 or 13))
            {
                return false;
            }

            if (type == 1)
            {
                return reader.Get(4, 16) != 0
                    || reader.Get(20, 16) != 0
                    || reader.Get(36, 16) != 0
                    || reader.Get(52, 16) != 0;
            }

            if (type == 2)
            {
                return Enumerable.Range(0, 7).All(index => reader.Get(12 + 5 * index, 4) <= 5);
            }

            if (type == 4)
            {
                var cornerSum = Enumerable.Range(0, 7).Sum(index => reader.Get(12 + 3 * index, 3));
                var edgeSum = Enumerable.Range(0, 11).Sum(index => reader.Get(47 + 4 * index, 4));
                return cornerSum <= 28 && edgeSum <= 66;
            }

            if (type == 9)
            {
                return reader.Get(8, 8) <= 100;
            }

            if (type == 5)
            {
                return Enumerable.Range(0, 8)
                    .Select(index => reader.Get(40 + 8 * index, 8))
                    .All(value => value == 0 || value is >= 32 and <= 126);
            }

            return true;
        }
        catch
        {
            return false;
        }
    }

    public static bool IsValidGen3Packet(IReadOnlyList<byte> packet)
    {
        if (packet.Count < 16)
        {
            return false;
        }

        try
        {
            var reader = new GanBitReader(packet);
            var header = reader.Get(0, 8);
            var type = reader.Get(8, 8);
            var length = reader.Get(16, 8);
            if (header != 0x55 || length == 0 || type is not (1 or 2 or 6 or 7 or 16 or 17))
            {
                return false;
            }

            return type != 1 || Array.IndexOf(new[] { 2, 32, 8, 1, 16, 4 }, reader.Get(74, 6)) >= 0;
        }
        catch
        {
            return false;
        }
    }

    public static bool IsValidGen4Packet(IReadOnlyList<byte> packet)
    {
        if (packet.Count < 16)
        {
            return false;
        }

        try
        {
            var reader = new GanBitReader(packet);
            var type = reader.Get(0, 8);
            if (type is not (1 or 209 or 237 or 236 or 239 or 234 or 250 or 251 or 252 or 253 or 254))
            {
                return false;
            }

            return type != 1 || Array.IndexOf(new[] { 2, 32, 8, 1, 16, 4 }, reader.Get(66, 6)) >= 0;
        }
        catch
        {
            return false;
        }
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
