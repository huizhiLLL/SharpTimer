using SharpTimer.Core.Models;
using SharpTimer.Core.Timer;

namespace SharpTimer.Tests.Timer;

public sealed class ManualTimerStateMachineTests
{
    private static readonly Guid SessionId = Guid.Parse("22222222-2222-2222-2222-222222222222");

    [Fact]
    public void BeginInspection_MovesFromIdleToInspecting()
    {
        var timeProvider = new TestTimeProvider(DateTimeOffset.Parse("2026-05-01T00:00:00Z"));
        var timer = new ManualTimerStateMachine(timeProvider: timeProvider);

        var snapshot = timer.BeginInspection();

        Assert.Equal(TimerPhase.Inspecting, snapshot.Phase);
        Assert.Equal(TimeSpan.Zero, snapshot.InspectionElapsed);
        Assert.Equal(TimeSpan.FromSeconds(15), snapshot.InspectionRemaining);
        Assert.Equal(Penalty.None, snapshot.PendingPenalty);
    }

    [Fact]
    public void Tick_MarksPlusTwo_WhenInspectionPassesFifteenSeconds()
    {
        var timeProvider = new TestTimeProvider(DateTimeOffset.Parse("2026-05-01T00:00:00Z"));
        var timer = new ManualTimerStateMachine(timeProvider: timeProvider);

        timer.BeginInspection();
        timeProvider.Advance(TimeSpan.FromMilliseconds(15001));
        var snapshot = timer.Tick();

        Assert.Equal(Penalty.PlusTwo, snapshot.PendingPenalty);
    }

    [Fact]
    public void StartSolve_CarriesDnfPenalty_WhenInspectionPassesSeventeenSeconds()
    {
        var timeProvider = new TestTimeProvider(DateTimeOffset.Parse("2026-05-01T00:00:00Z"));
        var timer = new ManualTimerStateMachine(timeProvider: timeProvider);

        timer.BeginInspection();
        timeProvider.Advance(TimeSpan.FromMilliseconds(17001));
        var snapshot = timer.StartSolve();

        Assert.Equal(TimerPhase.Running, snapshot.Phase);
        Assert.Equal(Penalty.Dnf, snapshot.PendingPenalty);
    }

    [Fact]
    public void StopSolve_ReturnsSolveWithElapsedDurationAndPendingPenalty()
    {
        var timeProvider = new TestTimeProvider(DateTimeOffset.Parse("2026-05-01T00:00:00Z"));
        var timer = new ManualTimerStateMachine(timeProvider: timeProvider);

        timer.BeginInspection();
        timeProvider.Advance(TimeSpan.FromSeconds(16));
        timer.StartSolve();
        timeProvider.Advance(TimeSpan.FromSeconds(10));
        var solve = timer.StopSolve(SessionId, "R U R'", "warmup");

        Assert.Equal(TimeSpan.FromSeconds(10), solve.Duration);
        Assert.Equal(Penalty.PlusTwo, solve.Penalty);
        Assert.Equal(TimeSpan.FromSeconds(12), solve.EffectiveDuration);
        Assert.Equal(SessionId, solve.SessionId);
        Assert.Equal("R U R'", solve.Scramble);
        Assert.Equal("warmup", solve.Comment);
    }

    [Fact]
    public void BeginInspection_StartsRunningImmediately_WhenInspectionIsDisabled()
    {
        var timeProvider = new TestTimeProvider(DateTimeOffset.Parse("2026-05-01T00:00:00Z"));
        var timer = new ManualTimerStateMachine(
            new ManualTimerOptions { UseInspection = false },
            timeProvider);

        timer.BeginInspection();
        timeProvider.Advance(TimeSpan.FromSeconds(7));
        var solve = timer.StopSolve(SessionId);

        Assert.Equal(TimeSpan.FromSeconds(7), solve.Duration);
        Assert.Equal(Penalty.None, solve.Penalty);
    }

    private sealed class TestTimeProvider : TimeProvider
    {
        private DateTimeOffset _utcNow;

        public TestTimeProvider(DateTimeOffset utcNow)
        {
            _utcNow = utcNow;
        }

        public override DateTimeOffset GetUtcNow()
        {
            return _utcNow;
        }

        public void Advance(TimeSpan duration)
        {
            _utcNow += duration;
        }
    }
}
