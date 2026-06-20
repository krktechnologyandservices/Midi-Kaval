import {
  mutationTypeLabel,
  resolveSyncQueueRowTitle,
} from '../src/services/sync/resolveSyncQueueLabel';
import {QueuedMutation} from '../src/services/sync/syncMutationTypes';

test('mutationTypeLabel returns travel copy', () => {
  expect(mutationTypeLabel('travel.claim.create')).toBe('Create travel claim');
});

test('resolveSyncQueueRowTitle formats travel destination and date', () => {
  const item: QueuedMutation = {
    clientMutationId: '11111111-1111-4111-8111-111111111111',
    type: 'travel.claim.create',
    clientTimestampUtc: '2026-06-20T10:00:00Z',
    localDraftKey: 'draft-1',
    claimDate: '2026-06-15T00:00:00Z',
    startLocation: 'Office',
    destination: 'District Court',
    transportMode: 'Bus',
    amount: 45.5,
    caseIds: ['22222222-2222-4222-8222-222222222222'],
    syncStatus: 'local',
  };

  expect(resolveSyncQueueRowTitle(item, [])).toBe('District Court · 2026-06-15');
});
