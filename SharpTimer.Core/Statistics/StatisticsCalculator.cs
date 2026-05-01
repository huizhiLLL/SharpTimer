using SharpTimer.Core.Models;

namespace SharpTimer.Core.Statistics;

public static class StatisticsCalculator
{
    public static SolveStatistics Calculate(IEnumerable<Solve> solves)
    {
        ArgumentNullException.ThrowIfNull(solves);

        var orderedSolves = solves.OrderBy(solve => solve.CreatedAt).ToArray();
        var completedDurations = orderedSolves
            .Select(solve => solve.EffectiveDuration)
            .OfType<TimeSpan>()
            .ToArray();

        return new SolveStatistics(
            Count: orderedSolves.Length,
            CompletedCount: completedDurations.Length,
            Best: completedDurations.Length == 0 ? null : completedDurations.Min(),
            Mean: CalculateMean(completedDurations),
            AverageOf5: CalculateAverageOf(orderedSolves, 5),
            AverageOf12: CalculateAverageOf(orderedSolves, 12));
    }

    public static TimeSpan? CalculateMean(IEnumerable<Solve> solves)
    {
        ArgumentNullException.ThrowIfNull(solves);

        return CalculateMean(solves.Select(solve => solve.EffectiveDuration).OfType<TimeSpan>());
    }

    public static TimeSpan? CalculateAverageOf(IEnumerable<Solve> solves, int windowSize)
    {
        ArgumentNullException.ThrowIfNull(solves);

        if (windowSize < 3)
        {
            throw new ArgumentOutOfRangeException(nameof(windowSize), windowSize, "Average window size must be at least 3.");
        }

        var window = solves
            .OrderBy(solve => solve.CreatedAt)
            .TakeLast(windowSize)
            .ToArray();

        if (window.Length < windowSize)
        {
            return null;
        }

        var effectiveDurations = window
            .Select(solve => solve.EffectiveDuration)
            .ToArray();

        var dnfCount = effectiveDurations.Count(duration => duration is null);
        if (dnfCount > 1)
        {
            return null;
        }

        var sortableDurations = effectiveDurations
            .Select(duration => duration ?? TimeSpan.MaxValue)
            .OrderBy(duration => duration)
            .ToArray();

        var trimmed = sortableDurations
            .Skip(1)
            .Take(windowSize - 2)
            .Where(duration => duration != TimeSpan.MaxValue)
            .ToArray();

        if (trimmed.Length != windowSize - 2)
        {
            return null;
        }

        return CalculateMean(trimmed);
    }

    private static TimeSpan? CalculateMean(IEnumerable<TimeSpan> durations)
    {
        var values = durations.ToArray();
        if (values.Length == 0)
        {
            return null;
        }

        var averageTicks = values.Average(duration => duration.Ticks);
        return TimeSpan.FromTicks((long)Math.Round(averageTicks, MidpointRounding.AwayFromZero));
    }
}
