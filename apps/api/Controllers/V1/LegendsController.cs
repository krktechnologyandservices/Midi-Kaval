using System.Text.Json;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using MidiKaval.Api.Domain.Entities;
using MidiKaval.Api.Domain.Entities.Legends;
using MidiKaval.Api.Infrastructure;
using MidiKaval.Api.Infrastructure.Audit;
using MidiKaval.Api.Infrastructure.Auth;
using MidiKaval.Api.Infrastructure.Persistence;
using MidiKaval.Api.Models;
using MidiKaval.Api.Models.Legends;

namespace MidiKaval.Api.Controllers.V1;

/// <summary>CRUD endpoints for Legends master data (FR-23).</summary>
[ApiController]
[Authorize(Policy = Policies.CoordinatorOrAbove)]
[Route("api/v1/legends")]
[Produces("application/json")]
public sealed class LegendsController(AppDbContext db) : ControllerBase
{
    private static readonly Dictionary<string, EntityDescriptor> TypeMap = new(StringComparer.OrdinalIgnoreCase)
    {
        ["offence-types"] = new("OffenceType", q => q.Set<OffenceType>()),
        ["classifications"] = new("Classification", q => q.Set<Classification>()),
        ["intervention-categories"] = new("InterventionCategory", q => q.Set<InterventionCategory>()),
        ["education-levels"] = new("EducationLevel", q => q.Set<EducationLevel>()),
        ["occupations"] = new("Occupation", q => q.Set<Occupation>()),
        ["visit-outcomes"] = new("VisitOutcome", q => q.Set<VisitOutcome>()),
        ["court-outcomes"] = new("CourtOutcome", q => q.Set<CourtOutcome>()),
        ["areas"] = new("Area", q => q.Set<Area>()),
        ["designations"] = new("Designation", q => q.Set<Designation>()),
        ["police-stations"] = new("PoliceStation", q => q.Set<PoliceStation>()),
    };

    /// <summary>List entries for a legend type.</summary>
    [HttpGet("{type}")]
    [ProducesResponseType(typeof(ApiResponse<LegendListResultDto>), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status403Forbidden)]
    public async Task<IActionResult> List(
        string type,
        [FromQuery] bool includeInactive = false,
        CancellationToken cancellationToken = default)
    {
        if (!TypeMap.TryGetValue(type, out var descriptor))
        {
            return BadRequest(new ProblemDetails
            {
                Detail = $"Invalid legend type '{type}'. Valid types: {string.Join(", ", TypeMap.Keys)}",
                Status = StatusCodes.Status400BadRequest,
            });
        }

        var organisationId = ResolveOrganisationId();
        var query = descriptor.DbSet(db).Where(e => EF.Property<Guid>(e, "OrganisationId") == organisationId);

        if (!includeInactive)
        {
            query = query.Where(e => EF.Property<bool>(e, "IsActive"));
        }

        var items = await query
            .OrderBy(e => EF.Property<string>(e, "Name"))
            .Select(e => new LegendDto(
                EF.Property<Guid>(e, "Id"),
                EF.Property<string>(e, "Name")!,
                EF.Property<bool>(e, "IsActive"),
                EF.Property<DateTime>(e, "CreatedAtUtc"),
                EF.Property<DateTime>(e, "UpdatedAtUtc")))
            .ToListAsync(cancellationToken);

        return Ok(new ApiResponse<LegendListResultDto>(
            new LegendListResultDto(items),
            new ApiMeta { RequestId = ResolveRequestId() }));
    }

    /// <summary>Get a single legend entry by ID.</summary>
    [HttpGet("{type}/{id:guid}")]
    [ProducesResponseType(typeof(ApiResponse<LegendDto>), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status403Forbidden)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status404NotFound)]
    public async Task<IActionResult> Get(
        string type,
        Guid id,
        CancellationToken cancellationToken = default)
    {
        if (!TypeMap.TryGetValue(type, out var descriptor))
        {
            return BadRequest(InvalidTypeProblem());
        }

        var organisationId = ResolveOrganisationId();
        var entity = await FindEntityAsync(descriptor, id, organisationId, cancellationToken);

        if (entity is null)
        {
            return NotFound();
        }

        var dto = MapToDto(entity);
        return Ok(new ApiResponse<LegendDto>(dto, new ApiMeta { RequestId = ResolveRequestId() }));
    }

    /// <summary>Create a new legend entry.</summary>
    [HttpPost("{type}")]
    [ProducesResponseType(typeof(ApiResponse<LegendDto>), StatusCodes.Status201Created)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status403Forbidden)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status409Conflict)]
    public async Task<IActionResult> Create(
        string type,
        LegendCreateRequest request,
        CancellationToken cancellationToken = default)
    {
        if (!TypeMap.TryGetValue(type, out var descriptor))
        {
            return BadRequest(InvalidTypeProblem());
        }

        if (string.IsNullOrWhiteSpace(request.Name))
        {
            return BadRequest(new ProblemDetails { Detail = "Name is required.", Status = StatusCodes.Status400BadRequest });
        }

        var organisationId = ResolveOrganisationId();
        var userId = ResolveUserId();
        var now = DateTime.UtcNow;

        if (await NameExistsAsync(descriptor, organisationId, request.Name.Trim(), null, cancellationToken))
        {
            return Conflict(new ProblemDetails
            {
                Detail = $"A '{descriptor.DisplayName}' with this name already exists.",
                Status = StatusCodes.Status409Conflict,
            });
        }

        var entity = CreateEntityInstance(descriptor, organisationId, request.Name.Trim(), userId, now);
        db.Add(entity);

        db.AuditEvents.Add(new AuditEvent
        {
            Id = Guid.NewGuid(),
            OrganisationId = organisationId,
            ActorUserId = userId,
            EventType = AuditEventTypes.LegendCreated,
            MetadataJson = JsonSerializer.Serialize(new
            {
                legendType = descriptor.DisplayName,
                name = request.Name.Trim(),
            }),
            CreatedAtUtc = now,
        });

        try
        {
            await db.SaveChangesAsync(cancellationToken);
        }
        catch (DbUpdateException ex) when (IsUniqueConstraintViolation(ex))
        {
            return Conflict(new ProblemDetails
            {
                Detail = $"A '{descriptor.DisplayName}' with this name already exists.",
                Status = StatusCodes.Status409Conflict,
            });
        }

        var dto = MapToDto(entity);
        return CreatedAtAction(nameof(Get), new { type, id = GetEntityId(entity) },
            new ApiResponse<LegendDto>(dto, new ApiMeta { RequestId = ResolveRequestId() }));
    }

    /// <summary>Update a legend entry name.</summary>
    [HttpPut("{type}/{id:guid}")]
    [ProducesResponseType(typeof(ApiResponse<LegendDto>), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status403Forbidden)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status404NotFound)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status409Conflict)]
    public async Task<IActionResult> Update(
        string type,
        Guid id,
        LegendUpdateRequest request,
        CancellationToken cancellationToken = default)
    {
        if (!TypeMap.TryGetValue(type, out var descriptor))
        {
            return BadRequest(InvalidTypeProblem());
        }

        if (string.IsNullOrWhiteSpace(request.Name))
        {
            return BadRequest(new ProblemDetails { Detail = "Name is required.", Status = StatusCodes.Status400BadRequest });
        }

        var organisationId = ResolveOrganisationId();
        var userId = ResolveUserId();
        var now = DateTime.UtcNow;

        var entity = await FindEntityAsync(descriptor, id, organisationId, cancellationToken);

        if (entity is null)
        {
            return NotFound();
        }

        var currentName = GetEntityName(entity);

        if (request.Name.Trim().Equals(currentName, StringComparison.OrdinalIgnoreCase))
        {
            // No change — return current state
            var dto = MapToDto(entity);
            return Ok(new ApiResponse<LegendDto>(dto, new ApiMeta { RequestId = ResolveRequestId() }));
        }

        if (await NameExistsAsync(descriptor, organisationId, request.Name.Trim(), id, cancellationToken))
        {
            return Conflict(new ProblemDetails
            {
                Detail = $"A '{descriptor.DisplayName}' with this name already exists.",
                Status = StatusCodes.Status409Conflict,
            });
        }

        SetEntityName(entity, request.Name.Trim());
        SetEntityUpdatedAt(entity, now);

        db.AuditEvents.Add(new AuditEvent
        {
            Id = Guid.NewGuid(),
            OrganisationId = organisationId,
            ActorUserId = userId,
            EventType = AuditEventTypes.LegendUpdated,
            MetadataJson = JsonSerializer.Serialize(new
            {
                legendType = descriptor.DisplayName,
                previousName = currentName,
                newName = request.Name.Trim(),
            }),
            CreatedAtUtc = now,
        });

        try
        {
            await db.SaveChangesAsync(cancellationToken);
        }
        catch (DbUpdateException ex) when (IsUniqueConstraintViolation(ex))
        {
            return Conflict(new ProblemDetails
            {
                Detail = $"A '{descriptor.DisplayName}' with this name already exists.",
                Status = StatusCodes.Status409Conflict,
            });
        }

        var resultDto = MapToDto(entity);
        return Ok(new ApiResponse<LegendDto>(resultDto, new ApiMeta { RequestId = ResolveRequestId() }));
    }

    /// <summary>Soft-deactivate a legend entry.</summary>
    [HttpDelete("{type}/{id:guid}")]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status403Forbidden)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status404NotFound)]
    public async Task<IActionResult> Deactivate(
        string type,
        Guid id,
        CancellationToken cancellationToken = default)
    {
        if (!TypeMap.TryGetValue(type, out var descriptor))
        {
            return BadRequest(InvalidTypeProblem());
        }

        var organisationId = ResolveOrganisationId();
        var userId = ResolveUserId();
        var now = DateTime.UtcNow;

        var entity = await FindEntityAsync(descriptor, id, organisationId, cancellationToken);

        if (entity is null)
        {
            return NotFound();
        }

        if (!GetEntityIsActive(entity))
        {
            return NoContent(); // Already inactive
        }

        SetEntityIsActive(entity, false);
        SetEntityUpdatedAt(entity, now);

        db.AuditEvents.Add(new AuditEvent
        {
            Id = Guid.NewGuid(),
            OrganisationId = organisationId,
            ActorUserId = userId,
            EventType = AuditEventTypes.LegendDeactivated,
            MetadataJson = JsonSerializer.Serialize(new
            {
                legendType = descriptor.DisplayName,
                name = GetEntityName(entity),
            }),
            CreatedAtUtc = now,
        });

        try
        {
            await db.SaveChangesAsync(cancellationToken);
        }
        catch (DbUpdateException)
        {
            return Conflict(new ProblemDetails
            {
                Detail = "A concurrency conflict occurred. Please try again.",
                Status = StatusCodes.Status409Conflict,
            });
        }

        return NoContent();
    }

    /// <summary>Reactivate a deactivated legend entry.</summary>
    [HttpPatch("{type}/{id:guid}/reactivate")]
    [ProducesResponseType(typeof(ApiResponse<LegendDto>), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status403Forbidden)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status404NotFound)]
    public async Task<IActionResult> Reactivate(
        string type,
        Guid id,
        CancellationToken cancellationToken = default)
    {
        if (!TypeMap.TryGetValue(type, out var descriptor))
        {
            return BadRequest(InvalidTypeProblem());
        }

        var organisationId = ResolveOrganisationId();
        var userId = ResolveUserId();
        var now = DateTime.UtcNow;

        var entity = await FindEntityAsync(descriptor, id, organisationId, cancellationToken);

        if (entity is null)
        {
            return NotFound();
        }

        if (GetEntityIsActive(entity))
        {
            // Already active — return current state
            var existingDto = MapToDto(entity);
            return Ok(new ApiResponse<LegendDto>(existingDto, new ApiMeta { RequestId = ResolveRequestId() }));
        }

        SetEntityIsActive(entity, true);
        SetEntityUpdatedAt(entity, now);

        db.AuditEvents.Add(new AuditEvent
        {
            Id = Guid.NewGuid(),
            OrganisationId = organisationId,
            ActorUserId = userId,
            EventType = AuditEventTypes.LegendReactivated,
            MetadataJson = JsonSerializer.Serialize(new
            {
                legendType = descriptor.DisplayName,
                name = GetEntityName(entity),
            }),
            CreatedAtUtc = now,
        });

        try
        {
            await db.SaveChangesAsync(cancellationToken);
        }
        catch (DbUpdateException)
        {
            return Conflict(new ProblemDetails
            {
                Detail = "A concurrency conflict occurred. Please try again.",
                Status = StatusCodes.Status409Conflict,
            });
        }

        var dto = MapToDto(entity);
        return Ok(new ApiResponse<LegendDto>(dto, new ApiMeta { RequestId = ResolveRequestId() }));
    }

    private ProblemDetails InvalidTypeProblem() => new()
    {
        Detail = $"Invalid legend type. Valid types: {string.Join(", ", TypeMap.Keys)}",
        Status = StatusCodes.Status400BadRequest,
    };

    private async Task<object?> FindEntityAsync(
        EntityDescriptor descriptor, Guid id, Guid organisationId, CancellationToken ct)
    {
        return await descriptor.DbSet(db)
            .FirstOrDefaultAsync(e =>
                EF.Property<Guid>(e, "Id") == id &&
                EF.Property<Guid>(e, "OrganisationId") == organisationId, ct);
    }

    private async Task<bool> NameExistsAsync(
        EntityDescriptor descriptor, Guid organisationId, string name, Guid? excludeId, CancellationToken ct)
    {
        var query = descriptor.DbSet(db)
            .Where(e => EF.Property<Guid>(e, "OrganisationId") == organisationId &&
                        EF.Property<string>(e, "Name")! == name);

        if (excludeId.HasValue)
        {
            query = query.Where(e => EF.Property<Guid>(e, "Id") != excludeId.Value);
        }

        return await query.AnyAsync(ct);
    }

    private static object CreateEntityInstance(
        EntityDescriptor descriptor, Guid organisationId, string name, Guid userId, DateTime now)
    {
        var type = descriptor switch
        {
            { DisplayName: "OffenceType" } => typeof(OffenceType),
            { DisplayName: "Classification" } => typeof(Classification),
            { DisplayName: "InterventionCategory" } => typeof(InterventionCategory),
            { DisplayName: "EducationLevel" } => typeof(EducationLevel),
            { DisplayName: "Occupation" } => typeof(Occupation),
            { DisplayName: "VisitOutcome" } => typeof(VisitOutcome),
            { DisplayName: "CourtOutcome" } => typeof(CourtOutcome),
            { DisplayName: "Area" } => typeof(Area),
            { DisplayName: "Designation" } => typeof(Designation),
            { DisplayName: "PoliceStation" } => typeof(PoliceStation),
            _ => throw new ArgumentException($"Unknown legend type: {descriptor.DisplayName}")
        };

        var entity = Activator.CreateInstance(type)!;
        var id = Guid.NewGuid();

        type.GetProperty("Id")!.SetValue(entity, id);
        type.GetProperty("OrganisationId")!.SetValue(entity, organisationId);
        type.GetProperty("Name")!.SetValue(entity, name);
        type.GetProperty("IsActive")!.SetValue(entity, true);
        type.GetProperty("CreatedByUserId")!.SetValue(entity, userId);
        type.GetProperty("CreatedAtUtc")!.SetValue(entity, now);
        type.GetProperty("UpdatedAtUtc")!.SetValue(entity, now);

        return entity;
    }

    private static Guid GetEntityId(object entity) =>
        (Guid)entity.GetType().GetProperty("Id")!.GetValue(entity)!;

    private static string GetEntityName(object entity) =>
        (string)entity.GetType().GetProperty("Name")!.GetValue(entity)!;

    private static void SetEntityName(object entity, string name) =>
        entity.GetType().GetProperty("Name")!.SetValue(entity, name);

    private static void SetEntityIsActive(object entity, bool isActive) =>
        entity.GetType().GetProperty("IsActive")!.SetValue(entity, isActive);

    private static void SetEntityUpdatedAt(object entity, DateTime updatedAt) =>
        entity.GetType().GetProperty("UpdatedAtUtc")!.SetValue(entity, updatedAt);

    private static bool GetEntityIsActive(object entity) =>
        (bool)entity.GetType().GetProperty("IsActive")!.GetValue(entity)!;

    private static bool IsUniqueConstraintViolation(DbUpdateException ex)
    {
        var innerMessage = ex.InnerException?.Message ?? string.Empty;
        return innerMessage.Contains("UNIQUE", StringComparison.OrdinalIgnoreCase) ||
               innerMessage.Contains("duplicate", StringComparison.OrdinalIgnoreCase);
    }

    private static LegendDto MapToDto(object entity)
    {
        var type = entity.GetType();

        return new LegendDto(
            (Guid)type.GetProperty("Id")!.GetValue(entity)!,
            (string)type.GetProperty("Name")!.GetValue(entity)!,
            (bool)type.GetProperty("IsActive")!.GetValue(entity)!,
            (DateTime)type.GetProperty("CreatedAtUtc")!.GetValue(entity)!,
            (DateTime)type.GetProperty("UpdatedAtUtc")!.GetValue(entity)!);
    }

    private string ResolveRequestId() =>
        HttpContext.Items[RequestIdMiddleware.RequestIdItemKey] as string
            ?? HttpContext.TraceIdentifier;

    private Guid ResolveOrganisationId()
    {
        var organisationClaim = HttpContext.User.FindFirst(AuthClaimTypes.OrganisationId)?.Value;

        if (!Guid.TryParse(organisationClaim, out var organisationId))
        {
            throw new InvalidOperationException("Authenticated user claims are missing or invalid.");
        }

        return organisationId;
    }

    private Guid ResolveUserId()
    {
        var userIdClaim = HttpContext.User.FindFirst(System.Security.Claims.ClaimTypes.NameIdentifier)?.Value
            ?? HttpContext.User.FindFirst("sub")?.Value;

        if (!Guid.TryParse(userIdClaim, out var userId))
        {
            throw new InvalidOperationException("Authenticated user claims are missing or invalid.");
        }

        return userId;
    }

    private sealed record EntityDescriptor(string DisplayName, Func<AppDbContext, IQueryable<object>> DbSet);
}
