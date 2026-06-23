using System.Security.Cryptography;
using Microsoft.Extensions.Options;

namespace MidiKaval.Api.Infrastructure.Encryption;

public sealed class EncryptionKeyProvider
{
    private readonly Dictionary<byte, byte[]> _keyRing;
    private readonly byte _activeKeyVersion;

    // Static instance for use by EF Core value converters (which cannot use DI).
    // Initialized explicitly via SetCurrent() during DI registration to avoid
    // constructor-time contamination and cross-test races.
    private static EncryptionKeyProvider? _staticInstance;
    private static readonly object _lock = new();

    public EncryptionKeyProvider(IOptions<EncryptionKeyProviderOptions> options)
    {
        var opts = options.Value;
        _activeKeyVersion = opts.ActiveKeyVersion;
        _keyRing = new Dictionary<byte, byte[]>();

        if (opts.Versions.Count > 0)
        {
            // Key ring mode: load all versions from config
            foreach (var (version, base64Key) in opts.Versions)
            {
                var key = Convert.FromBase64String(base64Key);
                if (key.Length != 32)
                {
                    throw new InvalidOperationException(
                        $"Encryption key for version {version} must be exactly 32 bytes (256 bits) for AES-256.");
                }
                _keyRing[version] = key;
            }
        }
        else
        {
            // Fallback: single key loaded from provider source (legacy mode)
            var key = opts.Provider switch
            {
                KeyProvider.Environment => LoadFromEnvironment(opts),
                KeyProvider.KeyVault => LoadFromKeyVault(opts),
                KeyProvider.UserSecrets or _ => LoadFromUserSecrets(opts),
            };

            if (key.Length != 32)
            {
                throw new InvalidOperationException("Encryption key must be exactly 32 bytes (256 bits) for AES-256.");
            }

            _keyRing[0] = key;
        }

        if (!_keyRing.ContainsKey(_activeKeyVersion))
        {
            throw new InvalidOperationException(
                $"ActiveKeyVersion {_activeKeyVersion} not found in key ring. " +
                $"Available versions: [{string.Join(", ", _keyRing.Keys)}]");
        }
    }

    /// <summary>
    /// Retrieves the key for the given version from the key ring.
    /// </summary>
    public byte[] GetKey(byte version) =>
        _keyRing.TryGetValue(version, out var key)
            ? key
            : throw new InvalidOperationException($"Key version {version} not found in key ring.");

    /// <summary>
    /// Returns the active encryption key (for encrypting new data).
    /// </summary>
    public byte[] GetActiveKey() => GetKey(_activeKeyVersion);

    public byte GetActiveKeyVersion() => _activeKeyVersion;

    /// <summary>
    /// Explicitly sets the static instance for EF Core value converters.
    /// Called once during DI registration — not in the constructor — to avoid
    /// cross-test contamination when multiple DI containers exist in the same process.
    /// </summary>
    public static void SetCurrent(EncryptionKeyProvider provider)
    {
        lock (_lock)
        {
            _staticInstance = provider;
        }
    }

    /// <summary>
    /// Static accessor for EF Core value converters that cannot use constructor injection.
    /// Must be called after SetCurrent() has been invoked during startup.
    /// </summary>
    public static EncryptionKeyProvider GetCurrent()
    {
        var instance = _staticInstance;
        if (instance is null)
        {
            throw new InvalidOperationException(
                "EncryptionKeyProvider has not been initialized. " +
                "Ensure AddMidiKavalSecurity is called during application startup.");
        }
        return instance;
    }

    private static byte[] LoadFromEnvironment(EncryptionKeyProviderOptions opts)
    {
        var keyString = opts.MasterKey
            ?? Environment.GetEnvironmentVariable("ENCRYPTION_KEY_MASTER")
            ?? throw new InvalidOperationException(
                "EncryptionKey:MasterKey configuration or ENCRYPTION_KEY_MASTER environment variable is required " +
                "when Provider is 'Environment'.");

        return Convert.FromBase64String(keyString);
    }

    private static byte[] LoadFromKeyVault(EncryptionKeyProviderOptions opts)
    {
        if (string.IsNullOrWhiteSpace(opts.KeyVaultUrl))
        {
            throw new InvalidOperationException("KeyVaultUrl is required when Provider is 'KeyVault'.");
        }

        // Future: Azure Key Vault / AWS KMS integration
        // For now, fall back to environment variable
        return LoadFromEnvironment(opts);
    }

    private static byte[] LoadFromUserSecrets(EncryptionKeyProviderOptions opts)
    {
        if (string.IsNullOrWhiteSpace(opts.MasterKey))
        {
            throw new InvalidOperationException(
                "EncryptionKey:MasterKey must be set via dotnet user-secrets in development. " +
                "Run: dotnet user-secrets set \"EncryptionKey:MasterKey\" \"<base64-32-byte-key>\"");
        }

        return Convert.FromBase64String(opts.MasterKey);
    }
}
