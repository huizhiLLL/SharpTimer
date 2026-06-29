using SharpTimer.Core.Models;

namespace SharpTimer.Core.Timer;

public sealed class ManualTimerStateMachine
{
    private readonly ManualTimerOptions _options;
    private readonly TimeProvider _timeProvider;
    private DateTimeOffset? _inspectionStartedAt;
    private DateTimeOffset? _startedAt;
    private DateTimeOffset? _stoppedAt;
    private TimeSpan _elapsedAtStop;

    public ManualTimerStateMachine(ManualTimerOptions? options = null, TimeProvider? timeProvider = null)
    {
        _options = options ?? new ManualTimerOptions();
        _timeProvider = timeProvider ?? TimeProvider.System;

        if (_options.InspectionLimit < TimeSpan.Zero)
        {
            throw new ArgumentOutOfRangeException(nameof(options), "Inspection limit cannot be negative.");
        }
    }

    public TimerPhase Phase { get; private set; } = TimerPhase.Idle;

    public TimerSnapshot Current => GetSnapshot();

    public TimerSnapshot BeginInspection()
    {
        EnsurePhase(TimerPhase.Idle);

        var now = _timeProvider.GetUtcNow();
        _inspectionStartedAt = _options.UseInspection ? now : null;
        _startedAt = null;
        _stoppedAt = null;
        _elapsedAtStop = TimeSpan.Zero;
        Phase = _options.UseInspection ? TimerPhase.Inspecting : TimerPhase.Running;

        if (!_options.UseInspection)
        {
            _startedAt = now;
        }

        return GetSnapshot(now);
    }

    public TimerSnapshot StartSolve()
    {
        EnsurePhase(TimerPhase.Inspecting);

        var now = _timeProvider.GetUtcNow();
        _startedAt = now;
        _stoppedAt = null;
        _elapsedAtStop = TimeSpan.Zero;
        Phase = TimerPhase.Running;

        return GetSnapshot(now);
    }

    public Solve StopSolve(Guid sessionId, string? scramble = null, string? comment = null)
    {
        EnsurePhase(TimerPhase.Running);

        var now = _timeProvider.GetUtcNow();
        _stoppedAt = now;
        _elapsedAtStop = GetRunningElapsed(now);
        Phase = TimerPhase.Stopped;

        return new Solve
        {
            SessionId = sessionId,
            Duration = _elapsedAtStop,
            CreatedAt = now,
            Scramble = scramble,
            Comment = comment
        };
    }

    public TimerSnapshot Reset()
    {
        _inspectionStartedAt = null;
        _startedAt = null;
        _stoppedAt = null;
        _elapsedAtStop = TimeSpan.Zero;
        Phase = TimerPhase.Idle;

        return GetSnapshot();
    }

    public TimerSnapshot Tick()
    {
        return GetSnapshot();
    }

    private TimerSnapshot GetSnapshot()
    {
        return GetSnapshot(_timeProvider.GetUtcNow());
    }

    private TimerSnapshot GetSnapshot(DateTimeOffset now)
    {
        var inspectionElapsed = GetInspectionElapsed(now);
        var inspectionRemaining = _options.InspectionLimit - inspectionElapsed;
        if (inspectionRemaining < TimeSpan.Zero)
        {
            inspectionRemaining = TimeSpan.Zero;
        }

        return new TimerSnapshot(
            Phase,
            GetElapsed(now),
            inspectionElapsed,
            inspectionRemaining,
            _startedAt,
            _stoppedAt);
    }

    private TimeSpan GetElapsed(DateTimeOffset now)
    {
        return Phase switch
        {
            TimerPhase.Running => GetRunningElapsed(now),
            TimerPhase.Stopped => _elapsedAtStop,
            _ => TimeSpan.Zero
        };
    }

    private TimeSpan GetRunningElapsed(DateTimeOffset now)
    {
        return _startedAt is null ? TimeSpan.Zero : now - _startedAt.Value;
    }

    private TimeSpan GetInspectionElapsed(DateTimeOffset now)
    {
        return _inspectionStartedAt is null ? TimeSpan.Zero : now - _inspectionStartedAt.Value;
    }

    private void EnsurePhase(TimerPhase expected)
    {
        if (Phase != expected)
        {
            throw new InvalidOperationException($"Expected timer phase {expected}, but current phase is {Phase}.");
        }
    }
}
