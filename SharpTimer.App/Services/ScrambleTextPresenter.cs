using SharpTimer.Core.SmartCubes;
using System;
using System.Collections.Generic;

namespace SharpTimer.App.Services;

public enum ScrambleTextRole
{
    Completed,
    Primary,
    Next,
    Correction
}

public readonly record struct ScrambleTextRun(string Text, ScrambleTextRole Role);

public static class ScrambleTextPresenter
{
    public static IReadOnlyList<ScrambleTextRun> BuildSmartCubeRuns(
        SmartCubeScrambleSnapshot snapshot,
        string restoreRequiredText,
        string fallbackScramble,
        SmartCubeScrambleProgressStyle progressStyle = SmartCubeScrambleProgressStyle.HideCompleted)
    {
        var runs = new List<ScrambleTextRun>();
        switch (snapshot.Status)
        {
            case SmartCubeScrambleStatus.Ready:
                return runs;
            case SmartCubeScrambleStatus.RestoreRequired:
                runs.Add(new ScrambleTextRun(restoreRequiredText, ScrambleTextRole.Correction));
                return runs;
            case SmartCubeScrambleStatus.Correction:
                if (progressStyle == SmartCubeScrambleProgressStyle.DimCompleted)
                {
                    AddDimCompletedCorrectionRuns(snapshot, fallbackScramble, runs);
                    return runs;
                }

                AddCorrectionRuns(snapshot, runs);
                return runs;
            case SmartCubeScrambleStatus.Scrambling:
                if (progressStyle == SmartCubeScrambleProgressStyle.DimCompleted)
                {
                    AddDimCompletedRuns(snapshot, fallbackScramble, runs);
                    return runs;
                }

                for (var index = 0; index < snapshot.RemainingMoves.Count; index++)
                {
                    runs.Add(new ScrambleTextRun(
                        snapshot.RemainingMoves[index],
                        index == 0 ? ScrambleTextRole.Next : ScrambleTextRole.Primary));
                }

                return runs;
            default:
                runs.Add(new ScrambleTextRun(fallbackScramble, ScrambleTextRole.Primary));
                return runs;
        }
    }

    private static void AddDimCompletedRuns(
        SmartCubeScrambleSnapshot snapshot,
        string fallbackScramble,
        ICollection<ScrambleTextRun> runs)
    {
        var moves = SmartCubeMoveNotation.ParseSequence(fallbackScramble);
        if (moves.Count == 0)
        {
            foreach (var move in snapshot.RemainingMoves)
            {
                runs.Add(new ScrambleTextRun(move, runs.Count == 0 ? ScrambleTextRole.Next : ScrambleTextRole.Primary));
            }

            return;
        }

        var nextIndex = Math.Max(0, Math.Min(snapshot.Progress, moves.Count));
        for (var index = 0; index < moves.Count; index++)
        {
            var role = index < nextIndex
                ? ScrambleTextRole.Completed
                : index == nextIndex
                    ? ScrambleTextRole.Next
                    : ScrambleTextRole.Primary;
            runs.Add(new ScrambleTextRun(moves[index], role));
        }
    }

    private static void AddCorrectionRuns(SmartCubeScrambleSnapshot snapshot, ICollection<ScrambleTextRun> runs)
    {
        var displayMoves = new List<(string Move, bool IsCorrection)>();
        foreach (var move in snapshot.CorrectionMoves)
        {
            AppendDisplayMove(displayMoves, move, isCorrection: true);
        }

        foreach (var move in snapshot.RemainingMoves)
        {
            AppendDisplayMove(displayMoves, move, isCorrection: false);
        }

        var highlightedNext = false;
        foreach (var move in displayMoves)
        {
            if (move.IsCorrection)
            {
                runs.Add(new ScrambleTextRun(move.Move, ScrambleTextRole.Correction));
            }
            else if (!highlightedNext)
            {
                runs.Add(new ScrambleTextRun(move.Move, ScrambleTextRole.Next));
                highlightedNext = true;
            }
            else
            {
                runs.Add(new ScrambleTextRun(move.Move, ScrambleTextRole.Primary));
            }
        }
    }

    private static void AddDimCompletedCorrectionRuns(
        SmartCubeScrambleSnapshot snapshot,
        string fallbackScramble,
        ICollection<ScrambleTextRun> runs)
    {
        var moves = SmartCubeMoveNotation.ParseSequence(fallbackScramble);
        var completedCount = Math.Max(0, Math.Min(snapshot.Progress, moves.Count));
        for (var index = 0; index < completedCount; index++)
        {
            runs.Add(new ScrambleTextRun(moves[index], ScrambleTextRole.Completed));
        }

        AddCorrectionRuns(snapshot, runs);
    }

    private static void AppendDisplayMove(IList<(string Move, bool IsCorrection)> moves, string move, bool isCorrection)
    {
        if (string.IsNullOrWhiteSpace(move))
        {
            return;
        }

        var normalized = SmartCubeMoveNotation.Normalize(move);
        if (moves.Count == 0 || moves[^1].Move[0] != normalized[0])
        {
            moves.Add((normalized, isCorrection));
            return;
        }

        var last = moves[^1];
        var mergedPower = (GetMovePower(last.Move) + GetMovePower(normalized)) % 4;
        var mergedCorrection = last.IsCorrection || isCorrection;
        moves.RemoveAt(moves.Count - 1);
        if (mergedPower != 0)
        {
            moves.Add((last.Move[0] + GetMoveSuffix(mergedPower), mergedCorrection));
        }
    }

    private static int GetMovePower(string move)
    {
        return move.Length == 1
            ? 1
            : move[1] == '2'
                ? 2
                : 3;
    }

    private static string GetMoveSuffix(int power)
    {
        return power switch
        {
            2 => "2",
            3 => "'",
            _ => string.Empty
        };
    }
}
