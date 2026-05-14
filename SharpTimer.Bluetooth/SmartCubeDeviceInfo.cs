namespace SharpTimer.Bluetooth;

public sealed record SmartCubeDeviceInfo(
    ulong BluetoothAddress,
    string? Name,
    short RawSignalStrengthInDBm,
    IReadOnlySet<Guid> ServiceUuids,
    DateTimeOffset SeenAt,
    IReadOnlyList<byte[]>? ManufacturerData = null);

public static class SmartCubeDeviceInfoExtensions
{
    public static SmartCubeDeviceInfo MergeAdvertisement(this SmartCubeDeviceInfo current, SmartCubeDeviceInfo next)
    {
        ArgumentNullException.ThrowIfNull(current);
        ArgumentNullException.ThrowIfNull(next);

        if (current.BluetoothAddress != next.BluetoothAddress)
        {
            throw new ArgumentException("Cannot merge advertisements from different Bluetooth addresses.", nameof(next));
        }

        var services = current.ServiceUuids
            .Concat(next.ServiceUuids)
            .ToHashSet();
        var manufacturerData = (current.ManufacturerData ?? Array.Empty<byte[]>())
            .Concat(next.ManufacturerData ?? Array.Empty<byte[]>())
            .DistinctBy(Convert.ToHexString)
            .ToArray();

        return current with
        {
            Name = string.IsNullOrWhiteSpace(next.Name) ? current.Name : next.Name,
            RawSignalStrengthInDBm = next.RawSignalStrengthInDBm,
            ServiceUuids = services,
            SeenAt = next.SeenAt,
            ManufacturerData = manufacturerData.Length == 0 ? null : manufacturerData
        };
    }
}
