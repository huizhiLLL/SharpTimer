namespace SharpTimer.Core.Models;

public sealed record Solve
{
    public Guid Id { get; init; } = Guid.NewGuid();

    public Guid SessionId { get; init; }

    public TimeSpan Duration { get; init; }

    public SolveSource Source { get; init; } = SolveSource.Manual;

    public DateTimeOffset CreatedAt { get; init; } = DateTimeOffset.UtcNow;

    public string? Scramble { get; init; }

    public string? Comment { get; init; }

    public string? MoveSequence { get; init; }

    public int? MoveCount { get; init; }

    public double? Tps { get; init; }

    public string? ReconstructionMethod { get; init; }

    public string? SolveMetaJson { get; init; }
}
