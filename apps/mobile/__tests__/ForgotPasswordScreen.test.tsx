import React from 'react';
import ReactTestRenderer from 'react-test-renderer';
import {AccessibleErrorRegion} from '../src/components/AccessibleErrorRegion';

test('forgot password feedback uses accessible error region', () => {
  const tree = ReactTestRenderer.create(
    <AccessibleErrorRegion message="If an account exists for that email, we sent reset instructions." />,
  );
  const json = JSON.stringify(tree.toJSON());
  expect(json).toContain('accessibilityLiveRegion');
});
