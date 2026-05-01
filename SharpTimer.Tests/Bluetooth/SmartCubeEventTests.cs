using SharpTimer.Bluetooth;

namespace SharpTimer.Tests.Bluetooth;

public sealed class SmartCubeEventTests
{
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
}
