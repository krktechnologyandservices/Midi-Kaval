import {getUtcWeekBounds} from '../src/utils/utcWeekBounds';

describe('getUtcWeekBounds', () => {
  it('keeps a sitting scheduled for later the same IST day inside the current week, even in the early-morning IST/UTC-date-mismatch window', () => {
    // 2026-07-09 02:00 IST == 2026-07-08 20:30 UTC — UTC calendar date is still
    // "yesterday" here, which used to shift the whole week window by a day.
    const nowDuringEarlyIst = new Date('2026-07-08T20:30:00.000Z');
    const {start, end} = getUtcWeekBounds(nowDuringEarlyIst);

    // 2026-07-09 10:00 IST == 2026-07-09 04:30 UTC.
    const sittingLaterTodayIst = new Date('2026-07-09T04:30:00.000Z');

    expect(sittingLaterTodayIst.getTime()).toBeGreaterThanOrEqual(start.getTime());
    expect(sittingLaterTodayIst.getTime()).toBeLessThanOrEqual(end.getTime());
  });

  it('spans exactly 7 days', () => {
    const {start, end} = getUtcWeekBounds(new Date('2026-07-09T04:30:00.000Z'));
    expect(end.getTime() - start.getTime()).toBe(7 * 24 * 60 * 60 * 1000 - 1);
  });
});
