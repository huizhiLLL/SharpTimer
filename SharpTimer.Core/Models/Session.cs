namespace SharpTimer.Core.Models;

public sealed record Session
{
    public Guid Id { get; init; } = Guid.NewGuid();

    public string Name { get; init; } = "Default";

    public string Puzzle { get; init; } = "333";

    public DateTimeOffset CreatedAt { get; init; } = DateTimeOffset.UtcNow;

    public DateTimeOffset UpdatedAt { get; init; } = DateTimeOffset.UtcNow;

    public bool IsArchived { get; init; }

    public int SortOrder { get; init; }
}
