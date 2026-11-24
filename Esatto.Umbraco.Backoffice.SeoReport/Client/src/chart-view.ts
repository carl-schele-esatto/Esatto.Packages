// What a chart card says about a chart block, decided without a DOM: the words of its heading, its x-axis labels, the legend's series names, the
// range it covers, and the data table that stands in for the picture (WCAG 1.1.1). Pure functions over a localizer (the element the text is written
// in), so the hidden table and the axis are tested with the real English texts.
//
// A chart block's labels are raw (Report.cs, ChartBlock.Labels):
//   month   "01".."12": a chart pivoted by year (one series per year), the x-axis is the calendar month
//   period  "YYYY-MM": every other chart of the report
//   day     "YYYY-MM-DD": the daily chart
// and the client formats them for the language. A gap in a series is null: in the table it reads "No data", never a dash and never zero.

import {
  formatDayLong,
  formatMonthDay,
  formatMonthName,
  formatMonthShort,
  formatNumber,
  formatPeriod,
  formatPeriodShort,
} from './format';
import { isEmptyChart } from './report-helpers';
import { culture, idText, seriesText, seriesYear, text, type Localizer } from './localize';
import type { ChartBlock, Series, TextArgs } from './types';

export type LabelKind = 'month' | 'period' | 'day' | 'raw';

const MONTH_NUMBER = /^(0[1-9]|1[0-2])$/;
const PERIOD = /^\d{4}-(0[1-9]|1[0-2])$/;
const DAY = /^\d{4}-\d{2}-\d{2}$/;

/** What the labels of a chart are. All of one kind, or raw: a chart that mixes kinds is not one this client knows how to write. */
export function labelKind(labels: readonly string[]): LabelKind {
  if (labels.length === 0) return 'raw';
  if (labels.every((label) => MONTH_NUMBER.test(label))) return 'month';
  if (labels.every((label) => PERIOD.test(label))) return 'period';
  if (labels.every((label) => DAY.test(label))) return 'day';
  return 'raw';
}

/**
 * The x-axis labels, written for the language. `axis` is short (it has to fit under each point), `table` is in full (it is read out). A label
 * that is not of the kind the others are is kept as it came.
 */
export function chartLabels(block: ChartBlock, cultureName: string, form: 'axis' | 'table'): string[] {
  const labels = block.labels ?? [];

  switch (labelKind(labels)) {
    case 'month':
      return labels.map((label) => (form === 'axis' ? formatMonthShort(label, cultureName) : formatMonthName(label, cultureName)));
    case 'period':
      return labels.map((label) => (form === 'axis' ? formatPeriodShort(label, cultureName) : formatPeriod(label, cultureName)));
    case 'day':
      return labels.map((label) => (form === 'axis' ? formatMonthDay(label, cultureName) : formatDayLong(label, cultureName)));
    default:
      return [...labels];
  }
}

/** The name of a series for the legend, the tooltip and the table: by its key, with the year the key carries. A key that is not well formed is its own text. */
export function chartSeriesLabel(host: Localizer, series: Pick<Series, 'key'>): string {
  try {
    const { key, args } = seriesText(series.key);
    return text(host, key, args);
  } catch (error) {
    if (error instanceof RangeError) return series.key;
    throw error;
  }
}

/** The title, subtitle or description of a chart by its id; nothing for a chart with no (or a malformed) id. `args` are the daily chart's, which come beside it. */
export function chartText(host: Localizer, block: ChartBlock, part: 'Title' | 'Subtitle' | 'Description', args?: TextArgs | null): string {
  return block?.id ? idText(host, 'chart', block.id, part, args) : '';
}

/**
 * What stretch the chart covers, built here because the server's subtitles do not carry it: the years of the series for a chart pivoted by year,
 * the first and last month for periods, the first and last day for days. Nothing for a chart with no points, no series, or labels that are not any
 * of those.
 */
export function chartRangeText(host: Localizer, block: ChartBlock): string {
  const labels = block?.labels ?? [];
  if (labels.length === 0 || (block.series ?? []).length === 0) return '';

  const c = culture(host);
  let from: string;
  let to: string;

  switch (labelKind(labels)) {
    case 'month': {
      const years = block.series.map((series) => seriesYear(series.key)).filter((year): year is number => year !== null);
      if (years.length === 0) return '';
      from = String(Math.min(...years));
      to = String(Math.max(...years));
      break;
    }
    case 'period':
      from = formatPeriod(labels[0], c);
      to = formatPeriod(labels[labels.length - 1], c);
      break;
    case 'day':
      from = formatMonthDay(labels[0], c);
      to = formatMonthDay(labels[labels.length - 1], c);
      break;
    default:
      return '';
  }

  return text(host, 'uiChartRangeText', { from, to });
}

/** The name of a canvas for assistive technology: the title, the subtitle and the range. The data table next to it spells the values out. */
export function chartAriaLabel(host: Localizer, block: ChartBlock, args?: TextArgs | null): string {
  return [chartText(host, block, 'Title', args), chartText(host, block, 'Subtitle', args), chartRangeText(host, block)].filter(Boolean).join('. ');
}

export interface ChartTable {
  /** The first column's header: Month, Day or Period. */
  periodHeader: string;
  seriesHeaders: string[];
  rows: { label: string; cells: string[] }[];
}

const PERIOD_HEADER: Record<LabelKind, string> = {
  month: 'uiColumnMonthLabel',
  period: 'uiColumnMonthLabel',
  day: 'uiColumnDayLabel',
  raw: 'uiColumnPeriodLabel',
};

/**
 * The data behind a chart as a grid, exactly: one row per label, one column per series, in the order the block has them. A value the series does
 * not have (null, or a series shorter than its labels) reads "No data". This is the hidden table that comes with every chart, so the values are in
 * text somewhere (WCAG 1.1.1). No rows for a chart with no labels or no series.
 */
export function chartTableRows(host: Localizer, block: ChartBlock): ChartTable {
  const labels = block?.labels ?? [];
  const series = block?.series ?? [];
  const c = culture(host);
  const periodHeader = text(host, PERIOD_HEADER[labelKind(labels)]);
  const seriesHeaders = series.map((item) => chartSeriesLabel(host, item));

  if (labels.length === 0 || series.length === 0) return { periodHeader, seriesHeaders, rows: [] };

  const noData = text(host, 'uiNoValueText');
  const written = chartLabels(block, c, 'table');

  return {
    periodHeader,
    seriesHeaders,
    rows: labels.map((_, index) => ({
      label: written[index],
      cells: series.map((item) => {
        const value = item.values?.[index];
        return value === null || value === undefined ? noData : formatNumber(value, c) || noData;
      }),
    })),
  };
}

/** What a chart card shows: the chart, or the no-data message (a block with no series or only gaps is never drawn as an empty plot). */
export function chartCardState(block: ChartBlock | null | undefined): 'chart' | 'empty' {
  return isEmptyChart(block) ? 'empty' : 'chart';
}

const DEFAULT_HEIGHT = 240;

/** The height of the plot in pixels: the block's own (290 for the year charts, 240 for the others), within reason. */
export function chartHeight(block: ChartBlock | null | undefined): number {
  const height = block?.height;
  if (typeof height !== 'number' || !Number.isFinite(height) || height <= 0) return DEFAULT_HEIGHT;
  return Math.min(600, Math.max(120, height));
}
