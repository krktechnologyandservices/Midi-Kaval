import { HttpClient, HttpErrorResponse } from '@angular/common/http';
import { Injectable, inject } from '@angular/core';
import { firstValueFrom } from 'rxjs';
import { environment } from '../../../../environments/environment';
import { ApiEnvelope } from '../../cases/models/case.models';
import { LegendDto } from '../legends.models';

@Injectable({ providedIn: 'root' })
export class LegendsApiService {
  private readonly http = inject(HttpClient);
  private readonly baseUrl = `${environment.apiBaseUrl}/api/v1/legends`;

  async list(type: string, includeInactive = false): Promise<LegendDto[]> {
    try {
      const envelope = await firstValueFrom(
        this.http.get<ApiEnvelope<{ items: LegendDto[] }>>(
          `${this.baseUrl}/${type}`,
          { params: includeInactive ? { includeInactive: 'true' } : {} },
        ),
      );
      return envelope.data.items;
    } catch (error) {
      throw this.wrapError(error);
    }
  }

  async get(type: string, id: string): Promise<LegendDto> {
    try {
      const envelope = await firstValueFrom(
        this.http.get<ApiEnvelope<LegendDto>>(`${this.baseUrl}/${type}/${id}`),
      );
      return envelope.data;
    } catch (error) {
      throw this.wrapError(error);
    }
  }

  async create(type: string, name: string): Promise<LegendDto> {
    try {
      const envelope = await firstValueFrom(
        this.http.post<ApiEnvelope<LegendDto>>(`${this.baseUrl}/${type}`, { name }),
      );
      return envelope.data;
    } catch (error) {
      throw this.wrapError(error);
    }
  }

  async update(type: string, id: string, name: string): Promise<LegendDto> {
    try {
      const envelope = await firstValueFrom(
        this.http.put<ApiEnvelope<LegendDto>>(`${this.baseUrl}/${type}/${id}`, { name }),
      );
      return envelope.data;
    } catch (error) {
      throw this.wrapError(error);
    }
  }

  async deactivate(type: string, id: string): Promise<void> {
    try {
      await firstValueFrom(
        this.http.delete(`${this.baseUrl}/${type}/${id}`),
      );
    } catch (error) {
      throw this.wrapError(error);
    }
  }

  async reactivate(type: string, id: string): Promise<LegendDto> {
    try {
      const envelope = await firstValueFrom(
        this.http.patch<ApiEnvelope<LegendDto>>(`${this.baseUrl}/${type}/${id}/reactivate`, {}),
      );
      return envelope.data;
    } catch (error) {
      throw this.wrapError(error);
    }
  }

  extractErrorMessage(error: unknown): string {
    if (error instanceof LegendsApiError) {
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

  private wrapError(error: unknown): LegendsApiError {
    if (error instanceof HttpErrorResponse) {
      return new LegendsApiError(error);
    }
    return new LegendsApiError(error);
  }
}

export class LegendsApiError extends Error {
  constructor(readonly sourceError: unknown) {
    super('Legends API error');
    this.name = 'LegendsApiError';
  }
}
