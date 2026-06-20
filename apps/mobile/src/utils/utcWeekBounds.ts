export function getUtcWeekBounds(utcNow = new Date()): {start: Date; end: Date} {
  const day = utcNow.getUTCDay();
  const mondayOffset = day === 0 ? -6 : 1 - day;
  const start = new Date(
    Date.UTC(
      utcNow.getUTCFullYear(),
      utcNow.getUTCMonth(),
      utcNow.getUTCDate() + mondayOffset,
    ),
  );
  const end = new Date(start.getTime() + 7 * 24 * 60 * 60 * 1000 - 1);
  return {start, end};
}
