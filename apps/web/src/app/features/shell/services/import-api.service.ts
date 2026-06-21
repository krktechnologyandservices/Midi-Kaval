import { HttpClient, HttpErrorResponse } from '@angular/common/http';
import { Injectable, inject } from '@angular/core';
import { firstValueFrom } from 'rxjs';
import { environment } from '../../../../environments/environment';

export interface MigrationImportRowResult {
  rowIndex: number;
  crimeNumber: string | null;
  stNumber: string | null;
  status: string;
  reason: string;
}

export interface MigrationImportResult {
  totalRows: number;
  created: number;
  skipped: MigrationImportRowResult[];
  errors: MigrationImportRowResult[];
  importedAtUtc: string;
}

@Injectable({ providedIn: 'root' })
export class ImportApiService {
  private readonly http = inject(HttpClient);
  private readonly baseUrl = `${environment.apiBaseUrl}/api/v1/migration/import`;

  async import(
    file: File,
    dryRun: boolean,
  ): Promise<MigrationImportResult> {
    const formData = new FormData();
    formData.append('file', file);
    const params = dryRun ? '?dryRun=true' : '';

    try {
      return await firstValueFrom(
        this.http.post<MigrationImportResult>(
          `${this.baseUrl}${params}`,
          formData,
        ),
      );
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
