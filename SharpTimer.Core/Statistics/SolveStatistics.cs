using SharpTimer.Core.Models;

namespace SharpTimer.Core.Statistics;

public sealed record SolveStatistics(
    int Count,
    int CompletedCount,
    TimeSpan? Best,
    TimeSpan? Mean,
    TimeSpan? AverageOf5,
    TimeSpan? AverageOf12);
