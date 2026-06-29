namespace SharpTimer.Bluetooth;

internal static class QiYiMoveHistory
{
    public const int SlotCount = 11;
    public const int SlotStart = 36;
    public const int SlotSize = 5;

    public static IReadOnlyList<QiYiMoveSample> Collect(
        IReadOnlyList<byte> message,
        long lastTimestamp,
        long frameTimestamp)
    {
        if (message.Count < 35)
        {
            return Array.Empty<QiYiMoveSample>();
        }

        var candidates = new List<QiYiMoveSample>
        {
            new(message[34], ReadUInt32BE(message, 3))
        };

        for (var index = 0; index < SlotCount; index++)
        {
            var offset = SlotStart + SlotSize * index;
            if (offset + SlotSize > message.Count)
            {
                break;
            }

            if (IsEmptyHistorySlot(message, offset))
            {
                continue;
            }

            candidates.Add(new QiYiMoveSample(message[offset + 4], ReadUInt32BE(message, offset)));
        }

        var seen = new HashSet<QiYiMoveSample>();
        return candidates
            .OrderBy(item => item.Timestamp)
            .Where(item => item.Timestamp > lastTimestamp
                && item.Timestamp <= frameTimestamp
                && ConvertMove(item.Code) >= 0)
            .Where(item => seen.Add(item))
            .ToArray();
    }

    public static int ConvertMove(byte rawMove)
    {
        if (rawMove == 0)
        {
            return -1;
        }

        var axisIndex = (rawMove - 1) >> 1;
        if (axisIndex < 0 || axisIndex >= 6)
        {
            return -1;
        }

        var axis = new[] { 4, 1, 3, 0, 2, 5 }[axisIndex];
        var power = (rawMove & 1) == 0 ? 0 : 2;
        return axis * 3 + power;
    }

    private static bool IsEmptyHistorySlot(IReadOnlyList<byte> message, int offset)
    {
        for (var index = 0; index < SlotSize; index++)
        {
            if (message[offset + index] != 0xFF)
            {
                return false;
            }
        }

        return true;
    }

    private static long ReadUInt32BE(IReadOnlyList<byte> data, int offset)
    {
        return (data[offset] & 0xFFL) << 24
            | (data[offset + 1] & 0xFFL) << 16
            | (data[offset + 2] & 0xFFL) << 8
            | (data[offset + 3] & 0xFFL);
    }
}

internal sealed record QiYiMoveSample(byte Code, long Timestamp);
