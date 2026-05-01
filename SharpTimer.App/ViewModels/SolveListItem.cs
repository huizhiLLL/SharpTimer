using SharpTimer.Core.Models;
using System;

namespace SharpTimer.App.ViewModels;

public sealed class SolveListItem
{
    public required Guid Id { get; init; }

    public required string Number { get; init; }

    public required string Time { get; init; }

    public required string Penalty { get; init; }

    public required string CreatedAt { get; init; }

    public required Solve Solve { get; init; }
}
