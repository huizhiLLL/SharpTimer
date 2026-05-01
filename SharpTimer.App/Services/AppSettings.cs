namespace SharpTimer.App.Services;

public sealed record AppSettings
{
    public bool UseInspection { get; init; } = true;

    public int DecimalPlaces { get; init; } = 2;

    public AppThemePreference Theme { get; init; } = AppThemePreference.System;
}
