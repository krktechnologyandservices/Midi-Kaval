using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using MidiKaval.Api.Infrastructure;
using MidiKaval.Api.Infrastructure.Auth;
using MidiKaval.Api.Infrastructure.Cases;
using MidiKaval.Api.Infrastructure.TravelClaims;
using MidiKaval.Api.Models;
using MidiKaval.Api.Models.TravelClaims;

namespace MidiKaval.Api.Controllers.V1;

/// <summary>Field-worker travel claim CRUD and submit.</summary>
[ApiController]
[Route("api/v1/travel-claims")]
public sealed class TravelClaimsController(TravelClaimService travelClaimService) : ControllerBase
{
    /// <summary>Lists travel claims for the authenticated field worker.</summary>
    [HttpGet]
    [Authorize(Policy = Policies.FieldWorker)]
    [ProducesResponseType(typeof(ApiResponse<TravelClaimListResultDto>), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status403Forbidden)]
    public async Task<IActionResult> List(CancellationToken cancellationToken)
    {
        var (result, totalCount) = await travelClaimService.ListMineAsync(cancellationToken);
        var requestId = HttpContext.Items[RequestIdMiddleware.RequestIdItemKey] as string
            ?? HttpContext.TraceIdentifier;

        return Ok(new ApiResponse<TravelClaimListResultDto>(
            result,
            new ApiMeta { RequestId = requestId, TotalCount = totalCount }));
    }

    /// <summary>Creates a draft travel claim.</summary>
    [HttpPost]
    [Authorize(Policy = Policies.FieldWorker)]
    [ProducesResponseType(typeof(TravelClaimDto), StatusCodes.Status201Created)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status403Forbidden)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status404NotFound)]
    public async Task<IActionResult> Create(
        [FromBody] CreateTravelClaimRequest? request,
        CancellationToken cancellationToken)
    {
        if (request is null)
        {
            return BadRequestProblem("Request body is required.");
        }

        try
        {
            var dto = await travelClaimService.CreateAsync(request, cancellationToken);
            return Created($"/api/v1/travel-claims/{dto.Id:D}", dto);
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
            return NotFoundProblem("Case not found.");
        }
    }

    /// <summary>Returns a single travel claim owned by the authenticated field worker.</summary>
    [HttpGet("{id:guid}")]
    [Authorize(Policy = Policies.FieldWorker)]
    [ProducesResponseType(typeof(TravelClaimDto), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status403Forbidden)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status404NotFound)]
    public async Task<IActionResult> Get(Guid id, CancellationToken cancellationToken)
    {
        try
        {
            var dto = await travelClaimService.GetAsync(id, cancellationToken);
            return Ok(dto);
        }
        catch (CaseNotFoundException)
        {
            return NotFoundProblem("Travel claim not found.");
        }
    }

    /// <summary>Updates a draft travel claim owned by the authenticated field worker.</summary>
    [HttpPatch("{id:guid}")]
    [Authorize(Policy = Policies.FieldWorker)]
    [ProducesResponseType(typeof(TravelClaimDto), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status403Forbidden)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status404NotFound)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status422UnprocessableEntity)]
    public async Task<IActionResult> Update(
        Guid id,
        [FromBody] UpdateTravelClaimRequest? request,
        CancellationToken cancellationToken)
    {
        if (request is null)
        {
            return BadRequestProblem("Request body is required.");
        }

        try
        {
            var dto = await travelClaimService.UpdateAsync(id, request, cancellationToken);
            return Ok(dto);
        }
        catch (CaseValidationException ex)
        {
            return BadRequestProblem(ex.Message);
        }
        catch (CaseBusinessRuleException ex)
        {
            return UnprocessableProblem(ex.Message);
        }
        catch (CaseForbiddenException)
        {
            return ForbiddenProblem(Policies.ForbiddenByRoleMessage);
        }
        catch (CaseNotFoundException)
        {
            return NotFoundProblem("Travel claim not found.");
        }
    }

    /// <summary>Submits a draft travel claim for supervisor review.</summary>
    [HttpPost("{id:guid}/submit")]
    [Authorize(Policy = Policies.FieldWorker)]
    [ProducesResponseType(typeof(TravelClaimDto), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status403Forbidden)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status404NotFound)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status422UnprocessableEntity)]
    public async Task<IActionResult> Submit(Guid id, CancellationToken cancellationToken)
    {
        try
        {
            var dto = await travelClaimService.SubmitAsync(id, cancellationToken);
            return Ok(dto);
        }
        catch (CaseBusinessRuleException ex)
        {
            return UnprocessableProblem(ex.Message);
        }
        catch (CaseForbiddenException)
        {
            return ForbiddenProblem(Policies.ForbiddenByRoleMessage);
        }
        catch (CaseNotFoundException)
        {
            return NotFoundProblem("Travel claim not found.");
        }
    }

    /// <summary>Approves a submitted travel claim (Director only).</summary>
    [HttpPost("{id:guid}/approve")]
    [Authorize(Policy = Policies.DirectorOnly)]
    [ProducesResponseType(typeof(TravelClaimDto), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status403Forbidden)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status404NotFound)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status422UnprocessableEntity)]
    public async Task<IActionResult> Approve(
        Guid id,
        [FromBody] ApproveTravelClaimRequest? request,
        CancellationToken cancellationToken)
    {
        try
        {
            var dto = await travelClaimService.ApproveAsync(id, request, cancellationToken);
            return Ok(dto);
        }
        catch (CaseValidationException ex)
        {
            return BadRequestProblem(ex.Message);
        }
        catch (CaseBusinessRuleException ex)
        {
            return UnprocessableProblem(ex.Message);
        }
        catch (CaseNotFoundException)
        {
            return NotFoundProblem("Travel claim not found.");
        }
    }

    /// <summary>Returns a submitted travel claim to the claimant with comment (Director only).</summary>
    [HttpPost("{id:guid}/return")]
    [Authorize(Policy = Policies.DirectorOnly)]
    [ProducesResponseType(typeof(TravelClaimDto), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status403Forbidden)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status404NotFound)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status422UnprocessableEntity)]
    public async Task<IActionResult> Return(
        Guid id,
        [FromBody] ReturnTravelClaimRequest? request,
        CancellationToken cancellationToken)
    {
        try
        {
            var dto = await travelClaimService.ReturnAsync(id, request, cancellationToken);
            return Ok(dto);
        }
        catch (CaseValidationException ex)
        {
            return BadRequestProblem(ex.Message);
        }
        catch (CaseBusinessRuleException ex)
        {
            return UnprocessableProblem(ex.Message);
        }
        catch (CaseNotFoundException)
        {
            return NotFoundProblem("Travel claim not found.");
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
