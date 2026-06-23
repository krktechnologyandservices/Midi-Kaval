using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Options;

namespace MidiKaval.Api.Infrastructure;

public static class DataRateLimitServiceCollectionExtensions
{
    public static IServiceCollection AddMidiKavalDataRateLimiting(
        this IServiceCollection services,
        IConfiguration configuration)
    {
        services.AddOptions<DataRateLimitOptions>()
            .Bind(configuration.GetSection(DataRateLimitOptions.SectionName))
            .ValidateOnStart();
        services.AddSingleton<IValidateOptions<DataRateLimitOptions>, DataRateLimitOptionsValidator>();

        return services;
    }
}
