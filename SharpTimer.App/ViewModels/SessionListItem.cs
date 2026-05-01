using System;

namespace SharpTimer.App.ViewModels;

public sealed class SessionListItem
{
    public required Guid Id { get; init; }

    public required string Name { get; init; }

    public required string Puzzle { get; init; }

    public string DisplayName => $"{Name} · {Puzzle}";
}
