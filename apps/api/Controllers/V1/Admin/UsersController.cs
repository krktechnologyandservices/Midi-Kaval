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
    UserManagementService userManagementService,
    LastDirectorGuard lastDirectorGuard,
    TwoFactorService twoFactorService,
    AdminTwoFactorService adminTwoFactorService) : ControllerBase
{
    private static readonly string[] ValidSortByFields = ["name", "email", "role", "status", "createdAt"];
    private static readonly string[] SuspendConflictMessages = ["User is already suspended", "Cannot suspend a deleted user"];
    private static readonly string[] ReactivateConflictMessages = ["User is not suspended", "Cannot reactivate a deleted user"];
    private static readonly string[] DeleteConflictMessages = ["User has already been deleted"];

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
            if (normalizedStatus != "active" && normalizedStatus != "suspended" && normalizedStatus != "deleted")
            {
                return UnprocessableEntity(new ProblemDetails
                {
                    Status = StatusCodes.Status422UnprocessableEntity,
                    Title = "Invalid Status Filter",
                    Detail = "Status must be 'active', 'suspended', or 'deleted'.",
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

    [HttpPost("users/{id:guid}/suspend")]
    [EnableRateLimiting("data-write")]
    [ProducesResponseType(typeof(ApiResponse<SuspendUserResponse>), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status403Forbidden)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status404NotFound)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status409Conflict)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status422UnprocessableEntity)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status429TooManyRequests)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status500InternalServerError)]
    public async Task<IActionResult> SuspendUser(
        Guid id,
        [FromBody] SuspendUserRequest request,
        CancellationToken cancellationToken)
    {
        if (request is null)
            return BadRequest(new ProblemDetails
            {
                Status = StatusCodes.Status400BadRequest,
                Title = "Request body required",
                Detail = "The request body must contain a reason field.",
                Type = "https://tools.ietf.org/html/rfc7231#section-6.5.1",
            });

        if (!ModelState.IsValid)
            return UnprocessableEntity(ModelState);

        if (!TryResolveOrganisationId(out var organisationId, out var orgError))
            return orgError!;

        if (!TryResolveActorUserId(out var actorUserId, out var actorError))
            return actorError!;

        try
        {
            var ipAddress = HttpContext.Connection.RemoteIpAddress?.ToString();

            var result = await userManagementService.SuspendAsync(
                organisationId!.Value, actorUserId!.Value, id, request.Reason, ipAddress, cancellationToken);

            return Ok(new ApiResponse<SuspendUserResponse>(
                result,
                new ApiMeta { RequestId = ResolveRequestId() }));
        }
        catch (KeyNotFoundException ex)
        {
            return NotFound(new ProblemDetails
            {
                Status = StatusCodes.Status404NotFound,
                Title = "User Not Found",
                Detail = ex.Message,
                Type = "https://tools.ietf.org/html/rfc7231#section-6.5.4",
            });
        }
        catch (InvalidOperationException ex)
        {
            if (SuspendConflictMessages.Any(m => ex.Message.Contains(m)))
            {
                return Conflict(new ProblemDetails
                {
                    Status = StatusCodes.Status409Conflict,
                    Title = "Conflict",
                    Detail = ex.Message,
                    Type = "https://tools.ietf.org/html/rfc7231#section-6.5.8",
                });
            }

            return BadRequest(new ProblemDetails
            {
                Status = StatusCodes.Status400BadRequest,
                Title = "Invalid Operation",
                Detail = ex.Message,
                Type = "https://tools.ietf.org/html/rfc7231#section-6.5.1",
            });
        }
    }

    [HttpPost("users/{id:guid}/reactivate")]
    [EnableRateLimiting("data-write")]
    [ProducesResponseType(typeof(ApiResponse<ReactivateUserResponse>), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status403Forbidden)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status404NotFound)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status409Conflict)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status422UnprocessableEntity)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status429TooManyRequests)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status500InternalServerError)]
    public async Task<IActionResult> ReactivateUser(
        Guid id,
        CancellationToken cancellationToken)
    {
        if (!TryResolveOrganisationId(out var organisationId, out var orgError))
            return orgError!;

        if (!TryResolveActorUserId(out var actorUserId, out var actorError))
            return actorError!;

        try
        {
            var ipAddress = HttpContext.Connection.RemoteIpAddress?.ToString();

            var result = await userManagementService.ReactivateAsync(
                organisationId!.Value, actorUserId!.Value, id, ipAddress, cancellationToken);

            return Ok(new ApiResponse<ReactivateUserResponse>(
                result,
                new ApiMeta { RequestId = ResolveRequestId() }));
        }
        catch (KeyNotFoundException ex)
        {
            return NotFound(new ProblemDetails
            {
                Status = StatusCodes.Status404NotFound,
                Title = "User Not Found",
                Detail = ex.Message,
                Type = "https://tools.ietf.org/html/rfc7231#section-6.5.4",
            });
        }
        catch (InvalidOperationException ex)
        {
            if (ReactivateConflictMessages.Any(m => ex.Message.Contains(m)))
            {
                return Conflict(new ProblemDetails
                {
                    Status = StatusCodes.Status409Conflict,
                    Title = "Conflict",
                    Detail = ex.Message,
                    Type = "https://tools.ietf.org/html/rfc7231#section-6.5.8",
                });
            }

            return BadRequest(new ProblemDetails
            {
                Status = StatusCodes.Status400BadRequest,
                Title = "Invalid Operation",
                Detail = ex.Message,
                Type = "https://tools.ietf.org/html/rfc7231#section-6.5.1",
            });
        }
    }

    [HttpDelete("users/{id:guid}")]
    [EnableRateLimiting("data-write")]
    [ProducesResponseType(typeof(ApiResponse<DeleteUserResponse>), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status403Forbidden)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status404NotFound)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status409Conflict)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status422UnprocessableEntity)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status429TooManyRequests)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status500InternalServerError)]
    public async Task<IActionResult> DeleteUser(
        Guid id,
        [FromBody] DeleteUserRequest request,
        CancellationToken cancellationToken)
    {
        if (request is null)
            return BadRequest(new ProblemDetails
            {
                Status = StatusCodes.Status400BadRequest,
                Title = "Request body required",
                Detail = "The request body must contain a confirmationEmail field.",
                Type = "https://tools.ietf.org/html/rfc7231#section-6.5.1",
            });

        if (!ModelState.IsValid)
            return UnprocessableEntity(ModelState);

        if (!TryResolveOrganisationId(out var organisationId, out var orgError))
            return orgError!;

        if (!TryResolveActorUserId(out var actorUserId, out var actorError))
            return actorError!;

        try
        {
            var ipAddress = HttpContext.Connection.RemoteIpAddress?.ToString();

            var result = await userManagementService.DeleteAsync(
                organisationId!.Value, actorUserId!.Value, id, request.ConfirmationEmail, ipAddress, cancellationToken);

            return Ok(new ApiResponse<DeleteUserResponse>(
                result,
                new ApiMeta { RequestId = ResolveRequestId() }));
        }
        catch (KeyNotFoundException ex)
        {
            return NotFound(new ProblemDetails
            {
                Status = StatusCodes.Status404NotFound,
                Title = "User Not Found",
                Detail = ex.Message,
                Type = "https://tools.ietf.org/html/rfc7231#section-6.5.4",
            });
        }
        catch (InvalidOperationException ex)
        {
            if (DeleteConflictMessages.Any(m => ex.Message.Contains(m)))
            {
                return Conflict(new ProblemDetails
                {
                    Status = StatusCodes.Status409Conflict,
                    Title = "Conflict",
                    Detail = ex.Message,
                    Type = "https://tools.ietf.org/html/rfc7231#section-6.5.8",
                });
            }

            return BadRequest(new ProblemDetails
            {
                Status = StatusCodes.Status400BadRequest,
                Title = "Invalid Operation",
                Detail = ex.Message,
                Type = "https://tools.ietf.org/html/rfc7231#section-6.5.1",
            });
        }
    }

    [HttpGet("users/{id:guid}/is-last-director")]
    [EnableRateLimiting("data-read")]
    [ProducesResponseType(typeof(object), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status403Forbidden)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status404NotFound)]
    public async Task<IActionResult> IsLastDirector(Guid id, CancellationToken cancellationToken)
    {
        if (!TryResolveOrganisationId(out var organisationId, out var orgError))
            return orgError!;

        var result = await lastDirectorGuard.IsLastActiveDirectorAsync(organisationId!.Value, id, cancellationToken);
        return Ok(new { isLastDirector = result });
    }

    [HttpPost("users/{id:guid}/reset-2fa")]
    [EnableRateLimiting("data-write")]
    [ProducesResponseType(typeof(ApiResponse<ResetTwoFactorResponse>), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status403Forbidden)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status404NotFound)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status422UnprocessableEntity)]
    public async Task<IActionResult> ResetTwoFactor(Guid id, CancellationToken ct)
    {
        if (!TryResolveOrganisationId(out var organisationId, out var orgError))
            return orgError!;

        if (!TryResolveActorUserId(out var actorUserId, out var actorError))
            return actorError!;

        try
        {
            var ipAddress = HttpContext.Connection.RemoteIpAddress?.ToString();
            var actorRole = User.FindFirst(System.Security.Claims.ClaimTypes.Role)?.Value;
            var result = await adminTwoFactorService.ResetTwoFactorAsync(
                actorUserId!.Value, id, organisationId!.Value, actorRole, ipAddress, ct);

            return Ok(new ApiResponse<ResetTwoFactorResponse>(
                result,
                new ApiMeta { RequestId = ResolveRequestId() }));
        }
        catch (KeyNotFoundException)
        {
            return NotFound(new ProblemDetails
            {
                Status = StatusCodes.Status404NotFound,
                Title = "User Not Found",
                Detail = "The specified user was not found.",
            });
        }
        catch (InvalidOperationException ex)
        {
            return UnprocessableEntity(new ProblemDetails
            {
                Status = StatusCodes.Status422UnprocessableEntity,
                Title = "Cannot Reset 2FA",
                Detail = ex.Message,
                Type = "https://tools.ietf.org/html/rfc4918#section-11.2",
            });
        }
    }

    private bool TryResolveOrganisationId(out Guid? organisationId, out IActionResult? error)
    {
        var claim = User.FindFirst(AuthClaimTypes.OrganisationId)?.Value;
        if (claim is null || !Guid.TryParse(claim, out var parsed))
        {
            organisationId = null;
            error = Unauthorized(new ProblemDetails
            {
                Status = StatusCodes.Status401Unauthorized,
                Title = "Invalid Token",
                Detail = "Organisation claim missing from token.",
                Type = "https://tools.ietf.org/html/rfc7235#section-3.1",
            });
            return false;
        }
        organisationId = parsed;
        error = null;
        return true;
    }

    private bool TryResolveActorUserId(out Guid? actorUserId, out IActionResult? error)
    {
        var claim = User.FindFirst(System.Security.Claims.ClaimTypes.NameIdentifier)?.Value;
        if (claim is null || !Guid.TryParse(claim, out var parsed))
        {
            actorUserId = null;
            error = Unauthorized(new ProblemDetails
            {
                Status = StatusCodes.Status401Unauthorized,
                Title = "Invalid Token",
                Detail = "User identifier claim missing from token.",
                Type = "https://tools.ietf.org/html/rfc7235#section-3.1",
            });
            return false;
        }
        actorUserId = parsed;
        error = null;
        return true;
    }

    private string ResolveRequestId() =>
        HttpContext.Items[RequestIdMiddleware.RequestIdItemKey] as string
            ?? HttpContext.TraceIdentifier;
}
