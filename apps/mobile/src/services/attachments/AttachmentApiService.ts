import {
  ApiClientError,
  authSessionService,
  AuthSessionService,
} from '../auth/AuthSessionService';
import {AttachmentDto} from '../cases/case.models';
import {CaseApiError} from '../cases/CaseApiService';

export class AttachmentApiService {
  constructor(private readonly auth: AuthSessionService = authSessionService) {}

  /**
   * Uploads and encrypts a file in a single request. Replaces the old
   * presign → PUT-to-blob → confirm three-step dance — the API now sees the bytes
   * directly (necessary to encrypt them before they're stored) instead of handing out
   * a presigned URL for the client to PUT to.
   */
  async upload(request: {
    resourceType: 'CaseNote' | 'TravelClaim' | 'BudgetUtilization';
    resourceId: string;
    fileUri: string;
    fileName: string;
    contentType: string;
  }): Promise<AttachmentDto> {
    try {
      const formData = new FormData();
      formData.append('resourceType', request.resourceType);
      formData.append('resourceId', request.resourceId);
      // React Native's FormData accepts this {uri, name, type} shape directly and
      // streams from disk rather than buffering the whole file in JS memory.
      formData.append('file', {
        uri: request.fileUri,
        name: request.fileName,
        type: request.contentType,
      } as unknown as Blob);

      const envelope = await this.auth.postMultipartApi<AttachmentDto>(
        '/api/v1/attachments/upload',
        formData,
      );
      return envelope.data;
    } catch (error) {
      throw this.wrapError(error);
    }
  }

  /** Downloads and decrypts an attachment, returning its bytes as a Blob. */
  async download(attachmentId: string): Promise<Blob> {
    try {
      return await this.auth.getBinaryApi(`/api/v1/attachments/${attachmentId}/download`);
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
