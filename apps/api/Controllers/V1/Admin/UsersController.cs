using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.RateLimiting;
using MidiKaval.Api.Authorization;
using MidiKaval.Api.Domain.Entities;
using MidiKaval.Api.Domain.RoleManagement;
using MidiKaval.Api.Infrastructure;
using MidiKaval.Api.Infrastructure.Auth;
using MidiKaval.Api.Models;
using MidiKaval.Api.Models.Admin;

namespace MidiKaval.Api.Controllers.V1.Admin;

[ApiController]
[Authorize(Policy = Policies.DirectorOnly)]
[Require2FA]
[Route("api/v1/admin")]
public class UsersController(
    UserManagementService userManagementService) : ControllerBase
{
    private static readonly string[] ValidSortByFields = ["name", "email", "role", "status", "createdAt"];

    [HttpGet("users")]
    [EnableRateLimiting("vendor-read")]
    [ProducesResponseType(typeof(ApiResponse<AdminUserListResult>), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status403Forbidden)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status422UnprocessableEntity)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status429TooManyRequests)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status500InternalServerError)]
    public async Task<IActionResult> GetUsers(
        [FromQuery] int page = 1,
        [FromQuery] int pageSize = 25,
        [FromQuery] string? search = null,
        [FromQuery] string? roles = null,
        [FromQuery] string? status = null,
        [FromQuery] string? sortBy = null,
        [FromQuery] bool sortDesc = true,
        CancellationToken cancellationToken = default)
    {
        var orgIdClaim = User.FindFirst(AuthClaimTypes.OrganisationId)?.Value;
        if (orgIdClaim is null || !Guid.TryParse(orgIdClaim, out var organisationId))
        {
            return Unauthorized(new ProblemDetails
            {
                Status = StatusCodes.Status401Unauthorized,
                Title = "Invalid Token",
                Detail = "Organisation claim missing from token.",
                Type = "https://tools.ietf.org/html/rfc7235#section-3.1",
            });
        }

        // Validate roles param
        if (!string.IsNullOrWhiteSpace(roles))
        {
            var roleList = roles.Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
            foreach (var role in roleList)
            {
                if (!UserRoles.IsValid(role))
                {
                    return UnprocessableEntity(new ProblemDetails
                    {
                        Status = StatusCodes.Status422UnprocessableEntity,
                        Title = "Invalid Role",
                        Detail = $"'{role}' is not a valid role. Valid roles: {string.Join(", ", UserRoles.All)}.",
                        Type = "https://tools.ietf.org/html/rfc4918#section-11.2",
                    });
                }
            }
        }

        // Validate status param
        if (!string.IsNullOrWhiteSpace(status))
        {
            var normalizedStatus = status.ToLowerInvariant();
            if (normalizedStatus != "active" && normalizedStatus != "suspended")
            {
                return UnprocessableEntity(new ProblemDetails
                {
                    Status = StatusCodes.Status422UnprocessableEntity,
                    Title = "Invalid Status Filter",
                    Detail = "Status must be 'active' or 'suspended'.",
                    Type = "https://tools.ietf.org/html/rfc4918#section-11.2",
                });
            }
        }

        // Validate sortBy param
        if (!string.IsNullOrWhiteSpace(sortBy) && !ValidSortByFields.Contains(sortBy))
        {
            return UnprocessableEntity(new ProblemDetails
            {
                Status = StatusCodes.Status422UnprocessableEntity,
                Title = "Invalid Sort Field",
                Detail = $"'{sortBy}' is not a valid sort field. Valid fields: {string.Join(", ", ValidSortByFields)}.",
                Type = "https://tools.ietf.org/html/rfc4918#section-11.2",
            });
        }

        var result = await userManagementService.GetUserListAsync(
            organisationId, page, pageSize, search, roles, status, sortBy, sortDesc, cancellationToken);

        return Ok(new ApiResponse<AdminUserListResult>(
            result,
            new ApiMeta
            {
                RequestId = ResolveRequestId(),
                TotalCount = result.TotalCount,
                Page = result.Page,
                PageSize = result.PageSize,
            }));
    }

    private string ResolveRequestId() =>
        HttpContext.Items[RequestIdMiddleware.RequestIdItemKey] as string
            ?? HttpContext.TraceIdentifier;
}
