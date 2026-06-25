using System.Security.Claims;
using System.Text.Json;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using MidiKaval.Api.Infrastructure.Auth;
using MidiKaval.Api.Infrastructure.Persistence;

namespace MidiKaval.Api.Infrastructure.Middleware;

public sealed class TokenVersionMiddleware(RequestDelegate next)
{
    public async Task InvokeAsync(HttpContext context, AppDbContext db, CancellationToken ct)
    {
        if (context.User.Identity?.IsAuthenticated != true)
        {
            await next(context);
            return;
        }

        var path = context.Request.Path.Value?.TrimEnd('/');
        if (path != null && AuthExcludedPaths.Paths.Contains(path))
        {
            await next(context);
            return;
        }

        var userIdClaim = context.User.FindFirst(ClaimTypes.NameIdentifier)?.Value
            ?? context.User.FindFirst("sub")?.Value;
        if (userIdClaim is null || !Guid.TryParse(userIdClaim, out var userId))
        {
            await next(context);
            return;
        }

        var user = await db.Users
            .AsNoTracking()
            .Where(u => u.Id == userId)
            .Select(u => new { u.TokenVersion })
            .FirstOrDefaultAsync(ct);

        if (user is null)
        {
            context.Response.StatusCode = StatusCodes.Status401Unauthorized;
            context.Response.ContentType = "application/problem+json";
            await context.Response.WriteAsync(
                JsonSerializer.Serialize(new ProblemDetails
                {
                    Status = StatusCodes.Status401Unauthorized,
                    Title = "Unauthorized",
                    Detail = "Your session has been revoked. Please log in again.",
                    Type = "https://tools.ietf.org/html/rfc7235#section-3.1",
                }), ct);
            return;
        }

        var jwtTokenVersionClaim = context.User.FindFirst(AuthClaimTypes.TokenVersion)?.Value;
        if (jwtTokenVersionClaim is null || !int.TryParse(jwtTokenVersionClaim, out var jwtTokenVersion))
        {
            await next(context);
            return;
        }

        if (jwtTokenVersion < user.TokenVersion)
        {
            context.Response.StatusCode = StatusCodes.Status401Unauthorized;
            context.Response.ContentType = "application/problem+json";
            await context.Response.WriteAsync(
                JsonSerializer.Serialize(new ProblemDetails
                {
                    Status = StatusCodes.Status401Unauthorized,
                    Title = "Unauthorized",
                    Detail = "Your session has been revoked. Please log in again.",
                    Type = "https://tools.ietf.org/html/rfc7235#section-3.1",
                }), ct);
            return;
        }

        await next(context);
    }
}
