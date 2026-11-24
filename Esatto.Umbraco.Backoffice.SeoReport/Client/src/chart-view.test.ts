import { describe, expect, it } from 'vitest';
import {
  chartAriaLabel,
  chartCardState,
  chartHeight,
  chartLabels,
  chartRangeText,
  chartSeriesLabel,
  chartTableRows,
  chartText,
  labelKind,
} from './chart-view';
import { chart } from './testing/fixtures';
import { enHost } from './testing/host';

const host = enHost();

const months = ['01', '02', '03', '04', '05', '06', '07', '08', '09', '10', '11', '12'];
const periods = ['2025-11', '2025-12', '2026-01'];
const days = ['2026-11-01', '2026-11-02', '2026-11-03'];

describe('labelKind', () => {
  it('tells a chart pivoted by year (calendar months) from periods and from days', () => {
    expect(labelKind(months)).toBe('month');
    expect(labelKind(periods)).toBe('period');
    expect(labelKind(days)).toBe('day');
  });

  it('reads anything mixed or unknown as raw text, and no labels as raw', () => {
    expect(labelKind(['01', '2026-02'])).toBe('raw');
    expect(labelKind(['week 1', 'week 2'])).toBe('raw');
    expect(labelKind([])).toBe('raw');
  });

  it('does not take 13 or 00 for a month', () => {
    expect(labelKind(['00', '01'])).toBe('raw');
    expect(labelKind(['12', '13'])).toBe('raw');
  });
});

describe('chartLabels', () => {
  it('writes month numbers as month names: short on an axis, in full in the table', () => {
    const block = chart({ labels: ['01', '02', '12'] });

    expect(chartLabels(block, 'en-gb', 'axis')).toEqual(['Jan', 'Feb', 'Dec']);
    expect(chartLabels(block, 'en-gb', 'table')).toEqual(['January', 'February', 'December']);
  });

  it('writes periods as a month and year, in the language', () => {
    const block = chart({ labels: periods });

    expect(chartLabels(block, 'en-gb', 'axis')).toEqual(['Nov 2025', 'Dec 2025', 'Jan 2026']);
    expect(chartLabels(block, 'en-gb', 'table')).toEqual(['November 2025', 'December 2025', 'January 2026']);
    expect(chartLabels(block, 'sv', 'table')[0]).toBe('november 2025');
  });

  it('writes days as day and month on an axis, and with the weekday in the table', () => {
    const block = chart({ labels: days });

    expect(chartLabels(block, 'en-gb', 'axis')).toEqual(['1 Nov', '2 Nov', '3 Nov']);
    expect(chartLabels(block, 'en-gb', 'table')[0]).toMatch(/^Sunday,? 1 November 2026$/);
  });

  it('keeps raw labels as they are', () => {
    expect(chartLabels(chart({ labels: ['a', 'b'] }), 'en', 'axis')).toEqual(['a', 'b']);
  });
});

describe('chartSeriesLabel', () => {
  const series = (key: string) => ({ key, values: [], emphasis: false, style: 'solid' as const });

  it('writes a series by its key, with the year the key carries', () => {
    expect(chartSeriesLabel(host, series('sessions_2026'))).toBe('2026 visits');
    expect(chartSeriesLabel(host, series('clicks_2025'))).toBe('2025 clicks');
    expect(chartSeriesLabel(host, series('year_2024'))).toBe('2024');
  });

  it('writes a series without a year as it is', () => {
    expect(chartSeriesLabel(host, series('brand'))).toBe('Brand');
    expect(chartSeriesLabel(host, series('non_brand'))).toBe('Non-brand');
    expect(chartSeriesLabel(host, series('daily'))).toBe('Visits per day');
    expect(chartSeriesLabel(host, series('baseline'))).toBe('Average of the preceding period');
  });

  it('does not throw for a key that is not well formed: the key itself is the text', () => {
    expect(chartSeriesLabel(host, series('not a key'))).toBe('not a key');
  });
});

describe('chartText', () => {
  it('writes the title, subtitle and description of a chart by its id', () => {
    const block = chart({ id: 'traffic_total' });

    expect(chartText(host, block, 'Title')).toBe('All visits: every channel');
    expect(chartText(host, block, 'Subtitle')).toBe('All visits per month, wherever they came from');
    expect(chartText(host, block, 'Description')).toMatch(/^This chart shows all visits together/);
  });

  it('writes the daily chart with the arguments that come beside it', () => {
    const block = chart({ id: 'daily_traffic' });

    expect(chartText(host, block, 'Title', { period: '2026-10', windowDays: 31, provisionalDays: null })).toBe('October 2026');
    expect(chartText(host, block, 'Title', { period: null, windowDays: 30, provisionalDays: 3 })).toBe('The last 30 days');
  });

  it('is empty for a chart with no id or a malformed one', () => {
    expect(chartText(host, chart({ id: '' }), 'Title')).toBe('');
    expect(chartText(host, chart({ id: 'no way' }), 'Title')).toBe('');
  });
});

describe('chartRangeText', () => {
  it('for a chart pivoted by year is the years of its series', () => {
    const block = chart({
      labels: months,
      series: [
        { key: 'sessions_2024', values: [], emphasis: false, style: 'dashed' },
        { key: 'clicks_2024', values: [], emphasis: false, style: 'dashed' },
        { key: 'sessions_2026', values: [], emphasis: false, style: 'solid' },
      ],
    });

    expect(chartRangeText(host, block)).toBe('2024 to 2026');
  });

  it('for a single year is that year', () => {
    const block = chart({ labels: months, series: [{ key: 'year_2026', values: [1], emphasis: true, style: 'solid' }] });

    expect(chartRangeText(host, block)).toBe('2026');
  });

  it('for periods is the first and the last month', () => {
    expect(chartRangeText(host, chart({ labels: periods }))).toBe('November 2025 to January 2026');
  });

  it('for days is the first and the last day', () => {
    expect(chartRangeText(host, chart({ labels: days }))).toBe('1 Nov to 3 Nov');
  });

  it('is the one point when there is one', () => {
    expect(chartRangeText(host, chart({ labels: ['2026-11'] }))).toBe('November 2026');
  });

  it('is empty for a chart with no labels, no series, raw labels, or a pivot whose series carry no year', () => {
    expect(chartRangeText(host, chart({ labels: [] }))).toBe('');
    expect(chartRangeText(host, chart({ labels: periods, series: [] }))).toBe('');
    expect(chartRangeText(host, chart({ labels: ['a', 'b'] }))).toBe('');
    expect(chartRangeText(host, chart({ labels: months, series: [{ key: 'daily', values: [1], emphasis: false, style: 'solid' }] }))).toBe('');
  });
});

describe('chartTableRows', () => {
  it('is the exact grid of the chart: one row per label, one column per series, in the order they come', () => {
    const block = chart({
      labels: ['01', '02', '03'],
      series: [
        { key: 'sessions_2025', values: [1200, 1300.5, 1400], emphasis: false, style: 'solid' },
        { key: 'sessions_2026', values: [2000, 2100, 12345], emphasis: true, style: 'solid' },
      ],
    });

    expect(chartTableRows(host, block)).toEqual({
      periodHeader: 'Month',
      seriesHeaders: ['2025 visits', '2026 visits'],
      rows: [
        { label: 'January', cells: ['1,200', '2,000'] },
        { label: 'February', cells: ['1,300.5', '2,100'] },
        { label: 'March', cells: ['1,400', '12,345'] },
      ],
    });
  });

  it('writes a gap as "No data", never a dash and never a zero', () => {
    const block = chart({
      labels: ['01', '02', '03'],
      series: [{ key: 'year_2026', values: [5, null, 0], emphasis: true, style: 'solid' }],
    });

    const cells = chartTableRows(host, block).rows.map((row) => row.cells[0]);

    expect(cells).toEqual(['5', 'No data', '0']);
    expect(JSON.stringify(chartTableRows(host, block))).not.toMatch(/[–—-]\s*"/);
  });

  it('reads a value the series lacks (a series shorter than its labels) as a gap', () => {
    const block = chart({ labels: ['01', '02', '03'], series: [{ key: 'year_2026', values: [5], emphasis: true, style: 'solid' }] });

    expect(chartTableRows(host, block).rows.map((row) => row.cells[0])).toEqual(['5', 'No data', 'No data']);
  });

  it('names the first column by what the labels are', () => {
    expect(chartTableRows(host, chart({ labels: days })).periodHeader).toBe('Day');
    expect(chartTableRows(host, chart({ labels: periods })).periodHeader).toBe('Month');
    expect(chartTableRows(host, chart({ labels: ['a', 'b', 'c'] })).periodHeader).toBe('Period');
  });

  it('formats the numbers for the language', () => {
    const block = chart({ labels: ['2026-01'], series: [{ key: 'brand', values: [12345.5], emphasis: false, style: 'solid' }] });

    expect(chartTableRows(enHost('sv'), block).rows[0].cells[0]).toBe('12 345,5');
  });

  it('has no rows for a chart with no labels or no series', () => {
    expect(chartTableRows(host, chart({ labels: [] })).rows).toEqual([]);
    expect(chartTableRows(host, chart({ series: [] })).rows).toEqual([]);
  });
});

describe('chartCardState', () => {
  it('is a chart when there is a value to draw', () => {
    expect(chartCardState(chart())).toBe('chart');
  });

  it('is empty, not a chart, for a block with no series, only gaps, or none at all', () => {
    expect(chartCardState(chart({ series: [] }))).toBe('empty');
    expect(chartCardState(chart({ series: [{ key: 'year_2026', values: [null, null], emphasis: true, style: 'solid' }] }))).toBe('empty');
    expect(chartCardState(null)).toBe('empty');
  });

  it('is a chart for a series of zeros: a day without visits is zero visits', () => {
    expect(chartCardState(chart({ series: [{ key: 'daily', values: [0, 0], emphasis: true, style: 'solid' }] }))).toBe('chart');
  });
});

describe('chartAriaLabel', () => {
  it('is the title, the subtitle and the range, which the data table then spells out', () => {
    const block = chart({ id: 'traffic_total', labels: periods });

    expect(chartAriaLabel(host, block)).toBe(
      'All visits: every channel. All visits per month, wherever they came from. November 2025 to January 2026',
    );
  });

  it('leaves out what the chart has none of', () => {
    expect(chartAriaLabel(host, chart({ id: '', labels: [] }))).toBe('');
  });
});

describe('chartHeight', () => {
  it('is the height the block asks for, in pixels', () => {
    expect(chartHeight(chart({ height: 290 }))).toBe(290);
    expect(chartHeight(chart({ height: 240 }))).toBe(240);
  });

  it('falls back to 240 when the block gives none, and keeps a silly one within reason', () => {
    expect(chartHeight(chart({ height: 0 }))).toBe(240);
    expect(chartHeight(chart({ height: Number.NaN }))).toBe(240);
    expect(chartHeight(null)).toBe(240);
    expect(chartHeight(chart({ height: 20 }))).toBe(120);
    expect(chartHeight(chart({ height: 5000 }))).toBe(600);
  });
});
