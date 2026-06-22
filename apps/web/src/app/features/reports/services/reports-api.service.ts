import { HttpClient, HttpErrorResponse, HttpParams } from '@angular/common/http';
import { Injectable, inject } from '@angular/core';
import { firstValueFrom } from 'rxjs';
import { environment } from '../../../../environments/environment';

@Injectable({ providedIn: 'root' })
export class ReportsApiService {
  private readonly http = inject(HttpClient);
  private readonly baseUrl = `${environment.apiBaseUrl}/api/v1/reports`;

  async exportSocioDemographicProfile(month: number, year: number): Promise<Blob> {
    const params = new HttpParams().set('month', month).set('year', year);
    try {
      return await firstValueFrom(
        this.http.get(`${this.baseUrl}/socio-demographic-profile`, {
          params,
          responseType: 'blob',
        }),
      );
    } catch (error) {
      if (error instanceof HttpErrorResponse && error.error instanceof Blob) {
        const text = await error.error.text();
        try {
          const parsed = JSON.parse(text);
          throw new ReportsApiError(parsed.error ?? text);
        } catch {
          throw new ReportsApiError(text);
        }
      }
      throw this.wrapError(error);
    }
  }

  extractErrorMessage(error: unknown): string {
    if (error instanceof ReportsApiError) {
      if (typeof error.sourceError === 'string') {
        return error.sourceError;
      }
      return this.extractErrorMessage(error.sourceError);
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
      if (error.error?.error) {
        return error.error.error;
      }
      return `Request failed (${error.status})`;
    }
    if (error instanceof Error) {
      return error.message;
    }
    return 'An unknown error occurred';
  }

  private wrapError(error: unknown): ReportsApiError {
    return new ReportsApiError(error);
  }
}

export class ReportsApiError extends Error {
  constructor(readonly sourceError: unknown) {
    super('Reports API error');
    this.name = 'ReportsApiError';
  }
}
