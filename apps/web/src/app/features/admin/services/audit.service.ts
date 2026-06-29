import { HttpClient, HttpErrorResponse, HttpParams } from '@angular/common/http';
import { Injectable, inject } from '@angular/core';
import { firstValueFrom } from 'rxjs';
import { environment } from '../../../../environments/environment';
import { AuditListResultDto, AuditLogFilter } from '../models/audit.models';

export interface AuditMeta {
  requestId: string;
  totalCount?: number | null;
  page?: number | null;
  pageSize?: number | null;
}

export interface AuditEnvelope {
  data: AuditListResultDto;
  meta: AuditMeta;
}

@Injectable({ providedIn: 'root' })
export class AuditApiService {
  private readonly http = inject(HttpClient);
  private readonly baseUrl = `${environment.apiBaseUrl}/api/v1/admin/audit`;

  async list(filters: AuditLogFilter): Promise<{ items: AuditListResultDto['items']; meta: AuditMeta }> {
    try {
      let params = new HttpParams();
      if (filters.eventType) params = params.set('eventType', filters.eventType);
      if (filters.actorUserId) params = params.set('actorUserId', filters.actorUserId);
      if (filters.subjectUserId) params = params.set('subjectUserId', filters.subjectUserId);
      if (filters.from) params = params.set('from', filters.from);
      if (filters.to) params = params.set('to', filters.to);
      if (filters.page != null) params = params.set('page', filters.page);
      if (filters.pageSize != null) params = params.set('pageSize', filters.pageSize);

      const envelope = await firstValueFrom(
        this.http.get<AuditEnvelope>(this.baseUrl, { params }),
      );
      if (!envelope?.data || !envelope?.meta) {
        throw new Error('Malformed API response — missing data or meta');
      }
      return { items: envelope.data.items, meta: envelope.meta };
    } catch (error) {
      throw error;
    }
  }

  extractErrorMessage(error: unknown): string {
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
}
