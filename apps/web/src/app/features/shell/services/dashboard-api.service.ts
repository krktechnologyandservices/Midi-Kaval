import { HttpClient, HttpErrorResponse } from '@angular/common/http';
import { Injectable, inject } from '@angular/core';
import { firstValueFrom } from 'rxjs';
import { environment } from '../../../../environments/environment';
import { ApiEnvelope } from '../../cases/models/case.models';
import { DashboardResultDto } from '../shell.models';

@Injectable({ providedIn: 'root' })
export class DashboardApiService {
  private readonly http = inject(HttpClient);

  async get(): Promise<DashboardResultDto> {
    try {
      const envelope = await firstValueFrom(
        this.http.get<ApiEnvelope<DashboardResultDto>>(
          `${environment.apiBaseUrl}/api/v1/supervisor/dashboard`,
        ),
      );
      return envelope.data;
    } catch (error) {
      throw this.wrapError(error);
    }
  }

  extractErrorMessage(error: unknown): string {
    if (error instanceof DashboardApiError) {
      return error.message;
    }
    if (error instanceof HttpErrorResponse) {
      if (error.status === 0 || error.status === undefined) {
        return 'Network error — check your connection';
      }
      if (error.error?.detail) {
        return error.error.detail;
      }
      if (error.error?.title) {
        return error.error.title;
      }
      return `Request failed (${error.status})`;
    }
    if (error instanceof Error) {
      return error.message;
    }
    return 'An unknown error occurred';
  }

  private wrapError(error: unknown): DashboardApiError {
    if (error instanceof HttpErrorResponse) {
      if (error.status === 0 || error.status === undefined) {
        return new DashboardApiError('network', error);
      }
      return new DashboardApiError('http', error);
    }
    return new DashboardApiError('unknown', error);
  }
}

export class DashboardApiError extends Error {
  constructor(
    readonly kind: 'network' | 'http' | 'unknown',
    readonly sourceError: unknown,
  ) {
    super('Dashboard API error');
    this.name = 'DashboardApiError';
  }
}
