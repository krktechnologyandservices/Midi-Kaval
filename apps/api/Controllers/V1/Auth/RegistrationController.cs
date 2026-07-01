using System.ComponentModel.DataAnnotations;
using Microsoft.AspNetCore.Authorization;
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
        [FromQuery(Name = "sig")] string signature,
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
            var ipAddress = HttpContext.Connection.RemoteIpAddress?.ToString();
            var result = await registrationService.ActivateOrganisationAsync(
                request.Token,
                request.Signature,
                request.FullName,
                request.Password,
                ipAddress,
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

    /// <summary>Validates an invitation link without consuming the token.</summary>
    [AllowAnonymous]
    [HttpGet("accept-invitation")]
    [EnableRateLimiting("auth-activate-read")]
    [ProducesResponseType(typeof(ApiResponse<ValidateInvitationLinkResponse>), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status404NotFound)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status429TooManyRequests)]
    public async Task<IActionResult> ValidateInvitationLink(
        [FromQuery] string token,
        [FromQuery] string sig,
        CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(token) || string.IsNullOrWhiteSpace(sig))
        {
            return BadRequest(new ProblemDetails
            {
                Status = StatusCodes.Status400BadRequest,
                Title = "Invalid invitation link.",
                Type = "https://tools.ietf.org/html/rfc7231#section-6.5.1",
            });
        }

        var result = await registrationService.ValidateInvitationLinkAsync(token, sig, cancellationToken);

        if (!result.IsValid || result.Email is null)
        {
            return NotFound(new ProblemDetails
            {
                Status = StatusCodes.Status404NotFound,
                Title = "Invitation not found or already used.",
                Detail = "This invitation was not found, has expired, or has already been used. Please contact your Director to request a new invitation.",
                Type = "https://tools.ietf.org/html/rfc7231#section-6.5.4",
            });
        }

        return Ok(new ApiResponse<ValidateInvitationLinkResponse>(
            new ValidateInvitationLinkResponse(
                result.Email,
                result.OrganisationName ?? string.Empty,
                result.Role ?? string.Empty,
                true),
            new ApiMeta { RequestId = ResolveRequestId() }));
    }

    /// <summary>Accepts an invitation, creates a user in pending confirmation state, and sends a confirmation email.</summary>
    [AllowAnonymous]
    [HttpPost("accept-invitation")]
    [EnableRateLimiting("auth-activate")]
    [ProducesResponseType(typeof(ApiResponse<AcceptInvitationResponse>), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status404NotFound)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status422UnprocessableEntity)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status429TooManyRequests)]
    public async Task<IActionResult> AcceptInvitation(
        [FromBody] AcceptInvitationRequest request,
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
            var ipAddress = HttpContext.Connection.RemoteIpAddress?.ToString();
            var result = await registrationService.AcceptInvitationAsync(
                request.Token,
                request.Signature,
                request.FullName,
                request.Password,
                ipAddress,
                cancellationToken);

            return Ok(new ApiResponse<AcceptInvitationResponse>(
                new AcceptInvitationResponse(
                    result.Email,
                    result.OrganisationName,
                    $"We've sent a confirmation email to {result.Email}. Please check your inbox and click the link to activate your account."),
                new ApiMeta { RequestId = ResolveRequestId() }));
        }
        catch (KeyNotFoundException ex)
        {
            return NotFound(new ProblemDetails
            {
                Status = StatusCodes.Status404NotFound,
                Title = "Invitation Not Found",
                Detail = ex.Message,
                Type = "https://tools.ietf.org/html/rfc7231#section-6.5.4",
            });
        }
        catch (RegistrationConflictException ex)
        {
            return UnprocessableEntity(new ProblemDetails
            {
                Status = StatusCodes.Status422UnprocessableEntity,
                Title = "Invitation Conflict",
                Detail = ex.Message,
                Type = "https://tools.ietf.org/html/rfc7231#section-6.5.2",
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

    /// <summary>Confirms email, activating the user so they can log in.</summary>
    [AllowAnonymous]
    [HttpPost("confirm-email")]
    [EnableRateLimiting("auth-activate")]
    [ProducesResponseType(typeof(ApiResponse<ConfirmEmailResponse>), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status404NotFound)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status422UnprocessableEntity)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status429TooManyRequests)]
    public async Task<IActionResult> ConfirmEmail(
        [FromBody] ConfirmEmailRequest request,
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

        if (string.IsNullOrWhiteSpace(request.Token) || string.IsNullOrWhiteSpace(request.Signature))
        {
            return BadRequest(new ProblemDetails
            {
                Status = StatusCodes.Status400BadRequest,
                Title = "Validation Error",
                Detail = "Token and signature are required.",
                Type = "https://tools.ietf.org/html/rfc7231#section-6.5.1",
            });
        }

        try
        {
            var ipAddress = HttpContext.Connection.RemoteIpAddress?.ToString();
            var result = await registrationService.ConfirmEmailAsync(
                request.Token,
                request.Signature,
                ipAddress,
                cancellationToken);

            return Ok(new ApiResponse<ConfirmEmailResponse>(
                new ConfirmEmailResponse("Your email has been confirmed! You can now log in."),
                new ApiMeta { RequestId = ResolveRequestId() }));
        }
        catch (KeyNotFoundException ex)
        {
            return NotFound(new ProblemDetails
            {
                Status = StatusCodes.Status404NotFound,
                Title = "Confirmation Link Not Found",
                Detail = ex.Message,
                Type = "https://tools.ietf.org/html/rfc7231#section-6.5.4",
            });
        }
        catch (RegistrationConflictException ex)
        {
            var problem = new ProblemDetails
            {
                Status = StatusCodes.Status422UnprocessableEntity,
                Title = "Confirmation Conflict",
                Detail = ex.Message,
                Type = ex.ErrorCode is not null
                    ? $"https://errors/{ex.ErrorCode}"
                    : "https://tools.ietf.org/html/rfc7231#section-6.5.2",
            };
            if (ex.ErrorCode is not null)
            {
                problem.Extensions["code"] = ex.ErrorCode;
            }
            return UnprocessableEntity(problem);
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
