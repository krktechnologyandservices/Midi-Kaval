import {
  ApiClientError,
  authSessionService,
  AuthSessionService,
} from '../auth/AuthSessionService';
import {
  buildOptimisticCompletedVisit,
  buildOptimisticStartedVisit,
} from '../sync/mergeQueueWithVisits';
import {enqueueOfflineMutation} from '../sync/offlineQueue';
import {isDeviceOffline} from '../sync/networkStatus';
import {flushOfflineQueue} from '../sync/mobileSyncPushService';
import {
  VisitGroupingSuggestionDto,
  VisitListItemDto,
  VisitListResultDto,
  VisitPlaceDto,
} from './visit.models';

export class VisitApiService {
  constructor(private readonly auth: AuthSessionService = authSessionService) {}

  async listToday(): Promise<VisitListResultDto> {
    try {
      const envelope = await this.auth.getApi<VisitListResultDto>(
        '/api/v1/visits/today',
      );
      void flushOfflineQueue();
      return envelope.data;
    } catch (error) {
      throw this.wrapError(error);
    }
  }

  /** Visits scheduled during the current UTC week (Mon–Sun), including today's. */
  async listWeekly(): Promise<VisitListResultDto> {
    try {
      const envelope = await this.auth.getApi<VisitListResultDto>(
        '/api/v1/visits/weekly',
      );
      return envelope.data;
    } catch (error) {
      throw this.wrapError(error);
    }
  }

  async getTodayGroupingSuggestion(): Promise<VisitGroupingSuggestionDto> {
    try {
      const envelope = await this.auth.getApi<VisitGroupingSuggestionDto>(
        '/api/v1/visits/today/grouping-suggestion',
      );
      return envelope.data;
    } catch (error) {
      throw this.wrapError(error);
    }
  }

  async startVisit(visit: VisitListItemDto): Promise<VisitListItemDto> {
    const visitId = visit.id;
    if (!visitId) {
      throw new VisitApiError('unknown', 0, new Error('Visit id is required.'));
    }

    if (await isDeviceOffline()) {
      await enqueueOfflineMutation({type: 'visit.start', visitId});
      return buildOptimisticStartedVisit(visit);
    }

    try {
      const envelope = await this.auth.postApi<VisitListItemDto>(
        `/api/v1/visits/${visitId}/start`,
      );
      void flushOfflineQueue();
      return envelope.data;
    } catch (error) {
      const wrapped = this.wrapError(error);
      if (wrapped.kind === 'network') {
        await enqueueOfflineMutation({type: 'visit.start', visitId});
        return buildOptimisticStartedVisit(visit);
      }

      throw wrapped;
    }
  }

  async completeVisit(
    visit: VisitListItemDto,
    note: string,
  ): Promise<VisitListItemDto> {
    const visitId = visit.id;
    if (!visitId) {
      throw new VisitApiError('unknown', 0, new Error('Visit id is required.'));
    }

    const trimmedNote = note.trim();
    if (!trimmedNote) {
      throw new VisitApiError(
        'unknown',
        0,
        new Error('Visit note is required.'),
      );
    }

    const noteClientTimestampUtc = new Date().toISOString();

    if (await isDeviceOffline()) {
      await enqueueOfflineMutation({
        type: 'visit.complete',
        visitId,
        note: trimmedNote,
        noteClientTimestampUtc,
      });
      return buildOptimisticCompletedVisit(visit, trimmedNote);
    }

    try {
      const envelope = await this.auth.postApi<VisitListItemDto>(
        `/api/v1/visits/${visitId}/complete`,
        {note: trimmedNote},
      );
      void flushOfflineQueue();
      return envelope.data;
    } catch (error) {
      const wrapped = this.wrapError(error);
      if (wrapped.kind === 'network') {
        await enqueueOfflineMutation({
          type: 'visit.complete',
          visitId,
          note: trimmedNote,
          noteClientTimestampUtc,
        });
        return buildOptimisticCompletedVisit(visit, trimmedNote);
      }

      throw wrapped;
    }
  }

  async rescheduleVisit(
    visitId: string,
    scheduledAtUtc: string,
    reason: string,
  ): Promise<VisitListItemDto> {
    try {
      const envelope = await this.auth.postApi<VisitListItemDto>(
        `/api/v1/visits/${visitId}/reschedule`,
        {scheduledAtUtc, reason},
      );
      return envelope.data;
    } catch (error) {
      throw this.wrapError(error);
    }
  }

  async logPlace(
    visitId: string,
    placeId: string,
    latitude: number,
    longitude: number,
  ): Promise<VisitPlaceDto> {
    try {
      const envelope = await this.auth.postApi<VisitPlaceDto>(
        `/api/v1/visits/${visitId}/places/${placeId}/log`,
        {latitude, longitude},
      );
      return envelope.data;
    } catch (error) {
      throw this.wrapError(error);
    }
  }

  extractErrorMessage(error: unknown): string {
    if (error instanceof VisitApiError) {
      if (error.kind === 'network') {
        return 'Could not reach the server — check connection and try again.';
      }

      return this.auth.extractErrorMessage(error.sourceError);
    }

    return this.auth.extractErrorMessage(error);
  }

  private wrapError(error: unknown): VisitApiError {
    if (error instanceof ApiClientError) {
      if (error.status === 0) {
        return new VisitApiError('network', 0, error);
      }

      return new VisitApiError('http', error.status, error);
    }

    return new VisitApiError('unknown', 0, error);
  }
}

export class VisitApiError extends Error {
  constructor(
    readonly kind: 'network' | 'http' | 'unknown',
    readonly status: number,
    readonly sourceError: unknown,
  ) {
    super('Visit API error');
    this.name = 'VisitApiError';
  }
}

export const visitApiService = new VisitApiService();
