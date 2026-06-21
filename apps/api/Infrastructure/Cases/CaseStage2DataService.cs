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

public sealed class CaseStage2DataService(
    AppDbContext db,
    IHttpContextAccessor httpContextAccessor)
{
    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web);
    private const int MaxFieldLength = 4000;

    public async Task<Stage2DataDto> GetAsync(Guid caseId, CancellationToken ct)
    {
        var (organisationId, _) = ResolveActorContext();
        var caseEntity = await db.Cases
            .Where(c => c.Id == caseId && c.OrganisationId == organisationId)
            .Select(c => new { c.CurrentStage })
            .FirstOrDefaultAsync(ct);

        if (caseEntity is null)
            throw new CaseNotFoundException();

        if (caseEntity.CurrentStage != CaseStage.MaintainAndDevelopment)
            throw new CaseNotFoundException();

        var data = await db.Set<CaseStage2Data>()
            .FirstOrDefaultAsync(d => d.CaseId == caseId, ct);

        return ToDto(data, caseId);
    }

    public async Task<Stage2DataDto> UpsertAsync(Guid caseId, UpsertStage2DataRequest request, CancellationToken ct)
    {
        var (organisationId, actorUserId) = ResolveActorContext();

        var caseEntity = await db.Cases
            .Where(c => c.Id == caseId && c.OrganisationId == organisationId)
            .Select(c => new { c.CurrentStage })
            .FirstOrDefaultAsync(ct);

        if (caseEntity is null)
            throw new CaseNotFoundException();

        if (caseEntity.CurrentStage != CaseStage.MaintainAndDevelopment)
            throw new CaseNotFoundException();

        ValidateFieldLengths(request);

        var now = DateTime.UtcNow;
        var existing = await db.Set<CaseStage2Data>()
            .FirstOrDefaultAsync(d => d.CaseId == caseId, ct);

        if (existing is not null)
        {
            existing.BioPsychoSocialAssessment = request.BioPsychoSocialAssessment;
            existing.IcpRecords = request.IcpRecords;
            existing.LifeSkillTraining = request.LifeSkillTraining;
            existing.ParentManagement = request.ParentManagement;
            existing.GroupWork = request.GroupWork;
            existing.CommunityProgramAttendance = request.CommunityProgramAttendance;
            existing.PmaStatus = request.PmaStatus;
            existing.OverallProgress = request.OverallProgress;
            existing.UpdatedAtUtc = now;
        }
        else
        {
            existing = new CaseStage2Data
            {
                Id = Guid.NewGuid(),
                CaseId = caseId,
                OrganisationId = organisationId,
                BioPsychoSocialAssessment = request.BioPsychoSocialAssessment,
                IcpRecords = request.IcpRecords,
                LifeSkillTraining = request.LifeSkillTraining,
                ParentManagement = request.ParentManagement,
                GroupWork = request.GroupWork,
                CommunityProgramAttendance = request.CommunityProgramAttendance,
                PmaStatus = request.PmaStatus,
                OverallProgress = request.OverallProgress,
                CreatedByUserId = actorUserId,
                CreatedAtUtc = now,
                UpdatedAtUtc = now,
            };
            db.Add(existing);
        }

        var isNew = existing is null;
        var eventType = isNew ? AuditEventTypes.Stage2DataCreated : AuditEventTypes.Stage2DataUpdated;

        db.AuditEvents.Add(new AuditEvent
        {
            Id = Guid.NewGuid(),
            OrganisationId = organisationId,
            ActorUserId = actorUserId,
            SubjectUserId = null,
            EventType = eventType,
            MetadataJson = JsonSerializer.Serialize(
                new Dictionary<string, object?>
                {
                    ["caseId"] = caseId.ToString("D"),
                    ["actorUserId"] = actorUserId.ToString("D"),
                }, JsonOptions),
            CreatedAtUtc = now,
        });

        await db.SaveChangesAsync(ct);

        return ToDto(existing, caseId);
    }

    private static void ValidateFieldLengths(UpsertStage2DataRequest request)
    {
        if (request.BioPsychoSocialAssessment?.Length > MaxFieldLength)
            throw new CaseBusinessRuleException($"BioPsychoSocialAssessment exceeds maximum length of {MaxFieldLength}.");
        if (request.IcpRecords?.Length > MaxFieldLength)
            throw new CaseBusinessRuleException($"IcpRecords exceeds maximum length of {MaxFieldLength}.");
        if (request.LifeSkillTraining?.Length > MaxFieldLength)
            throw new CaseBusinessRuleException($"LifeSkillTraining exceeds maximum length of {MaxFieldLength}.");
        if (request.ParentManagement?.Length > MaxFieldLength)
            throw new CaseBusinessRuleException($"ParentManagement exceeds maximum length of {MaxFieldLength}.");
        if (request.GroupWork?.Length > MaxFieldLength)
            throw new CaseBusinessRuleException($"GroupWork exceeds maximum length of {MaxFieldLength}.");
        if (request.CommunityProgramAttendance?.Length > MaxFieldLength)
            throw new CaseBusinessRuleException($"CommunityProgramAttendance exceeds maximum length of {MaxFieldLength}.");
        if (request.PmaStatus?.Length > MaxFieldLength)
            throw new CaseBusinessRuleException($"PmaStatus exceeds maximum length of {MaxFieldLength}.");
        if (request.OverallProgress?.Length > MaxFieldLength)
            throw new CaseBusinessRuleException($"OverallProgress exceeds maximum length of {MaxFieldLength}.");
    }

    private static Stage2DataDto ToDto(CaseStage2Data? data, Guid caseId)
    {
        if (data is null)
        {
            return new Stage2DataDto { CaseId = caseId };
        }

        return new Stage2DataDto
        {
            Id = data.Id,
            CaseId = data.CaseId,
            BioPsychoSocialAssessment = data.BioPsychoSocialAssessment,
            IcpRecords = data.IcpRecords,
            LifeSkillTraining = data.LifeSkillTraining,
            ParentManagement = data.ParentManagement,
            GroupWork = data.GroupWork,
            CommunityProgramAttendance = data.CommunityProgramAttendance,
            PmaStatus = data.PmaStatus,
            OverallProgress = data.OverallProgress,
            CreatedByUserId = data.CreatedByUserId,
            CreatedAtUtc = data.CreatedAtUtc,
            UpdatedAtUtc = data.UpdatedAtUtc,
        };
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
}
