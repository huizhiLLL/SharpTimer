namespace SharpTimer.Core.SmartCubes;

public sealed record SmartCubeSolveReconstruction(
    SmartCubeSolveMethod Method,
    string MethodId,
    string MoveSequence,
    string PrettySolve,
    int MoveCount,
    IReadOnlyList<SmartCubeSolvePhase> Phases)
{
    private const int SliceComboWindowMs = 100;
    private const string PrettyFaces = "URFDLBEMS";

    private static readonly string[] CfopPhaseNames = { "Cross", "F2L 1", "F2L 2", "F2L 3", "F2L 4", "OLL", "PLL" };
    private static readonly string[] RouxPhaseNames = { "FB", "SB", "CMLL", "L6E" };
    private static readonly int[][] CenterRotations =
    {
        new[] { 0, 2, 4, 3, 5, 1 },
        new[] { 5, 1, 0, 2, 4, 3 },
        new[] { 4, 0, 2, 1, 3, 5 }
    };
    private static readonly int[] SlicePowerSigns = { 1, 1, -1, -1, -1, 1 };

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
            var reconstructedMoves = ReconstructMoves(capture.Moves);
            var moves = JoinMoves(reconstructedMoves);
            return new SmartCubeSolveReconstruction(
                method,
                GetMethodId(method),
                moves,
                moves,
                CountMoves(reconstructedMoves),
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
        var allMoves = new List<PrettyMove>();
        var groupedMoves = ReconstructMoveGroups(buckets.Reverse());
        for (var index = phaseNames.Length - 1; index >= 0; index--)
        {
            var prettyMoves = groupedMoves[phaseNames.Length - 1 - index];
            var moveText = JoinMoves(prettyMoves);
            var moveCount = CountMoves(prettyMoves);
            allMoves.AddRange(prettyMoves);

            var phase = new SmartCubeSolvePhase(
                phaseNames[phaseNames.Length - 1 - index],
                moveText,
                moveCount,
                prettyMoves.Count == 0 ? 0 : prettyMoves[0].StartMs,
                prettyMoves.Count == 0 ? 0 : prettyMoves[^1].EndMs);
            phases.Add(phase);
        }

        var moveSequence = JoinMoves(allMoves);
        phases = IncludePhaseGaps(phases).ToList();
        var prettySolve = BuildPrettySolve(phases, moveSequence);
        return new SmartCubeSolveReconstruction(
            method,
            GetMethodId(method),
            moveSequence,
            prettySolve,
            CountMoves(allMoves),
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
        foreach (var variant in ThreeByThreeFacelets.GetAxisOrientationVariants(facelets))
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

    private static IReadOnlyList<PrettyMove> ReconstructMoves(IEnumerable<SmartCubeSolveMoveSample> moves)
    {
        return ReconstructMoveGroups(new[] { moves }).Single();
    }

    private static IReadOnlyList<IReadOnlyList<PrettyMove>> ReconstructMoveGroups(IEnumerable<IEnumerable<SmartCubeSolveMoveSample>> moveGroups)
    {
        var result = new List<IReadOnlyList<PrettyMove>>();
        var center = new[] { 0, 1, 2, 3, 4, 5 };
        foreach (var moveGroup in moveGroups)
        {
            var group = moveGroup.ToArray();
            var prettyMoves = new List<PrettyMove>();
            for (var index = 0; index < group.Length; index++)
            {
                var current = group[index];
                var currentMove = ToMoveParts(current.Move);
                var axis = IndexOf(center, currentMove.Face);
                if (axis < 0)
                {
                    continue;
                }

                if (index < group.Length - 1)
                {
                    var next = group[index + 1];
                    var nextMove = ToMoveParts(next.Move);
                    var axis2 = IndexOf(center, nextMove.Face);
                    var gapMs = ToMilliseconds(next.Elapsed) - ToMilliseconds(current.Elapsed);
                    if (gapMs <= SliceComboWindowMs
                        && axis2 >= 0
                        && axis != axis2
                        && axis % 3 == axis2 % 3
                        && currentMove.Power + nextMove.Power == 2)
                    {
                        var sliceAxis = axis % 3;
                        var slicePower = (currentMove.Power - 1) * SlicePowerSigns[axis] + 1;
                        PushMove(
                            prettyMoves,
                            sliceAxis + 6,
                            slicePower,
                            ToMilliseconds(current.Elapsed),
                            ToMilliseconds(next.Elapsed));

                        for (var turn = 0; turn < slicePower + 1; turn++)
                        {
                            center = RotateCenter(center, sliceAxis);
                        }

                        index++;
                        continue;
                    }
                }

                PushMove(
                    prettyMoves,
                    axis,
                    currentMove.Power,
                    ToMilliseconds(current.Elapsed),
                    ToMilliseconds(current.Elapsed));
            }

            result.Add(prettyMoves);
        }

        return result;
    }

    private static (int Face, int Power) ToMoveParts(string move)
    {
        var normalized = SmartCubeMoveNotation.Normalize(move);
        var face = "URFDLB".IndexOf(normalized[0], StringComparison.Ordinal);
        var power = normalized.Length == 1
            ? 0
            : normalized[1] == '2'
                ? 1
                : 2;
        return (face, power);
    }

    private static void PushMove(List<PrettyMove> moves, int axis, int power, int startMs, int endMs)
    {
        if (moves.Count == 0 || moves[^1].Axis != axis)
        {
            moves.Add(new PrettyMove(axis, power, startMs, endMs));
            return;
        }

        var last = moves[^1];
        var mergedPower = (power + last.Power + 1) % 4;
        if (mergedPower == 3)
        {
            moves.RemoveAt(moves.Count - 1);
            return;
        }

        moves[^1] = last with
        {
            Power = mergedPower,
            EndMs = endMs
        };
    }

    private static int[] RotateCenter(int[] center, int axis)
    {
        var rotation = CenterRotations[axis];
        var rotated = new int[6];
        for (var index = 0; index < rotated.Length; index++)
        {
            rotated[index] = center[rotation[index]];
        }

        return rotated;
    }

    private static int IndexOf(IReadOnlyList<int> values, int target)
    {
        for (var index = 0; index < values.Count; index++)
        {
            if (values[index] == target)
            {
                return index;
            }
        }

        return -1;
    }

    private static string JoinMoves(IReadOnlyList<PrettyMove> moves)
    {
        return string.Join(" ", moves.Select(move => move.Notation));
    }

    private static int CountMoves(IEnumerable<PrettyMove> moves)
    {
        return moves.Count(move => !move.IsRotation);
    }

    private static string BuildPrettySolve(IEnumerable<SmartCubeSolvePhase> phases, string fallback)
    {
        var lines = phases
            .Where(phase => !string.IsNullOrWhiteSpace(phase.Moves))
            .Select(phase => $"{phase.Moves} // {phase.Name}")
            .ToArray();
        return lines.Length == 0 ? fallback : string.Join(Environment.NewLine, lines);
    }

    private static IEnumerable<SmartCubeSolvePhase> IncludePhaseGaps(IEnumerable<SmartCubeSolvePhase> phases)
    {
        var previousEndMs = 0;
        foreach (var phase in phases)
        {
            if (phase.MoveCount <= 0)
            {
                yield return phase;
                continue;
            }

            yield return phase with { StartMs = previousEndMs };
            previousEndMs = phase.EndMs;
        }
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

    private sealed record PrettyMove(int Axis, int Power, int StartMs, int EndMs)
    {
        public string Notation
        {
            get
            {
                var suffix = Power switch
                {
                    1 => "2",
                    2 => "'",
                    _ => string.Empty
                };
                return PrettyFaces[Axis] + suffix;
            }
        }

        public bool IsRotation => false;
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
