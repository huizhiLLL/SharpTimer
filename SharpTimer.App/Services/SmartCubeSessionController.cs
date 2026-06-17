using SharpTimer.Bluetooth;
using SharpTimer.Core.SmartCubes;
using System;
using System.Threading.Tasks;
using Microsoft.UI.Xaml;

namespace SharpTimer.App.Services;

public sealed class SmartCubeSessionController : IAsyncDisposable
{
    private readonly DispatcherTimer _keepAliveTimer = new();
    private WindowsBleSmartCubeScanner? _scanner;
    private ISmartCubeConnection? _connection;

    public SmartCubeSessionController()
    {
        _keepAliveTimer.Interval = TimeSpan.FromSeconds(60);
        _keepAliveTimer.Tick += KeepAliveTimer_Tick;
    }

    public event EventHandler<SmartCubeDeviceInfo>? DeviceDiscovered;
    public event EventHandler<SmartCubeEvent>? CubeEventReceived;

    public ISmartCubeConnection? Connection => _connection;

    public void StartScan()
    {
        _scanner ??= CreateScanner();
        _scanner.Start();
    }

    public void StopScan()
    {
        _scanner?.Stop();
    }

    public async Task<ISmartCubeConnection> ConnectAsync(SmartCubeDeviceInfo device)
    {
        StopScan();
        var connection = await WindowsBleSmartCubeConnector.ConnectAsync(device);
        if (_connection is not null)
        {
            _connection.EventReceived -= Connection_EventReceived;
            await _connection.DisposeAsync();
        }

        _connection = connection;
        _connection.EventReceived += Connection_EventReceived;
        _keepAliveTimer.Start();
        await _connection.SendCommandAsync(SmartCubeCommand.RequestBattery);
        await _connection.SendCommandAsync(SmartCubeCommand.RequestFacelets);
        return _connection;
    }

    public async Task DisconnectAsync()
    {
        var connection = _connection;
        if (connection is null)
        {
            return;
        }

        _keepAliveTimer.Stop();
        _connection = null;
        connection.EventReceived -= Connection_EventReceived;
        await connection.DisposeAsync();
    }

    public async Task RequestFaceletsAsync()
    {
        var connection = _connection;
        if (connection is null)
        {
            return;
        }

        try
        {
            await connection.SendCommandAsync(SmartCubeCommand.RequestFacelets);
        }
        catch
        {
        }
    }

    public async ValueTask DisposeAsync()
    {
        StopScan();
        _scanner?.Dispose();
        _scanner = null;
        await DisconnectAsync();
        _keepAliveTimer.Tick -= KeepAliveTimer_Tick;
    }

    private WindowsBleSmartCubeScanner CreateScanner()
    {
        var scanner = new WindowsBleSmartCubeScanner();
        scanner.DeviceDiscovered += Scanner_DeviceDiscovered;
        return scanner;
    }

    private void Scanner_DeviceDiscovered(object? sender, SmartCubeDeviceInfo device)
    {
        DeviceDiscovered?.Invoke(this, device);
    }

    private void Connection_EventReceived(object? sender, SmartCubeEvent e)
    {
        if (_connection is not { } connection || !ReferenceEquals(sender, connection))
        {
            return;
        }

        CubeEventReceived?.Invoke(this, e);
        if (e is SmartCubeDisconnectEvent)
        {
            _keepAliveTimer.Stop();
            connection.EventReceived -= Connection_EventReceived;
            _connection = null;
        }
    }

    private async void KeepAliveTimer_Tick(object? sender, object e)
    {
        var connection = _connection;
        if (connection is null)
        {
            _keepAliveTimer.Stop();
            return;
        }

        try
        {
            await connection.SendCommandAsync(SmartCubeCommand.RequestBattery);
        }
        catch
        {
        }
    }
}
