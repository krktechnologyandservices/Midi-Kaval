import React from 'react';
import ReactTestRenderer from 'react-test-renderer';
import {Text} from 'react-native';
import {useCourtCountdown} from '../src/hooks/useCourtCountdown';
import {courtApiService} from '../src/services/court/CourtApiService';

jest.mock('../src/services/court/CourtApiService', () => ({
  courtApiService: {
    listUpcomingCourtSittings: jest.fn(),
  },
}));

function HookProbe({
  enabled,
  refreshToken,
}: {
  enabled: boolean;
  refreshToken?: number;
}): React.JSX.Element {
  const {label} = useCourtCountdown({enabled, refreshToken});
  return <Text>{label ?? 'none'}</Text>;
}

beforeEach(() => {
  jest.clearAllMocks();
});

test('returns null label when disabled', async () => {
  let tree!: ReactTestRenderer.ReactTestRenderer;
  await ReactTestRenderer.act(() => {
    tree = ReactTestRenderer.create(<HookProbe enabled={false} />);
  });

  await ReactTestRenderer.act(async () => {
    await Promise.resolve();
  });

  expect(tree.root.findByType(Text).props.children).toBe('none');
  expect(courtApiService.listUpcomingCourtSittings).not.toHaveBeenCalled();
});

test('derives banner label from first upcoming item', async () => {
  (courtApiService.listUpcomingCourtSittings as jest.Mock).mockResolvedValue([
    {
      id: 'sitting-1',
      courtName: 'District Court',
      scheduledAtUtc: '2020-01-01T10:00:00Z',
      isPastDue: true,
    },
  ]);

  let tree!: ReactTestRenderer.ReactTestRenderer;
  await ReactTestRenderer.act(() => {
    tree = ReactTestRenderer.create(<HookProbe enabled={true} refreshToken={1} />);
  });

  await ReactTestRenderer.act(async () => {
    await Promise.resolve();
  });

  expect(String(tree.root.findByType(Text).props.children)).toContain(
    'Court sitting overdue — District Court',
  );
});

test('refreshes when refreshToken changes', async () => {
  (courtApiService.listUpcomingCourtSittings as jest.Mock).mockResolvedValue([]);

  let tree!: ReactTestRenderer.ReactTestRenderer;
  await ReactTestRenderer.act(() => {
    tree = ReactTestRenderer.create(<HookProbe enabled={true} refreshToken={1} />);
  });

  await ReactTestRenderer.act(async () => {
    await Promise.resolve();
  });

  await ReactTestRenderer.act(() => {
    tree.update(<HookProbe enabled={true} refreshToken={2} />);
  });

  await ReactTestRenderer.act(async () => {
    await Promise.resolve();
  });

  expect(courtApiService.listUpcomingCourtSittings).toHaveBeenCalledTimes(2);
});
