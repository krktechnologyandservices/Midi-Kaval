import React from 'react';
import {Pressable, Text, TextInput} from 'react-native';
import ReactTestRenderer from 'react-test-renderer';
import {CaseCreateScreen} from '../src/screens/cases/CaseCreateScreen';
import {caseApiService} from '../src/services/cases/CaseApiService';

jest.mock('../src/context/AuthContext', () => ({
  useAuth: () => ({
    isSupervisorRole: true,
    isFieldRole: false,
    extractErrorMessage: () => 'error',
  }),
}));

jest.mock('../src/services/cases/CaseApiService', () => ({
  caseApiService: {
    checkDuplicate: jest.fn(),
    createCase: jest.fn(),
    mergeCase: jest.fn(),
    extractErrorMessage: jest.fn(() => 'error'),
    isConflict: jest.fn(() => false),
  },
}));

const navigation = {
  navigate: jest.fn(),
} as never;

function findPressableWithLabel(
  root: ReactTestRenderer.ReactTestInstance,
  label: string,
): ReactTestRenderer.ReactTestInstance | undefined {
  return root.findAllByType(Pressable).find(pressable => {
    try {
      return pressable
        .findAllByType(Text)
        .some(textNode => textNode.props.children === label);
    } catch {
      return false;
    }
  });
}

test('case create screen renders save action', () => {
  const tree = ReactTestRenderer.create(
    <CaseCreateScreen navigation={navigation} route={{} as never} />,
  );

  const json = JSON.stringify(tree.toJSON());
  expect(json).toContain('Save case');
  expect(json).toContain('Crime number');
});

test('case create screen calls duplicate check on save', async () => {
  (caseApiService.checkDuplicate as jest.Mock).mockResolvedValue({
    hasMatch: true,
    matches: [
      {
        caseId: '11111111-1111-4111-8111-111111111111',
        crimeNumber: 'CR-1',
        matchedOn: 'CrimeNumber',
      },
    ],
  });

  let tree!: ReactTestRenderer.ReactTestRenderer;

  await ReactTestRenderer.act(async () => {
    tree = ReactTestRenderer.create(
      <CaseCreateScreen navigation={navigation} route={{} as never} />,
    );
  });

  const root = tree.root;
  const textInputs = root.findAllByType(TextInput);

  await ReactTestRenderer.act(async () => {
    textInputs[0].props.onChangeText('CR-1');
    textInputs[1].props.onChangeText('ST-1');
    textInputs[2].props.onChangeText('Ravi');
    textInputs[3].props.onChangeText('Theft');
  });

  const saveButton = findPressableWithLabel(root, 'Save case');
  expect(saveButton).toBeDefined();

  await ReactTestRenderer.act(async () => {
    await saveButton!.props.onPress();
  });

  expect(caseApiService.checkDuplicate).toHaveBeenCalled();
  expect(caseApiService.createCase).not.toHaveBeenCalled();
});
