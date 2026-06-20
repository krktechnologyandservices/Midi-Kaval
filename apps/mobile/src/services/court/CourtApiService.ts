import {
  ApiClientError,
  authSessionService,
  AuthSessionService,
} from '../auth/AuthSessionService';
import {
  CourtSittingScheduleItemDto,
  CourtSittingUpcomingListResultDto,
} from '../cases/case.models';

export class CourtApiService {
  constructor(private readonly auth: AuthSessionService = authSessionService) {}

  async listUpcomingCourtSittings(): Promise<CourtSittingScheduleItemDto[]> {
    try {
      const envelope = await this.auth.getApi<CourtSittingUpcomingListResultDto>(
        '/api/v1/court-sittings/upcoming',
      );
      return envelope.data.items ?? [];
    } catch (error) {
      throw this.wrapError(error);
    }
  }

  extractErrorMessage(error: unknown): string {
    if (error instanceof CourtApiError) {
      if (error.kind === 'network') {
        return 'Could not load court schedule — check connection and try again.';
      }

      return this.auth.extractErrorMessage(error.sourceError);
    }

    return this.auth.extractErrorMessage(error);
  }

  private wrapError(error: unknown): CourtApiError {
    if (error instanceof ApiClientError) {
      if (error.status === 0) {
        return new CourtApiError('network', 0, error);
      }

      return new CourtApiError('http', error.status, error);
    }

    return new CourtApiError('unknown', 0, error);
  }
}

export class CourtApiError extends Error {
  constructor(
    readonly kind: 'network' | 'http' | 'unknown',
    readonly status: number,
    readonly sourceError: unknown,
  ) {
    super('Court API error');
    this.name = 'CourtApiError';
  }
}

export const courtApiService = new CourtApiService();
