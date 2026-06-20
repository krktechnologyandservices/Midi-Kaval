import React from 'react';
import {Text} from 'react-native';
import ReactTestRenderer from 'react-test-renderer';
import {SuggestedRouteSheet} from '../src/components/SuggestedRouteSheet';

const visits = [
  {
    id: '11111111-1111-4111-8111-111111111111',
    scheduledAtUtc: '2026-06-17T09:00:00Z',
    status: 'Scheduled',
    isOverdue: false,
    case: {
      id: 'aaaaaaaa-aaaa-4aaa-8aaa-aaaaaaaaaaaa',
      crimeNumber: 'CR-1',
      stNumber: 'ST-1',
      domicile: 'Urban',
      gpsVerified: true,
      latitude: 12.9716,
      longitude: 77.5946,
    },
  },
  {
    id: '22222222-2222-4222-8222-222222222222',
    scheduledAtUtc: '2026-06-17T10:00:00Z',
    status: 'Scheduled',
    isOverdue: false,
    case: {
      id: 'bbbbbbbb-bbbb-4bbb-8bbb-bbbbbbbbbbbb',
      crimeNumber: 'CR-2',
      stNumber: 'ST-2',
      domicile: 'Urban',
      gpsVerified: true,
      latitude: 12.98,
      longitude: 77.5946,
    },
  },
];

test('shows message without apply when suggested order is empty', () => {
  let tree!: ReactTestRenderer.ReactTestRenderer;
  ReactTestRenderer.act(() => {
    tree = ReactTestRenderer.create(
      <SuggestedRouteSheet
        visible
        suggestion={{
          clusters: [],
          suggestedVisitOrder: [],
          legs: [],
          excluded: [],
          eligibleCount: 1,
          excludedCount: 0,
          message: 'At least two visits with verified GPS are required for route grouping',
        }}
        visits={visits}
        onCancel={jest.fn()}
        onApply={jest.fn()}
      />,
    );
  });

  const combined = tree.root
    .findAllByType(Text)
    .map(node => String(node.props.children))
    .join(' ');
  expect(combined).toContain('At least two visits with verified GPS');
  expect(combined).not.toContain('Apply route');
});

test('shows excluded warning and apply for suggested order', () => {
  const onApply = jest.fn();

  let tree!: ReactTestRenderer.ReactTestRenderer;
  ReactTestRenderer.act(() => {
    tree = ReactTestRenderer.create(
      <SuggestedRouteSheet
        visible
        suggestion={{
          clusters: [{clusterIndex: 0, visitIds: [visits[0].id!, visits[1].id!]}],
          suggestedVisitOrder: [visits[0].id!, visits[1].id!],
          legs: [
            {visitId: visits[0].id!, distanceKmFromPrevious: null},
            {visitId: visits[1].id!, distanceKmFromPrevious: 1.2},
          ],
          excluded: [{visitId: '33333333-3333-4333-8333-333333333333', reason: 'gps_unverified'}],
          eligibleCount: 2,
          excludedCount: 1,
        }}
        visits={[
          ...visits,
          {
            id: '33333333-3333-4333-8333-333333333333',
            scheduledAtUtc: '2026-06-17T11:00:00Z',
            status: 'Scheduled',
            isOverdue: false,
            case: {
              id: 'cccccccc-cccc-4ccc-8ccc-cccccccccccc',
              crimeNumber: 'CR-3',
              stNumber: 'ST-3',
              domicile: 'Urban',
              gpsVerified: false,
            },
          },
        ]}
        onCancel={jest.fn()}
        onApply={onApply}
      />,
    );
  });

  const combined = tree.root
    .findAllByType(Text)
    .map(node => String(node.props.children))
    .join(' ');
  expect(combined).toContain('visit(s) skipped');
  expect(combined).toContain('CR-3');
  expect(combined).toContain('Apply route');

  const applyButton = tree.root
    .findAllByProps({accessibilityRole: 'button'})
    .find(node => String(node.findAllByType(Text)[0]?.props.children) === 'Apply route');
  ReactTestRenderer.act(() => {
    applyButton?.props.onPress();
  });
  expect(onApply).toHaveBeenCalledWith([visits[0].id, visits[1].id]);
});
