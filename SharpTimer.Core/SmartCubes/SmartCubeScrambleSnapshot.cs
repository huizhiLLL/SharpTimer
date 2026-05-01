namespace SharpTimer.Core.SmartCubes;

public sealed record SmartCubeScrambleSnapshot(
    SmartCubeScrambleStatus Status,
    int Progress,
    int Total,
    string? PendingMove,
    IReadOnlyList<string> RemainingMoves,
    IReadOnlyList<string> CorrectionMoves,
    string? CurrentFacelets)
{
    public bool IsReady => Status == SmartCubeScrambleStatus.Ready;

    public static SmartCubeScrambleSnapshot Unavailable { get; } = new(
        SmartCubeScrambleStatus.Unavailable,
        0,
        0,
        null,
        Array.Empty<string>(),
        Array.Empty<string>(),
        null);
}
