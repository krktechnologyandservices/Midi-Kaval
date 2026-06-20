import {buildCourtCountdownLabel, isCourtSittingPastDue} from '../src/utils/courtSittingUtils';

describe('courtSittingUtils', () => {
  it('detects past-due upcoming sittings client-side', () => {
    expect(
      isCourtSittingPastDue({
        status: 'Upcoming',
        scheduledAtUtc: '2020-01-01T10:00:00Z',
      }),
    ).toBe(true);

    expect(
      isCourtSittingPastDue({
        status: 'Attended',
        scheduledAtUtc: '2020-01-01T10:00:00Z',
      }),
    ).toBe(false);
  });

  it('builds overdue countdown label from API isPastDue', () => {
    expect(
      buildCourtCountdownLabel({
        courtName: 'District Court',
        scheduledAtUtc: '2020-01-01T10:00:00Z',
        isPastDue: true,
      }),
    ).toBe('Court sitting overdue — District Court');
  });

  it('uses today for same-calendar-day sittings', () => {
    const laterToday = new Date();
    laterToday.setHours(23, 59, 0, 0);

    const label = buildCourtCountdownLabel({
      courtName: 'District Court',
      scheduledAtUtc: laterToday.toISOString(),
      isPastDue: false,
    });

    expect(label).toContain('today');
  });

  it('builds future countdown label', () => {
    const future = new Date(Date.now() + 2 * 86400000);
    const label = buildCourtCountdownLabel({
      courtName: 'Family Court',
      scheduledAtUtc: future.toISOString(),
      isPastDue: false,
    });

    expect(label).toContain('Court sitting');
    expect(label).not.toContain('Family Court');
    expect(label).toMatch(/Court sitting \w+ — \d+ days|today|1 day/);
  });
});
