import {
  NotificationApiService,
} from '../NotificationApiService';
import {
  AuthSessionService,
} from '../../auth/AuthSessionService';

function createMockAuth(response: unknown): AuthSessionService {
  return {
    getApi: jest.fn().mockResolvedValue(response),
  } as unknown as AuthSessionService;
}

describe('NotificationApiService', () => {
  describe('getUnreadCount', () => {
    it('returns count from the API response', async () => {
      const auth = createMockAuth({ data: { count: 3 } });
      const service = new NotificationApiService(auth);
      const count = await service.getUnreadCount();
      expect(count).toBe(3);
      expect(auth.getApi).toHaveBeenCalledWith('/api/v1/notifications/unread-count');
    });

    it('returns 0 when API returns 0', async () => {
      const auth = createMockAuth({ data: { count: 0 } });
      const service = new NotificationApiService(auth);
      const count = await service.getUnreadCount();
      expect(count).toBe(0);
    });

    it('wraps API errors', async () => {
      const auth = {
        getApi: jest.fn().mockRejectedValue({ status: 0 }),
        extractErrorMessage: jest.fn().mockReturnValue('Network error'),
      } as unknown as AuthSessionService;

      const service = new NotificationApiService(auth);
      await expect(service.getUnreadCount()).rejects.toThrow();
    });
  });
});
