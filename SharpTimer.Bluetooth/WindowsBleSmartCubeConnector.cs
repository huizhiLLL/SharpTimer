using Windows.Devices.Bluetooth;
using Windows.Devices.Bluetooth.GenericAttributeProfile;
using Windows.Security.Cryptography;

namespace SharpTimer.Bluetooth;

public static class WindowsBleSmartCubeConnector
{
    public static async Task<ISmartCubeConnection> ConnectAsync(
        SmartCubeDeviceInfo device,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(device);

        var protocol = SmartCubeKnownProtocols.CreateDefaultRegistry().ResolveByGatt(device);
        if (protocol?.Info.Id != "moyu32")
        {
            throw new NotSupportedException("当前只实现了 MoYu32 连接链路。");
        }

        var bluetoothDevice = await BluetoothLEDevice.FromBluetoothAddressAsync(device.BluetoothAddress)
            .AsTask(cancellationToken);
        if (bluetoothDevice is null)
        {
            throw new InvalidOperationException("无法打开蓝牙设备。");
        }

        Moyu32SmartCubeConnection? connection = null;
        try
        {
            connection = new Moyu32SmartCubeConnection(bluetoothDevice, device);
            await connection.InitializeAsync(cancellationToken);
            return connection;
        }
        catch
        {
            if (connection is not null)
            {
                await connection.DisposeAsync();
            }
            else
            {
                bluetoothDevice.Dispose();
            }

            throw;
        }
    }

    private sealed class Moyu32SmartCubeConnection : ISmartCubeConnection
    {
        private static readonly byte[] BaseKey = { 21, 119, 58, 92, 103, 14, 45, 31, 23, 103, 42, 19, 155, 103, 82, 87 };
        private static readonly byte[] BaseIv = { 17, 35, 38, 37, 134, 42, 44, 59, 85, 6, 127, 49, 126, 103, 33, 87 };

        private readonly BluetoothLEDevice _device;
        private readonly IReadOnlyList<byte[]> _macCandidates;
        private readonly object _lifetimeLock = new();
        private readonly byte[] _key;
        private readonly byte[] _iv;
        private string _deviceMac;
        private GattDeviceService? _service;
        private GattCharacteristic? _readCharacteristic;
        private GattCharacteristic? _writeCharacteristic;
        private TaskCompletionSource<bool>? _strongPacketProbe;
        private int _moveCount = -1;
        private int _previousMoveCount = -1;
        private bool _hasStrongPacket;
        private bool _isDisconnecting;
        private bool _isDisposed;

        public Moyu32SmartCubeConnection(BluetoothLEDevice device, SmartCubeDeviceInfo advertisedDevice)
        {
            _device = device;
            DeviceName = !string.IsNullOrWhiteSpace(device.Name)
                ? device.Name
                : advertisedDevice.Name ?? "WCU_MY3";
            _macCandidates = ResolveMoyu32MacCandidates(advertisedDevice, device.BluetoothAddress);
            _deviceMac = FormatBluetoothAddress(_macCandidates[0]);
            Protocol = new SmartCubeProtocolInfo("moyu32", "MoYu32");
            Capabilities = new SmartCubeCapabilities(
                Gyroscope: true,
                Battery: true,
                Facelets: true,
                Hardware: true,
                Reset: false);

            (_key, _iv) = CreateKeyAndIv(_macCandidates[0]);
        }

        public string DeviceName { get; }

        public string? DeviceMac => _deviceMac;

        public SmartCubeProtocolInfo Protocol { get; }

        public SmartCubeCapabilities Capabilities { get; }

        public event EventHandler<SmartCubeEvent>? EventReceived;

        public async Task InitializeAsync(CancellationToken cancellationToken)
        {
            var servicesResult = await _device.GetGattServicesForUuidAsync(
                    SmartCubeBluetoothServices.MoYu32,
                    BluetoothCacheMode.Uncached)
                .AsTask(cancellationToken);

            if (servicesResult.Status != GattCommunicationStatus.Success || servicesResult.Services.Count == 0)
            {
                throw new InvalidOperationException("找不到 MoYu32 GATT 服务。");
            }

            _service = servicesResult.Services[0];
            var characteristicsResult = await _service.GetCharacteristicsAsync(BluetoothCacheMode.Uncached)
                .AsTask(cancellationToken);
            if (characteristicsResult.Status != GattCommunicationStatus.Success)
            {
                throw new InvalidOperationException("无法读取 MoYu32 GATT 特征。");
            }

            _readCharacteristic = characteristicsResult.Characteristics
                .FirstOrDefault(item => item.Uuid == SmartCubeBluetoothServices.MoYu32ReadCharacteristic)
                ?? throw new InvalidOperationException("找不到 MoYu32 通知特征。");
            _writeCharacteristic = characteristicsResult.Characteristics
                .FirstOrDefault(item => item.Uuid == SmartCubeBluetoothServices.MoYu32WriteCharacteristic)
                ?? throw new InvalidOperationException("找不到 MoYu32 写入特征。");

            _readCharacteristic.ValueChanged += ReadCharacteristic_ValueChanged;
            var status = await _readCharacteristic.WriteClientCharacteristicConfigurationDescriptorAsync(
                    GattClientCharacteristicConfigurationDescriptorValue.Notify)
                .AsTask(cancellationToken);
            if (status != GattCommunicationStatus.Success)
            {
                throw new InvalidOperationException("无法订阅 MoYu32 通知。");
            }

            await ProbeKeyAsync(cancellationToken);
        }

        public Task SendCommandAsync(SmartCubeCommand command, CancellationToken cancellationToken = default)
        {
            if (IsConnectionClosing())
            {
                return Task.CompletedTask;
            }

            return command switch
            {
                SmartCubeCommand.RequestHardware => SendSimpleRequestAsync(161, cancellationToken),
                SmartCubeCommand.RequestFacelets => SendSimpleRequestAsync(163, cancellationToken),
                SmartCubeCommand.RequestBattery => SendSimpleRequestAsync(164, cancellationToken),
                _ => Task.CompletedTask
            };
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
                _strongPacketProbe?.TrySetResult(false);
                _strongPacketProbe = null;
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

        private async Task SendSimpleRequestAsync(byte opcode, CancellationToken cancellationToken)
        {
            var payload = new byte[20];
            payload[0] = opcode;
            await SendRequestAsync(payload, cancellationToken);
        }

        private async Task SendRequestAsync(byte[] payload, CancellationToken cancellationToken)
        {
            var writeCharacteristic = _writeCharacteristic;
            if (IsConnectionClosing() || writeCharacteristic is null)
            {
                return;
            }

            var encrypted = Transform(payload, encrypt: true);
            var buffer = CryptographicBuffer.CreateFromByteArray(encrypted);
            await writeCharacteristic.WriteValueAsync(buffer)
                .AsTask(cancellationToken);
        }

        private async Task ProbeKeyAsync(CancellationToken cancellationToken)
        {
            foreach (var candidate in _macCandidates)
            {
                if (IsConnectionClosing())
                {
                    return;
                }

                SetActiveMac(candidate);
                _strongPacketProbe = new TaskCompletionSource<bool>(TaskCreationOptions.RunContinuationsAsynchronously);

                await SendSimpleRequestAsync(161, cancellationToken);
                await SendSimpleRequestAsync(163, cancellationToken);
                await SendSimpleRequestAsync(164, cancellationToken);

                var completed = await Task.WhenAny(
                    _strongPacketProbe.Task,
                    Task.Delay(TimeSpan.FromMilliseconds(700), cancellationToken));
                if (completed == _strongPacketProbe.Task && await _strongPacketProbe.Task)
                {
                    _strongPacketProbe = null;
                    return;
                }
            }

            _strongPacketProbe = null;
            SetActiveMac(_macCandidates[0]);
            await SendSimpleRequestAsync(161, cancellationToken);
            await SendSimpleRequestAsync(163, cancellationToken);
            await SendSimpleRequestAsync(164, cancellationToken);
        }

        private void SetActiveMac(IReadOnlyList<byte> macBytes)
        {
            var (key, iv) = CreateKeyAndIv(macBytes);
            Array.Copy(key, _key, _key.Length);
            Array.Copy(iv, _iv, _iv.Length);
            _deviceMac = FormatBluetoothAddress(macBytes);
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

                var decoded = DecryptWithBestCandidate(encrypted);
                ParseDecoded(decoded, timestamp);
            }
            catch
            {
            }
        }

        private ParseResult ParseDecoded(byte[] decoded, DateTimeOffset timestamp)
        {
            if (decoded.Length == 0)
            {
                return ParseResult.Dropped;
            }

            switch (decoded[0])
            {
                case 161:
                    if (decoded.Length < 13)
                    {
                        return ParseResult.Dropped;
                    }

                    EmitSmartCubeEvent(new SmartCubeHardwareEvent(
                        timestamp,
                        HardwareName: ReadAscii(decoded, 1, 8),
                        SoftwareVersion: $"{decoded[9]}.{decoded[10]}",
                        HardwareVersion: $"{decoded[11]}.{decoded[12]}",
                        GyroSupported: true));
                    MarkStrongPacket();
                    return ParseResult.Handled;
                case 163:
                    if (TryParseFacelets(decoded, out var facelets))
                    {
                        if (decoded.Length > 19)
                        {
                            _moveCount = decoded[19];
                            _previousMoveCount = _moveCount;
                        }

                        EmitSmartCubeEvent(new SmartCubeFaceletsEvent(timestamp, facelets));
                        MarkStrongPacket();
                        return ParseResult.Handled;
                    }

                    return ParseResult.Dropped;
                case 164:
                    if (!_hasStrongPacket)
                    {
                        return ParseResult.Ignored;
                    }

                    EmitSmartCubeEvent(new SmartCubeBatteryEvent(timestamp, decoded.Length > 1 ? decoded[1] : 0));
                    return ParseResult.Handled;
                case 165:
                    return EmitMoveEvents(decoded, timestamp);
                case 171:
                    return ParseResult.Ignored;
                default:
                    return ParseResult.Dropped;
            }
        }

        private ParseResult EmitMoveEvents(byte[] decoded, DateTimeOffset timestamp)
        {
            var bits = ToBitString(decoded);
            if (bits.Length < 121)
            {
                return ParseResult.Dropped;
            }

            _moveCount = Convert.ToInt32(bits.Substring(88, 8), 2);
            if (_previousMoveCount < 0 || _moveCount == _previousMoveCount)
            {
                _previousMoveCount = _moveCount;
                return ParseResult.Ignored;
            }

            var moveDelta = (_moveCount - _previousMoveCount) & 0xFF;
            var moveDiff = Math.Min(moveDelta, 5);
            _previousMoveCount = _moveCount;
            var moveCodes = Enumerable.Range(0, 5)
                .Select(index => Convert.ToInt32(bits.Substring(96 + index * 5, 5), 2))
                .ToArray();
            if (moveCodes.Any(code => code >= 12))
            {
                return ParseResult.Dropped;
            }

            var emitted = false;
            for (var index = moveDiff - 1; index >= 0; index--)
            {
                var code = moveCodes[index];
                var face = code >> 1;
                var direction = code & 1;
                var move = "FBUDLR"[face] + (direction == 0 ? string.Empty : "'");
                EmitSmartCubeEvent(new SmartCubeMoveEvent(timestamp, face, direction, move, CubeTimestamp: TimeSpan.FromMilliseconds(moveDelta)));
                emitted = true;
            }

            if (emitted)
            {
                MarkStrongPacket();
                return ParseResult.Handled;
            }

            return ParseResult.Dropped;
        }

        private void MarkStrongPacket()
        {
            _hasStrongPacket = true;
            _strongPacketProbe?.TrySetResult(true);
        }

        private void EmitSmartCubeEvent(SmartCubeEvent smartCubeEvent)
        {
            if (IsConnectionClosing())
            {
                return;
            }

            EventReceived?.Invoke(this, smartCubeEvent);
        }

        private bool IsConnectionClosing()
        {
            lock (_lifetimeLock)
            {
                return _isDisconnecting || _isDisposed;
            }
        }

        private bool TryParseFacelets(byte[] decoded, out string facelets)
        {
            facelets = string.Empty;
            var bits = ToBitString(decoded);
            if (bits.Length < 152)
            {
                return false;
            }

            var state = new List<char>(54);
            var faces = new[] { 2, 5, 0, 3, 4, 1 };
            for (var i = 0; i < 6; i++)
            {
                var start = 8 + faces[i] * 24;
                if (start + 24 > bits.Length)
                {
                    return false;
                }

                var face = bits.Substring(start, 24);
                for (var j = 0; j < 8; j++)
                {
                    var colorIndex = Convert.ToInt32(face.Substring(j * 3, 3), 2);
                    if (colorIndex < 0 || colorIndex >= "FBUDLR".Length)
                    {
                        return false;
                    }

                    state.Add("FBUDLR"[colorIndex]);
                    if (j == 3)
                    {
                        state.Add("FBUDLR"[faces[i]]);
                    }
                }
            }

            facelets = new string(state.ToArray());
            return facelets.Length == 54;
        }

        private static SmartCubeQuaternion ParseQuaternion(byte[] decoded)
        {
            if (decoded.Length < 17)
            {
                return new SmartCubeQuaternion(0, 0, 0, 1);
            }

            const double scale = 1073741824d;
            var w = BitConverter.ToInt32(decoded, 1) / scale;
            var x = BitConverter.ToInt32(decoded, 5) / scale;
            var y = BitConverter.ToInt32(decoded, 9) / scale;
            var z = BitConverter.ToInt32(decoded, 13) / scale;
            var length = Math.Sqrt(w * w + x * x + y * y + z * z);
            return length <= 0
                ? new SmartCubeQuaternion(0, 0, 0, 1)
                : new SmartCubeQuaternion(x / length, y / length, z / length, w / length);
        }

        private byte[] Transform(byte[] data, bool encrypt)
        {
            return TransformWithKey(data, encrypt, _key, _iv);
        }

        private byte[] DecryptWithBestCandidate(byte[] encrypted)
        {
            var decoded = TransformWithKey(encrypted, encrypt: false, _key, _iv);
            var bestScore = ScoreMoyu32Packet(decoded);
            if (_macCandidates.Count <= 1)
            {
                return decoded;
            }

            byte[]? bestKey = null;
            byte[]? bestIv = null;
            var bestDecoded = decoded;
            foreach (var candidate in _macCandidates.Skip(1))
            {
                var (candidateKey, candidateIv) = CreateKeyAndIv(candidate);
                var candidateDecoded = TransformWithKey(encrypted, encrypt: false, candidateKey, candidateIv);
                var candidateScore = ScoreMoyu32Packet(candidateDecoded);
                if (candidateScore <= bestScore)
                {
                    continue;
                }

                bestScore = candidateScore;
                bestKey = candidateKey;
                bestIv = candidateIv;
                bestDecoded = candidateDecoded;
            }

            if (bestKey is not null && bestIv is not null && bestScore >= 50)
            {
                Array.Copy(bestKey, _key, _key.Length);
                Array.Copy(bestIv, _iv, _iv.Length);
                var matched = _macCandidates.FirstOrDefault(candidate =>
                {
                    var (candidateKey, _) = CreateKeyAndIv(candidate);
                    return candidateKey.SequenceEqual(bestKey);
                });
                if (matched is not null)
                {
                    _deviceMac = FormatBluetoothAddress(matched);
                }
            }

            return bestDecoded;
        }

        private static byte[] TransformWithKey(byte[] data, bool encrypt, byte[] key, byte[] iv)
        {
            var result = data.ToArray();

            if (encrypt)
            {
                TransformBlock(result, 0, encrypt: true, key, iv);
                if (result.Length > 16)
                {
                    TransformBlock(result, result.Length - 16, encrypt: true, key, iv);
                }

                return result;
            }

            if (result.Length > 16)
            {
                TransformBlock(result, result.Length - 16, encrypt: false, key, iv);
            }

            TransformBlock(result, 0, encrypt: false, key, iv);
            return result;
        }

        private static void TransformBlock(byte[] data, int offset, bool encrypt, byte[] key, byte[] iv)
        {
            using var aes = System.Security.Cryptography.Aes.Create();
            aes.Mode = System.Security.Cryptography.CipherMode.ECB;
            aes.Padding = System.Security.Cryptography.PaddingMode.None;
            aes.Key = key;

            if (encrypt)
            {
                for (var index = 0; index < 16; index++)
                {
                    data[offset + index] ^= iv[index];
                }
            }

            using var transform = encrypt ? aes.CreateEncryptor() : aes.CreateDecryptor();
            var block = transform.TransformFinalBlock(data, offset, 16);
            for (var index = 0; index < 16; index++)
            {
                data[offset + index] = encrypt ? block[index] : (byte)(block[index] ^ iv[index]);
            }
        }

        private static int ScoreMoyu32Packet(byte[] decoded)
        {
            if (decoded.Length == 0)
            {
                return 0;
            }

            return decoded[0] switch
            {
                161 => HasLikelyHardwarePayload(decoded) ? 80 : 0,
                163 => decoded.Length >= 20 && HasLikelyFaceletPayload(decoded) ? 100 : 0,
                164 => decoded.Length >= 2 && decoded[1] <= 100 ? 10 : 0,
                165 => decoded.Length >= 16 && HasLikelyMovePayload(decoded) ? 60 : 0,
                171 => decoded.Length >= 17 ? 5 : 0,
                _ => 0
            };
        }

        private static bool HasLikelyHardwarePayload(byte[] decoded)
        {
            if (decoded.Length < 13)
            {
                return false;
            }

            var name = ReadAscii(decoded, 1, 8);
            return name.Length >= 2
                && decoded[9] < 100
                && decoded[10] < 100
                && decoded[11] < 100
                && decoded[12] < 100;
        }

        private static bool HasLikelyFaceletPayload(byte[] decoded)
        {
            var bits = ToBitString(decoded);
            if (bits.Length < 152)
            {
                return false;
            }

            for (var offset = 8; offset < 152; offset += 3)
            {
                if (offset + 3 > bits.Length)
                {
                    break;
                }

                if (Convert.ToInt32(bits.Substring(offset, 3), 2) >= 6)
                {
                    return false;
                }
            }

            return true;
        }

        private static bool HasLikelyMovePayload(byte[] decoded)
        {
            var bits = ToBitString(decoded);
            if (bits.Length < 121)
            {
                return false;
            }

            return Enumerable.Range(0, 5)
                .Select(index => Convert.ToInt32(bits.Substring(96 + index * 5, 5), 2))
                .All(code => code < 12);
        }

        private static byte[] GetBluetoothAddressBytes(ulong address)
        {
            var text = address.ToString("X12");
            return Enumerable.Range(0, 6)
                .Select(index => Convert.ToByte(text.Substring(index * 2, 2), 16))
                .ToArray();
        }

        private static IReadOnlyList<byte[]> ResolveMoyu32MacCandidates(SmartCubeDeviceInfo advertisedDevice, ulong bluetoothAddress)
        {
            var candidates = new List<byte[]>();

            var manufacturerMac = TryParseMoyu32ManufacturerMac(advertisedDevice.ManufacturerData);
            if (manufacturerMac is not null)
            {
                candidates.Add(manufacturerMac);
            }

            candidates.AddRange(BuildMoyu32MacCandidatesFromName(advertisedDevice.Name));
            candidates.Add(GetBluetoothAddressBytes(bluetoothAddress));

            return candidates
                .DistinctBy(item => Convert.ToHexString(item))
                .ToArray();
        }

        private static (byte[] Key, byte[] Iv) CreateKeyAndIv(IReadOnlyList<byte> macBytes)
        {
            var key = BaseKey.ToArray();
            var iv = BaseIv.ToArray();
            for (var index = 0; index < 6; index++)
            {
                key[index] = (byte)((key[index] + macBytes[5 - index]) % 255);
                iv[index] = (byte)((iv[index] + macBytes[5 - index]) % 255);
            }

            return (key, iv);
        }

        private static byte[]? TryParseMoyu32ManufacturerMac(IReadOnlyList<byte[]>? manufacturerData)
        {
            if (manufacturerData is null)
            {
                return null;
            }

            foreach (var item in manufacturerData)
            {
                if (item.Length < 6)
                {
                    continue;
                }

                return Enumerable.Range(0, 6)
                    .Select(index => item[item.Length - index - 1])
                    .ToArray();
            }

            return null;
        }

        private static IReadOnlyList<byte[]> BuildMoyu32MacCandidatesFromName(string? deviceName)
        {
            if (string.IsNullOrWhiteSpace(deviceName))
            {
                return Array.Empty<byte[]>();
            }

            var match = System.Text.RegularExpressions.Regex.Match(
                deviceName.Trim(),
                "^WCU_MY3[23]_([0-9A-Fa-f]{4})$");
            if (!match.Success)
            {
                return Array.Empty<byte[]>();
            }

            var suffix = match.Groups[1].Value.ToUpperInvariant();
            var mids = deviceName.Contains("MY33", StringComparison.OrdinalIgnoreCase)
                ? new byte[] { 0x02, 0x01, 0x00 }
                : new byte[] { 0x00 };
            return mids.Select(mid => new[]
            {
                (byte)0xCF,
                (byte)0x30,
                (byte)0x16,
                mid,
                Convert.ToByte(suffix.Substring(0, 2), 16),
                Convert.ToByte(suffix.Substring(2, 2), 16)
            }).ToArray();
        }

        private static string FormatBluetoothAddress(ulong address)
        {
            return FormatBluetoothAddress(GetBluetoothAddressBytes(address));
        }

        private static string FormatBluetoothAddress(IReadOnlyList<byte> bytes)
        {
            var text = string.Concat(bytes.Select(value => value.ToString("X2")));
            return string.Join(":", Enumerable.Range(0, 6).Select(index => text.Substring(index * 2, 2)));
        }

        private static string ToBitString(byte[] bytes)
        {
            return string.Concat(bytes.Select(item => Convert.ToString(item, 2).PadLeft(8, '0')));
        }

        private static string ReadAscii(byte[] bytes, int start, int count)
        {
            var chars = bytes.Skip(start).Take(count).Where(value => value >= 32 && value <= 126).Select(value => (char)value);
            return new string(chars.ToArray()).Trim();
        }

        private enum ParseResult
        {
            Handled,
            Ignored,
            Dropped
        }
    }
}
