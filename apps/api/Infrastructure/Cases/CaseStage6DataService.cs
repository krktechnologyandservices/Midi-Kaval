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

public sealed class CaseStage6DataService(
    AppDbContext db,
    IHttpContextAccessor httpContextAccessor)
{
    private const int MaxTextFieldLength = 2000;

    public async Task<Stage6TerminationExclusionDto> GetAsync(Guid caseId, CancellationToken ct)
    {
        var (organisationId, _) = ResolveActorContext();

        var caseEntity = await db.Cases
            .Where(c => c.Id == caseId && c.OrganisationId == organisationId)
            .Select(c => new { c.CurrentStage })
            .FirstOrDefaultAsync(ct);

        if (caseEntity is null)
            throw new CaseNotFoundException();

        if (caseEntity.CurrentStage != CaseStage.TerminationExclusion)
            throw new CaseNotFoundException();

        var data = await db.Set<CaseStage6TerminationExclusion>()
            .FirstOrDefaultAsync(d => d.CaseId == caseId, ct);

        return ToDto(data, caseId);
    }

    public async Task<Stage6TerminationExclusionDto> UpsertAsync(
        Guid caseId, UpsertStage6TerminationExclusionRequest request, CancellationToken ct)
    {
        var (organisationId, actorUserId) = ResolveActorContext();

        var caseEntity = await db.Cases
            .Where(c => c.Id == caseId && c.OrganisationId == organisationId)
            .Select(c => new { c.CurrentStage })
            .FirstOrDefaultAsync(ct);

        if (caseEntity is null)
            throw new CaseNotFoundException();

        if (caseEntity.CurrentStage != CaseStage.TerminationExclusion)
            throw new CaseNotFoundException();

        var terminationExclusionType = ValidateRequest(request);

        // Validate ReportAttachmentId if provided
        if (request.ReportAttachmentId.HasValue)
        {
            await ValidateReportAttachmentAsync(request.ReportAttachmentId.Value, organisationId, ct);
        }

        var now = DateTime.UtcNow;
        var existing = await db.Set<CaseStage6TerminationExclusion>()
            .FirstOrDefaultAsync(d => d.CaseId == caseId, ct);

        Stage6TerminationExclusionDto result;

        if (existing is not null)
        {
            existing.TerminationExclusionType = terminationExclusionType;
            existing.JjbDetails = request.JjbDetails;
            existing.ExclusionReason = request.ExclusionReason;
            existing.ReportAttachmentId = request.ReportAttachmentId;
            existing.UpdatedAtUtc = now;

            db.AuditEvents.Add(new AuditEvent
            {
                Id = Guid.NewGuid(),
                OrganisationId = organisationId,
                ActorUserId = actorUserId,
                SubjectUserId = null,
                EventType = AuditEventTypes.Stage6TerminationExclusionUpdated,
                MetadataJson = JsonSerializer.Serialize(
                    new Dictionary<string, object?>
                    {
                        ["caseId"] = caseId.ToString("D"),
                        ["actorUserId"] = actorUserId.ToString("D"),
                    }, new JsonSerializerOptions(JsonSerializerDefaults.Web)),
                CreatedAtUtc = now,
            });

            await db.SaveChangesAsync(ct);
            result = ToDto(existing, caseId);
        }
        else
        {
            var newRecord = new CaseStage6TerminationExclusion
            {
                Id = Guid.NewGuid(),
                CaseId = caseId,
                OrganisationId = organisationId,
                TerminationExclusionType = terminationExclusionType,
                JjbDetails = request.JjbDetails,
                ExclusionReason = request.ExclusionReason,
                ReportAttachmentId = request.ReportAttachmentId,
                CreatedByUserId = actorUserId,
                CreatedAtUtc = now,
                UpdatedAtUtc = now,
            };

            db.Add(newRecord);

            db.AuditEvents.Add(new AuditEvent
            {
                Id = Guid.NewGuid(),
                OrganisationId = organisationId,
                ActorUserId = actorUserId,
                SubjectUserId = null,
                EventType = AuditEventTypes.Stage6TerminationExclusionCreated,
                MetadataJson = JsonSerializer.Serialize(
                    new Dictionary<string, object?>
                    {
                        ["caseId"] = caseId.ToString("D"),
                        ["actorUserId"] = actorUserId.ToString("D"),
                    }, new JsonSerializerOptions(JsonSerializerDefaults.Web)),
                CreatedAtUtc = now,
            });

            await db.SaveChangesAsync(ct);
            result = ToDto(newRecord, caseId);
        }

        return result;
    }

    private static TerminationExclusionType ValidateRequest(UpsertStage6TerminationExclusionRequest request)
    {
        if (string.IsNullOrWhiteSpace(request.TerminationExclusionType))
            throw new CaseBusinessRuleException("TerminationExclusionType is required.");

        if (!Enum.TryParse<TerminationExclusionType>(request.TerminationExclusionType.Trim(), ignoreCase: true, out var parsed))
            throw new CaseBusinessRuleException($"'{request.TerminationExclusionType}' is not a valid TerminationExclusionType. Must be one of: Termination, Exclusion.");

        if (!Enum.IsDefined(parsed))
            throw new CaseBusinessRuleException($"'{request.TerminationExclusionType}' is not a valid named TerminationExclusionType.");

        // Cross-field validation: JjbDetails for Termination, ExclusionReason for Exclusion
        if (parsed == TerminationExclusionType.Termination)
        {
            if (string.IsNullOrWhiteSpace(request.JjbDetails))
                throw new CaseBusinessRuleException("JjbDetails is required when TerminationExclusionType is Termination.");

            if (!string.IsNullOrWhiteSpace(request.ExclusionReason))
                throw new CaseBusinessRuleException("ExclusionReason must not be set when TerminationExclusionType is Termination.");
        }
        else if (parsed == TerminationExclusionType.Exclusion)
        {
            if (string.IsNullOrWhiteSpace(request.ExclusionReason))
                throw new CaseBusinessRuleException("ExclusionReason is required when TerminationExclusionType is Exclusion.");

            if (!string.IsNullOrWhiteSpace(request.JjbDetails))
                throw new CaseBusinessRuleException("JjbDetails must not be set when TerminationExclusionType is Exclusion.");
        }

        if (request.JjbDetails?.Length > MaxTextFieldLength)
            throw new CaseBusinessRuleException($"JjbDetails exceeds maximum length of {MaxTextFieldLength}.");

        if (request.ExclusionReason?.Length > MaxTextFieldLength)
            throw new CaseBusinessRuleException($"ExclusionReason exceeds maximum length of {MaxTextFieldLength}.");

        return parsed;
    }

    private async Task ValidateReportAttachmentAsync(Guid attachmentId, Guid organisationId, CancellationToken ct)
    {
        var attachment = await db.Attachments
            .Where(a => a.Id == attachmentId && a.OrganisationId == organisationId)
            .Select(a => new { a.Status })
            .FirstOrDefaultAsync(ct);

        if (attachment is null)
            throw new CaseBusinessRuleException("Report attachment not found or not accessible.");

        if (attachment.Status != AttachmentStatus.Confirmed)
            throw new CaseBusinessRuleException("Report attachment must be in Confirmed status.");
    }

    private static Stage6TerminationExclusionDto ToDto(CaseStage6TerminationExclusion? data, Guid caseId)
    {
        if (data is null)
        {
            return new Stage6TerminationExclusionDto { CaseId = caseId };
        }

        return new Stage6TerminationExclusionDto
        {
            Id = data.Id,
            CaseId = data.CaseId,
            TerminationExclusionType = data.TerminationExclusionType.ToString(),
            JjbDetails = data.JjbDetails,
            ExclusionReason = data.ExclusionReason,
            ReportAttachmentId = data.ReportAttachmentId,
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
