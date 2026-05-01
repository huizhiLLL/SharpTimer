namespace SharpTimer.Bluetooth;

public static class SmartCubeBluetoothServices
{
    private const string BluetoothBaseUuidSuffix = "-0000-1000-8000-00805f9b34fb";

    public static readonly Guid GenericAccess = FromBluetooth16Bit(0x1800);
    public static readonly Guid DeviceInformation = FromBluetooth16Bit(0x180A);
    public static readonly Guid QiYiLikeFff0 = FromBluetooth16Bit(0xFFF0);
    public static readonly Guid MoYuPlain = FromBluetooth16Bit(0x1000);
    public static readonly Guid MoYu32 = Guid.Parse("0783b03e-7735-b5a0-1760-a305d2795cb0");
    public static readonly Guid MoYu32ReadCharacteristic = Guid.Parse("0783b03e-7735-b5a0-1760-a305d2795cb1");
    public static readonly Guid MoYu32WriteCharacteristic = Guid.Parse("0783b03e-7735-b5a0-1760-a305d2795cb2");
    public static readonly Guid GiikerData = FromBluetooth16Bit(0xAADB);
    public static readonly Guid GiikerControl = FromBluetooth16Bit(0xAAAA);
    public static readonly Guid GoCubeUart = Guid.Parse("6e400001-b5a3-f393-e0a9-e50e24dcca9e");

    public static readonly Guid GanGen1PrimaryService = FromBluetooth16Bit(0xFFF0);
    public static readonly Guid GanGen1StateCharacteristic = FromBluetooth16Bit(0xFFF5);
    public static readonly Guid GanGen1MovesCharacteristic = FromBluetooth16Bit(0xFFF6);
    public static readonly Guid GanGen1BatteryCharacteristic = FromBluetooth16Bit(0xFFF7);
    public static readonly Guid GanGen1FaceletsCharacteristic = FromBluetooth16Bit(0xFFF2);
    public static readonly Guid GanGen2Service = Guid.Parse("6e400001-b5a3-f393-e0a9-e50e24dc4179");
    public static readonly Guid GanGen2CommandCharacteristic = Guid.Parse("28be4a4a-cd67-11e9-a32f-2a2ae2dbcce4");
    public static readonly Guid GanGen2StateCharacteristic = Guid.Parse("28be4cb6-cd67-11e9-a32f-2a2ae2dbcce4");
    public static readonly Guid GanGen3Service = Guid.Parse("8653000a-43e6-47b7-9cb0-5fc21d4ae340");
    public static readonly Guid GanGen3CommandCharacteristic = Guid.Parse("8653000c-43e6-47b7-9cb0-5fc21d4ae340");
    public static readonly Guid GanGen3StateCharacteristic = Guid.Parse("8653000b-43e6-47b7-9cb0-5fc21d4ae340");
    public static readonly Guid GanGen4Service = Guid.Parse("00000010-0000-fff7-fff6-fff5fff4fff0");
    public static readonly Guid GanGen4CommandCharacteristic = Guid.Parse("0000fff5-0000-1000-8000-00805f9b34fb");
    public static readonly Guid GanGen4StateCharacteristic = Guid.Parse("0000fff6-0000-1000-8000-00805f9b34fb");

    public static IReadOnlySet<Guid> DefaultSmartCubeOptionalServices { get; } = new HashSet<Guid>
    {
        GenericAccess,
        DeviceInformation,
        GanGen1PrimaryService,
        GanGen1StateCharacteristic,
        GanGen1MovesCharacteristic,
        GanGen1BatteryCharacteristic,
        GanGen1FaceletsCharacteristic,
        GanGen2Service,
        GanGen2CommandCharacteristic,
        GanGen2StateCharacteristic,
        GanGen3Service,
        GanGen3CommandCharacteristic,
        GanGen3StateCharacteristic,
        GanGen4Service,
        GanGen4CommandCharacteristic,
        GanGen4StateCharacteristic,
        QiYiLikeFff0,
        MoYuPlain,
        MoYu32,
        GiikerData,
        GiikerControl,
        GoCubeUart
    };

    public static IReadOnlySet<Guid> MergeOptionalServices(IEnumerable<Guid> protocolServices)
    {
        ArgumentNullException.ThrowIfNull(protocolServices);

        var services = new HashSet<Guid>(DefaultSmartCubeOptionalServices);
        foreach (var service in protocolServices)
        {
            services.Add(service);
        }

        return services;
    }

    private static Guid FromBluetooth16Bit(ushort uuid)
    {
        return Guid.Parse($"0000{uuid:x4}{BluetoothBaseUuidSuffix}");
    }
}
