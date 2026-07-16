using SharpTimer.Core.SmartCubes;

namespace SharpTimer.Tests.SmartCubes;

public sealed class SmartCubeSolvePhaseStatisticsTests
{
    [Fact]
    public void FromSolveMetaJson_CalculatesTimeAndTpsFromPhaseBounds()
    {
        const string meta = """
            {
              "phases": [
                {
                  "name": "Cross",
                  "moveCount": 4,
                  "startMs": 100,
                  "endMs": 1100,
                  "durationMs": 9999,
                  "tps": 99
                }
              ]
            }
            """;

        var phase = Assert.Single(SmartCubeSolvePhaseStatistics.FromSolveMetaJson(meta));

        Assert.Equal(1000, phase.DurationMs);
        Assert.Equal(4, phase.MoveCount);
        Assert.Equal(4d, phase.Tps);
    }

    [Fact]
    public void FromSolveMetaJson_GroupsF2lAndKeepsDetailedPhases()
    {
        const string meta = """
            {
              "phases": [
                { "name": "F2L 1", "moveCount": 4, "startMs": 500, "endMs": 1000 },
                { "name": "F2L 2", "moveCount": 6, "startMs": 1000, "endMs": 2000 }
              ]
            }
            """;

        var f2l = Assert.Single(SmartCubeSolvePhaseStatistics.FromSolveMetaJson(meta));

        Assert.Equal("F2L", f2l.Name);
        Assert.Equal(1500, f2l.DurationMs);
        Assert.Equal(10, f2l.MoveCount);
        Assert.Equal(10 * 1000d / 1500d, f2l.Tps);
        Assert.Equal(new[] { "F2L 1", "F2L 2" }, f2l.Children.Select(phase => phase.Name));
    }

    [Fact]
    public void FromSolveMetaJson_UsesLegacyDurationWhenPhaseBoundsAreMissing()
    {
        const string meta = """
            {
              "phases": [
                { "name": "PLL", "moveCount": 8, "durationMs": 2000, "tps": 99 }
              ]
            }
            """;

        var phase = Assert.Single(SmartCubeSolvePhaseStatistics.FromSolveMetaJson(meta));

        Assert.Equal(2000, phase.DurationMs);
        Assert.Equal(4d, phase.Tps);
    }

    [Fact]
    public void FromSolveMetaJson_IgnoresNonObjectPhaseEntries()
    {
        const string meta = """{ "phases": [null, 1, "Cross"] }""";

        var phases = SmartCubeSolvePhaseStatistics.FromSolveMetaJson(meta);

        Assert.Empty(phases);
    }
}
