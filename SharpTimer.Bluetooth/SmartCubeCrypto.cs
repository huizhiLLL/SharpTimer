using System.Security.Cryptography;

namespace SharpTimer.Bluetooth;

internal static class SmartCubeCrypto
{
    public static byte[] TransformAesCbcBlocks(byte[] data, bool encrypt, byte[] key, byte[] iv)
    {
        var result = data.ToArray();
        TransformCbcBlock(result, 0, encrypt, key, iv);
        if (result.Length > 16)
        {
            TransformCbcBlock(result, result.Length - 16, encrypt, key, iv);
        }

        return result;
    }

    public static byte[] TransformAesCbcAllBlocks(byte[] data, bool encrypt, byte[] key, byte[] iv)
    {
        if (data.Length % 16 != 0)
        {
            throw new ArgumentException("AES-CBC payload length must be block aligned.", nameof(data));
        }

        using var aes = Aes.Create();
        aes.Mode = CipherMode.CBC;
        aes.Padding = PaddingMode.None;
        aes.Key = key;
        aes.IV = iv;
        using var transform = encrypt ? aes.CreateEncryptor() : aes.CreateDecryptor();
        return transform.TransformFinalBlock(data, 0, data.Length);
    }

    private static void TransformCbcBlock(byte[] data, int offset, bool encrypt, byte[] key, byte[] iv)
    {
        using var aes = Aes.Create();
        aes.Mode = CipherMode.CBC;
        aes.Padding = PaddingMode.None;
        aes.Key = key;
        aes.IV = iv;
        using var transform = encrypt ? aes.CreateEncryptor() : aes.CreateDecryptor();
        var block = transform.TransformFinalBlock(data, offset, 16);
        Array.Copy(block, 0, data, offset, 16);
    }
}
