using System.Security.Cryptography;

namespace MidiKaval.Api.Infrastructure.Encryption;

/// <summary>
/// AES-256-GCM encryption for file contents (attachments), using the same master key
/// infrastructure as PiiEncryptionConverter — one key system for both DB fields and
/// stored files, rather than a second one to manage separately.
/// </summary>
public sealed class FileEncryptionService
{
    private const int NonceSize = 12;
    private const int TagSize = 16;

    public byte[] Encrypt(byte[] plaintext)
    {
        var provider = EncryptionKeyProvider.GetCurrent();
        var key = provider.GetActiveKey();
        var keyVersion = provider.GetActiveKeyVersion();
        var nonce = RandomNumberGenerator.GetBytes(NonceSize);
        var ciphertext = new byte[plaintext.Length];
        var tag = new byte[TagSize];

        using var aes = new AesGcm(key, TagSize);
        aes.Encrypt(nonce, plaintext, ciphertext, tag);

        // Format: [1 byte version][12 byte nonce][ciphertext][16 byte tag]
        var result = new byte[1 + NonceSize + ciphertext.Length + TagSize];
        result[0] = keyVersion;
        Buffer.BlockCopy(nonce, 0, result, 1, NonceSize);
        Buffer.BlockCopy(ciphertext, 0, result, 1 + NonceSize, ciphertext.Length);
        Buffer.BlockCopy(tag, 0, result, 1 + NonceSize + ciphertext.Length, TagSize);

        return result;
    }

    public byte[] Decrypt(byte[] ciphertext)
    {
        var provider = EncryptionKeyProvider.GetCurrent();
        var key = provider.GetKey(ciphertext[0]);

        var nonce = new byte[NonceSize];
        var tag = new byte[TagSize];
        var encryptedData = new byte[ciphertext.Length - 1 - NonceSize - TagSize];

        Buffer.BlockCopy(ciphertext, 1, nonce, 0, NonceSize);
        Buffer.BlockCopy(ciphertext, 1 + NonceSize, encryptedData, 0, encryptedData.Length);
        Buffer.BlockCopy(ciphertext, 1 + NonceSize + encryptedData.Length, tag, 0, TagSize);

        var plaintext = new byte[encryptedData.Length];
        using var aes = new AesGcm(key, TagSize);
        aes.Decrypt(nonce, encryptedData, tag, plaintext);

        return plaintext;
    }
}
