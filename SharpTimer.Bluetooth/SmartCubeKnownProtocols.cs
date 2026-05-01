namespace SharpTimer.Bluetooth;

public static class SmartCubeKnownProtocols
{
    public static SmartCubeProtocolRegistry CreateDefaultRegistry()
    {
        var registry = new SmartCubeProtocolRegistry();
        foreach (var protocol in CreateDefaultProtocols())
        {
            registry.Register(protocol);
        }

        return registry;
    }

    public static IReadOnlyList<ISmartCubeProtocol> CreateDefaultProtocols()
    {
        return new ISmartCubeProtocol[]
        {
            new BasicSmartCubeProtocol(
                new SmartCubeProtocolInfo("gan", "GAN"),
                new[]
                {
                    new SmartCubeNameFilter(NamePrefix: "GAN"),
                    new SmartCubeNameFilter(NamePrefix: "MG"),
                    new SmartCubeNameFilter(NamePrefix: "AiCube")
                },
                new[]
                {
                    SmartCubeBluetoothServices.DeviceInformation,
                    SmartCubeBluetoothServices.GanGen2Service,
                    SmartCubeBluetoothServices.GanGen3Service,
                    SmartCubeBluetoothServices.GanGen4Service
                },
                120),
            new BasicSmartCubeProtocol(
                new SmartCubeProtocolInfo("giiker", "Giiker"),
                new[]
                {
                    new SmartCubeNameFilter(NamePrefix: "Gi"),
                    new SmartCubeNameFilter(NamePrefix: "Mi Smart Magic Cube"),
                    new SmartCubeNameFilter(NamePrefix: "Hi-")
                },
                new[]
                {
                    SmartCubeBluetoothServices.GiikerData,
                    SmartCubeBluetoothServices.GiikerControl
                },
                115),
            new BasicSmartCubeProtocol(
                new SmartCubeProtocolInfo("gocube", "GoCube"),
                new[]
                {
                    new SmartCubeNameFilter(NamePrefix: "GoCube_"),
                    new SmartCubeNameFilter(NamePrefix: "GoCube"),
                    new SmartCubeNameFilter(NamePrefix: "Rubiks")
                },
                new[] { SmartCubeBluetoothServices.GoCubeUart },
                110),
            new BasicSmartCubeProtocol(
                new SmartCubeProtocolInfo("moyu-mhc", "MoYu MHC"),
                new[] { new SmartCubeNameFilter(NamePrefix: "MHC") },
                new[] { SmartCubeBluetoothServices.MoYuPlain },
                110),
            new BasicSmartCubeProtocol(
                new SmartCubeProtocolInfo("moyu32", "MoYu32"),
                new[]
                {
                    new SmartCubeNameFilter(NamePrefix: "^S"),
                    new SmartCubeNameFilter(NamePrefix: "WCU_"),
                    new SmartCubeNameFilter(NamePrefix: "WCU_MY3")
                },
                new[] { SmartCubeBluetoothServices.MoYu32 },
                110),
            new BasicSmartCubeProtocol(
                new SmartCubeProtocolInfo("qiyi", "QiYi"),
                new[]
                {
                    new SmartCubeNameFilter(NamePrefix: "QY-QYSC"),
                    new SmartCubeNameFilter(NamePrefix: "XMD-TornadoV4-i")
                },
                new[] { SmartCubeBluetoothServices.QiYiLikeFff0 },
                110)
        };
    }

    private sealed class BasicSmartCubeProtocol : ISmartCubeProtocol
    {
        private readonly int _serviceAffinity;

        public BasicSmartCubeProtocol(
            SmartCubeProtocolInfo info,
            IReadOnlyList<SmartCubeNameFilter> nameFilters,
            IEnumerable<Guid> optionalServices,
            int serviceAffinity)
        {
            Info = info;
            NameFilters = nameFilters;
            OptionalServices = optionalServices.ToHashSet();
            _serviceAffinity = serviceAffinity;
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
            return OptionalServices.Overlaps(serviceUuids) ? _serviceAffinity : 0;
        }
    }
}
