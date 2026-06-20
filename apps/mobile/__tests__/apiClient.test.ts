import {shouldAttachBearer} from '../src/services/api/apiClient';

describe('apiClient helpers', () => {
  it('omits bearer on public auth endpoints', () => {
    expect(shouldAttachBearer('/api/v1/auth/login')).toBe(false);
    expect(shouldAttachBearer('/api/v1/auth/verify-otp')).toBe(false);
    expect(shouldAttachBearer('/api/v1/auth/refresh')).toBe(false);
    expect(shouldAttachBearer('/api/v1/auth/logout')).toBe(false);
  });

  it('attaches bearer on protected endpoints', () => {
    expect(shouldAttachBearer('/api/v1/auth/me')).toBe(true);
    expect(shouldAttachBearer('/api/v1/cases')).toBe(true);
  });
});
