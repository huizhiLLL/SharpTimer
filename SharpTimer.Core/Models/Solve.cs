namespace SharpTimer.Core.Models;

public sealed record Solve
{
    public Guid Id { get; init; } = Guid.NewGuid();

    public Guid SessionId { get; init; }

    public TimeSpan Duration { get; init; }

    public Penalty Penalty { get; init; } = Penalty.None;

    public DateTimeOffset CreatedAt { get; init; } = DateTimeOffset.UtcNow;

    public string? Scramble { get; init; }

    public string? Comment { get; init; }

    public TimeSpan? EffectiveDuration => Penalty switch
    {
        Penalty.None => Duration,
        Penalty.PlusTwo => Duration + TimeSpan.FromSeconds(2),
        Penalty.Dnf => null,
        _ => throw new ArgumentOutOfRangeException(nameof(Penalty), Penalty, "Unknown penalty.")
    };
}
