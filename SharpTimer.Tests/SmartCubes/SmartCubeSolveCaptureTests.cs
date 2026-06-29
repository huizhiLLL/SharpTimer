using SharpTimer.Core.SmartCubes;

namespace SharpTimer.Tests.SmartCubes;

public sealed class SmartCubeSolveCaptureTests
{
    [Fact]
    public void Snapshot_OnlyIncludesMovesAfterReadyMarker()
    {
        var capture = new SmartCubeSolveCapture();
        var now = DateTimeOffset.Parse("2026-05-01T00:00:00Z");

        capture.RecordMove("R", now, TimeSpan.FromMilliseconds(100));
        capture.RecordMove("U", now.AddMilliseconds(50), TimeSpan.FromMilliseconds(150));
        capture.MarkReadyToStart();
        capture.RecordMove("R", now.AddMilliseconds(100), TimeSpan.FromMilliseconds(200));
        capture.RecordMove("U'", now.AddMilliseconds(250), TimeSpan.FromMilliseconds(350));

        var snapshot = capture.Snapshot();

        Assert.Equal(new[] { "R", "U'" }, snapshot.MoveSequence);
        Assert.Equal(TimeSpan.Zero, snapshot.Moves[0].Delta);
        Assert.Equal(TimeSpan.FromMilliseconds(150), snapshot.Moves[1].Delta);
        Assert.Equal(TimeSpan.FromMilliseconds(150), snapshot.Moves[1].Elapsed);
    }

    [Fact]
    public void Snapshot_CanStartAtLastRecordedMove_WhenFirstTurnStartsTimer()
    {
        var capture = new SmartCubeSolveCapture();
        var now = DateTimeOffset.Parse("2026-05-01T00:00:00Z");

        capture.RecordMove("R", now, TimeSpan.FromMilliseconds(100));
        capture.MarkSolveStartedIncludingLastMove();
        capture.RecordMove("R", now.AddMilliseconds(80), TimeSpan.FromMilliseconds(180));

        var snapshot = capture.Snapshot();

        Assert.Equal(new[] { "R", "R" }, snapshot.MoveSequence);
        Assert.Equal(new[] { "R2" }, snapshot.CombinedMoveSequence);
    }
}
