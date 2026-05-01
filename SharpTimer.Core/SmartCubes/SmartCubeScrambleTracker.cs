namespace SharpTimer.Core.SmartCubes;

public sealed class SmartCubeScrambleTracker
{
    public const int CorrectionLimit = 10;

    private readonly List<string> _moves = new();
    private readonly List<string> _states = new();
    private readonly List<string> _deviationMoves = new();
    private readonly List<string> _correctionMoves = new();
    private string _scramble = string.Empty;
    private int _progress;
    private string? _pendingMove;
    private string? _currentFacelets;
    private int _correctionBaseProgress = -1;
    private string? _correctionBasePendingMove;
    private bool _correctionLocked;

    public SmartCubeScrambleSnapshot Current => CreateSnapshot();

    public bool SetScramble(string? scramble)
    {
        var normalized = string.Join(" ", SmartCubeMoveNotation.ParseSequence(scramble));
        if (normalized == _scramble)
        {
            return false;
        }

        _scramble = normalized;
        _moves.Clear();
        _states.Clear();
        ClearProgress();

        var state = ThreeByThreeFacelets.Solved;
        foreach (var move in SmartCubeMoveNotation.ParseSequence(normalized))
        {
            _moves.Add(move);
            state = ThreeByThreeFacelets.ApplyMove(state, move);
            _states.Add(state);
        }

        return true;
    }

    public SmartCubeScrambleSnapshot UpdateFacelets(string facelets)
    {
        if (!ThreeByThreeFacelets.IsValidState(facelets))
        {
            return CreateSnapshot();
        }

        _currentFacelets = facelets;
        UpdateProgress(facelets, null);
        return CreateSnapshot();
    }

    public SmartCubeScrambleSnapshot ApplyMove(string move)
    {
        if (!ThreeByThreeFacelets.IsValidState(_currentFacelets ?? string.Empty))
        {
            return CreateSnapshot();
        }

        var normalizedMove = SmartCubeMoveNotation.Normalize(move);
        _currentFacelets = ThreeByThreeFacelets.ApplyMove(_currentFacelets!, normalizedMove);
        UpdateProgress(_currentFacelets, normalizedMove);
        return CreateSnapshot();
    }

    public void Reset()
    {
        _scramble = string.Empty;
        _moves.Clear();
        _states.Clear();
        ClearProgress();
    }

    private void ClearProgress()
    {
        _progress = 0;
        _pendingMove = null;
        _currentFacelets = null;
        ClearCorrection();
    }

    private void ClearCorrection()
    {
        _correctionBaseProgress = -1;
        _correctionBasePendingMove = null;
        _deviationMoves.Clear();
        _correctionMoves.Clear();
        _correctionLocked = false;
    }

    private void UpdateProgress(string facelets, string? latestMove)
    {
        if (_moves.Count == 0)
        {
            _progress = 0;
            _pendingMove = null;
            ClearCorrection();
            return;
        }

        var progressInfo = ResolveSequenceProgress(facelets);
        if (progressInfo is not null)
        {
            _progress = progressInfo.Value.Progress;
            _pendingMove = progressInfo.Value.PendingMove;
            ClearCorrection();
            return;
        }

        if (ThreeByThreeFacelets.IsSolvedIgnoringRotation(facelets))
        {
            _progress = 0;
            _pendingMove = null;
            ClearCorrection();
            return;
        }

        if (_correctionLocked)
        {
            _progress = -1;
            _pendingMove = null;
            return;
        }

        if (latestMove is not null)
        {
            AppendDeviationMove(latestMove);
        }

        _progress = -1;
        _pendingMove = null;
    }

    private (int Progress, string? PendingMove)? ResolveSequenceProgress(string facelets)
    {
        if (facelets == ThreeByThreeFacelets.Solved)
        {
            return (0, null);
        }

        for (var index = 0; index < _moves.Count; index++)
        {
            var baseState = index == 0 ? ThreeByThreeFacelets.Solved : _states[index - 1];
            if (facelets == baseState)
            {
                return (index, null);
            }

            var move = _moves[index];
            if (!move.EndsWith("2", StringComparison.Ordinal))
            {
                continue;
            }

            var clockwisePartial = ThreeByThreeFacelets.ApplyMove(baseState, move[0].ToString());
            if (facelets == clockwisePartial)
            {
                return (index, move[0].ToString());
            }

            var counterClockwisePartial = ThreeByThreeFacelets.ApplyMove(baseState, move[0] + "'");
            if (facelets == counterClockwisePartial)
            {
                return (index, move[0] + "'");
            }
        }

        if (_states.Count > 0 && ThreeByThreeFacelets.IsSameStateIgnoringRotation(facelets, _states[^1]))
        {
            return (_states.Count, null);
        }

        return null;
    }

    private void AppendDeviationMove(string move)
    {
        if (_moves.Count == 0)
        {
            return;
        }

        if (_correctionBaseProgress < 0)
        {
            _correctionBaseProgress = Math.Max(0, _progress);
            _correctionBasePendingMove = _pendingMove;
        }

        SmartCubeMoveNotation.AppendCombined(_deviationMoves, move);
        RebuildCorrectionMoves();
        if (_correctionMoves.Count > CorrectionLimit)
        {
            _correctionLocked = true;
            _correctionBaseProgress = -1;
            _correctionBasePendingMove = null;
            _deviationMoves.Clear();
            _correctionMoves.Clear();
        }
    }

    private void RebuildCorrectionMoves()
    {
        _correctionMoves.Clear();
        for (var index = _deviationMoves.Count - 1; index >= 0; index--)
        {
            SmartCubeMoveNotation.AppendCombined(
                _correctionMoves,
                SmartCubeMoveNotation.Invert(_deviationMoves[index]));
        }
    }

    private SmartCubeScrambleSnapshot CreateSnapshot()
    {
        if (_moves.Count == 0)
        {
            return SmartCubeScrambleSnapshot.Unavailable with { CurrentFacelets = _currentFacelets };
        }

        if (_correctionLocked)
        {
            return new SmartCubeScrambleSnapshot(
                SmartCubeScrambleStatus.RestoreRequired,
                -1,
                _moves.Count,
                null,
                Array.Empty<string>(),
                Array.Empty<string>(),
                _currentFacelets);
        }

        if (_progress < 0)
        {
            return new SmartCubeScrambleSnapshot(
                SmartCubeScrambleStatus.Correction,
                _correctionBaseProgress,
                _moves.Count,
                _correctionBasePendingMove,
                BuildRemainingMoves(_correctionBaseProgress, _correctionBasePendingMove),
                _correctionMoves.ToArray(),
                _currentFacelets);
        }

        return new SmartCubeScrambleSnapshot(
            _progress >= _moves.Count ? SmartCubeScrambleStatus.Ready : SmartCubeScrambleStatus.Scrambling,
            _progress,
            _moves.Count,
            _pendingMove,
            BuildRemainingMoves(_progress, _pendingMove),
            Array.Empty<string>(),
            _currentFacelets);
    }

    private IReadOnlyList<string> BuildRemainingMoves(int progress, string? pendingMove)
    {
        if (_moves.Count == 0)
        {
            return Array.Empty<string>();
        }

        var remaining = new List<string>();
        var start = Math.Max(0, progress);
        if (!string.IsNullOrWhiteSpace(pendingMove) && start < _moves.Count)
        {
            remaining.Add(pendingMove);
            start++;
        }

        for (var index = start; index < _moves.Count; index++)
        {
            remaining.Add(_moves[index]);
        }

        return remaining;
    }
}
