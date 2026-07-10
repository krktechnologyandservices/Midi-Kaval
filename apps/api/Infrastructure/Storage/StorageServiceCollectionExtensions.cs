namespace MidiKaval.Api.Infrastructure.Storage;

public static class StorageServiceCollectionExtensions
{
    public static IServiceCollection AddBlobStorage(
        this IServiceCollection services,
        IConfiguration configuration)
    {
        services.Configure<B2StorageOptions>(configuration.GetSection(B2StorageOptions.SectionName));
        services.AddSingleton<IBlobStorageService, B2BlobStorageService>();
        return services;
    }
}
