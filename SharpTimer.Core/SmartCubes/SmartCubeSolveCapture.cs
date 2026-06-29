namespace SharpTimer.Core.SmartCubes;

public sealed class SmartCubeSolveCapture
{
    private static readonly TimeSpan SyntheticMoveGap = TimeSpan.FromMilliseconds(1);

    private readonly List<SmartCubeRecordedMove> _moves = new();
    private int _solveStartIndex;

    public void RecordMove(string move, DateTimeOffset timestamp, TimeSpan? cubeTimestamp = null)
    {
        _moves.Add(new SmartCubeRecordedMove(
            SmartCubeMoveNotation.Normalize(move),
            timestamp,
            cubeTimestamp));
    }

    public void MarkReadyToStart()
    {
        _solveStartIndex = _moves.Count;
    }

    public void MarkSolveStartedIncludingLastMove()
    {
        _solveStartIndex = Math.Max(0, _moves.Count - 1);
    }

    public SmartCubeSolveCaptureSnapshot Snapshot()
    {
        if (_solveStartIndex >= _moves.Count)
        {
            return SmartCubeSolveCaptureSnapshot.Empty;
        }

        var solveMoves = _moves.Skip(_solveStartIndex).ToArray();
        var previous = solveMoves[0];
        var samples = new List<SmartCubeSolveMoveSample>(solveMoves.Length);
        var localElapsed = TimeSpan.Zero;
        var deviceElapsed = TimeSpan.Zero;

        for (var index = 0; index < solveMoves.Length; index++)
        {
            var current = solveMoves[index];
            var delta = index == 0
                ? TimeSpan.Zero
                : ResolveDelta(previous, current);
            var localDelta = index == 0
                ? TimeSpan.Zero
                : ResolveLocalDelta(previous, current);
            var deviceDelta = index == 0
                ? TimeSpan.Zero
                : ResolveDeviceDelta(previous, current);
            localElapsed += localDelta;
            if (deviceDelta > TimeSpan.Zero)
            {
                deviceElapsed += deviceDelta;
            }

            var elapsed = deviceDelta > TimeSpan.Zero
                ? deviceElapsed
                : Max(deviceElapsed, localElapsed);

            samples.Add(new SmartCubeSolveMoveSample(
                current.Move,
                delta,
                elapsed,
                current.Timestamp,
                current.CubeTimestamp));
            previous = current;
        }

        return new SmartCubeSolveCaptureSnapshot(samples);
    }

    public void Clear()
    {
        _moves.Clear();
        _solveStartIndex = 0;
    }

    private static TimeSpan? ResolveDelta(SmartCubeRecordedMove previous, SmartCubeRecordedMove current)
    {
        var deviceDelta = ResolveDeviceDelta(previous, current);
        return deviceDelta > TimeSpan.Zero
            ? deviceDelta
            : ResolveLocalDelta(previous, current);
    }

    private static TimeSpan ResolveDeviceDelta(SmartCubeRecordedMove previous, SmartCubeRecordedMove current)
    {
        if (previous.CubeTimestamp is not null && current.CubeTimestamp is not null)
        {
            return ClampNonNegative(current.CubeTimestamp.Value - previous.CubeTimestamp.Value);
        }

        return TimeSpan.Zero;
    }

    private static TimeSpan ResolveLocalDelta(SmartCubeRecordedMove previous, SmartCubeRecordedMove current)
    {
        var delta = ClampNonNegative(current.Timestamp - previous.Timestamp);
        return delta > TimeSpan.Zero ? delta : SyntheticMoveGap;
    }

    private static TimeSpan ClampNonNegative(TimeSpan value)
    {
        return value < TimeSpan.Zero ? TimeSpan.Zero : value;
    }

    private static TimeSpan Max(TimeSpan left, TimeSpan right)
    {
        return left >= right ? left : right;
    }
}

public sealed record SmartCubeSolveCaptureSnapshot(IReadOnlyList<SmartCubeSolveMoveSample> Moves)
{
    public static SmartCubeSolveCaptureSnapshot Empty { get; } = new(Array.Empty<SmartCubeSolveMoveSample>());

    public IReadOnlyList<string> MoveSequence => Moves.Select(move => move.Move).ToArray();

    public IReadOnlyList<string> CombinedMoveSequence
    {
        get
        {
            var combined = new List<string>();
            foreach (var move in Moves)
            {
                SmartCubeMoveNotation.AppendCombined(combined, move.Move);
            }

            return combined;
        }
    }
}

public sealed record SmartCubeSolveMoveSample(
    string Move,
    TimeSpan? Delta,
    TimeSpan Elapsed,
    DateTimeOffset Timestamp,
    TimeSpan? CubeTimestamp);

internal sealed record SmartCubeRecordedMove(
    string Move,
    DateTimeOffset Timestamp,
    TimeSpan? CubeTimestamp);
