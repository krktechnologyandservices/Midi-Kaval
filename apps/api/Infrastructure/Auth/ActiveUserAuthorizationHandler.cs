using Microsoft.AspNetCore.Authorization;

namespace MidiKaval.Api.Infrastructure.Auth;

public sealed class ActiveUserRequirement : IAuthorizationRequirement;

public sealed class ActiveUserAuthorizationHandler(IHttpContextAccessor httpContextAccessor)
    : AuthorizationHandler<ActiveUserRequirement>
{
    protected override Task HandleRequirementAsync(
        AuthorizationHandlerContext context,
        ActiveUserRequirement requirement)
    {
        var httpContext = httpContextAccessor.HttpContext;
        if (httpContext is null)
        {
            return Task.CompletedTask;
        }

        if (httpContext.Items.ContainsKey(InactiveUserAuthConstants.InactiveUserItemKey))
        {
            return Task.CompletedTask;
        }

        context.Succeed(requirement);
        return Task.CompletedTask;
    }
}
