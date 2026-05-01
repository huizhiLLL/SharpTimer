using Windows.Devices.Bluetooth.Advertisement;

namespace SharpTimer.Bluetooth;

public sealed class WindowsBleSmartCubeScanner : IDisposable
{
    private readonly BluetoothLEAdvertisementWatcher _watcher;
    private bool _disposed;

    public WindowsBleSmartCubeScanner()
    {
        _watcher = new BluetoothLEAdvertisementWatcher
        {
            ScanningMode = BluetoothLEScanningMode.Active
        };
        _watcher.Received += Watcher_Received;
    }

    public event EventHandler<SmartCubeDeviceInfo>? DeviceDiscovered;

    public void Start()
    {
        ObjectDisposedException.ThrowIf(_disposed, this);
        _watcher.Start();
    }

    public void Stop()
    {
        ObjectDisposedException.ThrowIf(_disposed, this);
        _watcher.Stop();
    }

    public void Dispose()
    {
        if (_disposed)
        {
            return;
        }

        _watcher.Stop();
        _watcher.Received -= Watcher_Received;
        _disposed = true;
    }

    private void Watcher_Received(BluetoothLEAdvertisementWatcher sender, BluetoothLEAdvertisementReceivedEventArgs args)
    {
        var serviceUuids = args.Advertisement.ServiceUuids
            .Select(uuid => uuid)
            .ToHashSet();
        var manufacturerData = args.Advertisement.ManufacturerData
            .Select(item =>
            {
                var data = new byte[item.Data.Length + 2];
                data[0] = (byte)(item.CompanyId & 0xFF);
                data[1] = (byte)(item.CompanyId >> 8);
                using var reader = Windows.Storage.Streams.DataReader.FromBuffer(item.Data);
                var payload = new byte[item.Data.Length];
                reader.ReadBytes(payload);
                payload.CopyTo(data, 2);
                return data;
            })
            .ToArray();

        DeviceDiscovered?.Invoke(
            this,
            new SmartCubeDeviceInfo(
                args.BluetoothAddress,
                args.Advertisement.LocalName,
                args.RawSignalStrengthInDBm,
                serviceUuids,
                args.Timestamp,
                manufacturerData));
    }
}
