using System.Security.Cryptography;
using Microsoft.EntityFrameworkCore.Storage.ValueConversion;

namespace MidiKaval.Api.Infrastructure.Encryption;

/// <summary>
/// AES-256-GCM value converter for non-searchable string fields.
/// Each encryption uses a random nonce, producing different ciphertext for the same plaintext.
/// Encrypts with the active key; decrypts using the key version embedded in the ciphertext.
/// </summary>
public sealed class PiiEncryptionConverter : ValueConverter<string?, byte[]?>
{
    private const int NonceSize = 12;  // 96-bit nonce for GCM
    private const int TagSize = 16;     // 128-bit authentication tag

    public PiiEncryptionConverter()
        : base(
            v => Encrypt(v),
            v => Decrypt(v))
    { }

    private static byte[]? Encrypt(string? plaintext)
    {
        if (plaintext is null) return null;

        var provider = EncryptionKeyProvider.GetCurrent();
        var key = provider.GetActiveKey();
        var keyVersion = provider.GetActiveKeyVersion();
        var plaintextBytes = System.Text.Encoding.UTF8.GetBytes(plaintext);
        var nonce = RandomNumberGenerator.GetBytes(NonceSize);
        var ciphertext = new byte[plaintextBytes.Length];
        var tag = new byte[TagSize];

        using var aes = new AesGcm(key, TagSize);
        aes.Encrypt(nonce, plaintextBytes, ciphertext, tag);

        // Format: [1 byte version][12 byte nonce][ciphertext][16 byte tag]
        var result = new byte[1 + NonceSize + ciphertext.Length + TagSize];
        result[0] = keyVersion;
        Buffer.BlockCopy(nonce, 0, result, 1, NonceSize);
        Buffer.BlockCopy(ciphertext, 0, result, 1 + NonceSize, ciphertext.Length);
        Buffer.BlockCopy(tag, 0, result, 1 + NonceSize + ciphertext.Length, TagSize);

        return result;
    }

    private static string? Decrypt(byte[]? ciphertext)
    {
        if (ciphertext is null) return null;

        var provider = EncryptionKeyProvider.GetCurrent();
        // Use the version byte from the ciphertext to select the correct historical key
        var key = provider.GetKey(ciphertext[0]);

        // Parse: [1 byte version][12 byte nonce][ciphertext][16 byte tag]
        var nonce = new byte[NonceSize];
        var tag = new byte[TagSize];
        var encryptedData = new byte[ciphertext.Length - 1 - NonceSize - TagSize];

        Buffer.BlockCopy(ciphertext, 1, nonce, 0, NonceSize);
        Buffer.BlockCopy(ciphertext, 1 + NonceSize, encryptedData, 0, encryptedData.Length);
        Buffer.BlockCopy(ciphertext, 1 + NonceSize + encryptedData.Length, tag, 0, TagSize);

        var plaintextBytes = new byte[encryptedData.Length];
        using var aes = new AesGcm(key, TagSize);
        aes.Decrypt(nonce, encryptedData, tag, plaintextBytes);

        return System.Text.Encoding.UTF8.GetString(plaintextBytes);
    }
}
