using System.Security.Claims;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.RateLimiting;
using MidiKaval.Api.Authorization;
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
public class AdminTwoFactorController(
    AdminTwoFactorService adminTwoFactorService,
    ILogger<AdminTwoFactorController> logger) : ControllerBase
{
    [HttpPost("users/{id:guid}/reset-2fa")]
    [Authorize(Policy = Policies.CoordinatorOrAbove)]
    [EnableRateLimiting("data-write")]
    [ProducesResponseType(typeof(ApiResponse<ResetTwoFactorResponse>), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status403Forbidden)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status404NotFound)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status422UnprocessableEntity)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status429TooManyRequests)]
    public async Task<IActionResult> ResetTwoFactor(Guid id, CancellationToken ct)
    {
        if (!TryResolveOrganisationId(out var organisationId, out var orgError))
            return orgError!;

        if (!TryResolveActorUserId(out var actorUserId, out var actorError))
            return actorError!;

        var actorRole = User.FindFirstValue(ClaimTypes.Role);

        try
        {
            var ipAddress = HttpContext.Connection.RemoteIpAddress?.ToString();
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

    [HttpPost("users/{id:guid}/send-2fa-reminder")]
    [EnableRateLimiting("data-write")]
    [ProducesResponseType(typeof(ApiResponse<object>), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status403Forbidden)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status404NotFound)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status422UnprocessableEntity)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status429TooManyRequests)]
    public async Task<IActionResult> SendReminder(Guid id, CancellationToken ct)
    {
        if (!TryResolveOrganisationId(out var organisationId, out var orgError))
            return orgError!;

        if (!TryResolveActorUserId(out var actorUserId, out var actorError))
            return actorError!;

        try
        {
            var ipAddress = HttpContext.Connection.RemoteIpAddress?.ToString();
            await adminTwoFactorService.SendReminderAsync(actorUserId!.Value, id, organisationId!.Value, ipAddress, ct);

            return Ok(new ApiResponse<object>(
                new { message = "Reminder sent." },
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
    }

    [HttpPost("users/{id:guid}/generate-bypass-code")]
    [EnableRateLimiting("admin-bypass-code")]
    [ProducesResponseType(typeof(ApiResponse<BypassCodeResponse>), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status403Forbidden)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status404NotFound)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status422UnprocessableEntity)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status429TooManyRequests)]
    public async Task<IActionResult> GenerateBypassCode(Guid id, CancellationToken ct)
    {
        if (!TryResolveOrganisationId(out var organisationId, out var orgError))
            return orgError!;

        if (!TryResolveActorUserId(out var actorUserId, out var actorError))
            return actorError!;

        try
        {
            var ipAddress = HttpContext.Connection.RemoteIpAddress?.ToString();
            var result = await adminTwoFactorService.GenerateBypassCodeAsync(
                actorUserId!.Value, id, organisationId!.Value, ipAddress, ct);

            return Ok(new ApiResponse<BypassCodeResponse>(
                new BypassCodeResponse(result.BypassCode, result.ExpiresInSeconds),
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
                Title = "Rate Limit Exceeded",
                Detail = ex.Message,
            });
        }
    }

    [HttpGet("audit/2fa")]
    [EnableRateLimiting("data-read")]
    [ProducesResponseType(typeof(ApiResponse<MidiKaval.Api.Models.Audit.AuditListResultDto>), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status403Forbidden)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status400BadRequest)]
    public async Task<IActionResult> GetAuditLog(
        [FromQuery] string? eventType,
        [FromQuery] Guid? userId,
        [FromQuery] DateTime? from,
        [FromQuery] DateTime? to,
        [FromQuery] int page = 1,
        [FromQuery] int pageSize = 25,
        CancellationToken ct = default)
    {
        if (!TryResolveOrganisationId(out var organisationId, out var orgError))
            return orgError!;

        var (dto, totalCount, resolvedPage, resolvedPageSize) = await adminTwoFactorService.GetAuditLogAsync(
            organisationId!.Value, eventType, userId, from, to, page, pageSize, ct);

        return Ok(new ApiResponse<MidiKaval.Api.Models.Audit.AuditListResultDto>(dto, new ApiMeta
        {
            RequestId = ResolveRequestId(),
            TotalCount = totalCount,
            Page = resolvedPage,
            PageSize = resolvedPageSize,
        }));
    }

    [HttpPut("settings/require-2fa")]
    [EnableRateLimiting("data-write")]
    [ProducesResponseType(typeof(ApiResponse<object>), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status403Forbidden)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status404NotFound)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status429TooManyRequests)]
    public async Task<IActionResult> SetRequire2fa([FromBody] Require2faRequest request, CancellationToken ct)
    {
        if (!TryResolveOrganisationId(out var organisationId, out var orgError))
            return orgError!;

        if (!TryResolveActorUserId(out var actorUserId, out var actorError))
            return actorError!;

        try
        {
            var result = await adminTwoFactorService.SetRequire2faAsync(organisationId!.Value, actorUserId!.Value, request.Require2fa, ct);
            return Ok(new ApiResponse<object>(
                new { require2fa = result },
                new ApiMeta { RequestId = ResolveRequestId() }));
        }
        catch (KeyNotFoundException)
        {
            return NotFound(new ProblemDetails
            {
                Status = StatusCodes.Status404NotFound,
                Title = "Organisation Not Found",
                Detail = "The organisation was not found.",
            });
        }
    }

    [HttpPut("settings/delegate-2fa-reset")]
    [EnableRateLimiting("data-write")]
    [ProducesResponseType(typeof(ApiResponse<object>), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status403Forbidden)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status429TooManyRequests)]
    public async Task<IActionResult> SetDelegation([FromBody] DelegationRequest request, CancellationToken ct)
    {
        if (!TryResolveOrganisationId(out var organisationId, out var orgError))
            return orgError!;

        if (!TryResolveActorUserId(out var actorUserId, out var actorError))
            return actorError!;

        var result = await adminTwoFactorService.SetDelegationAsync(organisationId!.Value, actorUserId!.Value, request.Enabled, ct);
        return Ok(new ApiResponse<object>(
            new { enabled = result },
            new ApiMeta { RequestId = ResolveRequestId() }));
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
            });
            return false;
        }
        organisationId = parsed;
        error = null;
        return true;
    }

    private bool TryResolveActorUserId(out Guid? actorUserId, out IActionResult? error)
    {
        var claim = User.FindFirst(ClaimTypes.NameIdentifier)?.Value;
        if (claim is null || !Guid.TryParse(claim, out var parsed))
        {
            actorUserId = null;
            error = Unauthorized(new ProblemDetails
            {
                Status = StatusCodes.Status401Unauthorized,
                Title = "Invalid Token",
                Detail = "User identifier claim missing from token.",
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
