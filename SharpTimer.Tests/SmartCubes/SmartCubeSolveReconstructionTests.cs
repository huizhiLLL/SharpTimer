using SharpTimer.Core.SmartCubes;

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
}
