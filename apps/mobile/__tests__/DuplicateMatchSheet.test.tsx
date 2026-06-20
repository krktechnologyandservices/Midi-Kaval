import React from 'react';
import ReactTestRenderer from 'react-test-renderer';
import {DuplicateMatchSheet} from '../src/components/DuplicateMatchSheet';

const match = {
  caseId: '11111111-1111-4111-8111-111111111111',
  crimeNumber: 'CR-1',
  stNumber: 'ST-1',
  beneficiaryName: 'Ravi',
  currentStage: 'ProcessInitiation',
  matchedOn: 'Both',
};

test('duplicate match sheet renders headline and matched-on label', () => {
  let tree!: ReactTestRenderer.ReactTestRenderer;

  ReactTestRenderer.act(() => {
    tree = ReactTestRenderer.create(
      <DuplicateMatchSheet
        visible
        matches={[match]}
        canMerge
        merging={false}
        announcement="1 possible match found"
        onOpenExisting={jest.fn()}
        onMerge={jest.fn()}
        onCancel={jest.fn()}
      />,
    );
  });

  const json = JSON.stringify(tree.toJSON());
  expect(json).toContain('Possible match — review before saving.');
  expect(json).toContain('Matched on Crime and ST number');
  expect(json.toLowerCase()).not.toContain('create anyway');

  ReactTestRenderer.act(() => {
    tree.unmount();
  });
});

test('duplicate match sheet hides Merge when canMerge is false', () => {
  let tree!: ReactTestRenderer.ReactTestRenderer;

  ReactTestRenderer.act(() => {
    tree = ReactTestRenderer.create(
      <DuplicateMatchSheet
        visible
        matches={[match]}
        canMerge={false}
        merging={false}
        announcement="1 possible match found"
        onOpenExisting={jest.fn()}
        onMerge={jest.fn()}
        onCancel={jest.fn()}
      />,
    );
  });

  const json = JSON.stringify(tree.toJSON());
  expect(json).not.toContain('"Merge"');
  expect(json).toContain('Open existing');

  ReactTestRenderer.act(() => {
    tree.unmount();
  });
});

test('duplicate match sheet shows inline confirm before merge', () => {
  let tree!: ReactTestRenderer.ReactTestRenderer;

  ReactTestRenderer.act(() => {
    tree = ReactTestRenderer.create(
      <DuplicateMatchSheet
        visible
        matches={[match]}
        canMerge
        merging={false}
        announcement="1 possible match found"
        onOpenExisting={jest.fn()}
        onMerge={jest.fn()}
        onCancel={jest.fn()}
      />,
    );
  });

  const mergeButton = tree.root
    .findAllByType(require('react-native').Pressable)
    .find(node =>
      node.findAllByType(require('react-native').Text).some(
        (text: {props: {children: string}}) => text.props.children === 'Merge',
      ),
    );

  ReactTestRenderer.act(() => {
    mergeButton!.props.onPress();
  });

  const json = JSON.stringify(tree.toJSON());
  expect(json).toContain('Merge this intake into the existing case?');
  expect(json).toContain('Confirm merge');

  ReactTestRenderer.act(() => {
    tree.unmount();
  });
});
