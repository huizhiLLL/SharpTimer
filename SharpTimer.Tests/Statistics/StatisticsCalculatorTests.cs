using SharpTimer.Core.Models;
using SharpTimer.Core.Statistics;

namespace SharpTimer.Tests.Statistics;

public sealed class StatisticsCalculatorTests
{
    [Fact]
    public void Calculate_ReturnsBestMeanAndAverages()
    {
        var solves = CreateSolves(10, 11, 12, 13, 14, 15, 16, 17, 18, 19, 20, 21);

        var statistics = StatisticsCalculator.Calculate(solves);

        Assert.Equal(12, statistics.Count);
        Assert.Equal(12, statistics.CompletedCount);
        Assert.Equal(TimeSpan.FromSeconds(10), statistics.Best);
        Assert.Equal(TimeSpan.FromSeconds(15.5), statistics.Mean);
        Assert.Equal(TimeSpan.FromSeconds(19), statistics.AverageOf5);
        Assert.Equal(TimeSpan.FromSeconds(15.5), statistics.AverageOf12);
    }

    [Fact]
    public void CalculateAverageOf_DropsSingleDnfAsWorst()
    {
        var solves = new[]
        {
            CreateSolve(10, 0),
            CreateSolve(11, 1),
            CreateSolve(12, 2),
            CreateSolve(13, 3),
            CreateSolve(14, 4, Penalty.Dnf)
        };

        var average = StatisticsCalculator.CalculateAverageOf(solves, 5);

        Assert.Equal(TimeSpan.FromSeconds(12), average);
    }

    [Fact]
    public void CalculateAverageOf_ReturnsNull_WhenWindowHasMultipleDnfs()
    {
        var solves = new[]
        {
            CreateSolve(10, 0),
            CreateSolve(11, 1, Penalty.Dnf),
            CreateSolve(12, 2),
            CreateSolve(13, 3),
            CreateSolve(14, 4, Penalty.Dnf)
        };

        var average = StatisticsCalculator.CalculateAverageOf(solves, 5);

        Assert.Null(average);
    }

    [Fact]
    public void CalculateAverageOf_ReturnsNull_WhenWindowIsIncomplete()
    {
        var solves = CreateSolves(10, 11, 12, 13);

        var average = StatisticsCalculator.CalculateAverageOf(solves, 5);

        Assert.Null(average);
    }

    private static Solve[] CreateSolves(params double[] seconds)
    {
        return seconds
            .Select((second, index) => CreateSolve(second, index))
            .ToArray();
    }

    private static Solve CreateSolve(double seconds, int index, Penalty penalty = Penalty.None)
    {
        return new Solve
        {
            SessionId = Guid.Parse("11111111-1111-1111-1111-111111111111"),
            Duration = TimeSpan.FromSeconds(seconds),
            Penalty = penalty,
            CreatedAt = DateTimeOffset.Parse("2026-05-01T00:00:00Z").AddSeconds(index)
        };
    }
}
