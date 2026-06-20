import { formatMatchedOn } from './matched-on-label';

describe('formatMatchedOn', () => {
  it('maps CrimeNumber', () => {
    expect(formatMatchedOn('CrimeNumber')).toBe('Matched on Crime number');
  });

  it('maps StNumber', () => {
    expect(formatMatchedOn('StNumber')).toBe('Matched on ST number');
  });

  it('maps Both', () => {
    expect(formatMatchedOn('Both')).toBe('Matched on Crime and ST number');
  });
});
