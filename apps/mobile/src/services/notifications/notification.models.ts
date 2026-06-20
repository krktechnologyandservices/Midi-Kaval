export type NotificationDto = {
  id?: string;
  eventType?: string | null;
  title?: string | null;
  body?: string | null;
  caseId?: string;
  resourceType?: string | null;
  resourceId?: string;
  isRead?: boolean;
  createdAtUtc?: string;
  readAtUtc?: string | null;
};

export type NotificationListResultDto = {
  items?: NotificationDto[] | null;
};

export type UnreadCountDto = {
  count: number;
};

export const TRAVEL_CLAIM_NOTIFICATION_EVENTS = [
  'travel.claim.approved',
  'travel.claim.returned',
] as const;

export type TravelClaimNotificationEvent =
  (typeof TRAVEL_CLAIM_NOTIFICATION_EVENTS)[number];

export function isTravelClaimNotificationEvent(
  eventType: string | null | undefined,
): eventType is TravelClaimNotificationEvent {
  return TRAVEL_CLAIM_NOTIFICATION_EVENTS.includes(
    (eventType ?? '') as TravelClaimNotificationEvent,
  );
}
