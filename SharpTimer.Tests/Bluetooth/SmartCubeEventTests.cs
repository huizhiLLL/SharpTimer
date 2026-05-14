using SharpTimer.Bluetooth;

namespace SharpTimer.Tests.Bluetooth;

public sealed class SmartCubeEventTests
{
    private static readonly byte[] GanDefaultKey =
    {
        0x01, 0x02, 0x42, 0x28, 0x31, 0x91, 0x16, 0x07,
        0x20, 0x05, 0x18, 0x54, 0x42, 0x11, 0x12, 0x53
    };

    private static readonly byte[] GanDefaultIv =
    {
        0x11, 0x03, 0x32, 0x28, 0x21, 0x01, 0x76, 0x27,
        0x20, 0x95, 0x78, 0x14, 0x32, 0x12, 0x02, 0x43
    };

    [Fact]
    public void BatteryEvent_ClampsBatteryLevel()
    {
        var low = new SmartCubeBatteryEvent(DateTimeOffset.UtcNow, -10);
        var high = new SmartCubeBatteryEvent(DateTimeOffset.UtcNow, 180);

        Assert.Equal(0, low.BatteryLevel);
        Assert.Equal(100, high.BatteryLevel);
    }

    [Fact]
    public void NameFilter_MatchesExactNameOrPrefixIgnoringCase()
    {
        var exact = new SmartCubeNameFilter(Name: "GAN12 ui");
        var prefix = new SmartCubeNameFilter(NamePrefix: "gocube");

        Assert.True(exact.Matches("gan12 UI"));
        Assert.True(prefix.Matches("GoCube Edge"));
        Assert.False(prefix.Matches("QiYi Cube"));
    }

    [Fact]
    public void TransformAesCbcBlocks_DecryptsOverlappingGanGen4BatteryPacket()
    {
        var key = GanDefaultKey.ToArray();
        var iv = GanDefaultIv.ToArray();
        var macBytes = new byte[] { 0x0C, 0x3D, 0x5E, 0xBD, 0xA2, 0x6E };
        for (var index = 0; index < 6; index++)
        {
            key[index] = (byte)((key[index] + macBytes[5 - index]) % 0xFF);
            iv[index] = (byte)((iv[index] + macBytes[5 - index]) % 0xFF);
        }

        var encrypted = Convert.FromHexString("E8D837F9BC168D19D2D30EABB2C459B5DD890079");

        var decoded = SmartCubeCrypto.TransformAesCbcBlocks(encrypted, encrypt: false, key, iv);
        var reader = new GanBitReader(decoded);
        var dataLength = reader.Get(8, 8);

        Assert.True(GanPacketValidator.IsValidGen4Packet(decoded));
        Assert.Equal(0xEF, decoded[0]);
        Assert.Equal(37, reader.Get(8 + dataLength * 8, 8));
    }

    [Fact]
    public void GanPacketValidator_RejectsInvalidGen4Payload()
    {
        Assert.False(GanPacketValidator.IsValidGen4Packet(new byte[20]));
    }
}
