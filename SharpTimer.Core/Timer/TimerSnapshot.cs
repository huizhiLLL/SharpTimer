using SharpTimer.Core.Models;

namespace SharpTimer.Core.Timer;

public sealed record TimerSnapshot(
    TimerPhase Phase,
    TimeSpan Elapsed,
    TimeSpan InspectionElapsed,
    TimeSpan InspectionRemaining,
    Penalty PendingPenalty,
    DateTimeOffset? StartedAt,
    DateTimeOffset? StoppedAt);
