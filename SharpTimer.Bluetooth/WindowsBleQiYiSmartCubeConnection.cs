using SharpTimer.Core.SmartCubes;
using Windows.Devices.Bluetooth;
using Windows.Devices.Bluetooth.GenericAttributeProfile;
using Windows.Security.Cryptography;

namespace SharpTimer.Bluetooth;

internal sealed class WindowsBleQiYiSmartCubeConnection : ISmartCubeConnection
{
    private static readonly byte[] Key = { 87, 177, 249, 171, 205, 90, 232, 167, 156, 185, 140, 231, 87, 140, 81, 8 };
    private static readonly HashSet<ushort> QiYiCompanyIds = new() { 0x0504 };
    private const int HistorySlotCount = 11;
    private const int HistorySlotStart = 36;
    private const int HistorySlotSize = 5;
    private const double DeviceTimeScale = 1.6d;

    private readonly BluetoothLEDevice _device;
    private readonly SmartCubeDeviceInfo _advertisedDevice;
    private readonly IReadOnlyList<string> _macCandidates;
    private readonly object _lifetimeLock = new();
    private readonly SemaphoreSlim _helloLock = new(1, 1);
    private GattDeviceService? _service;
    private GattCharacteristic? _cubeCharacteristic;
    private GattCharacteristic? _writeCharacteristic;
    private string? _currentFacelets;
    private TaskCompletionSource<bool>? _helloProbe;
    private long _lastTimestamp = -1;
    private int? _batteryLevel;
    private bool _helloReceived;
    private bool _isDisconnecting;
    private bool _isDisposed;

    public WindowsBleQiYiSmartCubeConnection(BluetoothLEDevice device, SmartCubeDeviceInfo advertisedDevice)
    {
        _device = device;
        _advertisedDevice = advertisedDevice;
        DeviceName = !string.IsNullOrWhiteSpace(device.Name) ? device.Name : advertisedDevice.Name ?? "QiYi";
        _macCandidates = ResolveMacCandidates(advertisedDevice, device.BluetoothAddress);
        DeviceMac = _macCandidates[0];
        Protocol = new SmartCubeProtocolInfo("qiyi", "QiYi");
        Capabilities = new SmartCubeCapabilities(
            Gyroscope: false,
            Battery: true,
            Facelets: true,
            Hardware: true,
            Reset: true);
    }

    public string DeviceName { get; }

    public string? DeviceMac { get; private set; }

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

        var characteristics = characteristicsResult.Characteristics;
        _cubeCharacteristic = characteristics.FirstOrDefault(
            item => item.Uuid == SmartCubeBluetoothServices.GanGen1MovesCharacteristic);
        if (_cubeCharacteristic is null)
        {
            throw new InvalidOperationException("找不到 QiYi 通信特征。");
        }

        _writeCharacteristic = ResolveWriteCharacteristic(characteristics, _cubeCharacteristic)
            ?? throw new InvalidOperationException("找不到 QiYi 写入特征。");

        _cubeCharacteristic.ValueChanged += CubeCharacteristic_ValueChanged;
        var status = await _cubeCharacteristic.WriteClientCharacteristicConfigurationDescriptorAsync(
                ResolveNotifyMode(_cubeCharacteristic))
            .AsTask(cancellationToken);
        if (status != GattCommunicationStatus.Success)
        {
            throw new InvalidOperationException("无法订阅 QiYi 通知。");
        }

        EventReceived?.Invoke(this, new SmartCubeHardwareEvent(DateTimeOffset.UtcNow, HardwareName: DeviceName));
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
            _writeCharacteristic = null;
            _service = null;
            _helloProbe?.TrySetResult(false);
            _helloProbe = null;
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
        if (IsConnectionClosing() || string.IsNullOrWhiteSpace(DeviceMac))
        {
            return;
        }

        await _helloLock.WaitAsync(cancellationToken);
        try
        {
            if (_helloReceived)
            {
                await SendHelloForMacAsync(DeviceMac, cancellationToken);
                return;
            }

            foreach (var candidate in _macCandidates)
            {
                if (IsConnectionClosing())
                {
                    return;
                }

                DeviceMac = candidate;
                _helloProbe = new TaskCompletionSource<bool>(TaskCreationOptions.RunContinuationsAsynchronously);
                await SendHelloForMacAsync(candidate, cancellationToken);
                var completed = await Task.WhenAny(
                    _helloProbe.Task,
                    Task.Delay(TimeSpan.FromMilliseconds(900), cancellationToken));
                if (completed == _helloProbe.Task && await _helloProbe.Task)
                {
                    _helloProbe = null;
                    return;
                }
            }

            _helloProbe = null;
        }
        finally
        {
            _helloLock.Release();
        }
    }

    private async Task SendHelloForMacAsync(string macText, CancellationToken cancellationToken)
    {
        var content = new List<byte> { 0x00, 0x6b, 0x01, 0x00, 0x00, 0x22, 0x06, 0x00, 0x02, 0x08, 0x00 };
        var mac = SmartCubeBluetoothAddress.Parse(macText);
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
        var characteristic = _writeCharacteristic;
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

        var encrypted = SmartCubeCrypto.TransformAesEcbAllBlocks(message.ToArray(), encrypt: true, Key);
        await characteristic.WriteValueAsync(
                CryptographicBuffer.CreateFromByteArray(encrypted),
                ResolveWriteOption(characteristic))
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

            var decoded = SmartCubeCrypto.TransformAesEcbAllBlocks(encrypted, encrypt: false, Key);
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
            MarkHelloReceived();
            _ = SendAckAsync(message.Skip(2).Take(5).ToArray());
            _currentFacelets = ParseFacelets(message.Skip(7).Take(27).ToArray());
            _lastTimestamp = cubeTimestamp;
            Emit(new SmartCubeFaceletsEvent(timestamp, _currentFacelets, IsAuthoritative: true));
            EmitBattery(message[35], timestamp);
        }
        else if (opcode == 0x03 && message.Length >= 36)
        {
            MarkHelloReceived();
            _ = SendAckAsync(message.Skip(2).Take(5).ToArray());

            var facelets = ParseFacelets(message.Skip(7).Take(27).ToArray());
            if (!ThreeByThreeFacelets.IsValidState(_currentFacelets ?? string.Empty))
            {
                _currentFacelets = facelets;
                _lastTimestamp = cubeTimestamp;
                Emit(new SmartCubeFaceletsEvent(timestamp, facelets, IsAuthoritative: true));
                EmitBattery(message[35], timestamp);
                return;
            }

            var previousTimestamp = _lastTimestamp;
            var moves = CollectStateChangeMoves(message, previousTimestamp)
                .ToArray();
            foreach (var move in moves)
            {
                EmitMove(move.Code, move.Timestamp, timestamp);
            }

            _currentFacelets = facelets;
            Emit(new SmartCubeFaceletsEvent(timestamp, facelets, IsAuthoritative: true));

            _lastTimestamp = Math.Max(_lastTimestamp, cubeTimestamp);
            EmitBattery(message[35], timestamp);
        }
        else if (opcode == 0x04 && message.Length >= 36)
        {
            MarkHelloReceived();
            _currentFacelets = ParseFacelets(message.Skip(7).Take(27).ToArray());
            _lastTimestamp = cubeTimestamp;
            Emit(new SmartCubeFaceletsEvent(timestamp, _currentFacelets, IsAuthoritative: true));
            EmitBattery(message[35], timestamp);
        }
    }

    private void EmitMove(byte code, long cubeTimestamp, DateTimeOffset timestamp)
    {
        var face = new[] { 4, 1, 3, 0, 2, 5 }[(code - 1) >> 1];
        var direction = (code & 1) == 0 ? 0 : 1;
        var move = "URFDLB"[face] + ((code & 1) == 0 ? string.Empty : "'");
        Emit(new SmartCubeMoveEvent(
            timestamp,
            face,
            direction,
            move,
            LocalTimestamp: timestamp,
            CubeTimestamp: TimeSpan.FromMilliseconds(Math.Truncate(cubeTimestamp / DeviceTimeScale))));

        _lastTimestamp = cubeTimestamp;
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

    private void MarkHelloReceived()
    {
        _helloReceived = true;
        _helloProbe?.TrySetResult(true);
    }

    private static IEnumerable<(byte Code, long Timestamp)> CollectStateChangeMoves(
        byte[] message,
        long lastTimestamp)
    {
        var candidates = new List<(byte Code, long Timestamp)>
        {
            (message[34], ReadUInt32BE(message, 3))
        };

        for (var index = 0; index < HistorySlotCount; index++)
        {
            var offset = HistorySlotStart + HistorySlotSize * index;
            if (offset + HistorySlotSize > message.Length)
            {
                break;
            }

            if (IsEmptyHistorySlot(message, offset))
            {
                continue;
            }

            candidates.Add((message[offset + 4], ReadUInt32BE(message, offset)));
        }

        var seen = new HashSet<(byte Code, long Timestamp)>();
        foreach (var candidate in candidates
            .OrderBy(item => item.Timestamp)
            .Where(item => item.Timestamp > lastTimestamp
                && ConvertMove(item.Code) >= 0))
        {
            if (seen.Add(candidate))
            {
                yield return candidate;
            }
        }
    }

    private static bool IsEmptyHistorySlot(IReadOnlyList<byte> message, int offset)
    {
        for (var index = 0; index < HistorySlotSize; index++)
        {
            if (message[offset + index] != 0xFF)
            {
                return false;
            }
        }

        return true;
    }

    private static int ConvertMove(byte rawMove)
    {
        if (rawMove == 0)
        {
            return -1;
        }

        var axisIndex = (rawMove - 1) >> 1;
        if (axisIndex < 0 || axisIndex >= 6)
        {
            return -1;
        }

        var axis = new[] { 4, 1, 3, 0, 2, 5 }[axisIndex];
        var power = (rawMove & 1) == 0 ? 0 : 2;
        return axis * 3 + power;
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

    private static GattCharacteristic? ResolveWriteCharacteristic(
        IReadOnlyList<GattCharacteristic> characteristics,
        GattCharacteristic cubeCharacteristic)
    {
        return SupportsWrite(cubeCharacteristic)
            ? cubeCharacteristic
            : characteristics.FirstOrDefault(item =>
                item.Uuid == SmartCubeBluetoothServices.GanGen1StateCharacteristic
                && SupportsWrite(item));
    }

    private static bool SupportsWrite(GattCharacteristic characteristic)
    {
        return characteristic.CharacteristicProperties.HasFlag(GattCharacteristicProperties.Write)
            || characteristic.CharacteristicProperties.HasFlag(GattCharacteristicProperties.WriteWithoutResponse);
    }

    private static GattClientCharacteristicConfigurationDescriptorValue ResolveNotifyMode(GattCharacteristic characteristic)
    {
        return characteristic.CharacteristicProperties.HasFlag(GattCharacteristicProperties.Indicate)
            && !characteristic.CharacteristicProperties.HasFlag(GattCharacteristicProperties.Notify)
            ? GattClientCharacteristicConfigurationDescriptorValue.Indicate
            : GattClientCharacteristicConfigurationDescriptorValue.Notify;
    }

    private static GattWriteOption ResolveWriteOption(GattCharacteristic characteristic)
    {
        return characteristic.CharacteristicProperties.HasFlag(GattCharacteristicProperties.WriteWithoutResponse)
            ? GattWriteOption.WriteWithoutResponse
            : GattWriteOption.WriteWithResponse;
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

    private static long ReadUInt32BE(IReadOnlyList<byte> data, int offset)
    {
        return (data[offset] & 0xFFL) << 24
            | (data[offset + 1] & 0xFFL) << 16
            | (data[offset + 2] & 0xFFL) << 8
            | (data[offset + 3] & 0xFFL);
    }

    private static short ReadInt16BE(IReadOnlyList<byte> data, int offset)
    {
        return unchecked((short)(data[offset] << 8 | data[offset + 1]));
    }

    private static IReadOnlyList<string> ResolveMacCandidates(SmartCubeDeviceInfo advertisedDevice, ulong bluetoothAddress)
    {
        var candidates = new List<string>();
        candidates.AddRange(BuildQiYiMacCandidatesFromName(advertisedDevice.Name));

        var windowsAddress = SmartCubeBluetoothAddress.Format(bluetoothAddress);
        if (!string.IsNullOrWhiteSpace(windowsAddress))
        {
            candidates.Add(windowsAddress);
        }

        var manufacturerMac = SmartCubeBluetoothAddress.TryParseManufacturerMac(
            advertisedDevice.ManufacturerData,
            QiYiCompanyIds);
        if (manufacturerMac is not null)
        {
            candidates.Add(SmartCubeBluetoothAddress.Format(manufacturerMac));
        }

        return candidates
            .Where(item => !string.IsNullOrWhiteSpace(item))
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .DefaultIfEmpty(SmartCubeBluetoothAddress.Format(bluetoothAddress))
            .ToArray();
    }

    private static IReadOnlyList<string> BuildQiYiMacCandidatesFromName(string? deviceName)
    {
        if (string.IsNullOrWhiteSpace(deviceName))
        {
            return Array.Empty<string>();
        }

        var text = deviceName.Trim();
        var candidates = new List<string>();
        var qy = System.Text.RegularExpressions.Regex.Match(text, "^QY-QYSC-.*-([0-9A-Fa-f]{4})$");
        if (qy.Success)
        {
            var suffix = qy.Groups[1].Value.ToUpperInvariant();
            candidates.Add($"CC:A3:00:00:{suffix[..2]}:{suffix[2..]}");
        }

        var xmd = System.Text.RegularExpressions.Regex.Match(text, "^XMD-TornadoV4-i-([0-9A-Fa-f]{4})$");
        if (xmd.Success)
        {
            var suffix = xmd.Groups[1].Value.ToUpperInvariant();
            candidates.Add($"CC:A3:00:00:{suffix[..2]}:{suffix[2..]}");
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
