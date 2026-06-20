namespace MidiKaval.Api.Infrastructure;

public static class CorsServiceCollectionExtensions
{
    public static IServiceCollection AddMidiKavalCors(
        this IServiceCollection services,
        IConfiguration configuration,
        IWebHostEnvironment environment)
    {
        if (environment.IsTesting())
        {
            return services;
        }

        var corsOptions = configuration.GetSection(CorsOptions.SectionName).Get<CorsOptions>()
            ?? new CorsOptions();

        var origins = corsOptions.AllowedOrigins;
        if (origins.Length == 0)
        {
            if (environment.IsDevelopment())
            {
                origins = ["http://localhost:4200"];
            }
            else
            {
                throw new InvalidOperationException(
                    "Cors:AllowedOrigins must contain at least one origin outside Development and Testing.");
            }
        }

        services.AddCors(options =>
        {
            options.AddPolicy(CorsOptions.WebClientPolicy, policy =>
            {
                policy.WithOrigins(origins)
                    .AllowAnyHeader()
                    .AllowAnyMethod()
                    .AllowCredentials();
            });
        });

        return services;
    }
}
