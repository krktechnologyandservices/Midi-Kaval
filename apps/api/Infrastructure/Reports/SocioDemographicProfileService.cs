using System.Security.Claims;
using Microsoft.AspNetCore.Http;
using Microsoft.EntityFrameworkCore;
using MidiKaval.Api.Domain.Entities;
using MidiKaval.Api.Domain.Enums;
using MidiKaval.Api.Infrastructure.Auth;
using MidiKaval.Api.Infrastructure.Persistence;
using MidiKaval.Api.Models.Reports;

namespace MidiKaval.Api.Infrastructure.Reports;

public sealed class SocioDemographicProfileService(
    AppDbContext db,
    IHttpContextAccessor httpContextAccessor)
{
    public async Task<SocioDemographicProfileDto> BuildReportAsync(
        int month, int year, CancellationToken ct = default)
    {
        var (organisationId, _) = ResolveActorContext();

        var cases = await db.Cases
            .Include(c => c.Occupation)
            .Include(c => c.EducationLevel)
            .Where(c => c.OrganisationId == organisationId
                && c.CreatedAtUtc.Year == year
                && c.CreatedAtUtc.Month == month)
            .ToListAsync(ct);

        var children = cases
            .Select((c, i) => new ChildListItemDto
            {
                SlNo = i + 1,
                Name = c.BeneficiaryName,
                Age = c.BeneficiaryAge,
                Contact = c.BeneficiaryContact,
                CaseCommittedDate = c.CreatedAtUtc,
                CrimeNumber = c.CrimeNumber,
                StNumber = c.StNumber,
                Status = CaseStageFriendlyName(c.CurrentStage),
                PresentStage = c.CurrentStage.ToString(),
            })
            .ToList();

        var crossTab = BuildCrossTabulation(cases);

        return new SocioDemographicProfileDto
        {
            Month = month,
            Year = year,
            Children = children,
            CrossTabulation = crossTab,
        };
    }

    private static IReadOnlyList<CrossTabulationSectionDto> BuildCrossTabulation(
        IReadOnlyList<Case> cases)
    {
        var sections = new List<CrossTabulationSectionDto>();

        // Gender
        sections.Add(new CrossTabulationSectionDto
        {
            DimensionName = "Gender",
            Categories = Enum.GetValues<Gender>()
                .Select(g => new CrossTabulationCategoryDto
                {
                    CategoryName = g.ToString(),
                    Count = cases.Count(c => c.Gender == g),
                })
                .Append(new CrossTabulationCategoryDto
                {
                    CategoryName = "Unknown",
                    Count = cases.Count(c => c.Gender == null),
                })
                .ToList(),
        });

        // Age Group
        sections.Add(new CrossTabulationSectionDto
        {
            DimensionName = "Age Group",
            Categories =
            [
                new() { CategoryName = "0-6 years", Count = cases.Count(c => c.BeneficiaryAge >= 0 && c.BeneficiaryAge <= 6) },
                new() { CategoryName = "7-11 years", Count = cases.Count(c => c.BeneficiaryAge >= 7 && c.BeneficiaryAge <= 11) },
                new() { CategoryName = "12-15 years", Count = cases.Count(c => c.BeneficiaryAge >= 12 && c.BeneficiaryAge <= 15) },
                new() { CategoryName = "16-18 years", Count = cases.Count(c => c.BeneficiaryAge >= 16 && c.BeneficiaryAge <= 18) },
                new() { CategoryName = "Unknown", Count = cases.Count(c => c.BeneficiaryAge == null) },
            ],
        });

        // Occupation (from legends)
        var nullOccupationCount = cases.Count(c => c.Occupation == null);
        var occupations = cases
            .Where(c => c.Occupation != null)
            .GroupBy(c => c.Occupation!.Name)
            .Select(g => new CrossTabulationCategoryDto
            {
                CategoryName = g.Key,
                Count = g.Count(),
            })
            .ToList();
        if (nullOccupationCount > 0)
        {
            occupations.Add(new CrossTabulationCategoryDto
            {
                CategoryName = "Not Specified",
                Count = nullOccupationCount,
            });
        }
        sections.Add(new CrossTabulationSectionDto
        {
            DimensionName = "Occupation",
            Categories = occupations,
        });

        // Domicile
        sections.Add(new CrossTabulationSectionDto
        {
            DimensionName = "Domicile",
            Categories = Enum.GetValues<Domicile>()
                .Select(d => new CrossTabulationCategoryDto
                {
                    CategoryName = d.ToString(),
                    Count = cases.Count(c => c.Domicile == d),
                })
                .ToList(),
        });

        // Education (from legends)
        var nullEducationCount = cases.Count(c => c.EducationLevel == null);
        var educations = cases
            .Where(c => c.EducationLevel != null)
            .GroupBy(c => c.EducationLevel!.Name)
            .Select(g => new CrossTabulationCategoryDto
            {
                CategoryName = g.Key,
                Count = g.Count(),
            })
            .ToList();
        if (nullEducationCount > 0)
        {
            educations.Add(new CrossTabulationCategoryDto
            {
                CategoryName = "Not Specified",
                Count = nullEducationCount,
            });
        }
        sections.Add(new CrossTabulationSectionDto
        {
            DimensionName = "Education",
            Categories = educations,
        });

        // Family Type
        sections.Add(new CrossTabulationSectionDto
        {
            DimensionName = "Family Type",
            Categories = Enum.GetValues<FamilyType>()
                .Select(ft => new CrossTabulationCategoryDto
                {
                    CategoryName = ft.ToString(),
                    Count = cases.Count(c => c.FamilyType == ft),
                })
                .Append(new CrossTabulationCategoryDto
                {
                    CategoryName = "Unknown",
                    Count = cases.Count(c => c.FamilyType == null),
                })
                .ToList(),
        });

        // Economic Status
        sections.Add(new CrossTabulationSectionDto
        {
            DimensionName = "Economic Status",
            Categories = Enum.GetValues<EconomicStatus>()
                .Select(es => new CrossTabulationCategoryDto
                {
                    CategoryName = es.ToString(),
                    Count = cases.Count(c => c.EconomicStatus == es),
                })
                .Append(new CrossTabulationCategoryDto
                {
                    CategoryName = "Unknown",
                    Count = cases.Count(c => c.EconomicStatus == null),
                })
                .ToList(),
        });

        // Frequency (First time vs Repeat)
        sections.Add(new CrossTabulationSectionDto
        {
            DimensionName = "Frequency",
            Categories =
            [
                new() { CategoryName = "First time", Count = cases.Count(c => c.IsFirstTimeOffender) },
                new() { CategoryName = "Repeat", Count = cases.Count(c => !c.IsFirstTimeOffender) },
            ],
        });

        // Family History of Crime
        sections.Add(new CrossTabulationSectionDto
        {
            DimensionName = "Family History",
            Categories =
            [
                new() { CategoryName = "Yes", Count = cases.Count(c => c.FamilyHistoryOfCrime) },
                new() { CategoryName = "No", Count = cases.Count(c => !c.FamilyHistoryOfCrime) },
            ],
        });

        // Recidivism
        var recBeforeValues = cases
            .Select(c => c.RecidivismBeforeCount)
            .Where(v => v.HasValue)
            .Select(v => v!.Value)
            .ToList();
        var recAfterValues = cases
            .Select(c => c.RecidivismAfterCount)
            .Where(v => v.HasValue)
            .Select(v => v!.Value)
            .ToList();
        sections.Add(new CrossTabulationSectionDto
        {
            DimensionName = "Recidivism",
            Categories =
            [
                new() { CategoryName = "Avg Recidivism Before", Count = recBeforeValues.Count > 0 ? (int)Math.Round(recBeforeValues.Average()) : 0 },
                new() { CategoryName = "Avg Recidivism After", Count = recAfterValues.Count > 0 ? (int)Math.Round(recAfterValues.Average()) : 0 },
            ],
        });

        // Nature of Offence
        var offences = cases
            .Where(c => !string.IsNullOrWhiteSpace(c.TypeOfOffence))
            .GroupBy(c => c.TypeOfOffence)
            .Select(g => new CrossTabulationCategoryDto
            {
                CategoryName = g.Key,
                Count = g.Count(),
            })
            .ToList();
        sections.Add(new CrossTabulationSectionDto
        {
            DimensionName = "Nature of Offence",
            Categories = offences,
        });

        // Offence Classification
        sections.Add(new CrossTabulationSectionDto
        {
            DimensionName = "Offence Classification",
            Categories = Enum.GetValues<OffenceClassification>()
                .Select(oc => new CrossTabulationCategoryDto
                {
                    CategoryName = oc.ToString(),
                    Count = cases.Count(c => c.OffenceClassification == oc),
                })
                .ToList(),
        });

        return sections;
    }

    internal static string CaseStageFriendlyName(CaseStage stage) => stage switch
    {
        CaseStage.ProcessInitiation => "Process Initiation",
        CaseStage.MaintainAndDevelopment => "Maintain & Development",
        CaseStage.InterSectoralApproach => "Inter-Sectoral Approach",
        CaseStage.Rehabilitation => "Rehabilitation",
        CaseStage.Reintegration => "Reintegration",
        CaseStage.TerminationExclusion => "Termination/Exclusion",
        _ => stage.ToString(),
    };

    private (Guid OrganisationId, Guid ActorUserId) ResolveActorContext()
    {
        var httpContext = httpContextAccessor.HttpContext
            ?? throw new InvalidOperationException("HTTP context is required.");

        var principal = httpContext.User;
        var organisationClaim = principal.FindFirst(AuthClaimTypes.OrganisationId)?.Value;
        var userIdClaim = principal.FindFirst(ClaimTypes.NameIdentifier)?.Value
            ?? principal.FindFirst(System.IdentityModel.Tokens.Jwt.JwtRegisteredClaimNames.Sub)?.Value;

        if (!Guid.TryParse(organisationClaim, out var organisationId))
            throw new UnauthorizedAccessException("Organisation claim missing or invalid.");

        if (!Guid.TryParse(userIdClaim, out var actorUserId))
            throw new UnauthorizedAccessException("User identifier claim missing or invalid.");

        return (organisationId, actorUserId);
    }
}
