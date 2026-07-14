using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.RateLimiting;
using MidiKaval.Api.Infrastructure;
using MidiKaval.Api.Infrastructure.Auth;
using MidiKaval.Api.Infrastructure.Visits;
using MidiKaval.Api.Models;
using MidiKaval.Api.Models.Visits;

namespace MidiKaval.Api.Controllers.V1;

/// <summary>Field-worker visit scheduler lists and mutations.</summary>
[ApiController]
[Route("api/v1/visits")]
public sealed class VisitsController(VisitService visitService) : ControllerBase
{
    /// <summary>Returns visits scheduled for the current UTC day.</summary>
    [HttpGet("today")]
    [Authorize(Policy = Policies.FieldWorker)]
    [ProducesResponseType(typeof(ApiResponse<VisitListResultDto>), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status403Forbidden)]
    [EnableRateLimiting("data-read")]
    public async Task<IActionResult> ListToday(CancellationToken cancellationToken)
    {
        try
        {
            return Ok(await BuildListResponseAsync(
                () => visitService.ListTodayAsync(cancellationToken)));
        }
        catch (VisitForbiddenException)
        {
            return ForbiddenProblem(Policies.ForbiddenByRoleMessage);
        }
    }

    /// <summary>Returns proximity-based grouping suggestions for today's visits.</summary>
    [HttpGet("today/grouping-suggestion")]
    [Authorize(Policy = Policies.FieldWorker)]
    [ProducesResponseType(typeof(ApiResponse<VisitGroupingSuggestionDto>), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status403Forbidden)]
    [EnableRateLimiting("data-read")]
    public async Task<IActionResult> GetTodayGroupingSuggestion(CancellationToken cancellationToken)
    {
        try
        {
            var suggestion = await visitService.GetTodayGroupingSuggestionAsync(cancellationToken);
            var requestId = HttpContext.Items[RequestIdMiddleware.RequestIdItemKey] as string
                ?? HttpContext.TraceIdentifier;

            return Ok(new ApiResponse<VisitGroupingSuggestionDto>(
                suggestion,
                new ApiMeta { RequestId = requestId }));
        }
        catch (VisitForbiddenException)
        {
            return ForbiddenProblem(Policies.ForbiddenByRoleMessage);
        }
    }

    /// <summary>Returns visits scheduled during the current UTC week (Mon–Sun).</summary>
    [HttpGet("weekly")]
    [Authorize(Policy = Policies.FieldWorker)]
    [ProducesResponseType(typeof(ApiResponse<VisitListResultDto>), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status403Forbidden)]
    [EnableRateLimiting("data-read")]
    public async Task<IActionResult> ListWeekly(CancellationToken cancellationToken)
    {
        try
        {
            return Ok(await BuildListResponseAsync(
                () => visitService.ListWeeklyAsync(cancellationToken)));
        }
        catch (VisitForbiddenException)
        {
            return ForbiddenProblem(Policies.ForbiddenByRoleMessage);
        }
    }

    /// <summary>Returns overdue visits assigned to the current field worker.</summary>
    [HttpGet("overdue")]
    [Authorize(Policy = Policies.FieldWorker)]
    [ProducesResponseType(typeof(ApiResponse<VisitListResultDto>), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status403Forbidden)]
    [EnableRateLimiting("data-read")]
    public async Task<IActionResult> ListOverdue(CancellationToken cancellationToken)
    {
        try
        {
            return Ok(await BuildListResponseAsync(
                () => visitService.ListOverdueAsync(cancellationToken)));
        }
        catch (VisitForbiddenException)
        {
            return ForbiddenProblem(Policies.ForbiddenByRoleMessage);
        }
    }

    /// <summary>Marks a visit in progress for the assigned field worker.</summary>
    [HttpPost("{id:guid}/start")]
    [Authorize(Policy = Policies.FieldWorker)]
    [ProducesResponseType(typeof(VisitListItemDto), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status403Forbidden)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status404NotFound)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status422UnprocessableEntity)]
    [EnableRateLimiting("data-write")]
    public async Task<IActionResult> Start(Guid id, CancellationToken cancellationToken)
    {
        try
        {
            var dto = await visitService.StartAsync(id, cancellationToken);
            return Ok(dto);
        }
        catch (VisitBusinessRuleException ex)
        {
            return UnprocessableProblem(ex.Message);
        }
        catch (VisitForbiddenException)
        {
            return ForbiddenProblem(Policies.ForbiddenByRoleMessage);
        }
        catch (VisitNotFoundException)
        {
            return NotFoundProblem("Visit not found.");
        }
    }

    /// <summary>Marks a visit completed and increments the parent case visit count.</summary>
    [HttpPost("{id:guid}/complete")]
    [Authorize(Policy = Policies.FieldWorker)]
    [ProducesResponseType(typeof(VisitListItemDto), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status403Forbidden)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status404NotFound)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status422UnprocessableEntity)]
    [EnableRateLimiting("data-write")]
    public async Task<IActionResult> Complete(
        Guid id,
        [FromBody] CompleteVisitRequest? request,
        CancellationToken cancellationToken)
    {
        if (request is null)
        {
            return BadRequestProblem("Request body is required.");
        }

        try
        {
            var dto = await visitService.CompleteAsync(id, request, cancellationToken);
            return Ok(dto);
        }
        catch (VisitValidationException ex)
        {
            return BadRequestProblem(ex.Message);
        }
        catch (VisitBusinessRuleException ex)
        {
            return UnprocessableProblem(ex.Message);
        }
        catch (VisitForbiddenException)
        {
            return ForbiddenProblem(Policies.ForbiddenByRoleMessage);
        }
        catch (VisitNotFoundException)
        {
            return NotFoundProblem("Visit not found.");
        }
    }

    /// <summary>Reschedules a visit with a supervisor-visible reason. Coordinators/Directors
    /// can also use this to correct a scheduling mistake on the web app, not just the
    /// visit's assigned field worker.</summary>
    [HttpPost("{id:guid}/reschedule")]
    [Authorize(Policy = Policies.FieldWorkerOrCoordinatorOrAbove)]
    [ProducesResponseType(typeof(VisitListItemDto), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status403Forbidden)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status404NotFound)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status422UnprocessableEntity)]
    [EnableRateLimiting("data-write")]
    public async Task<IActionResult> Reschedule(
        Guid id,
        [FromBody] RescheduleVisitRequest? request,
        CancellationToken cancellationToken)
    {
        if (request is null)
        {
            return BadRequestProblem("Request body is required.");
        }

        try
        {
            var dto = await visitService.RescheduleAsync(id, request, cancellationToken);
            return Ok(dto);
        }
        catch (VisitValidationException ex)
        {
            return BadRequestProblem(ex.Message);
        }
        catch (VisitBusinessRuleException ex)
        {
            return UnprocessableProblem(ex.Message);
        }
        catch (VisitForbiddenException)
        {
            return ForbiddenProblem(Policies.ForbiddenByRoleMessage);
        }
        catch (VisitNotFoundException)
        {
            return NotFoundProblem("Visit not found.");
        }
    }

    /// <summary>Logs the field worker's actual GPS position on arrival at a planned place.
    /// Timestamp is server-assigned, not trusted from the client.</summary>
    [HttpPost("{id:guid}/places/{placeId:guid}/log")]
    [Authorize(Policy = Policies.FieldWorker)]
    [ProducesResponseType(typeof(ApiResponse<VisitPlaceDto>), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status403Forbidden)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status404NotFound)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status422UnprocessableEntity)]
    [EnableRateLimiting("data-write")]
    public async Task<IActionResult> LogPlace(
        Guid id,
        Guid placeId,
        [FromBody] LogVisitPlaceRequest? request,
        CancellationToken cancellationToken)
    {
        if (request is null)
        {
            return BadRequestProblem("Request body is required.");
        }

        try
        {
            var dto = await visitService.LogPlaceAsync(id, placeId, request, cancellationToken);
            var requestId = HttpContext.Items[RequestIdMiddleware.RequestIdItemKey] as string
                ?? HttpContext.TraceIdentifier;

            return Ok(new ApiResponse<VisitPlaceDto>(dto, new ApiMeta { RequestId = requestId }));
        }
        catch (VisitValidationException ex)
        {
            return BadRequestProblem(ex.Message);
        }
        catch (VisitBusinessRuleException ex)
        {
            return UnprocessableProblem(ex.Message);
        }
        catch (VisitForbiddenException)
        {
            return ForbiddenProblem(Policies.ForbiddenByRoleMessage);
        }
        catch (VisitNotFoundException)
        {
            return NotFoundProblem("Visit not found.");
        }
    }

    /// <summary>Adds or updates the field worker's own free-text remark on a place —
    /// independent of the one-time GPS log, usable before or after logging.</summary>
    [HttpPatch("{id:guid}/places/{placeId:guid}/comment")]
    [Authorize(Policy = Policies.FieldWorker)]
    [ProducesResponseType(typeof(ApiResponse<VisitPlaceDto>), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status403Forbidden)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status404NotFound)]
    [EnableRateLimiting("data-write")]
    public async Task<IActionResult> UpdatePlaceComment(
        Guid id,
        Guid placeId,
        [FromBody] UpdateVisitPlaceCommentRequest? request,
        CancellationToken cancellationToken)
    {
        if (request is null)
        {
            return BadRequestProblem("Request body is required.");
        }

        try
        {
            var dto = await visitService.UpdatePlaceCommentAsync(id, placeId, request, cancellationToken);
            var requestId = HttpContext.Items[RequestIdMiddleware.RequestIdItemKey] as string
                ?? HttpContext.TraceIdentifier;

            return Ok(new ApiResponse<VisitPlaceDto>(dto, new ApiMeta { RequestId = requestId }));
        }
        catch (VisitValidationException ex)
        {
            return BadRequestProblem(ex.Message);
        }
        catch (VisitForbiddenException)
        {
            return ForbiddenProblem(Policies.ForbiddenByRoleMessage);
        }
        catch (VisitNotFoundException)
        {
            return NotFoundProblem("Visit place not found.");
        }
    }

    private async Task<ApiResponse<VisitListResultDto>> BuildListResponseAsync(
        Func<Task<(VisitListResultDto Result, int TotalCount)>> load)
    {
        var (result, totalCount) = await load();
        var requestId = HttpContext.Items[RequestIdMiddleware.RequestIdItemKey] as string
            ?? HttpContext.TraceIdentifier;

        return new ApiResponse<VisitListResultDto>(
            result,
            new ApiMeta { RequestId = requestId, TotalCount = totalCount });
    }

    private IActionResult BadRequestProblem(string detail) =>
        Problem(detail: detail, statusCode: StatusCodes.Status400BadRequest, title: "Bad Request");

    private IActionResult NotFoundProblem(string detail) =>
        Problem(detail: detail, statusCode: StatusCodes.Status404NotFound, title: "Not Found");

    private IActionResult UnprocessableProblem(string detail) =>
        Problem(
            detail: detail,
            statusCode: StatusCodes.Status422UnprocessableEntity,
            title: "Unprocessable Entity");

    private IActionResult ForbiddenProblem(string detail) =>
        Problem(detail: detail, statusCode: StatusCodes.Status403Forbidden, title: "Forbidden");
}
