using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;
using MidiKaval.Api.Domain.Entities;
using MidiKaval.Api.Domain.Entities.Legends;
using MidiKaval.Api.Domain.Enums;
using MidiKaval.Api.Infrastructure.Audit;
using MidiKaval.Api.Infrastructure.Auth;
using MidiKaval.Api.Infrastructure.Email;
using MidiKaval.Api.Infrastructure.Persistence;
using MidiKaval.Api.Models.Cases;

namespace MidiKaval.Api.Infrastructure.Cases;

public sealed class CaseService(
    AppDbContext db,
    IHttpContextAccessor httpContextAccessor,
    IOptions<CaseExportOptions> exportOptions,
    AuthVerifiedStore authVerifiedStore,
    EmailDeliveryService emailDeliveryService)
{
    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web);
    private const string ILikeEscapeCharacter = "\\";

    public async Task<CaseDto> CreateAsync(CreateCaseRequest request, CancellationToken cancellationToken = default)
    {
        if (request is null)
        {
            throw new CaseValidationException("Request body is required.");
        }

        var (organisationId, actorUserId) = ResolveActorContext();
        var validated = await ValidateIntakeRequestAsync(request, organisationId, cancellationToken);
        var now = DateTime.UtcNow;
        var caseId = Guid.NewGuid();

        var entity = new Case
        {
            Id = caseId,
            OrganisationId = organisationId,
            CrimeNumber = validated.CrimeNumber,
            StNumber = validated.StNumber,
            BeneficiaryName = validated.BeneficiaryName,
            BeneficiaryAge = validated.BeneficiaryAge,
            BeneficiaryContact = validated.BeneficiaryContact,
            TypeOfOffence = validated.TypeOfOffence,
            OffenceClassification = validated.OffenceClassification,
            Domicile = validated.Domicile,
            Gender = validated.Gender,
            FamilyType = validated.FamilyType,
            EconomicStatus = validated.EconomicStatus,
            OccupationId = validated.OccupationId,
            EducationLevelId = validated.EducationLevelId,
            FamilyHistoryOfCrime = validated.FamilyHistoryOfCrime,
            RecidivismBeforeCount = validated.RecidivismBeforeCount,
            RecidivismAfterCount = validated.RecidivismAfterCount,
            IsFirstTimeOffender = validated.IsFirstTimeOffender,
            SensitivityLevel = validated.SensitivityLevel,
            CurrentStage = CaseStage.ProcessInitiation,
            VisitCount = 0,
            CreatedByUserId = actorUserId,
            CreatedAtUtc = now,
            UpdatedAtUtc = now,
        };

        db.Cases.Add(entity);
        db.AuditEvents.Add(new AuditEvent
        {
            Id = Guid.NewGuid(),
            OrganisationId = organisationId,
            ActorUserId = actorUserId,
            SubjectUserId = null,
            EventType = AuditEventTypes.CaseCreated,
            MetadataJson = JsonSerializer.Serialize(
                new Dictionary<string, object?>
                {
                    ["caseId"] = caseId.ToString("D"),
                    ["crimeNumber"] = validated.CrimeNumber,
                    ["stNumber"] = validated.StNumber,
                },
                JsonOptions),
            CreatedAtUtc = now,
        });

        await db.SaveChangesAsync(cancellationToken);

        // Load navigation properties for the response DTO
        if (entity.OccupationId is not null)
        {
            await db.Entry(entity).Reference(c => c.Occupation).LoadAsync(cancellationToken);
        }
        if (entity.EducationLevelId is not null)
        {
            await db.Entry(entity).Reference(c => c.EducationLevel).LoadAsync(cancellationToken);
        }

        return ToDto(entity);
    }

    public async Task<CheckCaseDuplicateResultDto> CheckDuplicateAsync(
        CheckCaseDuplicateRequest request,
        CancellationToken cancellationToken = default)
    {
        if (request is null)
        {
            throw new CaseValidationException("Request body is required.");
        }

        var (organisationId, _) = ResolveActorContext();
        var crimeNumber = TryNormalizeOptionalIdentifier(request.CrimeNumber, "crimeNumber");
        var stNumber = TryNormalizeOptionalIdentifier(request.StNumber, "stNumber");

        if (crimeNumber is null && stNumber is null)
        {
            throw new CaseValidationException("At least one of crimeNumber or stNumber is required.");
        }

        var matches = await db.Cases
            .Where(c => c.OrganisationId == organisationId)
            .Where(c =>
                (crimeNumber != null && c.CrimeNumber == crimeNumber)
                || (stNumber != null && c.StNumber == stNumber))
            .Select(c => new { c.Id, c.CrimeNumber, c.StNumber, c.BeneficiaryName, c.CurrentStage })
            .ToListAsync(cancellationToken);

        var result = matches
            .Select(c =>
            {
                var crimeMatch = crimeNumber is not null && c.CrimeNumber == crimeNumber;
                var stMatch = stNumber is not null && c.StNumber == stNumber;
                var matchedOn = crimeMatch && stMatch
                    ? "Both"
                    : crimeMatch
                        ? "CrimeNumber"
                        : "StNumber";

                return new CaseDuplicateMatchDto
                {
                    CaseId = c.Id,
                    CrimeNumber = c.CrimeNumber,
                    StNumber = c.StNumber,
                    BeneficiaryName = c.BeneficiaryName,
                    CurrentStage = c.CurrentStage.ToString(),
                    MatchedOn = matchedOn,
                };
            })
            .ToList();

        return new CheckCaseDuplicateResultDto
        {
            HasMatch = result.Count > 0,
            Matches = result,
        };
    }

    public async Task<CaseDto> TransitionStageAsync(
        Guid caseId,
        TransitionCaseStageRequest request,
        CancellationToken cancellationToken = default)
    {
        if (request is null)
        {
            throw new CaseValidationException("Request body is required.");
        }

        var (organisationId, actorUserId) = ResolveActorContext();
        var targetStage = ParseRequiredCaseStage(request.TargetStage);
        var notes = ValidateTransitionNotes(request.Notes);

        var entity = await db.Cases
            .Include(c => c.Occupation)
            .Include(c => c.EducationLevel)
            .SingleOrDefaultAsync(
            c => c.Id == caseId && c.OrganisationId == organisationId,
            cancellationToken);

        if (entity is null)
        {
            throw new CaseNotFoundException();
        }

        var fromStage = entity.CurrentStage;
        if (!CaseStageTransitionRules.IsValidForwardTransition(fromStage, targetStage))
        {
            throw new CaseBusinessRuleException(
                "Stage transition must advance exactly one stage forward in the lifecycle order.");
        }

        var now = DateTime.UtcNow;
        entity.CurrentStage = targetStage;
        entity.UpdatedAtUtc = now;

        db.CaseStageTransitions.Add(new CaseStageTransition
        {
            Id = Guid.NewGuid(),
            CaseId = entity.Id,
            OrganisationId = organisationId,
            FromStage = fromStage,
            ToStage = targetStage,
            Notes = notes,
            CreatedByUserId = actorUserId,
            CreatedAtUtc = now,
        });

        db.AuditEvents.Add(new AuditEvent
        {
            Id = Guid.NewGuid(),
            OrganisationId = organisationId,
            ActorUserId = actorUserId,
            SubjectUserId = null,
            EventType = AuditEventTypes.CaseStageChanged,
            MetadataJson = JsonSerializer.Serialize(
                new Dictionary<string, object?>
                {
                    ["caseId"] = entity.Id.ToString("D"),
                    ["fromStage"] = fromStage.ToString(),
                    ["toStage"] = targetStage.ToString(),
                },
                JsonOptions),
            CreatedAtUtc = now,
        });

        await db.SaveChangesAsync(cancellationToken);

        return ToDto(entity);
    }

    public async Task<CaseDto> MergeAsync(
        Guid targetCaseId,
        CreateCaseRequest request,
        CancellationToken cancellationToken = default)
    {
        if (request is null)
        {
            throw new CaseValidationException("Request body is required.");
        }

        var (organisationId, actorUserId) = ResolveActorContext();
        var validated = await ValidateIntakeRequestAsync(request, organisationId, cancellationToken);

        var entity = await db.Cases.SingleOrDefaultAsync(
            c => c.Id == targetCaseId && c.OrganisationId == organisationId,
            cancellationToken);

        if (entity is null)
        {
            throw new CaseNotFoundException();
        }

        var crimeMatchesTarget = validated.CrimeNumber == entity.CrimeNumber;
        var stMatchesTarget = validated.StNumber == entity.StNumber;

        if (!crimeMatchesTarget && !stMatchesTarget)
        {
            throw new CaseBusinessRuleException("Draft identifiers do not match the target case.");
        }

        if (!crimeMatchesTarget)
        {
            var crimeConflict = await db.Cases.AnyAsync(
                c => c.OrganisationId == organisationId
                    && c.CrimeNumber == validated.CrimeNumber
                    && c.Id != entity.Id,
                cancellationToken);

            if (crimeConflict)
            {
                throw new CaseConflictException(
                    "A case with this crime number already exists in your organisation.");
            }
        }

        if (!stMatchesTarget)
        {
            var stConflict = await db.Cases.AnyAsync(
                c => c.OrganisationId == organisationId
                    && c.StNumber == validated.StNumber
                    && c.Id != entity.Id,
                cancellationToken);

            if (stConflict)
            {
                throw new CaseConflictException(
                    "A case with this ST number already exists in your organisation.");
            }
        }

        var fieldsChanged = false;

        if (entity.BeneficiaryAge is null && validated.BeneficiaryAge is not null)
        {
            entity.BeneficiaryAge = validated.BeneficiaryAge;
            fieldsChanged = true;
        }

        if (string.IsNullOrWhiteSpace(entity.BeneficiaryContact)
            && !string.IsNullOrWhiteSpace(validated.BeneficiaryContact))
        {
            entity.BeneficiaryContact = validated.BeneficiaryContact;
            fieldsChanged = true;
        }

        if (entity.Gender is null && validated.Gender is not null)
        {
            entity.Gender = validated.Gender;
            fieldsChanged = true;
        }

        if (entity.FamilyType is null && validated.FamilyType is not null)
        {
            entity.FamilyType = validated.FamilyType;
            fieldsChanged = true;
        }

        if (entity.EconomicStatus is null && validated.EconomicStatus is not null)
        {
            entity.EconomicStatus = validated.EconomicStatus;
            fieldsChanged = true;
        }

        if (entity.OccupationId is null && validated.OccupationId is not null)
        {
            entity.OccupationId = validated.OccupationId;
            fieldsChanged = true;
        }

        if (entity.EducationLevelId is null && validated.EducationLevelId is not null)
        {
            entity.EducationLevelId = validated.EducationLevelId;
            fieldsChanged = true;
        }

        // FamilyHistoryOfCrime — fill-empty: only set if false → true
        if (!entity.FamilyHistoryOfCrime && validated.FamilyHistoryOfCrime)
        {
            entity.FamilyHistoryOfCrime = validated.FamilyHistoryOfCrime;
            fieldsChanged = true;
        }

        if (entity.RecidivismBeforeCount is null && validated.RecidivismBeforeCount is not null)
        {
            entity.RecidivismBeforeCount = validated.RecidivismBeforeCount;
            fieldsChanged = true;
        }

        if (entity.RecidivismAfterCount is null && validated.RecidivismAfterCount is not null)
        {
            entity.RecidivismAfterCount = validated.RecidivismAfterCount;
            fieldsChanged = true;
        }

        var now = DateTime.UtcNow;
        if (fieldsChanged)
        {
            entity.UpdatedAtUtc = now;
        }

        db.AuditEvents.Add(new AuditEvent
        {
            Id = Guid.NewGuid(),
            OrganisationId = organisationId,
            ActorUserId = actorUserId,
            SubjectUserId = null,
            EventType = AuditEventTypes.CaseMerged,
            MetadataJson = JsonSerializer.Serialize(
                new Dictionary<string, object?>
                {
                    ["targetCaseId"] = entity.Id.ToString("D"),
                    ["draftCrimeNumber"] = validated.CrimeNumber,
                    ["draftStNumber"] = validated.StNumber,
                    ["actorUserId"] = actorUserId.ToString("D"),
                    ["draftSnapshot"] = new Dictionary<string, object?>
                    {
                        ["beneficiaryName"] = validated.BeneficiaryName,
                        ["beneficiaryAge"] = validated.BeneficiaryAge,
                        ["beneficiaryContact"] = validated.BeneficiaryContact,
                        ["typeOfOffence"] = validated.TypeOfOffence,
                        ["offenceClassification"] = validated.OffenceClassification.ToString(),
                        ["domicile"] = validated.Domicile.ToString(),
                        ["gender"] = validated.Gender?.ToString(),
                        ["familyType"] = validated.FamilyType?.ToString(),
                        ["economicStatus"] = validated.EconomicStatus?.ToString(),
                        ["isFirstTimeOffender"] = validated.IsFirstTimeOffender,
                        ["occupationId"] = validated.OccupationId?.ToString("D"),
                        ["educationLevelId"] = validated.EducationLevelId?.ToString("D"),
                        ["familyHistoryOfCrime"] = validated.FamilyHistoryOfCrime,
                        ["recidivismBeforeCount"] = validated.RecidivismBeforeCount,
                        ["recidivismAfterCount"] = validated.RecidivismAfterCount,
                    },
                },
                JsonOptions),
            CreatedAtUtc = now,
        });

        await db.SaveChangesAsync(cancellationToken);

        // Load navigation properties for the response DTO
        if (entity.OccupationId is not null)
        {
            await db.Entry(entity).Reference(c => c.Occupation).LoadAsync(cancellationToken);
        }
        if (entity.EducationLevelId is not null)
        {
            await db.Entry(entity).Reference(c => c.EducationLevel).LoadAsync(cancellationToken);
        }

        return ToDto(entity);
    }

    public async Task<CaseDetailDto> TransferAsync(
        Guid caseId,
        TransferCaseRequest request,
        CancellationToken cancellationToken = default)
    {
        if (request is null)
        {
            throw new CaseValidationException("Request body is required.");
        }

        if (request.AssigneeUserId is null || request.AssigneeUserId == Guid.Empty)
        {
            throw new CaseValidationException("assigneeUserId is required.");
        }

        var priorActions = ValidateHandoffField(request.PriorActions, "priorActions");
        var openItems = ValidateHandoffField(request.OpenItems, "openItems");
        var nextVisitPurpose = ValidateHandoffField(request.NextVisitPurpose, "nextVisitPurpose");

        var (organisationId, actorUserId) = ResolveActorContext();

        var entity = await db.Cases
            .Include(c => c.Occupation)
            .Include(c => c.EducationLevel)
            .SingleOrDefaultAsync(
            c => c.Id == caseId && c.OrganisationId == organisationId,
            cancellationToken);

        if (entity is null)
        {
            throw new CaseNotFoundException();
        }

        var assignee = await db.Users.SingleOrDefaultAsync(
            u => u.Id == request.AssigneeUserId.Value && u.OrganisationId == organisationId,
            cancellationToken);

        if (assignee is null || !assignee.IsActive)
        {
            throw new CaseBusinessRuleException("Assignee must be an active user in your organisation.");
        }

        if (assignee.Role is not (UserRoles.SocialWorker or UserRoles.CaseWorker))
        {
            throw new CaseBusinessRuleException("Assignee must be a Social Worker or Case Worker.");
        }

        if (entity.AssignedWorkerId == assignee.Id)
        {
            throw new CaseBusinessRuleException("Case is already assigned to this worker.");
        }

        var now = DateTime.UtcNow;
        var fromWorkerId = entity.AssignedWorkerId;

        entity.AssignedWorkerId = assignee.Id;
        entity.AssignedAtUtc = now;
        entity.UpdatedAtUtc = now;

        db.CaseAssignments.Add(new CaseAssignment
        {
            Id = Guid.NewGuid(),
            CaseId = entity.Id,
            OrganisationId = organisationId,
            FromWorkerId = fromWorkerId,
            ToWorkerId = assignee.Id,
            PriorActions = priorActions,
            OpenItems = openItems,
            NextVisitPurpose = nextVisitPurpose,
            CreatedByUserId = actorUserId,
            CreatedAtUtc = now,
        });

        db.AuditEvents.Add(new AuditEvent
        {
            Id = Guid.NewGuid(),
            OrganisationId = organisationId,
            ActorUserId = actorUserId,
            SubjectUserId = assignee.Id,
            EventType = AuditEventTypes.CaseTransferred,
            MetadataJson = JsonSerializer.Serialize(
                new Dictionary<string, object?>
                {
                    ["caseId"] = entity.Id.ToString("D"),
                    ["fromWorkerId"] = fromWorkerId?.ToString("D"),
                    ["toWorkerId"] = assignee.Id.ToString("D"),
                },
                JsonOptions),
            CreatedAtUtc = now,
        });

        await db.SaveChangesAsync(cancellationToken);

        await emailDeliveryService.TrySendCaseTransferredAsync(
            entity,
            assignee,
            now,
            cancellationToken);

        return await BuildDetailDtoAsync(entity, actorUserId, ResolveActorRole(), cancellationToken);
    }

    public async Task<CaseDetailDto> GetDetailAsync(Guid caseId, CancellationToken cancellationToken = default)
    {
        var (organisationId, actorUserId) = ResolveActorContext();
        var actorRole = ResolveActorRole();

        var entity = await db.Cases
            .Include(c => c.Occupation)
            .Include(c => c.EducationLevel)
            .SingleOrDefaultAsync(
            c => c.Id == caseId && c.OrganisationId == organisationId,
            cancellationToken);

        if (entity is null)
        {
            throw new CaseNotFoundException();
        }

        EnsureCanReadCase(entity, actorUserId, actorRole);

        return await BuildDetailDtoAsync(entity, actorUserId, actorRole, cancellationToken);
    }

    public async Task<CaseGpsDto> VerifyGpsAsync(
        Guid caseId,
        VerifyCaseGpsRequest request,
        CancellationToken cancellationToken = default)
    {
        if (request is null)
        {
            throw new CaseValidationException("Request body is required.");
        }

        var landmark = request.Landmark?.Trim() ?? string.Empty;
        if (landmark.Length == 0)
        {
            throw new CaseValidationException("landmark is required.");
        }

        if (landmark.Length > 500)
        {
            throw new CaseValidationException("landmark must be at most 500 characters.");
        }

        if (request.Latitude is null)
        {
            throw new CaseValidationException("latitude is required.");
        }

        if (request.Longitude is null)
        {
            throw new CaseValidationException("longitude is required.");
        }

        if (request.Latitude < -90m || request.Latitude > 90m)
        {
            throw new CaseValidationException("latitude must be between -90 and 90.");
        }

        if (request.Longitude < -180m || request.Longitude > 180m)
        {
            throw new CaseValidationException("longitude must be between -180 and 180.");
        }

        var (organisationId, actorUserId) = ResolveActorContext();
        var actorRole = ResolveActorRole();

        if (IsSupervisorRole(actorRole))
        {
            throw new CaseForbiddenException();
        }

        var entity = await db.Cases.SingleOrDefaultAsync(
            c => c.Id == caseId && c.OrganisationId == organisationId,
            cancellationToken);

        if (entity is null)
        {
            throw new CaseNotFoundException();
        }

        EnsureCanReadCase(entity, actorUserId, actorRole);

        var now = DateTime.UtcNow;
        entity.Latitude = request.Latitude;
        entity.Longitude = request.Longitude;
        entity.Landmark = landmark;
        entity.GpsVerified = true;
        entity.GpsVerifiedAtUtc = now;
        entity.GpsVerifiedByUserId = actorUserId;
        entity.UpdatedAtUtc = now;

        db.AuditEvents.Add(new AuditEvent
        {
            Id = Guid.NewGuid(),
            OrganisationId = organisationId,
            ActorUserId = actorUserId,
            SubjectUserId = actorUserId,
            EventType = AuditEventTypes.CaseGpsVerified,
            MetadataJson = JsonSerializer.Serialize(
                new Dictionary<string, object?>
                {
                    ["caseId"] = caseId.ToString("D"),
                },
                JsonOptions),
            CreatedAtUtc = now,
        });

        await db.SaveChangesAsync(cancellationToken);

        return ToCaseGpsDto(entity);
    }

    public async Task<RevealCasePiiResponse> RevealPiiAsync(
        Guid caseId,
        CancellationToken cancellationToken = default)
    {
        var (organisationId, actorUserId) = ResolveActorContext();
        var actorRole = ResolveActorRole();

        if (IsSupervisorRole(actorRole))
        {
            throw new CaseForbiddenException();
        }

        if (!await authVerifiedStore.HasRecentVerificationAsync(actorUserId, cancellationToken))
        {
            throw new CaseStepUpRequiredException();
        }

        var entity = await db.Cases.SingleOrDefaultAsync(
            c => c.Id == caseId && c.OrganisationId == organisationId,
            cancellationToken);

        if (entity is null)
        {
            throw new CaseNotFoundException();
        }

        EnsureCanReadCase(entity, actorUserId, actorRole);

        if (entity.SensitivityLevel != SensitivityLevel.POCSO)
        {
            throw new CaseBusinessRuleException("PII reveal is only available for POCSO cases.");
        }

        var now = DateTime.UtcNow;
        db.AuditEvents.Add(new AuditEvent
        {
            Id = Guid.NewGuid(),
            OrganisationId = organisationId,
            ActorUserId = actorUserId,
            SubjectUserId = actorUserId,
            EventType = AuditEventTypes.CasePiiRevealed,
            MetadataJson = JsonSerializer.Serialize(
                new Dictionary<string, object?>
                {
                    ["caseId"] = caseId.ToString("D"),
                    ["userId"] = actorUserId.ToString("D"),
                },
                JsonOptions),
            CreatedAtUtc = now,
        });

        await db.SaveChangesAsync(cancellationToken);

        return new RevealCasePiiResponse
        {
            BeneficiaryName = entity.BeneficiaryName,
            BeneficiaryAge = entity.BeneficiaryAge,
            BeneficiaryContact = entity.BeneficiaryContact,
        };
    }

    public async Task<(CaseSearchResultDto Result, int TotalCount)> ListAssignedAsync(
        int page,
        int pageSize,
        CancellationToken cancellationToken = default)
    {
        var (organisationId, actorUserId) = ResolveActorContext();
        var actorRole = ResolveActorRole();

        if (IsSupervisorRole(actorRole))
        {
            throw new CaseForbiddenException();
        }

        page = page < 1 ? 1 : page;
        pageSize = pageSize switch
        {
            < 1 => 25,
            > 100 => 100,
            _ => pageSize,
        };

        var cases = db.Cases.Where(c =>
            c.OrganisationId == organisationId
            && c.AssignedWorkerId == actorUserId);

        var totalCount = await cases.CountAsync(cancellationToken);

        var entities = await cases
            .Include(c => c.Occupation)
            .Include(c => c.EducationLevel)
            .OrderByDescending(c => c.AssignedAtUtc)
            .Skip((page - 1) * pageSize)
            .Take(pageSize)
            .ToListAsync(cancellationToken);

        var items = entities
            .Select(c => CaseDtoMapper.ToCaseSummary(c, redactPocsoForFieldWorker: true))
            .ToList();

        return (new CaseSearchResultDto
        {
            Items = items,
            Page = page,
            PageSize = pageSize,
        }, totalCount);
    }

    public async Task<(CaseSearchResultDto Result, int TotalCount)> SearchAsync(
        CaseSearchQuery query,
        CancellationToken cancellationToken = default)
    {
        if (query is null)
        {
            throw new CaseValidationException("Search query is required.");
        }

        var (organisationId, _) = ResolveActorContext();
        var page = query.Page < 1 ? 1 : query.Page;
        var pageSize = query.PageSize switch
        {
            < 1 => 25,
            > 100 => 100,
            _ => query.PageSize,
        };

        var parsed = ValidateSearchFilters(query);
        var cases = ApplySearchFilters(
            db.Cases.Where(c => c.OrganisationId == organisationId),
            query,
            parsed);

        var totalCount = await cases.CountAsync(cancellationToken);

        var items = await cases
            .OrderByDescending(c => c.UpdatedAtUtc)
            .Skip((page - 1) * pageSize)
            .Take(pageSize)
            .Select(c => new CaseSummaryDto
            {
                Id = c.Id,
                CrimeNumber = c.CrimeNumber,
                StNumber = c.StNumber,
                BeneficiaryName = c.BeneficiaryName,
                CurrentStage = c.CurrentStage.ToString(),
                TypeOfOffence = c.TypeOfOffence,
                OffenceClassification = c.OffenceClassification.ToString(),
                Domicile = c.Domicile.ToString(),
                VisitCount = c.VisitCount,
                CreatedByUserId = c.CreatedByUserId,
                AssignedWorkerUserId = c.AssignedWorkerId,
                AssignedAtUtc = c.AssignedAtUtc,
                UpdatedAtUtc = c.UpdatedAtUtc,
                NextVisitDueAtUtc = c.NextVisitDueAtUtc,
                GpsVerified = c.GpsVerified,
                Latitude = c.Latitude,
                Longitude = c.Longitude,
                Landmark = c.Landmark,
                SensitivityLevel = c.SensitivityLevel.ToString(),
                Gender = c.Gender != null ? c.Gender.ToString()! : null,
                FamilyType = c.FamilyType != null ? c.FamilyType.ToString()! : null,
                EconomicStatus = c.EconomicStatus != null ? c.EconomicStatus.ToString()! : null,
                OccupationId = c.OccupationId,
                EducationLevelId = c.EducationLevelId,
                FamilyHistoryOfCrime = c.FamilyHistoryOfCrime,
                RecidivismBeforeCount = c.RecidivismBeforeCount,
                RecidivismAfterCount = c.RecidivismAfterCount,
            })
            .ToListAsync(cancellationToken);

        // Resolve Occupation and EducationLevel names
        var occupationIds = items.Where(i => i.OccupationId.HasValue).Select(i => i.OccupationId!.Value).Distinct().ToList();
        var educationLevelIds = items.Where(i => i.EducationLevelId.HasValue).Select(i => i.EducationLevelId!.Value).Distinct().ToList();

        var occupations = occupationIds.Count > 0
            ? await db.Set<Occupation>().Where(o => occupationIds.Contains(o.Id)).ToDictionaryAsync(o => o.Id, o => o.Name, cancellationToken)
            : new Dictionary<Guid, string>();

        var educationLevels = educationLevelIds.Count > 0
            ? await db.Set<EducationLevel>().Where(el => educationLevelIds.Contains(el.Id)).ToDictionaryAsync(el => el.Id, el => el.Name, cancellationToken)
            : new Dictionary<Guid, string>();

        foreach (var item in items)
        {
            item.OccupationName = item.OccupationId.HasValue && occupations.TryGetValue(item.OccupationId.Value, out var occName) ? occName : null;
            item.EducationLevelName = item.EducationLevelId.HasValue && educationLevels.TryGetValue(item.EducationLevelId.Value, out var elName) ? elName : null;
        }

        return (new CaseSearchResultDto
        {
            Items = items,
            Page = page,
            PageSize = pageSize,
        }, totalCount);
    }

    public async Task<(byte[] Content, string ContentType, string FileName)> ExportAsync(
        CaseSearchQuery query,
        string? format,
        CancellationToken cancellationToken = default)
    {
        if (query is null)
        {
            throw new CaseValidationException("Search query is required.");
        }

        var normalizedFormat = ValidateExportFormat(format);
        var (organisationId, _) = ResolveActorContext();
        var parsed = ValidateSearchFilters(query);
        var cases = ApplySearchFilters(
            db.Cases.Where(c => c.OrganisationId == organisationId),
            query,
            parsed);

        var totalCount = await cases.CountAsync(cancellationToken);
        var maxRows = exportOptions.Value.MaxRows;
        if (totalCount > maxRows)
        {
            throw new CaseBusinessRuleException(
                $"Export limited to {maxRows} cases; refine filters and try again.");
        }

        var rows = await cases
            .OrderByDescending(c => c.UpdatedAtUtc)
            .Select(c => new CaseExportRowDto
            {
                CrimeNumber = c.CrimeNumber,
                StNumber = c.StNumber,
                BeneficiaryName = c.BeneficiaryName,
                CurrentStage = c.CurrentStage.ToString(),
                TypeOfOffence = c.TypeOfOffence,
                OffenceClassification = c.OffenceClassification.ToString(),
                Domicile = c.Domicile.ToString(),
                VisitCount = c.VisitCount,
                NextVisitDueAtUtc = c.NextVisitDueAtUtc,
                UpdatedAtUtc = c.UpdatedAtUtc,
                Gender = c.Gender != null ? c.Gender.ToString()! : null,
                FamilyType = c.FamilyType != null ? c.FamilyType.ToString()! : null,
                EconomicStatus = c.EconomicStatus != null ? c.EconomicStatus.ToString()! : null,
                // EF Core translates navigation property access (c.Occupation.Name) in Select
                // projections via correlated subquery — no Include() needed here.
                Occupation = c.Occupation != null ? c.Occupation.Name : null,
                EducationLevel = c.EducationLevel != null ? c.EducationLevel.Name : null,
                FamilyHistoryOfCrime = c.FamilyHistoryOfCrime,
                RecidivismBeforeCount = c.RecidivismBeforeCount,
                RecidivismAfterCount = c.RecidivismAfterCount,
            })
            .ToListAsync(cancellationToken);

        var timestamp = DateTime.UtcNow.ToString("yyyyMMdd-HHmmss");
        var fileName = $"cases-export-{timestamp}Z.{normalizedFormat}";

        if (normalizedFormat == "xlsx")
        {
            return (
                CaseExcelExporter.Export(rows),
                "application/vnd.openxmlformats-officedocument.spreadsheetml.sheet",
                fileName);
        }

        return (CasePdfExporter.Export(rows), "application/pdf", fileName);
    }

    private static string ValidateExportFormat(string? format)
    {
        if (string.IsNullOrWhiteSpace(format))
        {
            throw new CaseValidationException("format is required and must be xlsx or pdf.");
        }

        var normalized = format.Trim().ToLowerInvariant();
        if (normalized is not ("xlsx" or "pdf"))
        {
            throw new CaseValidationException("format must be xlsx or pdf.");
        }

        return normalized;
    }

    private sealed record ParsedSearchFilters(
        CaseStage? CurrentStage,
        OffenceClassification? OffenceClassification,
        Domicile? Domicile,
        Gender? Gender,
        FamilyType? FamilyType,
        EconomicStatus? EconomicStatus,
        Guid? OccupationId,
        Guid? EducationLevelId);

    private static ParsedSearchFilters ValidateSearchFilters(CaseSearchQuery query)
    {
        CaseStage? currentStage = null;
        if (!string.IsNullOrWhiteSpace(query.CurrentStage))
        {
            if (!TryParseEnum(query.CurrentStage, out CaseStage parsedStage))
            {
                throw new CaseValidationException(
                    "currentStage must be one of: ProcessInitiation, MaintainAndDevelopment, InterSectoralApproach, Rehabilitation, Reintegration, TerminationExclusion.");
            }

            currentStage = parsedStage;
        }

        OffenceClassification? offenceClassification = null;
        if (!string.IsNullOrWhiteSpace(query.OffenceClassification))
        {
            if (!TryParseEnum(query.OffenceClassification, out OffenceClassification parsedClassification))
            {
                throw new CaseValidationException(
                    "offenceClassification must be one of: Petty, Serious, Heinous.");
            }

            offenceClassification = parsedClassification;
        }

        Domicile? domicile = null;
        if (!string.IsNullOrWhiteSpace(query.Domicile))
        {
            if (!TryParseEnum(query.Domicile, out Domicile parsedDomicile))
            {
                throw new CaseValidationException(
                    "domicile must be one of: Urban, Rural, Coastal, Tribal, Slum.");
            }

            domicile = parsedDomicile;
        }

        Gender? gender = null;
        if (!string.IsNullOrWhiteSpace(query.Gender))
        {
            if (!TryParseEnum(query.Gender, out Gender parsedGender))
            {
                throw new CaseValidationException(
                    "gender must be one of: Male, Female, Transgender.");
            }
            gender = parsedGender;
        }

        FamilyType? familyType = null;
        if (!string.IsNullOrWhiteSpace(query.FamilyType))
        {
            if (!TryParseEnum(query.FamilyType, out FamilyType parsedFamilyType))
            {
                throw new CaseValidationException(
                    "familyType must be one of: Joint, Nuclear, SingleParent, Others.");
            }
            familyType = parsedFamilyType;
        }

        EconomicStatus? economicStatus = null;
        if (!string.IsNullOrWhiteSpace(query.EconomicStatus))
        {
            if (!TryParseEnum(query.EconomicStatus, out EconomicStatus parsedEs))
            {
                throw new CaseValidationException(
                    "economicStatus must be one of: APL, BPL.");
            }
            economicStatus = parsedEs;
        }

        Guid? occupationId = null;
        if (query.OccupationId is not null)
        {
            if (query.OccupationId == Guid.Empty)
            {
                throw new CaseValidationException("occupationId must be a non-empty UUID.");
            }
            occupationId = query.OccupationId;
        }

        Guid? educationLevelId = null;
        if (query.EducationLevelId is not null)
        {
            if (query.EducationLevelId == Guid.Empty)
            {
                throw new CaseValidationException("educationLevelId must be a non-empty UUID.");
            }
            educationLevelId = query.EducationLevelId;
        }

        return new ParsedSearchFilters(currentStage, offenceClassification, domicile, gender, familyType, economicStatus, occupationId, educationLevelId);
    }

    private static IQueryable<Case> ApplySearchFilters(
        IQueryable<Case> cases,
        CaseSearchQuery query,
        ParsedSearchFilters parsed)
    {
        if (!string.IsNullOrWhiteSpace(query.Q))
        {
            var q = query.Q.Trim();
            var normalizedQ = q.ToUpperInvariant();
            var likePattern = ToILikeContainsPattern(q);
            var domicilesMatchingQ = Enum.GetValues<Domicile>()
                .Where(d => d.ToString().Contains(q, StringComparison.OrdinalIgnoreCase))
                .ToArray();

            cases = cases.Where(c =>
                c.CrimeNumber.Contains(normalizedQ)
                || c.StNumber.Contains(normalizedQ)
                || EF.Functions.ILike(c.BeneficiaryName, likePattern, ILikeEscapeCharacter)
                || (c.BeneficiaryContact != null && EF.Functions.ILike(c.BeneficiaryContact, likePattern, ILikeEscapeCharacter))
                || domicilesMatchingQ.Contains(c.Domicile));
        }

        if (parsed.CurrentStage is not null)
        {
            cases = cases.Where(c => c.CurrentStage == parsed.CurrentStage);
        }

        if (!string.IsNullOrWhiteSpace(query.TypeOfOffence))
        {
            var offencePattern = ToILikeContainsPattern(query.TypeOfOffence.Trim());
            cases = cases.Where(c => EF.Functions.ILike(c.TypeOfOffence, offencePattern, ILikeEscapeCharacter));
        }

        if (parsed.OffenceClassification is not null)
        {
            cases = cases.Where(c => c.OffenceClassification == parsed.OffenceClassification);
        }

        if (parsed.Domicile is not null)
        {
            cases = cases.Where(c => c.Domicile == parsed.Domicile);
        }

        if (parsed.Gender is not null)
        {
            cases = cases.Where(c => c.Gender == parsed.Gender);
        }

        if (parsed.FamilyType is not null)
        {
            cases = cases.Where(c => c.FamilyType == parsed.FamilyType);
        }

        if (parsed.EconomicStatus is not null)
        {
            cases = cases.Where(c => c.EconomicStatus == parsed.EconomicStatus);
        }

        if (parsed.OccupationId is not null)
        {
            cases = cases.Where(c => c.OccupationId == parsed.OccupationId);
        }

        if (parsed.EducationLevelId is not null)
        {
            cases = cases.Where(c => c.EducationLevelId == parsed.EducationLevelId);
        }

        if (query.AssignedWorkerUserId is not null)
        {
            cases = cases.Where(c => c.AssignedWorkerId == query.AssignedWorkerUserId);
        }
        else if (query.CreatedByUserId is not null)
        {
            cases = cases.Where(c => c.CreatedByUserId == query.CreatedByUserId);
        }

        if (query.Overdue == true)
        {
            var now = DateTime.UtcNow;
            cases = cases.Where(c =>
                c.NextVisitDueAtUtc != null
                && c.NextVisitDueAtUtc < now
                && c.CurrentStage != CaseStage.TerminationExclusion);
        }

        return cases;
    }

    private static CaseStage ParseRequiredCaseStage(string? value)
    {
        if (!TryParseEnum(value, out CaseStage parsed))
        {
            throw new CaseValidationException(
                "targetStage must be one of: ProcessInitiation, MaintainAndDevelopment, InterSectoralApproach, Rehabilitation, Reintegration, TerminationExclusion.");
        }

        return parsed;
    }

    private static string? ValidateTransitionNotes(string? notes)
    {
        if (string.IsNullOrWhiteSpace(notes))
        {
            return null;
        }

        var trimmed = notes.Trim();
        if (trimmed.Length > 2000)
        {
            throw new CaseValidationException("notes must be at most 2000 characters.");
        }

        return trimmed;
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

    private async Task<ValidatedCreateCaseRequest> ValidateIntakeRequestAsync(
        CreateCaseRequest request, Guid organisationId, CancellationToken cancellationToken)
    {
        var crimeNumber = NormalizeRequiredIdentifier(request.CrimeNumber, "crimeNumber");
        var stNumber = NormalizeRequiredIdentifier(request.StNumber, "stNumber");

        var beneficiaryName = request.BeneficiaryName?.Trim() ?? string.Empty;
        if (beneficiaryName.Length == 0)
        {
            throw new CaseValidationException("beneficiaryName is required.");
        }

        if (beneficiaryName.Length > 256)
        {
            throw new CaseValidationException("beneficiaryName must be at most 256 characters.");
        }

        if (request.BeneficiaryAge is < 0 or > 120)
        {
            throw new CaseValidationException("beneficiaryAge must be between 0 and 120.");
        }

        var beneficiaryContact = request.BeneficiaryContact?.Trim();
        if (beneficiaryContact?.Length > 32)
        {
            throw new CaseValidationException("beneficiaryContact must be at most 32 characters.");
        }

        var typeOfOffence = request.TypeOfOffence?.Trim() ?? string.Empty;
        if (typeOfOffence.Length == 0)
        {
            throw new CaseValidationException("typeOfOffence is required.");
        }

        if (typeOfOffence.Length > 128)
        {
            throw new CaseValidationException("typeOfOffence must be at most 128 characters.");
        }

        if (!TryParseEnum(request.OffenceClassification, out OffenceClassification offenceClassification))
        {
            throw new CaseValidationException(
                "offenceClassification must be one of: Petty, Serious, Heinous.");
        }

        if (!TryParseEnum(request.Domicile, out Domicile domicile))
        {
            throw new CaseValidationException(
                "domicile must be one of: Urban, Rural, Coastal, Tribal, Slum.");
        }

        Gender? gender = null;
        if (!string.IsNullOrWhiteSpace(request.Gender))
        {
            if (!TryParseEnum(request.Gender, out Gender parsedGender))
            {
                throw new CaseValidationException(
                    "gender must be one of: Male, Female, Transgender.");
            }
            gender = parsedGender;
        }

        FamilyType? familyType = null;
        if (!string.IsNullOrWhiteSpace(request.FamilyType))
        {
            if (!TryParseEnum(request.FamilyType, out FamilyType parsedFt))
            {
                throw new CaseValidationException(
                    "familyType must be one of: Joint, Nuclear, SingleParent, Others.");
            }
            familyType = parsedFt;
        }

        EconomicStatus? economicStatus = null;
        if (!string.IsNullOrWhiteSpace(request.EconomicStatus))
        {
            if (!TryParseEnum(request.EconomicStatus, out EconomicStatus parsedEs))
            {
                throw new CaseValidationException(
                    "economicStatus must be one of: APL, BPL.");
            }
            economicStatus = parsedEs;
        }

        // OccupationId validation — verify Legend entity exists and belongs to same organisation
        Guid? occupationId = request.OccupationId;
        if (occupationId is not null)
        {
            var occupation = await db.Set<Occupation>()
                .FirstOrDefaultAsync(o => o.Id == occupationId && o.OrganisationId == organisationId && o.IsActive, cancellationToken);
            if (occupation is null)
            {
                throw new CaseBusinessRuleException(
                    $"referenced occupationId not found for id: {occupationId}");
            }
        }

        // EducationLevelId validation — verify Legend entity exists and belongs to same organisation
        Guid? educationLevelId = request.EducationLevelId;
        if (educationLevelId is not null)
        {
            var educationLevel = await db.Set<EducationLevel>()
                .FirstOrDefaultAsync(el => el.Id == educationLevelId && el.OrganisationId == organisationId && el.IsActive, cancellationToken);
            if (educationLevel is null)
            {
                throw new CaseBusinessRuleException(
                    $"referenced educationLevelId not found for id: {educationLevelId}");
            }
        }

        // FamilyHistoryOfCrime — default false
        var familyHistoryOfCrime = request.FamilyHistoryOfCrime ?? false;

        // RecidivismBeforeCount — validate non-negative
        int? recidivismBeforeCount = request.RecidivismBeforeCount;
        if (recidivismBeforeCount < 0)
        {
            throw new CaseBusinessRuleException(
                $"recidivismBeforeCount must be a non-negative integer.");
        }

        // RecidivismAfterCount — validate non-negative
        int? recidivismAfterCount = request.RecidivismAfterCount;
        if (recidivismAfterCount < 0)
        {
            throw new CaseBusinessRuleException(
                $"recidivismAfterCount must be a non-negative integer.");
        }

        var sensitivityLevel = SensitivityLevel.Standard;
        if (!string.IsNullOrWhiteSpace(request.SensitivityLevel))
        {
            if (!TryParseEnum(request.SensitivityLevel, out SensitivityLevel parsedSensitivity))
            {
                throw new CaseValidationException(
                    "sensitivityLevel must be one of: Standard, POCSO.");
            }

            sensitivityLevel = parsedSensitivity;
        }

        return new ValidatedCreateCaseRequest(
            crimeNumber,
            stNumber,
            beneficiaryName,
            request.BeneficiaryAge,
            string.IsNullOrWhiteSpace(beneficiaryContact) ? null : beneficiaryContact,
            typeOfOffence,
            offenceClassification,
            domicile,
            request.IsFirstTimeOffender ?? true,
            sensitivityLevel,
            gender,
            familyType,
            economicStatus,
            occupationId,
            educationLevelId,
            familyHistoryOfCrime,
            recidivismBeforeCount,
            recidivismAfterCount);
    }

    private static string NormalizeRequiredIdentifier(string? value, string fieldName)
    {
        var normalized = TryNormalizeOptionalIdentifier(value, fieldName);
        if (normalized is null)
        {
            throw new CaseValidationException($"{fieldName} is required.");
        }

        return normalized;
    }

    private static string ToILikeContainsPattern(string value)
    {
        var escaped = value
            .Replace("\\", "\\\\", StringComparison.Ordinal)
            .Replace("%", "\\%", StringComparison.Ordinal)
            .Replace("_", "\\_", StringComparison.Ordinal);
        return $"%{escaped}%";
    }

    private static string? TryNormalizeOptionalIdentifier(string? value, string fieldName)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            return null;
        }

        var normalized = value.Trim().ToUpperInvariant();
        if (normalized.Length > 64)
        {
            throw new CaseValidationException($"{fieldName} must be at most 64 characters.");
        }

        return normalized;
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

    private static CaseDto ToDto(Case entity) => new()
    {
        Id = entity.Id,
        CrimeNumber = entity.CrimeNumber,
        StNumber = entity.StNumber,
        BeneficiaryName = entity.BeneficiaryName,
        CurrentStage = entity.CurrentStage.ToString(),
        VisitCount = entity.VisitCount,
        CreatedAtUtc = entity.CreatedAtUtc,
        Gender = entity.Gender?.ToString(),
        FamilyType = entity.FamilyType?.ToString(),
        EconomicStatus = entity.EconomicStatus?.ToString(),
        OccupationId = entity.OccupationId,
        OccupationName = entity.Occupation?.Name,
        EducationLevelId = entity.EducationLevelId,
        EducationLevelName = entity.EducationLevel?.Name,
        FamilyHistoryOfCrime = entity.FamilyHistoryOfCrime,
        RecidivismBeforeCount = entity.RecidivismBeforeCount,
        RecidivismAfterCount = entity.RecidivismAfterCount,
    };

    private async Task<CaseDetailDto> BuildDetailDtoAsync(
        Case entity,
        Guid actorUserId,
        string actorRole,
        CancellationToken cancellationToken)
    {
        HandoffWhisperDto? whisper = null;

        if (entity.AssignedWorkerId == actorUserId
            && entity.AssignedAtUtc is not null
            && IsWithinHandoffWindow(entity.AssignedAtUtc.Value))
        {
            var assignment = await db.CaseAssignments
                .Where(a =>
                    a.CaseId == entity.Id
                    && a.OrganisationId == entity.OrganisationId
                    && a.ToWorkerId == entity.AssignedWorkerId)
                .OrderByDescending(a => a.CreatedAtUtc)
                .FirstOrDefaultAsync(cancellationToken);

            if (assignment is not null)
            {
                whisper = new HandoffWhisperDto
                {
                    PriorActions = assignment.PriorActions,
                    OpenItems = assignment.OpenItems,
                    NextVisitPurpose = assignment.NextVisitPurpose,
                    TransferredAtUtc = assignment.CreatedAtUtc,
                };
            }
        }

        // Fetch related cases
        var relatedCaseDtos = await BuildRelatedCaseDtosAsync(entity.Id, entity.OrganisationId, cancellationToken);

        return new CaseDetailDto
        {
            Id = entity.Id,
            CrimeNumber = entity.CrimeNumber,
            StNumber = entity.StNumber,
            BeneficiaryName = BeneficiaryDisplayFormatter.FormatBeneficiaryName(
                entity,
                redactPocsoForFieldWorker: !IsSupervisorRole(actorRole)),
            Domicile = entity.Domicile.ToString(),
            CurrentStage = entity.CurrentStage.ToString(),
            VisitCount = entity.VisitCount,
            AssignedWorkerUserId = entity.AssignedWorkerId,
            AssignedAtUtc = entity.AssignedAtUtc,
            NextVisitDueAtUtc = entity.NextVisitDueAtUtc,
            CreatedAtUtc = entity.CreatedAtUtc,
            UpdatedAtUtc = entity.UpdatedAtUtc,
            GpsVerified = entity.GpsVerified,
            Latitude = entity.Latitude,
            Longitude = entity.Longitude,
            Landmark = entity.Landmark,
            GpsVerifiedAtUtc = entity.GpsVerifiedAtUtc,
            GpsVerifiedByUserId = entity.GpsVerifiedByUserId,
            HandoffWhisper = whisper,
            SensitivityLevel = entity.SensitivityLevel.ToString(),
            Gender = entity.Gender?.ToString(),
            FamilyType = entity.FamilyType?.ToString(),
            EconomicStatus = entity.EconomicStatus?.ToString(),
            OccupationId = entity.OccupationId,
            OccupationName = entity.Occupation?.Name,
            EducationLevelId = entity.EducationLevelId,
            EducationLevelName = entity.EducationLevel?.Name,
            FamilyHistoryOfCrime = entity.FamilyHistoryOfCrime,
            RecidivismBeforeCount = entity.RecidivismBeforeCount,
            RecidivismAfterCount = entity.RecidivismAfterCount,
            RelatedCases = relatedCaseDtos,
        };
    }

    private async Task<List<RelatedCaseDto>> BuildRelatedCaseDtosAsync(
        Guid caseId, Guid organisationId, CancellationToken ct)
    {
        var raw = await db.Set<CaseRelatedCase>()
            .Where(r => r.CaseIdA == caseId || r.CaseIdB == caseId)
            .Join(
                db.Cases.Where(c => c.OrganisationId == organisationId),
                r => r.CaseIdA == caseId ? r.CaseIdB : r.CaseIdA,
                c => c.Id,
                (r, c) => new { r.RelationshipType, c.Id, c.CrimeNumber, c.StNumber, c.BeneficiaryName, c.CurrentStage })
            .ToListAsync(ct);

        return raw.Select(x => new RelatedCaseDto
        {
            CaseId = x.Id,
            CrimeNumber = x.CrimeNumber,
            StNumber = x.StNumber,
            BeneficiaryName = x.BeneficiaryName,
            CurrentStage = x.CurrentStage.ToString(),
            RelationshipType = x.RelationshipType.ToString(),
        }).ToList();
    }

    private static CaseGpsDto ToCaseGpsDto(Case entity) => new()
    {
        CaseId = entity.Id,
        GpsVerified = entity.GpsVerified,
        Latitude = entity.Latitude,
        Longitude = entity.Longitude,
        Landmark = entity.Landmark,
        GpsVerifiedAtUtc = entity.GpsVerifiedAtUtc,
        GpsVerifiedByUserId = entity.GpsVerifiedByUserId,
    };

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

    private static bool IsWithinHandoffWindow(DateTime assignedAtUtc) =>
        (DateTime.UtcNow.Date - assignedAtUtc.Date).Days < 7;

    private static bool IsSupervisorRole(string role) =>
        role is UserRoles.Director or UserRoles.Coordinator;

    private string ResolveActorRole() =>
        httpContextAccessor.HttpContext?.User.FindFirstValue(ClaimTypes.Role) ?? string.Empty;

    private static string ValidateHandoffField(string? value, string fieldName)
    {
        var trimmed = value?.Trim() ?? string.Empty;
        if (trimmed.Length == 0)
        {
            throw new CaseValidationException($"{fieldName} is required.");
        }

        if (trimmed.Length > 500)
        {
            throw new CaseValidationException($"{fieldName} must be at most 500 characters.");
        }

        return trimmed;
    }

    private sealed record ValidatedCreateCaseRequest(
        string CrimeNumber,
        string StNumber,
        string BeneficiaryName,
        int? BeneficiaryAge,
        string? BeneficiaryContact,
        string TypeOfOffence,
        OffenceClassification OffenceClassification,
        Domicile Domicile,
        bool IsFirstTimeOffender,
        SensitivityLevel SensitivityLevel,
        Gender? Gender,
        FamilyType? FamilyType,
        EconomicStatus? EconomicStatus,
        Guid? OccupationId,
        Guid? EducationLevelId,
        bool FamilyHistoryOfCrime,
        int? RecidivismBeforeCount,
        int? RecidivismAfterCount);
}

public sealed class CaseValidationException(string message) : Exception(message);

public sealed class CaseBusinessRuleException(string message) : Exception(message);

public sealed class CaseNotFoundException : Exception;

public sealed class CaseConflictException(string message) : Exception(message);

public sealed class CaseForbiddenException : Exception;

public sealed class CaseStepUpRequiredException : Exception;
