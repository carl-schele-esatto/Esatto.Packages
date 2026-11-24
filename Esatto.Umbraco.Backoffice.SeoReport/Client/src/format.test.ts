import { describe, expect, it } from 'vitest';
import {
  formatDateTime,
  formatDuration,
  formatKpiValue,
  formatCompact,
  formatDayLong,
  formatFixed,
  formatMonthDay,
  formatMonthName,
  formatMonthShort,
  formatNumber,
  formatPercent,
  formatPeriod,
  formatPeriodShort,
  formatRelative,
} from './format';

// The test runner is Node, but the package takes no dependency on Node's types for this one use.
declare const process: { env: Record<string, string | undefined> };

describe('formatPeriod', () => {
  it('writes the month in full and the year, in the language it is asked for', () => {
    expect(formatPeriod('2026-10', 'en')).toBe('October 2026');
    expect(formatPeriod('2026-10', 'sv')).toBe('oktober 2026');
    expect(formatPeriod('2026-01', 'en')).toBe('January 2026');
  });

  it('is not moved by the time zone of the machine', () => {
    // The first of a month at midnight UTC is the evening before in a zone west of Greenwich.
    const previous = process.env.TZ;
    try {
      process.env.TZ = 'America/Los_Angeles';
      expect(formatPeriod('2026-03', 'en')).toBe('March 2026');
    } finally {
      process.env.TZ = previous;
    }
  });

  it('returns what it was given when that is not a month, and never throws', () => {
    expect(formatPeriod('2026-13', 'en')).toBe('2026-13');
    expect(formatPeriod('2026-1', 'en')).toBe('2026-1');
    expect(formatPeriod('soon', 'en')).toBe('soon');
    expect(formatPeriod('2026-10-05', 'en')).toBe('2026-10-05');
  });

  it('returns nothing for nothing', () => {
    expect(formatPeriod('', 'en')).toBe('');
    expect(formatPeriod(null, 'en')).toBe('');
    expect(formatPeriod(undefined, 'en')).toBe('');
  });

  it('falls back to English for a culture Intl does not know', () => {
    expect(formatPeriod('2026-10', 'not a culture!')).toBe('October 2026');
  });
});

describe('formatNumber', () => {
  it('groups thousands for the language and keeps at most two decimals', () => {
    expect(formatNumber(1234567, 'en')).toBe('1,234,567');
    expect(formatNumber(12.3456, 'en')).toBe('12.35');
    expect(formatNumber(1234.5, 'sv')).toBe('1 234,5');
  });

  it('leaves out what is not a number', () => {
    expect(formatNumber(null, 'en')).toBe('');
    expect(formatNumber(undefined, 'en')).toBe('');
    expect(formatNumber(Number.NaN, 'en')).toBe('');
    expect(formatNumber(Number.POSITIVE_INFINITY, 'en')).toBe('');
  });

  it('writes zero as zero', () => {
    expect(formatNumber(0, 'en')).toBe('0');
  });
});

describe('formatPercent', () => {
  // The value is already in percent (62.4 means 62.4 %), as the report's "percent" figures are.
  it('writes a figure that is already a percentage with the sign of the language', () => {
    expect(formatPercent(62.4, 'en')).toBe('62.4%');
    expect(formatPercent(62, 'en')).toBe('62%');
    expect(formatPercent(62.4, 'sv')).toBe('62,4 %');
  });

  it('can keep one decimal always, for a change that is shown as a figure', () => {
    expect(formatPercent(12, 'en', { fixed: true })).toBe('12.0%');
    expect(formatPercent(12.04, 'en', { fixed: true })).toBe('12.0%');
  });

  it('can write the sign of a change, also for a rise, and none for zero', () => {
    expect(formatPercent(30, 'en', { fixed: true, signed: true })).toBe('+30.0%');
    expect(formatPercent(-30, 'en', { fixed: true, signed: true })).toBe('-30.0%');
    expect(formatPercent(0, 'en', { fixed: true, signed: true })).toBe('0.0%');
    expect(formatPercent(30, 'en', { fixed: true })).toBe('30.0%');
  });

  it('keeps up to the number of decimals asked for: a click-through rate is shown with two, since 0.45% is not 0.5%', () => {
    expect(formatPercent(12.28, 'en', { digits: 2 })).toBe('12.28%');
    expect(formatPercent(0.45, 'en', { digits: 2 })).toBe('0.45%');
    expect(formatPercent(12.3, 'en', { digits: 2 })).toBe('12.3%');
    expect(formatPercent(12.28, 'en')).toBe('12.3%');
    expect(formatPercent(12.28, 'sv', { digits: 2 })).toBe('12,28 %');
  });

  it('leaves out what is not a number', () => {
    expect(formatPercent(null, 'en')).toBe('');
    expect(formatPercent(undefined, 'en')).toBe('');
    expect(formatPercent(Number.NaN, 'en')).toBe('');
  });
});

describe('formatDuration', () => {
  it('writes seconds as minutes:seconds', () => {
    expect(formatDuration(174)).toBe('2:54');
    expect(formatDuration(0)).toBe('0:00');
    expect(formatDuration(59)).toBe('0:59');
    expect(formatDuration(60)).toBe('1:00');
    expect(formatDuration(3725)).toBe('62:05');
  });

  it('rounds to a whole second and carries into the minute', () => {
    expect(formatDuration(173.6)).toBe('2:54');
    expect(formatDuration(119.6)).toBe('2:00');
  });

  it('leaves out what is not a number or is negative', () => {
    expect(formatDuration(null)).toBe('');
    expect(formatDuration(undefined)).toBe('');
    expect(formatDuration(Number.NaN)).toBe('');
    expect(formatDuration(-3)).toBe('');
  });
});

describe('formatKpiValue', () => {
  it('formats a figure the way its format says', () => {
    expect(formatKpiValue(12345, 'integer', 'en')).toBe('12,345');
    expect(formatKpiValue(12.5, 'decimal', 'en')).toBe('12.5');
    expect(formatKpiValue(62.4, 'percent', 'en')).toBe('62.4%');
    expect(formatKpiValue(174, 'duration', 'en')).toBe('2:54');
  });

  it('gives a signed figure its plus sign', () => {
    expect(formatKpiValue(40, 'signed', 'en')).toBe('+40');
    expect(formatKpiValue(-40, 'signed', 'en')).toBe('-40');
    expect(formatKpiValue(0, 'signed', 'en')).toBe('0');
  });

  it('writes an integer without decimals', () => {
    expect(formatKpiValue(12.6, 'integer', 'en')).toBe('13');
  });

  it('leaves out a missing value, whatever its format, so the caller can say "no data"', () => {
    for (const format of ['integer', 'signed', 'percent', 'decimal', 'duration']) {
      expect(formatKpiValue(null, format, 'en'), format).toBe('');
      expect(formatKpiValue(undefined, format, 'en'), format).toBe('');
      expect(formatKpiValue(Number.NaN, format, 'en'), format).toBe('');
    }
  });

  it('writes an unknown format as a plain number rather than guessing a unit', () => {
    expect(formatKpiValue(1234.5, 'surprise', 'en')).toBe('1,234.5');
  });
});

describe('formatDateTime', () => {
  it('writes a moment in the language it is asked for', () => {
    const en = formatDateTime('2026-11-19T12:30:00+00:00', 'en', 'UTC');

    expect(en).toContain('2026');
    expect(en).toContain('Nov');
    expect(en).toContain('12:30');
    expect(formatDateTime('2026-11-19T12:30:00Z', 'sv', 'UTC')).toContain('nov');
  });

  it('leaves out what is not a moment', () => {
    expect(formatDateTime(null, 'en')).toBe('');
    expect(formatDateTime(undefined, 'en')).toBe('');
    expect(formatDateTime('', 'en')).toBe('');
    expect(formatDateTime('yesterday-ish', 'en')).toBe('');
  });
});

describe('formatRelative', () => {
  const now = new Date('2026-11-19T12:00:00Z');

  it('says how long ago in the biggest fitting unit', () => {
    expect(formatRelative('2026-11-19T11:59:40Z', 'en', now)).toBe('now');
    expect(formatRelative('2026-11-19T11:55:00Z', 'en', now)).toBe('5 minutes ago');
    expect(formatRelative('2026-11-19T09:00:00Z', 'en', now)).toBe('3 hours ago');
    expect(formatRelative('2026-11-18T12:00:00Z', 'en', now)).toBe('yesterday');
    expect(formatRelative('2026-11-14T12:00:00Z', 'en', now)).toBe('5 days ago');
  });

  it('writes it in the other language too', () => {
    expect(formatRelative('2026-11-19T09:00:00Z', 'sv', now)).toBe('för 3 timmar sedan');
  });

  it('gives nothing for a moment more than a month back, which the caller shows as a date', () => {
    expect(formatRelative('2026-09-01T12:00:00Z', 'en', now)).toBe('');
  });

  it('gives nothing for a moment in the future or one that is not a moment', () => {
    expect(formatRelative('2026-11-20T12:00:00Z', 'en', now)).toBe('');
    expect(formatRelative(null, 'en', now)).toBe('');
    expect(formatRelative('garbage', 'en', now)).toBe('');
  });
});

describe('formatMonthDay', () => {
  it('writes a day as day and short month', () => {
    // The backoffice's English is British (its manager serves a bare "en" as en-gb), so the day comes first.
    expect(formatMonthDay('2026-11-05', 'en-gb')).toBe('5 Nov');
    expect(formatMonthDay('2026-11-05', 'sv')).toBe('5 nov.');
  });

  it('returns what it was given when that is not a day, and nothing for nothing', () => {
    expect(formatMonthDay('2026-11', 'en')).toBe('2026-11');
    expect(formatMonthDay('2026-02-31', 'en')).toBe('2026-02-31');
    expect(formatMonthDay(null, 'en')).toBe('');
  });
});

describe('formatMonthShort and formatMonthName', () => {
  it('write a calendar month number as its name, short for an axis and in full for a table', () => {
    expect(formatMonthShort('01', 'en-gb')).toBe('Jan');
    expect(formatMonthShort(12, 'en-gb')).toBe('Dec');
    expect(formatMonthShort('03', 'sv')).toBe('mars');
    expect(formatMonthName('09', 'en')).toBe('September');
    expect(formatMonthName(2, 'sv')).toBe('februari');
  });

  it('are not moved by the time zone of the machine', () => {
    const previous = process.env.TZ;
    try {
      process.env.TZ = 'America/Los_Angeles';
      expect(formatMonthName('01', 'en')).toBe('January');
      expect(formatMonthShort('12', 'en')).toBe('Dec');
    } finally {
      process.env.TZ = previous;
    }
  });

  it('return what they were given when it is not a month from 1 to 12, and nothing for nothing', () => {
    expect(formatMonthShort('13', 'en')).toBe('13');
    expect(formatMonthShort('00', 'en')).toBe('00');
    expect(formatMonthName('2026-10', 'en')).toBe('2026-10');
    expect(formatMonthName('x', 'en')).toBe('x');
    expect(formatMonthShort(null, 'en')).toBe('');
    expect(formatMonthName(undefined, 'en')).toBe('');
  });
});

describe('formatPeriodShort', () => {
  it('writes a month as a short month name and the year', () => {
    expect(formatPeriodShort('2026-10', 'en-gb')).toBe('Oct 2026');
    expect(formatPeriodShort('2026-10', 'sv')).toBe('okt. 2026');
  });

  it('returns what it was given when that is not a month, and nothing for nothing', () => {
    expect(formatPeriodShort('2026-13', 'en')).toBe('2026-13');
    expect(formatPeriodShort('', 'en')).toBe('');
    expect(formatPeriodShort(null, 'en')).toBe('');
  });
});

describe('formatDayLong', () => {
  it('writes a day with its weekday', () => {
    // Whether a comma follows the weekday depends on the ICU data of the runtime ("Thursday, 5 November" or "Thursday 5 November").
    expect(formatDayLong('2026-11-05', 'en-gb')).toMatch(/^Thursday,? 5 November 2026$/);
    expect(formatDayLong('2026-11-05', 'sv')).toMatch(/^torsdag,? 5 november 2026$/);
  });

  it('returns what it was given when that is not a real day, and nothing for nothing', () => {
    expect(formatDayLong('2026-02-31', 'en')).toBe('2026-02-31');
    expect(formatDayLong('2026-11', 'en')).toBe('2026-11');
    expect(formatDayLong(null, 'en')).toBe('');
  });
});

describe('formatFixed', () => {
  it('writes a figure with exactly the number of decimals asked for, grouped for the language', () => {
    expect(formatFixed(3, 'en', 1)).toBe('3.0');
    expect(formatFixed(14.46, 'en', 1)).toBe('14.5');
    expect(formatFixed(1234.5, 'en', 1)).toBe('1,234.5');
    expect(formatFixed(3, 'sv', 1)).toBe('3,0');
  });

  it('leaves out what is not a number', () => {
    expect(formatFixed(null, 'en', 1)).toBe('');
    expect(formatFixed(Number.NaN, 'en', 1)).toBe('');
  });
});

describe('formatCompact', () => {
  it('abbreviates a large number the way the language does, for an axis', () => {
    expect(formatCompact(12000, 'en')).toBe('12K');
    expect(formatCompact(1500000, 'en')).toBe('1.5M');
    expect(formatCompact(950, 'en')).toBe('950');
    expect(formatCompact(12000, 'sv')).toBe('12 tn');
  });

  it('leaves out what is not a number', () => {
    expect(formatCompact(null, 'en')).toBe('');
    expect(formatCompact(Number.POSITIVE_INFINITY, 'en')).toBe('');
  });
});
