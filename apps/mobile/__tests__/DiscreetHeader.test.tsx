import React from 'react';
import {Text} from 'react-native';
import ReactTestRenderer from 'react-test-renderer';
import {DiscreetHeader} from '../src/components/DiscreetHeader';

test('discreet header collapsed announces limited detail mode', () => {
  let tree!: ReactTestRenderer.ReactTestRenderer;
  ReactTestRenderer.act(() => {
    tree = ReactTestRenderer.create(
      <DiscreetHeader
        caseInfo={{
          crimeNumber: 'CR-POCSO',
          stNumber: 'ST-1',
          domicile: 'Urban',
          beneficiaryName: 'R. K.',
          sensitivityLevel: 'POCSO',
        }}
        expanded={false}
        onExpandPress={jest.fn()}
      />,
    );
  });

  const header = tree.root.find(
    node => node.props.accessibilityLabel === 'Limited detail mode',
  );
  expect(header).toBeTruthy();

  const combined = tree.root
    .findAllByType(Text)
    .map(node => String(node.props.children))
    .join(' ');
  expect(combined).toContain('R. K.');
  expect(combined).toContain('CR-POCSO');
  expect(combined).toContain('Show full detail');
});

test('discreet header expanded shows full fields', () => {
  let tree!: ReactTestRenderer.ReactTestRenderer;
  ReactTestRenderer.act(() => {
    tree = ReactTestRenderer.create(
      <DiscreetHeader
        caseInfo={{
          crimeNumber: 'CR-POCSO',
          stNumber: 'ST-1',
          domicile: 'Urban',
          beneficiaryName: 'Ravi Kumar',
          beneficiaryContact: '9876543210',
          beneficiaryAge: 14,
          sensitivityLevel: 'POCSO',
        }}
        expanded
        onExpandPress={jest.fn()}
      />,
    );
  });

  const combined = tree.root
    .findAllByType(Text)
    .map(node => String(node.props.children))
    .join(' ');
  expect(combined).toContain('Ravi Kumar');
  expect(combined).toContain('ST-1');
  expect(combined).toContain('Urban');
  expect(combined).toContain('9876543210');
});
