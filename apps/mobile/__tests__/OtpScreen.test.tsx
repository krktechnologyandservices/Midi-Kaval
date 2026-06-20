import React from 'react';
import ReactTestRenderer from 'react-test-renderer';
import {AccessibleErrorRegion} from '../src/components/AccessibleErrorRegion';

test('otp feedback uses accessible error region', () => {
  const tree = ReactTestRenderer.create(
    <AccessibleErrorRegion message="Code expired — request new code" />,
  );
  const json = JSON.stringify(tree.toJSON());
  expect(json).toContain('accessibilityLiveRegion');
});
