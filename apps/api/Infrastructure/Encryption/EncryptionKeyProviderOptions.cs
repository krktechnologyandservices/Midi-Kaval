using System.ComponentModel.DataAnnotations;

namespace MidiKaval.Api.Infrastructure.Encryption;

public sealed class EncryptionKeyProviderOptions
{
    public const string SectionName = "EncryptionKey";

    public KeyProvider Provider { get; set; } = KeyProvider.UserSecrets;

    /// <summary>
    /// Key ring: maps key version (byte) to base64-encoded 32-byte AES-256 key.
    /// On rotation, add a new entry and update <see cref="ActiveKeyVersion"/>.
    /// Example: { 0 → "aGVsbG8=...", 1 → "d29ybGQ=..." }
    /// </summary>
    public Dictionary<byte, string> Versions { get; set; } = new();

    /// <summary>
    /// Fallback MasterKey for single-key configurations (treated as version 0).
    /// Only used when <see cref="Versions"/> is empty.
    /// </summary>
    public string? MasterKey { get; set; }

    public string? KeyVaultUrl { get; set; }

    public byte ActiveKeyVersion { get; set; } = 0;
}

public enum KeyProvider
{
    UserSecrets,
    KeyVault,
    Environment,
}
