using System.Text.Json;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Authorization.Policy;
using Microsoft.AspNetCore.Mvc;

namespace MidiKaval.Api.Infrastructure.Auth;

public static class InactiveUserAuthConstants
{
    public const string InactiveUserItemKey = "AuthInactiveUser";
}

public static class AuthorizationProblemTypes
{
    public const string Forbidden = "https://tools.ietf.org/html/rfc7231#section-6.5.3";
}

public sealed class InactiveUserAuthorizationMiddlewareResultHandler : IAuthorizationMiddlewareResultHandler
{
    private readonly AuthorizationMiddlewareResultHandler _defaultHandler = new();

    public async Task HandleAsync(
        RequestDelegate next,
        HttpContext context,
        AuthorizationPolicy policy,
        PolicyAuthorizationResult authorizeResult)
    {
        if (context.Items.ContainsKey(InactiveUserAuthConstants.InactiveUserItemKey)
            && !authorizeResult.Succeeded)
        {
            await WriteForbiddenProblemAsync(context, AuthService.DeactivatedMessage);
            return;
        }

        if (!authorizeResult.Succeeded && authorizeResult.Forbidden)
        {
            await WriteForbiddenProblemAsync(context, Policies.ForbiddenByRoleMessage);
            return;
        }

        await _defaultHandler.HandleAsync(next, context, policy, authorizeResult);
    }

    private static async Task WriteForbiddenProblemAsync(HttpContext context, string detail)
    {
        context.Response.StatusCode = StatusCodes.Status403Forbidden;
        context.Response.ContentType = "application/problem+json";

        var problem = new ProblemDetails
        {
            Status = StatusCodes.Status403Forbidden,
            Title = "Forbidden",
            Detail = detail,
            Type = AuthorizationProblemTypes.Forbidden,
        };

        await JsonSerializer.SerializeAsync(
            context.Response.Body,
            problem,
            new JsonSerializerOptions(JsonSerializerDefaults.Web));
    }
}
