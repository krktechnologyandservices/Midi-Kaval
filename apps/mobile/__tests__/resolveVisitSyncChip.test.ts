import {
  mutationTypeLabel,
  resolveVisitSyncChip,
  resolveVisitTitleFromCache,
  SYNC_CHIP_LABELS,
} from '../src/services/sync/resolveVisitSyncChip';
import {QueuedMutation} from '../src/services/sync/syncMutationTypes';

const visitId = '11111111-1111-4111-8111-111111111111';

function mutation(
  overrides: Partial<QueuedMutation> & Pick<QueuedMutation, 'syncStatus'>,
): QueuedMutation {
  return {
    clientMutationId: 'mut-1',
    type: 'visit.start',
    clientTimestampUtc: '2026-06-17T10:00:00Z',
    visitId,
    syncStatus: overrides.syncStatus,
    ...overrides,
  };
}

test('resolveVisitSyncChip returns synced when queue is empty', () => {
  const chip = resolveVisitSyncChip(visitId, []);
  expect(chip.state).toBe('synced');
  expect(chip.label).toBe(SYNC_CHIP_LABELS.synced);
  expect(chip.backgroundColor).toBe('#ECFDF3');
  expect(chip.color).toBe('#027A48');
});

test('resolveVisitSyncChip returns synced when visit has no queue rows', () => {
  const chip = resolveVisitSyncChip(visitId, [
    mutation({visitId: 'other-id', syncStatus: 'local'}),
  ]);
  expect(chip.state).toBe('synced');
});

test('resolveVisitSyncChip returns local when only local mutations exist', () => {
  const chip = resolveVisitSyncChip(visitId, [mutation({syncStatus: 'local'})]);
  expect(chip.state).toBe('local');
  expect(chip.label).toBe('Saved on this device');
  expect(chip.backgroundColor).toBe('#F4F3FF');
});

test('resolveVisitSyncChip returns pending when pending mutation exists', () => {
  const chip = resolveVisitSyncChip(visitId, [mutation({syncStatus: 'pending'})]);
  expect(chip.state).toBe('pending');
  expect(chip.label).toBe('Uploading');
});

test('resolveVisitSyncChip returns error when error mutation exists', () => {
  const chip = resolveVisitSyncChip(visitId, [
    mutation({syncStatus: 'error', lastError: 'Server rejected'}),
  ]);
  expect(chip.state).toBe('error');
  expect(chip.label).toBe('Sync failed');
  expect(chip.backgroundColor).toBe('#FEF3F2');
});

test('resolveVisitSyncChip prioritizes error over pending and local', () => {
  const chip = resolveVisitSyncChip(visitId, [
    mutation({clientMutationId: 'a', syncStatus: 'local'}),
    mutation({clientMutationId: 'b', syncStatus: 'pending'}),
    mutation({clientMutationId: 'c', syncStatus: 'error'}),
  ]);
  expect(chip.state).toBe('error');
});

test('resolveVisitSyncChip prioritizes pending over local', () => {
  const chip = resolveVisitSyncChip(visitId, [
    mutation({clientMutationId: 'a', syncStatus: 'local'}),
    mutation({clientMutationId: 'b', syncStatus: 'pending'}),
  ]);
  expect(chip.state).toBe('pending');
});

test('mutationTypeLabel maps visit mutation types', () => {
  expect(mutationTypeLabel('visit.start')).toBe('Start visit');
  expect(mutationTypeLabel('visit.complete')).toBe('Complete visit');
});

test('resolveVisitTitleFromCache uses crime and st numbers', () => {
  const title = resolveVisitTitleFromCache(visitId, [
    {
      id: visitId,
      case: {crimeNumber: 'CR-1', stNumber: 'ST-1'},
    },
  ]);
  expect(title).toBe('CR-1 · ST-1');
});

test('resolveVisitTitleFromCache falls back to short visit id', () => {
  const title = resolveVisitTitleFromCache(visitId, []);
  expect(title).toBe(`Visit ${visitId.slice(0, 8)}`);
});
