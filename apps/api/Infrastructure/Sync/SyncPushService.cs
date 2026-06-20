using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using MidiKaval.Api.Domain.Entities;
using MidiKaval.Api.Infrastructure.Persistence;
using MidiKaval.Api.Infrastructure.TravelClaims;
using MidiKaval.Api.Infrastructure.Visits;
using MidiKaval.Api.Models.Sync;
using MidiKaval.Api.Models.TravelClaims;
using MidiKaval.Api.Models.Visits;
using Npgsql;

namespace MidiKaval.Api.Infrastructure.Sync;

public sealed class SyncPushService(
    AppDbContext db,
    VisitService visitService,
    TravelClaimService travelClaimService)
{
    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web);

    public async Task<SyncPushResultDto> PushAsync(
        SyncPushRequest request,
        CancellationToken cancellationToken = default)
    {
        if (request.Mutations is null || request.Mutations.Count == 0)
        {
            return new SyncPushResultDto { Results = Array.Empty<SyncMutationResultDto>() };
        }

        var (organisationId, userId) = visitService.ResolveActorContextForSync();
        var results = new List<SyncMutationResultDto>();

        foreach (var mutation in request.Mutations)
        {
            results.Add(await ProcessMutationAsync(
                mutation,
                organisationId,
                userId,
                cancellationToken));
        }

        return new SyncPushResultDto { Results = results };
    }

    private async Task<SyncMutationResultDto> ProcessMutationAsync(
        SyncMutationDto mutation,
        Guid organisationId,
        Guid userId,
        CancellationToken cancellationToken)
    {
        if (mutation.ClientMutationId == Guid.Empty)
        {
            return await RecordAndReturnAsync(
                organisationId,
                userId,
                mutation,
                SyncApplyOutcome.Rejected("clientMutationId is required."),
                cancellationToken);
        }

        var existing = await db.SyncMutations.SingleOrDefaultAsync(
            m => m.OrganisationId == organisationId
                && m.UserId == userId
                && m.ClientMutationId == mutation.ClientMutationId,
            cancellationToken);

        if (existing is not null)
        {
            return await BuildDuplicateResultAsync(existing, mutation.ClientMutationId, cancellationToken);
        }

        try
        {
            var outcome = await ApplyMutationAsync(mutation, cancellationToken);
            return await RecordAndReturnAsync(
                organisationId,
                userId,
                mutation,
                outcome,
                cancellationToken);
        }
        catch (VisitForbiddenException)
        {
            return await RecordAndReturnAsync(
                organisationId,
                userId,
                mutation,
                SyncApplyOutcome.Rejected("Forbidden."),
                cancellationToken);
        }
        catch (DbUpdateException ex) when (IsUniqueViolation(ex))
        {
            var raced = await db.SyncMutations.SingleOrDefaultAsync(
                m => m.OrganisationId == organisationId
                    && m.UserId == userId
                    && m.ClientMutationId == mutation.ClientMutationId,
                cancellationToken);

            if (raced is not null)
            {
                return await BuildDuplicateResultAsync(raced, mutation.ClientMutationId, cancellationToken);
            }

            return new SyncMutationResultDto
            {
                ClientMutationId = mutation.ClientMutationId,
                Status = SyncMutationStatuses.Rejected,
                ServerMessage = "Sync conflict — retry this mutation.",
            };
        }
        catch (Exception ex)
        {
            return await RecordAndReturnAsync(
                organisationId,
                userId,
                mutation,
                SyncApplyOutcome.Rejected(ex.Message),
                cancellationToken);
        }
    }

    private async Task<SyncMutationResultDto> BuildDuplicateResultAsync(
        SyncMutation existing,
        Guid clientMutationId,
        CancellationToken cancellationToken)
    {
        VisitListItemDto? visit = null;
        if (existing.Status == SyncMutationStatuses.Applied
            && existing.ResultVisitId is Guid visitId)
        {
            visit = await visitService.GetAssignedVisitListItemAsync(visitId, cancellationToken);
        }

        TravelClaimDto? travelClaim = null;
        if (existing.Status == SyncMutationStatuses.Applied
            && existing.ResultTravelClaimId is Guid claimId)
        {
            try
            {
                travelClaim = await travelClaimService.GetAsync(claimId, cancellationToken);
            }
            catch
            {
                travelClaim = null;
            }
        }

        return new SyncMutationResultDto
        {
            ClientMutationId = clientMutationId,
            Status = SyncMutationStatuses.Duplicate,
            ServerMessage = existing.ServerMessage,
            Visit = visit,
            TravelClaim = travelClaim,
        };
    }

    private async Task<SyncApplyOutcome> ApplyMutationAsync(
        SyncMutationDto mutation,
        CancellationToken cancellationToken)
    {
        if (string.Equals(mutation.Type, SyncMutationTypes.VisitStart, StringComparison.Ordinal))
        {
            var payload = DeserializePayload<VisitStartSyncPayload>(mutation.Payload);
            if (payload is null || payload.VisitId == Guid.Empty)
            {
                return SyncApplyOutcome.Rejected("Invalid visit.start payload.");
            }

            var outcome = await visitService.ApplyVisitStartForSync(payload.VisitId, cancellationToken);
            return outcome.VisitId is Guid visitId && outcome.Visit is not null
                ? SyncApplyOutcome.AppliedVisit(outcome.Visit, visitId)
                : SyncApplyOutcome.Rejected(outcome.ServerMessage ?? "visit.start rejected.");
        }

        if (string.Equals(mutation.Type, SyncMutationTypes.VisitComplete, StringComparison.Ordinal))
        {
            var payload = DeserializePayload<VisitCompleteSyncPayload>(mutation.Payload);
            if (payload is null || payload.VisitId == Guid.Empty)
            {
                return SyncApplyOutcome.Rejected("Invalid visit.complete payload.");
            }

            var outcome = await visitService.ApplyVisitCompleteForSync(
                payload.VisitId,
                payload.Note,
                payload.NoteClientTimestampUtc,
                cancellationToken);
            return outcome.VisitId is Guid visitId && outcome.Visit is not null
                ? SyncApplyOutcome.AppliedVisit(outcome.Visit, visitId)
                : SyncApplyOutcome.Rejected(outcome.ServerMessage ?? "visit.complete rejected.");
        }

        if (string.Equals(mutation.Type, SyncMutationTypes.TravelClaimCreate, StringComparison.Ordinal))
        {
            var payload = DeserializePayload<TravelClaimCreateSyncPayload>(mutation.Payload);
            if (payload is null)
            {
                return SyncApplyOutcome.Rejected("Invalid travel.claim.create payload.");
            }

            var request = new CreateTravelClaimRequest
            {
                ClaimDate = payload.ClaimDate,
                StartLocation = payload.StartLocation,
                Destination = payload.Destination,
                TransportMode = payload.TransportMode,
                Amount = payload.Amount,
                AutoNumber = payload.AutoNumber,
                Notes = payload.Notes,
                CaseIds = payload.CaseIds,
            };

            var claim = await travelClaimService.CreateAsync(request, cancellationToken);
            return SyncApplyOutcome.AppliedTravelClaim(claim, claim.Id);
        }

        return SyncApplyOutcome.Rejected($"Unsupported mutation type '{mutation.Type}'.");
    }

    private async Task<SyncMutationResultDto> RecordAndReturnAsync(
        Guid organisationId,
        Guid userId,
        SyncMutationDto mutation,
        SyncApplyOutcome outcome,
        CancellationToken cancellationToken)
    {
        var now = DateTime.UtcNow;
        db.SyncMutations.Add(new SyncMutation
        {
            Id = Guid.NewGuid(),
            OrganisationId = organisationId,
            UserId = userId,
            ClientMutationId = mutation.ClientMutationId,
            MutationType = mutation.Type,
            PayloadJson = mutation.Payload.ValueKind == JsonValueKind.Undefined
                ? "{}"
                : mutation.Payload.GetRawText(),
            Status = outcome.Status,
            ServerMessage = outcome.ServerMessage,
            ResultVisitId = outcome.VisitId,
            ResultTravelClaimId = outcome.TravelClaimId,
            CreatedAtUtc = now,
            ProcessedAtUtc = now,
        });

        await db.SaveChangesAsync(cancellationToken);

        return new SyncMutationResultDto
        {
            ClientMutationId = mutation.ClientMutationId,
            Status = outcome.Status,
            ServerMessage = outcome.ServerMessage,
            Visit = outcome.Visit,
            TravelClaim = outcome.TravelClaim,
        };
    }

    private static bool IsUniqueViolation(DbUpdateException ex)
    {
        for (var current = (Exception?)ex; current is not null; current = current.InnerException)
        {
            if (current is PostgresException { SqlState: "23505" })
            {
                return true;
            }
        }

        return false;
    }

    private static T? DeserializePayload<T>(JsonElement payload)
        where T : class
    {
        if (payload.ValueKind is JsonValueKind.Null or JsonValueKind.Undefined)
        {
            return null;
        }

        try
        {
            return payload.Deserialize<T>(JsonOptions);
        }
        catch (JsonException)
        {
            return null;
        }
    }
}
