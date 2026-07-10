namespace MidiKaval.Api.Infrastructure;

public static class RedisConnectionStringNormalizer
{
    // Render's fromService.connectionString returns a "redis://[:password@]host:port" URI,
    // but StackExchange.Redis's ConfigurationOptions.Parse expects its own
    // "host:port,password=...,ssl=true" format and doesn't understand a URI scheme prefix —
    // passing the raw URI through mis-tokenizes the endpoint (observed as a doubled port,
    // e.g. "host:6379:6379"). Local dev connection strings ("localhost:6379") are already
    // in the expected format, so this is a no-op for them.
    public static string Normalize(string connectionString)
    {
        if (!connectionString.StartsWith("redis://", StringComparison.OrdinalIgnoreCase) &&
            !connectionString.StartsWith("rediss://", StringComparison.OrdinalIgnoreCase))
        {
            return connectionString;
        }

        var uri = new Uri(connectionString);
        var useSsl = uri.Scheme.Equals("rediss", StringComparison.OrdinalIgnoreCase);
        var port = uri.Port > 0 ? uri.Port : 6379;

        var options = $"{uri.Host}:{port}";

        if (!string.IsNullOrEmpty(uri.UserInfo))
        {
            var parts = uri.UserInfo.Split(':', 2);
            var password = parts.Length > 1 ? parts[1] : parts[0];
            if (!string.IsNullOrEmpty(password))
            {
                options += $",password={Uri.UnescapeDataString(password)}";
            }
        }

        if (useSsl)
        {
            options += ",ssl=true";
        }

        return options;
    }
}
