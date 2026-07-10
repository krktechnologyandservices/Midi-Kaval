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
    /// <summary>Uploads and encrypts an attachment in a single request.</summary>
    [HttpPost("upload")]
    [Authorize]
    [RequestSizeLimit(10_485_760)]
    [ProducesResponseType(typeof(AttachmentDto), StatusCodes.Status201Created)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status403Forbidden)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status404NotFound)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status422UnprocessableEntity)]
    [EnableRateLimiting("data-write")]
    public async Task<IActionResult> Upload(
        [FromForm] string? resourceType,
        [FromForm] Guid resourceId,
        [FromForm] IFormFile? file,
        CancellationToken cancellationToken)
    {
        if (file is null || file.Length == 0)
        {
            return BadRequestProblem("file is required.");
        }

        byte[] content;
        using (var memoryStream = new MemoryStream())
        {
            await file.CopyToAsync(memoryStream, cancellationToken);
            content = memoryStream.ToArray();
        }

        try
        {
            var dto = await attachmentService.UploadAsync(
                resourceType,
                resourceId,
                file.FileName,
                file.ContentType,
                content,
                cancellationToken);

            return Created($"/api/v1/attachments/{dto.Id:D}/download", dto);
        }
        catch (CaseValidationException ex)
        {
            return BadRequestProblem(ex.Message);
        }
        catch (CaseForbiddenException)
        {
            return ForbiddenProblem(Policies.ForbiddenByRoleMessage);
        }
        catch (CaseBusinessRuleException ex)
        {
            return UnprocessableProblem(ex.Message);
        }
        catch (CaseNotFoundException)
        {
            return NotFoundProblem("Resource not found.");
        }
    }

    /// <summary>Decrypts and streams back a confirmed attachment.</summary>
    [HttpGet("{id:guid}/download")]
    [Authorize]
    [ProducesResponseType(StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status403Forbidden)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status404NotFound)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status422UnprocessableEntity)]
    [EnableRateLimiting("data-read")]
    public async Task<IActionResult> Download(Guid id, CancellationToken cancellationToken)
    {
        try
        {
            var content = await attachmentService.DownloadAsync(id, cancellationToken);
            return File(content.Content, content.ContentType, content.OriginalFileName);
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
