using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.RateLimiting;
using MidiKaval.Api.Infrastructure.Auth;
using MidiKaval.Api.Infrastructure.Cases;
using MidiKaval.Api.Infrastructure.Storage;
using MidiKaval.Api.Models.Attachments;

namespace MidiKaval.Api.Controllers.V1;

[ApiController]
[Route("api/v1/attachments")]
public sealed class AttachmentsController(AttachmentService attachmentService) : ControllerBase
{
    /// <summary>Issues a time-limited SAS URL for uploading an attachment blob.</summary>
    [HttpPost("presign")]
    [Authorize]
    [ProducesResponseType(typeof(AttachmentPresignResultDto), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status403Forbidden)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status404NotFound)]
    [EnableRateLimiting("data-write")]
    public async Task<IActionResult> Presign(
        [FromBody] AttachmentPresignRequest? request,
        CancellationToken cancellationToken)
    {
        if (request is null)
        {
            return BadRequestProblem("Request body is required.");
        }

        try
        {
            var dto = await attachmentService.PresignAsync(request, cancellationToken);
            return Ok(dto);
        }
        catch (CaseValidationException ex)
        {
            return BadRequestProblem(ex.Message);
        }
        catch (CaseForbiddenException)
        {
            return ForbiddenProblem(Policies.ForbiddenByRoleMessage);
        }
        catch (CaseNotFoundException)
        {
            return NotFoundProblem("Resource not found.");
        }
    }

    /// <summary>Confirms a blob upload and returns a time-limited read URL.</summary>
    [HttpPost("confirm")]
    [Authorize]
    [ProducesResponseType(typeof(AttachmentDto), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status403Forbidden)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status404NotFound)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status409Conflict)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status422UnprocessableEntity)]
    [EnableRateLimiting("data-write")]
    public async Task<IActionResult> Confirm(
        [FromBody] AttachmentConfirmRequest? request,
        CancellationToken cancellationToken)
    {
        if (request is null)
        {
            return BadRequestProblem("Request body is required.");
        }

        try
        {
            var dto = await attachmentService.ConfirmAsync(request, cancellationToken);
            return Ok(dto);
        }
        catch (CaseValidationException ex)
        {
            return BadRequestProblem(ex.Message);
        }
        catch (CaseForbiddenException)
        {
            return ForbiddenProblem(Policies.ForbiddenByRoleMessage);
        }
        catch (AttachmentNotFoundException)
        {
            return NotFoundProblem("Attachment not found.");
        }
        catch (CaseConflictException ex)
        {
            return ConflictProblem(ex.Message);
        }
        catch (CaseBusinessRuleException ex)
        {
            return UnprocessableProblem(ex.Message);
        }
        catch (CaseNotFoundException)
        {
            return NotFoundProblem("Case not found.");
        }
    }

    /// <summary>Issues a fresh time-limited read URL for a confirmed attachment.</summary>
    [HttpGet("{id:guid}/download-url")]
    [Authorize]
    [ProducesResponseType(typeof(AttachmentDownloadUrlDto), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status403Forbidden)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status404NotFound)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status422UnprocessableEntity)]
    [EnableRateLimiting("data-read")]
    public async Task<IActionResult> GetDownloadUrl(Guid id, CancellationToken cancellationToken)
    {
        try
        {
            var dto = await attachmentService.GetDownloadUrlAsync(id, cancellationToken);
            return Ok(dto);
        }
        catch (CaseForbiddenException)
        {
            return ForbiddenProblem(Policies.ForbiddenByRoleMessage);
        }
        catch (AttachmentNotFoundException)
        {
            return NotFoundProblem("Attachment not found.");
        }
        catch (CaseBusinessRuleException ex)
        {
            return UnprocessableProblem(ex.Message);
        }
        catch (CaseNotFoundException)
        {
            return NotFoundProblem("Case not found.");
        }
    }

    private IActionResult BadRequestProblem(string detail) =>
        Problem(
            detail: detail,
            statusCode: StatusCodes.Status400BadRequest,
            title: "Bad Request");

    private IActionResult ConflictProblem(string detail) =>
        Problem(
            detail: detail,
            statusCode: StatusCodes.Status409Conflict,
            title: "Conflict");

    private IActionResult NotFoundProblem(string detail) =>
        Problem(
            detail: detail,
            statusCode: StatusCodes.Status404NotFound,
            title: "Not Found");

    private IActionResult UnprocessableProblem(string detail) =>
        Problem(
            detail: detail,
            statusCode: StatusCodes.Status422UnprocessableEntity,
            title: "Unprocessable Entity");

    private IActionResult ForbiddenProblem(string detail) =>
        Problem(
            detail: detail,
            statusCode: StatusCodes.Status403Forbidden,
            title: "Forbidden");
}
