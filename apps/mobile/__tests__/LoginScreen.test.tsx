import React from 'react';
import ReactTestRenderer from 'react-test-renderer';
import {AccessibleErrorRegion} from '../src/components/AccessibleErrorRegion';

test('accessible error region uses polite live region', () => {
  const tree = ReactTestRenderer.create(
    <AccessibleErrorRegion message="Invalid email or password." />,
  );
  const json = JSON.stringify(tree.toJSON());
  expect(json).toContain('accessibilityLiveRegion');
});
