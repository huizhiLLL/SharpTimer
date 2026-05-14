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

    [Fact]
    public void MergeAdvertisement_PreservesPreviouslySeenServices()
    {
        var first = new SmartCubeDeviceInfo(
            1,
            "GANicE2_3835",
            -50,
            new HashSet<Guid> { SmartCubeBluetoothServices.GanGen3Service },
            DateTimeOffset.UtcNow);
        var second = new SmartCubeDeviceInfo(
            1,
            "GANicE2_3835",
            -48,
            new HashSet<Guid>(),
            DateTimeOffset.UtcNow.AddSeconds(1));

        var merged = first.MergeAdvertisement(second);

        Assert.Contains(SmartCubeBluetoothServices.GanGen3Service, merged.ServiceUuids);
        Assert.Equal(-48, merged.RawSignalStrengthInDBm);
    }
}
