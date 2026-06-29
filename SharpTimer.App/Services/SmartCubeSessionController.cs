using SharpTimer.Bluetooth;
using SharpTimer.Core.SmartCubes;
using System;
using System.Threading.Tasks;
using Microsoft.UI.Dispatching;
using Microsoft.UI.Xaml;

namespace SharpTimer.App.Services;

public sealed class SmartCubeSessionController : IAsyncDisposable
{
    private const int MaxReconnectAttempts = 3;
    private static readonly TimeSpan ReconnectBaseDelay = TimeSpan.FromSeconds(2);

    private readonly DispatcherQueue? _dispatcherQueue = DispatcherQueue.GetForCurrentThread();
    private readonly DispatcherTimer _keepAliveTimer = new();
    private WindowsBleSmartCubeScanner? _scanner;
    private ISmartCubeConnection? _connection;
    private SmartCubeDeviceInfo? _lastConnectedDevice;
    private bool _manualDisconnectRequested;
    private bool _isDisposed;
    private int _connectionVersion;

    public SmartCubeSessionController()
    {
        _keepAliveTimer.Interval = TimeSpan.FromSeconds(60);
        _keepAliveTimer.Tick += KeepAliveTimer_Tick;
    }

    public event EventHandler<SmartCubeDeviceInfo>? DeviceDiscovered;
    public event EventHandler<SmartCubeEvent>? CubeEventReceived;
    public event EventHandler? ConnectionChanged;

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
        _manualDisconnectRequested = false;
        _connectionVersion++;
        var connection = await WindowsBleSmartCubeConnector.ConnectAsync(device);
        if (_connection is not null)
        {
            _connection.EventReceived -= Connection_EventReceived;
            await _connection.DisposeAsync();
        }

        _connection = connection;
        _lastConnectedDevice = device;
        _connectionVersion++;
        _connection.EventReceived += Connection_EventReceived;
        try
        {
            await RequestInitialStateAsync(_connection);
            StartKeepAliveTimer();
            ConnectionChanged?.Invoke(this, EventArgs.Empty);
        }
        catch
        {
            _connection = null;
            _connectionVersion++;
            connection.EventReceived -= Connection_EventReceived;
            await connection.DisposeAsync();
            ConnectionChanged?.Invoke(this, EventArgs.Empty);
            throw;
        }

        return _connection;
    }

    public async Task DisconnectAsync()
    {
        _manualDisconnectRequested = true;
        _connectionVersion++;
        var connection = _connection;
        if (connection is null)
        {
            return;
        }

        StopKeepAliveTimer();
        _connection = null;
        connection.EventReceived -= Connection_EventReceived;
        await connection.DisposeAsync();
        ConnectionChanged?.Invoke(this, EventArgs.Empty);
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
        _isDisposed = true;
        _connectionVersion++;
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
            StopKeepAliveTimer();
            connection.EventReceived -= Connection_EventReceived;
            _connection = null;
            _connectionVersion++;
            _ = connection.DisposeAsync();
            ConnectionChanged?.Invoke(this, EventArgs.Empty);
            if (!_manualDisconnectRequested)
            {
                _ = ReconnectAsync(_connectionVersion);
            }
        }
    }

    private async void KeepAliveTimer_Tick(object? sender, object e)
    {
        var connection = _connection;
        if (connection is null)
        {
            StopKeepAliveTimer();
            return;
        }

        try
        {
            await connection.SendCommandAsync(SmartCubeCommand.RequestBattery);
        }
        catch
        {
            // 保活写入失败，很可能底层链路已断，触发断连
            StopKeepAliveTimer();
            if (_connection is not null && ReferenceEquals(connection, _connection))
            {
                _connection = null;
                _connectionVersion++;
                connection.EventReceived -= Connection_EventReceived;
                _ = connection.DisposeAsync();
                ConnectionChanged?.Invoke(this, EventArgs.Empty);
                if (!_manualDisconnectRequested)
                {
                    _ = ReconnectAsync(_connectionVersion);
                }
            }
        }
    }

    private async Task ReconnectAsync(int expectedVersion)
    {
        var device = _lastConnectedDevice;
        if (device is null)
        {
            return;
        }

        for (var attempt = 1; attempt <= MaxReconnectAttempts; attempt++)
        {
            await Task.Delay(TimeSpan.FromTicks(ReconnectBaseDelay.Ticks * attempt));
            if (_isDisposed
                || _manualDisconnectRequested
                || _connection is not null
                || expectedVersion != _connectionVersion)
            {
                return;
            }

            ISmartCubeConnection? connection = null;
            try
            {
                connection = await WindowsBleSmartCubeConnector.ConnectAsync(device);
                if (_isDisposed
                    || _manualDisconnectRequested
                    || _connection is not null
                    || expectedVersion != _connectionVersion)
                {
                    await connection.DisposeAsync();
                    return;
                }

                _connection = connection;
                _connectionVersion++;
                connection.EventReceived += Connection_EventReceived;
                await RequestInitialStateAsync(connection);
                StartKeepAliveTimer();
                ConnectionChanged?.Invoke(this, EventArgs.Empty);
                return;
            }
            catch
            {
                if (_connection is not null && ReferenceEquals(_connection, connection))
                {
                    _connection = null;
                    _connectionVersion++;
                    connection.EventReceived -= Connection_EventReceived;
                    ConnectionChanged?.Invoke(this, EventArgs.Empty);
                }

                if (connection is not null)
                {
                    await connection.DisposeAsync();
                }
            }
        }

        StartScan();
        ConnectionChanged?.Invoke(this, EventArgs.Empty);
    }

    private static async Task RequestInitialStateAsync(ISmartCubeConnection connection)
    {
        await connection.SendCommandAsync(SmartCubeCommand.RequestBattery);
        await connection.SendCommandAsync(SmartCubeCommand.RequestFacelets);
    }

    private void StartKeepAliveTimer()
    {
        RunOnDispatcher(_keepAliveTimer.Start);
    }

    private void StopKeepAliveTimer()
    {
        RunOnDispatcher(_keepAliveTimer.Stop);
    }

    private void RunOnDispatcher(Action action)
    {
        if (_dispatcherQueue is null || _dispatcherQueue.HasThreadAccess)
        {
            action();
            return;
        }

        _dispatcherQueue.TryEnqueue(() => action());
    }
}
