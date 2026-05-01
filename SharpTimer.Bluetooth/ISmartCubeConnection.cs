namespace SharpTimer.Bluetooth;

public interface ISmartCubeConnection : IAsyncDisposable
{
    string DeviceName { get; }

    string? DeviceMac { get; }

    SmartCubeProtocolInfo Protocol { get; }

    SmartCubeCapabilities Capabilities { get; }

    event EventHandler<SmartCubeEvent>? EventReceived;

    Task SendCommandAsync(SmartCubeCommand command, CancellationToken cancellationToken = default);

    Task DisconnectAsync(CancellationToken cancellationToken = default);
}
