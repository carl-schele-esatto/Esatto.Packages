// Pure helpers for showing what the stamp says. No DOM, no Lit, so they are easy to test.

const EMPTY = '—';
const MINUTE_MS = 60_000;
const HOUR_MS = 60 * MINUTE_MS;
const DAY_MS = 24 * HOUR_MS;
const SHORT_SHA_LENGTH = 7;

// The relative phrase is English whatever the browser's locale ("10 days ago").
const relativeFormat = new Intl.RelativeTimeFormat('en', { numeric: 'auto' });

/** A date and time in the given locale (the browser's when omitted), e.g. "9 Nov 2026, 23:44". */
export function formatDateTime(value: string, locale?: Intl.LocalesArgument): string {
  return new Intl.DateTimeFormat(locale, { dateStyle: 'medium', timeStyle: 'short' }).format(new Date(value));
}

/** "9 Nov 2026, 23:44 (10 days ago)", or a dash when there is no value. */
export function formatDate(
  value: string | null | undefined,
  options: { now?: Date; locale?: Intl.LocalesArgument } = {},
): string {
  if (!value?.trim()) return EMPTY;
  const date = new Date(value);
  // Show what the stamp says rather than throw from Intl when it is not a date.
  if (Number.isNaN(date.getTime())) return value;
  return `${formatDateTime(value, options.locale)} (${relativeTime(date, options.now)})`;
}

/**
 * How far `date` is from `now`: minutes under an hour, hours under 48 hours, otherwise days.
 * Whole units only (the remainder is dropped), so "10 days ago" means at least 10 days.
 */
export function relativeTime(date: Date, now: Date = new Date()): string {
  const diff = date.getTime() - now.getTime();
  const elapsed = Math.abs(diff);

  if (elapsed < HOUR_MS) return relativeFormat.format(Math.trunc(diff / MINUTE_MS), 'minute');
  if (elapsed < 2 * DAY_MS) return relativeFormat.format(Math.trunc(diff / HOUR_MS), 'hour');
  return relativeFormat.format(Math.trunc(diff / DAY_MS), 'day');
}

/** The first 7 characters of a commit sha. */
export function shortSha(sha: string): string {
  return sha.slice(0, SHORT_SHA_LENGTH);
}

// A commit id as the SDK appends it to an informational version: 7 to 40 hexadecimal digits (a short or a full sha).
const COMMIT_ID = /^[0-9a-f]{7,40}$/i;

/**
 * The commit in an informational version ("1.0.0+f1ff7ec" gives "f1ff7ec"), or null when there is none. Other build metadata
 * (a CI build number or a date, as in "1.2.3+20261120.5") is not a commit, so it is not shown as one.
 */
export function builtFrom(appVersion: string | null | undefined): string | null {
  const plus = appVersion?.indexOf('+') ?? -1;
  if (!appVersion || plus < 0) return null;
  const metadata = appVersion.slice(plus + 1);
  return COMMIT_ID.test(metadata) ? metadata : null;
}

/** The normalised URL when it is an absolute http or https URL, otherwise null. Never returns a javascript: or data: URL. */
export function safeHref(url: string | null | undefined): string | null {
  if (!url) return null;
  try {
    const parsed = new URL(url);
    return parsed.protocol === 'http:' || parsed.protocol === 'https:' ? parsed.href : null;
  } catch {
    return null;
  }
}
