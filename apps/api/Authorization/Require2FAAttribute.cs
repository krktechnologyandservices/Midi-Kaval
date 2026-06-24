using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Filters;
using Microsoft.EntityFrameworkCore;
using MidiKaval.Api.Infrastructure.Persistence;

namespace MidiKaval.Api.Authorization;

[AttributeUsage(AttributeTargets.Class | AttributeTargets.Method)]
public sealed class Require2FAAttribute : Attribute, IAsyncAuthorizationFilter
{
    public async Task OnAuthorizationAsync(AuthorizationFilterContext context)
    {
        var user = context.HttpContext.User;
        if (user.Identity?.IsAuthenticated != true)
        {
            return; // Let [Authorize] handle unauthenticated
        }

        var userIdClaim = user.FindFirst(System.Security.Claims.ClaimTypes.NameIdentifier)
            ?? user.FindFirst("sub");
        if (userIdClaim is null || !Guid.TryParse(userIdClaim.Value, out var userId))
        {
            context.Result = new ForbidResult();
            return;
        }

        try
        {
            var db = context.HttpContext.RequestServices.GetRequiredService<AppDbContext>();
            var enrolledAt = await db.Users
                .AsNoTracking()
                .Where(u => u.Id == userId)
                .Select(u => u.TotpEnrolledAt)
                .FirstOrDefaultAsync();

            if (enrolledAt is null)
            {
                context.Result = new ObjectResult(new Microsoft.AspNetCore.Mvc.ProblemDetails
                {
                    Status = StatusCodes.Status403Forbidden,
                    Title = "Two-Factor Authentication Required",
                    Detail = "Two-factor authentication is required to perform this action. Please enroll in 2FA first.",
                    Type = "https://tools.ietf.org/html/rfc7231#section-6.5.3"
                })
                {
                    StatusCode = StatusCodes.Status403Forbidden
                };
            }
        }
        catch (Exception)
        {
            // If DB is unreachable, degrade to a 403 rather than a raw 500
            context.Result = new ObjectResult(new Microsoft.AspNetCore.Mvc.ProblemDetails
            {
                Status = StatusCodes.Status403Forbidden,
                Title = "Two-Factor Authentication Unavailable",
                Detail = "Cannot verify two-factor authentication status at this time. Please try again.",
                Type = "https://tools.ietf.org/html/rfc7231#section-6.5.3"
            })
            {
                StatusCode = StatusCodes.Status403Forbidden
            };
        }
    }
}
