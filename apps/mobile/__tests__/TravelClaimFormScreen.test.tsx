import React from 'react';
import ReactTestRenderer from 'react-test-renderer';
import {Pressable, Text} from 'react-native';
import {TravelClaimFormScreen} from '../src/screens/travel/TravelClaimFormScreen';
import {caseApiService} from '../src/services/cases/CaseApiService';
import {travelClaimApiService} from '../src/services/travel/TravelClaimApiService';
import {isDeviceOffline} from '../src/services/sync/networkStatus';
import {RECEIPT_REQUIRED_MESSAGE} from '../src/services/travel/travel.models';

const mockNavigate = jest.fn();
const mockReplace = jest.fn();
const mockRouteParams: {
  claimId?: string;
  localDraftKey?: string;
  mode?: 'create' | 'edit' | 'view';
} = {mode: 'create'};

jest.mock('@react-navigation/native', () => ({
  useNavigation: () => ({
    navigate: mockNavigate,
    replace: mockReplace,
  }),
  useRoute: () => ({
    params: mockRouteParams,
  }),
}));

jest.mock('../src/services/cases/CaseApiService', () => ({
  caseApiService: {
    listAssignedCases: jest.fn(),
  },
}));

jest.mock('../src/services/travel/TravelClaimApiService', () => ({
  travelClaimApiService: {
    create: jest.fn(),
    createOfflineDraft: jest.fn(),
    get: jest.fn(),
    extractErrorMessage: jest.fn((error: {message?: string}) => error.message ?? 'error'),
  },
  TravelClaimApiError: jest.fn().mockImplementation(function TravelClaimApiError(
    this: {kind: string; status: number; sourceError: unknown},
    kind: string,
    status: number,
    sourceError: unknown,
  ) {
    this.kind = kind;
    this.status = status;
    this.sourceError = sourceError;
  }),
}));

jest.mock('../src/services/sync/networkStatus', () => ({
  isDeviceOffline: jest.fn(async () => false),
}));

jest.mock('../src/services/sync/offlineQueue', () => ({
  readOfflineQueue: jest.fn(async () => []),
  findTravelDraftByKey: jest.fn(),
}));

jest.mock('../src/services/sync/useSyncOnForeground', () => ({
  useSyncOnForeground: jest.fn(),
}));

jest.mock('react-native-document-picker', () => ({
  types: {images: 'images', pdf: 'pdf'},
  pickSingle: jest.fn(),
  isCancel: () => false,
}));

jest.mock('@react-native-community/datetimepicker', () => 'DateTimePicker');

beforeEach(() => {
  jest.clearAllMocks();
  mockRouteParams.claimId = undefined;
  mockRouteParams.localDraftKey = undefined;
  mockRouteParams.mode = 'create';
  (caseApiService.listAssignedCases as jest.Mock).mockResolvedValue({
    items: [
      {
        id: '22222222-2222-4222-8222-222222222222',
        crimeNumber: 'CR-1',
        stNumber: 'ST-1',
      },
    ],
  });
});

function screenText(tree: ReactTestRenderer.ReactTestRenderer): string {
  return tree.root
    .findAllByType(Text)
    .map(node => node.props.children)
    .flat()
    .filter((value): value is string => typeof value === 'string')
    .join(' ');
}

test('requires auto number when transport mode is Auto', async () => {
  let tree!: ReactTestRenderer.ReactTestRenderer;
  await ReactTestRenderer.act(() => {
    tree = ReactTestRenderer.create(<TravelClaimFormScreen />);
  });

  await ReactTestRenderer.act(async () => {
    await Promise.resolve();
  });

  const autoChip = tree.root
    .findAllByType(Pressable)
    .find(node => node.props.accessibilityLabel === 'Transport mode Auto');
  expect(autoChip).toBeTruthy();

  await ReactTestRenderer.act(() => {
    autoChip?.props.onPress();
  });

  const inputs = tree.root.findAll(node => node.type === 'TextInput');
  await ReactTestRenderer.act(() => {
    inputs[0].props.onChangeText('Office');
    inputs[1].props.onChangeText('District Court');
    inputs[2].props.onChangeText('45.5');
  });

  const caseRow = tree.root.find(
    node => node.props.accessibilityRole === 'checkbox',
  );
  await ReactTestRenderer.act(() => {
    caseRow.props.onPress();
  });

  const saveButton = tree.root.find(
    node => node.props.accessibilityLabel === 'Save draft',
  );
  await ReactTestRenderer.act(() => {
    saveButton.props.onPress();
  });

  expect(screenText(tree)).toContain('autoNumber is required when transportMode is Auto.');
});

test('enqueues offline draft when device is offline on create', async () => {
  (isDeviceOffline as jest.Mock).mockResolvedValue(true);
  (travelClaimApiService.createOfflineDraft as jest.Mock).mockResolvedValue({
    localDraftKey: 'draft-1',
  });

  let tree!: ReactTestRenderer.ReactTestRenderer;
  await ReactTestRenderer.act(() => {
    tree = ReactTestRenderer.create(<TravelClaimFormScreen />);
  });

  await ReactTestRenderer.act(async () => {
    await Promise.resolve();
  });

  const inputs = tree.root.findAll(node => node.type === 'TextInput');
  await ReactTestRenderer.act(() => {
    inputs[0].props.onChangeText('Office');
    inputs[1].props.onChangeText('District Court');
    inputs[2].props.onChangeText('45.5');
  });

  const caseRow = tree.root.find(
    node => node.props.accessibilityRole === 'checkbox',
  );
  await ReactTestRenderer.act(() => {
    caseRow.props.onPress();
  });

  const saveButton = tree.root.find(
    node => node.props.accessibilityLabel === 'Save draft',
  );
  await ReactTestRenderer.act(() => {
    saveButton.props.onPress();
  });

  await ReactTestRenderer.act(async () => {
    await Promise.resolve();
  });

  expect(travelClaimApiService.createOfflineDraft).toHaveBeenCalled();
  expect(mockNavigate).toHaveBeenCalledWith('TravelClaimsList');
});

test('disables submit when receipt is required but missing', async () => {
  mockRouteParams.claimId = '33333333-3333-4333-8333-333333333333';
  mockRouteParams.mode = 'edit';
  (travelClaimApiService.get as jest.Mock).mockResolvedValue({
    id: '33333333-3333-4333-8333-333333333333',
    status: 'Draft',
    transportMode: 'Bus',
    attachments: [],
    claimDate: '2026-06-15',
    startLocation: 'Office',
    destination: 'District Court',
    amount: 45.5,
    caseIds: ['22222222-2222-4222-8222-222222222222'],
  });

  let tree!: ReactTestRenderer.ReactTestRenderer;
  await ReactTestRenderer.act(() => {
    tree = ReactTestRenderer.create(<TravelClaimFormScreen />);
  });

  await ReactTestRenderer.act(async () => {
    await Promise.resolve();
  });

  const submitButton = tree.root.find(
    node => node.props.accessibilityLabel === 'Submit claim',
  );
  expect(submitButton.props.disabled).toBe(true);
  expect(screenText(tree)).toContain(RECEIPT_REQUIRED_MESSAGE);
});

test('read-only approved claim shows status chip and director feedback', async () => {
  mockRouteParams.claimId = '44444444-4444-4444-8444-444444444444';
  mockRouteParams.mode = 'view';
  (travelClaimApiService.get as jest.Mock).mockResolvedValue({
    id: '44444444-4444-4444-8444-444444444444',
    status: 'Approved',
    transportMode: 'Bus',
    claimDate: '2026-06-15',
    startLocation: 'Office',
    destination: 'District Court',
    amount: 45.5,
    caseIds: ['22222222-2222-4222-8222-222222222222'],
    decisionComment: 'Approved for June visit',
    decidedAtUtc: '2026-06-16T10:00:00Z',
  });

  let tree!: ReactTestRenderer.ReactTestRenderer;
  await ReactTestRenderer.act(() => {
    tree = ReactTestRenderer.create(<TravelClaimFormScreen />);
  });

  await ReactTestRenderer.act(async () => {
    await Promise.resolve();
  });

  const text = screenText(tree);
  expect(text).toContain('Approved');
  expect(text).toContain('Director feedback');
  expect(text).toContain('Approved for June visit');
});

test('read-only approved claim without decision comment shows fallback message', async () => {
  mockRouteParams.claimId = '77777777-7777-4777-8777-777777777777';
  mockRouteParams.mode = 'view';
  (travelClaimApiService.get as jest.Mock).mockResolvedValue({
    id: '77777777-7777-4777-8777-777777777777',
    status: 'Approved',
    transportMode: 'Bus',
    claimDate: '2026-06-15',
    startLocation: 'Office',
    destination: 'District Court',
    amount: 45.5,
    caseIds: ['22222222-2222-4222-8222-222222222222'],
    decidedAtUtc: '2026-06-16T10:00:00Z',
  });

  let tree!: ReactTestRenderer.ReactTestRenderer;
  await ReactTestRenderer.act(() => {
    tree = ReactTestRenderer.create(<TravelClaimFormScreen />);
  });

  await ReactTestRenderer.act(async () => {
    await Promise.resolve();
  });

  const text = screenText(tree);
  expect(text).toContain('Approved');
  expect(text).toContain('Director feedback');
  expect(text).toContain('Your claim was approved.');
  expect(text).not.toContain('Director note');
});

test('read-only returned claim shows director comment', async () => {
  mockRouteParams.claimId = '55555555-5555-4555-8555-555555555555';
  mockRouteParams.mode = 'view';
  (travelClaimApiService.get as jest.Mock).mockResolvedValue({
    id: '55555555-5555-4555-8555-555555555555',
    status: 'Returned',
    transportMode: 'Bus',
    claimDate: '2026-06-15',
    startLocation: 'Office',
    destination: 'District Court',
    amount: 45.5,
    caseIds: ['22222222-2222-4222-8222-222222222222'],
    decisionComment: 'Receipt unclear',
    decidedAtUtc: '2026-06-16T11:00:00Z',
  });

  let tree!: ReactTestRenderer.ReactTestRenderer;
  await ReactTestRenderer.act(() => {
    tree = ReactTestRenderer.create(<TravelClaimFormScreen />);
  });

  await ReactTestRenderer.act(async () => {
    await Promise.resolve();
  });

  const text = screenText(tree);
  expect(text).toContain('Returned');
  expect(text).toContain('Receipt unclear');
});

test('read-only submitted claim shows status only without director feedback', async () => {
  mockRouteParams.claimId = '66666666-6666-4666-8666-666666666666';
  mockRouteParams.mode = 'view';
  (travelClaimApiService.get as jest.Mock).mockResolvedValue({
    id: '66666666-6666-4666-8666-666666666666',
    status: 'Submitted',
    transportMode: 'Bus',
    claimDate: '2026-06-15',
    startLocation: 'Office',
    destination: 'District Court',
    amount: 45.5,
    caseIds: ['22222222-2222-4222-8222-222222222222'],
  });

  let tree!: ReactTestRenderer.ReactTestRenderer;
  await ReactTestRenderer.act(() => {
    tree = ReactTestRenderer.create(<TravelClaimFormScreen />);
  });

  await ReactTestRenderer.act(async () => {
    await Promise.resolve();
  });

  const text = screenText(tree);
  expect(text).toContain('Submitted');
  expect(text).not.toContain('Director feedback');
});
