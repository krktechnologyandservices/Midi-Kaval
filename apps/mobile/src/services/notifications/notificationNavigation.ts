import {
  isTravelClaimNotificationEvent,
  NotificationDto,
} from './notification.models';

export type NotificationPayload = {
  notificationId?: string;
  eventType?: string;
  resourceId?: string;
};

export async function handleNotificationNavigation(
  payload: NotificationPayload,
  options: {
    markRead: (id: string) => Promise<void>;
    navigateToClaim: (claimId: string) => void;
    markReadBeforeNavigate?: boolean;
  },
): Promise<void> {
  const shouldMarkRead = options.markReadBeforeNavigate ?? true;
  if (shouldMarkRead && payload.notificationId) {
    await options.markRead(payload.notificationId);
  }

  navigateForNotificationPayload(payload, options.navigateToClaim);
}

export function navigateForNotificationPayload(
  payload: NotificationPayload,
  navigateToClaim: (claimId: string) => void,
): void {
  const eventType = payload.eventType ?? null;
  const resourceId = payload.resourceId;

  if (isTravelClaimNotificationEvent(eventType) && resourceId) {
    navigateToClaim(resourceId);
  }
}

export function toNotificationPayload(
  item: NotificationDto,
): NotificationPayload {
  return {
    notificationId: item.id,
    eventType: item.eventType ?? undefined,
    resourceId: item.resourceId,
  };
}
