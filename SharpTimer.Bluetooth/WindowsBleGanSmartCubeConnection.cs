using Windows.Devices.Bluetooth;
using Windows.Devices.Bluetooth.GenericAttributeProfile;
using Windows.Security.Cryptography;

namespace SharpTimer.Bluetooth;

internal sealed class WindowsBleGanSmartCubeConnection : ISmartCubeConnection
{
    private static readonly byte[] DefaultKey = { 0x01, 0x02, 0x42, 0x28, 0x31, 0x91, 0x16, 0x07, 0x20, 0x05, 0x18, 0x54, 0x42, 0x11, 0x12, 0x53 };
    private static readonly byte[] DefaultIv = { 0x11, 0x03, 0x32, 0x28, 0x21, 0x01, 0x76, 0x27, 0x20, 0x95, 0x78, 0x14, 0x32, 0x12, 0x02, 0x43 };
    private static readonly byte[] AiCubeKey = { 0x05, 0x12, 0x02, 0x45, 0x02, 0x01, 0x29, 0x56, 0x12, 0x78, 0x12, 0x76, 0x81, 0x01, 0x08, 0x03 };
    private static readonly byte[] AiCubeIv = { 0x01, 0x44, 0x28, 0x06, 0x86, 0x21, 0x22, 0x28, 0x51, 0x05, 0x08, 0x31, 0x82, 0x02, 0x21, 0x06 };

    private readonly BluetoothLEDevice _device;
    private readonly SmartCubeDeviceInfo _advertisedDevice;
    private readonly GanGeneration _generation;
    private readonly object _lifetimeLock = new();
    private readonly byte[] _key;
    private readonly byte[] _iv;
    private GattDeviceService? _service;
    private GattCharacteristic? _readCharacteristic;
    private GattCharacteristic? _writeCharacteristic;
    private int _lastMoveCount = -1;
    private long _cubeTimestamp;
    private bool _isDisconnecting;
    private bool _isDisposed;

    private WindowsBleGanSmartCubeConnection(
        BluetoothLEDevice device,
        SmartCubeDeviceInfo advertisedDevice,
        GanGeneration generation)
    {
        _device = device;
        _advertisedDevice = advertisedDevice;
        _generation = generation;
        DeviceName = !string.IsNullOrWhiteSpace(device.Name) ? device.Name : advertisedDevice.Name ?? "GAN";
        DeviceMac = ResolveMac(advertisedDevice, device.BluetoothAddress);
        Protocol = new SmartCubeProtocolInfo("gan", $"GAN {generation}");
        Capabilities = new SmartCubeCapabilities(
            Gyroscope: generation == GanGeneration.Gen2,
            Battery: true,
            Facelets: true,
            Hardware: true,
            Reset: true);

        var baseKey = DeviceName.StartsWith("AiCube", StringComparison.OrdinalIgnoreCase) && generation == GanGeneration.Gen2
            ? AiCubeKey
            : DefaultKey;
        var baseIv = DeviceName.StartsWith("AiCube", StringComparison.OrdinalIgnoreCase) && generation == GanGeneration.Gen2
            ? AiCubeIv
            : DefaultIv;
        (_key, _iv) = CreateKeyAndIv(baseKey, baseIv, SmartCubeBluetoothAddress.Parse(DeviceMac));
    }

    public string DeviceName { get; }

    public string? DeviceMac { get; }

    public SmartCubeProtocolInfo Protocol { get; }

    public SmartCubeCapabilities Capabilities { get; }

    public event EventHandler<SmartCubeEvent>? EventReceived;

    public static WindowsBleGanSmartCubeConnection Create(BluetoothLEDevice device, SmartCubeDeviceInfo advertisedDevice)
    {
        var services = advertisedDevice.ServiceUuids;
        var generation = services.Contains(SmartCubeBluetoothServices.GanGen4Service)
            ? GanGeneration.Gen4
            : services.Contains(SmartCubeBluetoothServices.GanGen3Service)
                ? GanGeneration.Gen3
                : GanGeneration.Gen2;
        return new WindowsBleGanSmartCubeConnection(device, advertisedDevice, generation);
    }

    public async Task InitializeAsync(CancellationToken cancellationToken)
    {
        var serviceUuid = _generation switch
        {
            GanGeneration.Gen4 => SmartCubeBluetoothServices.GanGen4Service,
            GanGeneration.Gen3 => SmartCubeBluetoothServices.GanGen3Service,
            _ => SmartCubeBluetoothServices.GanGen2Service
        };

        var servicesResult = await _device.GetGattServicesForUuidAsync(serviceUuid, BluetoothCacheMode.Uncached)
            .AsTask(cancellationToken);
        if (servicesResult.Status != GattCommunicationStatus.Success || servicesResult.Services.Count == 0)
        {
            throw new InvalidOperationException($"找不到 GAN {_generation} GATT 服务。");
        }

        _service = servicesResult.Services[0];
        var characteristicsResult = await _service.GetCharacteristicsAsync(BluetoothCacheMode.Uncached)
            .AsTask(cancellationToken);
        if (characteristicsResult.Status != GattCommunicationStatus.Success)
        {
            throw new InvalidOperationException("无法读取 GAN GATT 特征。");
        }

        var readUuid = _generation switch
        {
            GanGeneration.Gen4 => SmartCubeBluetoothServices.GanGen4StateCharacteristic,
            GanGeneration.Gen3 => SmartCubeBluetoothServices.GanGen3StateCharacteristic,
            _ => SmartCubeBluetoothServices.GanGen2StateCharacteristic
        };
        var writeUuid = _generation switch
        {
            GanGeneration.Gen4 => SmartCubeBluetoothServices.GanGen4CommandCharacteristic,
            GanGeneration.Gen3 => SmartCubeBluetoothServices.GanGen3CommandCharacteristic,
            _ => SmartCubeBluetoothServices.GanGen2CommandCharacteristic
        };

        _readCharacteristic = characteristicsResult.Characteristics.FirstOrDefault(item => item.Uuid == readUuid)
            ?? throw new InvalidOperationException("找不到 GAN 通知特征。");
        _writeCharacteristic = characteristicsResult.Characteristics.FirstOrDefault(item => item.Uuid == writeUuid)
            ?? throw new InvalidOperationException("找不到 GAN 写入特征。");

        _readCharacteristic.ValueChanged += ReadCharacteristic_ValueChanged;
        var status = await _readCharacteristic.WriteClientCharacteristicConfigurationDescriptorAsync(
                GattClientCharacteristicConfigurationDescriptorValue.Notify)
            .AsTask(cancellationToken);
        if (status != GattCommunicationStatus.Success)
        {
            throw new InvalidOperationException("无法订阅 GAN 通知。");
        }

        await SendCommandAsync(SmartCubeCommand.RequestHardware, cancellationToken);
        await SendCommandAsync(SmartCubeCommand.RequestFacelets, cancellationToken);
        await SendCommandAsync(SmartCubeCommand.RequestBattery, cancellationToken);
    }

    public Task SendCommandAsync(SmartCubeCommand command, CancellationToken cancellationToken = default)
    {
        var payload = CreateCommandPayload(command);
        return payload is null ? Task.CompletedTask : SendRequestAsync(payload, cancellationToken);
    }

    public async Task DisconnectAsync(CancellationToken cancellationToken = default)
    {
        GattCharacteristic? readCharacteristic;
        GattDeviceService? service;
        lock (_lifetimeLock)
        {
            if (_isDisconnecting || _isDisposed)
            {
                return;
            }

            _isDisconnecting = true;
            readCharacteristic = _readCharacteristic;
            service = _service;
            _readCharacteristic = null;
            _writeCharacteristic = null;
            _service = null;
        }

        if (readCharacteristic is not null)
        {
            readCharacteristic.ValueChanged -= ReadCharacteristic_ValueChanged;
            try
            {
                await readCharacteristic.WriteClientCharacteristicConfigurationDescriptorAsync(
                        GattClientCharacteristicConfigurationDescriptorValue.None)
                    .AsTask(cancellationToken);
            }
            catch
            {
            }
        }

        service?.Dispose();
        _device.Dispose();
        lock (_lifetimeLock)
        {
            _isDisposed = true;
        }

        EventReceived?.Invoke(this, new SmartCubeDisconnectEvent(DateTimeOffset.UtcNow));
    }

    public async ValueTask DisposeAsync()
    {
        await DisconnectAsync();
    }

    private async Task SendRequestAsync(byte[] payload, CancellationToken cancellationToken)
    {
        var writeCharacteristic = _writeCharacteristic;
        if (IsConnectionClosing() || writeCharacteristic is null)
        {
            return;
        }

        var encrypted = SmartCubeCrypto.TransformAesCbcBlocks(payload, encrypt: true, _key, _iv);
        await writeCharacteristic.WriteValueAsync(CryptographicBuffer.CreateFromByteArray(encrypted))
            .AsTask(cancellationToken);
    }

    private byte[]? CreateCommandPayload(SmartCubeCommand command)
    {
        return _generation switch
        {
            GanGeneration.Gen2 => command switch
            {
                SmartCubeCommand.RequestFacelets => CreatePayload(20, 0x04),
                SmartCubeCommand.RequestHardware => CreatePayload(20, 0x05),
                SmartCubeCommand.RequestBattery => CreatePayload(20, 0x09),
                _ => null
            },
            GanGeneration.Gen3 => command switch
            {
                SmartCubeCommand.RequestFacelets => CreatePayload(16, 0x68, 0x01),
                SmartCubeCommand.RequestHardware => CreatePayload(16, 0x68, 0x04),
                SmartCubeCommand.RequestBattery => CreatePayload(16, 0x68, 0x07),
                _ => null
            },
            _ => command switch
            {
                SmartCubeCommand.RequestFacelets => CreatePayload(20, 0xDD, 0x04, 0x00, 0xED),
                SmartCubeCommand.RequestHardware => CreatePayload(20, 0xDF, 0x03),
                SmartCubeCommand.RequestBattery => CreatePayload(20, 0xDD, 0x04, 0x00, 0xEF),
                _ => null
            }
        };
    }

    private static byte[] CreatePayload(int length, params byte[] prefix)
    {
        var payload = new byte[length];
        prefix.CopyTo(payload, 0);
        return payload;
    }

    private void ReadCharacteristic_ValueChanged(GattCharacteristic sender, GattValueChangedEventArgs args)
    {
        if (IsConnectionClosing())
        {
            return;
        }

        var timestamp = DateTimeOffset.UtcNow;
        try
        {
            CryptographicBuffer.CopyToByteArray(args.CharacteristicValue, out var encrypted);
            if (encrypted.Length < 16)
            {
                return;
            }

            var decoded = SmartCubeCrypto.TransformAesCbcBlocks(encrypted, encrypt: false, _key, _iv);
            foreach (var smartCubeEvent in Parse(decoded, timestamp))
            {
                EventReceived?.Invoke(this, smartCubeEvent);
            }
        }
        catch
        {
        }
    }

    private IEnumerable<SmartCubeEvent> Parse(byte[] decoded, DateTimeOffset timestamp)
    {
        return _generation switch
        {
            GanGeneration.Gen2 => ParseGen2(decoded, timestamp),
            GanGeneration.Gen3 => ParseGen3(decoded, timestamp),
            _ => ParseGen4(decoded, timestamp)
        };
    }

    private IEnumerable<SmartCubeEvent> ParseGen2(byte[] decoded, DateTimeOffset timestamp)
    {
        var reader = new GanBitReader(decoded);
        var type = reader.Get(0, 4);
        if (type == 0x02)
        {
            var serial = reader.Get(4, 8);
            foreach (var item in ParseGanMoveBlock(reader, serial, 12, 47, 7, timestamp))
            {
                yield return item;
            }
        }
        else if (type == 0x04)
        {
            var serial = reader.Get(4, 8);
            if (_lastMoveCount == -1)
            {
                _lastMoveCount = serial;
            }

            yield return new SmartCubeFaceletsEvent(timestamp, ParseGanFacelets(reader, 12, 33, 47, 91));
        }
        else if (type == 0x05)
        {
            yield return new SmartCubeHardwareEvent(
                timestamp,
                HardwareName: ReadAsciiFromBits(reader, 40, 8),
                HardwareVersion: $"{reader.Get(8, 8)}.{reader.Get(16, 8)}",
                SoftwareVersion: $"{reader.Get(24, 8)}.{reader.Get(32, 8)}",
                GyroSupported: reader.Get(104, 1) == 1);
        }
        else if (type == 0x09)
        {
            yield return new SmartCubeBatteryEvent(timestamp, reader.Get(8, 8));
        }
    }

    private IEnumerable<SmartCubeEvent> ParseGen3(byte[] decoded, DateTimeOffset timestamp)
    {
        var reader = new GanBitReader(decoded);
        if (reader.Get(0, 8) != 0x55 || reader.Get(16, 8) <= 0)
        {
            yield break;
        }

        var type = reader.Get(8, 8);
        if (type == 0x01)
        {
            var serial = reader.Get(56, 16, littleEndian: true);
            var direction = reader.Get(72, 2);
            var face = Array.IndexOf(new[] { 2, 32, 8, 1, 16, 4 }, reader.Get(74, 6));
            if (face >= 0 && _lastMoveCount != -1 && serial != _lastMoveCount)
            {
                _lastMoveCount = serial;
                yield return CreateMoveEvent(timestamp, face, direction, reader.Get(24, 32, littleEndian: true));
            }
        }
        else if (type == 0x02)
        {
            var serial = reader.Get(24, 16, littleEndian: true);
            if (_lastMoveCount == -1)
            {
                _lastMoveCount = serial;
            }

            yield return new SmartCubeFaceletsEvent(timestamp, ParseGanFacelets(reader, 40, 61, 77, 121));
        }
        else if (type == 0x07)
        {
            yield return new SmartCubeHardwareEvent(
                timestamp,
                HardwareName: ReadAsciiFromBits(reader, 32, 5),
                HardwareVersion: $"{reader.Get(80, 4)}.{reader.Get(84, 4)}",
                SoftwareVersion: $"{reader.Get(72, 4)}.{reader.Get(76, 4)}",
                GyroSupported: false);
        }
        else if (type == 0x10)
        {
            yield return new SmartCubeBatteryEvent(timestamp, reader.Get(24, 8));
        }
    }

    private IEnumerable<SmartCubeEvent> ParseGen4(byte[] decoded, DateTimeOffset timestamp)
    {
        var reader = new GanBitReader(decoded);
        var type = reader.Get(0, 8);
        var len = reader.Get(8, 8);
        if (type == 0x01)
        {
            var bitLength = decoded.Length * 8;
            for (var offset = 0; offset + 72 <= bitLength && reader.Get(offset, 8) == 0x01; offset += 72)
            {
                var serial = reader.Get(offset + 48, 16, littleEndian: true);
                var direction = reader.Get(offset + 64, 2);
                var face = Array.IndexOf(new[] { 2, 32, 8, 1, 16, 4 }, reader.Get(offset + 66, 6));
                if (face >= 0 && _lastMoveCount != -1 && serial != _lastMoveCount)
                {
                    _lastMoveCount = serial;
                    yield return CreateMoveEvent(timestamp, face, direction, reader.Get(offset + 16, 32, littleEndian: true));
                }
            }
        }
        else if (type == 0xED)
        {
            var serial = reader.Get(16, 16, littleEndian: true);
            if (_lastMoveCount == -1)
            {
                _lastMoveCount = serial;
            }

            yield return new SmartCubeFaceletsEvent(timestamp, ParseGanFacelets(reader, 32, 53, 69, 113));
        }
        else if (type == 0xEF)
        {
            yield return new SmartCubeBatteryEvent(timestamp, reader.Get(8 + len * 8, 8));
        }
        else if (type is 0xFC or 0xFD or 0xFE or 0xFA)
        {
            yield return new SmartCubeHardwareEvent(timestamp, HardwareName: DeviceName, GyroSupported: true);
        }
    }

    private IEnumerable<SmartCubeMoveEvent> ParseGanMoveBlock(
        GanBitReader reader,
        int serial,
        int moveOffset,
        int timeOffset,
        int maxMoves,
        DateTimeOffset timestamp)
    {
        if (_lastMoveCount == -1 || serial == _lastMoveCount)
        {
            yield break;
        }

        var diff = Math.Min((serial - _lastMoveCount) & 0xFF, maxMoves);
        _lastMoveCount = serial;
        for (var index = diff - 1; index >= 0; index--)
        {
            var code = reader.Get(moveOffset + index * 5, 5);
            var elapsed = reader.Get(timeOffset + index * 16, 16);
            var face = code >> 1;
            var direction = code & 1;
            if (face >= 6)
            {
                continue;
            }

            _cubeTimestamp += elapsed;
            yield return CreateMoveEvent(timestamp, face, direction, _cubeTimestamp);
        }
    }

    private static string ParseGanFacelets(GanBitReader reader, int cpOffset, int coOffset, int epOffset, int eoOffset)
    {
        var cp = new List<int>(8);
        var co = new List<int>(8);
        var ep = new List<int>(12);
        var eo = new List<int>(12);

        for (var i = 0; i < 7; i++)
        {
            cp.Add(reader.Get(cpOffset + i * 3, 3));
            co.Add(reader.Get(coOffset + i * 2, 2));
        }

        cp.Add(28 - cp.Sum());
        co.Add((3 - co.Sum() % 3) % 3);
        for (var i = 0; i < 11; i++)
        {
            ep.Add(reader.Get(epOffset + i * 4, 4));
            eo.Add(reader.Get(eoOffset + i, 1));
        }

        ep.Add(66 - ep.Sum());
        eo.Add((2 - eo.Sum() % 2) % 2);
        return GanFaceletConverter.ToFacelets(cp, co, ep, eo);
    }

    private SmartCubeMoveEvent CreateMoveEvent(DateTimeOffset timestamp, int face, int direction, long? cubeTimestamp)
    {
        var move = "URFDLB"[face] + (direction == 0 ? string.Empty : "'");
        return new SmartCubeMoveEvent(
            timestamp,
            face,
            direction,
            move,
            LocalTimestamp: timestamp,
            CubeTimestamp: cubeTimestamp.HasValue ? TimeSpan.FromMilliseconds(cubeTimestamp.Value) : null);
    }

    private static string ReadAsciiFromBits(GanBitReader reader, int startBit, int count)
    {
        return new string(Enumerable.Range(0, count)
            .Select(index => (char)reader.Get(startBit + index * 8, 8))
            .Where(value => value >= 32 && value <= 126)
            .ToArray()).Trim();
    }

    private static (byte[] Key, byte[] Iv) CreateKeyAndIv(byte[] baseKey, byte[] baseIv, IReadOnlyList<byte> macBytes)
    {
        var key = baseKey.ToArray();
        var iv = baseIv.ToArray();
        for (var index = 0; index < 6; index++)
        {
            key[index] = (byte)((key[index] + macBytes[5 - index]) % 255);
            iv[index] = (byte)((iv[index] + macBytes[5 - index]) % 255);
        }

        return (key, iv);
    }

    private static string ResolveMac(SmartCubeDeviceInfo advertisedDevice, ulong bluetoothAddress)
    {
        var manufacturerMac = SmartCubeBluetoothAddress.TryParseManufacturerMac(
            advertisedDevice.ManufacturerData,
            companyIds: Enumerable.Range(0, 256).Select(index => (ushort)((index << 8) | 0x01)).ToHashSet(),
            useLastBytes: true);
        return manufacturerMac is not null
            ? SmartCubeBluetoothAddress.Format(manufacturerMac)
            : SmartCubeBluetoothAddress.Format(bluetoothAddress);
    }

    private bool IsConnectionClosing()
    {
        lock (_lifetimeLock)
        {
            return _isDisconnecting || _isDisposed;
        }
    }

    private enum GanGeneration
    {
        Gen2,
        Gen3,
        Gen4
    }
}
