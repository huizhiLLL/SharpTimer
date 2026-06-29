using SharpTimer.Bluetooth;

namespace SharpTimer.Tests.Bluetooth;

public sealed class QiYiMoveHistoryTests
{
    [Fact]
    public void Collect_splits_current_and_future_moves_by_frame_timestamp()
    {
        var message = CreateStateChangeMessage(frameTimestamp: 100, frameMove: 1);
        WriteHistorySlot(message, 0, timestamp: 80, code: 3);
        WriteHistorySlot(message, 1, timestamp: 140, code: 5);

        var currentMoves = QiYiMoveHistory.Collect(message, lastTimestamp: 60, frameTimestamp: 100);
        var futureMoves = QiYiMoveHistory.Collect(message, lastTimestamp: 100, frameTimestamp: long.MaxValue);

        Assert.Equal(new byte[] { 3, 1 }, currentMoves.Select(item => item.Code));
        Assert.Equal(new byte[] { 5 }, futureMoves.Select(item => item.Code));
    }

    [Fact]
    public void Collect_ignores_duplicate_and_invalid_moves()
    {
        var message = CreateStateChangeMessage(frameTimestamp: 100, frameMove: 0);
        WriteHistorySlot(message, 0, timestamp: 110, code: 3);
        WriteHistorySlot(message, 1, timestamp: 110, code: 3);
        WriteHistorySlot(message, 2, timestamp: 120, code: 99);

        var moves = QiYiMoveHistory.Collect(message, lastTimestamp: 100, frameTimestamp: long.MaxValue);

        var move = Assert.Single(moves);
        Assert.Equal(3, move.Code);
        Assert.Equal(110, move.Timestamp);
    }

    private static byte[] CreateStateChangeMessage(long frameTimestamp, byte frameMove)
    {
        var message = Enumerable.Repeat((byte)0xFF, 96).ToArray();
        WriteUInt32BE(message, 3, frameTimestamp);
        message[34] = frameMove;
        return message;
    }

    private static void WriteHistorySlot(byte[] message, int index, long timestamp, byte code)
    {
        var offset = QiYiMoveHistory.SlotStart + QiYiMoveHistory.SlotSize * index;
        WriteUInt32BE(message, offset, timestamp);
        message[offset + 4] = code;
    }

    private static void WriteUInt32BE(byte[] data, int offset, long value)
    {
        data[offset] = (byte)(value >> 24);
        data[offset + 1] = (byte)(value >> 16);
        data[offset + 2] = (byte)(value >> 8);
        data[offset + 3] = (byte)value;
    }
}
