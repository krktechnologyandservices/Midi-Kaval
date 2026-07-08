// Field workers experience "this week" in IST, not UTC. Computing the week purely from
// UTC calendar fields means every day between midnight and 5:30 AM IST, the UTC date is
// still "yesterday" — silently dropping a sitting scheduled for today (IST) off the
// current week's bounds right at the week's start/end edges. Bounds returned are still
// real UTC instants (safe to compare against scheduledAtUtc directly); only the
// Mon–Sun week grid used to compute them is shifted to line up with the IST calendar.
const IST_OFFSET_MS = 5.5 * 60 * 60 * 1000;

export function getUtcWeekBounds(utcNow = new Date()): {start: Date; end: Date} {
  const istNow = new Date(utcNow.getTime() + IST_OFFSET_MS);
  const day = istNow.getUTCDay();
  const mondayOffset = day === 0 ? -6 : 1 - day;
  const istMonday = new Date(
    Date.UTC(
      istNow.getUTCFullYear(),
      istNow.getUTCMonth(),
      istNow.getUTCDate() + mondayOffset,
    ),
  );
  const start = new Date(istMonday.getTime() - IST_OFFSET_MS);
  const end = new Date(start.getTime() + 7 * 24 * 60 * 60 * 1000 - 1);
  return {start, end};
}
