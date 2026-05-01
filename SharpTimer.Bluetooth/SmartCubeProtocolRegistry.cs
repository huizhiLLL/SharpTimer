namespace SharpTimer.Bluetooth;

public sealed class SmartCubeProtocolRegistry
{
    private readonly List<ISmartCubeProtocol> _protocols = new();

    public IReadOnlyList<ISmartCubeProtocol> Protocols => _protocols;

    public void Register(ISmartCubeProtocol protocol)
    {
        ArgumentNullException.ThrowIfNull(protocol);
        if (_protocols.Any(item => item.Info.Id == protocol.Info.Id))
        {
            throw new InvalidOperationException($"Smart cube protocol '{protocol.Info.Id}' is already registered.");
        }

        _protocols.Add(protocol);
    }

    public ISmartCubeProtocol? ResolveByGatt(SmartCubeDeviceInfo device)
    {
        ArgumentNullException.ThrowIfNull(device);

        return _protocols
            .Select(protocol => new
            {
                Protocol = protocol,
                Score = protocol.GetGattAffinity(device.ServiceUuids, device)
            })
            .Where(item => item.Score > 0 || item.Protocol.MatchesDevice(device))
            .OrderByDescending(item => item.Score)
            .ThenBy(item => item.Protocol.Info.Id, StringComparer.Ordinal)
            .Select(item => item.Protocol)
            .FirstOrDefault();
    }
}
