import {mergeQueueWithTravelClaims} from '../src/services/sync/mergeQueueWithTravelClaims';
import {QueuedMutation} from '../src/services/sync/syncMutationTypes';

test('mergeQueueWithTravelClaims prepends local draft rows', () => {
  const queue: QueuedMutation[] = [
    {
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
    },
  ];

  const merged = mergeQueueWithTravelClaims(
    [
      {
        id: '33333333-3333-4333-8333-333333333333',
        destination: 'Server claim',
        status: 'Submitted',
      },
    ],
    queue,
  );

  expect(merged).toHaveLength(2);
  expect(merged[0].isLocalOnly).toBe(true);
  expect(merged[0].destination).toBe('District Court');
  expect(merged[1].destination).toBe('Server claim');
});
