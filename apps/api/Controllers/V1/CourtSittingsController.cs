using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using MidiKaval.Api.Infrastructure;
using MidiKaval.Api.Infrastructure.Auth;
using MidiKaval.Api.Infrastructure.Cases;
using MidiKaval.Api.Models;
using MidiKaval.Api.Models.Cases;
using Microsoft.AspNetCore.RateLimiting;

namespace MidiKaval.Api.Controllers.V1;

/// <summary>Field-worker court sitting schedule lists.</summary>
[ApiController]
[EnableRateLimiting("data-read")]
[Route("api/v1/court-sittings")]
public sealed class CourtSittingsController(CourtSittingService courtSittingService) : ControllerBase
{
    /// <summary>Returns upcoming court sittings assigned to the current field worker.</summary>
    [HttpGet("upcoming")]
    [Authorize(Policy = Policies.FieldWorker)]
    [ProducesResponseType(typeof(ApiResponse<CourtSittingUpcomingListResultDto>), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status403Forbidden)]
    public async Task<IActionResult> ListUpcoming(CancellationToken cancellationToken)
    {
        var (result, totalCount) = await courtSittingService.ListUpcomingForFieldWorkerAsync(cancellationToken);
        var requestId = HttpContext.Items[RequestIdMiddleware.RequestIdItemKey] as string
            ?? HttpContext.TraceIdentifier;

        return Ok(new ApiResponse<CourtSittingUpcomingListResultDto>(
            result,
            new ApiMeta { RequestId = requestId, TotalCount = totalCount }));
    }
}
