using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using MidiKaval.Api.Domain.Entities;
using MidiKaval.Api.Domain.Enums;
using MidiKaval.Api.Infrastructure.Audit;
using MidiKaval.Api.Infrastructure.Auth;
using MidiKaval.Api.Infrastructure.Persistence;
using MidiKaval.Api.Models.Attachments;
using MidiKaval.Api.Models.Cases;

namespace MidiKaval.Api.Infrastructure.Cases;

public sealed class CaseNoteService(AppDbContext db, IHttpContextAccessor httpContextAccessor)
{
    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web);

    public async Task<CaseNoteDto> CreateAsync(
        Guid caseId,
        CreateCaseNoteRequest request,
        CancellationToken cancellationToken = default)
    {
        if (request is null)
        {
            throw new CaseValidationException("Request body is required.");
        }

        var (organisationId, actorUserId) = ResolveActorContext();
        var actorRole = ResolveActorRole();

        var caseEntity = await db.Cases.SingleOrDefaultAsync(
            c => c.Id == caseId && c.OrganisationId == organisationId,
            cancellationToken);

        if (caseEntity is null)
        {
            throw new CaseNotFoundException();
        }

        EnsureCanReadCase(caseEntity, actorUserId, actorRole);

        var noteType = ParseNoteType(request.NoteType);
        var bodyText = ValidateBodyText(request.BodyText);
        var (actionRequired, actionDueAtUtc) = ValidateActionFields(request.ActionRequired, request.ActionDueAtUtc);

        var now = DateTime.UtcNow;
        var noteId = Guid.NewGuid();

        var note = new CaseNote
        {
            Id = noteId,
            OrganisationId = organisationId,
            CaseId = caseId,
            AuthorUserId = actorUserId,
            NoteType = noteType,
            BodyText = bodyText,
            ActionRequired = actionRequired,
            ActionDueAtUtc = actionDueAtUtc,
            CreatedAtUtc = now,
        };

        db.CaseNotes.Add(note);
        db.AuditEvents.Add(new AuditEvent
        {
            Id = Guid.NewGuid(),
            OrganisationId = organisationId,
            ActorUserId = actorUserId,
            SubjectUserId = actorUserId,
            EventType = AuditEventTypes.CaseNoteCreated,
            MetadataJson = JsonSerializer.Serialize(
                new Dictionary<string, object?>
                {
                    ["caseId"] = caseId.ToString("D"),
                    ["noteId"] = noteId.ToString("D"),
                    ["noteType"] = noteType.ToString(),
                },
                JsonOptions),
            CreatedAtUtc = now,
        });

        await db.SaveChangesAsync(cancellationToken);

        var authorEmail = await ResolveAuthorEmailAsync(actorUserId, organisationId, cancellationToken);
        return MapToDto(note, authorEmail, Array.Empty<AttachmentSummaryDto>());
    }

    public async Task<CaseNoteListResultDto> ListAsync(
        Guid caseId,
        CancellationToken cancellationToken = default)
    {
        var (organisationId, actorUserId) = ResolveActorContext();
        var actorRole = ResolveActorRole();

        var caseEntity = await db.Cases.SingleOrDefaultAsync(
            c => c.Id == caseId && c.OrganisationId == organisationId,
            cancellationToken);

        if (caseEntity is null)
        {
            throw new CaseNotFoundException();
        }

        EnsureCanReadCase(caseEntity, actorUserId, actorRole);

        var rows = await (
            from note in db.CaseNotes
            join user in db.Users on note.AuthorUserId equals user.Id into users
            from author in users.DefaultIfEmpty()
            where note.OrganisationId == organisationId && note.CaseId == caseId
            orderby note.CreatedAtUtc, note.Id
            select new
            {
                Note = note,
                AuthorEmail = author != null
                    && author.OrganisationId == organisationId
                    && author.IsActive
                    ? author.Email
                    : null,
            }).ToListAsync(cancellationToken);

        var noteIds = rows.Select(row => row.Note.Id).ToList();
        var attachmentRows = noteIds.Count == 0
            ? []
            : await db.Attachments
                .Where(a => a.OrganisationId == organisationId
                    && a.ResourceType == AttachmentResourceType.CaseNote
                    && noteIds.Contains(a.ResourceId)
                    && a.Status == AttachmentStatus.Confirmed)
                .OrderBy(a => a.ConfirmedAtUtc)
                .ThenBy(a => a.Id)
                .ToListAsync(cancellationToken);

        var attachmentsByNoteId = attachmentRows
            .GroupBy(a => a.ResourceId)
            .ToDictionary(
                g => g.Key,
                g => (IReadOnlyList<AttachmentSummaryDto>)g
                    .Select(a => new AttachmentSummaryDto
                    {
                        Id = a.Id,
                        OriginalFileName = a.OriginalFileName,
                        ContentType = a.ContentType,
                        FileSizeBytes = a.FileSizeBytes,
                        ConfirmedAtUtc = a.ConfirmedAtUtc!.Value,
                    })
                    .ToList());

        var items = rows
            .Select(row =>
            {
                attachmentsByNoteId.TryGetValue(row.Note.Id, out var attachments);
                return MapToDto(row.Note, row.AuthorEmail, attachments);
            })
            .ToList();

        return new CaseNoteListResultDto { Items = items };
    }

    private async Task<string?> ResolveAuthorEmailAsync(
        Guid authorUserId,
        Guid organisationId,
        CancellationToken cancellationToken)
    {
        return await db.Users
            .Where(u => u.Id == authorUserId && u.OrganisationId == organisationId)
            .Select(u => u.Email)
            .SingleOrDefaultAsync(cancellationToken);
    }

    private static CaseNoteDto MapToDto(
        CaseNote note,
        string? authorEmail,
        IReadOnlyList<AttachmentSummaryDto>? attachments = null) =>
        new()
        {
            Id = note.Id,
            CaseId = note.CaseId,
            NoteType = note.NoteType.ToString(),
            BodyText = note.BodyText,
            ActionRequired = note.ActionRequired,
            ActionDueAtUtc = note.ActionDueAtUtc,
            AuthorUserId = note.AuthorUserId,
            AuthorEmail = authorEmail,
            CreatedAtUtc = note.CreatedAtUtc,
            Attachments = attachments ?? Array.Empty<AttachmentSummaryDto>(),
        };

    private static readonly string[] AllowedNoteTypeNames =
        ["Visit", "Court", "Intervention", "General"];

    private static CaseNoteType ParseNoteType(string? value)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            throw new CaseValidationException(
                "noteType is required. Allowed values: Visit, Court, Intervention, General.");
        }

        var trimmed = value.Trim();
        if (!AllowedNoteTypeNames.Contains(trimmed, StringComparer.Ordinal))
        {
            throw new CaseValidationException(
                "noteType is invalid. Allowed values: Visit, Court, Intervention, General.");
        }

        return Enum.Parse<CaseNoteType>(trimmed, ignoreCase: false);
    }

    private static string ValidateBodyText(string? value)
    {
        var trimmed = value?.Trim() ?? string.Empty;
        if (trimmed.Length == 0)
        {
            throw new CaseValidationException("bodyText is required.");
        }

        if (trimmed.Length > 4000)
        {
            throw new CaseValidationException("bodyText must be at most 4000 characters.");
        }

        return trimmed;
    }

    private static (bool ActionRequired, DateTime? ActionDueAtUtc) ValidateActionFields(
        bool actionRequired,
        DateTime? actionDueAtUtc)
    {
        if (actionDueAtUtc is not null)
        {
            if (actionDueAtUtc.Value.Kind == DateTimeKind.Unspecified)
            {
                throw new CaseValidationException("actionDueAtUtc must be a valid UTC timestamp.");
            }

            var dueUtc = actionDueAtUtc.Value.Kind == DateTimeKind.Utc
                ? actionDueAtUtc.Value
                : actionDueAtUtc.Value.ToUniversalTime();

            if (dueUtc < DateTime.UtcNow)
            {
                throw new CaseBusinessRuleException("Action due date must be in the future.");
            }

            return (true, dueUtc);
        }

        if (actionRequired)
        {
            throw new CaseValidationException("actionDueAtUtc is required when actionRequired is true.");
        }

        return (false, null);
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
