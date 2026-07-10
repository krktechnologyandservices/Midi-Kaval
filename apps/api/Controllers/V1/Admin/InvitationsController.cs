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
[Require2FA]
[Route("api/v1/admin/invitations")]
public class InvitationsController(
    InvitationService invitationService) : ControllerBase
{
    // Vendor is included here specifically so a Vendor account can bootstrap the first
    // Director account in a fresh deployment. Listing/resending invitations stays
    // Director-only below.
    [HttpPost]
    [Authorize(Policy = Policies.DirectorOrVendor)]
    [EnableRateLimiting("data-write")]
    [ProducesResponseType(typeof(ApiResponse<SendInvitationResponse>), StatusCodes.Status201Created)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status403Forbidden)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status409Conflict)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status422UnprocessableEntity)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status429TooManyRequests)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status500InternalServerError)]
    public async Task<IActionResult> SendInvitation(
        [FromBody] SendInvitationRequest request,
        CancellationToken cancellationToken)
    {
        if (!ModelState.IsValid)
        {
            return UnprocessableEntity(ModelState);
        }

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

        var userIdClaim = User.FindFirst(System.Security.Claims.ClaimTypes.NameIdentifier)?.Value
            ?? User.FindFirst("sub")?.Value;
        if (userIdClaim is null || !Guid.TryParse(userIdClaim, out var userId))
        {
            return Unauthorized(new ProblemDetails
            {
                Status = StatusCodes.Status401Unauthorized,
                Title = "Invalid Token",
                Detail = "User claim missing from token.",
                Type = "https://tools.ietf.org/html/rfc7235#section-3.1",
            });
        }

        try
        {
            var ipAddress = HttpContext.Connection.RemoteIpAddress?.ToString();
            var result = await invitationService.SendInvitationAsync(
                organisationId, userId, request, ipAddress, cancellationToken);

            return CreatedAtAction(nameof(SendInvitation), null,
                new ApiResponse<SendInvitationResponse>(result, new ApiMeta
                {
                    RequestId = ResolveRequestId(),
                }));
        }
        catch (InvalidOperationException ex)
        {
            return Conflict(new ProblemDetails
            {
                Status = StatusCodes.Status409Conflict,
                Title = "Invitation Conflict",
                Detail = ex.Message,
                Type = "https://tools.ietf.org/html/rfc7231#section-6.5.8",
            });
        }
    }

    [HttpGet]
    [Authorize(Policy = Policies.DirectorOnly)]
    [EnableRateLimiting("vendor-read")]
    [ProducesResponseType(typeof(ApiResponse<InvitationListResult>), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status403Forbidden)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status429TooManyRequests)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status500InternalServerError)]
    public async Task<IActionResult> GetInvitations(
        [FromQuery] int page = 1,
        [FromQuery] int pageSize = 25,
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

        var result = await invitationService.GetInvitationListAsync(
            organisationId, page, pageSize, cancellationToken);

        return Ok(new ApiResponse<InvitationListResult>(
            result,
            new ApiMeta
            {
                RequestId = ResolveRequestId(),
                TotalCount = result.TotalCount,
                Page = result.Page,
                PageSize = result.PageSize,
            }));
    }

    [HttpPost("{id:guid}/resend")]
    [Authorize(Policy = Policies.DirectorOnly)]
    [EnableRateLimiting("data-write")]
    [ProducesResponseType(typeof(ApiResponse<ResendInvitationResponse>), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status403Forbidden)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status404NotFound)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status409Conflict)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status429TooManyRequests)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status500InternalServerError)]
    public async Task<IActionResult> ResendInvitation(
        Guid id,
        CancellationToken cancellationToken)
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

        var userIdClaim = User.FindFirst(System.Security.Claims.ClaimTypes.NameIdentifier)?.Value
            ?? User.FindFirst("sub")?.Value;
        if (userIdClaim is null || !Guid.TryParse(userIdClaim, out var userId))
        {
            return Unauthorized(new ProblemDetails
            {
                Status = StatusCodes.Status401Unauthorized,
                Title = "Invalid Token",
                Detail = "User claim missing from token.",
                Type = "https://tools.ietf.org/html/rfc7235#section-3.1",
            });
        }

        try
        {
            var ipAddress = HttpContext.Connection.RemoteIpAddress?.ToString();
            var result = await invitationService.ResendInvitationAsync(
                organisationId, id, userId, ipAddress, cancellationToken);

            return Ok(new ApiResponse<ResendInvitationResponse>(result, new ApiMeta
            {
                RequestId = ResolveRequestId(),
            }));
        }
        catch (KeyNotFoundException)
        {
            return NotFound(new ProblemDetails
            {
                Status = StatusCodes.Status404NotFound,
                Title = "Not Found",
                Detail = "Invitation not found.",
                Type = "https://tools.ietf.org/html/rfc7231#section-6.5.4",
            });
        }
        catch (InvalidOperationException ex)
        {
            return Conflict(new ProblemDetails
            {
                Status = StatusCodes.Status409Conflict,
                Title = "Resend Conflict",
                Detail = ex.Message,
                Type = "https://tools.ietf.org/html/rfc7231#section-6.5.8",
            });
        }
    }

    private string ResolveRequestId() =>
        HttpContext.Items[RequestIdMiddleware.RequestIdItemKey] as string
            ?? HttpContext.TraceIdentifier;
}
