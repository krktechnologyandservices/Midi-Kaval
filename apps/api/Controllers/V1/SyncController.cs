using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using MidiKaval.Api.Infrastructure;
using MidiKaval.Api.Infrastructure.Auth;
using MidiKaval.Api.Infrastructure.Sync;
using MidiKaval.Api.Infrastructure.Visits;
using MidiKaval.Api.Models;
using MidiKaval.Api.Models.Sync;

namespace MidiKaval.Api.Controllers.V1;

/// <summary>Mobile offline sync push endpoint.</summary>
[ApiController]
[Route("api/v1/sync")]
public sealed class SyncController(SyncPushService syncPushService) : ControllerBase
{
    /// <summary>Replays queued offline mutations idempotently.</summary>
    [HttpPost("push")]
    [Authorize(Policy = Policies.FieldWorker)]
    [ProducesResponseType(typeof(ApiResponse<SyncPushResultDto>), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status403Forbidden)]
    public async Task<IActionResult> Push(SyncPushRequest request, CancellationToken cancellationToken)
    {
        try
        {
            var result = await syncPushService.PushAsync(request, cancellationToken);
            var requestId = HttpContext.Items[RequestIdMiddleware.RequestIdItemKey] as string
                ?? HttpContext.TraceIdentifier;

            return Ok(new ApiResponse<SyncPushResultDto>(result, new ApiMeta { RequestId = requestId }));
        }
        catch (VisitForbiddenException)
        {
            return ForbiddenProblem(Policies.ForbiddenByRoleMessage);
        }
    }

    private ObjectResult ForbiddenProblem(string detail) =>
        Problem(
            detail: detail,
            statusCode: StatusCodes.Status403Forbidden,
            title: "Forbidden");
}
