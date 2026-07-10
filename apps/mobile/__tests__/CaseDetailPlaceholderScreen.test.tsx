import React from 'react';
import ReactTestRenderer from 'react-test-renderer';
import {Pressable, ScrollView, Text} from 'react-native';
import {CaseDetailPlaceholderScreen} from '../src/screens/cases/CaseDetailPlaceholderScreen';
import {caseApiService} from '../src/services/cases/CaseApiService';
import {attachmentApiService} from '../src/services/attachments/AttachmentApiService';

function screenText(tree: ReactTestRenderer.ReactTestRenderer): string {
  return tree.root
    .findAllByType(Text)
    .map(node => node.props.children)
    .flat()
    .filter((value): value is string => typeof value === 'string')
    .join(' ');
}

jest.mock('react-native-document-picker', () => ({
  __esModule: true,
  default: {
    pickSingle: jest.fn(),
    isCancel: () => false,
    types: {
      images: 'images',
      pdf: 'pdf',
    },
  },
  types: {
    images: 'images',
    pdf: 'pdf',
  },
}));

jest.mock('../src/services/cases/CaseApiService', () => ({
  caseApiService: {
    getCaseDetail: jest.fn(),
    listCaseNotes: jest.fn(),
    createCaseNote: jest.fn(),
    listInterventions: jest.fn(),
    createIntervention: jest.fn(),
    updateIntervention: jest.fn(),
    listCourtSittings: jest.fn(),
    createCourtSitting: jest.fn(),
    updateCourtSitting: jest.fn(),
    listFieldWorkers: jest.fn(),
    revealCasePii: jest.fn(),
    extractErrorMessage: jest.fn(() => 'error'),
  },
}));

jest.mock('../src/services/attachments/AttachmentApiService', () => ({
  attachmentApiService: {
    upload: jest.fn(),
    download: jest.fn(),
    extractErrorMessage: jest.fn(() => 'attachment error'),
    extractDownloadErrorMessage: jest.fn(() => 'attachment error'),
  },
}));

jest.mock('../src/services/auth/AuthSessionService', () => ({
  authSessionService: {
    getUser: jest.fn(() => ({id: 'worker-1', email: 'worker@test'})),
    getLastLoginOtpVerifiedAtUtc: jest.fn(() => new Date().toISOString()),
    stepUp: jest.fn().mockResolvedValue({challengeId: 'challenge-1'}),
    verifyStepUp: jest.fn().mockResolvedValue(undefined),
    extractErrorMessage: jest.fn(() => 'error'),
  },
}));

const navigation = {} as never;
const caseId = '11111111-1111-4111-8111-111111111111';

beforeEach(() => {
  (caseApiService.getCaseDetail as jest.Mock).mockResolvedValue({
    id: caseId,
    currentStage: 'ProcessInitiation',
  });
  (caseApiService.listCaseNotes as jest.Mock).mockResolvedValue([
    {
      id: 'note-1',
      caseId,
      noteType: 'Visit',
      bodyText: 'Timeline note',
      authorEmail: 'worker@test',
      createdAtUtc: '2026-06-10T10:00:00Z',
      attachments: [],
    },
  ]);
  (caseApiService.listInterventions as jest.Mock).mockResolvedValue([]);
  (caseApiService.listCourtSittings as jest.Mock).mockResolvedValue([]);
  (caseApiService.listFieldWorkers as jest.Mock).mockResolvedValue([
    {id: 'worker-1', email: 'worker@test'},
  ]);
});

test('case detail shows handoff whisper when present', async () => {
  (caseApiService.getCaseDetail as jest.Mock).mockResolvedValue({
    id: caseId,
    crimeNumber: 'CR-1',
    stNumber: 'ST-1',
    beneficiaryName: 'Test',
    currentStage: 'ProcessInitiation',
    handoffWhisper: {
      priorActions: 'Visited home',
      openItems: 'Open task',
      nextVisitPurpose: 'Follow up',
      transferredAtUtc: '2026-06-15T00:00:00Z',
    },
  });

  let tree!: ReactTestRenderer.ReactTestRenderer;
  await ReactTestRenderer.act(() => {
    tree = ReactTestRenderer.create(
      <CaseDetailPlaceholderScreen
        navigation={navigation}
        route={{
          key: 'detail',
          name: 'CaseDetailPlaceholder',
          params: {caseId},
        }}
      />,
    );
  });

  await ReactTestRenderer.act(async () => {
    await Promise.resolve();
  });

  const text = screenText(tree);
  expect(text).toContain('Prior actions:');
  expect(text).toContain('Visited home');
  expect(text).toContain('Notes timeline');
  expect(text).toContain('Timeline note');
});

test('view full timeline scrolls instead of showing alert', async () => {
  (caseApiService.getCaseDetail as jest.Mock).mockResolvedValue({
    id: caseId,
    crimeNumber: 'CR-1',
    currentStage: 'ProcessInitiation',
    handoffWhisper: {
      priorActions: 'Visited home',
      openItems: 'Open task',
      nextVisitPurpose: 'Follow up',
      transferredAtUtc: '2026-06-15T00:00:00Z',
    },
  });

  let tree!: ReactTestRenderer.ReactTestRenderer;
  await ReactTestRenderer.act(() => {
    tree = ReactTestRenderer.create(
      <CaseDetailPlaceholderScreen
        navigation={navigation}
        route={{
          key: 'detail',
          name: 'CaseDetailPlaceholder',
          params: {caseId},
        }}
      />,
    );
  });

  await ReactTestRenderer.act(async () => {
    await Promise.resolve();
  });

  const scrollView = tree.root.findByType(ScrollView);
  scrollView.instance.scrollTo = jest.fn();

  const timelineLink = tree.root.findByProps({accessibilityLabel: 'View full timeline'});

  await ReactTestRenderer.act(() => {
    timelineLink.props.onPress();
  });

  expect(scrollView.instance.scrollTo).toHaveBeenCalled();
});

test('add note calls createCaseNote', async () => {
  (caseApiService.getCaseDetail as jest.Mock).mockResolvedValue({
    id: caseId,
    currentStage: 'ProcessInitiation',
  });
  (caseApiService.createCaseNote as jest.Mock).mockResolvedValue({
    id: 'new-note',
    caseId,
    noteType: 'General',
    bodyText: 'Saved note',
    attachments: [],
  });

  let tree!: ReactTestRenderer.ReactTestRenderer;
  await ReactTestRenderer.act(() => {
    tree = ReactTestRenderer.create(
      <CaseDetailPlaceholderScreen
        navigation={navigation}
        route={{
          key: 'detail',
          name: 'CaseDetailPlaceholder',
          params: {caseId},
        }}
      />,
    );
  });

  await ReactTestRenderer.act(async () => {
    await Promise.resolve();
  });

  const noteInput = tree.root.findByProps({accessibilityLabel: 'Note text'});
  await ReactTestRenderer.act(() => {
    noteInput.props.onChangeText('Saved note');
  });

  const addButton = tree.root.findByProps({accessibilityLabel: 'Add case note'});

  await ReactTestRenderer.act(async () => {
    addButton?.props.onPress();
    await Promise.resolve();
  });

  expect(caseApiService.createCaseNote).toHaveBeenCalledWith(caseId, {
    noteType: 'General',
    bodyText: 'Saved note',
    actionRequired: false,
    actionDueAtUtc: null,
  });
});

test('add intervention calls createIntervention', async () => {
  (caseApiService.getCaseDetail as jest.Mock).mockResolvedValue({
    id: caseId,
    currentStage: 'ProcessInitiation',
  });
  (caseApiService.createIntervention as jest.Mock).mockResolvedValue({
    id: 'intervention-1',
    caseId,
    direction: 'Needed',
    categoryName: 'Counselling',
    status: 'Open',
  });

  let tree!: ReactTestRenderer.ReactTestRenderer;
  await ReactTestRenderer.act(() => {
    tree = ReactTestRenderer.create(
      <CaseDetailPlaceholderScreen
        navigation={navigation}
        route={{
          key: 'detail',
          name: 'CaseDetailPlaceholder',
          params: {caseId},
        }}
      />,
    );
  });

  await ReactTestRenderer.act(async () => {
    await Promise.resolve();
  });

  const categoryInput = tree.root.findByProps({accessibilityLabel: 'Intervention category'});
  const descriptionInput = tree.root.findByProps({accessibilityLabel: 'Intervention description'});

  await ReactTestRenderer.act(() => {
    categoryInput.props.onChangeText('Counselling');
    descriptionInput.props.onChangeText('Weekly session');
  });

  const addButton = tree.root.findByProps({accessibilityLabel: 'Add intervention'});

  await ReactTestRenderer.act(async () => {
    addButton.props.onPress();
    await Promise.resolve();
  });

  expect(caseApiService.createIntervention).toHaveBeenCalledWith(
    caseId,
    expect.objectContaining({
      direction: 'Needed',
      categoryName: 'Counselling',
      description: 'Weekly session',
      assignedStaffUserId: 'worker-1',
    }),
  );
});

test('add court sitting calls createCourtSitting', async () => {
  (caseApiService.getCaseDetail as jest.Mock).mockResolvedValue({
    id: caseId,
    currentStage: 'ProcessInitiation',
  });
  (caseApiService.createCourtSitting as jest.Mock).mockResolvedValue({
    id: 'sitting-1',
    caseId,
    courtName: 'District Court',
    status: 'Upcoming',
  });

  let tree!: ReactTestRenderer.ReactTestRenderer;
  await ReactTestRenderer.act(() => {
    tree = ReactTestRenderer.create(
      <CaseDetailPlaceholderScreen
        navigation={navigation}
        route={{
          key: 'detail',
          name: 'CaseDetailPlaceholder',
          params: {caseId},
        }}
      />,
    );
  });

  await ReactTestRenderer.act(async () => {
    await Promise.resolve();
  });

  const courtNameInput = tree.root.findByProps({accessibilityLabel: 'Court name'});
  const purposeInput = tree.root.findByProps({accessibilityLabel: 'Court sitting purpose'});

  await ReactTestRenderer.act(() => {
    courtNameInput.props.onChangeText('District Court');
    purposeInput.props.onChangeText('Hearing');
  });

  const addButton = tree.root.findByProps({accessibilityLabel: 'Add court sitting'});

  await ReactTestRenderer.act(async () => {
    addButton.props.onPress();
    await Promise.resolve();
  });

  expect(caseApiService.createCourtSitting).toHaveBeenCalledWith(
    caseId,
    expect.objectContaining({
      courtName: 'District Court',
      purpose: 'Hearing',
      status: 'Upcoming',
    }),
  );
});

test('POCSO case detail shows discreet header and reveal flow', async () => {
  (caseApiService.getCaseDetail as jest.Mock).mockResolvedValue({
    id: caseId,
    crimeNumber: 'CR-POCSO',
    stNumber: 'ST-1',
    beneficiaryName: 'R. K.',
    sensitivityLevel: 'POCSO',
    currentStage: 'ProcessInitiation',
  });
  (caseApiService.revealCasePii as jest.Mock).mockResolvedValue({
    beneficiaryName: 'Ravi Kumar',
  });

  let tree!: ReactTestRenderer.ReactTestRenderer;
  await ReactTestRenderer.act(() => {
    tree = ReactTestRenderer.create(
      <CaseDetailPlaceholderScreen
        navigation={navigation}
        route={{
          key: 'detail',
          name: 'CaseDetailPlaceholder',
          params: {caseId},
        }}
      />,
    );
  });

  await ReactTestRenderer.act(async () => {
    await Promise.resolve();
  });

  const expandButton = tree.root.findByProps({accessibilityLabel: 'Show full detail'});
  await ReactTestRenderer.act(async () => {
    expandButton.props.onPress();
    await Promise.resolve();
  });

  expect(caseApiService.revealCasePii).toHaveBeenCalled();
  expect(attachmentApiService.download).not.toHaveBeenCalled();
});
