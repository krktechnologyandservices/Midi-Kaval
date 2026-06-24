using System.ComponentModel.DataAnnotations;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.RateLimiting;
using MidiKaval.Api.Domain.RoleManagement;
using MidiKaval.Api.Infrastructure;
using MidiKaval.Api.Models;
using MidiKaval.Api.Models.Auth;

namespace MidiKaval.Api.Controllers.V1.Auth;

[ApiController]
[Route("api/v1/auth")]
public class RegistrationController(
    RegistrationService registrationService) : ControllerBase
{
    /// <summary>Validates an activation link and returns the target email without consuming the token.</summary>
    [HttpGet("activate")]
    [EnableRateLimiting("auth-activate-read")]
    [ProducesResponseType(typeof(ApiResponse<ValidateActivationLinkResponse>), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status404NotFound)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status429TooManyRequests)]
    public async Task<IActionResult> ValidateLink(
        [FromQuery] string signature,
        [FromQuery] string token,
        CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(token) || string.IsNullOrWhiteSpace(signature))
        {
            return BadRequest(new ProblemDetails
            {
                Status = StatusCodes.Status400BadRequest,
                Title = "Invalid activation link.",
                Type = "https://tools.ietf.org/html/rfc7231#section-6.5.1",
            });
        }

        var result = await registrationService.ValidateLinkAsync(token, signature, cancellationToken);

        if (!result.IsValid || result.Email is null)
        {
            return NotFound(new ProblemDetails
            {
                Status = StatusCodes.Status404NotFound,
                Title = "Activation link not found or already used.",
                Detail = "This activation link was not found, has expired, or has already been used. Please contact the Vendor to request a new activation link.",
                Type = "https://tools.ietf.org/html/rfc7231#section-6.5.4",
            });
        }

        return Ok(new ApiResponse<ValidateActivationLinkResponse>(
            new ValidateActivationLinkResponse
            {
                Email = result.Email,
                OrganisationName = result.OrganisationName ?? string.Empty,
            },
            new ApiMeta { RequestId = ResolveRequestId() }));
    }

    /// <summary>Consumes an activation token and creates the first Director account.</summary>
    [HttpPost("activate")]
    [EnableRateLimiting("auth-activate")]
    [ProducesResponseType(typeof(ApiResponse<ActivateOrganisationResponse>), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status409Conflict)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status429TooManyRequests)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status500InternalServerError)]
    public async Task<IActionResult> ActivateOrganisation(
        [FromBody] ActivateOrganisationRequest request,
        CancellationToken cancellationToken)
    {
        if (!ModelState.IsValid)
        {
            return BadRequest(new ProblemDetails
            {
                Status = StatusCodes.Status400BadRequest,
                Title = "Validation Error",
                Type = "https://tools.ietf.org/html/rfc7231#section-6.5.1",
            });
        }

        try
        {
            var result = await registrationService.ActivateOrganisationAsync(
                request.Token,
                request.Signature,
                request.FullName,
                request.Password,
                cancellationToken);

            var response = new ActivateOrganisationResponse
            {
                UserId = result.UserId,
                OrganisationId = result.OrganisationId,
                OrganisationName = result.OrganisationName,
                Message = "Your organisation is active. Welcome to Kaval.",
            };

            return Ok(new ApiResponse<ActivateOrganisationResponse>(
                response,
                new ApiMeta { RequestId = ResolveRequestId() }));
        }
        catch (ActivationConflictException ex)
        {
            return Conflict(new ProblemDetails
            {
                Status = StatusCodes.Status409Conflict,
                Title = "Activation Conflict",
                Detail = ex.Message,
                Type = "https://tools.ietf.org/html/rfc7231#section-6.5.8",
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
    }

    private string ResolveRequestId() =>
        HttpContext.Items[RequestIdMiddleware.RequestIdItemKey] as string
            ?? HttpContext.TraceIdentifier;
}
