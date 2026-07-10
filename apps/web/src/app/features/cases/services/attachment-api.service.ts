import { HttpClient, HttpErrorResponse } from '@angular/common/http';
import { Injectable, inject } from '@angular/core';
import { firstValueFrom } from 'rxjs';
import { environment } from '../../../../environments/environment';
import { AuthSessionService } from '../../../core/auth/auth-session.service';
import { ApiEnvelope, AttachmentDto } from '../models/case.models';
import { CaseApiError } from './case-api.service';

@Injectable({ providedIn: 'root' })
export class AttachmentApiService {
  private readonly http = inject(HttpClient);
  private readonly auth = inject(AuthSessionService);

  async upload(request: {
    resourceType: 'CaseNote' | 'TravelClaim' | 'BudgetUtilization';
    resourceId: string;
    file: File;
  }): Promise<AttachmentDto> {
    try {
      const formData = new FormData();
      formData.append('resourceType', request.resourceType);
      formData.append('resourceId', request.resourceId);
      formData.append('file', request.file, request.file.name);

      const envelope = await firstValueFrom(
        this.http.post<ApiEnvelope<AttachmentDto>>(
          `${environment.apiBaseUrl}/api/v1/attachments/upload`,
          formData,
        ),
      );
      return envelope.data;
    } catch (error) {
      throw this.wrapError(error);
    }
  }

  async download(attachmentId: string): Promise<Blob> {
    try {
      return await firstValueFrom(
        this.http.get(`${environment.apiBaseUrl}/api/v1/attachments/${attachmentId}/download`, {
          responseType: 'blob',
        }),
      );
    } catch (error) {
      throw this.wrapError(error);
    }
  }

  extractErrorMessage(error: unknown): string {
    if (error instanceof CaseApiError) {
      return this.auth.extractErrorMessage(error.sourceError);
    }

    return this.auth.extractErrorMessage(error);
  }

  extractDownloadErrorMessage(error: unknown): string {
    if (error instanceof CaseApiError && error.status === 403) {
      return "You don't have permission to view this attachment.";
    }

    return this.extractErrorMessage(error);
  }

  private wrapError(error: unknown): CaseApiError {
    if (error instanceof HttpErrorResponse) {
      if (error.status === 0 || error.status === undefined) {
        return new CaseApiError('network', 0, error);
      }

      return new CaseApiError('http', error.status, error);
    }

    return new CaseApiError('unknown', 0, error);
  }
}
