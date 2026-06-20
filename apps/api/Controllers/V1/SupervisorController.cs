using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using MidiKaval.Api.Infrastructure;
using MidiKaval.Api.Infrastructure.Auth;
using MidiKaval.Api.Infrastructure.Cases;
using MidiKaval.Api.Infrastructure.Supervisor;
using MidiKaval.Api.Infrastructure.TravelClaims;
using MidiKaval.Api.Models;
using MidiKaval.Api.Models.Supervisor;
using MidiKaval.Api.Models.TravelClaims;

namespace MidiKaval.Api.Controllers.V1;

/// <summary>Supervisor crisis queue (court-miss rows in v1).</summary>
[ApiController]
[Route("api/v1/supervisor")]
public sealed class SupervisorController(
    CrisisQueueService crisisQueueService,
    DashboardService dashboardService,
    TravelClaimService travelClaimService) : ControllerBase
{
    /// <summary>Returns prioritized crisis queue rows (court-miss only until Epic 8.1).</summary>
    [HttpGet("crisis-queue")]
    [Authorize(Policy = Policies.CoordinatorOrAbove)]
    [ProducesResponseType(typeof(ApiResponse<CrisisQueueListResultDto>), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status403Forbidden)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status500InternalServerError)]
    public async Task<IActionResult> ListCrisisQueue(CancellationToken cancellationToken)
    {
        try
        {
            var (result, totalCount) = await crisisQueueService.ListAsync(cancellationToken);
            var requestId = HttpContext.Items[RequestIdMiddleware.RequestIdItemKey] as string
                ?? HttpContext.TraceIdentifier;

            return Ok(new ApiResponse<CrisisQueueListResultDto>(
                result,
                new ApiMeta { RequestId = requestId, TotalCount = totalCount }));
        }
        catch (InvalidOperationException ex)
        {
            return Problem(
                detail: ex.Message,
                statusCode: StatusCodes.Status500InternalServerError,
                title: "Internal Server Error");
        }
    }

    /// <summary>Returns operational dashboard widget data (cases, visits, court, claims, intake trend).</summary>
    [HttpGet("dashboard")]
    [Authorize(Policy = Policies.CoordinatorOrAbove)]
    [ProducesResponseType(typeof(ApiResponse<DashboardResultDto>), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status403Forbidden)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status500InternalServerError)]
    public async Task<IActionResult> GetDashboard(CancellationToken cancellationToken)
    {
        try
        {
            var (result, totalCount) = await dashboardService.GetDashboardAsync(cancellationToken);
            var requestId = HttpContext.Items[RequestIdMiddleware.RequestIdItemKey] as string
                ?? HttpContext.TraceIdentifier;

            return Ok(new ApiResponse<DashboardResultDto>(
                result,
                new ApiMeta { RequestId = requestId, TotalCount = totalCount }));
        }
        catch (Exception ex)
        {
            return Problem(
                detail: ex.Message,
                statusCode: StatusCodes.Status500InternalServerError,
                title: "Internal Server Error");
        }
    }

    /// <summary>Returns monthly travel claim totals grouped by staff member.</summary>
    [HttpGet("travel-claims/monthly-totals")]
    [Authorize(Policy = Policies.CoordinatorOrAbove)]
    [ProducesResponseType(typeof(ApiResponse<TravelClaimMonthlyTotalsResultDto>), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status403Forbidden)]
    public async Task<IActionResult> ListTravelClaimMonthlyTotals(
        [FromQuery] int year,
        [FromQuery] int month,
        CancellationToken cancellationToken)
    {
        try
        {
            var (result, totalCount) = await travelClaimService.ListMonthlyTotalsAsync(
                year,
                month,
                cancellationToken);
            var requestId = HttpContext.Items[RequestIdMiddleware.RequestIdItemKey] as string
                ?? HttpContext.TraceIdentifier;

            return Ok(new ApiResponse<TravelClaimMonthlyTotalsResultDto>(
                result,
                new ApiMeta { RequestId = requestId, TotalCount = totalCount }));
        }
        catch (CaseValidationException ex)
        {
            return Problem(
                detail: ex.Message,
                statusCode: StatusCodes.Status400BadRequest,
                title: "Bad Request");
        }
    }

    /// <summary>Returns a travel claim for coordinator read-only review.</summary>
    [HttpGet("travel-claims/{id:guid}")]
    [Authorize(Policy = Policies.CoordinatorOrAbove)]
    [ProducesResponseType(typeof(TravelClaimDto), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status403Forbidden)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status404NotFound)]
    public async Task<IActionResult> GetTravelClaim(Guid id, CancellationToken cancellationToken)
    {
        try
        {
            var dto = await travelClaimService.GetForSupervisorAsync(id, cancellationToken);
            return Ok(dto);
        }
        catch (CaseNotFoundException)
        {
            return Problem(
                detail: "Travel claim not found.",
                statusCode: StatusCodes.Status404NotFound,
                title: "Not Found");
        }
    }
}
