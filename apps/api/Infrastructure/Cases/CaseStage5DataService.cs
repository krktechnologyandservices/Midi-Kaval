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

public sealed class CaseStage5DataService(
    AppDbContext db,
    IHttpContextAccessor httpContextAccessor)
{
    private const int MaxInstitutionDetailsLength = 2000;

    public async Task<Stage5ReintegrationDto> GetAsync(Guid caseId, CancellationToken ct)
    {
        var (organisationId, _) = ResolveActorContext();

        var caseEntity = await db.Cases
            .Where(c => c.Id == caseId && c.OrganisationId == organisationId)
            .Select(c => new { c.CurrentStage })
            .FirstOrDefaultAsync(ct);

        if (caseEntity is null)
            throw new CaseNotFoundException();

        if (caseEntity.CurrentStage != CaseStage.Reintegration)
            throw new CaseNotFoundException();

        var data = await db.Set<CaseStage5Reintegration>()
            .FirstOrDefaultAsync(d => d.CaseId == caseId, ct);

        return ToDto(data, caseId);
    }

    public async Task<Stage5ReintegrationDto> UpsertAsync(
        Guid caseId, UpsertStage5ReintegrationRequest request, CancellationToken ct)
    {
        var (organisationId, actorUserId) = ResolveActorContext();

        var caseEntity = await db.Cases
            .Where(c => c.Id == caseId && c.OrganisationId == organisationId)
            .Select(c => new { c.CurrentStage })
            .FirstOrDefaultAsync(ct);

        if (caseEntity is null)
            throw new CaseNotFoundException();

        if (caseEntity.CurrentStage != CaseStage.Reintegration)
            throw new CaseNotFoundException();

        var reintegrationLevel = ValidateRequest(request);

        var now = DateTime.UtcNow;
        var existing = await db.Set<CaseStage5Reintegration>()
            .FirstOrDefaultAsync(d => d.CaseId == caseId, ct);

        Stage5ReintegrationDto result;

        if (existing is not null)
        {
            existing.ReintegrationLevel = reintegrationLevel;
            existing.InstitutionDetails = request.InstitutionDetails;
            existing.UpdatedAtUtc = now;

            db.AuditEvents.Add(new AuditEvent
            {
                Id = Guid.NewGuid(),
                OrganisationId = organisationId,
                ActorUserId = actorUserId,
                SubjectUserId = null,
                EventType = AuditEventTypes.Stage5ReintegrationUpdated,
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
            var newRecord = new CaseStage5Reintegration
            {
                Id = Guid.NewGuid(),
                CaseId = caseId,
                OrganisationId = organisationId,
                ReintegrationLevel = reintegrationLevel,
                InstitutionDetails = request.InstitutionDetails,
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
                EventType = AuditEventTypes.Stage5ReintegrationCreated,
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

    private static ReintegrationLevel ValidateRequest(UpsertStage5ReintegrationRequest request)
    {
        if (string.IsNullOrWhiteSpace(request.ReintegrationLevel))
            throw new CaseBusinessRuleException("ReintegrationLevel is required.");

        if (!Enum.TryParse<ReintegrationLevel>(request.ReintegrationLevel.Trim(), ignoreCase: true, out var parsed))
            throw new CaseBusinessRuleException($"'{request.ReintegrationLevel}' is not a valid ReintegrationLevel. Must be one of: Community, Institutional.");

        if (!Enum.IsDefined(parsed))
            throw new CaseBusinessRuleException($"'{request.ReintegrationLevel}' is not a valid named ReintegrationLevel.");

        if (request.InstitutionDetails is not null && string.IsNullOrWhiteSpace(request.InstitutionDetails))
            throw new CaseBusinessRuleException("InstitutionDetails cannot be whitespace only.");

        if (request.InstitutionDetails?.Length > MaxInstitutionDetailsLength)
            throw new CaseBusinessRuleException($"InstitutionDetails exceeds maximum length of {MaxInstitutionDetailsLength}.");

        return parsed;
    }

    private static Stage5ReintegrationDto ToDto(CaseStage5Reintegration? data, Guid caseId)
    {
        if (data is null)
        {
            return new Stage5ReintegrationDto { CaseId = caseId };
        }

        return new Stage5ReintegrationDto
        {
            Id = data.Id,
            CaseId = data.CaseId,
            ReintegrationLevel = data.ReintegrationLevel.ToString(),
            InstitutionDetails = data.InstitutionDetails,
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
