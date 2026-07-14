using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using MidiKaval.Api.Domain.Entities;
using MidiKaval.Api.Domain.Enums;
using MidiKaval.Api.Infrastructure.Audit;
using MidiKaval.Api.Infrastructure.Auth;
using MidiKaval.Api.Infrastructure.Cases;
using MidiKaval.Api.Infrastructure.Persistence;
using MidiKaval.Api.Infrastructure.Sync;
using MidiKaval.Api.Models.Cases;
using MidiKaval.Api.Models.Visits;

namespace MidiKaval.Api.Infrastructure.Visits;

public sealed class VisitService(AppDbContext db, IHttpContextAccessor httpContextAccessor)
{
    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web);
    private static readonly VisitStatus[] ActiveStatuses = [VisitStatus.Scheduled, VisitStatus.InProgress];
    private static readonly VisitStatus[] WeeklyStatuses =
        [VisitStatus.Scheduled, VisitStatus.InProgress, VisitStatus.Completed];

    // Field workers experience "today" and "this week" in IST, not UTC. Using
    // DateTime.UtcNow.Date directly means every day between midnight and 5:30 AM IST,
    // the UTC calendar day is still "yesterday" — silently hiding visits that are
    // genuinely scheduled for today (IST) from the field worker's Today list.
    private static readonly TimeSpan IstOffset = TimeSpan.FromMinutes(330);

    public Task<(VisitListResultDto Result, int TotalCount)> ListTodayAsync(CancellationToken cancellationToken = default)
    {
        var (today, tomorrow) = GetIstDayBoundsUtc(DateTime.UtcNow);
        return ListForFieldWorkerAsync(
            (visit, _) => visit.ScheduledAtUtc >= today
                && visit.ScheduledAtUtc < tomorrow
                && ActiveStatuses.Contains(visit.Status),
            includeHandoff: true,
            cancellationToken);
    }

    public Task<(VisitListResultDto Result, int TotalCount)> ListWeeklyAsync(CancellationToken cancellationToken = default)
    {
        var (weekStart, weekEnd) = GetUtcWeekBounds(DateTime.UtcNow);
        return ListForFieldWorkerAsync(
            (visit, _) => visit.ScheduledAtUtc >= weekStart
                && visit.ScheduledAtUtc <= weekEnd
                && WeeklyStatuses.Contains(visit.Status),
            includeHandoff: true,
            includeCompletionNotesForCompleted: true,
            cancellationToken);
    }

    private static (DateTime StartUtc, DateTime EndUtc) GetIstDayBoundsUtc(DateTime utcNow)
    {
        var istNow = utcNow + IstOffset;
        var istTodayStart = istNow.Date;
        return (istTodayStart - IstOffset, istTodayStart.AddDays(1) - IstOffset);
    }

    public Task<(VisitListResultDto Result, int TotalCount)> ListOverdueAsync(CancellationToken cancellationToken = default)
    {
        var now = DateTime.UtcNow;
        return ListForFieldWorkerAsync(
            (visit, _) => visit.ScheduledAtUtc < now && ActiveStatuses.Contains(visit.Status),
            includeHandoff: true,
            cancellationToken);
    }

    public async Task<VisitGroupingSuggestionDto> GetTodayGroupingSuggestionAsync(
        CancellationToken cancellationToken = default)
    {
        var (result, _) = await ListTodayAsync(cancellationToken);
        return BuildGroupingSuggestion(result.Items);
    }

    public async Task<(VisitListResultDto Result, int TotalCount)> ListForCaseAsync(
        Guid caseId,
        CancellationToken cancellationToken = default)
    {
        var organisationId = ResolveOrganisationId();

        var caseEntity = await db.Cases
            .Include(c => c.Occupation)
            .Include(c => c.EducationLevel)
            .SingleOrDefaultAsync(
            c => c.Id == caseId && c.OrganisationId == organisationId,
            cancellationToken);

        if (caseEntity is null)
        {
            throw new VisitNotFoundException();
        }

        var rows = await LoadVisitCaseRowsAsync(
            v => v.OrganisationId == organisationId && v.CaseId == caseId,
            cancellationToken);

        var items = new List<VisitListItemDto>();
        var completionNotes = await LoadCompletionNotesAsync(
            rows.Select(r => r.Visit),
            organisationId,
            cancellationToken);

        foreach (var row in rows)
        {
            completionNotes.TryGetValue(row.Visit.Id, out var note);
            items.Add(await ToListItemAsync(
                row.Visit,
                row.Case,
                includeHandoff: false,
                completionNote: note,
                redactPocsoForFieldWorker: false,
                cancellationToken));
        }

        items.Sort((a, b) => a.ScheduledAtUtc.CompareTo(b.ScheduledAtUtc));

        return (BuildResult(items), items.Count);
    }

    public async Task<VisitListItemDto> ScheduleAsync(
        Guid caseId,
        ScheduleVisitRequest request,
        CancellationToken cancellationToken = default)
    {
        if (request is null)
        {
            throw new VisitValidationException("Request body is required.");
        }

        if (request.ScheduledAtUtc is null)
        {
            throw new VisitValidationException("scheduledAtUtc is required.");
        }

        var scheduledAtUtc = request.ScheduledAtUtc.Value;
        if (scheduledAtUtc.Kind == DateTimeKind.Unspecified)
        {
            scheduledAtUtc = DateTime.SpecifyKind(scheduledAtUtc, DateTimeKind.Utc);
        }
        else if (scheduledAtUtc.Kind == DateTimeKind.Local)
        {
            scheduledAtUtc = scheduledAtUtc.ToUniversalTime();
        }

        if (scheduledAtUtc < DateTime.UtcNow)
        {
            throw new VisitValidationException("scheduledAtUtc must be in the future.");
        }

        var (organisationId, actorUserId) = ResolveActorContext();

        var caseEntity = await db.Cases
            .Include(c => c.Occupation)
            .Include(c => c.EducationLevel)
            .SingleOrDefaultAsync(
            c => c.Id == caseId && c.OrganisationId == organisationId,
            cancellationToken);

        if (caseEntity is null)
        {
            throw new VisitNotFoundException();
        }

        if (caseEntity.CurrentStage == CaseStage.TerminationExclusion)
        {
            throw new VisitBusinessRuleException("Cannot schedule visits for a case at TerminationExclusion.");
        }

        var assigneeUserId = request.AssigneeUserId ?? caseEntity.AssignedWorkerId;
        if (assigneeUserId is null)
        {
            throw new VisitBusinessRuleException("Case has no assigned field worker; provide assigneeUserId.");
        }

        await EnsureFieldWorkerAsync(assigneeUserId.Value, organisationId, cancellationToken);

        var hasActiveVisit = await db.Visits.AnyAsync(
            v => v.CaseId == caseId
                && v.OrganisationId == organisationId
                && ActiveStatuses.Contains(v.Status),
            cancellationToken);

        if (hasActiveVisit)
        {
            throw new VisitBusinessRuleException("Case already has an active scheduled visit.");
        }

        var now = DateTime.UtcNow;
        var visitId = Guid.NewGuid();

        var visit = new Visit
        {
            Id = visitId,
            OrganisationId = organisationId,
            CaseId = caseId,
            AssigneeUserId = assigneeUserId.Value,
            ScheduledAtUtc = scheduledAtUtc,
            Status = VisitStatus.Scheduled,
            CreatedAtUtc = now,
            UpdatedAtUtc = now,
        };

        caseEntity.NextVisitDueAtUtc = scheduledAtUtc;
        caseEntity.UpdatedAtUtc = now;

        db.Visits.Add(visit);
        db.AuditEvents.Add(new AuditEvent
        {
            Id = Guid.NewGuid(),
            OrganisationId = organisationId,
            ActorUserId = actorUserId,
            SubjectUserId = assigneeUserId,
            EventType = AuditEventTypes.VisitScheduled,
            MetadataJson = JsonSerializer.Serialize(
                new Dictionary<string, object?>
                {
                    ["visitId"] = visitId.ToString("D"),
                    ["caseId"] = caseId.ToString("D"),
                    ["scheduledAtUtc"] = scheduledAtUtc.ToString("O"),
                },
                JsonOptions),
            CreatedAtUtc = now,
        });

        await db.SaveChangesAsync(cancellationToken);

        return await ToListItemAsync(
            visit,
            caseEntity,
            includeHandoff: false,
            completionNote: null,
            redactPocsoForFieldWorker: false,
            cancellationToken);
    }

    public async Task<VisitListItemDto> CancelAsync(
        Guid caseId,
        Guid visitId,
        CancelVisitRequest request,
        CancellationToken cancellationToken = default)
    {
        if (request is null)
        {
            throw new VisitValidationException("Request body is required.");
        }

        var reason = request.Reason?.Trim() ?? string.Empty;
        if (reason.Length == 0)
        {
            throw new VisitValidationException("reason is required.");
        }

        if (reason.Length > 500)
        {
            throw new VisitValidationException("reason must be at most 500 characters.");
        }

        var (organisationId, actorUserId) = ResolveActorContext();
        var row = await LoadVisitCaseRowAsync(visitId, organisationId, cancellationToken);

        if (row is null || row.Case.Id != caseId)
        {
            throw new VisitNotFoundException();
        }

        if (row.Visit.Status == VisitStatus.Completed)
        {
            throw new VisitBusinessRuleException("Completed visits cannot be cancelled.");
        }

        if (row.Visit.Status == VisitStatus.Cancelled)
        {
            throw new VisitBusinessRuleException("Visit is already cancelled.");
        }

        var now = DateTime.UtcNow;
        row.Visit.Status = VisitStatus.Cancelled;
        row.Visit.CancellationReason = reason;
        row.Visit.CancelledAtUtc = now;
        row.Visit.CancelledByUserId = actorUserId;
        row.Visit.UpdatedAtUtc = now;

        if (row.Case.NextVisitDueAtUtc == row.Visit.ScheduledAtUtc)
        {
            row.Case.NextVisitDueAtUtc = null;
        }

        row.Case.UpdatedAtUtc = now;

        db.AuditEvents.Add(new AuditEvent
        {
            Id = Guid.NewGuid(),
            OrganisationId = organisationId,
            ActorUserId = actorUserId,
            SubjectUserId = row.Visit.AssigneeUserId,
            EventType = AuditEventTypes.VisitCancelled,
            MetadataJson = JsonSerializer.Serialize(
                new Dictionary<string, object?>
                {
                    ["visitId"] = visitId.ToString("D"),
                    ["caseId"] = caseId.ToString("D"),
                },
                JsonOptions),
            CreatedAtUtc = now,
        });

        await db.SaveChangesAsync(cancellationToken);

        return await ToListItemAsync(
            row.Visit,
            row.Case,
            includeHandoff: false,
            completionNote: null,
            redactPocsoForFieldWorker: false,
            cancellationToken);
    }

    public async Task<VisitPlaceDto> AddPlaceAsync(
        Guid caseId,
        Guid visitId,
        AddVisitPlaceRequest request,
        CancellationToken cancellationToken = default)
    {
        if (request is null)
        {
            throw new VisitValidationException("Request body is required.");
        }

        var address = request.Address?.Trim() ?? string.Empty;
        if (address.Length == 0)
        {
            throw new VisitValidationException("address is required.");
        }

        if (address.Length > 500)
        {
            throw new VisitValidationException("address must be at most 500 characters.");
        }

        if (request.PlannedLatitude is < -90 or > 90)
        {
            throw new VisitValidationException("plannedLatitude must be between -90 and 90.");
        }

        if (request.PlannedLongitude is < -180 or > 180)
        {
            throw new VisitValidationException("plannedLongitude must be between -180 and 180.");
        }

        var (organisationId, actorUserId) = ResolveActorContext();
        var row = await LoadVisitCaseRowAsync(visitId, organisationId, cancellationToken);

        if (row is null || row.Case.Id != caseId)
        {
            throw new VisitNotFoundException();
        }

        if (row.Visit.Status is VisitStatus.Completed or VisitStatus.Cancelled)
        {
            throw new VisitBusinessRuleException("Cannot add places to a completed or cancelled visit.");
        }

        var now = DateTime.UtcNow;
        var place = new VisitPlace
        {
            Id = Guid.NewGuid(),
            OrganisationId = organisationId,
            VisitId = visitId,
            Address = address,
            OsmReference = request.OsmReference,
            PlannedLatitude = request.PlannedLatitude,
            PlannedLongitude = request.PlannedLongitude,
            CreatedByUserId = actorUserId,
            CreatedAtUtc = now,
        };

        db.VisitPlaces.Add(place);
        db.AuditEvents.Add(new AuditEvent
        {
            Id = Guid.NewGuid(),
            OrganisationId = organisationId,
            ActorUserId = actorUserId,
            SubjectUserId = row.Visit.AssigneeUserId,
            EventType = AuditEventTypes.VisitPlaceAdded,
            MetadataJson = JsonSerializer.Serialize(
                new Dictionary<string, object?>
                {
                    ["visitId"] = visitId.ToString("D"),
                    ["placeId"] = place.Id.ToString("D"),
                },
                JsonOptions),
            CreatedAtUtc = now,
        });

        await db.SaveChangesAsync(cancellationToken);

        return new VisitPlaceDto
        {
            Id = place.Id,
            VisitId = place.VisitId,
            Address = place.Address,
            OsmReference = place.OsmReference,
            PlannedLatitude = place.PlannedLatitude,
            PlannedLongitude = place.PlannedLongitude,
            CreatedAtUtc = place.CreatedAtUtc,
        };
    }

    public async Task<VisitPlaceDto> UpdatePlaceAsync(
        Guid caseId,
        Guid visitId,
        Guid placeId,
        UpdateVisitPlaceRequest request,
        CancellationToken cancellationToken = default)
    {
        if (request is null)
        {
            throw new VisitValidationException("Request body is required.");
        }

        var address = request.Address?.Trim() ?? string.Empty;
        if (address.Length == 0)
        {
            throw new VisitValidationException("address is required.");
        }

        if (address.Length > 500)
        {
            throw new VisitValidationException("address must be at most 500 characters.");
        }

        if (request.PlannedLatitude is < -90 or > 90)
        {
            throw new VisitValidationException("plannedLatitude must be between -90 and 90.");
        }

        if (request.PlannedLongitude is < -180 or > 180)
        {
            throw new VisitValidationException("plannedLongitude must be between -180 and 180.");
        }

        var (organisationId, actorUserId) = ResolveActorContext();
        var row = await LoadVisitCaseRowAsync(visitId, organisationId, cancellationToken);

        if (row is null || row.Case.Id != caseId)
        {
            throw new VisitNotFoundException();
        }

        if (row.Visit.Status is VisitStatus.Completed or VisitStatus.Cancelled)
        {
            throw new VisitBusinessRuleException("Cannot edit places on a completed or cancelled visit.");
        }

        var place = await db.VisitPlaces.SingleOrDefaultAsync(
            p => p.Id == placeId && p.VisitId == visitId && p.OrganisationId == organisationId,
            cancellationToken);

        if (place is null || place.RemovedAtUtc is not null)
        {
            throw new VisitNotFoundException();
        }

        if (place.LoggedAtUtc is not null)
        {
            throw new VisitBusinessRuleException("Cannot edit a place that has already been logged.");
        }

        place.Address = address;
        place.OsmReference = request.OsmReference;
        place.PlannedLatitude = request.PlannedLatitude;
        place.PlannedLongitude = request.PlannedLongitude;

        var now = DateTime.UtcNow;
        db.AuditEvents.Add(new AuditEvent
        {
            Id = Guid.NewGuid(),
            OrganisationId = organisationId,
            ActorUserId = actorUserId,
            SubjectUserId = row.Visit.AssigneeUserId,
            EventType = AuditEventTypes.VisitPlaceUpdated,
            MetadataJson = JsonSerializer.Serialize(
                new Dictionary<string, object?>
                {
                    ["visitId"] = visitId.ToString("D"),
                    ["placeId"] = place.Id.ToString("D"),
                },
                JsonOptions),
            CreatedAtUtc = now,
        });

        await db.SaveChangesAsync(cancellationToken);

        return new VisitPlaceDto
        {
            Id = place.Id,
            VisitId = place.VisitId,
            Address = place.Address,
            OsmReference = place.OsmReference,
            PlannedLatitude = place.PlannedLatitude,
            PlannedLongitude = place.PlannedLongitude,
            CreatedAtUtc = place.CreatedAtUtc,
            Comment = place.Comment,
            CommentUpdatedAtUtc = place.CommentUpdatedAtUtc,
        };
    }

    public async Task RemovePlaceAsync(
        Guid caseId,
        Guid visitId,
        Guid placeId,
        CancellationToken cancellationToken = default)
    {
        var (organisationId, actorUserId) = ResolveActorContext();
        var row = await LoadVisitCaseRowAsync(visitId, organisationId, cancellationToken);

        if (row is null || row.Case.Id != caseId)
        {
            throw new VisitNotFoundException();
        }

        if (row.Visit.Status is VisitStatus.Completed or VisitStatus.Cancelled)
        {
            throw new VisitBusinessRuleException("Cannot remove places from a completed or cancelled visit.");
        }

        var place = await db.VisitPlaces.SingleOrDefaultAsync(
            p => p.Id == placeId && p.VisitId == visitId && p.OrganisationId == organisationId,
            cancellationToken);

        if (place is null || place.RemovedAtUtc is not null)
        {
            throw new VisitNotFoundException();
        }

        if (place.LoggedAtUtc is not null)
        {
            throw new VisitBusinessRuleException("Cannot remove a place that has already been logged.");
        }

        var now = DateTime.UtcNow;
        place.RemovedAtUtc = now;
        place.RemovedByUserId = actorUserId;

        db.AuditEvents.Add(new AuditEvent
        {
            Id = Guid.NewGuid(),
            OrganisationId = organisationId,
            ActorUserId = actorUserId,
            SubjectUserId = row.Visit.AssigneeUserId,
            EventType = AuditEventTypes.VisitPlaceRemoved,
            MetadataJson = JsonSerializer.Serialize(
                new Dictionary<string, object?>
                {
                    ["visitId"] = visitId.ToString("D"),
                    ["placeId"] = place.Id.ToString("D"),
                },
                JsonOptions),
            CreatedAtUtc = now,
        });

        await db.SaveChangesAsync(cancellationToken);
    }

    public async Task<VisitPlaceDto> UpdatePlaceCommentAsync(
        Guid visitId,
        Guid placeId,
        UpdateVisitPlaceCommentRequest request,
        CancellationToken cancellationToken = default)
    {
        if (request is null)
        {
            throw new VisitValidationException("Request body is required.");
        }

        var comment = request.Comment?.Trim() ?? string.Empty;
        if (comment.Length > 1000)
        {
            throw new VisitValidationException("comment must be at most 1000 characters.");
        }

        var (organisationId, actorUserId) = ResolveActorContext();
        var row = await LoadVisitCaseRowAsync(visitId, organisationId, cancellationToken);

        if (row is null)
        {
            throw new VisitNotFoundException();
        }

        if (row.Visit.AssigneeUserId != actorUserId)
        {
            throw new VisitForbiddenException();
        }

        var place = await db.VisitPlaces.SingleOrDefaultAsync(
            p => p.Id == placeId && p.VisitId == visitId && p.OrganisationId == organisationId,
            cancellationToken);

        if (place is null || place.RemovedAtUtc is not null)
        {
            throw new VisitNotFoundException();
        }

        var now = DateTime.UtcNow;
        place.Comment = comment.Length == 0 ? null : comment;
        place.CommentUpdatedAtUtc = now;
        place.CommentUpdatedByUserId = actorUserId;

        db.AuditEvents.Add(new AuditEvent
        {
            Id = Guid.NewGuid(),
            OrganisationId = organisationId,
            ActorUserId = actorUserId,
            SubjectUserId = actorUserId,
            EventType = AuditEventTypes.VisitPlaceCommentUpdated,
            MetadataJson = JsonSerializer.Serialize(
                new Dictionary<string, object?>
                {
                    ["visitId"] = visitId.ToString("D"),
                    ["placeId"] = placeId.ToString("D"),
                },
                JsonOptions),
            CreatedAtUtc = now,
        });

        await db.SaveChangesAsync(cancellationToken);

        var loggedByEmail = place.LoggedByUserId.HasValue
            ? await db.Users
                .Where(u => u.Id == place.LoggedByUserId)
                .Select(u => u.Email)
                .SingleOrDefaultAsync(cancellationToken)
            : null;

        return new VisitPlaceDto
        {
            Id = place.Id,
            VisitId = place.VisitId,
            Address = place.Address,
            OsmReference = place.OsmReference,
            PlannedLatitude = place.PlannedLatitude,
            PlannedLongitude = place.PlannedLongitude,
            CreatedAtUtc = place.CreatedAtUtc,
            LoggedLatitude = place.LoggedLatitude,
            LoggedLongitude = place.LoggedLongitude,
            LoggedAtUtc = place.LoggedAtUtc,
            LoggedByEmail = loggedByEmail,
            Comment = place.Comment,
            CommentUpdatedAtUtc = place.CommentUpdatedAtUtc,
        };
    }

    public async Task<VisitPlaceDto> LogPlaceAsync(
        Guid visitId,
        Guid placeId,
        LogVisitPlaceRequest request,
        CancellationToken cancellationToken = default)
    {
        if (request is null)
        {
            throw new VisitValidationException("Request body is required.");
        }

        if (request.Latitude is null || request.Longitude is null)
        {
            throw new VisitValidationException("latitude and longitude are required.");
        }

        if (request.Latitude is < -90 or > 90)
        {
            throw new VisitValidationException("latitude must be between -90 and 90.");
        }

        if (request.Longitude is < -180 or > 180)
        {
            throw new VisitValidationException("longitude must be between -180 and 180.");
        }

        var (organisationId, actorUserId) = ResolveActorContext();
        var row = await LoadVisitCaseRowAsync(visitId, organisationId, cancellationToken);

        if (row is null)
        {
            throw new VisitNotFoundException();
        }

        if (row.Visit.AssigneeUserId != actorUserId)
        {
            throw new VisitForbiddenException();
        }

        var place = await db.VisitPlaces.SingleOrDefaultAsync(
            p => p.Id == placeId && p.VisitId == visitId && p.OrganisationId == organisationId,
            cancellationToken);

        if (place is null || place.RemovedAtUtc is not null)
        {
            throw new VisitNotFoundException();
        }

        if (place.LoggedAtUtc is not null)
        {
            throw new VisitBusinessRuleException("This place has already been logged.");
        }

        // Server-assigned timestamp — deliberately not trusting any client-supplied value,
        // since the device clock can't be relied on for an audit-quality arrival record.
        var now = DateTime.UtcNow;
        place.LoggedLatitude = request.Latitude;
        place.LoggedLongitude = request.Longitude;
        place.LoggedAtUtc = now;
        place.LoggedByUserId = actorUserId;

        db.AuditEvents.Add(new AuditEvent
        {
            Id = Guid.NewGuid(),
            OrganisationId = organisationId,
            ActorUserId = actorUserId,
            SubjectUserId = actorUserId,
            EventType = AuditEventTypes.VisitPlaceLogged,
            MetadataJson = JsonSerializer.Serialize(
                new Dictionary<string, object?>
                {
                    ["visitId"] = visitId.ToString("D"),
                    ["placeId"] = placeId.ToString("D"),
                },
                JsonOptions),
            CreatedAtUtc = now,
        });

        await db.SaveChangesAsync(cancellationToken);

        var loggedByEmail = await db.Users
            .Where(u => u.Id == actorUserId)
            .Select(u => u.Email)
            .SingleOrDefaultAsync(cancellationToken);

        return new VisitPlaceDto
        {
            Id = place.Id,
            VisitId = place.VisitId,
            Address = place.Address,
            OsmReference = place.OsmReference,
            PlannedLatitude = place.PlannedLatitude,
            PlannedLongitude = place.PlannedLongitude,
            CreatedAtUtc = place.CreatedAtUtc,
            LoggedLatitude = place.LoggedLatitude,
            LoggedLongitude = place.LoggedLongitude,
            LoggedAtUtc = place.LoggedAtUtc,
            LoggedByEmail = loggedByEmail,
            Comment = place.Comment,
            CommentUpdatedAtUtc = place.CommentUpdatedAtUtc,
        };
    }

    public async Task<VisitListItemDto> StartAsync(Guid visitId, CancellationToken cancellationToken = default)
    {
        var (organisationId, actorUserId) = ResolveActorContext();
        var row = await LoadVisitCaseRowAsync(visitId, organisationId, cancellationToken);

        if (row is null)
        {
            throw new VisitNotFoundException();
        }

        if (row.Visit.AssigneeUserId != actorUserId)
        {
            throw new VisitForbiddenException();
        }

        if (row.Visit.Status == VisitStatus.InProgress)
        {
            throw new VisitBusinessRuleException("Visit is already in progress.");
        }

        if (row.Visit.Status == VisitStatus.Completed)
        {
            throw new VisitBusinessRuleException("Visit is already completed.");
        }

        if (row.Visit.Status != VisitStatus.Scheduled)
        {
            throw new VisitBusinessRuleException("Visit cannot be started in its current status.");
        }

        var now = DateTime.UtcNow;
        row.Visit.Status = VisitStatus.InProgress;
        row.Visit.StartedAtUtc = now;
        row.Visit.UpdatedAtUtc = now;

        db.AuditEvents.Add(new AuditEvent
        {
            Id = Guid.NewGuid(),
            OrganisationId = organisationId,
            ActorUserId = actorUserId,
            SubjectUserId = actorUserId,
            EventType = AuditEventTypes.VisitStarted,
            MetadataJson = JsonSerializer.Serialize(
                new Dictionary<string, object?>
                {
                    ["visitId"] = visitId.ToString("D"),
                    ["caseId"] = row.Case.Id.ToString("D"),
                },
                JsonOptions),
            CreatedAtUtc = now,
        });

        await db.SaveChangesAsync(cancellationToken);

        return await ToListItemAsync(
            row.Visit,
            row.Case,
            includeHandoff: true,
            completionNote: null,
            redactPocsoForFieldWorker: true,
            cancellationToken);
    }

    public async Task<VisitListItemDto> CompleteAsync(
        Guid visitId,
        CompleteVisitRequest? request,
        CancellationToken cancellationToken = default)
    {
        if (request is null)
        {
            throw new VisitValidationException("Request body is required.");
        }

        var note = request.Note?.Trim() ?? string.Empty;
        if (note.Length == 0)
        {
            throw new VisitValidationException("note is required.");
        }

        if (note.Length > 4000)
        {
            throw new VisitValidationException("note must be at most 4000 characters.");
        }

        var (organisationId, actorUserId) = ResolveActorContext();
        var row = await LoadVisitCaseRowAsync(visitId, organisationId, cancellationToken);

        if (row is null)
        {
            throw new VisitNotFoundException();
        }

        if (row.Visit.AssigneeUserId != actorUserId)
        {
            throw new VisitForbiddenException();
        }

        if (row.Visit.Status == VisitStatus.Completed)
        {
            throw new VisitBusinessRuleException("Visit is already completed.");
        }

        if (!ActiveStatuses.Contains(row.Visit.Status))
        {
            throw new VisitBusinessRuleException("Visit cannot be completed in its current status.");
        }

        var now = DateTime.UtcNow;
        row.Visit.Status = VisitStatus.Completed;
        row.Visit.CompletedAtUtc = now;
        row.Visit.UpdatedAtUtc = now;
        row.Case.VisitCount += 1;
        row.Case.NextVisitDueAtUtc = null;
        row.Case.UpdatedAtUtc = now;

        db.VisitNotes.Add(new VisitNote
        {
            Id = Guid.NewGuid(),
            OrganisationId = organisationId,
            VisitId = visitId,
            CaseId = row.Case.Id,
            AuthorUserId = actorUserId,
            BodyText = note,
            CreatedAtUtc = now,
        });

        db.AuditEvents.Add(new AuditEvent
        {
            Id = Guid.NewGuid(),
            OrganisationId = organisationId,
            ActorUserId = actorUserId,
            SubjectUserId = actorUserId,
            EventType = AuditEventTypes.VisitCompleted,
            MetadataJson = JsonSerializer.Serialize(
                new Dictionary<string, object?>
                {
                    ["visitId"] = visitId.ToString("D"),
                    ["caseId"] = row.Case.Id.ToString("D"),
                },
                JsonOptions),
            CreatedAtUtc = now,
        });

        await db.SaveChangesAsync(cancellationToken);

        return await ToListItemAsync(
            row.Visit,
            row.Case,
            includeHandoff: true,
            completionNote: note,
            redactPocsoForFieldWorker: true,
            cancellationToken);
    }

    public async Task<VisitSyncOutcome> ApplyVisitStartForSync(
        Guid visitId,
        CancellationToken cancellationToken = default)
    {
        var (organisationId, actorUserId) = ResolveActorContext();
        var row = await LoadVisitCaseRowAsync(visitId, organisationId, cancellationToken);

        if (row is null)
        {
            return VisitSyncOutcome.Rejected("Visit not found.");
        }

        if (row.Visit.AssigneeUserId != actorUserId)
        {
            return VisitSyncOutcome.Rejected("Visit is not assigned to you.");
        }

        if (row.Visit.Status == VisitStatus.Scheduled)
        {
            var now = DateTime.UtcNow;
            row.Visit.Status = VisitStatus.InProgress;
            row.Visit.StartedAtUtc = now;
            row.Visit.UpdatedAtUtc = now;

            db.AuditEvents.Add(new AuditEvent
            {
                Id = Guid.NewGuid(),
                OrganisationId = organisationId,
                ActorUserId = actorUserId,
                SubjectUserId = actorUserId,
                EventType = AuditEventTypes.VisitStarted,
                MetadataJson = JsonSerializer.Serialize(
                    new Dictionary<string, object?>
                    {
                        ["visitId"] = visitId.ToString("D"),
                        ["caseId"] = row.Case.Id.ToString("D"),
                        ["source"] = "sync.push",
                    },
                    JsonOptions),
                CreatedAtUtc = now,
            });

            await db.SaveChangesAsync(cancellationToken);
            var visit = await ToListItemAsync(
                row.Visit,
                row.Case,
                includeHandoff: true,
                completionNote: null,
                redactPocsoForFieldWorker: true,
                cancellationToken);
            return VisitSyncOutcome.Applied(visit, visitId);
        }

        if (row.Visit.Status == VisitStatus.InProgress)
        {
            return VisitSyncOutcome.Rejected("Visit is already in progress.");
        }

        if (row.Visit.Status == VisitStatus.Completed)
        {
            return VisitSyncOutcome.Rejected("Visit is already completed.");
        }

        return VisitSyncOutcome.Rejected("Visit cannot be started in its current status.");
    }

    public async Task<VisitSyncOutcome> ApplyVisitCompleteForSync(
        Guid visitId,
        string note,
        DateTime noteClientTimestampUtc,
        CancellationToken cancellationToken = default)
    {
        var trimmedNote = note?.Trim() ?? string.Empty;
        if (trimmedNote.Length == 0)
        {
            return VisitSyncOutcome.Rejected("note is required.");
        }

        if (trimmedNote.Length > 4000)
        {
            return VisitSyncOutcome.Rejected("note must be at most 4000 characters.");
        }

        var (organisationId, actorUserId) = ResolveActorContext();
        var row = await LoadVisitCaseRowAsync(visitId, organisationId, cancellationToken);

        if (row is null)
        {
            return VisitSyncOutcome.Rejected("Visit not found.");
        }

        if (row.Visit.AssigneeUserId != actorUserId)
        {
            return VisitSyncOutcome.Rejected("Visit is not assigned to you.");
        }

        if (row.Visit.Status == VisitStatus.Completed)
        {
            return await MergeVisitNoteForSyncAsync(
                row,
                organisationId,
                actorUserId,
                trimmedNote,
                noteClientTimestampUtc,
                cancellationToken);
        }

        if (!ActiveStatuses.Contains(row.Visit.Status))
        {
            return VisitSyncOutcome.Rejected("Visit cannot be completed in its current status.");
        }

        var now = DateTime.UtcNow;
        row.Visit.Status = VisitStatus.Completed;
        row.Visit.CompletedAtUtc = now;
        row.Visit.UpdatedAtUtc = now;
        row.Case.VisitCount += 1;
        row.Case.NextVisitDueAtUtc = null;
        row.Case.UpdatedAtUtc = now;

        db.VisitNotes.Add(new VisitNote
        {
            Id = Guid.NewGuid(),
            OrganisationId = organisationId,
            VisitId = visitId,
            CaseId = row.Case.Id,
            AuthorUserId = actorUserId,
            BodyText = trimmedNote,
            CreatedAtUtc = noteClientTimestampUtc.Kind == DateTimeKind.Unspecified
                ? DateTime.SpecifyKind(noteClientTimestampUtc, DateTimeKind.Utc)
                : noteClientTimestampUtc.ToUniversalTime(),
        });

        db.AuditEvents.Add(new AuditEvent
        {
            Id = Guid.NewGuid(),
            OrganisationId = organisationId,
            ActorUserId = actorUserId,
            SubjectUserId = actorUserId,
            EventType = AuditEventTypes.VisitCompleted,
            MetadataJson = JsonSerializer.Serialize(
                new Dictionary<string, object?>
                {
                    ["visitId"] = visitId.ToString("D"),
                    ["caseId"] = row.Case.Id.ToString("D"),
                    ["source"] = "sync.push",
                },
                JsonOptions),
            CreatedAtUtc = now,
        });

        await db.SaveChangesAsync(cancellationToken);
        var visitDto = await ToListItemAsync(
            row.Visit,
            row.Case,
            includeHandoff: true,
            completionNote: trimmedNote,
            redactPocsoForFieldWorker: true,
            cancellationToken);
        return VisitSyncOutcome.Applied(visitDto, visitId);
    }

    public async Task<VisitListItemDto?> GetAssignedVisitListItemAsync(
        Guid visitId,
        CancellationToken cancellationToken = default)
    {
        var (organisationId, actorUserId) = ResolveActorContext();
        var row = await LoadVisitCaseRowAsync(visitId, organisationId, cancellationToken);
        if (row is null || row.Visit.AssigneeUserId != actorUserId)
        {
            return null;
        }

        string? completionNote = null;
        if (row.Visit.Status == VisitStatus.Completed)
        {
            completionNote = await db.VisitNotes
                .Where(n => n.OrganisationId == organisationId && n.VisitId == visitId)
                .Select(n => n.BodyText)
                .SingleOrDefaultAsync(cancellationToken);
        }

        return await ToListItemAsync(
            row.Visit,
            row.Case,
            includeHandoff: true,
            completionNote,
            redactPocsoForFieldWorker: true,
            cancellationToken);
    }

    private async Task<VisitSyncOutcome> MergeVisitNoteForSyncAsync(
        VisitCaseRow row,
        Guid organisationId,
        Guid actorUserId,
        string note,
        DateTime noteClientTimestampUtc,
        CancellationToken cancellationToken)
    {
        var clientTimestamp = noteClientTimestampUtc.Kind == DateTimeKind.Unspecified
            ? DateTime.SpecifyKind(noteClientTimestampUtc, DateTimeKind.Utc)
            : noteClientTimestampUtc.ToUniversalTime();

        var existingNote = await db.VisitNotes.SingleOrDefaultAsync(
            n => n.OrganisationId == organisationId && n.VisitId == row.Visit.Id,
            cancellationToken);

        if (existingNote is null)
        {
            return VisitSyncOutcome.Rejected("Visit is already completed.");
        }

        if (!VisitSyncNoteMerge.ShouldMergeNote(existingNote.CreatedAtUtc, clientTimestamp))
        {
            return VisitSyncOutcome.Rejected("Visit is already completed.");
        }

        var now = DateTime.UtcNow;
        existingNote.BodyText = note;
        existingNote.CreatedAtUtc = clientTimestamp;

        db.AuditEvents.Add(new AuditEvent
        {
            Id = Guid.NewGuid(),
            OrganisationId = organisationId,
            ActorUserId = actorUserId,
            SubjectUserId = actorUserId,
            EventType = AuditEventTypes.VisitNoteMerged,
            MetadataJson = JsonSerializer.Serialize(
                new Dictionary<string, object?>
                {
                    ["visitId"] = row.Visit.Id.ToString("D"),
                    ["caseId"] = row.Case.Id.ToString("D"),
                    ["source"] = "sync.push",
                },
                JsonOptions),
            CreatedAtUtc = now,
        });

        await db.SaveChangesAsync(cancellationToken);
        var visitDto = await ToListItemAsync(
            row.Visit,
            row.Case,
            includeHandoff: true,
            completionNote: note,
            redactPocsoForFieldWorker: true,
            cancellationToken);
        return VisitSyncOutcome.Applied(visitDto, row.Visit.Id);
    }

    public async Task<VisitListItemDto> RescheduleAsync(
        Guid visitId,
        RescheduleVisitRequest request,
        CancellationToken cancellationToken = default)
    {
        if (request is null)
        {
            throw new VisitValidationException("Request body is required.");
        }

        if (request.ScheduledAtUtc is null)
        {
            throw new VisitValidationException("scheduledAtUtc is required.");
        }

        var reason = request.Reason?.Trim() ?? string.Empty;
        if (reason.Length == 0)
        {
            throw new VisitValidationException("reason is required.");
        }

        if (reason.Length > 500)
        {
            throw new VisitValidationException("reason must be at most 500 characters.");
        }

        var scheduledAtUtc = request.ScheduledAtUtc.Value;
        if (scheduledAtUtc.Kind == DateTimeKind.Unspecified)
        {
            scheduledAtUtc = DateTime.SpecifyKind(scheduledAtUtc, DateTimeKind.Utc);
        }
        else if (scheduledAtUtc.Kind == DateTimeKind.Local)
        {
            scheduledAtUtc = scheduledAtUtc.ToUniversalTime();
        }

        if (scheduledAtUtc < DateTime.UtcNow)
        {
            throw new VisitValidationException("scheduledAtUtc must be in the future.");
        }

        var (organisationId, actorUserId) = ResolveActorContext();
        var row = await LoadVisitCaseRowAsync(visitId, organisationId, cancellationToken);

        if (row is null)
        {
            throw new VisitNotFoundException();
        }

        if (row.Visit.AssigneeUserId != actorUserId && !IsSupervisorRole(ResolveActorRole()))
        {
            throw new VisitForbiddenException();
        }

        if (row.Visit.Status == VisitStatus.Completed)
        {
            throw new VisitBusinessRuleException("Completed visits cannot be rescheduled.");
        }

        if (!ActiveStatuses.Contains(row.Visit.Status))
        {
            throw new VisitBusinessRuleException("Visit cannot be rescheduled in its current status.");
        }

        var now = DateTime.UtcNow;
        row.Visit.ScheduledAtUtc = scheduledAtUtc;
        row.Visit.LastRescheduleReason = reason;
        row.Visit.RescheduledAtUtc = now;
        row.Visit.RescheduledByUserId = actorUserId;
        row.Visit.Status = VisitStatus.Scheduled;
        row.Visit.StartedAtUtc = null;
        row.Visit.UpdatedAtUtc = now;
        row.Case.NextVisitDueAtUtc = scheduledAtUtc;
        row.Case.UpdatedAtUtc = now;

        db.AuditEvents.Add(new AuditEvent
        {
            Id = Guid.NewGuid(),
            OrganisationId = organisationId,
            ActorUserId = actorUserId,
            SubjectUserId = actorUserId,
            EventType = AuditEventTypes.VisitRescheduled,
            MetadataJson = JsonSerializer.Serialize(
                new Dictionary<string, object?>
                {
                    ["visitId"] = visitId.ToString("D"),
                    ["caseId"] = row.Case.Id.ToString("D"),
                    ["scheduledAtUtc"] = scheduledAtUtc.ToString("O"),
                },
                JsonOptions),
            CreatedAtUtc = now,
        });

        await db.SaveChangesAsync(cancellationToken);

        return await ToListItemAsync(
            row.Visit,
            row.Case,
            includeHandoff: true,
            completionNote: null,
            redactPocsoForFieldWorker: true,
            cancellationToken);
    }

    private async Task<(VisitListResultDto Result, int TotalCount)> ListForFieldWorkerAsync(
        Func<Visit, Case, bool> predicate,
        bool includeHandoff,
        bool includeCompletionNotesForCompleted,
        CancellationToken cancellationToken)
    {
        var (organisationId, actorUserId) = ResolveActorContext();

        var rows = await LoadVisitCaseRowsAsync(
            v => v.OrganisationId == organisationId && v.AssigneeUserId == actorUserId,
            cancellationToken);

        var filtered = rows
            .Where(r => r.Case.CurrentStage != CaseStage.TerminationExclusion && predicate(r.Visit, r.Case))
            .ToList();

        var completionNotes = includeCompletionNotesForCompleted
            ? await LoadCompletionNotesAsync(filtered.Select(r => r.Visit), organisationId, cancellationToken)
            : new Dictionary<Guid, string>();

        var items = new List<VisitListItemDto>();
        foreach (var row in filtered)
        {
            string? note = null;
            if (includeCompletionNotesForCompleted)
            {
                completionNotes.TryGetValue(row.Visit.Id, out note);
            }

            items.Add(await ToListItemAsync(
                row.Visit,
                row.Case,
                includeHandoff,
                note,
                redactPocsoForFieldWorker: true,
                cancellationToken));
        }

        items.Sort((a, b) => a.ScheduledAtUtc.CompareTo(b.ScheduledAtUtc));

        return (BuildResult(items), items.Count);
    }

    private async Task<Dictionary<Guid, string>> LoadCompletionNotesAsync(
        IEnumerable<Visit> visits,
        Guid organisationId,
        CancellationToken cancellationToken)
    {
        var completedVisitIds = visits
            .Where(v => v.Status == VisitStatus.Completed)
            .Select(v => v.Id)
            .Distinct()
            .ToList();

        if (completedVisitIds.Count == 0)
        {
            return new Dictionary<Guid, string>();
        }

        return await db.VisitNotes
            .Where(n => n.OrganisationId == organisationId && completedVisitIds.Contains(n.VisitId))
            .ToDictionaryAsync(n => n.VisitId, n => n.BodyText, cancellationToken);
    }

    private async Task<(VisitListResultDto Result, int TotalCount)> ListForFieldWorkerAsync(
        Func<Visit, Case, bool> predicate,
        bool includeHandoff,
        CancellationToken cancellationToken) =>
        await ListForFieldWorkerAsync(predicate, includeHandoff, includeCompletionNotesForCompleted: false, cancellationToken);

    private async Task<List<VisitCaseRow>> LoadVisitCaseRowsAsync(
        System.Linq.Expressions.Expression<Func<Visit, bool>> visitFilter,
        CancellationToken cancellationToken)
    {
        return await (
            from visit in db.Visits.Where(visitFilter)
            join caseEntity in db.Cases on visit.CaseId equals caseEntity.Id
            where visit.OrganisationId == caseEntity.OrganisationId
            orderby visit.ScheduledAtUtc
            select new VisitCaseRow(visit, caseEntity))
            .ToListAsync(cancellationToken);
    }

    private async Task<VisitCaseRow?> LoadVisitCaseRowAsync(
        Guid visitId,
        Guid organisationId,
        CancellationToken cancellationToken)
    {
        return await (
            from visit in db.Visits
            where visit.Id == visitId && visit.OrganisationId == organisationId
            join caseEntity in db.Cases on visit.CaseId equals caseEntity.Id
            where caseEntity.OrganisationId == organisationId
            select new VisitCaseRow(visit, caseEntity))
            .SingleOrDefaultAsync(cancellationToken);
    }

    private async Task<VisitListItemDto> ToListItemAsync(
        Visit visit,
        Case caseEntity,
        bool includeHandoff,
        string? completionNote,
        bool redactPocsoForFieldWorker,
        CancellationToken cancellationToken)
    {
        var now = DateTime.UtcNow;
        var isOverdue = visit.ScheduledAtUtc < now && ActiveStatuses.Contains(visit.Status);
        var assigneeEmail = await db.Users
            .Where(u => u.Id == visit.AssigneeUserId)
            .Select(u => u.Email)
            .SingleOrDefaultAsync(cancellationToken);

        return new VisitListItemDto
        {
            Id = visit.Id,
            AssigneeUserId = visit.AssigneeUserId,
            AssigneeEmail = assigneeEmail,
            ScheduledAtUtc = visit.ScheduledAtUtc,
            Status = visit.Status.ToString(),
            IsOverdue = isOverdue,
            StartedAtUtc = visit.StartedAtUtc,
            CompletedAtUtc = visit.CompletedAtUtc,
            CompletionNote = completionNote,
            LastRescheduleReason = visit.LastRescheduleReason,
            CancellationReason = visit.CancellationReason,
            HandoffWhisper = includeHandoff
                ? await BuildHandoffWhisperAsync(caseEntity, visit.AssigneeUserId, cancellationToken)
                : null,
            Places = await LoadPlacesAsync(visit.Id, cancellationToken),
            Case = CaseDtoMapper.ToCaseSummary(caseEntity, redactPocsoForFieldWorker),
        };
    }

    private async Task<IReadOnlyList<VisitPlaceDto>> LoadPlacesAsync(
        Guid visitId,
        CancellationToken cancellationToken)
    {
        var rows = await (
            from place in db.VisitPlaces
            where place.VisitId == visitId && place.RemovedAtUtc == null
            join loggedBy in db.Users on place.LoggedByUserId equals loggedBy.Id into loggedByJoin
            from loggedBy in loggedByJoin.DefaultIfEmpty()
            orderby place.CreatedAtUtc
            select new { place, LoggedByEmail = (string?)loggedBy.Email })
            .ToListAsync(cancellationToken);

        return rows
            .Select(row => new VisitPlaceDto
            {
                Id = row.place.Id,
                VisitId = row.place.VisitId,
                Address = row.place.Address,
                OsmReference = row.place.OsmReference,
                PlannedLatitude = row.place.PlannedLatitude,
                PlannedLongitude = row.place.PlannedLongitude,
                CreatedAtUtc = row.place.CreatedAtUtc,
                LoggedLatitude = row.place.LoggedLatitude,
                LoggedLongitude = row.place.LoggedLongitude,
                LoggedAtUtc = row.place.LoggedAtUtc,
                LoggedByEmail = row.LoggedByEmail,
                Comment = row.place.Comment,
                CommentUpdatedAtUtc = row.place.CommentUpdatedAtUtc,
            })
            .ToList();
    }

    private async Task<HandoffWhisperDto?> BuildHandoffWhisperAsync(
        Case caseEntity,
        Guid assigneeUserId,
        CancellationToken cancellationToken)
    {
        if (caseEntity.AssignedWorkerId != assigneeUserId
            || caseEntity.AssignedAtUtc is null
            || !IsWithinHandoffWindow(caseEntity.AssignedAtUtc.Value))
        {
            return null;
        }

        var assignment = await db.CaseAssignments
            .Where(a =>
                a.CaseId == caseEntity.Id
                && a.OrganisationId == caseEntity.OrganisationId
                && a.ToWorkerId == caseEntity.AssignedWorkerId)
            .OrderByDescending(a => a.CreatedAtUtc)
            .FirstOrDefaultAsync(cancellationToken);

        if (assignment is null)
        {
            return null;
        }

        return new HandoffWhisperDto
        {
            PriorActions = assignment.PriorActions,
            OpenItems = assignment.OpenItems,
            NextVisitPurpose = assignment.NextVisitPurpose,
            TransferredAtUtc = assignment.CreatedAtUtc,
        };
    }

    private static VisitListResultDto BuildResult(IReadOnlyList<VisitListItemDto> items) => new()
    {
        Items = items,
        Page = 1,
        PageSize = items.Count,
    };

    private static VisitGroupingSuggestionDto BuildGroupingSuggestion(
        IReadOnlyList<VisitListItemDto> todayVisits)
    {
        const string insufficientMessage =
            "At least two visits with verified GPS are required for route grouping";

        var excluded = new List<VisitGroupingExcludedDto>();
        var eligible = new List<VisitGroupingPoint>();

        foreach (var visit in todayVisits)
        {
            var caseSummary = visit.Case;
            if (!caseSummary.GpsVerified)
            {
                excluded.Add(new VisitGroupingExcludedDto
                {
                    VisitId = visit.Id,
                    Reason = "gps_unverified",
                });
                continue;
            }

            if (caseSummary.Latitude is null || caseSummary.Longitude is null)
            {
                excluded.Add(new VisitGroupingExcludedDto
                {
                    VisitId = visit.Id,
                    Reason = "gps_coordinates_missing",
                });
                continue;
            }

            eligible.Add(new VisitGroupingPoint(
                visit.Id,
                (double)caseSummary.Latitude.Value,
                (double)caseSummary.Longitude.Value,
                visit.ScheduledAtUtc));
        }

        if (eligible.Count < 2)
        {
            return new VisitGroupingSuggestionDto
            {
                Clusters = Array.Empty<VisitGroupingClusterDto>(),
                SuggestedVisitOrder = Array.Empty<Guid>(),
                Legs = Array.Empty<VisitGroupingLegDto>(),
                Excluded = excluded,
                EligibleCount = eligible.Count,
                ExcludedCount = excluded.Count,
                Message = insufficientMessage,
            };
        }

        var grouper = new VisitProximityGrouper();
        var grouping = grouper.Group(eligible);
        var byId = eligible.ToDictionary(v => v.VisitId);

        var clusters = grouping.Clusters
            .Select((cluster, index) => new VisitGroupingClusterDto
            {
                ClusterIndex = index,
                VisitIds = cluster.Select(v => v.VisitId).ToArray(),
                CentroidLatitude = cluster.Average(v => v.Latitude),
                CentroidLongitude = cluster.Average(v => v.Longitude),
            })
            .ToArray();

        var legs = new List<VisitGroupingLegDto>();
        for (var i = 0; i < grouping.SuggestedVisitOrder.Count; i++)
        {
            var visitId = grouping.SuggestedVisitOrder[i];
            double? distanceKm = null;
            if (i > 0)
            {
                var previous = byId[grouping.SuggestedVisitOrder[i - 1]];
                var current = byId[visitId];
                distanceKm = GeoDistance.RoundKmOneDecimal(GeoDistance.DistanceKm(
                    previous.Latitude,
                    previous.Longitude,
                    current.Latitude,
                    current.Longitude));
            }

            legs.Add(new VisitGroupingLegDto
            {
                VisitId = visitId,
                DistanceKmFromPrevious = distanceKm,
            });
        }

        return new VisitGroupingSuggestionDto
        {
            Clusters = clusters,
            SuggestedVisitOrder = grouping.SuggestedVisitOrder.ToArray(),
            Legs = legs,
            Excluded = excluded,
            EligibleCount = eligible.Count,
            ExcludedCount = excluded.Count,
        };
    }

    // Returns UTC instants bounding the current IST calendar week (Mon–Sun), not the UTC week.
    private static (DateTime Start, DateTime End) GetUtcWeekBounds(DateTime utcNow)
    {
        var istNow = utcNow + IstOffset;
        var day = (int)istNow.DayOfWeek;
        var mondayOffset = day == 0 ? -6 : 1 - day;
        var istWeekStart = istNow.Date.AddDays(mondayOffset);
        var weekStart = istWeekStart - IstOffset;
        var weekEnd = istWeekStart.AddDays(7).AddTicks(-1) - IstOffset;
        return (weekStart, weekEnd);
    }

    private static bool IsWithinHandoffWindow(DateTime assignedAtUtc) =>
        (DateTime.UtcNow.Date - assignedAtUtc.Date).Days < 7;

    private async Task EnsureFieldWorkerAsync(
        Guid userId,
        Guid organisationId,
        CancellationToken cancellationToken)
    {
        var user = await db.Users.SingleOrDefaultAsync(
            u => u.Id == userId && u.OrganisationId == organisationId,
            cancellationToken);

        if (user is null || !user.IsActive)
        {
            throw new VisitBusinessRuleException("Assignee must be an active field worker in your organisation.");
        }

        if (user.Role is not (UserRoles.SocialWorker or UserRoles.CaseWorker))
        {
            throw new VisitBusinessRuleException("Assignee must be a SocialWorker or CaseWorker.");
        }
    }

    private Guid ResolveOrganisationId() => ResolveActorContext().OrganisationId;

    public (Guid OrganisationId, Guid ActorUserId) ResolveActorContextForSync() => ResolveActorContext();

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

    private static bool IsSupervisorRole(string role) =>
        role is UserRoles.Director or UserRoles.Coordinator;

    private string ResolveActorRole() =>
        httpContextAccessor.HttpContext?.User.FindFirstValue(ClaimTypes.Role) ?? string.Empty;

    private sealed record VisitCaseRow(Visit Visit, Case Case);
}

public sealed class VisitValidationException(string message) : Exception(message);

public sealed class VisitBusinessRuleException(string message) : Exception(message);

public sealed class VisitNotFoundException : Exception;

public sealed class VisitForbiddenException : Exception;
