using System.Globalization;
using System.Security.Cryptography;
using Microsoft.EntityFrameworkCore.Storage.ValueConversion;

namespace MidiKaval.Api.Infrastructure.Encryption;

/// <summary>
/// AES-256-GCM value converter for decimal? GPS coordinates (Latitude / Longitude).
/// Serializes to string using invariant culture, then encrypts with random nonce.
/// Encrypts with the active key; decrypts using the key version embedded in the ciphertext.
/// </summary>
public sealed class GpsEncryptionConverter : ValueConverter<decimal?, byte[]?>
{
    private const int NonceSize = 12;
    private const int TagSize = 16;

    public GpsEncryptionConverter()
        : base(
            v => Encrypt(v),
            v => Decrypt(v))
    { }

    private static byte[]? Encrypt(decimal? plaintext)
    {
        if (plaintext is null) return null;

        var provider = EncryptionKeyProvider.GetCurrent();
        var key = provider.GetActiveKey();
        var keyVersion = provider.GetActiveKeyVersion();
        var plaintextBytes = System.Text.Encoding.UTF8.GetBytes(
            plaintext.Value.ToString(CultureInfo.InvariantCulture));
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

    private static decimal? Decrypt(byte[]? ciphertext)
    {
        if (ciphertext is null) return null;

        var provider = EncryptionKeyProvider.GetCurrent();
        // Use the version byte from the ciphertext to select the correct historical key
        var key = provider.GetKey(ciphertext[0]);

        var nonce = new byte[NonceSize];
        var tag = new byte[TagSize];
        var encryptedData = new byte[ciphertext.Length - 1 - NonceSize - TagSize];

        Buffer.BlockCopy(ciphertext, 1, nonce, 0, NonceSize);
        Buffer.BlockCopy(ciphertext, 1 + NonceSize, encryptedData, 0, encryptedData.Length);
        Buffer.BlockCopy(ciphertext, 1 + NonceSize + encryptedData.Length, tag, 0, TagSize);

        var plaintextBytes = new byte[encryptedData.Length];
        using var aes = new AesGcm(key, TagSize);
        aes.Decrypt(nonce, encryptedData, tag, plaintextBytes);

        var plaintext = System.Text.Encoding.UTF8.GetString(plaintextBytes);
        return decimal.TryParse(plaintext, NumberStyles.Any, CultureInfo.InvariantCulture, out var result)
            ? result
            : null;
    }
}
