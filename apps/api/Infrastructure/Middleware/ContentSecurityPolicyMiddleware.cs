namespace MidiKaval.Api.Infrastructure.Middleware;

/// <summary>
/// Adds a Content-Security-Policy header to every HTTP response if one is not already set.
/// Registered after UseExceptionHandler so even error responses carry the policy.
/// Implements FR-8 (CSP headers) per AD-04 (two-layer defense: middleware + meta tag).
/// </summary>
public sealed class ContentSecurityPolicyMiddleware(RequestDelegate next)
{
    private const string Policy =
        "default-src 'self'; " +
        "script-src 'self' 'unsafe-inline'; " +
        "style-src 'self' 'unsafe-inline'; " +
        "img-src 'self' data: blob:; " +
        "connect-src 'self'; " +
        "font-src 'self'; " +
        "object-src 'none'; " +
        "base-uri 'self'; " +
        "form-action 'self'; " +
        "frame-ancestors 'self'; " +
        "report-uri /api/v1/security/csp-violation;";

    public async Task InvokeAsync(HttpContext context)
    {
        if (!context.Response.Headers.ContainsKey("Content-Security-Policy"))
        {
            context.Response.Headers["Content-Security-Policy"] = Policy;
        }

        await next(context);
    }
}
