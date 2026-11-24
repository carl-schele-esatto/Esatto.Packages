import { describe, expect, it } from 'vitest';
import { kpiTileView, toneOf, trendText } from './kpi-view';
import { kpi } from './testing/fixtures';
import { enHost } from './testing/host';
import type { Trend } from './types';

const host = enHost();

const trend = (direction: string, change: number | null, key = 'vsSameMonthLastYear'): Trend => ({
  direction,
  change_percent: change,
  text: { key },
});

describe('trendText', () => {
  it('writes a rise with an arrow, the word and the size, and the text it is measured against', () => {
    expect(trendText(host, trend('up', 12.34))).toEqual({
      pill: { direction: 'up', arrow: '↑', words: 'Up', figure: '12.3%' },
      note: 'vs the same month last year',
    });
  });

  it('writes a fall by its sign, without the minus (the arrow and the word carry it)', () => {
    expect(trendText(host, trend('down', -8))?.pill).toEqual({ direction: 'down', arrow: '↓', words: 'Down', figure: '8.0%' });
  });

  it('takes up or down from the sign of the figure, never from colour and not from the word the server sent', () => {
    expect(trendText(host, trend('down', 5))?.pill?.direction).toBe('up');
    expect(trendText(host, trend('up', -5))?.pill?.direction).toBe('down');
  });

  it('writes a note with a figure as flat, keeping the sign, so a fall measured against an odd baseline still reads as a fall', () => {
    expect(trendText(host, trend('note', -30, 'anomalousBaseline'))?.pill).toEqual({ direction: 'flat', arrow: '→', words: 'Change', figure: '-30.0%' });
    expect(trendText(host, trend('note', 3))?.pill?.figure).toBe('+3.0%');
  });

  it('writes no pill for a note without a figure, only its text', () => {
    expect(trendText(host, trend('note', null, 'engagementShare'))).toEqual({ pill: null, note: 'share of visits with engagement' });
  });

  it('is nothing for no trend, or for one with neither a figure nor a text', () => {
    expect(trendText(host, null)).toBeNull();
    expect(trendText(host, undefined)).toBeNull();
    expect(trendText(host, { direction: 'note', change_percent: null, text: null })).toBeNull();
  });

  it('writes the figure for the language', () => {
    expect(trendText(enHost('sv'), trend('up', 12.34))?.pill?.figure).toBe('12,3 %');
  });
});

describe('toneOf', () => {
  it('is good for a rise, critical for a fall and notice for anything else: an extra signal, never the only one', () => {
    expect(toneOf('up')).toBe('good');
    expect(toneOf('down')).toBe('critical');
    expect(toneOf('flat')).toBe('notice');
    expect(toneOf(null)).toBe('notice');
  });
});

describe('kpiTileView', () => {
  it('is the figure written by its format, with its label, description and icon', () => {
    const view = kpiTileView(host, kpi('organic_sessions', 12345, 'integer', { trend: trend('up', 4.2) }));

    expect(view).toMatchObject({
      label: 'Visits from search engines',
      description: 'Through the result lists of Google and other search engines',
      value: '12,345',
      hasValue: true,
      icon: 'icon-search',
      tone: 'good',
    });
    expect(view.trend?.pill?.arrow).toBe('↑');
  });

  it('writes a percentage, a duration and a signed figure the way their format says', () => {
    expect(kpiTileView(host, kpi('organic_engagement_rate', 62.4, 'percent')).value).toBe('62.4%');
    expect(kpiTileView(host, kpi('organic_session_seconds', 174, 'duration')).value).toBe('2:54');
    expect(kpiTileView(host, kpi('clicks_vs_sessions', 120, 'signed')).value).toBe('+120');
  });

  it('has no value, and says so in words, for a figure that is null: never a dash and never a zero', () => {
    const view = kpiTileView(host, kpi('total_sessions', null));

    expect(view.hasValue).toBe(false);
    expect(view.value).toBe('No data');
  });

  it('keeps a zero a value: no visits is a measurement', () => {
    const view = kpiTileView(host, kpi('total_sessions', 0));

    expect(view.hasValue).toBe(true);
    expect(view.value).toBe('0');
  });

  it('gives a figure the report brings that the client does not know the chart icon, so a new figure is still drawn', () => {
    expect(kpiTileView(host, kpi('something_new', 5)).icon).toBe('icon-chart');
  });

  it('does not throw for a key that is not well formed', () => {
    const view = kpiTileView(host, kpi('not a key', 5));

    expect(view.label).toBe('');
    expect(view.value).toBe('5');
  });

  it('has no trend for a figure without one, and the notice tone', () => {
    const view = kpiTileView(host, kpi('organic_sessions', 5));

    expect(view.trend).toBeNull();
    expect(view.tone).toBe('notice');
  });
});
