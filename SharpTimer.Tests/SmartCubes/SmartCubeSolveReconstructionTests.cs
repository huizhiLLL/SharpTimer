using SharpTimer.Core.SmartCubes;
using System.Reflection;

namespace SharpTimer.Tests.SmartCubes;

public sealed class SmartCubeSolveReconstructionTests
{
    [Fact]
    public void FromCapture_BuildsCfopPhaseMetadata()
    {
        var capture = CreateCapture("R", "U", "R'");

        var reconstruction = SmartCubeSolveReconstruction.FromCapture(
            ThreeByThreeFacelets.Solved,
            capture,
            SmartCubeSolveMethod.Cfop);

        Assert.Equal("333-smart-cf4op", reconstruction.MethodId);
        Assert.Equal("R U R'", reconstruction.MoveSequence);
        Assert.Equal(3, reconstruction.MoveCount);
        Assert.Equal(new[] { "Cross", "F2L 1", "F2L 2", "F2L 3", "F2L 4", "OLL", "PLL" }, reconstruction.Phases.Select(phase => phase.Name));
        Assert.Contains(reconstruction.Phases, phase => phase.Name == "PLL" && phase.MoveCount == 3);
        var pll = Assert.Single(reconstruction.Phases.Where(phase => phase.Name == "PLL" && phase.MoveCount == 3));
        Assert.Equal(200, pll.DurationMs);
        Assert.Equal("R U R' // PLL", reconstruction.PrettySolve);
    }

    [Fact]
    public void FromCapture_BuildsRouxPhaseMetadata()
    {
        var capture = CreateCapture("R", "U");

        var reconstruction = SmartCubeSolveReconstruction.FromCapture(
            ThreeByThreeFacelets.Solved,
            capture,
            SmartCubeSolveMethod.Roux);

        Assert.Equal("333-smart-roux", reconstruction.MethodId);
        Assert.Equal(new[] { "FB", "SB", "CMLL", "L6E" }, reconstruction.Phases.Select(phase => phase.Name));
    }

    [Fact]
    public void FromCapture_MergesAdjacentSameFaceTurnsIntoOneMove()
    {
        var capture = CreateCapture("R", "R");

        var reconstruction = SmartCubeSolveReconstruction.FromCapture(
            ThreeByThreeFacelets.Solved,
            capture,
            SmartCubeSolveMethod.Cfop);

        Assert.Equal("R2", reconstruction.MoveSequence);
        Assert.Equal(1, reconstruction.MoveCount);
    }

    [Fact]
    public void FromCapture_RecognizesOppositeLayerComboAsSliceWithinWindow()
    {
        var capture = CreateCapture(("U", 0), ("D'", 90));

        var reconstruction = SmartCubeSolveReconstruction.FromCapture(
            ThreeByThreeFacelets.Solved,
            capture,
            SmartCubeSolveMethod.Cfop);

        Assert.Equal("E", reconstruction.MoveSequence);
        Assert.Equal(1, reconstruction.MoveCount);
        Assert.Contains(reconstruction.Phases, phase => phase.Name == "PLL" && phase.Moves == "E" && phase.MoveCount == 1);
    }

    [Fact]
    public void FromCapture_KeepsOppositeLayerTurnsSeparateOutsideWindow()
    {
        var capture = CreateCapture(("U", 0), ("D'", 120));

        var reconstruction = SmartCubeSolveReconstruction.FromCapture(
            ThreeByThreeFacelets.Solved,
            capture,
            SmartCubeSolveMethod.Cfop);

        Assert.Equal("U D'", reconstruction.MoveSequence);
        Assert.Equal(2, reconstruction.MoveCount);
    }

    [Fact]
    public void FromCapture_PreservesZeroPhaseTiming_WhenRawTimingIsZero()
    {
        var capture = CreateCapture(("U", 0), ("D'", 0));

        var reconstruction = SmartCubeSolveReconstruction.FromCapture(
            ThreeByThreeFacelets.Solved,
            capture,
            SmartCubeSolveMethod.Cfop);

        var pll = Assert.Single(reconstruction.Phases.Where(phase => phase.Name == "PLL" && phase.MoveCount == 1));
        Assert.Equal(0, pll.StartMs);
        Assert.Equal(0, pll.EndMs);
        Assert.Equal(0, pll.DurationMs);
    }

    [Fact]
    public void GetCfopProgress_UsesSixAxisOrientations()
    {
        var facelets = ThreeByThreeFacelets.Solved.ToCharArray();
        facelets[21] = 'B';
        facelets[14] = 'F';
        facelets[39] = 'B';

        Assert.Equal(5, InvokeCfopProgress(new string(facelets)));
    }

    private static SmartCubeSolveCaptureSnapshot CreateCapture(params string[] moves)
    {
        var capture = new SmartCubeSolveCapture();
        var now = DateTimeOffset.Parse("2026-05-01T00:00:00Z");
        capture.MarkReadyToStart();
        for (var index = 0; index < moves.Length; index++)
        {
            capture.RecordMove(moves[index], now.AddMilliseconds(index * 100), TimeSpan.FromMilliseconds(index * 100));
        }

        return capture.Snapshot();
    }

    private static SmartCubeSolveCaptureSnapshot CreateCapture(params (string Move, int ElapsedMs)[] moves)
    {
        var capture = new SmartCubeSolveCapture();
        var now = DateTimeOffset.Parse("2026-05-01T00:00:00Z");
        capture.MarkReadyToStart();
        foreach (var move in moves)
        {
            capture.RecordMove(move.Move, now.AddMilliseconds(move.ElapsedMs), TimeSpan.FromMilliseconds(move.ElapsedMs));
        }

        return capture.Snapshot();
    }

    private static int InvokeCfopProgress(string facelets)
    {
        var method = typeof(SmartCubeSolveReconstruction).GetMethod(
            "GetCfopProgress",
            BindingFlags.NonPublic | BindingFlags.Static);
        Assert.NotNull(method);

        return (int)method.Invoke(null, new object[] { facelets })!;
    }
}
