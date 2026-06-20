import {
  ApiClientError,
  authSessionService,
  AuthSessionService,
} from '../auth/AuthSessionService';
import {
  NotificationDto,
  NotificationListResultDto,
  UnreadCountDto,
} from './notification.models';

export class NotificationApiService {
  constructor(private readonly auth: AuthSessionService = authSessionService) {}

  async list(): Promise<NotificationDto[]> {
    try {
      const envelope = await this.auth.getApi<NotificationListResultDto>(
        '/api/v1/notifications',
      );
      return envelope.data.items ?? [];
    } catch (error) {
      throw this.wrapError(error);
    }
  }

  async getUnreadCount(): Promise<number> {
    try {
      const envelope = await this.auth.getApi<UnreadCountDto>(
        '/api/v1/notifications/unread-count',
      );
      return envelope.data.count;
    } catch (error) {
      throw this.wrapError(error);
    }
  }

  async markRead(notificationId: string): Promise<NotificationDto> {
    try {
      const envelope = await this.auth.patchApi<NotificationDto>(
        `/api/v1/notifications/${notificationId}/read`,
        {},
      );
      return envelope.data;
    } catch (error) {
      throw this.wrapError(error);
    }
  }

  extractErrorMessage(error: unknown): string {
    if (error instanceof NotificationApiError) {
      if (error.kind === 'network') {
        return 'Could not reach the server — check connection and try again.';
      }

      return this.auth.extractErrorMessage(error.sourceError);
    }

    return this.auth.extractErrorMessage(error);
  }

  private wrapError(error: unknown): NotificationApiError {
    if (error instanceof ApiClientError) {
      if (error.status === 0) {
        return new NotificationApiError('network', 0, error);
      }

      return new NotificationApiError('http', error.status, error);
    }

    return new NotificationApiError('unknown', 0, error);
  }
}

export class NotificationApiError extends Error {
  constructor(
    public readonly kind: 'network' | 'http' | 'unknown',
    public readonly status: number,
    public readonly sourceError: unknown,
  ) {
    super('Notification API error');
    this.name = 'NotificationApiError';
  }
}

export const notificationApiService = new NotificationApiService();
