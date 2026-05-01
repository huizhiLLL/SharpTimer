using SharpTimer.Core.SmartCubes;

namespace SharpTimer.Tests.SmartCubes;

public sealed class SmartCubeScrambleTrackerTests
{
    [Fact]
    public void ApplyMove_AdvancesScrambleProgressToReady()
    {
        var tracker = new SmartCubeScrambleTracker();
        tracker.SetScramble("R U");
        tracker.UpdateFacelets(ThreeByThreeFacelets.Solved);

        var first = tracker.ApplyMove("R");
        var ready = tracker.ApplyMove("U");

        Assert.Equal(SmartCubeScrambleStatus.Scrambling, first.Status);
        Assert.Equal(1, first.Progress);
        Assert.Equal(SmartCubeScrambleStatus.Ready, ready.Status);
        Assert.True(ready.IsReady);
    }

    [Fact]
    public void UpdateFacelets_TargetScrambleState_EntersReady()
    {
        var tracker = new SmartCubeScrambleTracker();
        tracker.SetScramble("R U");

        var targetState = ThreeByThreeFacelets.ApplyScramble("R U");
        var ready = tracker.UpdateFacelets(targetState);

        Assert.Equal(SmartCubeScrambleStatus.Ready, ready.Status);
        Assert.Empty(ready.RemainingMoves);
    }

    [Fact]
    public void UpdateFacelets_RotatedTargetScrambleState_EntersReady()
    {
        var tracker = new SmartCubeScrambleTracker();
        tracker.SetScramble("R U");

        var targetState = ThreeByThreeFacelets.ApplyScramble("R U");
        var rotatedTargetState = ThreeByThreeFacelets.GetOrientationVariants(targetState)[1];
        var ready = tracker.UpdateFacelets(rotatedTargetState);

        Assert.Equal(SmartCubeScrambleStatus.Ready, ready.Status);
        Assert.Empty(ready.RemainingMoves);
    }

    [Fact]
    public void ApplyMove_TracksHalfTurnPendingMove()
    {
        var tracker = new SmartCubeScrambleTracker();
        tracker.SetScramble("R2 U");
        tracker.UpdateFacelets(ThreeByThreeFacelets.Solved);

        var halfTurn = tracker.ApplyMove("R");

        Assert.Equal(SmartCubeScrambleStatus.Scrambling, halfTurn.Status);
        Assert.Equal(0, halfTurn.Progress);
        Assert.Equal("R", halfTurn.PendingMove);
        Assert.Equal(new[] { "R", "U" }, halfTurn.RemainingMoves);
    }

    [Fact]
    public void ApplyMove_BuildsReverseCorrectionForDeviation()
    {
        var tracker = new SmartCubeScrambleTracker();
        tracker.SetScramble("R U");
        tracker.UpdateFacelets(ThreeByThreeFacelets.Solved);

        var correction = tracker.ApplyMove("U");

        Assert.Equal(SmartCubeScrambleStatus.Correction, correction.Status);
        Assert.Equal(new[] { "U'" }, correction.CorrectionMoves);
        Assert.Equal(new[] { "R", "U" }, correction.RemainingMoves);
    }

    [Fact]
    public void ApplyMove_CorrectionResumesFromDeviationProgress()
    {
        var tracker = new SmartCubeScrambleTracker();
        tracker.SetScramble("R U F");
        tracker.UpdateFacelets(ThreeByThreeFacelets.Solved);

        tracker.ApplyMove("R");
        var correction = tracker.ApplyMove("F");

        Assert.Equal(SmartCubeScrambleStatus.Correction, correction.Status);
        Assert.Equal(1, correction.Progress);
        Assert.Equal(new[] { "F'" }, correction.CorrectionMoves);
        Assert.Equal(new[] { "U", "F" }, correction.RemainingMoves);
    }

    [Fact]
    public void ApplyMove_CorrectionResumesFromPendingHalfTurn()
    {
        var tracker = new SmartCubeScrambleTracker();
        tracker.SetScramble("R2 U");
        tracker.UpdateFacelets(ThreeByThreeFacelets.Solved);

        tracker.ApplyMove("R");
        var correction = tracker.ApplyMove("U");

        Assert.Equal(SmartCubeScrambleStatus.Correction, correction.Status);
        Assert.Equal(0, correction.Progress);
        Assert.Equal("R", correction.PendingMove);
        Assert.Equal(new[] { "U'" }, correction.CorrectionMoves);
        Assert.Equal(new[] { "R", "U" }, correction.RemainingMoves);
    }

    [Fact]
    public void ApplyMove_CombinesDeviationCorrection()
    {
        var tracker = new SmartCubeScrambleTracker();
        tracker.SetScramble("R U");
        tracker.UpdateFacelets(ThreeByThreeFacelets.Solved);

        tracker.ApplyMove("U");
        var correction = tracker.ApplyMove("U");

        Assert.Equal(SmartCubeScrambleStatus.Correction, correction.Status);
        Assert.Equal(new[] { "U2" }, correction.CorrectionMoves);
    }
}
