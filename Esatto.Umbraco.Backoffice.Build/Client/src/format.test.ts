import { describe, expect, it } from 'vitest';
import { builtFrom, formatDate, formatDateTime, relativeTime, safeHref, shortSha } from './format';

const NOW = new Date('2026-11-20T12:00:00Z');
const MINUTE = 60_000;
const HOUR = 60 * MINUTE;
const DAY = 24 * HOUR;
const before = (ms: number) => new Date(NOW.getTime() - ms);

const reference = (locale: string, iso: string) =>
  new Intl.DateTimeFormat(locale, { dateStyle: 'medium', timeStyle: 'short' }).format(new Date(iso));

describe('formatDateTime', () => {
  const iso = '2026-11-09T23:44:00Z';

  it('formats with the given locale', () => {
    expect(formatDateTime(iso, 'sv-SE')).toBe(reference('sv-SE', iso));
    expect(formatDateTime(iso, 'en-US')).toBe(reference('en-US', iso));
  });

  it('follows the locale, so different locales give different text', () => {
    expect(formatDateTime(iso, 'sv-SE')).not.toBe(formatDateTime(iso, 'en-US'));
  });

  it('uses the browser locale when none is given', () => {
    const browser = new Intl.DateTimeFormat(undefined, { dateStyle: 'medium', timeStyle: 'short' }).format(new Date(iso));
    expect(formatDateTime(iso)).toBe(browser);
  });
});

describe('formatDate', () => {
  const iso = '2026-11-09T23:44:00Z';

  it('passes the locale through and ends with the relative part', () => {
    const text = formatDate(iso, { now: NOW, locale: 'sv-SE' });
    expect(text.startsWith(reference('sv-SE', iso))).toBe(true);
    expect(text.endsWith(' (10 days ago)')).toBe(true);
  });

  it('shows a dash for null, undefined and empty values', () => {
    expect(formatDate(null)).toBe('—');
    expect(formatDate(undefined)).toBe('—');
    expect(formatDate('')).toBe('—');
    expect(formatDate('   ')).toBe('—');
  });

  it('shows an unreadable value as it is instead of throwing', () => {
    expect(formatDate('not a date', { now: NOW })).toBe('not a date');
  });

  it('measures the relative part from the current time when no now is given', () => {
    expect(formatDate(new Date().toISOString())).toMatch(/\((this minute|\d+ minutes? ago|in \d+ minutes?)\)$/);
  });
});

describe('relativeTime', () => {
  it('counts minutes when under 60 minutes away', () => {
    expect(relativeTime(before(5 * MINUTE), NOW)).toBe('5 minutes ago');
    expect(relativeTime(before(59 * MINUTE), NOW)).toBe('59 minutes ago');
  });

  it('switches to hours at 60 minutes', () => {
    expect(relativeTime(before(60 * MINUTE), NOW)).toBe('1 hour ago');
    expect(relativeTime(before(3 * HOUR), NOW)).toBe('3 hours ago');
  });

  it('counts hours up to 47 hours', () => {
    expect(relativeTime(before(47 * HOUR), NOW)).toBe('47 hours ago');
  });

  it('counts whole units and drops the remainder', () => {
    expect(relativeTime(before(59 * MINUTE + 59_000), NOW)).toBe('59 minutes ago');
    expect(relativeTime(before(47 * HOUR + 59 * MINUTE), NOW)).toBe('47 hours ago');
    expect(relativeTime(before(10 * DAY + 12 * HOUR), NOW)).toBe('10 days ago');
  });

  it('switches to days from 48 hours', () => {
    expect(relativeTime(before(48 * HOUR), NOW)).toBe('2 days ago');
    expect(relativeTime(before(49 * HOUR), NOW)).toBe('2 days ago');
    expect(relativeTime(before(10 * DAY), NOW)).toBe('10 days ago');
  });

  it('says "this minute" for a moment ago', () => {
    expect(relativeTime(before(10_000), NOW)).toBe('this minute');
  });

  it('reads a time in the future as "in …"', () => {
    expect(relativeTime(new Date(NOW.getTime() + 5 * MINUTE), NOW)).toBe('in 5 minutes');
    expect(relativeTime(new Date(NOW.getTime() + 3 * HOUR), NOW)).toBe('in 3 hours');
    expect(relativeTime(new Date(NOW.getTime() + 10 * DAY), NOW)).toBe('in 10 days');
  });

  it('defaults to the current time', () => {
    expect(relativeTime(new Date(Date.now() - 3 * HOUR))).toBe('3 hours ago');
  });
});

describe('shortSha', () => {
  it('keeps the first 7 characters', () => {
    expect(shortSha('f1ff7ec3b1c9d0aa1122334455667788990011ff')).toBe('f1ff7ec');
  });

  it('keeps a sha that is already short', () => {
    expect(shortSha('f1ff7ec')).toBe('f1ff7ec');
    expect(shortSha('abc')).toBe('abc');
    expect(shortSha('')).toBe('');
  });
});

describe('builtFrom', () => {
  it('returns the commit after the plus sign', () => {
    expect(builtFrom('1.0.0+f1ff7ec')).toBe('f1ff7ec');
    expect(builtFrom('1.0.0-preview.1+f1ff7ec')).toBe('f1ff7ec');
  });

  it('returns a full 40-digit sha as it is', () => {
    const sha = 'f1ff7ec3b1c9d0aa1122334455667788990011ff';
    expect(builtFrom(`1.0.0+${sha}`)).toBe(sha);
  });

  it('accepts upper-case hexadecimal digits', () => {
    expect(builtFrom('1.0.0+F1FF7EC')).toBe('F1FF7EC');
  });

  it('returns null when the build metadata is not a commit id', () => {
    // A CI build number or a date is build metadata too, but it is not a commit.
    expect(builtFrom('1.2.3+20261120.5')).toBeNull();
    expect(builtFrom('1.2.3+build.5')).toBeNull();
    expect(builtFrom('1.0.0+f1ff7ec.dirty')).toBeNull();
    expect(builtFrom('1.0.0+f1ff7eg')).toBeNull();
  });

  it('returns null when the commit id is too short or too long', () => {
    expect(builtFrom('1.0.0+abc')).toBeNull();
    expect(builtFrom('1.0.0+f1ff7e')).toBeNull();
    expect(builtFrom(`1.0.0+${'a'.repeat(41)}`)).toBeNull();
  });

  it('returns null when there is no build metadata', () => {
    expect(builtFrom('1.0.0')).toBeNull();
    expect(builtFrom('1.0.0+')).toBeNull();
    expect(builtFrom('')).toBeNull();
    expect(builtFrom(null)).toBeNull();
    expect(builtFrom(undefined)).toBeNull();
  });
});

describe('safeHref', () => {
  it('accepts http and https URLs', () => {
    expect(safeHref('https://example.com/a?b=1')).toBe('https://example.com/a?b=1');
    expect(safeHref('http://example.com')).toBe('http://example.com/');
  });

  it.each([
    ['javascript:alert(1)'],
    ['JaVaScRiPt:alert(1)'],
    ['data:text/html,x'],
    ['/relative'],
    ['//example.com/x'],
    ['not a url'],
    [''],
    [null],
    [undefined],
  ])('rejects %s', (url) => {
    expect(safeHref(url)).toBeNull();
  });
});
