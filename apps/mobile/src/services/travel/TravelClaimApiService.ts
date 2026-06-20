import {
  ApiClientError,
  authSessionService,
  AuthSessionService,
} from '../auth/AuthSessionService';
import {flushOfflineQueue} from '../sync/mobileSyncPushService';
import {enqueueTravelClaimDraft} from '../sync/offlineQueue';
import {isDeviceOffline} from '../sync/networkStatus';
import {
  CreateTravelClaimRequest,
  TravelClaimDto,
  TravelClaimListResultDto,
  UpdateTravelClaimRequest,
} from './travel.models';

export class TravelClaimApiService {
  constructor(private readonly auth: AuthSessionService = authSessionService) {}

  async listMine(): Promise<TravelClaimDto[]> {
    try {
      const envelope = await this.auth.getApi<TravelClaimListResultDto>(
        '/api/v1/travel-claims',
      );
      void flushOfflineQueue();
      return envelope.data.items ?? [];
    } catch (error) {
      throw this.wrapError(error);
    }
  }

  async get(claimId: string): Promise<TravelClaimDto> {
    try {
      const envelope = await this.auth.getApi<TravelClaimDto>(
        `/api/v1/travel-claims/${claimId}`,
      );
      return envelope.data;
    } catch (error) {
      throw this.wrapError(error);
    }
  }

  async create(request: CreateTravelClaimRequest): Promise<TravelClaimDto> {
    if (await isDeviceOffline()) {
      throw new TravelClaimApiError(
        'network',
        0,
        new Error('Offline — save as local draft instead.'),
      );
    }

    try {
      const envelope = await this.auth.postApi<TravelClaimDto>(
        '/api/v1/travel-claims',
        request,
      );
      void flushOfflineQueue();
      return envelope.data;
    } catch (error) {
      const wrapped = this.wrapError(error);
      if (wrapped.kind === 'network') {
        throw wrapped;
      }
      throw wrapped;
    }
  }

  async createOfflineDraft(input: {
    localDraftKey?: string;
    claimDate: string;
    startLocation: string;
    destination: string;
    transportMode: string;
    amount: number;
    autoNumber?: string | null;
    notes?: string | null;
    caseIds: string[];
    localReceiptUri?: string;
    receiptFileName?: string;
    receiptContentType?: string;
  }) {
    return enqueueTravelClaimDraft(input);
  }

  async update(
    claimId: string,
    request: UpdateTravelClaimRequest,
  ): Promise<TravelClaimDto> {
    if (await isDeviceOffline()) {
      throw new TravelClaimApiError(
        'network',
        0,
        new Error('Offline — draft updates require a connection.'),
      );
    }

    try {
      const envelope = await this.auth.patchApi<TravelClaimDto>(
        `/api/v1/travel-claims/${claimId}`,
        request,
      );
      return envelope.data;
    } catch (error) {
      throw this.wrapError(error);
    }
  }

  async submit(claimId: string): Promise<TravelClaimDto> {
    if (await isDeviceOffline()) {
      throw new TravelClaimApiError(
        'network',
        0,
        new Error('Offline — submit requires a connection.'),
      );
    }

    try {
      const envelope = await this.auth.postApi<TravelClaimDto>(
        `/api/v1/travel-claims/${claimId}/submit`,
      );
      return envelope.data;
    } catch (error) {
      throw this.wrapError(error);
    }
  }

  extractErrorMessage(error: unknown): string {
    if (error instanceof TravelClaimApiError) {
      if (error.kind === 'network') {
        return 'Could not reach the server — check connection and try again.';
      }

      return this.auth.extractErrorMessage(error.sourceError);
    }

    return this.auth.extractErrorMessage(error);
  }

  private wrapError(error: unknown): TravelClaimApiError {
    if (error instanceof ApiClientError) {
      if (error.status === 0) {
        return new TravelClaimApiError('network', 0, error);
      }

      return new TravelClaimApiError('http', error.status, error);
    }

    return new TravelClaimApiError('unknown', 0, error);
  }
}

export class TravelClaimApiError extends Error {
  constructor(
    public readonly kind: 'network' | 'http' | 'unknown',
    public readonly status: number,
    public readonly sourceError: unknown,
  ) {
    super('Travel claim API error');
    this.name = 'TravelClaimApiError';
  }
}

export const travelClaimApiService = new TravelClaimApiService();
