using System.Text.Json;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using MidiKaval.Api.Infrastructure;
using MidiKaval.Api.Infrastructure.Auth;
using MidiKaval.Api.Infrastructure.Persistence;
using MidiKaval.Api.Models;
using MidiKaval.Api.Models.Audit;
using Microsoft.AspNetCore.RateLimiting;

namespace MidiKaval.Api.Controllers.V1;

[ApiController]
[EnableRateLimiting("data-read")]
[Authorize(Policy = Policies.DirectorOnly)]
[Route("api/v1/admin/audit")]
[Produces("application/json")]
    public sealed class AuditLogController(AppDbContext db, ILogger<AuditLogController> logger) : ControllerBase
{
    [HttpGet]
    [ProducesResponseType(typeof(ApiResponse<AuditListResultDto>), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status403Forbidden)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status400BadRequest)]
    public async Task<IActionResult> ListEvents(
        [FromQuery] string? eventType,
        [FromQuery] Guid? actorUserId,
        [FromQuery] Guid? subjectUserId,
        [FromQuery] DateTime? from,
        [FromQuery] DateTime? to,
        [FromQuery] int page = 1,
        [FromQuery] int pageSize = 25,
        CancellationToken cancellationToken = default)
    {
        if (pageSize > 100)
        {
            return Problem(
                detail: "Page size cannot exceed 100.",
                statusCode: StatusCodes.Status400BadRequest);
        }

        if (pageSize < 1)
        {
            return Problem(
                detail: "Page size must be at least 1.",
                statusCode: StatusCodes.Status400BadRequest);
        }

        if (page < 1)
        {
            return Problem(
                detail: "Page must be 1 or greater.",
                statusCode: StatusCodes.Status400BadRequest);
        }

        var organisationId = ResolveOrganisationId();

        var query = db.AuditEvents
            .Where(e => e.OrganisationId == organisationId);

        if (!string.IsNullOrEmpty(eventType))
        {
            query = query.Where(e => e.EventType.StartsWith(eventType));
        }

        if (actorUserId.HasValue)
        {
            query = query.Where(e => e.ActorUserId == actorUserId.Value);
        }

        if (subjectUserId.HasValue)
        {
            query = query.Where(e => e.SubjectUserId == subjectUserId.Value);
        }

        if (from.HasValue && to.HasValue && from.Value > to.Value)
        {
            return Problem(
                detail: "From date must be before or equal to To date.",
                statusCode: StatusCodes.Status400BadRequest);
        }

        if (from.HasValue)
        {
            query = query.Where(e => e.CreatedAtUtc >= from.Value);
        }

        if (to.HasValue)
        {
            query = query.Where(e => e.CreatedAtUtc <= to.Value);
        }

        var totalCount = await query.CountAsync(cancellationToken);

        var rawItems = await query
            .OrderByDescending(e => e.CreatedAtUtc)
            .Skip((page - 1) * pageSize)
            .Take(pageSize)
            .Select(e => new
            {
                e.Id,
                e.EventType,
                e.CreatedAtUtc,
                e.ActorUserId,
                ActorEmail = e.ActorUser != null ? e.ActorUser.Email : null,
                ActorName = e.ActorUser != null ? e.ActorUser.FirstName + " " + e.ActorUser.LastName : null,
                e.SubjectUserId,
                SubjectEmail = e.SubjectUser != null ? e.SubjectUser.Email : null,
                SubjectName = e.SubjectUser != null ? e.SubjectUser.FirstName + " " + e.SubjectUser.LastName : null,
                e.TargetUserSnapshot,
                e.ActorIpAddress,
                e.MetadataJson,
            })
            .ToListAsync(cancellationToken);

        var items = rawItems.Select(r => new AuditEventDto(
            r.Id,
            r.EventType,
            r.CreatedAtUtc,
            r.ActorUserId,
            r.ActorEmail,
            r.ActorName,
            r.SubjectUserId,
            r.SubjectEmail,
            r.SubjectName,
            DeserializeTargetSnapshot(r.TargetUserSnapshot),
            r.ActorIpAddress,
            DeserializeMetadata(r.MetadataJson)
        )).ToList();

        var dto = new AuditListResultDto(items);
        return Ok(new ApiResponse<AuditListResultDto>(dto, new ApiMeta
        {
            RequestId = ResolveRequestId(),
            TotalCount = totalCount,
            Page = page,
            PageSize = pageSize,
        }));
    }

    private string ResolveRequestId() =>
        HttpContext.Items[RequestIdMiddleware.RequestIdItemKey] as string
            ?? HttpContext.TraceIdentifier;

    private TargetUserSnapshotDto? DeserializeTargetSnapshot(string? snapshotJson)
    {
        if (snapshotJson is null) return null;
        try
        {
            return JsonSerializer.Deserialize<TargetUserSnapshotDto>(snapshotJson);
        }
        catch (JsonException ex)
        {
            logger.LogWarning(ex, "Failed to deserialize target user snapshot from audit event.");
            return null;
        }
    }

    private static object? DeserializeMetadata(string? metadataJson)
    {
        if (metadataJson is null) return null;
        try
        {
            return JsonSerializer.Deserialize<object>(metadataJson);
        }
        catch (JsonException)
        {
            return null;
        }
    }

    private Guid ResolveOrganisationId()
    {
        var organisationClaim = HttpContext.User.FindFirst(AuthClaimTypes.OrganisationId)?.Value;
        if (!Guid.TryParse(organisationClaim, out var organisationId))
        {
            throw new InvalidOperationException("Authenticated user claims are missing or invalid.");
        }
        return organisationId;
    }
}
