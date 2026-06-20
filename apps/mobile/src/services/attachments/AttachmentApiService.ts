import {
  ApiClientError,
  authSessionService,
  AuthSessionService,
} from '../auth/AuthSessionService';
import {
  AttachmentConfirmRequest,
  AttachmentDownloadUrlDto,
  AttachmentDto,
  AttachmentPresignRequest,
  AttachmentPresignResultDto,
} from '../cases/case.models';
import {CaseApiError} from '../cases/CaseApiService';

export class AttachmentApiService {
  constructor(private readonly auth: AuthSessionService = authSessionService) {}

  async presign(request: AttachmentPresignRequest): Promise<AttachmentPresignResultDto> {
    try {
      const envelope = await this.auth.postApi<AttachmentPresignResultDto>(
        '/api/v1/attachments/presign',
        request,
      );
      return envelope.data;
    } catch (error) {
      throw this.wrapError(error);
    }
  }

  async confirm(request: AttachmentConfirmRequest): Promise<AttachmentDto> {
    try {
      const envelope = await this.auth.postApi<AttachmentDto>(
        '/api/v1/attachments/confirm',
        request,
      );
      return envelope.data;
    } catch (error) {
      throw this.wrapError(error);
    }
  }

  async getDownloadUrl(attachmentId: string): Promise<AttachmentDownloadUrlDto> {
    try {
      const envelope = await this.auth.getApi<AttachmentDownloadUrlDto>(
        `/api/v1/attachments/${attachmentId}/download-url`,
      );
      return envelope.data;
    } catch (error) {
      throw this.wrapError(error);
    }
  }

  async uploadToPresignedUrl(
    uploadUrl: string,
    body: Blob,
    requiredHeaders: Record<string, string>,
  ): Promise<void> {
    const response = await fetch(uploadUrl, {
      method: 'PUT',
      headers: requiredHeaders,
      body,
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
    if (error instanceof ApiClientError) {
      if (error.status === 0) {
        return new CaseApiError('network', 0, error);
      }

      return new CaseApiError('http', error.status, error);
    }

    return new CaseApiError('unknown', 0, error);
  }
}

export const attachmentApiService = new AttachmentApiService();
