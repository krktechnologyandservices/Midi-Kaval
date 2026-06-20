namespace MidiKaval.Api.Infrastructure;

public sealed class CorsOptions
{
    public const string SectionName = "Cors";
    public const string WebClientPolicy = "WebClient";

    public string[] AllowedOrigins { get; set; } = [];
}
