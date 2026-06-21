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

public sealed class CaseRelatedCasesService(
    AppDbContext db,
    IHttpContextAccessor httpContextAccessor)
{
    public async Task<List<RelatedCaseDto>> GetAsync(Guid caseId, CancellationToken ct)
    {
        var (organisationId, _) = ResolveActorContext();

        var caseExists = await db.Cases.AnyAsync(c => c.Id == caseId && c.OrganisationId == organisationId, ct);
        if (!caseExists)
            throw new CaseNotFoundException();

        var links = await db.Set<CaseRelatedCase>()
            .Where(r => r.CaseIdA == caseId || r.CaseIdB == caseId)
            .Join(
                db.Cases.Where(c => c.OrganisationId == organisationId),
                r => r.CaseIdA == caseId ? r.CaseIdB : r.CaseIdA,
                c => c.Id,
                (r, c) => new { r.RelationshipType, c.Id, c.CrimeNumber, c.StNumber, c.BeneficiaryName, c.CurrentStage })
            .ToListAsync(ct);

        var dtos = links.Select(l => new RelatedCaseDto
        {
            CaseId = l.Id,
            CrimeNumber = l.CrimeNumber,
            StNumber = l.StNumber,
            BeneficiaryName = l.BeneficiaryName,
            CurrentStage = l.CurrentStage.ToString(),
            RelationshipType = l.RelationshipType.ToString(),
        }).ToList();

        return dtos;
    }

    public async Task<RelatedCaseDto> LinkAsync(
        Guid caseId, LinkRelatedCaseRequest request, CancellationToken ct)
    {
        var (organisationId, actorUserId) = ResolveActorContext();

        if (request.RelatedCaseId == caseId)
            throw new CaseBusinessRuleException("A case cannot be linked to itself.");

        var relationshipType = ValidateRelationshipType(request.RelationshipType);

        var caseIds = new[] { caseId, request.RelatedCaseId };
        var existingCases = await db.Cases
            .Where(c => caseIds.Contains(c.Id) && c.OrganisationId == organisationId)
            .Select(c => new { c.Id, c.CrimeNumber, c.StNumber, c.BeneficiaryName, c.CurrentStage })
            .ToListAsync(ct);

        if (existingCases.Count != 2)
            throw new CaseNotFoundException();

        var targetCase = existingCases.First(c => c.Id == request.RelatedCaseId);

        var (caseIdA, caseIdB) = OrderGuids(caseId, request.RelatedCaseId);

        var existingLink = await db.Set<CaseRelatedCase>()
            .AnyAsync(r => r.CaseIdA == caseIdA && r.CaseIdB == caseIdB, ct);

        if (existingLink)
            throw new CaseBusinessRuleException("Cases are already linked.");

        var now = DateTime.UtcNow;
        var link = new CaseRelatedCase
        {
            Id = Guid.NewGuid(),
            CaseIdA = caseIdA,
            CaseIdB = caseIdB,
            RelationshipType = relationshipType,
            CreatedByUserId = actorUserId,
            CreatedAtUtc = now,
        };

        db.Add(link);

        db.AuditEvents.Add(new AuditEvent
        {
            Id = Guid.NewGuid(),
            OrganisationId = organisationId,
            ActorUserId = actorUserId,
            SubjectUserId = null,
            EventType = AuditEventTypes.CaseLinked,
            MetadataJson = JsonSerializer.Serialize(
                new Dictionary<string, object?>
                {
                    ["caseId"] = caseId.ToString("D"),
                    ["relatedCaseId"] = request.RelatedCaseId.ToString("D"),
                    ["relationshipType"] = relationshipType.ToString(),
                }, new JsonSerializerOptions(JsonSerializerDefaults.Web)),
            CreatedAtUtc = now,
        });

        await db.SaveChangesAsync(ct);

        return new RelatedCaseDto
        {
            CaseId = targetCase.Id,
            CrimeNumber = targetCase.CrimeNumber,
            StNumber = targetCase.StNumber,
            BeneficiaryName = targetCase.BeneficiaryName,
            CurrentStage = targetCase.CurrentStage.ToString(),
            RelationshipType = relationshipType.ToString(),
        };
    }

    public async Task UnlinkAsync(Guid caseId, Guid relatedCaseId, CancellationToken ct)
    {
        var (organisationId, actorUserId) = ResolveActorContext();

        // Verify at least one case belongs to the caller's organisation
        var caseIds = new[] { caseId, relatedCaseId };
        var ownedCaseCount = await db.Cases.CountAsync(c => caseIds.Contains(c.Id) && c.OrganisationId == organisationId, ct);
        if (ownedCaseCount == 0)
            throw new CaseNotFoundException();

        var (caseIdA, caseIdB) = OrderGuids(caseId, relatedCaseId);

        var link = await db.Set<CaseRelatedCase>()
            .FirstOrDefaultAsync(r => r.CaseIdA == caseIdA && r.CaseIdB == caseIdB, ct);

        if (link is null)
            throw new CaseNotFoundException();

        db.Remove(link);

        db.AuditEvents.Add(new AuditEvent
        {
            Id = Guid.NewGuid(),
            OrganisationId = organisationId,
            ActorUserId = actorUserId,
            SubjectUserId = null,
            EventType = AuditEventTypes.CaseUnlinked,
            MetadataJson = JsonSerializer.Serialize(
                new Dictionary<string, object?>
                {
                    ["caseId"] = caseId.ToString("D"),
                    ["relatedCaseId"] = relatedCaseId.ToString("D"),
                }, new JsonSerializerOptions(JsonSerializerDefaults.Web)),
            CreatedAtUtc = DateTime.UtcNow,
        });

        await db.SaveChangesAsync(ct);
    }

    private static RelationshipType ValidateRelationshipType(string value)
    {
        if (string.IsNullOrWhiteSpace(value))
            throw new CaseBusinessRuleException("RelationshipType is required.");

        if (!Enum.TryParse<RelationshipType>(value.Trim(), ignoreCase: true, out var parsed))
            throw new CaseBusinessRuleException($"'{value}' is not a valid RelationshipType. Must be one of: Sibling, CoAccused, LinkedChild.");

        if (!Enum.IsDefined(parsed))
            throw new CaseBusinessRuleException($"'{value}' is not a valid named RelationshipType.");

        // Reject numeric strings (e.g. "0", "1") that parse but are not canonical names
        if (!Enum.GetName(parsed)!.Equals(value.Trim(), StringComparison.OrdinalIgnoreCase))
            throw new CaseBusinessRuleException($"'{value}' is not a valid named RelationshipType. Must be one of: Sibling, CoAccused, LinkedChild.");

        return parsed;
    }

    private static (Guid caseIdA, Guid caseIdB) OrderGuids(Guid a, Guid b)
    {
        if (a.CompareTo(b) < 0)
            return (a, b);
        return (b, a);
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
