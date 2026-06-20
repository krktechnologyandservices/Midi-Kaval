import {resolveAuthDestination} from '../src/services/auth/roleRouting';
import {AppRole} from '@midi-kaval/shared-types';

describe('role routing', () => {
  it('routes SocialWorker to tabs', () => {
    expect(
      resolveAuthDestination(true, AppRole.SocialWorker),
    ).toBe('tabs');
  });

  it('routes Director to web-only', () => {
    expect(
      resolveAuthDestination(true, AppRole.Director),
    ).toBe('web-only');
  });

  it('routes unauthenticated users to auth', () => {
    expect(resolveAuthDestination(false, null)).toBe('auth');
  });
});
