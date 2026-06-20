import {mergeQueueWithVisits} from '../src/services/sync/mergeQueueWithVisits';
import {VisitListItemDto} from '../src/services/visits/visit.models';
import {QueuedMutation} from '../src/services/sync/syncMutationTypes';

const baseVisit: VisitListItemDto = {
  id: '11111111-1111-4111-8111-111111111111',
  scheduledAtUtc: '2026-06-17T09:00:00Z',
  status: 'Scheduled',
  isOverdue: false,
  case: {
    id: '22222222-2222-4222-8222-222222222222',
    crimeNumber: 'CR-1',
    stNumber: 'ST-1',
    domicile: 'Urban',
    gpsVerified: true,
  },
};

test('mergeQueueWithVisits ignores error-state queue items', () => {
  const queue: QueuedMutation[] = [
    {
      clientMutationId: 'a',
      type: 'visit.start',
      clientTimestampUtc: '2026-06-17T09:00:00Z',
      visitId: baseVisit.id!,
      syncStatus: 'error',
      lastError: 'Sync rejected.',
    },
  ];

  const merged = mergeQueueWithVisits([baseVisit], queue)[0];
  expect(merged.status).toBe('Scheduled');
});

test('mergeQueueWithVisits applies start then complete overlays', () => {
  const queue: QueuedMutation[] = [
    {
      clientMutationId: 'a',
      type: 'visit.start',
      clientTimestampUtc: '2026-06-17T09:00:00Z',
      visitId: baseVisit.id!,
      syncStatus: 'local',
    },
    {
      clientMutationId: 'b',
      type: 'visit.complete',
      clientTimestampUtc: '2026-06-17T10:00:00Z',
      visitId: baseVisit.id!,
      note: 'Completed offline',
      noteClientTimestampUtc: '2026-06-17T10:00:00Z',
      syncStatus: 'local',
    },
  ];

  const merged = mergeQueueWithVisits([baseVisit], queue)[0];
  expect(merged.status).toBe('Completed');
  expect(merged.completionNote).toBe('Completed offline');
});
