using SharpTimer.Bluetooth;

namespace SharpTimer.Tests.Bluetooth;

public sealed class SmartCubeBluetoothServicesTests
{
    [Fact]
    public void DefaultSmartCubeOptionalServices_IncludeKnownVendorServices()
    {
        var services = SmartCubeBluetoothServices.DefaultSmartCubeOptionalServices;

        Assert.Contains(SmartCubeBluetoothServices.GanGen2Service, services);
        Assert.Contains(SmartCubeBluetoothServices.GanGen3Service, services);
        Assert.Contains(SmartCubeBluetoothServices.GanGen4Service, services);
        Assert.Contains(SmartCubeBluetoothServices.GanGen1PrimaryService, services);
        Assert.Contains(SmartCubeBluetoothServices.QiYiLikeFff0, services);
        Assert.Contains(SmartCubeBluetoothServices.MoYu32, services);
        Assert.Contains(SmartCubeBluetoothServices.GiikerData, services);
        Assert.Contains(SmartCubeBluetoothServices.GoCubeUart, services);
    }

    [Fact]
    public void MergeOptionalServices_AddsProtocolServicesWithoutDuplicates()
    {
        var custom = Guid.Parse("11111111-2222-3333-4444-555555555555");

        var services = SmartCubeBluetoothServices.MergeOptionalServices(new[]
        {
            SmartCubeBluetoothServices.GanGen2Service,
            custom
        });

        Assert.Contains(custom, services);
        Assert.Equal(services.Count, services.Distinct().Count());
    }
}
