using MidiKaval.Api.Models.Auth;

namespace MidiKaval.Api.Infrastructure.Auth;

public static class AuthTokenHelpers
{
    public const string RefreshCookieName = "refresh_token";

    public static string? ReadRefreshToken(HttpRequest request, RefreshRequest? body = null)
    {
        if (!string.IsNullOrWhiteSpace(body?.RefreshToken))
        {
            return body.RefreshToken;
        }

        return ReadRefreshTokenFromCookie(request);
    }

    public static string? ReadRefreshToken(HttpRequest request, LogoutRequest? body)
    {
        if (!string.IsNullOrWhiteSpace(body?.RefreshToken))
        {
            return body.RefreshToken;
        }

        return ReadRefreshTokenFromCookie(request);
    }

    private static string? ReadRefreshTokenFromCookie(HttpRequest request)
    {
        if (request.Cookies.TryGetValue(RefreshCookieName, out var cookieToken)
            && !string.IsNullOrWhiteSpace(cookieToken))
        {
            return cookieToken;
        }

        return null;
    }
}
