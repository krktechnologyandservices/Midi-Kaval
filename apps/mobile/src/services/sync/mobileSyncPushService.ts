import {attachmentApiService} from '../attachments/AttachmentApiService';
import {TravelClaimDto} from '../travel/travel.models';
import {authSessionService} from '../auth/AuthSessionService';
import {VisitListItemDto} from '../visits/visit.models';
import {
  markQueueSyncStatus,
  readOfflineQueue,
  removeMutationsByClientIds,
  updateOfflineQueue,
} from './offlineQueue';
import {isDeviceOffline} from './networkStatus';
import {
  isTravelClaimMutation,
  isVisitMutation,
  SyncMutationResult,
  SyncPushResult,
  QueuedMutation,
  TravelClaimQueuedMutation,
} from './syncMutationTypes';

function toApiMutation(mutation: QueuedMutation): Record<string, unknown> {
  const base = {
    clientMutationId: mutation.clientMutationId,
    type: mutation.type,
    clientTimestampUtc: mutation.clientTimestampUtc,
  };

  if (isVisitMutation(mutation)) {
    if (mutation.type === 'visit.start') {
      return {
        ...base,
        payload: {visitId: mutation.visitId},
      };
    }

    return {
      ...base,
      payload: {
        visitId: mutation.visitId,
        note: mutation.note ?? '',
        noteClientTimestampUtc:
          mutation.noteClientTimestampUtc ?? mutation.clientTimestampUtc,
      },
    };
  }

  return {
    ...base,
    payload: {
      claimDate: mutation.claimDate,
      startLocation: mutation.startLocation,
      destination: mutation.destination,
      transportMode: mutation.transportMode,
      amount: mutation.amount,
      autoNumber: mutation.autoNumber,
      notes: mutation.notes,
      caseIds: mutation.caseIds,
    },
  };
}

function shouldRemoveFromQueue(result: SyncMutationResult): boolean {
  if (result.status === 'applied') {
    return true;
  }

  return (
    result.status === 'duplicate' &&
    (!!result.visit || !!result.travelClaim)
  );
}

async function uploadDeferredReceipt(
  mutation: TravelClaimQueuedMutation,
  claimId: string,
): Promise<void> {
  if (!mutation.localReceiptUri || !mutation.receiptFileName || !mutation.receiptContentType) {
    return;
  }

  await attachmentApiService.upload({
    resourceType: 'TravelClaim',
    resourceId: claimId,
    fileUri: mutation.localReceiptUri,
    fileName: mutation.receiptFileName,
    contentType: mutation.receiptContentType,
  });
}

export type FlushOfflineQueueResult = {
  appliedVisits: Map<string, VisitListItemDto>;
  appliedTravelClaims: Map<string, TravelClaimDto>;
  appliedTravelClaimsByLocalDraftKey: Map<string, TravelClaimDto>;
  remaining: QueuedMutation[];
};

let flushInFlight: Promise<FlushOfflineQueueResult> | null = null;

async function flushOfflineQueueInternal(): Promise<FlushOfflineQueueResult> {
  const appliedVisits = new Map<string, VisitListItemDto>();
  const appliedTravelClaims = new Map<string, TravelClaimDto>();
  const appliedTravelClaimsByLocalDraftKey = new Map<string, TravelClaimDto>();

  if (await isDeviceOffline()) {
    return {
      appliedVisits,
      appliedTravelClaims,
      appliedTravelClaimsByLocalDraftKey,
      remaining: await readOfflineQueue(),
    };
  }

  let queue = await readOfflineQueue();
  if (!queue.length) {
    return {
      appliedVisits,
      appliedTravelClaims,
      appliedTravelClaimsByLocalDraftKey,
      remaining: [],
    };
  }

  const refreshed = await authSessionService.refreshSession();
  if (!refreshed) {
    queue = queue.map(item => ({
      ...item,
      syncStatus: 'error' as const,
      lastError: 'Session expired — sign in again to sync.',
    }));
    await updateOfflineQueue(queue);
    return {
      appliedVisits,
      appliedTravelClaims,
      appliedTravelClaimsByLocalDraftKey,
      remaining: queue,
    };
  }

  queue = queue.map(item => ({...item, syncStatus: 'pending' as const}));
  await updateOfflineQueue(queue);

  try {
    const envelope = await authSessionService.postApi<SyncPushResult>(
      '/api/v1/sync/push',
      {mutations: queue.map(toApiMutation)},
    );

    const removeIds: string[] = [];
    for (const result of envelope.data.results ?? []) {
      const mutation = queue.find(
        item => item.clientMutationId === result.clientMutationId,
      );
      if (!mutation) {
        continue;
      }

      if (shouldRemoveFromQueue(result)) {
        let removeThis = true;

        if (isTravelClaimMutation(mutation) && mutation.localReceiptUri) {
          const claim = result.travelClaim as TravelClaimDto | undefined;
          if (claim?.id) {
            try {
              await uploadDeferredReceipt(mutation, claim.id);
            } catch (error) {
              removeThis = false;
              await markQueueSyncStatus(
                result.clientMutationId,
                'error',
                authSessionService.extractErrorMessage(error) ??
                  'Receipt upload failed.',
              );
            }
          }
        }

        if (removeThis) {
          removeIds.push(result.clientMutationId);

          const visit = result.visit as VisitListItemDto | undefined;
          if (visit?.id) {
            appliedVisits.set(visit.id, visit);
          }

          const travelClaim = result.travelClaim as TravelClaimDto | undefined;
          if (travelClaim?.id) {
            appliedTravelClaims.set(travelClaim.id, travelClaim);
            if (isTravelClaimMutation(mutation) && mutation.localDraftKey) {
              appliedTravelClaimsByLocalDraftKey.set(
                mutation.localDraftKey,
                travelClaim,
              );
            }
          }
        }

        continue;
      }

      if (result.status === 'rejected' || result.status === 'duplicate') {
        await markQueueSyncStatus(
          result.clientMutationId,
          'error',
          result.serverMessage ?? 'Sync rejected.',
        );
      }
    }

    const remaining = await removeMutationsByClientIds(removeIds);

    return {
      appliedVisits,
      appliedTravelClaims,
      appliedTravelClaimsByLocalDraftKey,
      remaining,
    };
  } catch (error) {
    const message = authSessionService.extractErrorMessage(error);
    queue = (await readOfflineQueue()).map(item => ({
      ...item,
      syncStatus: 'error' as const,
      lastError: message,
    }));
    await updateOfflineQueue(queue);
    return {
      appliedVisits,
      appliedTravelClaims,
      appliedTravelClaimsByLocalDraftKey,
      remaining: queue,
    };
  }
}

export function flushOfflineQueue(): Promise<FlushOfflineQueueResult> {
  if (!flushInFlight) {
    flushInFlight = flushOfflineQueueInternal().finally(() => {
      flushInFlight = null;
    });
  }

  return flushInFlight;
}
