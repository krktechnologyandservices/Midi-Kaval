import { HttpClient, HttpErrorResponse } from '@angular/common/http';
import { Injectable, inject } from '@angular/core';
import { firstValueFrom } from 'rxjs';
import { environment } from '../../../../environments/environment';
import { AuthSessionService } from '../../../core/auth/auth-session.service';
import {
  ApiEnvelope,
  AttachmentConfirmRequest,
  AttachmentDownloadUrlDto,
  AttachmentDto,
  AttachmentPresignRequest,
  AttachmentPresignResultDto,
} from '../models/case.models';
import { CaseApiError } from './case-api.service';

@Injectable({ providedIn: 'root' })
export class AttachmentApiService {
  private readonly http = inject(HttpClient);
  private readonly auth = inject(AuthSessionService);

  async presign(request: AttachmentPresignRequest): Promise<AttachmentPresignResultDto> {
    try {
      const envelope = await firstValueFrom(
        this.http.post<ApiEnvelope<AttachmentPresignResultDto>>(
          `${environment.apiBaseUrl}/api/v1/attachments/presign`,
          request,
        ),
      );
      return envelope.data;
    } catch (error) {
      throw this.wrapError(error);
    }
  }

  async confirm(request: AttachmentConfirmRequest): Promise<AttachmentDto> {
    try {
      const envelope = await firstValueFrom(
        this.http.post<ApiEnvelope<AttachmentDto>>(
          `${environment.apiBaseUrl}/api/v1/attachments/confirm`,
          request,
        ),
      );
      return envelope.data;
    } catch (error) {
      throw this.wrapError(error);
    }
  }

  async getDownloadUrl(attachmentId: string): Promise<AttachmentDownloadUrlDto> {
    try {
      const envelope = await firstValueFrom(
        this.http.get<ApiEnvelope<AttachmentDownloadUrlDto>>(
          `${environment.apiBaseUrl}/api/v1/attachments/${attachmentId}/download-url`,
        ),
      );
      return envelope.data;
    } catch (error) {
      throw this.wrapError(error);
    }
  }

  async uploadToPresignedUrl(
    uploadUrl: string,
    file: File,
    requiredHeaders: Record<string, string>,
  ): Promise<void> {
    const response = await fetch(uploadUrl, {
      method: 'PUT',
      headers: requiredHeaders,
      body: file,
    });

    if (!response.ok) {
      throw new Error(`Upload failed (${response.status})`);
    }
  }

  extractErrorMessage(error: unknown): string {
    if (error instanceof CaseApiError) {
      return this.auth.extractErrorMessage(error.sourceError);
    }

    if (
      error instanceof Error &&
      (error.message.startsWith('Upload failed') || error.message === 'Presign failed')
    ) {
      return 'Attachment upload failed. The note was saved without the file.';
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
