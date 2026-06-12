export interface CalendarDay {
  /** Local YYYY-MM-DD key. */
  key: string;
  /** Day-of-month number for display. */
  day: number;
  /** Whether this cell belongs to the month being displayed. */
  inCurrentMonth: boolean;
}

/** Format a Date as a local YYYY-MM-DD key (no timezone shift). */
export function toDayKey(date: Date): string {
  const y = date.getFullYear();
  const m = String(date.getMonth() + 1).padStart(2, "0");
  const d = String(date.getDate()).padStart(2, "0");
  return `${y}-${m}-${d}`;
}

/**
 * Take just the date part of an ISO string ("2026-05-03T14:00" -> "2026-05-03").
 * Null-aware: returns null when the input is null.
 */
export function dayPart(iso: string | null): string | null {
  return iso ? iso.slice(0, 10) : null;
}

/**
 * Parse a "YYYY-MM-DD" (optionally longer ISO) day key into a LOCAL Date.
 * Uses the numeric constructor so the date is NOT interpreted as UTC midnight,
 * which would shift a day for users west of UTC.
 */
export function parseDayKey(key: string): Date {
  const [y, m, d] = key.slice(0, 10).split("-").map(Number);
  return new Date(y, m - 1, d);
}

/**
 * Build a Monday-first grid of whole weeks covering the given month.
 * @param year full year, e.g. 2026
 * @param month 0-based month, e.g. 4 = May
 */
export function buildMonthGrid(year: number, month: number): CalendarDay[] {
  const firstOfMonth = new Date(year, month, 1);
  const firstOfMonthTime = firstOfMonth.getTime();
  // JS getDay(): 0=Sun..6=Sat. Convert to Monday-first offset (Mon=0..Sun=6).
  const mondayOffset = (firstOfMonth.getDay() + 6) % 7;

  const start = new Date(year, month, 1 - mondayOffset);
  const days: CalendarDay[] = [];

  // Always emit whole weeks until we've passed the month and completed a week.
  const cursor = new Date(start);
  do {
    for (let i = 0; i < 7; i++) {
      days.push({
        key: toDayKey(cursor),
        day: cursor.getDate(),
        inCurrentMonth: cursor.getMonth() === month,
      });
      cursor.setDate(cursor.getDate() + 1);
    }
  } while (cursor.getMonth() === month || cursor.getTime() < firstOfMonthTime);

  return days;
}

/** True if dayKey falls outside [min, max] (date-only comparison). */
export function isDayDisabled(
  dayKey: string,
  min: string | null,
  max: string | null
): boolean {
  const minDay = dayPart(min);
  const maxDay = dayPart(max);
  if (minDay !== null && dayKey < minDay) return true;
  if (maxDay !== null && dayKey > maxDay) return true;
  return false;
}
