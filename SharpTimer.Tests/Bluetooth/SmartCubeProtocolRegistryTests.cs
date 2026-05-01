using SharpTimer.Bluetooth;

namespace SharpTimer.Tests.Bluetooth;

public sealed class SmartCubeProtocolRegistryTests
{
    [Fact]
    public void Register_RejectsDuplicateProtocolId()
    {
        var registry = new SmartCubeProtocolRegistry();
        var protocol = new TestProtocol("gan", 1, SmartCubeBluetoothServices.GanGen3Service);

        registry.Register(protocol);

        Assert.Throws<InvalidOperationException>(() => registry.Register(protocol));
    }

    [Fact]
    public void ResolveByGatt_ReturnsHighestAffinityProtocol()
    {
        var registry = new SmartCubeProtocolRegistry();
        var weakProtocol = new TestProtocol("weak", 1, SmartCubeBluetoothServices.GanGen3Service);
        var strongProtocol = new TestProtocol("strong", 10, SmartCubeBluetoothServices.GanGen3Service);
        registry.Register(weakProtocol);
        registry.Register(strongProtocol);
        var device = CreateDevice(SmartCubeBluetoothServices.GanGen3Service);

        var resolved = registry.ResolveByGatt(device);

        Assert.Same(strongProtocol, resolved);
    }

    [Fact]
    public void ResolveByGatt_FallsBackToDeviceNameMatch()
    {
        var registry = new SmartCubeProtocolRegistry();
        var protocol = new TestProtocol(
            "named",
            0,
            SmartCubeBluetoothServices.GanGen3Service,
            new SmartCubeNameFilter(NamePrefix: "GAN"));
        registry.Register(protocol);
        var device = new SmartCubeDeviceInfo(
            1,
            "GAN12 ui",
            -40,
            new HashSet<Guid>(),
            DateTimeOffset.UtcNow);

        var resolved = registry.ResolveByGatt(device);

        Assert.Same(protocol, resolved);
    }

    private static SmartCubeDeviceInfo CreateDevice(Guid serviceUuid)
    {
        return new SmartCubeDeviceInfo(
            1,
            "Cube",
            -40,
            new HashSet<Guid> { serviceUuid },
            DateTimeOffset.UtcNow);
    }

    private sealed class TestProtocol : ISmartCubeProtocol
    {
        private readonly int _affinity;

        public TestProtocol(
            string id,
            int affinity,
            Guid serviceUuid,
            params SmartCubeNameFilter[] nameFilters)
        {
            _affinity = affinity;
            Info = new SmartCubeProtocolInfo(id, id);
            OptionalServices = new HashSet<Guid> { serviceUuid };
            NameFilters = nameFilters;
        }

        public SmartCubeProtocolInfo Info { get; }

        public IReadOnlyList<SmartCubeNameFilter> NameFilters { get; }

        public IReadOnlySet<Guid> OptionalServices { get; }

        public bool MatchesDevice(SmartCubeDeviceInfo device)
        {
            return NameFilters.Any(filter => filter.Matches(device.Name));
        }

        public int GetGattAffinity(IReadOnlySet<Guid> serviceUuids, SmartCubeDeviceInfo device)
        {
            return OptionalServices.Overlaps(serviceUuids) ? _affinity : 0;
        }
    }
}
