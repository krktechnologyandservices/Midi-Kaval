import React from 'react';
import {Pressable, Text} from 'react-native';
import ReactTestRenderer from 'react-test-renderer';
import {CommandStripCard} from '../src/components/CommandStripCard';
import {resolveVisitSyncChip} from '../src/services/sync/resolveVisitSyncChip';

const baseVisit = {
  id: '11111111-1111-4111-8111-111111111111',
  scheduledAtUtc: '2026-06-16T09:00:00Z',
  status: 'Scheduled',
  isOverdue: false,
  case: {
    id: '22222222-2222-4222-8222-222222222222',
    crimeNumber: 'CR-4521',
    stNumber: 'ST-8842',
    domicile: 'Slum',
  },
};

const syncedChip = resolveVisitSyncChip(baseVisit.id, []);

test('command strip card renders crime line and meta', () => {
  let tree!: ReactTestRenderer.ReactTestRenderer;
  ReactTestRenderer.act(() => {
    tree = ReactTestRenderer.create(
      <CommandStripCard
        visit={baseVisit}
        visitIndex={0}
        syncChip={syncedChip}
        onNavigate={jest.fn()}
        onStartVisit={jest.fn()}
        onOpenCase={jest.fn()}
      />,
    );
  });

  const combined = tree.root
    .findAllByType(Text)
    .map(node => String(node.props.children))
    .join(' ');
  expect(combined).toContain('CR-4521');
  expect(combined).toContain('ST-8842');
  expect(combined).toContain('Visit 1');
  expect(combined).toContain('Slum');
  expect(combined).toContain('Synced');
});

test('command strip card shows handoff whisper when present', () => {
  const visit = {
    ...baseVisit,
    handoffWhisper: {
      priorActions: 'Vocational referral pending',
      openItems: 'Family follow-up',
      nextVisitPurpose: 'First home visit',
      transferredAtUtc: '2026-06-15T10:00:00Z',
    },
  };

  let tree!: ReactTestRenderer.ReactTestRenderer;
  ReactTestRenderer.act(() => {
    tree = ReactTestRenderer.create(
      <CommandStripCard
        visit={visit}
        visitIndex={0}
        syncChip={syncedChip}
        onNavigate={jest.fn()}
        onStartVisit={jest.fn()}
        onOpenCase={jest.fn()}
      />,
    );
  });

  const combined = tree.root
    .findAllByType(Text)
    .map(node => String(node.props.children))
    .join(' ');
  expect(combined).toContain('Handoff:');
  expect(combined).toContain('Vocational referral pending');
});

test('command strip card applies overdue border when isOverdue', () => {
  const visit = {...baseVisit, isOverdue: true};

  let tree!: ReactTestRenderer.ReactTestRenderer;
  ReactTestRenderer.act(() => {
    tree = ReactTestRenderer.create(
      <CommandStripCard
        visit={visit}
        visitIndex={1}
        syncChip={syncedChip}
        onNavigate={jest.fn()}
        onStartVisit={jest.fn()}
        onOpenCase={jest.fn()}
      />,
    );
  });

  const card = tree.root.findByProps({accessibilityLabel: 'Visit card 2'});
  const flatStyle = Array.isArray(card.props.style)
    ? Object.assign({}, ...card.props.style.filter(Boolean))
    : card.props.style;
  expect(flatStyle.borderLeftColor).toBe('#B42318');
});

test('shows continue visit label when status is in progress', () => {
  const visit = {...baseVisit, status: 'InProgress'};

  let tree!: ReactTestRenderer.ReactTestRenderer;
  ReactTestRenderer.act(() => {
    tree = ReactTestRenderer.create(
      <CommandStripCard
        visit={visit}
        visitIndex={0}
        syncChip={syncedChip}
        onNavigate={jest.fn()}
        onStartVisit={jest.fn()}
        onOpenCase={jest.fn()}
      />,
    );
  });

  const combined = tree.root
    .findAllByType(Text)
    .map(node => String(node.props.children))
    .join(' ');
  expect(combined).toContain('Continue visit');
});

test('shows gps unverified suffix on meta line', () => {
  const visit = {...baseVisit, case: {...baseVisit.case, gpsVerified: false}};

  let tree!: ReactTestRenderer.ReactTestRenderer;
  ReactTestRenderer.act(() => {
    tree = ReactTestRenderer.create(
      <CommandStripCard
        visit={visit}
        visitIndex={0}
        syncChip={syncedChip}
        onNavigate={jest.fn()}
        onStartVisit={jest.fn()}
        onOpenCase={jest.fn()}
      />,
    );
  });

  const combined = tree.root
    .findAllByType(Text)
    .map(node => String(node.props.children))
    .join(' ');
  expect(combined).toContain('GPS unverified');
});

test('shows distance suffix when distanceKm provided for verified visit', () => {
  const visit = {...baseVisit, case: {...baseVisit.case, gpsVerified: true}};

  let tree!: ReactTestRenderer.ReactTestRenderer;
  ReactTestRenderer.act(() => {
    tree = ReactTestRenderer.create(
      <CommandStripCard
        visit={visit}
        visitIndex={1}
        syncChip={syncedChip}
        distanceKm={1.2}
        onNavigate={jest.fn()}
        onStartVisit={jest.fn()}
        onOpenCase={jest.fn()}
      />,
    );
  });

  const combined = tree.root
    .findAllByType(Text)
    .map(node => String(node.props.children))
    .join(' ');
  expect(combined).toContain('1.2 km');
});

test('start visit button calls handler', () => {
  const onStartVisit = jest.fn();

  let tree!: ReactTestRenderer.ReactTestRenderer;
  ReactTestRenderer.act(() => {
    tree = ReactTestRenderer.create(
      <CommandStripCard
        visit={baseVisit}
        visitIndex={0}
        syncChip={syncedChip}
        onNavigate={jest.fn()}
        onStartVisit={onStartVisit}
        onOpenCase={jest.fn()}
      />,
    );
  });

  const startButton = tree.root
    .findAllByProps({accessibilityLabel: 'Start visit'})
    .find(node => node.props.onPress);
  ReactTestRenderer.act(() => {
    startButton?.props.onPress();
  });

  expect(onStartVisit).toHaveBeenCalled();
});

test('renders local sync chip label', () => {
  const localChip = resolveVisitSyncChip(baseVisit.id, [
    {
      clientMutationId: 'mut-1',
      type: 'visit.start',
      clientTimestampUtc: '2026-06-17T10:00:00Z',
      visitId: baseVisit.id,
      syncStatus: 'local',
    },
  ]);

  let tree!: ReactTestRenderer.ReactTestRenderer;
  ReactTestRenderer.act(() => {
    tree = ReactTestRenderer.create(
      <CommandStripCard
        visit={baseVisit}
        visitIndex={0}
        syncChip={localChip}
        onNavigate={jest.fn()}
        onStartVisit={jest.fn()}
        onOpenCase={jest.fn()}
      />,
    );
  });

  const combined = tree.root
    .findAllByType(Text)
    .map(node => String(node.props.children))
    .join(' ');
  expect(combined).toContain('Saved on this device');
});

test('renders pending sync chip label', () => {
  const pendingChip = resolveVisitSyncChip(baseVisit.id, [
    {
      clientMutationId: 'mut-1',
      type: 'visit.start',
      clientTimestampUtc: '2026-06-17T10:00:00Z',
      visitId: baseVisit.id,
      syncStatus: 'pending',
    },
  ]);

  let tree!: ReactTestRenderer.ReactTestRenderer;
  ReactTestRenderer.act(() => {
    tree = ReactTestRenderer.create(
      <CommandStripCard
        visit={baseVisit}
        visitIndex={0}
        syncChip={pendingChip}
        onNavigate={jest.fn()}
        onStartVisit={jest.fn()}
        onOpenCase={jest.fn()}
      />,
    );
  });

  const combined = tree.root
    .findAllByType(Text)
    .map(node => String(node.props.children))
    .join(' ');
  expect(combined).toContain('Uploading');
});

test('error sync chip press calls onSyncChipPress', () => {
  const onSyncChipPress = jest.fn();
  const errorChip = resolveVisitSyncChip(baseVisit.id, [
    {
      clientMutationId: 'mut-1',
      type: 'visit.start',
      clientTimestampUtc: '2026-06-17T10:00:00Z',
      visitId: baseVisit.id,
      syncStatus: 'error',
      lastError: 'failed',
    },
  ]);

  let tree!: ReactTestRenderer.ReactTestRenderer;
  ReactTestRenderer.act(() => {
    tree = ReactTestRenderer.create(
      <CommandStripCard
        visit={baseVisit}
        visitIndex={0}
        syncChip={errorChip}
        onNavigate={jest.fn()}
        onStartVisit={jest.fn()}
        onOpenCase={jest.fn()}
        onSyncChipPress={onSyncChipPress}
      />,
    );
  });

  const errorButton = tree.root.findByProps({accessibilityLabel: 'Sync failed'});
  ReactTestRenderer.act(() => {
    errorButton.props.onPress();
  });

  expect(onSyncChipPress).toHaveBeenCalled();
});

test('synced chip is not pressable', () => {
  let tree!: ReactTestRenderer.ReactTestRenderer;
  ReactTestRenderer.act(() => {
    tree = ReactTestRenderer.create(
      <CommandStripCard
        visit={baseVisit}
        visitIndex={0}
        syncChip={syncedChip}
        onNavigate={jest.fn()}
        onStartVisit={jest.fn()}
        onOpenCase={jest.fn()}
        onSyncChipPress={jest.fn()}
      />,
    );
  });

  const syncedChipNode = tree.root.findByProps({accessibilityLabel: 'Synced'});
  expect(syncedChipNode.type).not.toBe(Pressable);
});

test('POCSO command strip hides ST and domicile in headline/meta', () => {
  const visit = {
    ...baseVisit,
    case: {
      ...baseVisit.case,
      beneficiaryName: 'R. K.',
      sensitivityLevel: 'POCSO',
    },
  };

  let tree!: ReactTestRenderer.ReactTestRenderer;
  ReactTestRenderer.act(() => {
    tree = ReactTestRenderer.create(
      <CommandStripCard
        visit={visit}
        visitIndex={0}
        syncChip={syncedChip}
        onNavigate={jest.fn()}
        onStartVisit={jest.fn()}
        onOpenCase={jest.fn()}
      />,
    );
  });

  const combined = tree.root
    .findAllByType(Text)
    .map(node => String(node.props.children))
    .join(' ');
  expect(combined).toContain('R. K.');
  expect(combined).toContain('CR-4521');
  expect(combined).not.toContain('ST-8842');
  expect(combined).not.toContain('Slum');
});
