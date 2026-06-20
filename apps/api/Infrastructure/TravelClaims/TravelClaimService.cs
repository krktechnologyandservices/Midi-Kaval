using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using MidiKaval.Api.Domain.Entities;
using MidiKaval.Api.Domain.Enums;
using MidiKaval.Api.Infrastructure.Audit;
using MidiKaval.Api.Infrastructure.Auth;
using MidiKaval.Api.Infrastructure.Cases;
using MidiKaval.Api.Infrastructure.Email;
using MidiKaval.Api.Infrastructure.Notifications;
using MidiKaval.Api.Infrastructure.Persistence;
using MidiKaval.Api.Models.Attachments;
using MidiKaval.Api.Models.TravelClaims;

namespace MidiKaval.Api.Infrastructure.TravelClaims;

public sealed class TravelClaimService(
    AppDbContext db,
    IHttpContextAccessor httpContextAccessor,
    NotificationService notificationService,
    PushDeliveryService pushDeliveryService,
    EmailDeliveryService emailDeliveryService,
    ILogger<TravelClaimService> logger)
{
    private const decimal MaxAmount = 999_999.99m;

    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web);

    private static readonly string[] AllowedTransportModeNames = ["Bus", "Auto", "Petrol", "Other"];

    public async Task<TravelClaimDto> CreateAsync(
        CreateTravelClaimRequest request,
        CancellationToken cancellationToken = default)
    {
        if (request is null)
        {
            throw new CaseValidationException("Request body is required.");
        }

        var (organisationId, actorUserId) = ResolveActorContext();
        var actorRole = ResolveActorRole();

        var claimDate = NormalizeClaimDate(request.ClaimDate);
        var startLocation = ValidateLocation(request.StartLocation, "startLocation");
        var destination = ValidateLocation(request.Destination, "destination");
        var transportMode = ParseTransportMode(request.TransportMode);
        var amount = ValidateAmount(request.Amount);
        var autoNumber = ValidateAutoNumber(transportMode, request.AutoNumber);
        var notes = ValidateOptionalNotes(request.Notes);
        var caseIds = ValidateCaseIds(request.CaseIds);

        await EnsureCanLinkCasesAsync(caseIds, organisationId, actorUserId, actorRole, cancellationToken);

        var now = DateTime.UtcNow;
        var claimId = Guid.NewGuid();

        var claim = new TravelClaim
        {
            Id = claimId,
            OrganisationId = organisationId,
            ClaimantUserId = actorUserId,
            ClaimDate = claimDate,
            StartLocation = startLocation,
            Destination = destination,
            TransportMode = transportMode,
            Amount = amount,
            AutoNumber = autoNumber,
            Notes = notes,
            Status = TravelClaimStatus.Draft,
            CreatedAtUtc = now,
            UpdatedAtUtc = now,
        };

        db.TravelClaims.Add(claim);
        AddCaseLinks(claimId, organisationId, caseIds);
        AddAuditEvent(
            organisationId,
            actorUserId,
            AuditEventTypes.TravelClaimCreated,
            claimId,
            TravelClaimStatus.Draft,
            transportMode,
            now);

        await db.SaveChangesAsync(cancellationToken);

        return await MapToDtoAsync(claim, organisationId, cancellationToken);
    }

    public async Task<(TravelClaimListResultDto Result, int TotalCount)> ListMineAsync(
        CancellationToken cancellationToken = default)
    {
        var (organisationId, actorUserId) = ResolveActorContext();

        var claims = await db.TravelClaims
            .Where(c => c.OrganisationId == organisationId && c.ClaimantUserId == actorUserId)
            .OrderByDescending(c => c.ClaimDate)
            .ThenByDescending(c => c.Id)
            .ToListAsync(cancellationToken);

        var items = new List<TravelClaimDto>(claims.Count);
        foreach (var claim in claims)
        {
            items.Add(await MapToDtoAsync(claim, organisationId, cancellationToken));
        }

        return (new TravelClaimListResultDto { Items = items }, items.Count);
    }

    public async Task<TravelClaimDto> GetAsync(Guid claimId, CancellationToken cancellationToken = default)
    {
        var (organisationId, actorUserId) = ResolveActorContext();

        var claim = await db.TravelClaims.SingleOrDefaultAsync(
            c => c.Id == claimId && c.OrganisationId == organisationId,
            cancellationToken);

        if (claim is null || claim.ClaimantUserId != actorUserId)
        {
            throw new CaseNotFoundException();
        }

        return await MapToDtoAsync(claim, organisationId, cancellationToken);
    }

    public async Task<TravelClaimDto> UpdateAsync(
        Guid claimId,
        UpdateTravelClaimRequest request,
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

        var claim = await db.TravelClaims.SingleOrDefaultAsync(
            c => c.Id == claimId && c.OrganisationId == organisationId,
            cancellationToken);

        if (claim is null)
        {
            throw new CaseNotFoundException();
        }

        if (claim.ClaimantUserId != actorUserId)
        {
            throw new CaseForbiddenException();
        }

        if (claim.Status != TravelClaimStatus.Draft)
        {
            throw new CaseBusinessRuleException("Travel claims can only be updated while in Draft status.");
        }

        if (request.ClaimDate.HasValue)
        {
            claim.ClaimDate = NormalizeClaimDate(request.ClaimDate);
        }

        if (request.StartLocation is not null)
        {
            claim.StartLocation = ValidateLocation(request.StartLocation, "startLocation");
        }

        if (request.Destination is not null)
        {
            claim.Destination = ValidateLocation(request.Destination, "destination");
        }

        TransportMode? updatedMode = null;
        if (request.TransportMode is not null)
        {
            updatedMode = ParseTransportMode(request.TransportMode);
            claim.TransportMode = updatedMode.Value;
            if (updatedMode != TransportMode.Auto)
            {
                claim.AutoNumber = null;
            }
        }

        if (request.Amount.HasValue)
        {
            claim.Amount = ValidateAmount(request.Amount);
        }

        if (request.AutoNumber is not null || updatedMode == TransportMode.Auto)
        {
            claim.AutoNumber = ValidateAutoNumber(
                updatedMode ?? claim.TransportMode,
                request.AutoNumber ?? claim.AutoNumber);
        }

        if (request.Notes is not null)
        {
            claim.Notes = ValidateOptionalNotes(request.Notes);
        }

        if (request.CaseIds is not null)
        {
            var caseIds = ValidateCaseIds(request.CaseIds);
            await EnsureCanLinkCasesAsync(caseIds, organisationId, actorUserId, actorRole, cancellationToken);

            var existingLinks = await db.TravelClaimCaseLinks
                .Where(l => l.TravelClaimId == claimId)
                .ToListAsync(cancellationToken);
            db.TravelClaimCaseLinks.RemoveRange(existingLinks);
            AddCaseLinks(claimId, organisationId, caseIds);
        }

        claim.UpdatedAtUtc = DateTime.UtcNow;

        await EnsureStillDraftAsync(
            claimId,
            organisationId,
            "Travel claims can only be updated while in Draft status.",
            cancellationToken);

        AddAuditEvent(
            organisationId,
            actorUserId,
            AuditEventTypes.TravelClaimUpdated,
            claim.Id,
            claim.Status,
            claim.TransportMode,
            claim.UpdatedAtUtc);

        await db.SaveChangesAsync(cancellationToken);

        return await MapToDtoAsync(claim, organisationId, cancellationToken);
    }

    public async Task<TravelClaimDto> SubmitAsync(Guid claimId, CancellationToken cancellationToken = default)
    {
        var (organisationId, actorUserId) = ResolveActorContext();

        var claim = await db.TravelClaims.SingleOrDefaultAsync(
            c => c.Id == claimId && c.OrganisationId == organisationId,
            cancellationToken);

        if (claim is null)
        {
            throw new CaseNotFoundException();
        }

        if (claim.ClaimantUserId != actorUserId)
        {
            throw new CaseForbiddenException();
        }

        if (claim.Status != TravelClaimStatus.Draft)
        {
            throw new CaseBusinessRuleException("Travel claims can only be submitted from Draft status.");
        }

        if (RequiresReceipt(claim.TransportMode))
        {
            var hasConfirmedReceipt = await db.Attachments.AnyAsync(
                a => a.OrganisationId == organisationId
                    && a.ResourceType == AttachmentResourceType.TravelClaim
                    && a.ResourceId == claimId
                    && a.Status == AttachmentStatus.Confirmed,
                cancellationToken);

            if (!hasConfirmedReceipt)
            {
                throw new CaseBusinessRuleException(
                    "Receipt image is required for Bus, Auto, and Petrol claims before submit.");
            }
        }

        if (claim.TransportMode == TransportMode.Auto && string.IsNullOrWhiteSpace(claim.AutoNumber))
        {
            throw new CaseBusinessRuleException("autoNumber is required when transportMode is Auto.");
        }

        await EnsureStillDraftAsync(
            claimId,
            organisationId,
            "Travel claims can only be submitted from Draft status.",
            cancellationToken);

        var now = DateTime.UtcNow;
        claim.Status = TravelClaimStatus.Submitted;
        claim.SubmittedAtUtc = now;
        claim.UpdatedAtUtc = now;

        AddAuditEvent(
            organisationId,
            actorUserId,
            AuditEventTypes.TravelClaimSubmitted,
            claim.Id,
            claim.Status,
            claim.TransportMode,
            now);

        await db.SaveChangesAsync(cancellationToken);

        var submitClaimantEmail = await GetClaimantEmailAsync(
            claim.ClaimantUserId,
            organisationId,
            cancellationToken);
        await emailDeliveryService.TrySendTravelClaimSubmittedAsync(
            claim,
            submitClaimantEmail,
            cancellationToken);

        return await MapToDtoAsync(claim, organisationId, cancellationToken);
    }

    public async Task<(TravelClaimMonthlyTotalsResultDto Result, int TotalCount)> ListMonthlyTotalsAsync(
        int year,
        int month,
        CancellationToken cancellationToken = default)
    {
        ValidateYearMonth(year, month);

        var organisationId = ResolveActorContext().OrganisationId;
        var periodStart = new DateTime(year, month, 1, 0, 0, 0, DateTimeKind.Utc);
        var periodEnd = periodStart.AddMonths(1);

        var rows = await (
            from claim in db.TravelClaims
            join user in db.Users on claim.ClaimantUserId equals user.Id
            where claim.OrganisationId == organisationId
                && claim.ClaimDate >= periodStart
                && claim.ClaimDate < periodEnd
                && (claim.Status == TravelClaimStatus.Submitted || claim.Status == TravelClaimStatus.Approved)
                && user.OrganisationId == organisationId
            group claim by new { claim.ClaimantUserId, user.Email } into grouped
            orderby grouped.Key.Email
            select new TravelClaimMonthlyTotalDto
            {
                StaffUserId = grouped.Key.ClaimantUserId,
                StaffEmail = grouped.Key.Email,
                ClaimCount = grouped.Count(),
                TotalAmount = grouped.Sum(c => c.Amount),
            }).ToListAsync(cancellationToken);

        return (new TravelClaimMonthlyTotalsResultDto { Items = rows }, rows.Count);
    }

    public async Task<(TravelClaimListResultDto Result, int TotalCount)> ListPendingForDirectorAsync(
        CancellationToken cancellationToken = default)
    {
        var organisationId = ResolveActorContext().OrganisationId;

        var rows = await (
            from claim in db.TravelClaims
            join user in db.Users on claim.ClaimantUserId equals user.Id
            where claim.OrganisationId == organisationId
                && claim.Status == TravelClaimStatus.Submitted
                && user.OrganisationId == organisationId
                && db.TravelClaimCaseLinks.Any(l =>
                    l.TravelClaimId == claim.Id && l.OrganisationId == organisationId)
            orderby claim.SubmittedAtUtc, claim.Id
            select new { Claim = claim, ClaimantEmail = user.Email }).ToListAsync(cancellationToken);

        var items = new List<TravelClaimDto>(rows.Count);
        foreach (var row in rows)
        {
            items.Add(await MapToSupervisorDtoAsync(row.Claim, organisationId, row.ClaimantEmail, cancellationToken));
        }

        return (new TravelClaimListResultDto { Items = items }, items.Count);
    }

    public async Task<TravelClaimDto> GetForDirectorAsync(
        Guid claimId,
        CancellationToken cancellationToken = default)
    {
        var organisationId = ResolveActorContext().OrganisationId;

        var row = await (
            from claim in db.TravelClaims
            join user in db.Users on claim.ClaimantUserId equals user.Id
            where claim.Id == claimId
                && claim.OrganisationId == organisationId
                && user.OrganisationId == organisationId
            select new { Claim = claim, ClaimantEmail = user.Email }).SingleOrDefaultAsync(cancellationToken);

        if (row is null)
        {
            throw new CaseNotFoundException();
        }

        return await MapToSupervisorDtoAsync(row.Claim, organisationId, row.ClaimantEmail, cancellationToken);
    }

    public async Task<TravelClaimDto> GetForSupervisorAsync(
        Guid claimId,
        CancellationToken cancellationToken = default)
    {
        var organisationId = ResolveActorContext().OrganisationId;

        var row = await (
            from claim in db.TravelClaims
            join user in db.Users on claim.ClaimantUserId equals user.Id
            where claim.Id == claimId
                && claim.OrganisationId == organisationId
                && user.OrganisationId == organisationId
                && (claim.Status == TravelClaimStatus.Submitted
                    || claim.Status == TravelClaimStatus.Approved
                    || claim.Status == TravelClaimStatus.Returned)
            select new { Claim = claim, ClaimantEmail = user.Email }).SingleOrDefaultAsync(cancellationToken);

        if (row is null)
        {
            throw new CaseNotFoundException();
        }

        return await MapToSupervisorDtoAsync(row.Claim, organisationId, row.ClaimantEmail, cancellationToken);
    }

    public async Task<TravelClaimDto> ApproveAsync(
        Guid claimId,
        ApproveTravelClaimRequest? request,
        CancellationToken cancellationToken = default)
    {
        var (organisationId, actorUserId) = ResolveActorContext();
        var comment = ValidateOptionalDecisionComment(request?.Comment);

        var claim = await LoadOrgClaimAsync(claimId, organisationId, cancellationToken);
        EnsureSubmitted(claim);
        EnsureActorNotClaimant(claim, actorUserId);

        await EnsureStillSubmittedAsync(claimId, organisationId, cancellationToken);

        var now = DateTime.UtcNow;
        claim.Status = TravelClaimStatus.Approved;
        claim.DecisionComment = comment;
        claim.DecidedAtUtc = now;
        claim.DecidedByUserId = actorUserId;
        claim.UpdatedAtUtc = now;

        var caseId = await GetFirstLinkedCaseIdAsync(claimId, organisationId, cancellationToken);

        AddDecisionAuditEvent(
            organisationId,
            actorUserId,
            AuditEventTypes.TravelClaimApproved,
            claim,
            now);

        var notification = notificationService.CreateTravelClaimDecisionNotificationForSave(
            claim,
            caseId,
            NotificationEventTypes.TravelClaimApproved,
            comment);

        await db.SaveChangesAsync(cancellationToken);
        await pushDeliveryService.TrySendAsync(notification, cancellationToken);

        var approveClaimantEmail = await GetClaimantEmailAsync(
            claim.ClaimantUserId,
            organisationId,
            cancellationToken);
        await emailDeliveryService.TrySendTravelClaimDecisionAsync(
            claim,
            approveClaimantEmail,
            NotificationEventTypes.TravelClaimApproved,
            comment,
            cancellationToken);

        return await MapToSupervisorDtoAsync(
            claim,
            organisationId,
            approveClaimantEmail,
            cancellationToken);
    }

    public async Task<TravelClaimDto> ReturnAsync(
        Guid claimId,
        ReturnTravelClaimRequest? request,
        CancellationToken cancellationToken = default)
    {
        if (request is null)
        {
            throw new CaseValidationException("Request body is required.");
        }

        var comment = ValidateRequiredDecisionComment(request.Comment);
        var (organisationId, actorUserId) = ResolveActorContext();

        var claim = await LoadOrgClaimAsync(claimId, organisationId, cancellationToken);
        EnsureSubmitted(claim);
        EnsureActorNotClaimant(claim, actorUserId);

        await EnsureStillSubmittedAsync(claimId, organisationId, cancellationToken);

        var now = DateTime.UtcNow;
        claim.Status = TravelClaimStatus.Returned;
        claim.DecisionComment = comment;
        claim.DecidedAtUtc = now;
        claim.DecidedByUserId = actorUserId;
        claim.UpdatedAtUtc = now;

        var caseId = await GetFirstLinkedCaseIdAsync(claimId, organisationId, cancellationToken);

        AddDecisionAuditEvent(
            organisationId,
            actorUserId,
            AuditEventTypes.TravelClaimReturned,
            claim,
            now);

        var notification = notificationService.CreateTravelClaimDecisionNotificationForSave(
            claim,
            caseId,
            NotificationEventTypes.TravelClaimReturned,
            comment);

        await db.SaveChangesAsync(cancellationToken);
        await pushDeliveryService.TrySendAsync(notification, cancellationToken);

        var returnClaimantEmail = await GetClaimantEmailAsync(
            claim.ClaimantUserId,
            organisationId,
            cancellationToken);
        await emailDeliveryService.TrySendTravelClaimDecisionAsync(
            claim,
            returnClaimantEmail,
            NotificationEventTypes.TravelClaimReturned,
            comment,
            cancellationToken);

        return await MapToSupervisorDtoAsync(
            claim,
            organisationId,
            returnClaimantEmail,
            cancellationToken);
    }

    public static void ValidateYearMonth(int year, int month)
    {
        if (year < 2000 || year > 2100)
        {
            throw new CaseValidationException("year must be between 2000 and 2100.");
        }

        if (month is < 1 or > 12)
        {
            throw new CaseValidationException("month must be between 1 and 12.");
        }
    }

    private async Task EnsureCanLinkCasesAsync(
        IReadOnlyList<Guid> caseIds,
        Guid organisationId,
        Guid actorUserId,
        string actorRole,
        CancellationToken cancellationToken)
    {
        foreach (var caseId in caseIds)
        {
            var caseEntity = await db.Cases.SingleOrDefaultAsync(
                c => c.Id == caseId && c.OrganisationId == organisationId,
                cancellationToken);

            if (caseEntity is null)
            {
                throw new CaseNotFoundException();
            }

            EnsureCanReadCase(caseEntity, actorUserId, actorRole);
        }
    }

    private void AddCaseLinks(Guid claimId, Guid organisationId, IReadOnlyList<Guid> caseIds)
    {
        foreach (var caseId in caseIds)
        {
            db.TravelClaimCaseLinks.Add(new TravelClaimCaseLink
            {
                TravelClaimId = claimId,
                CaseId = caseId,
                OrganisationId = organisationId,
            });
        }
    }

    private async Task<TravelClaimDto> MapToDtoAsync(
        TravelClaim claim,
        Guid organisationId,
        CancellationToken cancellationToken)
    {
        var caseIds = await db.TravelClaimCaseLinks
            .Where(l => l.TravelClaimId == claim.Id && l.OrganisationId == organisationId)
            .Select(l => l.CaseId)
            .OrderBy(id => id)
            .ToListAsync(cancellationToken);

        var attachments = await db.Attachments
            .Where(a => a.OrganisationId == organisationId
                && a.ResourceType == AttachmentResourceType.TravelClaim
                && a.ResourceId == claim.Id
                && a.Status == AttachmentStatus.Confirmed)
            .OrderBy(a => a.ConfirmedAtUtc)
            .Select(a => new AttachmentSummaryDto
            {
                Id = a.Id,
                OriginalFileName = a.OriginalFileName,
                ContentType = a.ContentType,
                FileSizeBytes = a.FileSizeBytes,
                ConfirmedAtUtc = a.ConfirmedAtUtc!.Value,
            })
            .ToListAsync(cancellationToken);

        return new TravelClaimDto
        {
            Id = claim.Id,
            ClaimDate = claim.ClaimDate,
            StartLocation = claim.StartLocation,
            Destination = claim.Destination,
            TransportMode = claim.TransportMode.ToString(),
            Amount = claim.Amount,
            AutoNumber = claim.AutoNumber,
            Notes = claim.Notes,
            Status = claim.Status.ToString(),
            ClaimantUserId = claim.ClaimantUserId,
            SubmittedAtUtc = claim.SubmittedAtUtc,
            DecisionComment = claim.DecisionComment,
            DecidedAtUtc = claim.DecidedAtUtc,
            DecidedByUserId = claim.DecidedByUserId,
            CreatedAtUtc = claim.CreatedAtUtc,
            UpdatedAtUtc = claim.UpdatedAtUtc,
            CaseIds = caseIds,
            Attachments = attachments,
        };
    }

    private async Task<TravelClaimDto> MapToSupervisorDtoAsync(
        TravelClaim claim,
        Guid organisationId,
        string claimantEmail,
        CancellationToken cancellationToken)
    {
        var dto = await MapToDtoAsync(claim, organisationId, cancellationToken);
        dto.ClaimantEmail = claimantEmail;
        return dto;
    }

    private async Task<TravelClaim> LoadOrgClaimAsync(
        Guid claimId,
        Guid organisationId,
        CancellationToken cancellationToken)
    {
        var claim = await db.TravelClaims.SingleOrDefaultAsync(
            c => c.Id == claimId && c.OrganisationId == organisationId,
            cancellationToken);

        if (claim is null)
        {
            throw new CaseNotFoundException();
        }

        return claim;
    }

    private static void EnsureSubmitted(TravelClaim claim)
    {
        if (claim.Status != TravelClaimStatus.Submitted)
        {
            throw new CaseBusinessRuleException("Travel claims can only be approved or returned from Submitted status.");
        }
    }

    private static void EnsureActorNotClaimant(TravelClaim claim, Guid actorUserId)
    {
        if (claim.ClaimantUserId == actorUserId)
        {
            throw new CaseBusinessRuleException("Directors cannot approve or return their own travel claims.");
        }
    }

    private async Task EnsureStillSubmittedAsync(
        Guid claimId,
        Guid organisationId,
        CancellationToken cancellationToken)
    {
        var currentStatus = await db.TravelClaims
            .AsNoTracking()
            .Where(c => c.Id == claimId && c.OrganisationId == organisationId)
            .Select(c => c.Status)
            .SingleOrDefaultAsync(cancellationToken);

        if (currentStatus != TravelClaimStatus.Submitted)
        {
            throw new CaseBusinessRuleException("Travel claims can only be approved or returned from Submitted status.");
        }
    }

    private async Task<Guid> GetFirstLinkedCaseIdAsync(
        Guid claimId,
        Guid organisationId,
        CancellationToken cancellationToken)
    {
        var caseId = await db.TravelClaimCaseLinks
            .Where(l => l.TravelClaimId == claimId && l.OrganisationId == organisationId)
            .OrderBy(l => l.CaseId)
            .Select(l => l.CaseId)
            .FirstOrDefaultAsync(cancellationToken);

        if (caseId == Guid.Empty)
        {
            throw new CaseBusinessRuleException("Travel claim must be linked to at least one case before approval.");
        }

        return caseId;
    }

    private async Task<string> GetClaimantEmailAsync(
        Guid claimantUserId,
        Guid organisationId,
        CancellationToken cancellationToken)
    {
        var email = await db.Users
            .Where(u => u.Id == claimantUserId && u.OrganisationId == organisationId)
            .Select(u => u.Email)
            .SingleOrDefaultAsync(cancellationToken);

        if (email is null)
        {
            logger.LogWarning(
                "Claimant user {UserId} not found in org {OrganisationId}",
                claimantUserId,
                organisationId);
            return string.Empty;
        }

        return email;
    }

    private static string? ValidateOptionalDecisionComment(string? value)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            return null;
        }

        var trimmed = value.Trim();
        if (trimmed.Length > 2000)
        {
            throw new CaseValidationException("comment must be at most 2000 characters.");
        }

        return trimmed;
    }

    private static string ValidateRequiredDecisionComment(string? value)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            throw new CaseValidationException("comment is required.");
        }

        var trimmed = value.Trim();
        if (trimmed.Length > 2000)
        {
            throw new CaseValidationException("comment must be at most 2000 characters.");
        }

        return trimmed;
    }

    private void AddDecisionAuditEvent(
        Guid organisationId,
        Guid actorUserId,
        string eventType,
        TravelClaim claim,
        DateTime createdAtUtc)
    {
        db.AuditEvents.Add(new AuditEvent
        {
            Id = Guid.NewGuid(),
            OrganisationId = organisationId,
            ActorUserId = actorUserId,
            SubjectUserId = claim.ClaimantUserId,
            EventType = eventType,
            MetadataJson = JsonSerializer.Serialize(
                new Dictionary<string, object?>
                {
                    ["claimId"] = claim.Id.ToString("D"),
                    ["status"] = claim.Status.ToString(),
                    ["claimantUserId"] = claim.ClaimantUserId.ToString("D"),
                    ["amount"] = claim.Amount,
                },
                JsonOptions),
            CreatedAtUtc = createdAtUtc,
        });
    }

    private async Task EnsureStillDraftAsync(
        Guid claimId,
        Guid organisationId,
        string notDraftMessage,
        CancellationToken cancellationToken)
    {
        var currentStatus = await db.TravelClaims
            .AsNoTracking()
            .Where(c => c.Id == claimId && c.OrganisationId == organisationId)
            .Select(c => c.Status)
            .SingleOrDefaultAsync(cancellationToken);

        if (currentStatus != TravelClaimStatus.Draft)
        {
            throw new CaseBusinessRuleException(notDraftMessage);
        }
    }

    private void AddAuditEvent(
        Guid organisationId,
        Guid actorUserId,
        string eventType,
        Guid claimId,
        TravelClaimStatus status,
        TransportMode transportMode,
        DateTime createdAtUtc)
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
                    ["claimId"] = claimId.ToString("D"),
                    ["status"] = status.ToString(),
                    ["transportMode"] = transportMode.ToString(),
                },
                JsonOptions),
            CreatedAtUtc = createdAtUtc,
        });
    }

    private static bool RequiresReceipt(TransportMode mode) =>
        mode is TransportMode.Bus or TransportMode.Auto or TransportMode.Petrol;

    private static bool HasAnyUpdateField(UpdateTravelClaimRequest request) =>
        request.ClaimDate.HasValue
        || request.StartLocation is not null
        || request.Destination is not null
        || request.TransportMode is not null
        || request.Amount.HasValue
        || request.AutoNumber is not null
        || request.Notes is not null
        || request.CaseIds is not null;

    private static IReadOnlyList<Guid> ValidateCaseIds(IReadOnlyList<Guid>? caseIds)
    {
        if (caseIds is null || caseIds.Count == 0)
        {
            throw new CaseValidationException("caseIds must contain at least one case id.");
        }

        if (caseIds.Any(id => id == Guid.Empty))
        {
            throw new CaseValidationException("caseIds must not contain empty GUIDs.");
        }

        if (caseIds.Distinct().Count() != caseIds.Count)
        {
            throw new CaseValidationException("caseIds must not contain duplicate case ids.");
        }

        return caseIds;
    }

    private static DateTime NormalizeClaimDate(DateTime? value)
    {
        if (!value.HasValue || value.Value == default)
        {
            throw new CaseValidationException("claimDate is required.");
        }

        var date = value.Value;
        if (date.Kind == DateTimeKind.Unspecified)
        {
            date = DateTime.SpecifyKind(date, DateTimeKind.Utc);
        }
        else if (date.Kind == DateTimeKind.Local)
        {
            date = date.ToUniversalTime();
        }

        return new DateTime(date.Year, date.Month, date.Day, 0, 0, 0, DateTimeKind.Utc);
    }

    private static string ValidateLocation(string? value, string fieldName)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            throw new CaseValidationException($"{fieldName} is required.");
        }

        var trimmed = value.Trim();
        if (trimmed.Length > 256)
        {
            throw new CaseValidationException($"{fieldName} must be at most 256 characters.");
        }

        return trimmed;
    }

    private static TransportMode ParseTransportMode(string? value)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            throw new CaseValidationException("transportMode is required.");
        }

        var trimmed = value.Trim();
        if (!AllowedTransportModeNames.Contains(trimmed, StringComparer.Ordinal))
        {
            throw new CaseValidationException(
                "transportMode is invalid. Allowed values: Bus, Auto, Petrol, Other.");
        }

        return Enum.Parse<TransportMode>(trimmed, ignoreCase: false);
    }

    private static decimal ValidateAmount(decimal? value)
    {
        if (!value.HasValue)
        {
            throw new CaseValidationException("amount is required.");
        }

        var rounded = decimal.Round(value.Value, 2, MidpointRounding.AwayFromZero);
        if (rounded <= 0 || rounded > MaxAmount)
        {
            throw new CaseValidationException("amount must be greater than zero and at most 999999.99.");
        }

        return rounded;
    }

    private static string? ValidateAutoNumber(TransportMode mode, string? value)
    {
        if (mode != TransportMode.Auto)
        {
            return null;
        }

        if (string.IsNullOrWhiteSpace(value))
        {
            throw new CaseValidationException("autoNumber is required when transportMode is Auto.");
        }

        var trimmed = value.Trim();
        if (trimmed.Length > 32)
        {
            throw new CaseValidationException("autoNumber must be at most 32 characters.");
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
