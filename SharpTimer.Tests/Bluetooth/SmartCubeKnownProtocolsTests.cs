using SharpTimer.Bluetooth;

namespace SharpTimer.Tests.Bluetooth;

public sealed class SmartCubeKnownProtocolsTests
{
    [Fact]
    public void CreateDefaultRegistry_ResolvesKnownDeviceByName()
    {
        var registry = SmartCubeKnownProtocols.CreateDefaultRegistry();
        var device = new SmartCubeDeviceInfo(
            1,
            "QY-QYSC-001",
            -50,
            new HashSet<Guid>(),
            DateTimeOffset.UtcNow);

        var protocol = registry.ResolveByGatt(device);

        Assert.NotNull(protocol);
        Assert.Equal("qiyi", protocol.Info.Id);
    }

    [Fact]
    public void CreateDefaultRegistry_ResolvesKnownDeviceByService()
    {
        var registry = SmartCubeKnownProtocols.CreateDefaultRegistry();
        var device = new SmartCubeDeviceInfo(
            1,
            null,
            -50,
            new HashSet<Guid> { SmartCubeBluetoothServices.GoCubeUart },
            DateTimeOffset.UtcNow);

        var protocol = registry.ResolveByGatt(device);

        Assert.NotNull(protocol);
        Assert.Equal("gocube", protocol.Info.Id);
    }
}
