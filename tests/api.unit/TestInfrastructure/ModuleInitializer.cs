using System.Runtime.CompilerServices;
using Microsoft.Extensions.Options;
using MidiKaval.Api.Infrastructure.Encryption;

namespace MidiKaval.Api.UnitTests.TestInfrastructure;

/// <summary>
/// Module initializer that runs when the test assembly is loaded.
/// Initializes the EncryptionKeyProvider static instance so that EF Core value converters
/// (SearchablePiiEncryptionConverter) work without requiring per-class initialization.
/// </summary>
internal static class ModuleInitializer
{
    [ModuleInitializer]
    internal static void Initialize()
    {
        var options = Options.Create(new EncryptionKeyProviderOptions
        {
            Versions = new Dictionary<byte, string>
            {
                [0] = Convert.ToBase64String(new byte[32]) // Zero-filled 32-byte key for tests
            }
        });
        var provider = new EncryptionKeyProvider(options);
        EncryptionKeyProvider.SetCurrent(provider);
    }
}
