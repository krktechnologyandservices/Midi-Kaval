export interface TimeOfDayOption {
  value: string;
  label: string;
}

function formatTimeLabel(time: string): string {
  const [hoursStr, minutesStr] = time.split(':');
  const hours24 = Number(hoursStr);
  const minutes = Number(minutesStr);
  const period = hours24 < 12 ? 'AM' : 'PM';
  const hours12 = hours24 % 12 === 0 ? 12 : hours24 % 12;
  return `${hours12}:${String(minutes).padStart(2, '0')} ${period}`;
}

/** Builds a fixed-step list of selectable times of day, e.g. 12:00 AM, 12:15 AM, … 11:45 PM. */
export function buildTimeOfDayOptions(stepMinutes = 15): TimeOfDayOption[] {
  const options: TimeOfDayOption[] = [];
  for (let minutes = 0; minutes < 24 * 60; minutes += stepMinutes) {
    const value = `${String(Math.floor(minutes / 60)).padStart(2, '0')}:${String(minutes % 60).padStart(2, '0')}`;
    options.push({ value, label: formatTimeLabel(value) });
  }
  return options;
}

/**
 * Ensures a specific time (e.g. loaded from an existing record that doesn't fall on the
 * fixed step) appears as a selectable option — without this, editing a record whose time
 * doesn't land on a step boundary would silently show a blank time selector.
 */
export function ensureTimeOptionPresent(
  options: TimeOfDayOption[],
  time: string | null | undefined,
): TimeOfDayOption[] {
  if (!time || options.some((option) => option.value === time)) {
    return options;
  }
  return [...options, { value: time, label: formatTimeLabel(time) }].sort((a, b) =>
    a.value.localeCompare(b.value),
  );
}

/** Combines a calendar date with a 'HH:mm' time-of-day string into a single local Date. */
export function combineDateAndTime(
  date: Date | null,
  time: string | null | undefined,
): Date | null {
  if (!date || !time) {
    return null;
  }
  const [hoursStr, minutesStr] = time.split(':');
  const hours = Number(hoursStr);
  const minutes = Number(minutesStr);
  if (Number.isNaN(hours) || Number.isNaN(minutes)) {
    return null;
  }
  const combined = new Date(date);
  combined.setHours(hours, minutes, 0, 0);
  return combined;
}

/** Tomorrow at midnight local time — a sensible default date for a new scheduling form. */
export function defaultScheduleDate(): Date {
  const date = new Date();
  date.setDate(date.getDate() + 1);
  date.setHours(0, 0, 0, 0);
  return date;
}

/** Today at midnight local time — used as the earliest selectable date on scheduling pickers. */
export function startOfToday(): Date {
  const date = new Date();
  date.setHours(0, 0, 0, 0);
  return date;
}

/** Splits an ISO timestamp into a calendar Date and 'HH:mm' time-of-day string, for pre-filling date + time picker controls. */
export function splitIsoToDateAndTime(
  iso: string | null | undefined,
): { date: Date | null; time: string } {
  if (!iso) {
    return { date: null, time: '' };
  }
  const date = new Date(iso);
  if (Number.isNaN(date.getTime())) {
    return { date: null, time: '' };
  }
  return {
    date,
    time: `${String(date.getHours()).padStart(2, '0')}:${String(date.getMinutes()).padStart(2, '0')}`,
  };
}
