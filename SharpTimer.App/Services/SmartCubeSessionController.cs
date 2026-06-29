using SharpTimer.Bluetooth;
using SharpTimer.Core.SmartCubes;
using System;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.UI.Dispatching;
using Microsoft.UI.Xaml;

namespace SharpTimer.App.Services;

public sealed class SmartCubeSessionController : IAsyncDisposable
{
    private const int MaxReconnectAttempts = 3;
    private static readonly TimeSpan KeepAliveInterval = TimeSpan.FromSeconds(20);
    private static readonly TimeSpan CommandTimeout = TimeSpan.FromSeconds(8);
    private static readonly TimeSpan KeepAliveResponseTimeout = TimeSpan.FromSeconds(8);
    private static readonly TimeSpan ReconnectBaseDelay = TimeSpan.FromSeconds(2);

    private readonly DispatcherQueue? _dispatcherQueue = DispatcherQueue.GetForCurrentThread();
    private readonly DispatcherTimer _keepAliveTimer = new();
    private WindowsBleSmartCubeScanner? _scanner;
    private ISmartCubeConnection? _connection;
    private SmartCubeDeviceInfo? _lastConnectedDevice;
    private bool _manualDisconnectRequested;
    private bool _isDisposed;
    private int _connectionVersion;
    private int _keepAliveInFlight;
    private long _lastCubeEventTicks;

    public SmartCubeSessionController()
    {
        _keepAliveTimer.Interval = KeepAliveInterval;
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
        MarkConnectionAlive(DateTimeOffset.UtcNow);
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
            await SendCommandWithTimeoutAsync(connection, SmartCubeCommand.RequestFacelets);
        }
        catch
        {
            HandleConnectionLost(connection, notifyDisconnect: true);
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
        else
        {
            MarkConnectionAlive(e.Timestamp);
        }
    }

    private async void KeepAliveTimer_Tick(object? sender, object e)
    {
        if (Interlocked.CompareExchange(ref _keepAliveInFlight, 1, 0) != 0)
        {
            return;
        }

        var connection = _connection;
        if (connection is null)
        {
            Volatile.Write(ref _keepAliveInFlight, 0);
            StopKeepAliveTimer();
            return;
        }

        var lastEventTicksBeforeRequest = Interlocked.Read(ref _lastCubeEventTicks);
        try
        {
            await SendCommandWithTimeoutAsync(connection, SmartCubeCommand.RequestBattery);
            await Task.Delay(KeepAliveResponseTimeout);
            if (_connection is not null
                && ReferenceEquals(connection, _connection)
                && Interlocked.Read(ref _lastCubeEventTicks) <= lastEventTicksBeforeRequest)
            {
                HandleConnectionLost(connection, notifyDisconnect: true);
            }
        }
        catch
        {
            HandleConnectionLost(connection, notifyDisconnect: true);
        }
        finally
        {
            Volatile.Write(ref _keepAliveInFlight, 0);
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
                MarkConnectionAlive(DateTimeOffset.UtcNow);
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
        await SendCommandWithTimeoutAsync(connection, SmartCubeCommand.RequestBattery);
        await SendCommandWithTimeoutAsync(connection, SmartCubeCommand.RequestFacelets);
    }

    private static async Task SendCommandWithTimeoutAsync(ISmartCubeConnection connection, SmartCubeCommand command)
    {
        using var timeout = new CancellationTokenSource(CommandTimeout);
        await connection.SendCommandAsync(command, timeout.Token);
    }

    private void MarkConnectionAlive(DateTimeOffset timestamp)
    {
        Interlocked.Exchange(ref _lastCubeEventTicks, timestamp.UtcTicks);
    }

    private void HandleConnectionLost(ISmartCubeConnection connection, bool notifyDisconnect)
    {
        StopKeepAliveTimer();
        if (_connection is null || !ReferenceEquals(connection, _connection))
        {
            return;
        }

        _connection = null;
        _connectionVersion++;
        connection.EventReceived -= Connection_EventReceived;
        _ = connection.DisposeAsync();
        if (notifyDisconnect)
        {
            CubeEventReceived?.Invoke(this, new SmartCubeDisconnectEvent(DateTimeOffset.UtcNow));
        }

        ConnectionChanged?.Invoke(this, EventArgs.Empty);
        if (!_manualDisconnectRequested)
        {
            _ = ReconnectAsync(_connectionVersion);
        }
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
