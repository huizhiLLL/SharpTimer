namespace SharpTimer.Core.SmartCubes;

public sealed record SmartCubeSolveReconstruction(
    SmartCubeSolveMethod Method,
    string MethodId,
    string MoveSequence,
    string PrettySolve,
    int MoveCount,
    IReadOnlyList<SmartCubeSolvePhase> Phases)
{
    private static readonly string[] CfopPhaseNames = { "Cross", "F2L 1", "F2L 2", "F2L 3", "F2L 4", "OLL", "PLL" };
    private static readonly string[] RouxPhaseNames = { "FB", "SB", "CMLL", "L6E" };

    private static readonly IReadOnlyList<int[]> CrossMask = ToEqus("----U--------R--R-----F--F--D-DDD-D-----L--L-----B--B-");
    private static readonly IReadOnlyList<int[]> F2L1Mask = ToEqus("----U-------RR-RR-----FF-FF-DDDDD-D-----L--L-----B--B-");
    private static readonly IReadOnlyList<int[]> F2L2Mask = ToEqus("----U--------R--R----FF-FF-DD-DDD-D-----LL-LL----B--B-");
    private static readonly IReadOnlyList<int[]> F2L3Mask = ToEqus("----U--------RR-RR----F--F--D-DDD-DD----L--L----BB-BB-");
    private static readonly IReadOnlyList<int[]> F2L4Mask = ToEqus("----U--------R--R-----F--F--D-DDDDD----LL-LL-----BB-BB");
    private static readonly IReadOnlyList<int[]> F2LMask = ToEqus("----U-------RRRRRR---FFFFFFDDDDDDDDD---LLLLLL---BBBBBB");
    private static readonly IReadOnlyList<int[]> OllMask = ToEqus("UUUUUUUUU---RRRRRR---FFFFFFDDDDDDDDD---LLLLLL---BBBBBB");
    private static readonly IReadOnlyList<int[]> SolvedMask = ToEqus(ThreeByThreeFacelets.Solved);
    private static readonly IReadOnlyList<int[]> RouxFbMask = ToEqus("---------------------F--F--D--D--D-----LLLLLL-----B--B");
    private static readonly IReadOnlyList<int[]> RouxSbMask = ToEqus("------------RRRRRR---F-FF-FD-DD-DD-D---LLLLLL---B-BB-B");
    private static readonly IReadOnlyList<int[]> RouxCmllMask = ToEqus("U-U---U-Ur-rRRRRRRf-fF-FF-FD-DD-DD-Dl-lLLLLLLb-bB-BB-B");

    public static SmartCubeSolveReconstruction FromCapture(
        string startFacelets,
        SmartCubeSolveCaptureSnapshot capture,
        SmartCubeSolveMethod method)
    {
        var phaseNames = method == SmartCubeSolveMethod.Roux ? RouxPhaseNames : CfopPhaseNames;
        if (!ThreeByThreeFacelets.IsValidState(startFacelets) || capture.Moves.Count == 0)
        {
            var moves = BuildMoveSequence(capture.Moves);
            return new SmartCubeSolveReconstruction(
                method,
                GetMethodId(method),
                moves,
                moves,
                CountMoves(moves),
                CreateEmptyPhases(phaseNames));
        }

        var buckets = phaseNames.Select(_ => new List<SmartCubeSolveMoveSample>()).ToArray();
        var state = startFacelets;
        var status = UpdatePhaseStatus(phaseNames.Length, GetMethodProgress(state, method));

        foreach (var move in capture.Moves)
        {
            buckets[status - 1].Add(move);
            state = ThreeByThreeFacelets.ApplyMove(state, move.Move);
            status = UpdatePhaseStatus(status, GetMethodProgress(state, method));
        }

        var phases = new List<SmartCubeSolvePhase>();
        var allMoves = new List<string>();
        var previousEndMs = 0;
        for (var index = phaseNames.Length - 1; index >= 0; index--)
        {
            var phaseMoves = BuildCombinedMoves(buckets[index]);
            var moveText = string.Join(" ", phaseMoves);
            var moveCount = phaseMoves.Count;
            allMoves.AddRange(phaseMoves);

            var endMs = buckets[index].Count == 0
                ? previousEndMs
                : ToMilliseconds(buckets[index][^1].Elapsed);
            var phase = new SmartCubeSolvePhase(
                phaseNames[phaseNames.Length - 1 - index],
                moveText,
                moveCount,
                previousEndMs,
                endMs);
            phases.Add(phase);
            if (moveCount > 0)
            {
                previousEndMs = endMs;
            }
        }

        var moveSequence = string.Join(" ", allMoves);
        var prettySolve = BuildPrettySolve(phases, moveSequence);
        return new SmartCubeSolveReconstruction(
            method,
            GetMethodId(method),
            moveSequence,
            prettySolve,
            allMoves.Count,
            phases);
    }

    private static int UpdatePhaseStatus(int status, int progress)
    {
        var nextStatus = Math.Min(progress, status);
        return nextStatus == 0 ? 1 : nextStatus;
    }

    private static int GetMethodProgress(string facelets, SmartCubeSolveMethod method)
    {
        return method == SmartCubeSolveMethod.Roux
            ? GetRouxProgress(facelets)
            : GetCfopProgress(facelets);
    }

    private static int GetCfopProgress(string facelets)
    {
        var minProgress = int.MaxValue;
        foreach (var variant in ThreeByThreeFacelets.GetOrientationVariants(facelets))
        {
            minProgress = Math.Min(minProgress, GetCfopProgressForOrientation(variant));
        }

        return minProgress == int.MaxValue ? 7 : minProgress;
    }

    private static int GetCfopProgressForOrientation(string facelets)
    {
        if (IsUnsolvedForMask(facelets, CrossMask))
        {
            return 7;
        }

        if (IsUnsolvedForMask(facelets, F2LMask))
        {
            return 2
                + (IsUnsolvedForMask(facelets, F2L1Mask) ? 1 : 0)
                + (IsUnsolvedForMask(facelets, F2L2Mask) ? 1 : 0)
                + (IsUnsolvedForMask(facelets, F2L3Mask) ? 1 : 0)
                + (IsUnsolvedForMask(facelets, F2L4Mask) ? 1 : 0);
        }

        if (IsUnsolvedForMask(facelets, OllMask))
        {
            return 2;
        }

        return IsUnsolvedForMask(facelets, SolvedMask) ? 1 : 0;
    }

    private static int GetRouxProgress(string facelets)
    {
        var minProgress = int.MaxValue;
        foreach (var variant in ThreeByThreeFacelets.GetOrientationVariants(facelets))
        {
            minProgress = Math.Min(minProgress, GetRouxProgressForOrientation(variant));
        }

        return minProgress == int.MaxValue ? 4 : minProgress;
    }

    private static int GetRouxProgressForOrientation(string facelets)
    {
        if (IsUnsolvedForMask(facelets, RouxFbMask))
        {
            return 4;
        }

        if (IsUnsolvedForMask(facelets, RouxSbMask))
        {
            return 3;
        }

        if (IsUnsolvedForMask(facelets, RouxCmllMask))
        {
            return 2;
        }

        return IsUnsolvedForMask(facelets, SolvedMask) ? 1 : 0;
    }

    private static bool IsUnsolvedForMask(string facelets, IReadOnlyList<int[]> mask)
    {
        return !IsSolvedForMask(facelets, mask);
    }

    private static bool IsSolvedForMask(string facelets, IReadOnlyList<int[]> mask)
    {
        if (!ThreeByThreeFacelets.IsValidState(facelets))
        {
            return false;
        }

        foreach (var group in mask)
        {
            if (group.Length == 0)
            {
                continue;
            }

            var color = facelets[group[0]];
            for (var index = 1; index < group.Length; index++)
            {
                if (facelets[group[index]] != color)
                {
                    return false;
                }
            }
        }

        return true;
    }

    private static IReadOnlyList<int[]> ToEqus(string facelets)
    {
        var result = new List<int[]>();
        var chars = facelets.ToCharArray();
        for (var index = 0; index < chars.Length; index++)
        {
            var color = chars[index];
            if (color == '-')
            {
                continue;
            }

            var indices = new List<int>();
            for (var next = index; next < chars.Length; next++)
            {
                if (chars[next] == color)
                {
                    indices.Add(next);
                }
            }

            if (indices.Count > 1)
            {
                result.Add(indices.ToArray());
            }

            for (var next = 0; next < chars.Length; next++)
            {
                if (chars[next] == color)
                {
                    chars[next] = '-';
                }
            }
        }

        return result;
    }

    private static IReadOnlyList<string> BuildCombinedMoves(IEnumerable<SmartCubeSolveMoveSample> moves)
    {
        var combined = new List<string>();
        foreach (var move in moves)
        {
            SmartCubeMoveNotation.AppendCombined(combined, move.Move);
        }

        return combined;
    }

    private static string BuildMoveSequence(IEnumerable<SmartCubeSolveMoveSample> moves)
    {
        return string.Join(" ", BuildCombinedMoves(moves));
    }

    private static int CountMoves(string moveSequence)
    {
        return SmartCubeMoveNotation.ParseSequence(moveSequence).Count;
    }

    private static string BuildPrettySolve(IEnumerable<SmartCubeSolvePhase> phases, string fallback)
    {
        var lines = phases
            .Where(phase => !string.IsNullOrWhiteSpace(phase.Moves))
            .Select(phase => $"{phase.Moves} // {phase.Name}")
            .ToArray();
        return lines.Length == 0 ? fallback : string.Join(Environment.NewLine, lines);
    }

    private static IReadOnlyList<SmartCubeSolvePhase> CreateEmptyPhases(IEnumerable<string> phaseNames)
    {
        return phaseNames
            .Select(name => new SmartCubeSolvePhase(name, string.Empty, 0, 0, 0))
            .ToArray();
    }

    private static string GetMethodId(SmartCubeSolveMethod method)
    {
        return method == SmartCubeSolveMethod.Roux ? "333-smart-roux" : "333-smart-cf4op";
    }

    private static int ToMilliseconds(TimeSpan value)
    {
        return (int)Math.Round(value.TotalMilliseconds, MidpointRounding.AwayFromZero);
    }
}

public sealed record SmartCubeSolvePhase(
    string Name,
    string Moves,
    int MoveCount,
    int StartMs,
    int EndMs)
{
    public int DurationMs => Math.Max(0, EndMs - StartMs);
}
