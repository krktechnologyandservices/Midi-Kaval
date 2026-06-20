import type {CourtSittingDto, CourtSittingScheduleItemDto} from '../services/cases/case.models';

export function isCourtSittingPastDue(item: CourtSittingDto): boolean {
  return (
    item.status === 'Upcoming'
    && !!item.scheduledAtUtc
    && new Date(item.scheduledAtUtc).getTime() < Date.now()
  );
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
  const startOfToday = new Date();
  startOfToday.setHours(0, 0, 0, 0);
  const startOfScheduled = new Date(scheduled);
  startOfScheduled.setHours(0, 0, 0, 0);
  const calendarDays = Math.round(
    (startOfScheduled.getTime() - startOfToday.getTime()) / 86400000,
  );
  const dayLabel =
    calendarDays <= 0 ? 'today' : calendarDays === 1 ? '1 day' : `${calendarDays} days`;
  return `Court sitting ${weekday} — ${dayLabel}`;
}
