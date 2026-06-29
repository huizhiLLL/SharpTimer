namespace SharpTimer.Core.SmartCubes;

public sealed class SmartCubeSolveCapture
{
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
        var first = solveMoves[0];
        var previous = first;
        var samples = new List<SmartCubeSolveMoveSample>(solveMoves.Length);
        var localElapsed = TimeSpan.Zero;

        for (var index = 0; index < solveMoves.Length; index++)
        {
            var current = solveMoves[index];
            var delta = index == 0
                ? TimeSpan.Zero
                : ResolveDelta(previous, current);
            localElapsed += delta ?? TimeSpan.Zero;
            var elapsed = current.CubeTimestamp is not null && first.CubeTimestamp is not null
                ? ClampNonNegative(current.CubeTimestamp.Value - first.CubeTimestamp.Value)
                : localElapsed;

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
        if (previous.CubeTimestamp is not null && current.CubeTimestamp is not null)
        {
            return ClampNonNegative(current.CubeTimestamp.Value - previous.CubeTimestamp.Value);
        }

        return ClampNonNegative(current.Timestamp - previous.Timestamp);
    }

    private static TimeSpan ClampNonNegative(TimeSpan value)
    {
        return value < TimeSpan.Zero ? TimeSpan.Zero : value;
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
