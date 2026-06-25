namespace MidiKaval.Api.Infrastructure.Middleware;

internal static class AuthExcludedPaths
{
    internal static readonly HashSet<string> Paths =
    [
        "/health",
        "/swagger",
        "/api/v1/auth/login",
        "/api/v1/auth/verify-otp",
        "/api/v1/auth/refresh",
        "/api/v1/auth/logout",
        "/api/v1/auth/activate",
    ];
}
