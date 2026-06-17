using System;

namespace SharpTimer.App.ViewModels;

public sealed class SessionListItem
{
    public Guid Id { get; init; }

    public string Name { get; init; } = "";

    public string Puzzle { get; init; } = "";

    public string DisplayName => $"{Name} · {Puzzle}";
}
