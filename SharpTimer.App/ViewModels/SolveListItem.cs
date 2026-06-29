using SharpTimer.Core.Models;
using Microsoft.UI.Xaml.Media;
using System;

namespace SharpTimer.App.ViewModels;

public sealed class SolveListItem
{
    public Guid Id { get; init; }

    public string Number { get; init; } = "";

    public string Time { get; init; } = "";

    public Brush TimeForeground { get; init; } = new SolidColorBrush(Microsoft.UI.Colors.Transparent);

    public string AverageOf5 { get; init; } = "";

    public Brush AverageOf5Foreground { get; init; } = new SolidColorBrush(Microsoft.UI.Colors.Transparent);

    public string AverageOf12 { get; init; } = "";

    public Brush AverageOf12Foreground { get; init; } = new SolidColorBrush(Microsoft.UI.Colors.Transparent);

    public Solve Solve { get; set; } = new();
}
