import React from 'react';
import ReactTestRenderer from 'react-test-renderer';
import {AccessibleErrorRegion} from '../src/components/AccessibleErrorRegion';

test('reset password feedback uses accessible error region', () => {
  const tree = ReactTestRenderer.create(
    <AccessibleErrorRegion message="Invalid or expired reset token." />,
  );
  const json = JSON.stringify(tree.toJSON());
  expect(json).toContain('accessibilityLiveRegion');
});
