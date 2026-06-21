using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using MidiKaval.Api.Domain.Entities;
using MidiKaval.Api.Domain.Enums;
using MidiKaval.Api.Infrastructure.Audit;
using MidiKaval.Api.Infrastructure.Auth;
using MidiKaval.Api.Infrastructure.Persistence;
using MidiKaval.Api.Models.Cases;

namespace MidiKaval.Api.Infrastructure.Cases;

public sealed class CourtSittingService(
    AppDbContext db,
    IHttpContextAccessor httpContextAccessor,
    CourtMissFlagService courtMissFlagService)
{
    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web);

    private static readonly string[] AllowedStatusNames = ["Upcoming", "Attended", "Postponed"];

    public async Task<CourtSittingDto> CreateAsync(
        Guid caseId,
        CreateCourtSittingRequest request,
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
        EnsureCaseAllowsNewSitting(caseEntity);

        var scheduledAtUtc = NormalizeRequiredUtc(request.ScheduledAtUtc, "scheduledAtUtc");
        var courtName = ValidateCourtName(request.CourtName);
        var purpose = ValidatePurpose(request.Purpose);
        var status = ParseRequiredOrDefaultStatus(request.Status);
        var notes = ValidateOptionalNotes(request.Notes);
        var outcome = ValidateOptionalOutcome(request.Outcome);

        ValidateCreateStatusRules(status, scheduledAtUtc, outcome);

        var now = DateTime.UtcNow;
        var sittingId = Guid.NewGuid();

        var sitting = new CourtSitting
        {
            Id = sittingId,
            OrganisationId = organisationId,
            CaseId = caseId,
            ScheduledAtUtc = scheduledAtUtc,
            CourtName = courtName,
            Purpose = purpose,
            Status = status,
            Notes = notes,
            Outcome = outcome,
            CreatedByUserId = actorUserId,
            CreatedAtUtc = now,
            UpdatedAtUtc = now,
        };

        db.CourtSittings.Add(sitting);
        AddAuditEvent(
            organisationId,
            actorUserId,
            AuditEventTypes.CourtSittingCreated,
            caseId,
            sittingId,
            status);

        await db.SaveChangesAsync(cancellationToken);

        return await MapToDtoAsync(sitting, organisationId, cancellationToken);
    }

    public async Task<CourtSittingListResultDto> ListAsync(
        Guid caseId,
        CancellationToken cancellationToken = default)
    {
        var (organisationId, actorUserId) = ResolveActorContext();
        var actorRole = ResolveActorRole();
        var caseEntity = await LoadCaseAsync(caseId, organisationId, cancellationToken);
        EnsureCanReadCase(caseEntity, actorUserId, actorRole);

        var rows = await (
            from sitting in db.CourtSittings
            join creator in db.Users on sitting.CreatedByUserId equals creator.Id into creators
            from creatorUser in creators.DefaultIfEmpty()
            where sitting.OrganisationId == organisationId && sitting.CaseId == caseId
            orderby sitting.ScheduledAtUtc, sitting.Id
            select new
            {
                Sitting = sitting,
                CreatedByEmail = creatorUser != null
                    && creatorUser.OrganisationId == organisationId
                    && creatorUser.IsActive
                    ? creatorUser.Email
                    : null,
            }).ToListAsync(cancellationToken);

        var items = rows
            .Select(row => MapToDto(row.Sitting, row.CreatedByEmail))
            .ToList();

        return new CourtSittingListResultDto { Items = items };
    }

    public async Task<CourtSittingDto> GetAsync(
        Guid caseId,
        Guid sittingId,
        CancellationToken cancellationToken = default)
    {
        var (organisationId, actorUserId) = ResolveActorContext();
        var actorRole = ResolveActorRole();
        var caseEntity = await LoadCaseAsync(caseId, organisationId, cancellationToken);
        EnsureCanReadCase(caseEntity, actorUserId, actorRole);

        var sitting = await db.CourtSittings.SingleOrDefaultAsync(
            s => s.Id == sittingId
                && s.CaseId == caseId
                && s.OrganisationId == organisationId,
            cancellationToken);

        if (sitting is null)
        {
            throw new CaseNotFoundException();
        }

        return await MapToDtoAsync(sitting, organisationId, cancellationToken);
    }

    public async Task<CourtSittingDto> UpdateAsync(
        Guid caseId,
        Guid sittingId,
        UpdateCourtSittingRequest request,
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

        var sitting = await db.CourtSittings.SingleOrDefaultAsync(
            s => s.Id == sittingId
                && s.CaseId == caseId
                && s.OrganisationId == organisationId,
            cancellationToken);

        if (sitting is null)
        {
            throw new CaseNotFoundException();
        }

        if (request.CourtName is not null)
        {
            sitting.CourtName = ValidateCourtName(request.CourtName);
        }

        if (request.Purpose is not null)
        {
            sitting.Purpose = ValidatePurpose(request.Purpose);
        }

        if (request.Notes is not null)
        {
            sitting.Notes = ValidateOptionalNotes(request.Notes);
        }

        if (request.Outcome is not null)
        {
            sitting.Outcome = ValidateOptionalOutcome(request.Outcome);
        }

        if (request.Status is not null)
        {
            sitting.Status = ParseRequiredStatus(request.Status);
        }

        if (request.ScheduledAtUtc.HasValue)
        {
            var previousScheduledAtUtc = sitting.ScheduledAtUtc;
            sitting.ScheduledAtUtc = NormalizeRequiredUtc(request.ScheduledAtUtc, "scheduledAtUtc");
            if (sitting.ScheduledAtUtc != previousScheduledAtUtc)
            {
                sitting.ReminderSentAtUtc = null;
                if (sitting.ScheduledAtUtc > DateTime.UtcNow)
                {
                    sitting.MissEscalatedAtUtc = null;
                }
            }
        }

        if (request.NextCourtAtUtc.HasValue)
        {
            sitting.NextCourtAtUtc = NormalizeOptionalUtc(request.NextCourtAtUtc);
        }

        ValidateUpdateStatusRules(sitting);

        var now = DateTime.UtcNow;
        sitting.UpdatedAtUtc = now;

        var recalculateCourtMissFlag = request.Status is not null
            && sitting.Status is CourtSittingStatus.Attended or CourtSittingStatus.Postponed
            || request.ScheduledAtUtc.HasValue;

        AddAuditEvent(
            organisationId,
            actorUserId,
            AuditEventTypes.CourtSittingUpdated,
            caseId,
            sittingId,
            sitting.Status);

        await db.SaveChangesAsync(cancellationToken);

        if (recalculateCourtMissFlag)
        {
            await courtMissFlagService.RecalculateCaseCourtMissFlagAsync(
                caseId,
                organisationId,
                cancellationToken);
            await db.SaveChangesAsync(cancellationToken);
        }

        return await MapToDtoAsync(sitting, organisationId, cancellationToken);
    }

    public async Task<(CourtSittingUpcomingListResultDto Result, int TotalCount)> ListUpcomingForFieldWorkerAsync(
        CancellationToken cancellationToken = default)
    {
        var (organisationId, actorUserId) = ResolveActorContext();
        var now = DateTime.UtcNow;

        var rows = await (
            from sitting in db.CourtSittings
            join caseEntity in db.Cases on sitting.CaseId equals caseEntity.Id
            where sitting.OrganisationId == organisationId
                && caseEntity.OrganisationId == organisationId
                && caseEntity.AssignedWorkerId == actorUserId
                && sitting.Status == CourtSittingStatus.Upcoming
                && caseEntity.CurrentStage != CaseStage.TerminationExclusion
            orderby sitting.ScheduledAtUtc, sitting.Id
            select new { Sitting = sitting, Case = caseEntity })
            .ToListAsync(cancellationToken);

        var items = rows
            .Select(row => MapToScheduleItem(row.Sitting, row.Case, now))
            .ToList();

        return (new CourtSittingUpcomingListResultDto { Items = items }, items.Count);
    }

    private async Task<Case> LoadCaseAsync(
        Guid caseId,
        Guid organisationId,
        CancellationToken cancellationToken)
    {
        var caseEntity = await db.Cases
            .Include(c => c.Occupation)
            .Include(c => c.EducationLevel)
            .SingleOrDefaultAsync(
            c => c.Id == caseId && c.OrganisationId == organisationId,
            cancellationToken);

        if (caseEntity is null)
        {
            throw new CaseNotFoundException();
        }

        return caseEntity;
    }

    private static void EnsureCaseAllowsNewSitting(Case caseEntity)
    {
        if (caseEntity.CurrentStage == CaseStage.TerminationExclusion)
        {
            throw new CaseBusinessRuleException("Cannot add court sittings to a terminal case.");
        }
    }

    private async Task<CourtSittingDto> MapToDtoAsync(
        CourtSitting sitting,
        Guid organisationId,
        CancellationToken cancellationToken)
    {
        var createdByEmail = await db.Users
            .Where(u => u.Id == sitting.CreatedByUserId
                && u.OrganisationId == organisationId
                && u.IsActive)
            .Select(u => u.Email)
            .SingleOrDefaultAsync(cancellationToken);

        return MapToDto(sitting, createdByEmail);
    }

    private static CourtSittingDto MapToDto(CourtSitting sitting, string? createdByEmail) =>
        new()
        {
            Id = sitting.Id,
            CaseId = sitting.CaseId,
            ScheduledAtUtc = sitting.ScheduledAtUtc,
            CourtName = sitting.CourtName,
            Purpose = sitting.Purpose,
            Status = sitting.Status.ToString(),
            Notes = sitting.Notes,
            Outcome = sitting.Outcome,
            NextCourtAtUtc = sitting.NextCourtAtUtc,
            CreatedByUserId = sitting.CreatedByUserId,
            CreatedByEmail = createdByEmail,
            CreatedAtUtc = sitting.CreatedAtUtc,
            UpdatedAtUtc = sitting.UpdatedAtUtc,
        };

    private static CourtSittingScheduleItemDto MapToScheduleItem(
        CourtSitting sitting,
        Case caseEntity,
        DateTime nowUtc) =>
        new()
        {
            Id = sitting.Id,
            CaseId = sitting.CaseId,
            ScheduledAtUtc = sitting.ScheduledAtUtc,
            CourtName = sitting.CourtName,
            Purpose = sitting.Purpose,
            Status = sitting.Status.ToString(),
            Notes = sitting.Notes,
            Outcome = sitting.Outcome,
            NextCourtAtUtc = sitting.NextCourtAtUtc,
            IsPastDue = sitting.Status == CourtSittingStatus.Upcoming
                && sitting.ScheduledAtUtc < nowUtc,
            Case = CaseDtoMapper.ToCaseSummary(caseEntity, redactPocsoForFieldWorker: true),
        };

    private void AddAuditEvent(
        Guid organisationId,
        Guid actorUserId,
        string eventType,
        Guid caseId,
        Guid sittingId,
        CourtSittingStatus status)
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
                    ["courtSittingId"] = sittingId.ToString("D"),
                    ["status"] = status.ToString(),
                },
                JsonOptions),
            CreatedAtUtc = DateTime.UtcNow,
        });
    }

    private static bool HasAnyUpdateField(UpdateCourtSittingRequest request) =>
        request.Status is not null
        || request.ScheduledAtUtc.HasValue
        || request.CourtName is not null
        || request.Purpose is not null
        || request.Notes is not null
        || request.Outcome is not null
        || request.NextCourtAtUtc.HasValue;

    private static CourtSittingStatus? ParseStatus(string? value)
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

        return Enum.Parse<CourtSittingStatus>(trimmed, ignoreCase: false);
    }

    private static CourtSittingStatus ParseRequiredOrDefaultStatus(string? value)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            return CourtSittingStatus.Upcoming;
        }

        return ParseStatus(value)
            ?? throw new CaseValidationException(
                "status is invalid. Allowed values: Upcoming, Attended, Postponed.");
    }

    private static CourtSittingStatus ParseRequiredStatus(string value) =>
        ParseStatus(value)
        ?? throw new CaseValidationException(
            "status is invalid. Allowed values: Upcoming, Attended, Postponed.");

    private static string ValidateCourtName(string? value)
    {
        var trimmed = value?.Trim() ?? string.Empty;
        if (trimmed.Length == 0)
        {
            throw new CaseValidationException("courtName is required.");
        }

        if (trimmed.Length > 128)
        {
            throw new CaseValidationException("courtName must be at most 128 characters.");
        }

        return trimmed;
    }

    private static string ValidatePurpose(string? value)
    {
        var trimmed = value?.Trim() ?? string.Empty;
        if (trimmed.Length == 0)
        {
            throw new CaseValidationException("purpose is required.");
        }

        if (trimmed.Length > 500)
        {
            throw new CaseValidationException("purpose must be at most 500 characters.");
        }

        return trimmed;
    }

    private static string? ValidateOptionalNotes(string? value)
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

        if (trimmed.Length > 4000)
        {
            throw new CaseValidationException("notes must be at most 4000 characters.");
        }

        return trimmed;
    }

    private static string? ValidateOptionalOutcome(string? value)
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

    private static void ValidateCreateStatusRules(
        CourtSittingStatus status,
        DateTime scheduledAtUtc,
        string? outcome)
    {
        if (status == CourtSittingStatus.Attended && string.IsNullOrWhiteSpace(outcome))
        {
            throw new CaseValidationException("outcome is required when status is Attended.");
        }

        if (status == CourtSittingStatus.Upcoming && scheduledAtUtc < DateTime.UtcNow)
        {
            throw new CaseBusinessRuleException("Scheduled date must be in the future for new Upcoming sittings.");
        }
    }

    private static void ValidateUpdateStatusRules(CourtSitting sitting)
    {
        if (sitting.Status == CourtSittingStatus.Attended)
        {
            if (string.IsNullOrWhiteSpace(sitting.Outcome))
            {
                throw new CaseValidationException("outcome is required when status is Attended.");
            }

            if (sitting.NextCourtAtUtc is not null && sitting.NextCourtAtUtc.Value < DateTime.UtcNow)
            {
                throw new CaseBusinessRuleException("nextCourtAtUtc must be in the future.");
            }
        }

        if (sitting.Status == CourtSittingStatus.Upcoming && sitting.ScheduledAtUtc < DateTime.UtcNow)
        {
            throw new CaseBusinessRuleException("Scheduled date must be in the future when status is Upcoming.");
        }
    }

    private static DateTime NormalizeRequiredUtc(DateTime? value, string fieldName)
    {
        if (value is null)
        {
            throw new CaseValidationException($"{fieldName} is required.");
        }

        if (value.Value.Kind == DateTimeKind.Unspecified)
        {
            throw new CaseValidationException("Timestamps must be valid UTC values.");
        }

        return value.Value.Kind == DateTimeKind.Utc
            ? value.Value
            : value.Value.ToUniversalTime();
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
