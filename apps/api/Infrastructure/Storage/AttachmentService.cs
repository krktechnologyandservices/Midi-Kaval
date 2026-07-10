using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;
using MidiKaval.Api.Domain.Entities;
using MidiKaval.Api.Domain.Enums;
using MidiKaval.Api.Infrastructure.Audit;
using MidiKaval.Api.Infrastructure.Auth;
using MidiKaval.Api.Infrastructure.Cases;
using MidiKaval.Api.Infrastructure.Encryption;
using MidiKaval.Api.Infrastructure.Persistence;
using MidiKaval.Api.Models.Attachments;

namespace MidiKaval.Api.Infrastructure.Storage;

public sealed class AttachmentService(
    AppDbContext db,
    IHttpContextAccessor httpContextAccessor,
    IBlobStorageService blobStorage,
    FileEncryptionService fileEncryption,
    IOptions<B2StorageOptions> storageOptions)
{
    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web);

    private static readonly string[] AllowedResourceTypeNames = ["CaseNote", "TravelClaim", "BudgetUtilization"];

    private static readonly string[] AllowedContentTypes =
    [
        "image/jpeg",
        "image/png",
        "image/webp",
        "application/pdf",
    ];

    public async Task<AttachmentDto> UploadAsync(
        string? resourceTypeRaw,
        Guid resourceId,
        string? fileName,
        string? contentType,
        byte[] fileContent,
        CancellationToken cancellationToken = default)
    {
        var resourceType = ParseResourceType(resourceTypeRaw);
        var (organisationId, actorUserId) = ResolveActorContext();
        var actorRole = ResolveActorRole();

        var validatedFileName = ValidateFileName(fileName);
        var validatedContentType = ValidateContentType(contentType);
        ValidateFileSizeBytes(fileContent.Length);

        Guid resolvedResourceId;
        AttachmentResourceType attachmentResourceType;

        if (resourceType == AttachmentResourceType.CaseNote)
        {
            var note = await db.CaseNotes.SingleOrDefaultAsync(
                n => n.Id == resourceId && n.OrganisationId == organisationId,
                cancellationToken);

            if (note is null)
            {
                throw new CaseNotFoundException();
            }

            var caseEntity = await db.Cases.SingleOrDefaultAsync(
                c => c.Id == note.CaseId && c.OrganisationId == organisationId,
                cancellationToken);

            if (caseEntity is null)
            {
                throw new CaseNotFoundException();
            }

            EnsureCanReadCase(caseEntity, actorUserId, actorRole);
            resolvedResourceId = note.Id;
            attachmentResourceType = AttachmentResourceType.CaseNote;
        }
        else if (resourceType == AttachmentResourceType.BudgetUtilization)
        {
            var utilization = await db.BudgetUtilizations
                .Include(u => u.BudgetLineItem)
                .SingleOrDefaultAsync(u => u.Id == resourceId, cancellationToken);

            var belongsToOrg = utilization is not null && await db.ProjectBudgets.AnyAsync(
                pb => pb.Id == utilization.BudgetLineItem.ProjectBudgetId && pb.OrganisationId == organisationId,
                cancellationToken);

            if (utilization is null || !belongsToOrg)
            {
                throw new CaseNotFoundException();
            }

            EnsureCanWriteBudgetUtilization(actorRole);
            resolvedResourceId = utilization.Id;
            attachmentResourceType = AttachmentResourceType.BudgetUtilization;
        }
        else
        {
            var claim = await db.TravelClaims.SingleOrDefaultAsync(
                c => c.Id == resourceId && c.OrganisationId == organisationId,
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
                throw new CaseBusinessRuleException(
                    "Receipts can only be added while the claim is in Draft status.");
            }

            resolvedResourceId = claim.Id;
            attachmentResourceType = AttachmentResourceType.TravelClaim;
        }

        if (attachmentResourceType == AttachmentResourceType.TravelClaim)
        {
            await EnsureTravelClaimStillDraftAsync(resolvedResourceId, organisationId, cancellationToken);
        }

        var now = DateTime.UtcNow;
        var attachmentId = Guid.NewGuid();
        var blobName = attachmentResourceType switch
        {
            AttachmentResourceType.CaseNote => BuildBlobName(organisationId, resolvedResourceId, attachmentId, validatedFileName),
            AttachmentResourceType.BudgetUtilization => BuildBudgetUtilizationBlobName(organisationId, resolvedResourceId, attachmentId, validatedFileName),
            _ => BuildTravelClaimBlobName(organisationId, resolvedResourceId, attachmentId, validatedFileName),
        };

        var encrypted = fileEncryption.Encrypt(fileContent);
        await blobStorage.UploadAsync(blobName, encrypted, validatedContentType, cancellationToken);

        var attachment = new Attachment
        {
            Id = attachmentId,
            OrganisationId = organisationId,
            ResourceType = attachmentResourceType,
            ResourceId = resolvedResourceId,
            BlobName = blobName,
            OriginalFileName = validatedFileName,
            ContentType = validatedContentType,
            FileSizeBytes = fileContent.Length,
            Status = AttachmentStatus.Confirmed,
            UploadedByUserId = actorUserId,
            CreatedAtUtc = now,
            ConfirmedAtUtc = now,
        };

        db.Attachments.Add(attachment);
        db.AuditEvents.Add(new AuditEvent
        {
            Id = Guid.NewGuid(),
            OrganisationId = organisationId,
            ActorUserId = actorUserId,
            SubjectUserId = actorUserId,
            EventType = AuditEventTypes.AttachmentConfirmed,
            MetadataJson = JsonSerializer.Serialize(
                new Dictionary<string, object?>
                {
                    ["attachmentId"] = attachmentId.ToString("D"),
                    ["resourceType"] = attachmentResourceType.ToString(),
                    ["resourceId"] = resolvedResourceId.ToString("D"),
                    ["contentType"] = validatedContentType,
                },
                JsonOptions),
            CreatedAtUtc = now,
        });

        await db.SaveChangesAsync(cancellationToken);

        return MapToDto(attachment);
    }

    public async Task<AttachmentContentDto> DownloadAsync(
        Guid attachmentId,
        CancellationToken cancellationToken = default)
    {
        var (organisationId, actorUserId) = ResolveActorContext();
        var actorRole = ResolveActorRole();

        var attachment = await db.Attachments.SingleOrDefaultAsync(
            a => a.Id == attachmentId && a.OrganisationId == organisationId,
            cancellationToken);

        if (attachment is null)
        {
            throw new AttachmentNotFoundException();
        }

        if (attachment.Status != AttachmentStatus.Confirmed)
        {
            throw new CaseBusinessRuleException("Attachment upload not confirmed.");
        }

        await EnsureCanAccessAttachmentAsync(attachment, organisationId, actorUserId, actorRole, isWriteOperation: false, cancellationToken);

        var encrypted = await blobStorage.DownloadAsync(attachment.BlobName, cancellationToken)
            ?? throw new AttachmentNotFoundException();

        var content = fileEncryption.Decrypt(encrypted);

        return new AttachmentContentDto
        {
            Content = content,
            ContentType = attachment.ContentType,
            OriginalFileName = attachment.OriginalFileName,
        };
    }

    private async Task EnsureCanAccessAttachmentAsync(
        Attachment attachment,
        Guid organisationId,
        Guid actorUserId,
        string actorRole,
        bool isWriteOperation,
        CancellationToken cancellationToken)
    {
        if (attachment.ResourceType == AttachmentResourceType.CaseNote)
        {
            var note = await db.CaseNotes.SingleOrDefaultAsync(
                n => n.Id == attachment.ResourceId && n.OrganisationId == organisationId,
                cancellationToken);

            if (note is null)
            {
                throw new AttachmentNotFoundException();
            }

            var caseEntity = await db.Cases.SingleOrDefaultAsync(
                c => c.Id == note.CaseId && c.OrganisationId == organisationId,
                cancellationToken);

            if (caseEntity is null)
            {
                throw new CaseNotFoundException();
            }

            EnsureCanReadCase(caseEntity, actorUserId, actorRole);
            return;
        }

        if (attachment.ResourceType == AttachmentResourceType.TravelClaim)
        {
            var claim = await db.TravelClaims.SingleOrDefaultAsync(
                c => c.Id == attachment.ResourceId && c.OrganisationId == organisationId,
                cancellationToken);

            if (claim is null)
            {
                throw new AttachmentNotFoundException();
            }

            if (claim.ClaimantUserId == actorUserId)
            {
                return;
            }

            if (IsSupervisorRole(actorRole)
                && claim.Status is TravelClaimStatus.Submitted
                    or TravelClaimStatus.Approved
                    or TravelClaimStatus.Returned)
            {
                return;
            }

            throw new CaseForbiddenException();
        }

        if (attachment.ResourceType == AttachmentResourceType.BudgetUtilization)
        {
            var utilization = await db.BudgetUtilizations
                .Include(u => u.BudgetLineItem)
                .SingleOrDefaultAsync(u => u.Id == attachment.ResourceId, cancellationToken);

            var belongsToOrg = utilization is not null && await db.ProjectBudgets.AnyAsync(
                pb => pb.Id == utilization.BudgetLineItem.ProjectBudgetId && pb.OrganisationId == organisationId,
                cancellationToken);

            if (utilization is null || !belongsToOrg)
            {
                throw new AttachmentNotFoundException();
            }

            if (isWriteOperation)
            {
                EnsureCanWriteBudgetUtilization(actorRole);
            }
            else
            {
                EnsureCanReadBudgetUtilization(actorRole);
            }
            return;
        }

        throw new CaseForbiddenException();
    }

    // Mirrors Policies.AccountantOrAbove — matches who can create/update/delete a utilization entry
    // on BudgetsController, so uploading/finalizing a receipt requires the same rights as the entry itself.
    private static void EnsureCanWriteBudgetUtilization(string actorRole)
    {
        if (actorRole is not (UserRoles.Director or UserRoles.Accountant))
        {
            throw new CaseForbiddenException();
        }
    }

    // Mirrors Policies.CoordinatorOrAbove — matches who can list/view utilization entries.
    private static void EnsureCanReadBudgetUtilization(string actorRole)
    {
        if (actorRole is not (UserRoles.Director or UserRoles.Accountant or UserRoles.Coordinator))
        {
            throw new CaseForbiddenException();
        }
    }

    public static string BuildBlobName(
        Guid organisationId,
        Guid caseNoteId,
        Guid attachmentId,
        string sanitizedFileName) =>
        $"{organisationId:D}/case-note/{caseNoteId:D}/{attachmentId:D}/{sanitizedFileName}";

    public static string BuildTravelClaimBlobName(
        Guid organisationId,
        Guid travelClaimId,
        Guid attachmentId,
        string sanitizedFileName) =>
        $"{organisationId:D}/travel-claim/{travelClaimId:D}/{attachmentId:D}/{sanitizedFileName}";

    public static string BuildBudgetUtilizationBlobName(
        Guid organisationId,
        Guid budgetUtilizationId,
        Guid attachmentId,
        string sanitizedFileName) =>
        $"{organisationId:D}/budget-utilization/{budgetUtilizationId:D}/{attachmentId:D}/{sanitizedFileName}";

    private async Task EnsureTravelClaimStillDraftAsync(
        Guid claimId,
        Guid organisationId,
        CancellationToken cancellationToken)
    {
        var status = await db.TravelClaims
            .AsNoTracking()
            .Where(c => c.Id == claimId && c.OrganisationId == organisationId)
            .Select(c => (TravelClaimStatus?)c.Status)
            .SingleOrDefaultAsync(cancellationToken);

        if (status is null)
        {
            throw new CaseNotFoundException();
        }

        if (status != TravelClaimStatus.Draft)
        {
            throw new CaseBusinessRuleException(
                "Receipts can only be added while the claim is in Draft status.");
        }
    }

    private static AttachmentDto MapToDto(Attachment attachment) => new()
    {
        Id = attachment.Id,
        ResourceType = attachment.ResourceType.ToString(),
        ResourceId = attachment.ResourceId,
        OriginalFileName = attachment.OriginalFileName,
        ContentType = attachment.ContentType,
        FileSizeBytes = attachment.FileSizeBytes,
        Status = attachment.Status.ToString(),
        UploadedByUserId = attachment.UploadedByUserId,
        CreatedAtUtc = attachment.CreatedAtUtc,
        ConfirmedAtUtc = attachment.ConfirmedAtUtc,
    };

    private static AttachmentResourceType ParseResourceType(string? value)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            throw new CaseValidationException("resourceType is required. Allowed values: CaseNote, TravelClaim.");
        }

        var trimmed = value.Trim();
        if (!AllowedResourceTypeNames.Contains(trimmed, StringComparer.Ordinal))
        {
            throw new CaseValidationException("resourceType is invalid. Allowed values: CaseNote, TravelClaim.");
        }

        return Enum.Parse<AttachmentResourceType>(trimmed, ignoreCase: false);
    }

    private static string ValidateFileName(string? value)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            throw new CaseValidationException("fileName is required.");
        }

        var trimmed = value.Trim();
        if (trimmed.Contains('/') || trimmed.Contains('\\') || trimmed.Contains("..", StringComparison.Ordinal))
        {
            throw new CaseValidationException("fileName must not contain path separators or '..'.");
        }

        var baseName = Path.GetFileName(trimmed);
        if (string.IsNullOrWhiteSpace(baseName) || baseName.Contains("..", StringComparison.Ordinal))
        {
            throw new CaseValidationException("fileName is invalid.");
        }

        if (baseName.Length > 255)
        {
            throw new CaseValidationException("fileName must be at most 255 characters.");
        }

        return baseName;
    }

    private static string ValidateContentType(string? value)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            throw new CaseValidationException("contentType is required.");
        }

        var trimmed = value.Trim();
        if (!AllowedContentTypes.Contains(trimmed, StringComparer.OrdinalIgnoreCase))
        {
            throw new CaseValidationException(
                "contentType is invalid. Allowed values: image/jpeg, image/png, image/webp, application/pdf.");
        }

        return trimmed;
    }

    private void ValidateFileSizeBytes(long fileSizeBytes)
    {
        if (fileSizeBytes <= 0)
        {
            throw new CaseValidationException("The uploaded file is empty.");
        }

        if (fileSizeBytes > storageOptions.Value.MaxUploadBytes)
        {
            throw new CaseValidationException(
                $"File exceeds the {storageOptions.Value.MaxUploadBytes} byte upload limit.");
        }
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

public sealed class AttachmentNotFoundException : Exception;
