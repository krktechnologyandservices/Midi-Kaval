using System.ComponentModel.DataAnnotations;
using Hangfire;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.RateLimiting;
using MidiKaval.Api.Authorization;
using MidiKaval.Api.Domain.RoleManagement;
using MidiKaval.Api.Infrastructure;
using MidiKaval.Api.Infrastructure.Auth;
using MidiKaval.Api.Jobs;
using MidiKaval.Api.Models;
using MidiKaval.Api.Models.Vendor;

namespace MidiKaval.Api.Controllers.V1.Vendor;

[ApiController]
[Authorize(Policy = Policies.VendorOnly)]
[Route("api/v1/vendor")]
public class OrganisationsController(
    OrganisationService organisationService) : ControllerBase
{
    [HttpPost("organisations")]
    [Require2FA]
    [EnableRateLimiting("vendor-create")]
    [ProducesResponseType(typeof(ApiResponse<CreateOrganisationResponse>), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status403Forbidden)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status429TooManyRequests)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status500InternalServerError)]
    public async Task<IActionResult> CreateOrganisation(
        [FromBody] CreateOrganisationRequest request,
        CancellationToken cancellationToken)
    {
        try
        {
            var result = await organisationService.CreateOrganisationAsync(
                request.Name,
                request.TargetDirectorEmail,
                cancellationToken);

            // If initial email delivery failed, enqueue a Hangfire job for retry
            if (result.Status == "delivery_failed")
            {
                BackgroundJob.Enqueue<ActivationEmailDeliveryJob>(
                    j => j.ExecuteAsync(result.ActivationTokenId, CancellationToken.None));
            }

            var response = new CreateOrganisationResponse(
                result.OrganisationId,
                result.Name,
                result.Status);

            return Ok(new ApiResponse<CreateOrganisationResponse>(
                response,
                new ApiMeta { RequestId = ResolveRequestId() }));
        }
        catch (ValidationException ex)
        {
            return BadRequest(new ProblemDetails
            {
                Status = StatusCodes.Status400BadRequest,
                Title = "Validation Error",
                Detail = ex.Message,
                Type = "https://tools.ietf.org/html/rfc7231#section-6.5.1",
            });
        }
        catch (RateLimitExceededException ex)
        {
            return StatusCode(StatusCodes.Status429TooManyRequests, new ProblemDetails
            {
                Status = StatusCodes.Status429TooManyRequests,
                Title = "Too Many Requests",
                Detail = ex.Message,
                Type = "https://tools.ietf.org/html/rfc6585#section-4",
            });
        }
    }

    [HttpGet("organisations")]
    [Require2FA]
    [EnableRateLimiting("vendor-read")]
    [ProducesResponseType(typeof(ApiResponse<List<VendorOrganisationSummary>>), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status403Forbidden)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status429TooManyRequests)]
    public async Task<IActionResult> GetOrganisations(CancellationToken cancellationToken)
    {
        var orgs = await organisationService.GetOrganisationListAsync(cancellationToken);
        return Ok(new ApiResponse<List<VendorOrganisationSummary>>(
            orgs,
            new ApiMeta { RequestId = ResolveRequestId() }));
    }

    [HttpGet("organisations/{id:guid}")]
    [Require2FA]
    [EnableRateLimiting("vendor-read")]
    [ProducesResponseType(typeof(ApiResponse<VendorOrganisationDetail>), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status403Forbidden)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status404NotFound)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status429TooManyRequests)]
    public async Task<IActionResult> GetOrganisationDetail(
        Guid id,
        CancellationToken cancellationToken)
    {
        var org = await organisationService.GetOrganisationDetailAsync(id, cancellationToken);
        if (org is null)
        {
            return NotFound(new ProblemDetails
            {
                Status = StatusCodes.Status404NotFound,
                Title = "Not Found",
                Detail = "Organisation not found.",
                Type = "https://tools.ietf.org/html/rfc7231#section-6.5.4",
            });
        }

        return Ok(new ApiResponse<VendorOrganisationDetail>(
            org,
            new ApiMeta { RequestId = ResolveRequestId() }));
    }

    [HttpPost("organisations/{id:guid}/reissue-activation")]
    [Require2FA]
    [EnableRateLimiting("vendor-create")]
    [ProducesResponseType(typeof(ApiResponse<ReissueActivationResponse>), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status403Forbidden)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status404NotFound)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status409Conflict)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status429TooManyRequests)]
    public async Task<IActionResult> ReissueActivation(
        Guid id,
        [FromBody] ReissueActivationRequest request,
        CancellationToken cancellationToken)
    {
        try
        {
            var actorIdClaim = User.FindFirst(System.Security.Claims.ClaimTypes.NameIdentifier)?.Value;
            Guid? actorUserId = Guid.TryParse(actorIdClaim, out var aid) ? aid : null;

            var result = await organisationService.ReissueActivationAsync(
                id,
                request.TargetDirectorEmail,
                actorUserId,
                cancellationToken);

            // If initial email delivery failed, enqueue a Hangfire job for retry
            if (result.Status == "delivery_failed")
            {
                BackgroundJob.Enqueue<ActivationEmailDeliveryJob>(
                    j => j.ExecuteAsync(result.ActivationTokenId, CancellationToken.None));
            }

            var response = new ReissueActivationResponse(result.Status, request.TargetDirectorEmail);
            return Ok(new ApiResponse<ReissueActivationResponse>(
                response,
                new ApiMeta { RequestId = ResolveRequestId() }));
        }
        catch (ValidationException ex) when (
            ex.Message.Contains("already has active Directors", StringComparison.OrdinalIgnoreCase))
        {
            return Conflict(new ProblemDetails
            {
                Status = StatusCodes.Status409Conflict,
                Title = "Conflict",
                Detail = ex.Message,
                Type = "https://tools.ietf.org/html/rfc7231#section-6.5.8",
            });
        }
        catch (ValidationException ex) when (
            ex.Message.Contains("not found", StringComparison.OrdinalIgnoreCase))
        {
            return NotFound(new ProblemDetails
            {
                Status = StatusCodes.Status404NotFound,
                Title = "Not Found",
                Detail = ex.Message,
                Type = "https://tools.ietf.org/html/rfc7231#section-6.5.4",
            });
        }
        catch (ValidationException ex)
        {
            return BadRequest(new ProblemDetails
            {
                Status = StatusCodes.Status400BadRequest,
                Title = "Validation Error",
                Detail = ex.Message,
                Type = "https://tools.ietf.org/html/rfc7231#section-6.5.1",
            });
        }
        catch (RateLimitExceededException ex)
        {
            return StatusCode(StatusCodes.Status429TooManyRequests, new ProblemDetails
            {
                Status = StatusCodes.Status429TooManyRequests,
                Title = "Too Many Requests",
                Detail = ex.Message,
                Type = "https://tools.ietf.org/html/rfc6585#section-4",
            });
        }
    }

    private string ResolveRequestId() =>
        HttpContext.Items[RequestIdMiddleware.RequestIdItemKey] as string
            ?? HttpContext.TraceIdentifier;
}
