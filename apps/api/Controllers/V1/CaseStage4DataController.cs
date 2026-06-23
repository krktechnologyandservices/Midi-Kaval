using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.RateLimiting;
using Microsoft.EntityFrameworkCore;
using MidiKaval.Api.Infrastructure.Auth;
using MidiKaval.Api.Infrastructure.Cases;
using MidiKaval.Api.Models.Cases;

namespace MidiKaval.Api.Controllers.V1;

[ApiController]
[Route("api/v1/cases/{caseId:guid}/stage4-data")]
public sealed class CaseStage4DataController(
    CaseStage4DataService stage4DataService) : ControllerBase
{
    [HttpGet]
    [Authorize(Policy = Policies.CoordinatorOrAbove)]
    [ProducesResponseType(typeof(Stage4PlacementDto), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status403Forbidden)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status404NotFound)]
    [EnableRateLimiting("data-read")]
    public async Task<IActionResult> Get(
        Guid caseId,
        CancellationToken cancellationToken)
    {
        try
        {
            var dto = await stage4DataService.GetAsync(caseId, cancellationToken);
            return Ok(dto);
        }
        catch (CaseNotFoundException)
        {
            return NotFoundProblem("Case not found or not in Stage 4.");
        }
        catch (InvalidOperationException)
        {
            return UnauthorizedProblem();
        }
    }

    [HttpPut]
    [RequestSizeLimit(16_384)]
    [Authorize(Policy = Policies.CoordinatorOrAbove)]
    [ProducesResponseType(typeof(Stage4PlacementDto), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status403Forbidden)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status404NotFound)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status409Conflict)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status422UnprocessableEntity)]
    [EnableRateLimiting("data-write")]
    public async Task<IActionResult> Upsert(
        Guid caseId,
        [FromBody] UpsertStage4PlacementRequest request,
        CancellationToken cancellationToken)
    {
        if (request is null)
        {
            return BadRequestProblem("Request body is required.");
        }

        try
        {
            var dto = await stage4DataService.UpsertAsync(caseId, request, cancellationToken);
            return Ok(dto);
        }
        catch (CaseNotFoundException)
        {
            return NotFoundProblem("Case not found or not in Stage 4.");
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
            return ConflictProblem("Concurrent update conflict.");
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
