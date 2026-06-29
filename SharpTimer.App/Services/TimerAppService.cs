using SharpTimer.Core.Models;
using SharpTimer.Core.Scrambles;
using SharpTimer.Core.Statistics;
using SharpTimer.Core.SmartCubes;
using SharpTimer.Core.Timer;
using SharpTimer.Storage;
using SharpTimer.Storage.Repositories;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;

namespace SharpTimer.App.Services;

public sealed class TimerAppService
{
    private readonly SharpTimerDatabase _database;
    private readonly SqliteSessionRepository _sessionRepository;
    private readonly SqliteSolveRepository _solveRepository;
    private readonly ThreeByThreeScrambleGenerator _scrambleGenerator = new();
    private readonly List<string> _scrambleHistory = new();
    private ManualTimerStateMachine _timer;
    private readonly SemaphoreSlim _gate = new(1, 1);
    private Session? _currentSession;
    private IReadOnlyList<Session> _sessions = Array.Empty<Session>();
    private IReadOnlyList<Solve> _solves = Array.Empty<Solve>();
    private int _currentScrambleIndex = -1;
    private AppSettings _settings;

    public TimerAppService(string databasePath, AppSettings settings)
    {
        var connectionFactory = new SqliteConnectionFactory(databasePath);
        _database = new SharpTimerDatabase(connectionFactory);
        _sessionRepository = new SqliteSessionRepository(connectionFactory);
        _solveRepository = new SqliteSolveRepository(connectionFactory);
        _timer = CreateTimer(settings);
        _settings = settings;
    }

    public async Task<TimerAppSnapshot> InitializeAsync(CancellationToken cancellationToken = default)
    {
        await _gate.WaitAsync(cancellationToken);
        try
        {
            await _database.EnsureCreatedAsync(cancellationToken);

            _sessions = await _sessionRepository.ListActiveAsync(cancellationToken);
            _currentSession = _sessions.FirstOrDefault() ?? await CreateDefaultSessionAsync(cancellationToken);
            _sessions = await _sessionRepository.ListActiveAsync(cancellationToken);
            _solves = await _solveRepository.ListBySessionAsync(_currentSession.Id, cancellationToken);
            EnsureCurrentScramble();

            return CreateSnapshot();
        }
        finally
        {
            _gate.Release();
        }
    }

    public TimerAppSnapshot Tick()
    {
        EnsureInitialized();
        _timer.Tick();

        return CreateSnapshot();
    }

    public async Task<TimerAppSnapshot> HandlePrimaryTimerActionAsync(CancellationToken cancellationToken = default)
    {
        await _gate.WaitAsync(cancellationToken);
        try
        {
            EnsureInitialized();

            switch (_timer.Phase)
            {
                case TimerPhase.Idle:
                    _timer.BeginInspection();
                    break;
                case TimerPhase.Inspecting:
                    _timer.StartSolve();
                    break;
                case TimerPhase.Running:
                    var solve = _timer.StopSolve(_currentSession!.Id, GetCurrentScramble());
                    await _solveRepository.SaveAsync(solve, cancellationToken);
                    _solves = await _solveRepository.ListBySessionAsync(_currentSession.Id, cancellationToken);
                    MoveToNextScramble();
                    break;
                case TimerPhase.Stopped:
                    _timer.Reset();
                    _timer.BeginInspection();
                    break;
                default:
                    throw new InvalidOperationException($"Unsupported timer phase: {_timer.Phase}.");
            }

            return CreateSnapshot();
        }
        finally
        {
            _gate.Release();
        }
    }

    public async Task<TimerAppSnapshot> HandleSmartCubeMoveAsync(CancellationToken cancellationToken = default)
    {
        await _gate.WaitAsync(cancellationToken);
        try
        {
            EnsureInitialized();

            switch (_timer.Phase)
            {
                case TimerPhase.Idle:
                    _timer.BeginInspection();
                    if (_timer.Phase == TimerPhase.Inspecting)
                    {
                        _timer.StartSolve();
                    }

                    break;
                case TimerPhase.Inspecting:
                    _timer.StartSolve();
                    break;
                case TimerPhase.Stopped:
                    _timer.Reset();
                    _timer.BeginInspection();
                    if (_timer.Phase == TimerPhase.Inspecting)
                    {
                        _timer.StartSolve();
                    }

                    break;
                case TimerPhase.Running:
                    break;
                default:
                    throw new InvalidOperationException($"Unsupported timer phase: {_timer.Phase}.");
            }

            return CreateSnapshot();
        }
        finally
        {
            _gate.Release();
        }
    }

    public async Task<TimerAppSnapshot> StopSmartCubeSolveAsync(
        SmartCubeSolveCaptureSnapshot? solveCapture = null,
        CancellationToken cancellationToken = default)
    {
        await _gate.WaitAsync(cancellationToken);
        try
        {
            EnsureInitialized();
            if (_timer.Phase != TimerPhase.Running)
            {
                return CreateSnapshot();
            }

            var currentScramble = GetCurrentScramble();
            var solve = BuildSmartCubeSolve(
                _timer.StopSolve(_currentSession!.Id, currentScramble),
                solveCapture,
                currentScramble,
                _settings.SmartCubeSolveMethod);
            await _solveRepository.SaveAsync(solve, cancellationToken);
            _solves = await _solveRepository.ListBySessionAsync(_currentSession.Id, cancellationToken);
            MoveToNextScramble();

            return CreateSnapshot();
        }
        finally
        {
            _gate.Release();
        }
    }

    public TimerAppSnapshot MoveToPreviousScramble()
    {
        EnsureInitialized();
        EnsureCurrentScramble();

        if (_currentScrambleIndex > 0)
        {
            _currentScrambleIndex--;
        }

        return CreateSnapshot();
    }

    public TimerAppSnapshot MoveToNextScramble()
    {
        EnsureInitialized();
        EnsureCurrentScramble();

        if (_currentScrambleIndex < _scrambleHistory.Count - 1)
        {
            _currentScrambleIndex++;
        }
        else
        {
            _scrambleHistory.Add(_scrambleGenerator.Generate());
            _currentScrambleIndex = _scrambleHistory.Count - 1;
        }

        return CreateSnapshot();
    }

    public TimerAppSnapshot ApplySettings(AppSettings settings)
    {
        EnsureInitialized();
        _settings = settings;

        if (_timer.Current.Phase != TimerPhase.Running)
        {
            _timer = CreateTimer(settings);
        }

        return CreateSnapshot();
    }

    public async Task<TimerAppSnapshot> SetPenaltyAsync(
        Guid solveId,
        Penalty penalty,
        CancellationToken cancellationToken = default)
    {
        await _gate.WaitAsync(cancellationToken);
        try
        {
            EnsureInitialized();

            await _solveRepository.UpdatePenaltyAsync(solveId, penalty, cancellationToken);
            _solves = await _solveRepository.ListBySessionAsync(_currentSession!.Id, cancellationToken);

            return CreateSnapshot();
        }
        finally
        {
            _gate.Release();
        }
    }

    public async Task<TimerAppSnapshot> UpdateSolveCommentAsync(
        Guid solveId,
        string? comment,
        CancellationToken cancellationToken = default)
    {
        await _gate.WaitAsync(cancellationToken);
        try
        {
            EnsureInitialized();

            await _solveRepository.UpdateCommentAsync(solveId, comment, cancellationToken);
            _solves = await _solveRepository.ListBySessionAsync(_currentSession!.Id, cancellationToken);

            return CreateSnapshot();
        }
        finally
        {
            _gate.Release();
        }
    }

    public async Task<TimerAppSnapshot> DeleteSolveAsync(Guid solveId, CancellationToken cancellationToken = default)
    {
        await _gate.WaitAsync(cancellationToken);
        try
        {
            EnsureInitialized();

            await _solveRepository.DeleteAsync(solveId, cancellationToken);
            _solves = await _solveRepository.ListBySessionAsync(_currentSession!.Id, cancellationToken);

            return CreateSnapshot();
        }
        finally
        {
            _gate.Release();
        }
    }

    public async Task<TimerAppSnapshot> CreateSessionAsync(
        string name,
        string puzzle = "333",
        CancellationToken cancellationToken = default)
    {
        await _gate.WaitAsync(cancellationToken);
        try
        {
            EnsureInitialized();

            var now = DateTimeOffset.UtcNow;
            var session = new Session
            {
                Name = NormalizeSessionName(name),
                Puzzle = NormalizePuzzle(puzzle),
                CreatedAt = now,
                UpdatedAt = now,
                SortOrder = _sessions.Count
            };

            await _sessionRepository.SaveAsync(session, cancellationToken);
            _sessions = await _sessionRepository.ListActiveAsync(cancellationToken);
            _currentSession = session;
            _solves = Array.Empty<Solve>();
            _timer.Reset();
            ResetScrambleHistory();

            return CreateSnapshot();
        }
        finally
        {
            _gate.Release();
        }
    }

    public async Task<TimerAppSnapshot> SwitchSessionAsync(Guid sessionId, CancellationToken cancellationToken = default)
    {
        await _gate.WaitAsync(cancellationToken);
        try
        {
            EnsureInitialized();

            var session = _sessions.FirstOrDefault(item => item.Id == sessionId)
                ?? await _sessionRepository.GetAsync(sessionId, cancellationToken)
                ?? throw new InvalidOperationException("Session does not exist.");

            _currentSession = session;
            _solves = await _solveRepository.ListBySessionAsync(session.Id, cancellationToken);
            _timer.Reset();
            ResetScrambleHistory();

            return CreateSnapshot();
        }
        finally
        {
            _gate.Release();
        }
    }

    public async Task<TimerAppSnapshot> RenameCurrentSessionAsync(string name, CancellationToken cancellationToken = default)
    {
        await _gate.WaitAsync(cancellationToken);
        try
        {
            EnsureInitialized();

            var renamedSession = _currentSession! with
            {
                Name = NormalizeSessionName(name),
                UpdatedAt = DateTimeOffset.UtcNow
            };

            await _sessionRepository.SaveAsync(renamedSession, cancellationToken);
            _sessions = await _sessionRepository.ListActiveAsync(cancellationToken);
            _currentSession = _sessions.First(session => session.Id == renamedSession.Id);

            return CreateSnapshot();
        }
        finally
        {
            _gate.Release();
        }
    }

    public async Task<TimerAppSnapshot> ArchiveCurrentSessionAsync(CancellationToken cancellationToken = default)
    {
        await _gate.WaitAsync(cancellationToken);
        try
        {
            EnsureInitialized();

            var archivedSession = _currentSession! with
            {
                IsArchived = true,
                UpdatedAt = DateTimeOffset.UtcNow
            };

            await _sessionRepository.SaveAsync(archivedSession, cancellationToken);
            _sessions = await _sessionRepository.ListActiveAsync(cancellationToken);
            _currentSession = _sessions.FirstOrDefault() ?? await CreateDefaultSessionAsync(cancellationToken);
            _sessions = await _sessionRepository.ListActiveAsync(cancellationToken);
            _solves = await _solveRepository.ListBySessionAsync(_currentSession.Id, cancellationToken);
            _timer.Reset();
            ResetScrambleHistory();

            return CreateSnapshot();
        }
        finally
        {
            _gate.Release();
        }
    }

    private async Task<Session> CreateDefaultSessionAsync(CancellationToken cancellationToken)
    {
        var now = DateTimeOffset.UtcNow;
        var session = new Session
        {
            Name = "Main",
            Puzzle = "333",
            CreatedAt = now,
            UpdatedAt = now
        };

        await _sessionRepository.SaveAsync(session, cancellationToken);
        return session;
    }

    private TimerAppSnapshot CreateSnapshot()
    {
        EnsureInitialized();
        return new TimerAppSnapshot(
            _currentSession!,
            _sessions,
            _timer.Current,
            GetCurrentScramble(),
            _solves,
            StatisticsCalculator.Calculate(_solves));
    }

    private string GetCurrentScramble()
    {
        EnsureCurrentScramble();

        return _scrambleHistory[_currentScrambleIndex];
    }

    private void EnsureCurrentScramble()
    {
        if (_currentScrambleIndex >= 0 && _currentScrambleIndex < _scrambleHistory.Count)
        {
            return;
        }

        _scrambleHistory.Clear();
        _scrambleHistory.Add(_scrambleGenerator.Generate());
        _currentScrambleIndex = 0;
    }

    private void ResetScrambleHistory()
    {
        _scrambleHistory.Clear();
        _currentScrambleIndex = -1;
        EnsureCurrentScramble();
    }

    private void EnsureInitialized()
    {
        if (_currentSession is null)
        {
            throw new InvalidOperationException("Timer app service has not been initialized.");
        }
    }

    private static string NormalizeSessionName(string name)
    {
        var normalizedName = name.Trim();
        return string.IsNullOrWhiteSpace(normalizedName) ? "Untitled" : normalizedName;
    }

    private static string NormalizePuzzle(string puzzle)
    {
        var normalizedPuzzle = puzzle.Trim();
        return string.IsNullOrWhiteSpace(normalizedPuzzle) ? "333" : normalizedPuzzle;
    }

    private static ManualTimerStateMachine CreateTimer(AppSettings settings)
    {
        return new ManualTimerStateMachine(new ManualTimerOptions
        {
            UseInspection = settings.UseInspection
        });
    }

    private static Solve BuildSmartCubeSolve(
        Solve solve,
        SmartCubeSolveCaptureSnapshot? solveCapture,
        string scramble,
        SmartCubeSolveMethod solveMethod)
    {
        var capture = solveCapture ?? SmartCubeSolveCaptureSnapshot.Empty;
        var startFacelets = ThreeByThreeFacelets.ApplyScramble(scramble);
        var reconstruction = SmartCubeSolveReconstruction.FromCapture(startFacelets, capture, solveMethod);
        var normalizedMoves = NormalizeMoveSequence(SmartCubeMoveNotation.ParseSequence(reconstruction.MoveSequence));
        var moveText = normalizedMoves.Count == 0 ? null : string.Join(" ", normalizedMoves);
        var moveCount = normalizedMoves.Count == 0 ? (int?)null : normalizedMoves.Count;
        var tps = moveCount is null || solve.Duration <= TimeSpan.Zero
            ? (double?)null
            : moveCount.Value / solve.Duration.TotalSeconds;

        return solve with
        {
            Source = SolveSource.SmartCube,
            MoveSequence = moveText,
            MoveCount = moveCount,
            Tps = tps,
            ReconstructionMethod = reconstruction.MethodId,
            SolveMetaJson = BuildSolveMetaJson(solve.Duration, moveText, moveCount, tps, capture, reconstruction)
        };
    }

    private static IReadOnlyList<string> NormalizeMoveSequence(IReadOnlyList<string>? moveSequence)
    {
        if (moveSequence is null || moveSequence.Count == 0)
        {
            return Array.Empty<string>();
        }

        var moves = new List<string>();
        foreach (var move in moveSequence)
        {
            if (string.IsNullOrWhiteSpace(move))
            {
                continue;
            }

            moves.Add(SharpTimer.Core.SmartCubes.SmartCubeMoveNotation.Normalize(move));
        }

        return moves;
    }

    private static string BuildSolveMetaJson(
        TimeSpan duration,
        string? moveSequence,
        int? moveCount,
        double? tps,
        SmartCubeSolveCaptureSnapshot capture,
        SmartCubeSolveReconstruction reconstruction)
    {
        return JsonSerializer.Serialize(new
        {
            version = 1,
            method = reconstruction.MethodId,
            solveTimeMs = (int)Math.Round(duration.TotalMilliseconds, MidpointRounding.AwayFromZero),
            moves = moveSequence ?? string.Empty,
            moveCount = moveCount ?? 0,
            tps,
            prettySolve = reconstruction.PrettySolve,
            rawMoves = capture.Moves.Select(move => new
            {
                move = move.Move,
                deltaMs = move.Delta is null
                    ? (int?)null
                    : (int)Math.Round(move.Delta.Value.TotalMilliseconds, MidpointRounding.AwayFromZero),
                elapsedMs = (int)Math.Round(move.Elapsed.TotalMilliseconds, MidpointRounding.AwayFromZero),
                cubeTimestampMs = move.CubeTimestamp is null
                    ? (int?)null
                    : (int)Math.Round(move.CubeTimestamp.Value.TotalMilliseconds, MidpointRounding.AwayFromZero)
            }).ToArray(),
            phases = reconstruction.Phases.Select(phase => new
            {
                name = phase.Name,
                moves = phase.Moves,
                moveCount = phase.MoveCount,
                startMs = phase.StartMs,
                endMs = phase.EndMs,
                durationMs = phase.DurationMs,
                tps = phase.DurationMs > 0
                    ? phase.MoveCount * 1000d / phase.DurationMs
                    : 0d
            }).ToArray()
        });
    }
}
