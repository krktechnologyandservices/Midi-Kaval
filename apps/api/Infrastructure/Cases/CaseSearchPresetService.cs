using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using MidiKaval.Api.Domain.Entities;
using MidiKaval.Api.Domain.Enums;
using MidiKaval.Api.Infrastructure.Auth;
using MidiKaval.Api.Infrastructure.Persistence;
using MidiKaval.Api.Models.Cases;

namespace MidiKaval.Api.Infrastructure.Cases;

public sealed class CaseSearchPresetService(AppDbContext db, IHttpContextAccessor httpContextAccessor)
{
    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web);

    public async Task<IReadOnlyList<CaseSearchPresetDto>> ListAsync(CancellationToken cancellationToken = default)
    {
        var (organisationId, userId) = ResolveActorContext();

        return await db.CaseSearchPresets
            .Where(p => p.OrganisationId == organisationId && p.UserId == userId)
            .OrderBy(p => p.Name)
            .Select(p => new CaseSearchPresetDto
            {
                Id = p.Id,
                Name = p.Name,
                Filters = DeserializeFilters(p.FiltersJson),
                CreatedAtUtc = p.CreatedAtUtc,
            })
            .ToListAsync(cancellationToken);
    }

    public async Task<CaseSearchPresetDto> CreateAsync(
        CreateCaseSearchPresetRequest request,
        CancellationToken cancellationToken = default)
    {
        if (request is null)
        {
            throw new CaseValidationException("Request body is required.");
        }

        var name = ValidatePresetName(request.Name);
        var filters = request.Filters ?? new CaseSearchFiltersDto();
        ValidateFilters(filters);

        var (organisationId, userId) = ResolveActorContext();
        var duplicate = await db.CaseSearchPresets.AnyAsync(
            p => p.OrganisationId == organisationId && p.UserId == userId && p.Name == name,
            cancellationToken);

        if (duplicate)
        {
            throw new CaseConflictException("A preset with this name already exists.");
        }

        var now = DateTime.UtcNow;
        var entity = new CaseSearchPreset
        {
            Id = Guid.NewGuid(),
            OrganisationId = organisationId,
            UserId = userId,
            Name = name,
            FiltersJson = JsonSerializer.Serialize(filters, JsonOptions),
            CreatedAtUtc = now,
            UpdatedAtUtc = now,
        };

        db.CaseSearchPresets.Add(entity);
        await db.SaveChangesAsync(cancellationToken);

        return new CaseSearchPresetDto
        {
            Id = entity.Id,
            Name = entity.Name,
            Filters = filters,
            CreatedAtUtc = entity.CreatedAtUtc,
        };
    }

    public async Task DeleteAsync(Guid presetId, CancellationToken cancellationToken = default)
    {
        var (organisationId, userId) = ResolveActorContext();

        var entity = await db.CaseSearchPresets.SingleOrDefaultAsync(
            p => p.Id == presetId && p.OrganisationId == organisationId && p.UserId == userId,
            cancellationToken);

        if (entity is null)
        {
            throw new CaseNotFoundException();
        }

        db.CaseSearchPresets.Remove(entity);
        await db.SaveChangesAsync(cancellationToken);
    }

    private static string ValidatePresetName(string? name)
    {
        var trimmed = name?.Trim() ?? string.Empty;
        if (trimmed.Length == 0)
        {
            throw new CaseValidationException("name is required.");
        }

        if (trimmed.Length > 64)
        {
            throw new CaseValidationException("name must be at most 64 characters.");
        }

        return trimmed;
    }

    private static void ValidateFilters(CaseSearchFiltersDto filters)
    {
        if (!string.IsNullOrWhiteSpace(filters.CurrentStage)
            && !TryParseEnum(filters.CurrentStage, out CaseStage _))
        {
            throw new CaseValidationException(
                "filters.currentStage must be one of: ProcessInitiation, MaintainAndDevelopment, InterSectoralApproach, Rehabilitation, Reintegration, TerminationExclusion.");
        }

        if (!string.IsNullOrWhiteSpace(filters.OffenceClassification)
            && !TryParseEnum(filters.OffenceClassification, out OffenceClassification _))
        {
            throw new CaseValidationException(
                "filters.offenceClassification must be one of: Petty, Serious, Heinous.");
        }

        if (!string.IsNullOrWhiteSpace(filters.Domicile)
            && !TryParseEnum(filters.Domicile, out Domicile _))
        {
            throw new CaseValidationException(
                "filters.domicile must be one of: Urban, Rural, Coastal, Tribal, Slum.");
        }
    }

    private static CaseSearchFiltersDto DeserializeFilters(string json)
    {
        if (string.IsNullOrWhiteSpace(json))
        {
            return new CaseSearchFiltersDto();
        }

        return JsonSerializer.Deserialize<CaseSearchFiltersDto>(json, JsonOptions)
            ?? new CaseSearchFiltersDto();
    }

    private static bool TryParseEnum<TEnum>(string? value, out TEnum parsed)
        where TEnum : struct, Enum
    {
        parsed = default;
        if (string.IsNullOrWhiteSpace(value))
        {
            return false;
        }

        return Enum.TryParse(value.Trim(), ignoreCase: true, out parsed)
            && Enum.IsDefined(typeof(TEnum), parsed)
            && string.Equals(parsed.ToString(), value.Trim(), StringComparison.OrdinalIgnoreCase);
    }

    private (Guid OrganisationId, Guid UserId) ResolveActorContext()
    {
        var httpContext = httpContextAccessor.HttpContext
            ?? throw new InvalidOperationException("HTTP context is required.");

        var principal = httpContext.User;
        var organisationClaim = principal.FindFirst(AuthClaimTypes.OrganisationId)?.Value;
        var userIdClaim = principal.FindFirst(System.Security.Claims.ClaimTypes.NameIdentifier)?.Value
            ?? principal.FindFirst(System.IdentityModel.Tokens.Jwt.JwtRegisteredClaimNames.Sub)?.Value;

        if (!Guid.TryParse(organisationClaim, out var organisationId)
            || !Guid.TryParse(userIdClaim, out var userId))
        {
            throw new InvalidOperationException("Authenticated user claims are missing or invalid.");
        }

        return (organisationId, userId);
    }
}
