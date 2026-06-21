using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using MidiKaval.Api.Infrastructure.Auth;
using MidiKaval.Api.Infrastructure.Cases;
using MidiKaval.Api.Models.Cases;

namespace MidiKaval.Api.Controllers.V1;

[ApiController]
[Route("api/v1/cases/{caseId:guid}/related-cases")]
public sealed class CaseRelatedCasesController(
    CaseRelatedCasesService relatedCasesService) : ControllerBase
{
    [HttpGet]
    [Authorize(Policy = Policies.CoordinatorOrAbove)]
    [ProducesResponseType(typeof(List<RelatedCaseDto>), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status403Forbidden)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status404NotFound)]
    public async Task<IActionResult> Get(
        Guid caseId,
        CancellationToken cancellationToken)
    {
        try
        {
            var dtos = await relatedCasesService.GetAsync(caseId, cancellationToken);
            return Ok(dtos);
        }
        catch (CaseNotFoundException)
        {
            return NotFoundProblem("Case not found.");
        }
        catch (InvalidOperationException)
        {
            return UnauthorizedProblem();
        }
        catch (OperationCanceledException)
        {
            throw;
        }
    }

    [HttpPost]
    [Authorize(Policy = Policies.CoordinatorOrAbove)]
    [ProducesResponseType(typeof(RelatedCaseDto), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status403Forbidden)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status404NotFound)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status409Conflict)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status422UnprocessableEntity)]
    public async Task<IActionResult> Link(
        Guid caseId,
        [FromBody] LinkRelatedCaseRequest request,
        CancellationToken cancellationToken)
    {
        if (request is null)
        {
            return BadRequestProblem("Request body is required.");
        }

        try
        {
            var dto = await relatedCasesService.LinkAsync(caseId, request, cancellationToken);
            return Ok(dto);
        }
        catch (CaseNotFoundException)
        {
            return NotFoundProblem("One or both cases not found.");
        }
        catch (CaseBusinessRuleException ex)
        {
            return UnprocessableProblem(ex.Message);
        }
        catch (InvalidOperationException)
        {
            return UnauthorizedProblem();
        }
        catch (DbUpdateException)
        {
            return ConflictProblem("Link already exists or concurrent conflict.");
        }
        catch (OperationCanceledException)
        {
            throw;
        }
    }

    [HttpDelete("{relatedCaseId:guid}")]
    [Authorize(Policy = Policies.CoordinatorOrAbove)]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status403Forbidden)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status404NotFound)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status409Conflict)]
    public async Task<IActionResult> Unlink(
        Guid caseId,
        Guid relatedCaseId,
        CancellationToken cancellationToken)
    {
        try
        {
            await relatedCasesService.UnlinkAsync(caseId, relatedCaseId, cancellationToken);
            return NoContent();
        }
        catch (CaseNotFoundException)
        {
            return NotFoundProblem("Link not found.");
        }
        catch (InvalidOperationException)
        {
            return UnauthorizedProblem();
        }
        catch (DbUpdateException)
        {
            return ConflictProblem("Concurrent conflict.");
        }
        catch (OperationCanceledException)
        {
            throw;
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

    private IActionResult ConflictProblem(string detail) =>
        Problem(
            detail: detail,
            statusCode: StatusCodes.Status409Conflict,
            title: "Conflict");

    private IActionResult UnauthorizedProblem() =>
        Problem(
            statusCode: StatusCodes.Status401Unauthorized,
            title: "Unauthorized");
}
