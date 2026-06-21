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

public sealed class CaseStage3DataService(
    AppDbContext db,
    IHttpContextAccessor httpContextAccessor)
{
    private const int MaxNotesLength = 2000;
    private const int MaxProviderNameLength = 200;

    public async Task<List<Stage3SupportDto>> GetAsync(Guid caseId, CancellationToken ct)
    {
        var (organisationId, _) = ResolveActorContext();

        var caseEntity = await db.Cases
            .Where(c => c.Id == caseId && c.OrganisationId == organisationId)
            .Select(c => new { c.CurrentStage })
            .FirstOrDefaultAsync(ct);

        if (caseEntity is null)
            throw new CaseNotFoundException();

        if (caseEntity.CurrentStage != CaseStage.InterSectoralApproach)
            throw new CaseNotFoundException();

        var supports = await db.Set<CaseStage3Support>()
            .Where(d => d.CaseId == caseId)
            .OrderBy(d => d.SupportType)
            .ToListAsync(ct);

        return supports.Select(ToDto).ToList();
    }

    public async Task<List<Stage3SupportDto>> UpsertAsync(
        Guid caseId, UpsertStage3SupportsRequest request, CancellationToken ct)
    {
        var (organisationId, actorUserId) = ResolveActorContext();

        var caseEntity = await db.Cases
            .Where(c => c.Id == caseId && c.OrganisationId == organisationId)
            .Select(c => new { c.CurrentStage })
            .FirstOrDefaultAsync(ct);

        if (caseEntity is null)
            throw new CaseNotFoundException();

        if (caseEntity.CurrentStage != CaseStage.InterSectoralApproach)
            throw new CaseNotFoundException();

        ValidateRequest(request);

        var existing = await db.Set<CaseStage3Support>()
            .Where(d => d.CaseId == caseId)
            .ToListAsync(ct);

        // Build lookup of existing records by SupportType to preserve creation metadata
        var existingByType = existing
            .GroupBy(e => e.SupportType.ToString())
            .ToDictionary(g => g.Key, g => new Queue<CaseStage3Support>(g));

        var now = DateTime.UtcNow;

        // Atomic replace: remove old records, insert new ones preserving creation metadata
        db.Set<CaseStage3Support>().RemoveRange(existing);

        var newRecords = request.Items.Select(item =>
        {
            CaseStage3Support? matchedOld = null;
            if (existingByType.TryGetValue(item.SupportType, out var queue) && queue.Count > 0)
                matchedOld = queue.Dequeue();

            return new CaseStage3Support
            {
                Id = Guid.NewGuid(),
                CaseId = caseId,
                OrganisationId = organisationId,
                SupportType = Enum.Parse<SupportType>(item.SupportType, ignoreCase: true),
                ProviderName = item.ProviderName,
                Notes = item.Notes,
                ProvidedStatus = item.ProvidedStatus,
                CreatedByUserId = matchedOld?.CreatedByUserId ?? actorUserId,
                CreatedAtUtc = matchedOld?.CreatedAtUtc ?? now,
                UpdatedAtUtc = now,
            };
        }).ToList();

        db.Set<CaseStage3Support>().AddRange(newRecords);

        var isNew = existing.Count == 0;
        db.AuditEvents.Add(new AuditEvent
        {
            Id = Guid.NewGuid(),
            OrganisationId = organisationId,
            ActorUserId = actorUserId,
            SubjectUserId = null,
            EventType = isNew ? AuditEventTypes.Stage3DataCreated : AuditEventTypes.Stage3DataUpdated,
            MetadataJson = JsonSerializer.Serialize(
                new Dictionary<string, object?>
                {
                    ["caseId"] = caseId.ToString("D"),
                    ["actorUserId"] = actorUserId.ToString("D"),
                    ["supportCount"] = newRecords.Count,
                }, new JsonSerializerOptions(JsonSerializerDefaults.Web)),
            CreatedAtUtc = now,
        });

        await db.SaveChangesAsync(ct);

        return newRecords.Select(ToDto).ToList();
    }

    private static void ValidateRequest(UpsertStage3SupportsRequest request)
    {
        if (request.Items is null || request.Items.Count == 0)
            throw new CaseBusinessRuleException("At least one support item is required.");

        for (int i = 0; i < request.Items.Count; i++)
        {
            var item = request.Items[i];
            if (string.IsNullOrWhiteSpace(item.SupportType))
                throw new CaseBusinessRuleException($"Items[{i}].SupportType is required.");

            if (!Enum.TryParse<SupportType>(item.SupportType, ignoreCase: true, out _))
                throw new CaseBusinessRuleException($"Items[{i}].SupportType '{item.SupportType}' is not a valid SupportType.");

            if (item.ProviderName?.Length > MaxProviderNameLength)
                throw new CaseBusinessRuleException($"Items[{i}].ProviderName exceeds maximum length of {MaxProviderNameLength}.");

            if (item.ProviderName is not null && string.IsNullOrWhiteSpace(item.ProviderName))
                throw new CaseBusinessRuleException($"Items[{i}].ProviderName cannot be whitespace only.");

            if (item.Notes?.Length > MaxNotesLength)
                throw new CaseBusinessRuleException($"Items[{i}].Notes exceeds maximum length of {MaxNotesLength}.");
        }
    }

    private static Stage3SupportDto ToDto(CaseStage3Support data)
    {
        return new Stage3SupportDto
        {
            Id = data.Id,
            CaseId = data.CaseId,
            SupportType = data.SupportType.ToString(),
            ProviderName = data.ProviderName,
            Notes = data.Notes,
            ProvidedStatus = data.ProvidedStatus,
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
