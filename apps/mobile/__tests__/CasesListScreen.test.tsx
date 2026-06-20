import React from 'react';
import {Text} from 'react-native';
import ReactTestRenderer from 'react-test-renderer';
import {CasesListScreen} from '../src/screens/cases/CasesListScreen';
import {caseApiService} from '../src/services/cases/CaseApiService';

jest.mock('../src/context/AuthContext', () => ({
  useAuth: () => ({
    isSupervisorRole: false,
    isFieldRole: true,
    extractErrorMessage: () => 'error',
  }),
}));

jest.mock('../src/services/cases/CaseApiService', () => ({
  caseApiService: {
    listAssignedCases: jest.fn(),
    extractErrorMessage: jest.fn(() => 'error'),
  },
}));

const navigation = {
  navigate: jest.fn(),
} as never;

test('assigned cases list renders rows', async () => {
  (caseApiService.listAssignedCases as jest.Mock).mockResolvedValue({
    items: [
      {
        id: '11111111-1111-4111-8111-111111111111',
        crimeNumber: 'CR-1',
        stNumber: 'ST-1',
        currentStage: 'ProcessInitiation',
      },
    ],
    page: 1,
    pageSize: 25,
  });

  let tree!: ReactTestRenderer.ReactTestRenderer;
  await ReactTestRenderer.act(() => {
    tree = ReactTestRenderer.create(
      <CasesListScreen navigation={navigation} route={{} as never} />,
    );
  });

  await ReactTestRenderer.act(async () => {
    await Promise.resolve();
    await Promise.resolve();
  });

  const root = tree!.root;
  const texts = root.findAllByType(Text).map(node => String(node.props.children));
  const combined = texts.join(' ');
  expect(combined).toContain('CR-1');
  expect(combined).toContain('ST-1');
});
