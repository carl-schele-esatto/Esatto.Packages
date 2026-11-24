import { describe, expect, it } from 'vitest';
import { chart, kpi, report, searchBlock, warning, info } from './testing/fixtures';
import {
  brandChartsAvailable,
  hasWarnings,
  heroKpis,
  isEmptyChart,
  tileKpis,
  trendView,
  warningCount,
} from './report-helpers';

describe('isEmptyChart', () => {
  it('is empty without a chart', () => {
    expect(isEmptyChart(null)).toBe(true);
    expect(isEmptyChart(undefined)).toBe(true);
  });

  it('is empty without series', () => {
    expect(isEmptyChart(chart({ series: [] }))).toBe(true);
  });

  it('is empty when no series has a value, however many series there are', () => {
    const none = { emphasis: false, style: 'solid' as const };

    expect(isEmptyChart(chart({ series: [{ key: 'a', values: [null, null], ...none }] }))).toBe(true);
    expect(isEmptyChart(chart({ series: [{ key: 'a', values: [], ...none }, { key: 'b', values: [null], ...none }] }))).toBe(true);
  });

  it('is not empty when one series has one real value', () => {
    const none = { emphasis: false, style: 'solid' as const };

    expect(isEmptyChart(chart({ series: [{ key: 'a', values: [null, null], ...none }, { key: 'b', values: [null, 3], ...none }] }))).toBe(false);
  });

  it('counts zero as a value: a day without visits is zero visits, not no data', () => {
    expect(isEmptyChart(chart({ series: [{ key: 'a', values: [0, 0], emphasis: true, style: 'solid' }] }))).toBe(false);
  });

  it('is not empty for an ordinary chart', () => {
    expect(isEmptyChart(chart())).toBe(false);
  });
});

describe('brandChartsAvailable', () => {
  it('is true when the search block has segments', () => {
    expect(brandChartsAvailable(searchBlock())).toBe(true);
  });

  it('is false without segments, and without a search block (an older report)', () => {
    expect(brandChartsAvailable(searchBlock({ segments: [] }))).toBe(false);
    expect(brandChartsAvailable(null)).toBe(false);
    expect(brandChartsAvailable(undefined)).toBe(false);
  });
});

describe('hasWarnings', () => {
  it('is true when the search block carries a warning, which is what makes the To fix tab exist', () => {
    const r = report({}, searchBlock({ annotations: [info(), warning()] }));

    expect(hasWarnings(r)).toBe(true);
    expect(warningCount(r)).toBe(1);
  });

  it('is false for notes only, for no annotations, for no search block and for no report', () => {
    expect(hasWarnings(report({}, searchBlock({ annotations: [info()] })))).toBe(false);
    expect(hasWarnings(report({}, searchBlock()))).toBe(false);
    expect(hasWarnings(report({}, null))).toBe(false);
    expect(hasWarnings(null)).toBe(false);
    expect(warningCount(null)).toBe(0);
  });
});

describe('heroKpis', () => {
  it('takes organic visits as the main figure and the engagement rate for the ring', () => {
    const kpis = [kpi('organic_sessions', 1200), kpi('organic_engagement_rate', 62.4, 'percent'), kpi('total_sessions', 5000)];

    const { main, ring } = heroKpis(kpis);

    expect(main?.key).toBe('organic_sessions');
    expect(ring?.key).toBe('organic_engagement_rate');
  });

  it('falls back to the first figure for the main one', () => {
    expect(heroKpis([kpi('total_sessions', 5000)]).main?.key).toBe('total_sessions');
    expect(heroKpis([]).main).toBeNull();
  });

  it('leaves the ring out when the share is missing, below zero or above a hundred: a ring is a share of a whole', () => {
    for (const value of [null, -1, 100.5, 250]) {
      expect(heroKpis([kpi('organic_engagement_rate', value, 'percent')]).ring, String(value)).toBeNull();
    }
  });

  it('draws the ring for 0 and for 100', () => {
    expect(heroKpis([kpi('organic_engagement_rate', 0, 'percent')]).ring?.value).toBe(0);
    expect(heroKpis([kpi('organic_engagement_rate', 100, 'percent')]).ring?.value).toBe(100);
  });

  it('does not draw a ring for a figure that is not a percentage', () => {
    expect(heroKpis([kpi('organic_engagement_rate', 50, 'integer')]).ring).toBeNull();
  });
});

describe('tileKpis', () => {
  it('lists every figure except the main one and the one the ring shows', () => {
    const kpis = [kpi('organic_sessions', 1200), kpi('organic_engagement_rate', 62.4, 'percent'), kpi('total_sessions', 5000)];

    expect(tileKpis(kpis).map((k) => k.key)).toEqual(['total_sessions']);
  });

  it('keeps the engagement rate as a tile when there is no ring for it, so the figure is not lost', () => {
    const kpis = [kpi('organic_sessions', 1200), kpi('organic_engagement_rate', null, 'percent')];

    expect(tileKpis(kpis).map((k) => k.key)).toEqual(['organic_engagement_rate']);
  });
});

describe('trendView', () => {
  const trend = (direction: string, change: number | null) => ({ direction, change_percent: change, text: { key: 'vsSameMonthLastYear' } });

  it('shows an arrow with the size of the change', () => {
    expect(trendView(trend('up', 12.34))).toEqual({ direction: 'up', arrow: '↑', change: 12.3 });
    expect(trendView(trend('down', -8))).toEqual({ direction: 'down', arrow: '↓', change: 8 });
  });

  it('shows the size without a sign: the arrow carries the direction', () => {
    expect(trendView(trend('down', -8))?.change).toBe(8);
  });

  it('shows a flat arrow for a note that has a figure', () => {
    expect(trendView(trend('note', 3))).toEqual({ direction: 'flat', arrow: '→', change: 3 });
  });

  it('keeps the sign of a figure that has no direction of its own: a fall measured against an anomalous baseline is still a fall', () => {
    expect(trendView(trend('note', -30))).toEqual({ direction: 'flat', arrow: '→', change: -30 });
    expect(trendView(trend('note', -30.04))?.change).toBe(-30);
    expect(trendView(trend('note', 12.34))?.change).toBe(12.3);
  });

  it('shows no pill for a trend without a figure, for a missing one and for a figure that is not a number', () => {
    expect(trendView(trend('up', null))).toBeNull();
    expect(trendView(null)).toBeNull();
    expect(trendView(undefined)).toBeNull();
    expect(trendView(trend('up', Number.NaN))).toBeNull();
  });

  it('takes up or down from the SIGN of the figure, not from the word the server sends with it', () => {
    // A rise the server called "down" (or the other way round) is still read by its sign; the arrow never contradicts the number.
    expect(trendView(trend('down', 5))).toEqual({ direction: 'up', arrow: '↑', change: 5 });
    expect(trendView(trend('up', -5))).toEqual({ direction: 'down', arrow: '↓', change: 5 });
  });

  it('shows a flat arrow for a change that rounds to nothing, whatever direction the server gave it', () => {
    expect(trendView(trend('up', 0))).toEqual({ direction: 'flat', arrow: '→', change: 0 });
    expect(trendView(trend('up', 0.04))).toEqual({ direction: 'flat', arrow: '→', change: 0 });
    expect(trendView(trend('down', -0.04))).toEqual({ direction: 'flat', arrow: '→', change: 0 });
  });

  it('never shows negative zero', () => {
    expect(trendView(trend('down', -0.04))?.change).toBe(0);
    expect(Object.is(trendView(trend('down', -0.04))?.change, 0)).toBe(true);
    expect(Object.is(trendView(trend('note', -0.04))?.change, 0)).toBe(true);
  });
});
