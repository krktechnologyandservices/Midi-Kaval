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

public sealed class CaseStage4DataService(
    AppDbContext db,
    IHttpContextAccessor httpContextAccessor)
{
    private const int MaxInstitutionNameLength = 500;
    private const int MaxAddressLength = 2000;

    public async Task<Stage4PlacementDto> GetAsync(Guid caseId, CancellationToken ct)
    {
        var (organisationId, _) = ResolveActorContext();

        var caseEntity = await db.Cases
            .Where(c => c.Id == caseId && c.OrganisationId == organisationId)
            .Select(c => new { c.CurrentStage })
            .FirstOrDefaultAsync(ct);

        if (caseEntity is null)
            throw new CaseNotFoundException();

        if (caseEntity.CurrentStage != CaseStage.Rehabilitation)
            throw new CaseNotFoundException();

        var data = await db.Set<CaseStage4Placement>()
            .FirstOrDefaultAsync(d => d.CaseId == caseId, ct);

        return ToDto(data, caseId);
    }

    public async Task<Stage4PlacementDto> UpsertAsync(
        Guid caseId, UpsertStage4PlacementRequest request, CancellationToken ct)
    {
        var (organisationId, actorUserId) = ResolveActorContext();

        var caseEntity = await db.Cases
            .Where(c => c.Id == caseId && c.OrganisationId == organisationId)
            .Select(c => new { c.CurrentStage })
            .FirstOrDefaultAsync(ct);

        if (caseEntity is null)
            throw new CaseNotFoundException();

        if (caseEntity.CurrentStage != CaseStage.Rehabilitation)
            throw new CaseNotFoundException();

        var placementType = ValidateRequest(request);

        var now = DateTime.UtcNow;
        var existing = await db.Set<CaseStage4Placement>()
            .FirstOrDefaultAsync(d => d.CaseId == caseId, ct);

        Stage4PlacementDto result;

        if (existing is not null)
        {
            existing.PlacementType = placementType;
            existing.InstitutionName = request.InstitutionName;
            existing.Address = request.Address;
            existing.StartDate = request.StartDate;
            existing.UpdatedAtUtc = now;

            db.AuditEvents.Add(new AuditEvent
            {
                Id = Guid.NewGuid(),
                OrganisationId = organisationId,
                ActorUserId = actorUserId,
                SubjectUserId = null,
                EventType = AuditEventTypes.Stage4PlacementUpdated,
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
            var newRecord = new CaseStage4Placement
            {
                Id = Guid.NewGuid(),
                CaseId = caseId,
                OrganisationId = organisationId,
                PlacementType = placementType,
                InstitutionName = request.InstitutionName,
                Address = request.Address,
                StartDate = request.StartDate,
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
                EventType = AuditEventTypes.Stage4PlacementCreated,
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

    private static PlacementType ValidateRequest(UpsertStage4PlacementRequest request)
    {
        if (string.IsNullOrWhiteSpace(request.PlacementType))
            throw new CaseBusinessRuleException("PlacementType is required.");

        if (!Enum.TryParse<PlacementType>(request.PlacementType, ignoreCase: true, out var parsed))
            throw new CaseBusinessRuleException($"'{request.PlacementType}' is not a valid PlacementType. Must be one of: InHome, ObservationHome, SpecialHome.");

        if (!Enum.IsDefined(parsed))
            throw new CaseBusinessRuleException($"'{request.PlacementType}' is not a valid named PlacementType.");

        if (request.InstitutionName is not null && string.IsNullOrWhiteSpace(request.InstitutionName))
            throw new CaseBusinessRuleException("InstitutionName cannot be whitespace only.");

        if (request.InstitutionName?.Length > MaxInstitutionNameLength)
            throw new CaseBusinessRuleException($"InstitutionName exceeds maximum length of {MaxInstitutionNameLength}.");

        if (request.Address is not null && string.IsNullOrWhiteSpace(request.Address))
            throw new CaseBusinessRuleException("Address cannot be whitespace only.");

        if (request.Address?.Length > MaxAddressLength)
            throw new CaseBusinessRuleException($"Address exceeds maximum length of {MaxAddressLength}.");

        if (request.StartDate == default)
            throw new CaseBusinessRuleException("StartDate is required.");

        return parsed;
    }

    private static Stage4PlacementDto ToDto(CaseStage4Placement? data, Guid caseId)
    {
        if (data is null)
        {
            return new Stage4PlacementDto { CaseId = caseId };
        }

        return new Stage4PlacementDto
        {
            Id = data.Id,
            CaseId = data.CaseId,
            PlacementType = data.PlacementType.ToString(),
            InstitutionName = data.InstitutionName,
            Address = data.Address,
            StartDate = data.StartDate,
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
