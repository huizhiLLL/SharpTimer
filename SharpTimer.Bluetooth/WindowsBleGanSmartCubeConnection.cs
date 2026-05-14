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
    private readonly object _lifetimeLock = new();
    private readonly SemaphoreSlim _writeLock = new(1, 1);
    private byte[] _key = Array.Empty<byte>();
    private byte[] _iv = Array.Empty<byte>();
    private GanGeneration? _generation;
    private GattDeviceService? _service;
    private GattCharacteristic? _readCharacteristic;
    private GattCharacteristic? _writeCharacteristic;
    private readonly List<GanBufferedMove> _moveBuffer = new();
    private readonly Dictionary<int, string> _gen4HardwareInfo = new();
    private int _lastMoveCount = -1;
    private int _currentMoveCount = -1;
    private long _cubeTimestamp;
    private DateTimeOffset? _lastLocalMoveTimestamp;
    private bool _gen4GyroObserved;
    private bool _gen4HardwareInfoEmitted;
    private bool _isDisconnecting;
    private bool _isDisposed;

    private WindowsBleGanSmartCubeConnection(
        BluetoothLEDevice device,
        SmartCubeDeviceInfo advertisedDevice,
        GanGeneration? generation)
    {
        _device = device;
        _advertisedDevice = advertisedDevice;
        _generation = generation;
        DeviceName = !string.IsNullOrWhiteSpace(device.Name) ? device.Name : advertisedDevice.Name ?? "GAN";
        DeviceMac = ResolveMac(advertisedDevice, device.BluetoothAddress);
        ApplyGeneration(generation ?? GanGeneration.Gen2);
    }

    public string DeviceName { get; }

    public string DeviceMac { get; }

    public SmartCubeProtocolInfo Protocol { get; private set; } = new("gan", "GAN");

    public SmartCubeCapabilities Capabilities { get; private set; } = new(
        Gyroscope: false,
        Battery: true,
        Facelets: true,
        Hardware: true,
        Reset: true);

    public event EventHandler<SmartCubeEvent>? EventReceived;

    public static WindowsBleGanSmartCubeConnection Create(BluetoothLEDevice device, SmartCubeDeviceInfo advertisedDevice)
    {
        return new WindowsBleGanSmartCubeConnection(device, advertisedDevice, ResolveInitialGeneration(device, advertisedDevice));
    }

    private static GanGeneration? ResolveInitialGeneration(BluetoothLEDevice device, SmartCubeDeviceInfo advertisedDevice)
    {
        if (SmartCubeBluetoothServices.IsGanGen4DeviceName(device.Name)
            || SmartCubeBluetoothServices.IsGanGen4DeviceName(advertisedDevice.Name))
        {
            return GanGeneration.Gen4;
        }

        var services = advertisedDevice.ServiceUuids;
        if (services.Contains(SmartCubeBluetoothServices.GanGen4Service))
        {
            return GanGeneration.Gen4;
        }

        if (services.Contains(SmartCubeBluetoothServices.GanGen3Service))
        {
            return GanGeneration.Gen3;
        }

        return services.Contains(SmartCubeBluetoothServices.GanGen2Service)
            ? GanGeneration.Gen2
            : null;
    }

    public async Task InitializeAsync(CancellationToken cancellationToken)
    {
        var service = await FindServiceAsync(cancellationToken);
        if (service is null || _generation is null)
        {
            throw new InvalidOperationException("找不到 GAN GATT 服务（已尝试 Gen4 / Gen3 / Gen2）。");
        }

        _service = service;
        var generation = _generation.Value;

        var characteristicsResult = await _service.GetCharacteristicsAsync(BluetoothCacheMode.Uncached)
            .AsTask(cancellationToken);
        if (characteristicsResult.Status != GattCommunicationStatus.Success)
        {
            throw new InvalidOperationException("无法读取 GAN GATT 特征。");
        }

        var readUuid = generation switch
        {
            GanGeneration.Gen4 => SmartCubeBluetoothServices.GanGen4StateCharacteristic,
            GanGeneration.Gen3 => SmartCubeBluetoothServices.GanGen3StateCharacteristic,
            _ => SmartCubeBluetoothServices.GanGen2StateCharacteristic
        };
        var writeUuid = generation switch
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

    private async Task<GattDeviceService?> FindServiceAsync(CancellationToken cancellationToken)
    {
        var servicesResult = await _device.GetGattServicesAsync(BluetoothCacheMode.Uncached)
            .AsTask(cancellationToken);
        if (servicesResult.Status == GattCommunicationStatus.Success)
        {
            var services = servicesResult.Services.ToArray();
            var generation = ResolveGenerationFromServices(services.Select(item => item.Uuid).ToHashSet());
            if (generation is not null)
            {
                var selectedServiceUuid = GetServiceUuid(generation.Value);
                GattDeviceService? selectedService = null;
                foreach (var service in services)
                {
                    if (service.Uuid == selectedServiceUuid && selectedService is null)
                    {
                        selectedService = service;
                    }
                    else
                    {
                        service.Dispose();
                    }
                }

                if (selectedService is not null)
                {
                    _generation = generation;
                    ApplyGeneration(generation.Value);
                    return selectedService;
                }
            }
            else
            {
                foreach (var service in services)
                {
                    service.Dispose();
                }
            }
        }

        foreach (var generation in GetCandidateGenerations())
        {
            var serviceUuid = GetServiceUuid(generation);

            var candidateServicesResult = await _device.GetGattServicesForUuidAsync(serviceUuid, BluetoothCacheMode.Uncached)
                .AsTask(cancellationToken);
            if (candidateServicesResult.Status != GattCommunicationStatus.Success || candidateServicesResult.Services.Count == 0)
            {
                continue;
            }

            _generation = generation;
            ApplyGeneration(generation);
            return candidateServicesResult.Services[0];
        }

        return null;
    }

    private static GanGeneration? ResolveGenerationFromServices(IReadOnlySet<Guid> serviceUuids)
    {
        if (serviceUuids.Contains(SmartCubeBluetoothServices.GanGen2Service))
        {
            return GanGeneration.Gen2;
        }

        if (serviceUuids.Contains(SmartCubeBluetoothServices.GanGen3Service))
        {
            return GanGeneration.Gen3;
        }

        return serviceUuids.Contains(SmartCubeBluetoothServices.GanGen4Service)
            ? GanGeneration.Gen4
            : null;
    }

    private static Guid GetServiceUuid(GanGeneration generation)
    {
        return generation switch
        {
            GanGeneration.Gen4 => SmartCubeBluetoothServices.GanGen4Service,
            GanGeneration.Gen3 => SmartCubeBluetoothServices.GanGen3Service,
            _ => SmartCubeBluetoothServices.GanGen2Service
        };
    }

    private IReadOnlyList<GanGeneration> GetCandidateGenerations()
    {
        if (_generation is not null)
        {
            return new[] { _generation.Value };
        }

        return new[]
        {
            GanGeneration.Gen4,
            GanGeneration.Gen3,
            GanGeneration.Gen2
        };
    }

    private void ApplyGeneration(GanGeneration generation)
    {
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
        await _writeLock.WaitAsync(cancellationToken);
        try
        {
            await writeCharacteristic.WriteValueAsync(
                    CryptographicBuffer.CreateFromByteArray(encrypted),
                    ResolveWriteOption(writeCharacteristic))
                .AsTask(cancellationToken);
        }
        finally
        {
            _writeLock.Release();
        }
    }

    private byte[]? CreateCommandPayload(SmartCubeCommand command)
    {
        if (_generation is null)
        {
            return null;
        }

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
                SmartCubeCommand.RequestHardware => CreateGen4HardwareRequestPayload(),
                SmartCubeCommand.RequestBattery => CreatePayload(20, 0xDD, 0x04, 0x00, 0xEF),
                _ => null
            }
        };
    }

    private byte[] CreateGen4HardwareRequestPayload()
    {
        _gen4HardwareInfo.Clear();
        _gen4HardwareInfoEmitted = false;
        return CreatePayload(20, 0xDF, 0x03);
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
            if (_generation is not { } generation || !IsValidPacket(generation, decoded))
            {
                return;
            }

            foreach (var smartCubeEvent in Parse(decoded, timestamp))
            {
                EventReceived?.Invoke(this, smartCubeEvent);
            }
        }
        catch
        {
        }
    }

    private static GattWriteOption ResolveWriteOption(GattCharacteristic characteristic)
    {
        var properties = characteristic.CharacteristicProperties;
        if (properties.HasFlag(GattCharacteristicProperties.Write))
        {
            return GattWriteOption.WriteWithResponse;
        }

        return properties.HasFlag(GattCharacteristicProperties.WriteWithoutResponse)
            ? GattWriteOption.WriteWithoutResponse
            : GattWriteOption.WriteWithResponse;
    }

    private static bool IsValidPacket(GanGeneration generation, IReadOnlyList<byte> decoded)
    {
        return generation switch
        {
            GanGeneration.Gen2 => GanPacketValidator.IsValidGen2Packet(decoded),
            GanGeneration.Gen3 => GanPacketValidator.IsValidGen3Packet(decoded),
            _ => GanPacketValidator.IsValidGen4Packet(decoded)
        };
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
                yield return CreateMoveEvent(
                    timestamp,
                    face,
                    direction,
                    reader.Get(24, 32, littleEndian: true),
                    timestamp);
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
                if (face >= 0 && _lastMoveCount != -1)
                {
                    _currentMoveCount = serial;
                    _moveBuffer.Add(new GanBufferedMove(
                        serial,
                        face,
                        direction,
                        reader.Get(offset + 16, 32, littleEndian: true),
                        timestamp));
                    _lastLocalMoveTimestamp = timestamp;
                }
            }

            foreach (var smartCubeEvent in EvictMoveBuffer(requestHistory: true))
            {
                yield return smartCubeEvent;
            }
        }
        else if (type == 0xD1)
        {
            var startSerial = reader.Get(16, 8);
            var count = Math.Max(0, (len - 1) * 2);
            for (var index = 0; index < count; index++)
            {
                var face = Array.IndexOf(new[] { 1, 5, 3, 0, 4, 2 }, reader.Get(24 + 4 * index, 3));
                var direction = reader.Get(27 + 4 * index, 1);
                if (face >= 0)
                {
                    InjectMissedMove(new GanBufferedMove(
                        (startSerial - index) & 0xFF,
                        face,
                        direction,
                        null,
                        null));
                }
            }

            foreach (var smartCubeEvent in EvictMoveBuffer(requestHistory: false))
            {
                yield return smartCubeEvent;
            }
        }
        else if (type == 0xED)
        {
            var serial = reader.Get(16, 16, littleEndian: true);
            if (_lastMoveCount == -1)
            {
                _lastMoveCount = serial;
            }
            else if (_lastLocalMoveTimestamp is not null
                && timestamp - _lastLocalMoveTimestamp.Value > TimeSpan.FromMilliseconds(500))
            {
                _currentMoveCount = serial;
                RequestMissingMovesFromFacelets(serial);
            }

            yield return new SmartCubeFaceletsEvent(timestamp, ParseGanFacelets(reader, 32, 53, 69, 113));
        }
        else if (type is >= 0xFA and <= 0xFE)
        {
            switch (type)
            {
                case 0xFA:
                    _gen4HardwareInfo[type] = $"{reader.Get(24, 16, littleEndian: true):D4}-{reader.Get(40, 8):D2}-{reader.Get(48, 8):D2}";
                    break;
                case 0xFC:
                    _gen4HardwareInfo[type] = ReadAsciiFromBits(reader, 24, Math.Max(0, len - 1));
                    break;
                case 0xFD:
                case 0xFE:
                    _gen4HardwareInfo[type] = $"{reader.Get(24, 4)}.{reader.Get(28, 4)}";
                    break;
            }

            if (_gen4HardwareInfo.Count == 4)
            {
                _gen4HardwareInfoEmitted = true;
                yield return CreateGen4HardwareEvent(timestamp);
            }
        }
        else if (type == 0xEC)
        {
            var firstGyroThisConnection = !_gen4GyroObserved;
            _gen4GyroObserved = true;

            yield return new SmartCubeGyroEvent(
                timestamp,
                new SmartCubeQuaternion(
                    ParseGanGen4SignedUnit(reader.Get(32, 16)),
                    ParseGanGen4SignedUnit(reader.Get(48, 16)),
                    ParseGanGen4SignedUnit(reader.Get(64, 16)),
                    ParseGanGen4SignedUnit(reader.Get(16, 16))),
                new SmartCubeVector(
                    ParseGanGen4Velocity(reader.Get(80, 4)),
                    ParseGanGen4Velocity(reader.Get(84, 4)),
                    ParseGanGen4Velocity(reader.Get(88, 4))));

            if (firstGyroThisConnection && _gen4HardwareInfoEmitted && _gen4HardwareInfo.Count == 4)
            {
                yield return CreateGen4HardwareEvent(timestamp);
            }
        }
        else if (type == 0xEF)
        {
            yield return new SmartCubeBatteryEvent(timestamp, reader.Get(8 + len * 8, 8));
        }
        else if (type == 0xEA)
        {
            _ = DisconnectAsync();
        }
    }

    private SmartCubeHardwareEvent CreateGen4HardwareEvent(DateTimeOffset timestamp)
    {
        return new SmartCubeHardwareEvent(
            timestamp,
            HardwareName: _gen4HardwareInfo.GetValueOrDefault(0xFC),
            SoftwareVersion: _gen4HardwareInfo.GetValueOrDefault(0xFD),
            HardwareVersion: _gen4HardwareInfo.GetValueOrDefault(0xFE),
            ProductDate: _gen4HardwareInfo.GetValueOrDefault(0xFA),
            GyroSupported: _gen4GyroObserved);
    }

    private static double ParseGanGen4SignedUnit(int value)
    {
        return (1 - (value >> 15) * 2) * (value & 0x7FFF) / 32767d;
    }

    private static double ParseGanGen4Velocity(int value)
    {
        return (1 - (value >> 3) * 2) * (value & 0x7);
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
            yield return CreateMoveEvent(timestamp, face, direction, _cubeTimestamp, timestamp);
        }
    }

    private IEnumerable<SmartCubeMoveEvent> EvictMoveBuffer(bool requestHistory)
    {
        while (_moveBuffer.Count > 0)
        {
            var head = _moveBuffer[0];
            var diff = _lastMoveCount == -1 ? 1 : (head.Serial - _lastMoveCount) & 0xFF;
            if (diff > 1)
            {
                if (_moveBuffer.Count > 16)
                {
                    _ = DisconnectAsync();
                }

                if (requestHistory)
                {
                    _ = RequestMoveHistoryAsync(head.Serial, diff);
                }

                yield break;
            }

            _moveBuffer.RemoveAt(0);
            if (diff == 0)
            {
                continue;
            }

            _lastMoveCount = head.Serial;
            yield return CreateMoveEvent(
                head.EventTimestamp ?? DateTimeOffset.UtcNow,
                head.Face,
                head.Direction,
                head.CubeTimestamp,
                head.EventTimestamp);
        }

        if (_moveBuffer.Count > 16)
        {
            _ = DisconnectAsync();
        }
    }

    private void InjectMissedMove(GanBufferedMove move)
    {
        if (_moveBuffer.Any(item => item.Serial == move.Serial))
        {
            return;
        }

        if (_moveBuffer.Count > 0)
        {
            var head = _moveBuffer[0];
            if (!IsSerialInRange(_lastMoveCount, head.Serial, move.Serial))
            {
                return;
            }

            if (move.Serial == ((head.Serial - 1) & 0xFF))
            {
                _moveBuffer.Insert(0, move);
            }

            return;
        }

        if (IsSerialInRange(_lastMoveCount, _currentMoveCount, move.Serial, closedEnd: true))
        {
            _moveBuffer.Insert(0, move);
        }
    }

    private void RequestMissingMovesFromFacelets(int serial)
    {
        var diff = (serial - _lastMoveCount) & 0xFF;
        if (diff <= 0 || serial == 0)
        {
            return;
        }

        var startSerial = _moveBuffer.Count > 0
            ? _moveBuffer[0].Serial
            : (serial + 1) & 0xFF;
        _ = RequestMoveHistoryAsync(startSerial, diff + 1);
    }

    private async Task RequestMoveHistoryAsync(int serial, int count)
    {
        if (_generation != GanGeneration.Gen4 || IsConnectionClosing())
        {
            return;
        }

        if (serial % 2 == 0)
        {
            serial = (serial - 1) & 0xFF;
        }

        if (count % 2 == 1)
        {
            count++;
        }

        count = Math.Min(count, serial + 1);
        var payload = CreatePayload(20, 0xD1, 0x04, (byte)serial, 0x00, (byte)count);
        try
        {
            await SendRequestAsync(payload, CancellationToken.None);
        }
        catch
        {
        }
    }

    private static bool IsSerialInRange(
        int start,
        int end,
        int serial,
        bool closedStart = false,
        bool closedEnd = false)
    {
        return ((end - start) & 0xFF) >= ((serial - start) & 0xFF)
            && (closedStart || ((start - serial) & 0xFF) > 0)
            && (closedEnd || ((end - serial) & 0xFF) > 0);
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

    private SmartCubeMoveEvent CreateMoveEvent(
        DateTimeOffset timestamp,
        int face,
        int direction,
        long? cubeTimestamp,
        DateTimeOffset? localTimestamp = null)
    {
        var move = "URFDLB"[face] + (direction switch
        {
            1 => "'",
            2 => "2",
            _ => string.Empty
        });
        return new SmartCubeMoveEvent(
            timestamp,
            face,
            direction,
            move,
            LocalTimestamp: localTimestamp,
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

    private sealed record GanBufferedMove(
        int Serial,
        int Face,
        int Direction,
        long? CubeTimestamp,
        DateTimeOffset? EventTimestamp);
}
