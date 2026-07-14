import { HttpClient, HttpErrorResponse } from '@angular/common/http';
import { Injectable, inject } from '@angular/core';
import { firstValueFrom } from 'rxjs';
import { environment } from '../../../../environments/environment';
import { ApiEnvelope } from '../../cases/models/case.models';
import {
  ReportExportJobDto,
  ReportExportRequest,
  ReportExportStatusDto,
} from '../reports.models';

@Injectable({ providedIn: 'root' })
export class ReportsApiService {
  private readonly http = inject(HttpClient);
  private readonly baseUrl = `${environment.apiBaseUrl}/api/v1/reports`;

  async startExport(type: string, request: ReportExportRequest): Promise<ReportExportJobDto> {
    try {
      const envelope = await firstValueFrom(
        this.http.post<ApiEnvelope<ReportExportJobDto>>(
          `${this.baseUrl}/${type}/export`,
          request,
        ),
      );
      return envelope.data;
    } catch (error) {
      throw this.wrapError(error);
    }
  }

  async getExportStatus(jobId: string): Promise<ReportExportStatusDto> {
    try {
      const envelope = await firstValueFrom(
        this.http.get<ApiEnvelope<ReportExportStatusDto>>(
          `${this.baseUrl}/exports/${jobId}/status`,
        ),
      );
      return envelope.data;
    } catch (error) {
      throw this.wrapError(error);
    }
  }

  async listExports(
    page = 1,
    pageSize = 20,
  ): Promise<{ items: ReportExportJobDto[]; totalCount: number }> {
    try {
      const envelope = await firstValueFrom(
        this.http.get<ApiEnvelope<{ items: ReportExportJobDto[] }>>(
          `${this.baseUrl}/exports`,
          { params: { page: String(page), pageSize: String(pageSize) } },
        ),
      );
      return { items: envelope.data.items, totalCount: envelope.meta?.totalCount ?? 0 };
    } catch (error) {
      throw this.wrapError(error);
    }
  }

  // downloadUrl comes from the backend as a root-relative path
  // (e.g. "/api/v1/reports/exports/{jobId}/file") — must go through HttpClient, not a
  // raw window.open()/anchor href, because the download endpoint requires the JWT
  // bearer token that only Angular's auth interceptor attaches; a plain browser
  // request bypasses it entirely and gets a silent 401.
  async downloadFile(downloadUrl: string): Promise<Blob> {
    try {
      return await firstValueFrom(
        this.http.get(`${environment.apiBaseUrl}${downloadUrl}`, { responseType: 'blob' }),
      );
    } catch (error) {
      throw this.wrapError(error);
    }
  }

  extractErrorMessage(error: unknown): string {
    if (error instanceof ReportsApiError) {
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

  private wrapError(error: unknown): ReportsApiError {
    if (error instanceof HttpErrorResponse) {
      if (error.status === 0 || error.status === undefined) {
        return new ReportsApiError('network', error);
      }
      return new ReportsApiError('http', error);
    }
    return new ReportsApiError('unknown', error);
  }
}

export class ReportsApiError extends Error {
  constructor(
    readonly kind: 'network' | 'http' | 'unknown',
    readonly sourceError: unknown,
  ) {
    super('Reports API error');
    this.name = 'ReportsApiError';
  }
}
