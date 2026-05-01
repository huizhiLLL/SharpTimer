using SharpTimer.Bluetooth;
using System;

namespace SharpTimer.App.ViewModels;

public sealed class BluetoothDeviceListItem
{
    public required SmartCubeDeviceInfo Device { get; init; }

    public required string Address { get; init; }

    public required string Name { get; init; }

    public required string Signal { get; init; }

    public required string Protocol { get; init; }

    public required string Services { get; init; }

    public required string LastSeen { get; init; }
}
