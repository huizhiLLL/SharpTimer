using SharpTimer.App.ViewModels;
using SharpTimer.Bluetooth;
using System;
using System.Linq;

namespace SharpTimer.App.Services;

public sealed class BluetoothDeviceListItemFactory
{
    private readonly SmartCubeProtocolRegistry _protocolRegistry;

    public BluetoothDeviceListItemFactory(SmartCubeProtocolRegistry protocolRegistry)
    {
        _protocolRegistry = protocolRegistry;
    }

    public BluetoothDeviceListItem Create(SmartCubeDeviceInfo device, LocalizedStrings strings)
    {
        var protocol = _protocolRegistry.ResolveByGatt(device);
        var services = device.ServiceUuids.Count == 0
            ? strings.BluetoothNoServices
            : string.Join(", ", device.ServiceUuids.Take(3).Select(FormatUuid));
        if (device.ServiceUuids.Count > 3)
        {
            services = string.Format(strings.BluetoothServicesSummaryFormat, device.ServiceUuids.Count);
        }

        return new BluetoothDeviceListItem
        {
            Device = device,
            Address = FormatBluetoothAddress(device.BluetoothAddress),
            Name = string.IsNullOrWhiteSpace(device.Name) ? strings.BluetoothUnknownDevice : device.Name,
            Protocol = protocol?.Info.Name ?? strings.BluetoothUnknownProtocol,
            Services = services,
            LastSeen = device.SeenAt.ToLocalTime().ToString("HH:mm:ss")
        };
    }

    private static string FormatBluetoothAddress(ulong address)
    {
        var text = address.ToString("X12");
        return string.Join(":", Enumerable.Range(0, 6).Select(index => text.Substring(index * 2, 2)));
    }

    private static string FormatUuid(Guid uuid)
    {
        return uuid.ToString("D");
    }
}
