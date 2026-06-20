import { HttpClient, HttpErrorResponse } from '@angular/common/http';
import { Injectable, inject } from '@angular/core';
import { firstValueFrom } from 'rxjs';
import { environment } from '../../../../environments/environment';
import { AuthSessionService } from '../../../core/auth/auth-session.service';
import { ApiEnvelope } from '../../cases/models/case.models';
import { CrisisQueueItemDto, CrisisQueueListResultDto } from '../travel.models';

@Injectable({ providedIn: 'root' })
export class CrisisQueueApiService {
  private readonly http = inject(HttpClient);
  private readonly auth = inject(AuthSessionService);

  async list(): Promise<CrisisQueueItemDto[]> {
    try {
      const envelope = await firstValueFrom(
        this.http.get<ApiEnvelope<CrisisQueueListResultDto>>(
          `${environment.apiBaseUrl}/api/v1/supervisor/crisis-queue`,
        ),
      );
      return envelope.data.items ?? [];
    } catch (error) {
      throw this.wrapError(error);
    }
  }

  extractErrorMessage(error: unknown): string {
    if (error instanceof CrisisQueueApiError) {
      return this.auth.extractErrorMessage(error.sourceError);
    }

    return this.auth.extractErrorMessage(error);
  }

  private wrapError(error: unknown): CrisisQueueApiError {
    if (error instanceof HttpErrorResponse) {
      if (error.status === 0 || error.status === undefined) {
        return new CrisisQueueApiError('network', 0, error);
      }

      return new CrisisQueueApiError('http', error.status, error);
    }

    return new CrisisQueueApiError('unknown', 0, error);
  }
}

export class CrisisQueueApiError extends Error {
  constructor(
    readonly kind: 'network' | 'http' | 'unknown',
    readonly status: number,
    readonly sourceError: unknown,
  ) {
    super('Crisis queue API error');
    this.name = 'CrisisQueueApiError';
  }
}
