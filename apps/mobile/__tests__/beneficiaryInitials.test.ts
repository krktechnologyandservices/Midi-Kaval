import {beneficiaryInitials, isPocsoCase} from '../src/utils/beneficiaryInitials';

describe('beneficiaryInitials', () => {
  it('formats multi-word names', () => {
    expect(beneficiaryInitials('Ravi Kumar')).toBe('R. K.');
  });

  it('formats single-word names', () => {
    expect(beneficiaryInitials('Priya')).toBe('P.');
  });

  it('returns em dash for empty input', () => {
    expect(beneficiaryInitials('')).toBe('—');
    expect(beneficiaryInitials('   ')).toBe('—');
  });
});

describe('isPocsoCase', () => {
  it('detects POCSO sensitivity', () => {
    expect(isPocsoCase('POCSO')).toBe(true);
    expect(isPocsoCase('Standard')).toBe(false);
  });
});
