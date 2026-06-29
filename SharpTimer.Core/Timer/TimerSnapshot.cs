namespace SharpTimer.Core.Timer;

public sealed record TimerSnapshot(
    TimerPhase Phase,
    TimeSpan Elapsed,
    TimeSpan InspectionElapsed,
    TimeSpan InspectionRemaining,
    DateTimeOffset? StartedAt,
    DateTimeOffset? StoppedAt);
