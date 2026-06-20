using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.RateLimiting;
using Microsoft.EntityFrameworkCore;
using MidiKaval.Api.Infrastructure;
using MidiKaval.Api.Infrastructure.Auth;
using MidiKaval.Api.Infrastructure.Cases;
using MidiKaval.Api.Infrastructure.Visits;
using MidiKaval.Api.Models;
using MidiKaval.Api.Models.Cases;
using MidiKaval.Api.Models.Visits;
using Npgsql;

namespace MidiKaval.Api.Controllers.V1;

[ApiController]
[Route("api/v1/cases")]
public sealed class CasesController(
    CaseService caseService,
    CaseSearchPresetService presetService,
    VisitService visitService,
    CaseNoteService caseNoteService,
    InterventionService interventionService,
    CourtSittingService courtSittingService) : ControllerBase
{
    [HttpGet("search/export")]
    [Authorize(Policy = Policies.CoordinatorOrAbove)]
    [Produces("application/vnd.openxmlformats-officedocument.spreadsheetml.sheet", "application/pdf")]
    [ProducesResponseType(StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status403Forbidden)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status422UnprocessableEntity)]
    public async Task<IActionResult> Export(
        [FromQuery] string? format,
        [FromQuery] CaseSearchQuery query,
        CancellationToken cancellationToken)
    {
        try
        {
            var (content, contentType, fileName) = await caseService.ExportAsync(
                query,
                format,
                cancellationToken);
            return File(content, contentType, fileName);
        }
        catch (CaseValidationException ex)
        {
            return BadRequestProblem(ex.Message);
        }
        catch (CaseBusinessRuleException ex)
        {
            return UnprocessableProblem(ex.Message);
        }
    }

    [HttpGet("search")]
    [Authorize(Policy = Policies.CoordinatorOrAbove)]
    [ProducesResponseType(typeof(ApiResponse<CaseSearchResultDto>), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status403Forbidden)]
    public async Task<IActionResult> Search(
        [FromQuery] CaseSearchQuery query,
        CancellationToken cancellationToken)
    {
        try
        {
            var (result, totalCount) = await caseService.SearchAsync(query, cancellationToken);
            var requestId = HttpContext.Items[RequestIdMiddleware.RequestIdItemKey] as string
                ?? HttpContext.TraceIdentifier;

            return Ok(new ApiResponse<CaseSearchResultDto>(
                result,
                new ApiMeta { RequestId = requestId, TotalCount = totalCount }));
        }
        catch (CaseValidationException ex)
        {
            return BadRequestProblem(ex.Message);
        }
    }

    [HttpGet("search-presets")]
    [Authorize(Policy = Policies.CoordinatorOrAbove)]
    [ProducesResponseType(typeof(IReadOnlyList<CaseSearchPresetDto>), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status403Forbidden)]
    public async Task<IActionResult> ListSearchPresets(CancellationToken cancellationToken)
    {
        var presets = await presetService.ListAsync(cancellationToken);
        return Ok(presets);
    }

    [HttpPost("search-presets")]
    [Authorize(Policy = Policies.CoordinatorOrAbove)]
    [ProducesResponseType(typeof(CaseSearchPresetDto), StatusCodes.Status201Created)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status403Forbidden)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status409Conflict)]
    public async Task<IActionResult> CreateSearchPreset(
        [FromBody] CreateCaseSearchPresetRequest? request,
        CancellationToken cancellationToken)
    {
        if (request is null)
        {
            return BadRequestProblem("Request body is required.");
        }

        try
        {
            var dto = await presetService.CreateAsync(request, cancellationToken);
            return Created($"/api/v1/cases/search-presets/{dto.Id}", dto);
        }
        catch (CaseValidationException ex)
        {
            return BadRequestProblem(ex.Message);
        }
        catch (CaseConflictException ex)
        {
            return ConflictProblem(ex.Message);
        }
        catch (DbUpdateException ex) when (IsUniqueViolation(ex))
        {
            return ConflictProblem("A preset with this name already exists.");
        }
    }

    [HttpDelete("search-presets/{id:guid}")]
    [Authorize(Policy = Policies.CoordinatorOrAbove)]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status403Forbidden)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status404NotFound)]
    public async Task<IActionResult> DeleteSearchPreset(Guid id, CancellationToken cancellationToken)
    {
        try
        {
            await presetService.DeleteAsync(id, cancellationToken);
            return NoContent();
        }
        catch (CaseNotFoundException)
        {
            return NotFoundProblem("Search preset not found.");
        }
    }

    [HttpGet("assigned")]
    [Authorize(Policy = Policies.FieldWorker)]
    [ProducesResponseType(typeof(ApiResponse<CaseSearchResultDto>), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status403Forbidden)]
    public async Task<IActionResult> ListAssigned(
        [FromQuery] int page = 1,
        [FromQuery] int pageSize = 25,
        CancellationToken cancellationToken = default)
    {
        try
        {
            var (result, totalCount) = await caseService.ListAssignedAsync(page, pageSize, cancellationToken);
            var requestId = HttpContext.Items[RequestIdMiddleware.RequestIdItemKey] as string
                ?? HttpContext.TraceIdentifier;

            return Ok(new ApiResponse<CaseSearchResultDto>(
                result,
                new ApiMeta { RequestId = requestId, TotalCount = totalCount }));
        }
        catch (CaseForbiddenException)
        {
            return ForbiddenProblem(Policies.ForbiddenByRoleMessage);
        }
    }

    /// <summary>Lists visits for a case (supervisor visibility including reschedule reasons).</summary>
    [HttpGet("{id:guid}/visits")]
    [Authorize(Policy = Policies.CoordinatorOrAbove)]
    [ProducesResponseType(typeof(ApiResponse<VisitListResultDto>), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status403Forbidden)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status404NotFound)]
    public async Task<IActionResult> ListCaseVisits(Guid id, CancellationToken cancellationToken)
    {
        try
        {
            var (result, totalCount) = await visitService.ListForCaseAsync(id, cancellationToken);
            var requestId = HttpContext.Items[RequestIdMiddleware.RequestIdItemKey] as string
                ?? HttpContext.TraceIdentifier;

            return Ok(new ApiResponse<VisitListResultDto>(
                result,
                new ApiMeta { RequestId = requestId, TotalCount = totalCount }));
        }
        catch (VisitNotFoundException)
        {
            return NotFoundProblem("Case not found.");
        }
    }

    /// <summary>Schedules a field visit for a case.</summary>
    [HttpPost("{id:guid}/visits")]
    [Authorize(Policy = Policies.CoordinatorOrAbove)]
    [ProducesResponseType(typeof(VisitListItemDto), StatusCodes.Status201Created)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status403Forbidden)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status404NotFound)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status422UnprocessableEntity)]
    public async Task<IActionResult> ScheduleVisit(
        Guid id,
        [FromBody] ScheduleVisitRequest? request,
        CancellationToken cancellationToken)
    {
        if (request is null)
        {
            return BadRequestProblem("Request body is required.");
        }

        try
        {
            var dto = await visitService.ScheduleAsync(id, request, cancellationToken);
            return Created($"/api/v1/visits/{dto.Id:D}", dto);
        }
        catch (VisitValidationException ex)
        {
            return BadRequestProblem(ex.Message);
        }
        catch (VisitBusinessRuleException ex)
        {
            return UnprocessableProblem(ex.Message);
        }
        catch (VisitNotFoundException)
        {
            return NotFoundProblem("Case not found.");
        }
    }

    [HttpGet("{id:guid}")]
    [Authorize]
    [ProducesResponseType(typeof(CaseDetailDto), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status403Forbidden)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status404NotFound)]
    public async Task<IActionResult> GetById(Guid id, CancellationToken cancellationToken)
    {
        try
        {
            var dto = await caseService.GetDetailAsync(id, cancellationToken);
            return Ok(dto);
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

    /// <summary>Lists chronological case notes for a case timeline.</summary>
    [HttpGet("{id:guid}/notes")]
    [Authorize]
    [ProducesResponseType(typeof(CaseNoteListResultDto), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status403Forbidden)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status404NotFound)]
    public async Task<IActionResult> ListNotes(Guid id, CancellationToken cancellationToken)
    {
        try
        {
            var dto = await caseNoteService.ListAsync(id, cancellationToken);
            return Ok(dto);
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

    /// <summary>Creates a typed note on a case.</summary>
    [HttpPost("{id:guid}/notes")]
    [Authorize]
    [ProducesResponseType(typeof(CaseNoteDto), StatusCodes.Status201Created)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status403Forbidden)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status404NotFound)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status422UnprocessableEntity)]
    public async Task<IActionResult> CreateNote(
        Guid id,
        [FromBody] CreateCaseNoteRequest? request,
        CancellationToken cancellationToken)
    {
        if (request is null)
        {
            return BadRequestProblem("Request body is required.");
        }

        try
        {
            var dto = await caseNoteService.CreateAsync(id, request, cancellationToken);
            return Created($"/api/v1/cases/{id:D}/notes", dto);
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
            return NotFoundProblem("Case not found.");
        }
    }

    /// <summary>Lists interventions for a case.</summary>
    [HttpGet("{id:guid}/interventions")]
    [Authorize]
    [ProducesResponseType(typeof(InterventionListResultDto), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status403Forbidden)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status404NotFound)]
    public async Task<IActionResult> ListInterventions(Guid id, CancellationToken cancellationToken)
    {
        try
        {
            var dto = await interventionService.ListAsync(id, cancellationToken);
            return Ok(dto);
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

    /// <summary>Gets a single intervention on a case.</summary>
    [HttpGet("{id:guid}/interventions/{interventionId:guid}")]
    [Authorize]
    [ProducesResponseType(typeof(InterventionDto), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status403Forbidden)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status404NotFound)]
    public async Task<IActionResult> GetIntervention(
        Guid id,
        Guid interventionId,
        CancellationToken cancellationToken)
    {
        try
        {
            var dto = await interventionService.GetAsync(id, interventionId, cancellationToken);
            return Ok(dto);
        }
        catch (CaseForbiddenException)
        {
            return ForbiddenProblem(Policies.ForbiddenByRoleMessage);
        }
        catch (CaseNotFoundException)
        {
            return NotFoundProblem("Intervention not found.");
        }
    }

    /// <summary>Creates an intervention on a case.</summary>
    [HttpPost("{id:guid}/interventions")]
    [Authorize]
    [ProducesResponseType(typeof(InterventionDto), StatusCodes.Status201Created)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status403Forbidden)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status404NotFound)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status422UnprocessableEntity)]
    public async Task<IActionResult> CreateIntervention(
        Guid id,
        [FromBody] CreateInterventionRequest? request,
        CancellationToken cancellationToken)
    {
        if (request is null)
        {
            return BadRequestProblem("Request body is required.");
        }

        try
        {
            var dto = await interventionService.CreateAsync(id, request, cancellationToken);
            return Created($"/api/v1/cases/{id:D}/interventions/{dto.Id:D}", dto);
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
            return NotFoundProblem("Case not found.");
        }
    }

    /// <summary>Updates an intervention on a case.</summary>
    [HttpPatch("{id:guid}/interventions/{interventionId:guid}")]
    [Authorize]
    [ProducesResponseType(typeof(InterventionDto), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status403Forbidden)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status404NotFound)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status422UnprocessableEntity)]
    public async Task<IActionResult> UpdateIntervention(
        Guid id,
        Guid interventionId,
        [FromBody] UpdateInterventionRequest? request,
        CancellationToken cancellationToken)
    {
        if (request is null)
        {
            return BadRequestProblem("Request body is required.");
        }

        try
        {
            var dto = await interventionService.UpdateAsync(id, interventionId, request, cancellationToken);
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
            return NotFoundProblem("Intervention not found.");
        }
    }

    /// <summary>Lists court sittings for a case.</summary>
    [HttpGet("{id:guid}/court-sittings")]
    [Authorize]
    [ProducesResponseType(typeof(CourtSittingListResultDto), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status403Forbidden)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status404NotFound)]
    public async Task<IActionResult> ListCourtSittings(Guid id, CancellationToken cancellationToken)
    {
        try
        {
            var dto = await courtSittingService.ListAsync(id, cancellationToken);
            return Ok(dto);
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

    /// <summary>Gets a single court sitting on a case.</summary>
    [HttpGet("{id:guid}/court-sittings/{sittingId:guid}")]
    [Authorize]
    [ProducesResponseType(typeof(CourtSittingDto), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status403Forbidden)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status404NotFound)]
    public async Task<IActionResult> GetCourtSitting(
        Guid id,
        Guid sittingId,
        CancellationToken cancellationToken)
    {
        try
        {
            var dto = await courtSittingService.GetAsync(id, sittingId, cancellationToken);
            return Ok(dto);
        }
        catch (CaseForbiddenException)
        {
            return ForbiddenProblem(Policies.ForbiddenByRoleMessage);
        }
        catch (CaseNotFoundException)
        {
            return NotFoundProblem("Court sitting not found.");
        }
    }

    /// <summary>Creates a court sitting on a case.</summary>
    [HttpPost("{id:guid}/court-sittings")]
    [Authorize]
    [ProducesResponseType(typeof(CourtSittingDto), StatusCodes.Status201Created)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status403Forbidden)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status404NotFound)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status422UnprocessableEntity)]
    public async Task<IActionResult> CreateCourtSitting(
        Guid id,
        [FromBody] CreateCourtSittingRequest? request,
        CancellationToken cancellationToken)
    {
        if (request is null)
        {
            return BadRequestProblem("Request body is required.");
        }

        try
        {
            var dto = await courtSittingService.CreateAsync(id, request, cancellationToken);
            return Created($"/api/v1/cases/{id:D}/court-sittings/{dto.Id:D}", dto);
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
            return NotFoundProblem("Case not found.");
        }
    }

    /// <summary>Updates a court sitting on a case.</summary>
    [HttpPatch("{id:guid}/court-sittings/{sittingId:guid}")]
    [Authorize]
    [ProducesResponseType(typeof(CourtSittingDto), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status403Forbidden)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status404NotFound)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status422UnprocessableEntity)]
    public async Task<IActionResult> UpdateCourtSitting(
        Guid id,
        Guid sittingId,
        [FromBody] UpdateCourtSittingRequest? request,
        CancellationToken cancellationToken)
    {
        if (request is null)
        {
            return BadRequestProblem("Request body is required.");
        }

        try
        {
            var dto = await courtSittingService.UpdateAsync(id, sittingId, request, cancellationToken);
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
            return NotFoundProblem("Court sitting not found.");
        }
    }

    /// <summary>Verifies case GPS coordinates and landmark for the assigned field worker.</summary>
    [HttpPost("{id:guid}/gps/verify")]
    [Authorize(Policy = Policies.FieldWorker)]
    [ProducesResponseType(typeof(CaseGpsDto), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status403Forbidden)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status404NotFound)]
    public async Task<IActionResult> VerifyGps(
        Guid id,
        [FromBody] VerifyCaseGpsRequest? request,
        CancellationToken cancellationToken)
    {
        if (request is null)
        {
            return BadRequestProblem("Request body is required.");
        }

        try
        {
            var dto = await caseService.VerifyGpsAsync(id, request, cancellationToken);
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
            return NotFoundProblem("Case not found.");
        }
    }

    /// <summary>Reveal full beneficiary PII for an assigned POCSO case after recent OTP verification.</summary>
    [HttpPost("{id:guid}/reveal-pii")]
    [Authorize(Policy = Policies.FieldWorker)]
    [EnableRateLimiting("auth-step-up")]
    [ProducesResponseType(typeof(RevealCasePiiResponse), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status403Forbidden)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status404NotFound)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status422UnprocessableEntity)]
    public async Task<IActionResult> RevealPii(Guid id, CancellationToken cancellationToken)
    {
        try
        {
            var dto = await caseService.RevealPiiAsync(id, cancellationToken);
            return Ok(dto);
        }
        catch (CaseStepUpRequiredException)
        {
            return ForbiddenProblem("Recent OTP verification is required.");
        }
        catch (CaseForbiddenException)
        {
            return ForbiddenProblem(Policies.ForbiddenByRoleMessage);
        }
        catch (CaseNotFoundException)
        {
            return NotFoundProblem("Case not found.");
        }
        catch (CaseBusinessRuleException ex)
        {
            return UnprocessableProblem(ex.Message);
        }
    }

    [HttpPost("{id:guid}/transfer")]
    [Authorize(Policy = Policies.CoordinatorOrAbove)]
    [ProducesResponseType(typeof(CaseDetailDto), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status403Forbidden)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status404NotFound)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status422UnprocessableEntity)]
    public async Task<IActionResult> Transfer(
        Guid id,
        [FromBody] TransferCaseRequest? request,
        CancellationToken cancellationToken)
    {
        if (request is null)
        {
            return BadRequestProblem("Request body is required.");
        }

        try
        {
            var dto = await caseService.TransferAsync(id, request, cancellationToken);
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
            return NotFoundProblem("Case not found.");
        }
    }

    [HttpPost]
    [Authorize(Policy = Policies.CoordinatorOrAbove)]
    [ProducesResponseType(typeof(CaseDto), StatusCodes.Status201Created)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status403Forbidden)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status409Conflict)]
    public async Task<IActionResult> Create(
        [FromBody] CreateCaseRequest? request,
        CancellationToken cancellationToken)
    {
        if (request is null)
        {
            return BadRequestProblem("Request body is required.");
        }

        try
        {
            var dto = await caseService.CreateAsync(request, cancellationToken);
            return Created($"/api/v1/cases/{dto.Id}", dto);
        }
        catch (CaseValidationException ex)
        {
            return BadRequestProblem(ex.Message);
        }
        catch (DbUpdateException ex) when (IsUniqueViolation(ex))
        {
            return ConflictProblem("A case with this crime number or ST number already exists in your organisation.");
        }
    }

    [HttpPost("check-duplicate")]
    [Authorize(Policy = Policies.CoordinatorOrAbove)]
    [ProducesResponseType(typeof(CheckCaseDuplicateResultDto), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status403Forbidden)]
    public async Task<IActionResult> CheckDuplicate(
        [FromBody] CheckCaseDuplicateRequest? request,
        CancellationToken cancellationToken)
    {
        if (request is null)
        {
            return BadRequestProblem("Request body is required.");
        }

        try
        {
            var result = await caseService.CheckDuplicateAsync(request, cancellationToken);
            return Ok(result);
        }
        catch (CaseValidationException ex)
        {
            return BadRequestProblem(ex.Message);
        }
    }

    [HttpPatch("{id:guid}/stage")]
    [Authorize(Policy = Policies.CoordinatorOrAbove)]
    [ProducesResponseType(typeof(CaseDto), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status403Forbidden)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status404NotFound)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status422UnprocessableEntity)]
    public async Task<IActionResult> TransitionStage(
        Guid id,
        [FromBody] TransitionCaseStageRequest? request,
        CancellationToken cancellationToken)
    {
        if (request is null)
        {
            return BadRequestProblem("Request body is required.");
        }

        try
        {
            var dto = await caseService.TransitionStageAsync(id, request, cancellationToken);
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
            return NotFoundProblem("Case not found.");
        }
    }

    [HttpPost("{id:guid}/merge")]
    [Authorize(Policy = Policies.CoordinatorOrAbove)]
    [ProducesResponseType(typeof(CaseDto), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status403Forbidden)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status404NotFound)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status409Conflict)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status422UnprocessableEntity)]
    public async Task<IActionResult> Merge(
        Guid id,
        [FromBody] CreateCaseRequest? request,
        CancellationToken cancellationToken)
    {
        if (request is null)
        {
            return BadRequestProblem("Request body is required.");
        }

        try
        {
            var dto = await caseService.MergeAsync(id, request, cancellationToken);
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
        catch (CaseConflictException ex)
        {
            return ConflictProblem(ex.Message);
        }
        catch (CaseNotFoundException)
        {
            return NotFoundProblem("Case not found.");
        }
        catch (DbUpdateException ex) when (IsUniqueViolation(ex))
        {
            return ConflictProblem("A case with this crime number or ST number already exists in your organisation.");
        }
    }

    private static bool IsUniqueViolation(DbUpdateException ex)
    {
        for (var current = (Exception?)ex; current is not null; current = current.InnerException)
        {
            if (current is PostgresException { SqlState: "23505" })
            {
                return true;
            }
        }

        return false;
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
