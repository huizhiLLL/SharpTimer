namespace SharpTimer.Bluetooth;

public interface ISmartCubeProtocol
{
    SmartCubeProtocolInfo Info { get; }

    IReadOnlyList<SmartCubeNameFilter> NameFilters { get; }

    IReadOnlySet<Guid> OptionalServices { get; }

    bool MatchesDevice(SmartCubeDeviceInfo device);

    int GetGattAffinity(IReadOnlySet<Guid> serviceUuids, SmartCubeDeviceInfo device);
}
