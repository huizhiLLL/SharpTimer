using SharpTimer.Bluetooth;

namespace SharpTimer.Tests.Bluetooth;

public sealed class SmartCubeCryptoTests
{
    [Fact]
    public void TransformAesEcbAllBlocks_MatchesKnownMultiBlockVector()
    {
        var key = Convert.FromHexString("2B7E151628AED2A6ABF7158809CF4F3C");
        var plain = Convert.FromHexString(
            "6BC1BEE22E409F96E93D7E117393172A" +
            "AE2D8A571E03AC9C9EB76FAC45AF8E51");
        var expected = Convert.FromHexString(
            "3AD77BB40D7A3660A89ECAF32466EF97" +
            "F5D3D58503B9699DE785895A96FDBAAF");

        var encrypted = SmartCubeCrypto.TransformAesEcbAllBlocks(plain, encrypt: true, key);
        var decrypted = SmartCubeCrypto.TransformAesEcbAllBlocks(encrypted, encrypt: false, key);

        Assert.Equal(expected, encrypted);
        Assert.Equal(plain, decrypted);
    }
}
