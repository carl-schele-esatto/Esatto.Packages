// What the daily traffic card says about a response of GET daily, without a DOM: the picker's options, the figures above the chart, the peak day,
// the days to pick from, and the arguments the chart's texts are written with. Pure functions over a localizer.
//
// THE SERVER SENDS NUMBERS AND DATES, THE CLIENT WRITES THEM: "YYYY-MM-DD" days, "YYYY-MM" months, raw counts. Three rules decide what is claimed:
//   - "The last days are preliminary" is only said about days that exist and can still be revised: the count is the smaller of the server's
//     margin and the days the window has, and none (null) leaves the sentence out (provisionalDaysShown).
//   - A change against the period before is not shown when the window has no days (a month on its first day, where it would read "-100%" and
//     mean nothing), and says "no comparison" when the period before had no visits.
//   - A direction is read from the sign of the figure and written as an arrow and a word.

import { formatDayLong, formatNumber, formatPeriod } from './format';
import { trendText } from './kpi-view';
import { culture, text, type Localizer } from './localize';
import type { ChartBlock, DailyResponse, TextArgs } from './types';

/**
 * How many of the days shown can still be revised by GA4. The server counts the margin (its `provisionalDays`) from the end of the window, which
 * for a month on its first or second day reaches back past the first day, so a window cannot have more preliminary days than days. Null when the
 * server has none (a finished month older than the margin), when the window has no days, and for anything that is not a count above zero.
 */
export function provisionalDaysShown(provisionalDays: number | null | undefined, windowDays: number): number | null {
  if (typeof provisionalDays !== 'number' || !Number.isFinite(provisionalDays) || provisionalDays <= 0) return null;
  const shown = Math.min(provisionalDays, windowDays);
  return shown > 0 ? shown : null;
}

/** The arguments the chart's title, subtitle and description are written with (DailyChart.TextArgs, corrected by the rule above). */
export function dailyTextArgs(daily: DailyResponse): TextArgs {
  return {
    period: typeof daily.textArgs?.period === 'string' ? daily.textArgs.period : null,
    windowDays: daily.windowDays,
    provisionalDays: provisionalDaysShown(daily.textArgs?.provisionalDays as number | null | undefined, daily.windowDays),
  };
}

export interface DailyMonthOption {
  /** The empty string is the rolling window; anything else is "YYYY-MM". */
  value: string;
  label: string;
}

/**
 * What the picker offers: the rolling window first (named by its length, which the client knows once it has read the window), then the months
 * the server lists, newest first, in the language.
 */
export function dailyMonthOptions(host: Localizer, months: readonly string[], rollingDays: number | null): DailyMonthOption[] {
  return months.map((month) => ({
    value: month,
    label: month === '' ? text(host, 'uiDailyRollingLabel', { windowDays: rollingDays }) : formatPeriod(month, culture(host)),
  }));
}

export interface DailyChange {
  direction: 'up' | 'down' | 'flat';
  arrow: string;
  words: string;
  figure: string;
  /** What it is measured against: the previous month, or the previous days of the same length. */
  against: string;
}

export interface DailyView {
  /** The picker's value for this window: "" for the rolling window, else the month. */
  selected: string;
  /** The window has days to show; false for a running month on its first day. */
  hasDays: boolean;
  total: string;
  change: DailyChange | null;
  /** The period before had no visits, so there is nothing to compare with. */
  noComparison: boolean;
  /** The sentence about the busiest day, or ''. */
  peak: string;
  days: { date: string; label: string }[];
  textArgs: TextArgs;
  chart: ChartBlock;
}

export function dailyView(host: Localizer, daily: DailyResponse): DailyView {
  const c = culture(host);
  const hasDays = daily.windowDays > 0;
  const month = daily.window?.month ?? null;

  let change: DailyChange | null = null;
  if (hasDays && typeof daily.changePercent === 'number') {
    const pill = trendText(host, { direction: daily.changePercent >= 0 ? 'up' : 'down', change_percent: daily.changePercent, text: null })?.pill;
    if (pill) {
      change = { ...pill, against: month ? text(host, 'uiDailyAgainstMonthText') : text(host, 'uiDailyAgainstDaysText', { windowDays: daily.windowDays }) };
    }
  }

  return {
    selected: month ?? '',
    hasDays,
    total: formatNumber(daily.total, c),
    change,
    noComparison: hasDays && daily.changePercent === null,
    peak: daily.peak ? text(host, 'uiDailyPeakText', { day: formatDayLong(daily.peak.date, c), sessions: formatNumber(daily.peak.sessions, c) }) : '',
    days: (daily.days ?? []).map((day) => ({
      date: day.date,
      label: text(host, 'uiDailyDayOptionText', { day: formatDayLong(day.date, c), sessions: formatNumber(day.sessions, c) }),
    })),
    textArgs: dailyTextArgs(daily),
    chart: daily.chart,
  };
}
