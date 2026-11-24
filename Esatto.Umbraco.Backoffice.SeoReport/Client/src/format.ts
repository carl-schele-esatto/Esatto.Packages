// Formatting for the dashboard: periods, numbers, percentages, durations and moments, in the language of the backoffice.
//
// The server sends raw values ("YYYY-MM", "YYYY-MM-DD", numbers, ISO moments) and the client owns how they read.
//
// EVERY FUNCTION TOLERATES A MISSING VALUE (null, undefined, NaN, infinity) and returns '' for it, never a dash, a zero or the word
// "null". The caller decides what a missing figure says (the "no data" text), because only the caller knows whether a dash would be
// read as a value. A function that is given text it cannot read as what it formats (a period, a day) returns that text unchanged,
// and none of them throws: a bad value from an old archived report must not blank the dashboard.

const PERIOD = /^(\d{4})-(0[1-9]|1[0-2])$/;
const DAY = /^(\d{4})-(\d{2})-(\d{2})$/;

const isNumber = (value: number | null | undefined): value is number => typeof value === 'number' && Number.isFinite(value);

// Intl throws a RangeError for a culture it cannot parse. A culture comes from the backoffice, but "never throws" is cheaper to
// guarantee here than to prove at every call site.
function withCulture<T>(culture: string, make: (culture: string) => T): T {
  try {
    return make(culture);
  } catch {
    return make('en');
  }
}

/** "2026-10" as "October 2026" (or "oktober 2026"); text that is not a month comes back unchanged, nothing gives ''. */
export function formatPeriod(period: string | null | undefined, culture: string): string {
  if (!period) return '';
  const match = PERIOD.exec(period);
  if (!match) return period;

  const first = new Date(Date.UTC(Number(match[1]), Number(match[2]) - 1, 1));
  return withCulture(culture, (c) => new Intl.DateTimeFormat(c, { month: 'long', year: 'numeric', timeZone: 'UTC' }).format(first));
}

/** "2026-10" as "Oct 2026" (or "okt. 2026"), for an axis; text that is not a month comes back unchanged, nothing gives ''. */
export function formatPeriodShort(period: string | null | undefined, culture: string): string {
  if (!period) return '';
  const match = PERIOD.exec(period);
  if (!match) return period;

  const first = new Date(Date.UTC(Number(match[1]), Number(match[2]) - 1, 1));
  return withCulture(culture, (c) => new Intl.DateTimeFormat(c, { month: 'short', year: 'numeric', timeZone: 'UTC' }).format(first));
}

// A calendar month number ("01".."12", or 1..12) as a name. A chart pivoted by year has these as its x-axis (Report.cs, ChartBlock.Labels).
// Text that is not a whole number from 1 to 12 comes back unchanged.
function monthName(month: string | number | null | undefined, culture: string, style: 'short' | 'long'): string {
  if (month === null || month === undefined || month === '') return '';
  const number = typeof month === 'number' ? month : /^\d{1,2}$/.test(month) ? Number(month) : Number.NaN;
  if (!Number.isInteger(number) || number < 1 || number > 12) return String(month);

  // The year is only a carrier: 2000 is a leap year, so every month exists in it.
  const date = new Date(Date.UTC(2000, number - 1, 1));
  return withCulture(culture, (c) => new Intl.DateTimeFormat(c, { month: style, timeZone: 'UTC' }).format(date));
}

/** A calendar month number as its short name ("Jan"), for an axis. */
export const formatMonthShort = (month: string | number | null | undefined, culture: string): string => monthName(month, culture, 'short');

/** A calendar month number as its full name ("January"), for a table. */
export const formatMonthName = (month: string | number | null | undefined, culture: string): string => monthName(month, culture, 'long');

/** A number grouped for the language, at most two decimals; '' when it is not a number. */
export function formatNumber(value: number | null | undefined, culture: string, maximumFractionDigits = 2): string {
  if (!isNumber(value)) return '';
  return withCulture(culture, (c) => new Intl.NumberFormat(c, { maximumFractionDigits }).format(value));
}

/** A number abbreviated for an axis ("12K", "1.5M"; the language's own short forms); '' when it is not a number. */
export function formatCompact(value: number | null | undefined, culture: string): string {
  if (!isNumber(value)) return '';
  return withCulture(culture, (c) => new Intl.NumberFormat(c, { notation: 'compact', maximumFractionDigits: 1 }).format(value));
}

/** A number with exactly <c>digits</c> decimals, grouped for the language ("3.0", for a column of average positions); '' when it is not a number. */
export function formatFixed(value: number | null | undefined, culture: string, digits: number): string {
  if (!isNumber(value)) return '';
  return withCulture(culture, (c) => new Intl.NumberFormat(c, { minimumFractionDigits: digits, maximumFractionDigits: digits }).format(value));
}

/**
 * A figure that is ALREADY a percentage (62.4 is 62.4 %, as the report's "percent" figures are) with the percent sign of the
 * language. At most one decimal (or <c>digits</c>: a click-through rate is shown with two, since 0.45 % is not 0.5 %); with <c>fixed</c>
 * always one, for a change that reads as a figure ("12.0%"); with <c>signed</c> a rise has its plus ("+12.0%") and a fall its minus,
 * and zero has none.
 */
export function formatPercent(
  value: number | null | undefined,
  culture: string,
  options: { fixed?: boolean; signed?: boolean; digits?: number } = {},
): string {
  if (!isNumber(value)) return '';
  return withCulture(culture, (c) =>
    new Intl.NumberFormat(c, {
      style: 'percent',
      minimumFractionDigits: options.fixed ? 1 : 0,
      maximumFractionDigits: Math.max(options.digits ?? 1, options.fixed ? 1 : 0),
      signDisplay: options.signed ? 'exceptZero' : 'auto',
    }).format(value / 100),
  );
}

/** Seconds as minutes:seconds, as GA4 shows an average visit ("2:54"); a negative or missing value gives ''. */
export function formatDuration(seconds: number | null | undefined): string {
  if (!isNumber(seconds) || seconds < 0) return '';
  const whole = Math.round(seconds);
  return `${Math.floor(whole / 60)}:${String(whole % 60).padStart(2, '0')}`;
}

/**
 * A KPI's value by its <c>format</c> (integer | signed | percent | decimal | duration, see Kpi in Report.cs). A format this client
 * does not know is written as a plain number rather than guessed at; a missing value gives ''.
 */
export function formatKpiValue(value: number | null | undefined, format: string, culture: string): string {
  if (!isNumber(value)) return '';

  switch (format) {
    case 'integer':
      return formatNumber(value, culture, 0);
    case 'signed':
      return withCulture(culture, (c) => new Intl.NumberFormat(c, { maximumFractionDigits: 2, signDisplay: 'exceptZero' }).format(value));
    case 'percent':
      return formatPercent(value, culture);
    case 'duration':
      return formatDuration(value);
    default:
      return formatNumber(value, culture);
  }
}

// The server writes a moment as an ISO 8601 text with an offset; a text without one is read as UTC, which is what the server means.
function toDate(moment: string | null | undefined): Date | null {
  if (!moment) return null;
  const date = new Date(/[zZ]|[+-]\d\d:?\d\d$/.test(moment) ? moment : `${moment}Z`);
  return Number.isNaN(date.getTime()) ? null : date;
}

/** A moment as date and time in the language ("19 Nov 2026, 12:30"); '' when it is not a moment. <c>timeZone</c> is for tests. */
export function formatDateTime(moment: string | null | undefined, culture: string, timeZone?: string): string {
  const date = toDate(moment);
  if (!date) return '';
  return withCulture(culture, (c) => new Intl.DateTimeFormat(c, { dateStyle: 'medium', timeStyle: 'short', timeZone }).format(date));
}

const MINUTE = 60;
const HOUR = 60 * MINUTE;
const DAY_SECONDS = 24 * HOUR;

// A moment this far ahead of the clock is clock skew between the server and this browser, and reads as "now"; further ahead is not
// "how long ago" at all.
const SKEW_SECONDS = 5 * MINUTE;

/**
 * How long ago a moment was ("5 minutes ago", "yesterday"), up to a month back; '' for anything older, in the future or not a moment,
 * which the caller then writes as a date (see <see cref="formatDateTime"/>). <c>now</c> is for tests.
 */
export function formatRelative(moment: string | null | undefined, culture: string, now: Date = new Date()): string {
  const date = toDate(moment);
  if (!date) return '';

  const seconds = Math.floor((now.getTime() - date.getTime()) / 1000);
  if (seconds < -SKEW_SECONDS || seconds > 30 * DAY_SECONDS) return '';

  const [amount, unit]: [number, Intl.RelativeTimeFormatUnit] =
    seconds < MINUTE ? [0, 'second']
    : seconds < HOUR ? [-Math.floor(seconds / MINUTE), 'minute']
    : seconds < DAY_SECONDS ? [-Math.floor(seconds / HOUR), 'hour']
    : [-Math.floor(seconds / DAY_SECONDS), 'day'];

  return withCulture(culture, (c) => new Intl.RelativeTimeFormat(c, { numeric: 'auto' }).format(amount, unit));
}

// A day "YYYY-MM-DD" written with the given options; text that is not a real day comes back unchanged, nothing gives ''.
function formatDay(date: string | null | undefined, culture: string, options: Intl.DateTimeFormatOptions): string {
  if (!date) return '';
  const match = DAY.exec(date);
  if (!match) return date;

  const [year, month, day] = [Number(match[1]), Number(match[2]), Number(match[3])];
  const parsed = new Date(Date.UTC(year, month - 1, day));
  // A day that does not exist (the 31st of February) would roll over into the next month; keep what was typed instead.
  if (parsed.getUTCFullYear() !== year || parsed.getUTCMonth() !== month - 1 || parsed.getUTCDate() !== day) return date;

  return withCulture(culture, (c) => new Intl.DateTimeFormat(c, { ...options, timeZone: 'UTC' }).format(parsed));
}

/** A day "YYYY-MM-DD" as day and short month ("5 Nov"); text that is not a real day comes back unchanged, nothing gives ''. */
export const formatMonthDay = (date: string | null | undefined, culture: string): string =>
  formatDay(date, culture, { day: 'numeric', month: 'short' });

/** A day "YYYY-MM-DD" with its weekday and the year ("Thursday 5 November 2026"), for a table or a sentence. */
export const formatDayLong = (date: string | null | undefined, culture: string): string =>
  formatDay(date, culture, { weekday: 'long', day: 'numeric', month: 'long', year: 'numeric' });
