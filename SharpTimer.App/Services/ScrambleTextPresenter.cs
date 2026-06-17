using SharpTimer.Core.SmartCubes;
using System.Collections.Generic;

namespace SharpTimer.App.Services;

public enum ScrambleTextRole
{
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
        string fallbackScramble)
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
                AddCorrectionRuns(snapshot, runs);
                return runs;
            case SmartCubeScrambleStatus.Scrambling:
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
