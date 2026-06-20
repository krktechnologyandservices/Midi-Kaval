using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using MidiKaval.Api.Domain.Entities;
using MidiKaval.Api.Domain.Enums;
using MidiKaval.Api.Infrastructure.Audit;
using MidiKaval.Api.Infrastructure.Auth;
using MidiKaval.Api.Infrastructure.Notifications;
using MidiKaval.Api.Infrastructure.Persistence;
using MidiKaval.Api.Models.Cases;

namespace MidiKaval.Api.Infrastructure.Cases;

public sealed class InterventionService(
    AppDbContext db,
    IHttpContextAccessor httpContextAccessor,
    NotificationService notificationService)
{
    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web);

    private static readonly string[] AllowedDirectionNames = ["Needed", "Provided"];
    private static readonly string[] AllowedPriorityNames = ["High", "Medium", "Low"];
    private static readonly string[] AllowedStatusNames = ["Open", "InProgress", "Completed", "Cancelled"];

    public async Task<InterventionDto> CreateAsync(
        Guid caseId,
        CreateInterventionRequest request,
        CancellationToken cancellationToken = default)
    {
        if (request is null)
        {
            throw new CaseValidationException("Request body is required.");
        }

        var (organisationId, actorUserId) = ResolveActorContext();
        var actorRole = ResolveActorRole();
        var caseEntity = await LoadCaseAsync(caseId, organisationId, cancellationToken);
        EnsureCanReadCase(caseEntity, actorUserId, actorRole);

        var direction = ParseDirection(request.Direction);
        var categoryName = ValidateCategoryName(request.CategoryName);
        var description = ValidateDescription(request.Description);
        var priority = ParsePriority(request.Priority);
        var status = ParseRequiredOrDefaultStatus(request.Status);
        var outcome = ValidateOutcome(request.Outcome);
        var assignedStaffUserId = ValidateAssignedStaffUserId(request.AssignedStaffUserId);
        await EnsureActiveOrgUserAsync(assignedStaffUserId, organisationId, cancellationToken);

        var dueAtUtc = NormalizeOptionalUtc(request.DueAtUtc);
        var providedAtUtc = NormalizeOptionalUtc(request.ProvidedAtUtc);
        ValidateDirectionFields(direction, status, dueAtUtc, providedAtUtc);

        var now = DateTime.UtcNow;
        var interventionId = Guid.NewGuid();

        var intervention = new Intervention
        {
            Id = interventionId,
            OrganisationId = organisationId,
            CaseId = caseId,
            Direction = direction,
            CategoryName = categoryName,
            Description = description,
            Priority = priority,
            Status = status,
            DueAtUtc = dueAtUtc,
            ProvidedAtUtc = providedAtUtc,
            Outcome = outcome,
            AssignedStaffUserId = assignedStaffUserId,
            CreatedByUserId = actorUserId,
            CreatedAtUtc = now,
            UpdatedAtUtc = now,
        };

        db.Interventions.Add(intervention);
        AddAuditEvent(
            organisationId,
            actorUserId,
            AuditEventTypes.InterventionCreated,
            caseId,
            interventionId,
            status,
            priority);

        await db.SaveChangesAsync(cancellationToken);

        return await MapToDtoAsync(intervention, organisationId, cancellationToken);
    }

    public async Task<InterventionListResultDto> ListAsync(
        Guid caseId,
        CancellationToken cancellationToken = default)
    {
        var (organisationId, actorUserId) = ResolveActorContext();
        var actorRole = ResolveActorRole();
        var caseEntity = await LoadCaseAsync(caseId, organisationId, cancellationToken);
        EnsureCanReadCase(caseEntity, actorUserId, actorRole);

        var rows = await (
            from intervention in db.Interventions
            join assignee in db.Users on intervention.AssignedStaffUserId equals assignee.Id into assignees
            from assigneeUser in assignees.DefaultIfEmpty()
            join creator in db.Users on intervention.CreatedByUserId equals creator.Id into creators
            from creatorUser in creators.DefaultIfEmpty()
            where intervention.OrganisationId == organisationId && intervention.CaseId == caseId
            orderby intervention.CreatedAtUtc, intervention.Id
            select new
            {
                Intervention = intervention,
                AssignedStaffEmail = assigneeUser != null
                    && assigneeUser.OrganisationId == organisationId
                    && assigneeUser.IsActive
                    ? assigneeUser.Email
                    : null,
                CreatedByEmail = creatorUser != null
                    && creatorUser.OrganisationId == organisationId
                    && creatorUser.IsActive
                    ? creatorUser.Email
                    : null,
            }).ToListAsync(cancellationToken);

        var items = rows
            .Select(row => MapToDto(row.Intervention, row.AssignedStaffEmail, row.CreatedByEmail))
            .ToList();

        return new InterventionListResultDto { Items = items };
    }

    public async Task<InterventionDto> GetAsync(
        Guid caseId,
        Guid interventionId,
        CancellationToken cancellationToken = default)
    {
        var (organisationId, actorUserId) = ResolveActorContext();
        var actorRole = ResolveActorRole();
        var caseEntity = await LoadCaseAsync(caseId, organisationId, cancellationToken);
        EnsureCanReadCase(caseEntity, actorUserId, actorRole);

        var intervention = await db.Interventions.SingleOrDefaultAsync(
            i => i.Id == interventionId
                && i.CaseId == caseId
                && i.OrganisationId == organisationId,
            cancellationToken);

        if (intervention is null)
        {
            throw new CaseNotFoundException();
        }

        return await MapToDtoAsync(intervention, organisationId, cancellationToken);
    }

    public async Task<InterventionDto> UpdateAsync(
        Guid caseId,
        Guid interventionId,
        UpdateInterventionRequest request,
        CancellationToken cancellationToken = default)
    {
        if (request is null)
        {
            throw new CaseValidationException("Request body is required.");
        }

        if (!HasAnyUpdateField(request))
        {
            throw new CaseValidationException("At least one field must be supplied for update.");
        }

        var (organisationId, actorUserId) = ResolveActorContext();
        var actorRole = ResolveActorRole();
        var caseEntity = await LoadCaseAsync(caseId, organisationId, cancellationToken);
        EnsureCanReadCase(caseEntity, actorUserId, actorRole);

        var intervention = await db.Interventions.SingleOrDefaultAsync(
            i => i.Id == interventionId
                && i.CaseId == caseId
                && i.OrganisationId == organisationId,
            cancellationToken);

        if (intervention is null)
        {
            throw new CaseNotFoundException();
        }

        if (request.CategoryName is not null)
        {
            intervention.CategoryName = ValidateCategoryName(request.CategoryName);
        }

        if (request.Description is not null)
        {
            intervention.Description = ValidateDescription(request.Description);
        }

        if (request.Priority is not null)
        {
            intervention.Priority = ParsePriority(request.Priority);
        }

        InterventionStatus? newStatus = null;
        if (request.Status is not null)
        {
            newStatus = ParseStatus(request.Status)
                ?? throw new CaseValidationException(
                    "status is invalid. Allowed values: Open, InProgress, Completed, Cancelled.");
            intervention.Status = newStatus.Value;
        }

        if (request.DueAtUtc.HasValue)
        {
            intervention.DueAtUtc = NormalizeOptionalUtc(request.DueAtUtc);
        }

        if (request.ProvidedAtUtc.HasValue)
        {
            intervention.ProvidedAtUtc = NormalizeOptionalUtc(request.ProvidedAtUtc);
        }

        if (request.Outcome is not null)
        {
            intervention.Outcome = ValidateOutcome(request.Outcome);
        }

        if (request.AssignedStaffUserId.HasValue)
        {
            var assignedStaffUserId = ValidateAssignedStaffUserId(request.AssignedStaffUserId);
            await EnsureActiveOrgUserAsync(assignedStaffUserId, organisationId, cancellationToken);
            intervention.AssignedStaffUserId = assignedStaffUserId;
        }

        ValidateDirectionFields(
            intervention.Direction,
            intervention.Status,
            intervention.DueAtUtc,
            intervention.ProvidedAtUtc,
            allowPastDueOnUpdate: true);

        if (intervention.Direction == InterventionDirection.Needed
            && intervention.Status == InterventionStatus.Open
            && intervention.DueAtUtc is null)
        {
            throw new CaseValidationException("dueAtUtc is required for Needed interventions with Open status.");
        }

        if (intervention.Direction == InterventionDirection.Provided && intervention.ProvidedAtUtc is null)
        {
            throw new CaseValidationException("providedAtUtc is required for Provided interventions.");
        }

        if ((intervention.Status == InterventionStatus.Completed
                || intervention.Status == InterventionStatus.Cancelled)
            && string.IsNullOrWhiteSpace(intervention.Outcome))
        {
            throw new CaseValidationException("outcome is required when status is Completed or Cancelled.");
        }

        var now = DateTime.UtcNow;
        intervention.UpdatedAtUtc = now;

        AddAuditEvent(
            organisationId,
            actorUserId,
            AuditEventTypes.InterventionUpdated,
            caseId,
            interventionId,
            intervention.Status,
            intervention.Priority);

        await db.SaveChangesAsync(cancellationToken);

        if (IsTerminalWithOutcome(intervention))
        {
            await notificationService.ResolveInterventionOverdueAsync(
                interventionId,
                organisationId,
                cancellationToken);
        }

        return await MapToDtoAsync(intervention, organisationId, cancellationToken);
    }

    private static bool IsTerminalWithOutcome(Intervention intervention) =>
        (intervention.Status == InterventionStatus.Completed
            || intervention.Status == InterventionStatus.Cancelled)
        && !string.IsNullOrWhiteSpace(intervention.Outcome);

    private async Task<Case> LoadCaseAsync(
        Guid caseId,
        Guid organisationId,
        CancellationToken cancellationToken)
    {
        var caseEntity = await db.Cases.SingleOrDefaultAsync(
            c => c.Id == caseId && c.OrganisationId == organisationId,
            cancellationToken);

        if (caseEntity is null)
        {
            throw new CaseNotFoundException();
        }

        return caseEntity;
    }

    private async Task<InterventionDto> MapToDtoAsync(
        Intervention intervention,
        Guid organisationId,
        CancellationToken cancellationToken)
    {
        var userIds = new[] { intervention.AssignedStaffUserId, intervention.CreatedByUserId }.Distinct().ToList();
        var emails = await db.Users
            .Where(u => userIds.Contains(u.Id) && u.OrganisationId == organisationId && u.IsActive)
            .ToDictionaryAsync(u => u.Id, u => u.Email, cancellationToken);

        emails.TryGetValue(intervention.AssignedStaffUserId, out var assignedEmail);
        emails.TryGetValue(intervention.CreatedByUserId, out var createdByEmail);

        return MapToDto(intervention, assignedEmail, createdByEmail);
    }

    private static InterventionDto MapToDto(
        Intervention intervention,
        string? assignedStaffEmail,
        string? createdByEmail) =>
        new()
        {
            Id = intervention.Id,
            CaseId = intervention.CaseId,
            Direction = intervention.Direction.ToString(),
            CategoryName = intervention.CategoryName,
            Description = intervention.Description,
            Priority = intervention.Priority.ToString(),
            Status = intervention.Status.ToString(),
            DueAtUtc = intervention.DueAtUtc,
            ProvidedAtUtc = intervention.ProvidedAtUtc,
            Outcome = intervention.Outcome,
            AssignedStaffUserId = intervention.AssignedStaffUserId,
            AssignedStaffEmail = assignedStaffEmail,
            CreatedByUserId = intervention.CreatedByUserId,
            CreatedByEmail = createdByEmail,
            CreatedAtUtc = intervention.CreatedAtUtc,
            UpdatedAtUtc = intervention.UpdatedAtUtc,
        };

    private void AddAuditEvent(
        Guid organisationId,
        Guid actorUserId,
        string eventType,
        Guid caseId,
        Guid interventionId,
        InterventionStatus status,
        InterventionPriority priority)
    {
        db.AuditEvents.Add(new AuditEvent
        {
            Id = Guid.NewGuid(),
            OrganisationId = organisationId,
            ActorUserId = actorUserId,
            SubjectUserId = actorUserId,
            EventType = eventType,
            MetadataJson = JsonSerializer.Serialize(
                new Dictionary<string, object?>
                {
                    ["caseId"] = caseId.ToString("D"),
                    ["interventionId"] = interventionId.ToString("D"),
                    ["status"] = status.ToString(),
                    ["priority"] = priority.ToString(),
                },
                JsonOptions),
            CreatedAtUtc = DateTime.UtcNow,
        });
    }

    private static bool HasAnyUpdateField(UpdateInterventionRequest request) =>
        request.CategoryName is not null
        || request.Description is not null
        || request.Priority is not null
        || request.Status is not null
        || request.DueAtUtc.HasValue
        || request.ProvidedAtUtc.HasValue
        || request.Outcome is not null
        || request.AssignedStaffUserId.HasValue;

    private static InterventionDirection ParseDirection(string? value)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            throw new CaseValidationException(
                "direction is required. Allowed values: Needed, Provided.");
        }

        var trimmed = value.Trim();
        if (!AllowedDirectionNames.Contains(trimmed, StringComparer.Ordinal))
        {
            throw new CaseValidationException(
                "direction is invalid. Allowed values: Needed, Provided.");
        }

        return Enum.Parse<InterventionDirection>(trimmed, ignoreCase: false);
    }

    private static InterventionPriority ParsePriority(string? value)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            throw new CaseValidationException(
                "priority is required. Allowed values: High, Medium, Low.");
        }

        var trimmed = value.Trim();
        if (!AllowedPriorityNames.Contains(trimmed, StringComparer.Ordinal))
        {
            throw new CaseValidationException(
                "priority is invalid. Allowed values: High, Medium, Low.");
        }

        return Enum.Parse<InterventionPriority>(trimmed, ignoreCase: false);
    }

    private static InterventionStatus? ParseStatus(string? value)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            return null;
        }

        var trimmed = value.Trim();
        if (!AllowedStatusNames.Contains(trimmed, StringComparer.Ordinal))
        {
            return null;
        }

        return Enum.Parse<InterventionStatus>(trimmed, ignoreCase: false);
    }

    private static InterventionStatus ParseRequiredOrDefaultStatus(string? value)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            return InterventionStatus.Open;
        }

        return ParseStatus(value)
            ?? throw new CaseValidationException(
                "status is invalid. Allowed values: Open, InProgress, Completed, Cancelled.");
    }

    private static string ValidateCategoryName(string? value)
    {
        var trimmed = value?.Trim() ?? string.Empty;
        if (trimmed.Length == 0)
        {
            throw new CaseValidationException("categoryName is required.");
        }

        if (trimmed.Length > 128)
        {
            throw new CaseValidationException("categoryName must be at most 128 characters.");
        }

        return trimmed;
    }

    private static string ValidateDescription(string? value)
    {
        var trimmed = value?.Trim() ?? string.Empty;
        if (trimmed.Length == 0)
        {
            throw new CaseValidationException("description is required.");
        }

        if (trimmed.Length > 4000)
        {
            throw new CaseValidationException("description must be at most 4000 characters.");
        }

        return trimmed;
    }

    private static string? ValidateOutcome(string? value)
    {
        if (value is null)
        {
            return null;
        }

        var trimmed = value.Trim();
        if (trimmed.Length == 0)
        {
            return null;
        }

        if (trimmed.Length > 2000)
        {
            throw new CaseValidationException("outcome must be at most 2000 characters.");
        }

        return trimmed;
    }

    private static Guid ValidateAssignedStaffUserId(Guid? value)
    {
        if (value is null || value == Guid.Empty)
        {
            throw new CaseValidationException("assignedStaffUserId is required.");
        }

        return value.Value;
    }

    private async Task EnsureActiveOrgUserAsync(
        Guid userId,
        Guid organisationId,
        CancellationToken cancellationToken)
    {
        var exists = await db.Users.AnyAsync(
            u => u.Id == userId && u.OrganisationId == organisationId && u.IsActive,
            cancellationToken);

        if (!exists)
        {
            throw new CaseValidationException("assignedStaffUserId must reference an active user in your organisation.");
        }
    }

    private static DateTime? NormalizeOptionalUtc(DateTime? value)
    {
        if (value is null)
        {
            return null;
        }

        if (value.Value.Kind == DateTimeKind.Unspecified)
        {
            throw new CaseValidationException("Timestamps must be valid UTC values.");
        }

        return value.Value.Kind == DateTimeKind.Utc
            ? value.Value
            : value.Value.ToUniversalTime();
    }

    private static void ValidateDirectionFields(
        InterventionDirection direction,
        InterventionStatus status,
        DateTime? dueAtUtc,
        DateTime? providedAtUtc,
        bool allowPastDueOnUpdate = false)
    {
        if (direction == InterventionDirection.Needed)
        {
            if (status == InterventionStatus.Open && dueAtUtc is null)
            {
                throw new CaseValidationException("dueAtUtc is required for Needed interventions with Open status.");
            }

            if (dueAtUtc is not null
                && !allowPastDueOnUpdate
                && dueAtUtc.Value < DateTime.UtcNow)
            {
                throw new CaseBusinessRuleException("Due date must be in the future.");
            }
        }

        if (direction == InterventionDirection.Provided && providedAtUtc is null)
        {
            throw new CaseValidationException("providedAtUtc is required for Provided interventions.");
        }
    }

    private (Guid OrganisationId, Guid ActorUserId) ResolveActorContext()
    {
        var httpContext = httpContextAccessor.HttpContext
            ?? throw new InvalidOperationException("HTTP context is required.");

        var principal = httpContext.User;
        var organisationClaim = principal.FindFirst(AuthClaimTypes.OrganisationId)?.Value;
        var userIdClaim = principal.FindFirst(ClaimTypes.NameIdentifier)?.Value
            ?? principal.FindFirst(JwtRegisteredClaimNames.Sub)?.Value;

        if (!Guid.TryParse(organisationClaim, out var organisationId)
            || !Guid.TryParse(userIdClaim, out var actorUserId))
        {
            throw new InvalidOperationException("Authenticated user claims are missing or invalid.");
        }

        return (organisationId, actorUserId);
    }

    private string ResolveActorRole() =>
        httpContextAccessor.HttpContext?.User.FindFirstValue(ClaimTypes.Role) ?? string.Empty;

    private static void EnsureCanReadCase(Case entity, Guid actorUserId, string actorRole)
    {
        if (IsSupervisorRole(actorRole))
        {
            return;
        }

        if (entity.AssignedWorkerId != actorUserId)
        {
            throw new CaseForbiddenException();
        }
    }

    private static bool IsSupervisorRole(string role) =>
        role is UserRoles.Director or UserRoles.Coordinator;
}
