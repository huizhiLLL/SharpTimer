namespace SharpTimer.Core.Timer;

public sealed record ManualTimerOptions
{
    public TimeSpan InspectionLimit { get; init; } = TimeSpan.FromSeconds(15);

    public TimeSpan DnfInspectionLimit { get; init; } = TimeSpan.FromSeconds(17);

    public bool UseInspection { get; init; } = true;
}
