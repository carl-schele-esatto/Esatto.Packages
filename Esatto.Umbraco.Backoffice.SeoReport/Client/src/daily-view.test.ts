import { describe, expect, it } from 'vitest';
import { chartText } from './chart-view';
import { dailyMonthOptions, dailyTextArgs, dailyView, provisionalDaysShown } from './daily-view';
import { chart } from './testing/fixtures';
import { enHost } from './testing/host';
import type { DailyResponse } from './types';

const host = enHost();

// A response for a rolling window of 30 days, with invented figures; overrides replace whole members.
function daily(overrides: Partial<DailyResponse> = {}): DailyResponse {
  const days = ['2026-11-01', '2026-11-02', '2026-11-03'].map((date, index) => ({ date, sessions: 100 + index * 50 }));
  return {
    chart: chart({
      id: 'daily_traffic',
      labels: days.map((day) => day.date),
      series: [
        { key: 'daily', values: [100, 150, 200], emphasis: true, style: 'solid' },
        { key: 'baseline', values: [90, 90, 90], emphasis: false, style: 'dashed' },
      ],
    }),
    textArgs: { period: null, windowDays: 30, provisionalDays: 3 },
    months: ['', '2026-11', '2026-10'],
    window: { start: '2026-10-05', end: '2026-11-03', month: null },
    total: 4500,
    changePercent: 12.34,
    peak: { date: '2026-11-03', sessions: 200 },
    days,
    windowDays: 30,
    ...overrides,
  };
}

describe('provisionalDaysShown', () => {
  it('is the smaller of the server count and the days the window has: a window cannot hold more preliminary days than days', () => {
    expect(provisionalDaysShown(3, 30)).toBe(3);
    expect(provisionalDaysShown(3, 2)).toBe(2);
    expect(provisionalDaysShown(3, 1)).toBe(1);
  });

  it('is none when the server has none, or the window has no days (a month on its first day)', () => {
    expect(provisionalDaysShown(null, 30)).toBeNull();
    expect(provisionalDaysShown(undefined, 30)).toBeNull();
    expect(provisionalDaysShown(0, 30)).toBeNull();
    expect(provisionalDaysShown(3, 0)).toBeNull();
    expect(provisionalDaysShown('3' as never, 30)).toBeNull();
    expect(provisionalDaysShown(Number.NaN, 30)).toBeNull();
  });
});

describe('dailyTextArgs', () => {
  it('is what the chart texts are written with: the month, the days of the window, and the preliminary days actually shown', () => {
    expect(dailyTextArgs(daily({ textArgs: { period: '2026-03', windowDays: 31, provisionalDays: null }, windowDays: 31 }))).toEqual({
      period: '2026-03',
      windowDays: 31,
      provisionalDays: null,
    });
    expect(dailyTextArgs(daily({ textArgs: { period: null, windowDays: 30, provisionalDays: 3 } }))).toEqual({ period: null, windowDays: 30, provisionalDays: 3 });
  });

  it('caps the preliminary days at the days of the window', () => {
    expect(dailyTextArgs(daily({ textArgs: { period: '2026-11', windowDays: 1, provisionalDays: 3 }, windowDays: 1 })).provisionalDays).toBe(1);
  });

  it('carries no preliminary days for a window with no days', () => {
    expect(dailyTextArgs(daily({ textArgs: { period: '2026-11', windowDays: 0, provisionalDays: 3 }, windowDays: 0 })).provisionalDays).toBeNull();
  });

  it('keeps the texts free of a claim the data does not make: the description says nothing is preliminary when no day is', () => {
    const written = (response: DailyResponse) => {
      const view = dailyView(host, response);
      return chartText(host, view.chart, 'Description', view.textArgs);
    };

    expect(written(daily({ textArgs: { period: '2026-03', windowDays: 31, provisionalDays: null }, windowDays: 31 }))).not.toContain('preliminary');
    expect(written(daily())).toContain('The last 3 days are preliminary');
  });
});

describe('dailyMonthOptions', () => {
  it('offers the rolling window first, named by its length, and then each month in the language', () => {
    expect(dailyMonthOptions(host, ['', '2026-11', '2026-10'], 30)).toEqual([
      { value: '', label: 'Last 30 days' },
      { value: '2026-11', label: 'November 2026' },
      { value: '2026-10', label: 'October 2026' },
    ]);
  });

  it('names the rolling window without a length when none is known yet', () => {
    expect(dailyMonthOptions(host, [''], null)[0].label).toBe('Recent days');
  });

  it('writes the months for the language of the page', () => {
    expect(dailyMonthOptions(enHost('sv'), ['', '2026-11'], 30)[1].label).toBe('november 2026');
  });

  it('is empty when the server offers nothing', () => {
    expect(dailyMonthOptions(host, [], 30)).toEqual([]);
  });
});

describe('dailyView', () => {
  it('writes the visits of the window and the change against the one before it, with an arrow and a word', () => {
    const view = dailyView(host, daily());

    expect(view.hasDays).toBe(true);
    expect(view.total).toBe('4,500');
    expect(view.change).toEqual({ direction: 'up', arrow: '↑', words: 'Up', figure: '12.3%', against: 'vs the previous 30 days' });
    expect(view.noComparison).toBe(false);
  });

  it('writes a fall by its sign, and compares a month with the previous month', () => {
    const view = dailyView(host, daily({ changePercent: -8.2, window: { start: '2026-10-01', end: '2026-10-31', month: '2026-10' }, textArgs: { period: '2026-10', windowDays: 31, provisionalDays: null }, windowDays: 31 }));

    expect(view.change).toMatchObject({ direction: 'down', arrow: '↓', words: 'Down', figure: '8.2%', against: 'vs the previous month' });
  });

  it('shows no change on the first day of a month: the window has no days and "-100%" would mean nothing', () => {
    const view = dailyView(host, daily({ changePercent: -100, windowDays: 0, total: 0, days: [], textArgs: { period: '2026-11', windowDays: 0, provisionalDays: 3 }, window: { start: '2026-11-01', end: '2026-10-31', month: '2026-11' } }));

    expect(view.hasDays).toBe(false);
    expect(view.change).toBeNull();
    expect(view.noComparison).toBe(false);
  });

  it('says there is no comparison when the period before had no visits', () => {
    const view = dailyView(host, daily({ changePercent: null }));

    expect(view.change).toBeNull();
    expect(view.noComparison).toBe(true);
  });

  it('reads a change of nothing as flat, not as a rise', () => {
    expect(dailyView(host, daily({ changePercent: 0 })).change).toMatchObject({ direction: 'flat', arrow: '→', words: 'Change', figure: '0.0%' });
  });

  it('writes the peak day with its weekday and its visits, formatted here', () => {
    const view = dailyView(host, daily());

    expect(view.peak).toMatch(/^Peak day: Tuesday,? 3 November 2026, 200 visits\.$/);
  });

  it('has no peak line when the window has none', () => {
    expect(dailyView(host, daily({ peak: null })).peak).toBe('');
  });

  it('offers each day of the window for the day picker, written with its weekday and visits', () => {
    const view = dailyView(host, daily());

    expect(view.days.map((day) => day.date)).toEqual(['2026-11-01', '2026-11-02', '2026-11-03']);
    expect(view.days[0].label).toMatch(/^Sunday,? 1 November 2026: 100$/);
  });

  it('selects the rolling window as the empty string and a month as its YYYY-MM', () => {
    expect(dailyView(host, daily()).selected).toBe('');
    expect(dailyView(host, daily({ window: { start: '2026-10-01', end: '2026-10-31', month: '2026-10' } })).selected).toBe('2026-10');
  });

  it('keeps the chart for the card, and the title and subtitle of it from the arguments', () => {
    const view = dailyView(host, daily());

    expect(view.chart.id).toBe('daily_traffic');
    expect(view.textArgs).toEqual({ period: null, windowDays: 30, provisionalDays: 3 });
  });

  it('writes the numbers for the language', () => {
    expect(dailyView(enHost('sv'), daily()).total).toBe('4 500');
  });
});
