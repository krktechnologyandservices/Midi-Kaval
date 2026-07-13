import type {CourtSittingDto, CourtSittingScheduleItemDto} from '../services/cases/case.models';

export function isCourtSittingPastDue(item: CourtSittingDto): boolean {
  return (
    item.status === 'Upcoming'
    && !!item.scheduledAtUtc
    && new Date(item.scheduledAtUtc).getTime() < Date.now()
  );
}

/** Whole calendar days between today and the given date (negative if in the past). */
function calendarDaysUntil(dateIso: string): number | null {
  const scheduled = new Date(dateIso);
  if (Number.isNaN(scheduled.getTime())) {
    return null;
  }

  const startOfToday = new Date();
  startOfToday.setHours(0, 0, 0, 0);
  const startOfScheduled = new Date(scheduled);
  startOfScheduled.setHours(0, 0, 0, 0);
  return Math.round((startOfScheduled.getTime() - startOfToday.getTime()) / 86400000);
}

/** Short "in N days" / "today" / "N days ago" label for a row in a list. */
export function formatDaysUntilLabel(dateIso: string, isPastDue?: boolean): string {
  const calendarDays = calendarDaysUntil(dateIso);
  if (calendarDays === null) {
    return '';
  }

  if (isPastDue || calendarDays < 0) {
    const daysAgo = Math.abs(calendarDays);
    return daysAgo === 0 ? 'today' : daysAgo === 1 ? '1 day ago' : `${daysAgo} days ago`;
  }

  if (calendarDays === 0) {
    return 'today';
  }

  return calendarDays === 1 ? 'tomorrow' : `in ${calendarDays} days`;
}

export function buildCourtCountdownLabel(
  item: CourtSittingScheduleItemDto,
): string | null {
  if (!item.courtName || !item.scheduledAtUtc) {
    return null;
  }

  if (item.isPastDue) {
    return `Court sitting overdue — ${item.courtName}`;
  }

  const scheduled = new Date(item.scheduledAtUtc);
  if (Number.isNaN(scheduled.getTime())) {
    return null;
  }

  const weekday = scheduled.toLocaleDateString(undefined, {weekday: 'long'});
  const dayLabel = formatDaysUntilLabel(item.scheduledAtUtc, item.isPastDue).replace(
    /^in /,
    '',
  );
  return `Court sitting ${weekday} — ${dayLabel === 'tomorrow' ? '1 day' : dayLabel}`;
}
