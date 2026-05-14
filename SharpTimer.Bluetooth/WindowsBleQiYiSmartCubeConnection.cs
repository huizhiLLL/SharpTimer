using SharpTimer.Core.SmartCubes;
using Windows.Devices.Bluetooth;
using Windows.Devices.Bluetooth.GenericAttributeProfile;
using Windows.Security.Cryptography;

namespace SharpTimer.Bluetooth;

internal sealed class WindowsBleQiYiSmartCubeConnection : ISmartCubeConnection
{
    private static readonly byte[] Key = { 87, 177, 249, 171, 205, 90, 232, 167, 156, 185, 140, 231, 87, 140, 81, 8 };
    private static readonly byte[] Iv = new byte[16];
    private static readonly HashSet<ushort> QiYiCompanyIds = new() { 0x0504 };

    private readonly BluetoothLEDevice _device;
    private readonly SmartCubeDeviceInfo _advertisedDevice;
    private readonly object _lifetimeLock = new();
    private GattDeviceService? _service;
    private GattCharacteristic? _cubeCharacteristic;
    private string? _currentFacelets;
    private uint _lastTimestamp;
    private int? _batteryLevel;
    private bool _isDisconnecting;
    private bool _isDisposed;

    public WindowsBleQiYiSmartCubeConnection(BluetoothLEDevice device, SmartCubeDeviceInfo advertisedDevice)
    {
        _device = device;
        _advertisedDevice = advertisedDevice;
        DeviceName = !string.IsNullOrWhiteSpace(device.Name) ? device.Name : advertisedDevice.Name ?? "QiYi";
        DeviceMac = ResolveMac(advertisedDevice, device.BluetoothAddress);
        Protocol = new SmartCubeProtocolInfo("qiyi", "QiYi");
        Capabilities = new SmartCubeCapabilities(
            Gyroscope: false,
            Battery: true,
            Facelets: true,
            Hardware: true,
            Reset: true);
    }

    public string DeviceName { get; }

    public string? DeviceMac { get; }

    public SmartCubeProtocolInfo Protocol { get; }

    public SmartCubeCapabilities Capabilities { get; }

    public event EventHandler<SmartCubeEvent>? EventReceived;

    public async Task InitializeAsync(CancellationToken cancellationToken)
    {
        var servicesResult = await _device.GetGattServicesForUuidAsync(
                SmartCubeBluetoothServices.QiYiLikeFff0,
                BluetoothCacheMode.Uncached)
            .AsTask(cancellationToken);
        if (servicesResult.Status != GattCommunicationStatus.Success || servicesResult.Services.Count == 0)
        {
            throw new InvalidOperationException("找不到 QiYi GATT 服务。");
        }

        _service = servicesResult.Services[0];
        var characteristicsResult = await _service.GetCharacteristicsAsync(BluetoothCacheMode.Uncached)
            .AsTask(cancellationToken);
        if (characteristicsResult.Status != GattCommunicationStatus.Success)
        {
            throw new InvalidOperationException("无法读取 QiYi GATT 特征。");
        }

        _cubeCharacteristic = characteristicsResult.Characteristics.FirstOrDefault(
            item => item.Uuid == SmartCubeBluetoothServices.GanGen1MovesCharacteristic);
        if (_cubeCharacteristic is null)
        {
            throw new InvalidOperationException("找不到 QiYi 通信特征。");
        }

        _cubeCharacteristic.ValueChanged += CubeCharacteristic_ValueChanged;
        var status = await _cubeCharacteristic.WriteClientCharacteristicConfigurationDescriptorAsync(
                GattClientCharacteristicConfigurationDescriptorValue.Notify)
            .AsTask(cancellationToken);
        if (status != GattCommunicationStatus.Success)
        {
            throw new InvalidOperationException("无法订阅 QiYi 通知。");
        }

        EventReceived?.Invoke(this, new SmartCubeHardwareEvent(DateTimeOffset.UtcNow, HardwareName: DeviceName));
        await SendHelloAsync(cancellationToken);
    }

    public Task SendCommandAsync(SmartCubeCommand command, CancellationToken cancellationToken = default)
    {
        return command switch
        {
            SmartCubeCommand.RequestFacelets or SmartCubeCommand.RequestBattery => SendHelloAsync(cancellationToken),
            SmartCubeCommand.RequestHardware => EmitHardwareAsync(),
            _ => Task.CompletedTask
        };
    }

    public async Task DisconnectAsync(CancellationToken cancellationToken = default)
    {
        GattCharacteristic? cubeCharacteristic;
        GattDeviceService? service;
        lock (_lifetimeLock)
        {
            if (_isDisconnecting || _isDisposed)
            {
                return;
            }

            _isDisconnecting = true;
            cubeCharacteristic = _cubeCharacteristic;
            service = _service;
            _cubeCharacteristic = null;
            _service = null;
        }

        if (cubeCharacteristic is not null)
        {
            cubeCharacteristic.ValueChanged -= CubeCharacteristic_ValueChanged;
            try
            {
                await cubeCharacteristic.WriteClientCharacteristicConfigurationDescriptorAsync(
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

    private Task EmitHardwareAsync()
    {
        EventReceived?.Invoke(this, new SmartCubeHardwareEvent(DateTimeOffset.UtcNow, HardwareName: DeviceName));
        return Task.CompletedTask;
    }

    private async Task SendHelloAsync(CancellationToken cancellationToken)
    {
        var characteristic = _cubeCharacteristic;
        if (IsConnectionClosing() || characteristic is null || string.IsNullOrWhiteSpace(DeviceMac))
        {
            return;
        }

        var content = new List<byte> { 0x00, 0x6b, 0x01, 0x00, 0x00, 0x22, 0x06, 0x00, 0x02, 0x08, 0x00 };
        var mac = SmartCubeBluetoothAddress.Parse(DeviceMac);
        for (var index = 5; index >= 0; index--)
        {
            content.Add(mac[index]);
        }

        await SendMessageAsync(content.ToArray(), cancellationToken);
    }

    private async Task SendAckAsync(byte[] content, CancellationToken cancellationToken = default)
    {
        await SendMessageAsync(content, cancellationToken);
    }

    private async Task SendMessageAsync(byte[] content, CancellationToken cancellationToken)
    {
        var characteristic = _cubeCharacteristic;
        if (IsConnectionClosing() || characteristic is null)
        {
            return;
        }

        var message = new List<byte> { 0xFE, (byte)(4 + content.Length) };
        message.AddRange(content);
        var crc = Crc16Modbus(message);
        message.Add((byte)(crc & 0xFF));
        message.Add((byte)(crc >> 8));
        while (message.Count % 16 != 0)
        {
            message.Add(0);
        }

        var encrypted = SmartCubeCrypto.TransformAesCbcAllBlocks(message.ToArray(), encrypt: true, Key, Iv);
        await characteristic.WriteValueAsync(CryptographicBuffer.CreateFromByteArray(encrypted))
            .AsTask(cancellationToken);
    }

    private void CubeCharacteristic_ValueChanged(GattCharacteristic sender, GattValueChangedEventArgs args)
    {
        if (IsConnectionClosing())
        {
            return;
        }

        try
        {
            CryptographicBuffer.CopyToByteArray(args.CharacteristicValue, out var encrypted);
            if (encrypted.Length < 16 || encrypted.Length % 16 != 0)
            {
                return;
            }

            var decoded = SmartCubeCrypto.TransformAesCbcAllBlocks(encrypted, encrypt: false, Key, Iv);
            ParseDecoded(decoded, DateTimeOffset.UtcNow);
        }
        catch
        {
        }
    }

    private void ParseDecoded(byte[] decoded, DateTimeOffset timestamp)
    {
        var length = decoded.Length > 1 ? decoded[1] : 0;
        if (length <= 0 || length > decoded.Length)
        {
            return;
        }

        var message = decoded.Take(length).ToArray();
        if (message.Length < 7 || Crc16Modbus(message) != 0 || message[0] != 0xFE)
        {
            TryParseGyro(decoded, timestamp);
            return;
        }

        var opcode = message[2];
        var cubeTimestamp = ReadUInt32BE(message, 3);
        if (opcode == 0x02 && message.Length >= 36)
        {
            _ = SendAckAsync(message.Skip(2).Take(5).ToArray());
            _currentFacelets = ParseFacelets(message.Skip(7).Take(27).ToArray());
            _lastTimestamp = cubeTimestamp;
            Emit(new SmartCubeFaceletsEvent(timestamp, _currentFacelets));
            EmitBattery(message[35], timestamp);
        }
        else if (opcode == 0x03 && message.Length >= 36)
        {
            if (message.Length > 91 && message[91] != 0)
            {
                _ = SendAckAsync(message.Skip(2).Take(5).ToArray());
            }

            var moves = CollectStateChangeMoves(message, cubeTimestamp)
                .Where(item => item.Code is >= 1 and <= 12 && IsTimestampNewer(item.Timestamp, _lastTimestamp))
                .ToArray();
            foreach (var move in moves)
            {
                EmitMove(move.Code, move.Timestamp, timestamp);
            }

            if (moves.Length > 0)
            {
                _lastTimestamp = moves[^1].Timestamp;
            }

            EmitBattery(message[35], timestamp);
        }
        else if (opcode == 0x04 && message[1] == 38)
        {
            _currentFacelets = ThreeByThreeFacelets.Solved;
            _lastTimestamp = cubeTimestamp;
            Emit(new SmartCubeFaceletsEvent(timestamp, _currentFacelets));
        }
    }

    private void EmitMove(byte code, uint cubeTimestamp, DateTimeOffset timestamp)
    {
        var face = new[] { 4, 1, 3, 0, 2, 5 }[(code - 1) >> 1];
        var direction = (code & 1) == 0 ? 0 : 1;
        var move = "URFDLB"[face] + ((code & 1) == 0 ? string.Empty : "'");
        if (ThreeByThreeFacelets.IsValidState(_currentFacelets ?? string.Empty))
        {
            _currentFacelets = ThreeByThreeFacelets.ApplyMove(_currentFacelets!, move);
        }

        Emit(new SmartCubeMoveEvent(
            timestamp,
            face,
            direction,
            move,
            LocalTimestamp: timestamp,
            CubeTimestamp: TimeSpan.FromMilliseconds(Math.Truncate(cubeTimestamp / 1.6))));
        if (ThreeByThreeFacelets.IsValidState(_currentFacelets ?? string.Empty))
        {
            Emit(new SmartCubeFaceletsEvent(timestamp, _currentFacelets!));
        }
    }

    private void TryParseGyro(byte[] decoded, DateTimeOffset timestamp)
    {
        if (decoded.Length < 16 || decoded[0] != 0xCC || decoded[1] != 0x10)
        {
            return;
        }

        var expected = Crc16Modbus(decoded.Take(14).ToArray());
        var actual = decoded[14] | decoded[15] << 8;
        if (expected != actual)
        {
            return;
        }

        Emit(new SmartCubeGyroEvent(
            timestamp,
            new SmartCubeQuaternion(
                ReadInt16BE(decoded, 6) / 1000d,
                -ReadInt16BE(decoded, 10) / 1000d,
                ReadInt16BE(decoded, 8) / 1000d,
                ReadInt16BE(decoded, 12) / 1000d)));
    }

    private void EmitBattery(byte level, DateTimeOffset timestamp)
    {
        if (_batteryLevel == level)
        {
            return;
        }

        _batteryLevel = level;
        Emit(new SmartCubeBatteryEvent(timestamp, level));
    }

    private void Emit(SmartCubeEvent smartCubeEvent)
    {
        if (!IsConnectionClosing())
        {
            EventReceived?.Invoke(this, smartCubeEvent);
        }
    }

    private static IEnumerable<(byte Code, uint Timestamp)> CollectStateChangeMoves(byte[] message, uint headerTimestamp)
    {
        yield return (message[34], headerTimestamp);
        for (var count = 1; count < 10; count++)
        {
            var offset = 91 - 5 * count;
            if (offset + 4 >= message.Length)
            {
                yield break;
            }

            var timestamp = ReadUInt32BE(message, offset);
            var code = message[offset + 4];
            if (timestamp == 0)
            {
                yield break;
            }

            yield return (code, timestamp);
        }
    }

    private static string ParseFacelets(byte[] faceMessage)
    {
        var facelets = new char[54];
        const string faces = "LRDUFB";
        for (var index = 0; index < facelets.Length; index++)
        {
            facelets[index] = faces[(faceMessage[index >> 1] >> (index % 2 << 2)) & 0x0F];
        }

        return new string(facelets);
    }

    private static bool IsTimestampNewer(uint timestamp, uint previous)
    {
        return previous == 0 || timestamp != previous && unchecked(timestamp - previous) < 0x80000000;
    }

    private static ushort Crc16Modbus(IReadOnlyList<byte> data)
    {
        var crc = 0xFFFF;
        foreach (var value in data)
        {
            crc ^= value;
            for (var index = 0; index < 8; index++)
            {
                crc = (crc & 1) > 0 ? (crc >> 1) ^ 0xA001 : crc >> 1;
            }
        }

        return (ushort)crc;
    }

    private static uint ReadUInt32BE(IReadOnlyList<byte> data, int offset)
    {
        return (uint)(data[offset] << 24 | data[offset + 1] << 16 | data[offset + 2] << 8 | data[offset + 3]);
    }

    private static short ReadInt16BE(IReadOnlyList<byte> data, int offset)
    {
        return unchecked((short)(data[offset] << 8 | data[offset + 1]));
    }

    private static string ResolveMac(SmartCubeDeviceInfo advertisedDevice, ulong bluetoothAddress)
    {
        var manufacturerMac = SmartCubeBluetoothAddress.TryParseManufacturerMac(
            advertisedDevice.ManufacturerData,
            QiYiCompanyIds);
        if (manufacturerMac is not null)
        {
            return SmartCubeBluetoothAddress.Format(manufacturerMac);
        }

        var nameMac = BuildQiYiMacCandidatesFromName(advertisedDevice.Name).FirstOrDefault();
        return nameMac ?? SmartCubeBluetoothAddress.Format(bluetoothAddress);
    }

    private static IReadOnlyList<string> BuildQiYiMacCandidatesFromName(string? deviceName)
    {
        if (string.IsNullOrWhiteSpace(deviceName))
        {
            return Array.Empty<string>();
        }

        var text = deviceName.Trim();
        var candidates = new List<string>();
        var qyA = System.Text.RegularExpressions.Regex.Match(text, "^QY-QYSC-A-([0-9A-Fa-f]{4})$");
        if (qyA.Success)
        {
            var suffix = qyA.Groups[1].Value.ToUpperInvariant();
            candidates.Add($"CC:A2:00:00:{suffix[..2]}:{suffix[2..]}");
        }

        var qyS = System.Text.RegularExpressions.Regex.Match(text, "^QY-QYSC-S-([0-9A-Fa-f]{4})$");
        if (qyS.Success)
        {
            var suffix = qyS.Groups[1].Value.ToUpperInvariant();
            candidates.Add($"CC:A3:00:00:{suffix[..2]}:{suffix[2..]}");
            candidates.Add($"CC:A3:00:01:{suffix[..2]}:{suffix[2..]}");
        }

        var xmd = System.Text.RegularExpressions.Regex.Match(text, "^XMD-TornadoV4-i-([0-9A-Fa-f]{4})$");
        if (xmd.Success)
        {
            var suffix = xmd.Groups[1].Value.ToUpperInvariant();
            candidates.Add($"CC:A6:00:00:{suffix[..2]}:{suffix[2..]}");
        }

        return candidates.Distinct(StringComparer.OrdinalIgnoreCase).ToArray();
    }

    private bool IsConnectionClosing()
    {
        lock (_lifetimeLock)
        {
            return _isDisconnecting || _isDisposed;
        }
    }
}
