namespace MidiKaval.Api.Infrastructure.Persistence;

public static class PostgresConnectionStringNormalizer
{
    // Render's fromDatabase.connectionString returns a "postgres://user:pass@host:port/db" URI,
    // but Npgsql (both EF Core's provider and Hangfire.PostgreSql) only understands the
    // traditional "Host=...;Port=...;Database=...;Username=...;Password=..." keyword format.
    // Local dev connection strings are already in that format, so this is a no-op for them.
    public static string Normalize(string connectionString)
    {
        if (!connectionString.StartsWith("postgres://", StringComparison.OrdinalIgnoreCase) &&
            !connectionString.StartsWith("postgresql://", StringComparison.OrdinalIgnoreCase))
        {
            return connectionString;
        }

        var uri = new Uri(connectionString);
        var userInfo = uri.UserInfo.Split(':', 2);
        var username = Uri.UnescapeDataString(userInfo[0]);
        var password = userInfo.Length > 1 ? Uri.UnescapeDataString(userInfo[1]) : string.Empty;
        var database = uri.AbsolutePath.TrimStart('/');
        var port = uri.Port > 0 ? uri.Port : 5432;

        return $"Host={uri.Host};Port={port};Database={database};Username={username};Password={password};SSL Mode=Prefer;Trust Server Certificate=true";
    }
}
