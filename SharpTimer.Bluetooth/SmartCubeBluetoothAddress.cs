namespace SharpTimer.Bluetooth;

internal static class SmartCubeBluetoothAddress
{
    public static byte[] GetBluetoothAddressBytes(ulong address)
    {
        var text = address.ToString("X12");
        return Enumerable.Range(0, 6)
            .Select(index => Convert.ToByte(text.Substring(index * 2, 2), 16))
            .ToArray();
    }

    public static string Format(ulong address)
    {
        return Format(GetBluetoothAddressBytes(address));
    }

    public static string Format(IReadOnlyList<byte> bytes)
    {
        var text = string.Concat(bytes.Select(value => value.ToString("X2")));
        return string.Join(":", Enumerable.Range(0, 6).Select(index => text.Substring(index * 2, 2)));
    }

    public static byte[]? TryParseManufacturerMac(
        IReadOnlyList<byte[]>? manufacturerData,
        IReadOnlySet<ushort>? companyIds = null,
        bool skipCompanyId = true,
        bool reversedByteOrder = true,
        bool useLastBytes = false)
    {
        if (manufacturerData is null)
        {
            return null;
        }

        foreach (var item in manufacturerData)
        {
            if (item.Length < 8)
            {
                continue;
            }

            var companyId = (ushort)(item[0] | item[1] << 8);
            if (companyIds is not null && !companyIds.Contains(companyId))
            {
                continue;
            }

            var data = skipCompanyId ? item.Skip(2).ToArray() : item;
            if (data.Length < 6)
            {
                continue;
            }

            if (useLastBytes)
            {
                return Enumerable.Range(0, 6)
                    .Select(index => data[data.Length - index - 1])
                    .ToArray();
            }

            return reversedByteOrder
                ? Enumerable.Range(0, 6).Select(index => data[5 - index]).ToArray()
                : data.Take(6).ToArray();
        }

        return null;
    }

    public static byte[] Parse(string mac)
    {
        return mac.Split(':', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
            .Select(part => Convert.ToByte(part, 16))
            .ToArray();
    }
}
