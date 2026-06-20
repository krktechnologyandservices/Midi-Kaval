namespace MidiKaval.Api.Infrastructure;

public static class HostEnvironmentExtensions
{
    public static bool IsTesting(this IHostEnvironment environment) =>
        string.Equals(environment.EnvironmentName, "Testing", StringComparison.OrdinalIgnoreCase);
}
