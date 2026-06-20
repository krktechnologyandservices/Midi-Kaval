import React from 'react';
import {Text} from 'react-native';
import ReactTestRenderer from 'react-test-renderer';
import {CourtCountdownBanner} from '../src/components/CourtCountdownBanner';

test('court countdown banner renders fixture label', () => {
  let tree!: ReactTestRenderer.ReactTestRenderer;
  ReactTestRenderer.act(() => {
    tree = ReactTestRenderer.create(
      <CourtCountdownBanner label="Court sitting Thursday — 2 days" />,
    );
  });

  const text = tree.root.findByType(Text);
  expect(String(text.props.children)).toContain('Court sitting Thursday — 2 days');
});

test('court countdown banner hidden when label is null', () => {
  let tree!: ReactTestRenderer.ReactTestRenderer;
  ReactTestRenderer.act(() => {
    tree = ReactTestRenderer.create(<CourtCountdownBanner label={null} />);
  });

  expect(tree.toJSON()).toBeNull();
});
