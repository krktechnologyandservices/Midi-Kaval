import { HttpClient, HttpErrorResponse } from '@angular/common/http';
import { Injectable, inject } from '@angular/core';
import { firstValueFrom } from 'rxjs';
import { environment } from '../../../../environments/environment';
import { ApiEnvelope, StaffDto, CreateStaffRequest, UpdateStaffRequest } from '../staff.models';

@Injectable({ providedIn: 'root' })
export class StaffApiService {
  private readonly http = inject(HttpClient);
  private readonly baseUrl = `${environment.apiBaseUrl}/api/v1/staff`;

  async list(): Promise<StaffDto[]> {
    try {
      const envelope = await firstValueFrom(
        this.http.get<ApiEnvelope<{ items: StaffDto[] }>>(this.baseUrl),
      );
      return envelope.data.items;
    } catch (error) {
      throw this.wrapError(error);
    }
  }

  async get(id: string): Promise<StaffDto> {
    try {
      const envelope = await firstValueFrom(
        this.http.get<ApiEnvelope<StaffDto>>(`${this.baseUrl}/${id}`),
      );
      return envelope.data;
    } catch (error) {
      throw this.wrapError(error);
    }
  }

  async create(request: CreateStaffRequest): Promise<StaffDto> {
    try {
      const envelope = await firstValueFrom(
        this.http.post<ApiEnvelope<StaffDto>>(this.baseUrl, request),
      );
      return envelope.data;
    } catch (error) {
      throw this.wrapError(error);
    }
  }

  async update(id: string, request: UpdateStaffRequest): Promise<StaffDto> {
    try {
      const envelope = await firstValueFrom(
        this.http.put<ApiEnvelope<StaffDto>>(`${this.baseUrl}/${id}`, request),
      );
      return envelope.data;
    } catch (error) {
      throw this.wrapError(error);
    }
  }

  async deactivate(id: string): Promise<void> {
    try {
      await firstValueFrom(
        this.http.delete(`${this.baseUrl}/${id}`),
      );
    } catch (error) {
      throw this.wrapError(error);
    }
  }

  async reactivate(id: string): Promise<StaffDto> {
    try {
      const envelope = await firstValueFrom(
        this.http.patch<ApiEnvelope<StaffDto>>(`${this.baseUrl}/${id}/reactivate`, {}),
      );
      return envelope.data;
    } catch (error) {
      throw this.wrapError(error);
    }
  }

  async forceReset(id: string): Promise<void> {
    try {
      await firstValueFrom(
        this.http.post(`${this.baseUrl}/${id}/force-reset`, {}),
      );
    } catch (error) {
      throw this.wrapError(error);
    }
  }

  extractErrorMessage(error: unknown): string {
    if (error instanceof StaffApiError) {
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
      return `Request failed (${error.status})`;
    }
    if (error instanceof Error) {
      return error.message;
    }
    return 'An unknown error occurred';
  }

  private wrapError(error: unknown): StaffApiError | HttpErrorResponse {
    if (error instanceof HttpErrorResponse) {
      return error;
    }
    return new StaffApiError(error);
  }
}

export class StaffApiError extends Error {
  constructor(readonly sourceError: unknown) {
    super('Staff API error');
    this.name = 'StaffApiError';
  }
}
