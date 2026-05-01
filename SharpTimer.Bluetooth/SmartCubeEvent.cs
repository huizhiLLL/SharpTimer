namespace SharpTimer.Bluetooth;

public abstract record SmartCubeEvent(DateTimeOffset Timestamp);

public sealed record SmartCubeMoveEvent(
    DateTimeOffset Timestamp,
    int Face,
    int Direction,
    string Move,
    DateTimeOffset? LocalTimestamp = null,
    TimeSpan? CubeTimestamp = null)
    : SmartCubeEvent(Timestamp);

public sealed record SmartCubeFaceletsEvent(DateTimeOffset Timestamp, string Facelets)
    : SmartCubeEvent(Timestamp);

public sealed record SmartCubeGyroEvent(
    DateTimeOffset Timestamp,
    SmartCubeQuaternion Quaternion,
    SmartCubeVector? Velocity = null)
    : SmartCubeEvent(Timestamp);

public sealed record SmartCubeBatteryEvent(DateTimeOffset Timestamp, int BatteryLevel)
    : SmartCubeEvent(Timestamp)
{
    public int BatteryLevel { get; init; } = Math.Clamp(BatteryLevel, 0, 100);
}

public sealed record SmartCubeHardwareEvent(
    DateTimeOffset Timestamp,
    string? HardwareName = null,
    string? SoftwareVersion = null,
    string? HardwareVersion = null,
    string? ProductDate = null,
    bool? GyroSupported = null)
    : SmartCubeEvent(Timestamp);

public sealed record SmartCubeDisconnectEvent(DateTimeOffset Timestamp)
    : SmartCubeEvent(Timestamp);

public sealed record SmartCubeQuaternion(double X, double Y, double Z, double W);

public sealed record SmartCubeVector(double X, double Y, double Z);
