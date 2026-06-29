namespace SharpTimer.App.Services;

public sealed record AppSettings
{
    public bool UseInspection { get; init; } = true;

    public int DecimalPlaces { get; init; } = 2;

    public int ScrambleFontSize { get; init; } = 22;

    public int SmartCubePreviewSize { get; init; } = 180;

    public SmartCubeScrambleProgressStyle SmartCubeScrambleProgressStyle { get; init; } =
        SmartCubeScrambleProgressStyle.HideCompleted;

    public AppThemePreference Theme { get; init; } = AppThemePreference.Light;

    public AppBackdropMaterialPreference BackdropMaterial { get; init; } = AppBackdropMaterialPreference.Mica;

    public AppLanguagePreference Language { get; init; } = AppLanguagePreference.English;
}

public enum SmartCubeScrambleProgressStyle
{
    HideCompleted,
    DimCompleted
}
