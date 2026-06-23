using MidiKaval.Api.Infrastructure.Encryption;

namespace MidiKaval.Api.Infrastructure;

public static class SecurityServiceCollectionExtensions
{
    public static IServiceCollection AddMidiKavalSecurity(
        this IServiceCollection services,
        IConfiguration configuration)
    {
        services.Configure<EncryptionKeyProviderOptions>(
            configuration.GetSection(EncryptionKeyProviderOptions.SectionName));

        // Register as singleton and explicitly initialize the static accessor
        // for EF Core value converters (which cannot use constructor injection).
        services.AddSingleton<EncryptionKeyProvider>(sp =>
        {
            var options = sp.GetRequiredService<Microsoft.Extensions.Options.IOptions<EncryptionKeyProviderOptions>>();
            var provider = new EncryptionKeyProvider(options);
            EncryptionKeyProvider.SetCurrent(provider);
            return provider;
        });

        return services;
    }
}
