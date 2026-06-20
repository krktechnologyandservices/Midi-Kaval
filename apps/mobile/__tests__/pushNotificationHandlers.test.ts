import {
  handleNotificationNavigation,
  navigateForNotificationPayload,
  NotificationPayload,
} from '../src/services/notifications/notificationNavigation';
import {parsePushData} from '../src/services/devices/pushNotificationHandlers';

describe('notificationNavigation', () => {
  test('handleNotificationNavigation marks read and navigates for travel claim', async () => {
    const markRead = jest.fn().mockResolvedValue(undefined);
    const navigateToClaim = jest.fn();

    await handleNotificationNavigation(
      {
        notificationId: '11111111-1111-4111-8111-111111111111',
        eventType: 'travel.claim.approved',
        resourceId: '22222222-2222-4222-8222-222222222222',
      },
      {markRead, navigateToClaim},
    );

    expect(markRead).toHaveBeenCalledWith('11111111-1111-4111-8111-111111111111');
    expect(navigateToClaim).toHaveBeenCalledWith('22222222-2222-4222-8222-222222222222');
  });

  test('navigateForNotificationPayload ignores unknown events', () => {
    const navigateToClaim = jest.fn();
    navigateForNotificationPayload(
      {eventType: 'intervention.overdue', resourceId: 'abc'},
      navigateToClaim,
    );
    expect(navigateToClaim).not.toHaveBeenCalled();
  });
});

describe('parsePushData', () => {
  test('parses string payload fields', () => {
    expect(
      parsePushData({
        notificationId: '11111111-1111-4111-8111-111111111111',
        eventType: 'travel.claim.returned',
        resourceId: '22222222-2222-4222-8222-222222222222',
      }),
    ).toEqual({
      notificationId: '11111111-1111-4111-8111-111111111111',
      eventType: 'travel.claim.returned',
      resourceId: '22222222-2222-4222-8222-222222222222',
    } satisfies NotificationPayload);
  });
});

describe('push notification handler navigation callback', () => {
  test('nested More stack navigation uses travel claim view mode', async () => {
    const navigationRef = {
      isReady: () => true,
      navigate: jest.fn(),
    };
    const markRead = jest.fn().mockResolvedValue(undefined);

    await handleNotificationNavigation(
      {
        notificationId: '33333333-3333-4333-8333-333333333333',
        eventType: 'travel.claim.approved',
        resourceId: '44444444-4444-4444-8444-444444444444',
      },
      {
        markRead,
        navigateToClaim: claimId => {
          navigationRef.navigate('More', {
            screen: 'TravelClaimForm',
            params: {claimId, mode: 'view'},
          });
        },
      },
    );

    expect(navigationRef.navigate).toHaveBeenCalledWith('More', {
      screen: 'TravelClaimForm',
      params: {
        claimId: '44444444-4444-4444-8444-444444444444',
        mode: 'view',
      },
    });
  });
});
