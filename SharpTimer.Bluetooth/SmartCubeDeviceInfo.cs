namespace SharpTimer.Bluetooth;

public sealed record SmartCubeDeviceInfo(
    ulong BluetoothAddress,
    string? Name,
    short RawSignalStrengthInDBm,
    IReadOnlySet<Guid> ServiceUuids,
    DateTimeOffset SeenAt,
    IReadOnlyList<byte[]>? ManufacturerData = null);
