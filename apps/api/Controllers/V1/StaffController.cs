using System.Text.Json;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.RateLimiting;
using Microsoft.EntityFrameworkCore;
using MidiKaval.Api.Domain.Entities;
using MidiKaval.Api.Infrastructure.Audit;
using MidiKaval.Api.Infrastructure.Auth;
using MidiKaval.Api.Infrastructure;
using MidiKaval.Api.Infrastructure.Persistence;
using MidiKaval.Api.Models;
using MidiKaval.Api.Models.Users;

namespace MidiKaval.Api.Controllers.V1;

[ApiController]
[Authorize(Policy = Policies.DirectorOnly)]
[Route("api/v1/staff")]
[Produces("application/json")]
public sealed class StaffController(AppDbContext db) : ControllerBase
{
    [HttpGet]
    [ProducesResponseType(typeof(ApiResponse<StaffListResultDto>), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status403Forbidden)]
    [EnableRateLimiting("data-read")]
    public async Task<IActionResult> List(CancellationToken cancellationToken)
    {
        var organisationId = ResolveOrganisationId();

        var items = await db.Users
            .Where(u => u.OrganisationId == organisationId)
            .OrderBy(u => u.FirstName)
            .ThenBy(u => u.LastName)
            .Select(u => new StaffDto(
                u.Id,
                u.Email,
                u.FirstName,
                u.LastName,
                u.PhoneNumber,
                u.Role,
                u.IsActive,
                u.CreatedAtUtc,
                u.UpdatedAtUtc))
            .ToListAsync(cancellationToken);

        return Ok(new ApiResponse<StaffListResultDto>(
            new StaffListResultDto(items),
            new ApiMeta { RequestId = ResolveRequestId() }));
    }

    [HttpGet("{id:guid}")]
    [ProducesResponseType(typeof(ApiResponse<StaffDto>), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status403Forbidden)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status404NotFound)]
    [EnableRateLimiting("data-read")]
    public async Task<IActionResult> Get(Guid id, CancellationToken cancellationToken)
    {
        var organisationId = ResolveOrganisationId();

        var user = await db.Users
            .FirstOrDefaultAsync(u => u.Id == id && u.OrganisationId == organisationId, cancellationToken);

        if (user is null)
        {
            return NotFound();
        }

        var dto = MapToDto(user);
        return Ok(new ApiResponse<StaffDto>(dto, new ApiMeta { RequestId = ResolveRequestId() }));
    }

    [HttpPost]
    [ProducesResponseType(typeof(ApiResponse<StaffDto>), StatusCodes.Status201Created)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status403Forbidden)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status409Conflict)]
    [EnableRateLimiting("data-write")]
    public async Task<IActionResult> Create(
        CreateStaffRequest request,
        CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(request.Email) ||
            string.IsNullOrWhiteSpace(request.FirstName) ||
            string.IsNullOrWhiteSpace(request.LastName))
        {
            return Problem(
                detail: "Email, first name, and last name are required.",
                statusCode: StatusCodes.Status400BadRequest);
        }

        if (!IsValidRole(request.Role))
        {
            return Problem(
                detail: $"Invalid role '{request.Role}'. Valid roles: {string.Join(", ", UserRoles.All)}",
                statusCode: StatusCodes.Status400BadRequest);
        }

        var organisationId = ResolveOrganisationId();
        var actorUserId = ResolveUserId();
        var now = DateTime.UtcNow;

        if (await db.Users.AnyAsync(u =>
            u.OrganisationId == organisationId &&
            u.Email == request.Email, cancellationToken))
        {
            return Problem(
                detail: "A user with this email already exists in your organisation.",
                statusCode: StatusCodes.Status409Conflict);
        }

        var user = new User
        {
            Id = Guid.NewGuid(),
            OrganisationId = organisationId,
            Email = request.Email.Trim().ToLowerInvariant(),
            FirstName = request.FirstName.Trim(),
            LastName = request.LastName.Trim(),
            PhoneNumber = request.PhoneNumber?.Trim(),
            Role = request.Role,
            TokenVersion = 1,
            PasswordHash = string.Empty,
            IsActive = true,
            CreatedAtUtc = now,
            UpdatedAtUtc = now,
        };

        db.Users.Add(user);
        db.AuditEvents.Add(new AuditEvent
        {
            Id = Guid.NewGuid(),
            OrganisationId = organisationId,
            ActorUserId = actorUserId,
            SubjectUserId = user.Id,
            EventType = AuditEventTypes.StaffCreated,
            MetadataJson = JsonSerializer.Serialize(new
            {
                email = user.Email,
                firstName = user.FirstName,
                lastName = user.LastName,
                role = user.Role,
            }),
            CreatedAtUtc = now,
        });

        try
        {
            await db.SaveChangesAsync(cancellationToken);
        }
        catch (DbUpdateException ex) when (IsUniqueConstraintViolation(ex))
        {
            return Problem(
                detail: "A user with this email already exists in your organisation.",
                statusCode: StatusCodes.Status409Conflict);
        }

        var dto = MapToDto(user);
        return CreatedAtAction(nameof(Get), new { id = user.Id },
            new ApiResponse<StaffDto>(dto, new ApiMeta { RequestId = ResolveRequestId() }));
    }

    [HttpPut("{id:guid}")]
    [ProducesResponseType(typeof(ApiResponse<StaffDto>), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status403Forbidden)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status404NotFound)]
    [EnableRateLimiting("data-write")]
    public async Task<IActionResult> Update(
        Guid id,
        UpdateStaffRequest request,
        CancellationToken cancellationToken)
    {
        var organisationId = ResolveOrganisationId();
        var actorUserId = ResolveUserId();
        var now = DateTime.UtcNow;

        var user = await db.Users
            .FirstOrDefaultAsync(u => u.Id == id && u.OrganisationId == organisationId, cancellationToken);

        if (user is null)
        {
            return NotFound();
        }

        if (string.IsNullOrWhiteSpace(request.FirstName) ||
            string.IsNullOrWhiteSpace(request.LastName))
        {
            return Problem(
                detail: "First name and last name are required.",
                statusCode: StatusCodes.Status400BadRequest);
        }

        var previousRole = user.Role;

        user.FirstName = request.FirstName.Trim();
        user.LastName = request.LastName.Trim();
        user.PhoneNumber = request.PhoneNumber?.Trim();

        if (!string.IsNullOrEmpty(request.Role) && request.Role != user.Role)
        {
            if (!IsValidRole(request.Role))
            {
                return Problem(
                    detail: $"Invalid role '{request.Role}'.",
                    statusCode: StatusCodes.Status400BadRequest);
            }

            user.Role = request.Role;
            user.TokenVersion++;
        }

        user.UpdatedAtUtc = now;

        db.AuditEvents.Add(new AuditEvent
        {
            Id = Guid.NewGuid(),
            OrganisationId = organisationId,
            ActorUserId = actorUserId,
            SubjectUserId = user.Id,
            EventType = AuditEventTypes.StaffUpdated,
            MetadataJson = JsonSerializer.Serialize(new
            {
                email = user.Email,
                firstName = user.FirstName,
                lastName = user.LastName,
                previousRole,
                newRole = user.Role,
                roleChanged = previousRole != user.Role,
            }),
            CreatedAtUtc = now,
        });

        await db.SaveChangesAsync(cancellationToken);

        var dto = MapToDto(user);
        return Ok(new ApiResponse<StaffDto>(dto, new ApiMeta { RequestId = ResolveRequestId() }));
    }

    [HttpDelete("{id:guid}")]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status403Forbidden)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status404NotFound)]
    [EnableRateLimiting("data-write")]
    public async Task<IActionResult> Deactivate(Guid id, CancellationToken cancellationToken)
    {
        var organisationId = ResolveOrganisationId();
        var actorUserId = ResolveUserId();
        var now = DateTime.UtcNow;

        var user = await db.Users
            .FirstOrDefaultAsync(u => u.Id == id && u.OrganisationId == organisationId, cancellationToken);

        if (user is null)
        {
            return NotFound();
        }

        if (user.Id == actorUserId)
        {
            return Problem(
                detail: "You cannot deactivate your own account.",
                statusCode: StatusCodes.Status400BadRequest);
        }

        if (!user.IsActive)
        {
            return NoContent();
        }

        user.IsActive = false;
        user.TokenVersion++;
        user.UpdatedAtUtc = now;

        db.AuditEvents.Add(new AuditEvent
        {
            Id = Guid.NewGuid(),
            OrganisationId = organisationId,
            ActorUserId = actorUserId,
            SubjectUserId = user.Id,
            EventType = AuditEventTypes.StaffDeactivated,
            MetadataJson = JsonSerializer.Serialize(new
            {
                email = user.Email,
                firstName = user.FirstName,
                lastName = user.LastName,
            }),
            CreatedAtUtc = now,
        });

        await db.SaveChangesAsync(cancellationToken);

        return NoContent();
    }

    [HttpPatch("{id:guid}/reactivate")]
    [ProducesResponseType(typeof(ApiResponse<StaffDto>), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status403Forbidden)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status404NotFound)]
    [EnableRateLimiting("data-write")]
    public async Task<IActionResult> Reactivate(Guid id, CancellationToken cancellationToken)
    {
        var organisationId = ResolveOrganisationId();
        var actorUserId = ResolveUserId();
        var now = DateTime.UtcNow;

        var user = await db.Users
            .FirstOrDefaultAsync(u => u.Id == id && u.OrganisationId == organisationId, cancellationToken);

        if (user is null)
        {
            return NotFound();
        }

        if (user.IsActive)
        {
            return NoContent();
        }

        user.IsActive = true;
        user.UpdatedAtUtc = now;

        db.AuditEvents.Add(new AuditEvent
        {
            Id = Guid.NewGuid(),
            OrganisationId = organisationId,
            ActorUserId = actorUserId,
            SubjectUserId = user.Id,
            EventType = AuditEventTypes.StaffReactivated,
            MetadataJson = JsonSerializer.Serialize(new
            {
                email = user.Email,
                firstName = user.FirstName,
                lastName = user.LastName,
            }),
            CreatedAtUtc = now,
        });

        await db.SaveChangesAsync(cancellationToken);

        var dto = MapToDto(user);
        return Ok(new ApiResponse<StaffDto>(dto, new ApiMeta { RequestId = ResolveRequestId() }));
    }

    [HttpPost("{id:guid}/force-reset")]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status403Forbidden)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status404NotFound)]
    [EnableRateLimiting("data-write")]
    public async Task<IActionResult> ForceReset(Guid id, CancellationToken cancellationToken)
    {
        var organisationId = ResolveOrganisationId();
        var actorUserId = ResolveUserId();
        var now = DateTime.UtcNow;

        var user = await db.Users
            .FirstOrDefaultAsync(u => u.Id == id && u.OrganisationId == organisationId, cancellationToken);

        if (user is null)
        {
            return NotFound();
        }

        if (user.Id == actorUserId)
        {
            return Problem(
                detail: "You cannot force-reset your own account.",
                statusCode: StatusCodes.Status400BadRequest);
        }

        user.TokenVersion++;
        user.PasswordHash = string.Empty;
        user.UpdatedAtUtc = now;

        db.AuditEvents.Add(new AuditEvent
        {
            Id = Guid.NewGuid(),
            OrganisationId = organisationId,
            ActorUserId = actorUserId,
            SubjectUserId = user.Id,
            EventType = AuditEventTypes.StaffForceReset,
            MetadataJson = JsonSerializer.Serialize(new
            {
                email = user.Email,
                firstName = user.FirstName,
                lastName = user.LastName,
            }),
            CreatedAtUtc = now,
        });

        await db.SaveChangesAsync(cancellationToken);

        return NoContent();
    }

    private static StaffDto MapToDto(User user) => new(
        user.Id,
        user.Email,
        user.FirstName,
        user.LastName,
        user.PhoneNumber,
        user.Role,
        user.IsActive,
        user.CreatedAtUtc,
        user.UpdatedAtUtc);

    private static bool IsValidRole(string role) =>
        UserRoles.IsValid(role);

    private static bool IsUniqueConstraintViolation(DbUpdateException ex)
    {
        var innerMessage = ex.InnerException?.Message ?? string.Empty;
        return innerMessage.Contains("UNIQUE", StringComparison.OrdinalIgnoreCase) ||
               innerMessage.Contains("duplicate", StringComparison.OrdinalIgnoreCase);
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
}
