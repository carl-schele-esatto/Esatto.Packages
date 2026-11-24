import { describe, expect, it } from 'vitest';
import { comparisonView, queriesView, toFixItems, topListsView, trafficView } from './tab-views';
import { hasWarnings } from '../report-helpers';
import { chart, landingPage, query, report, searchBlock, segment, warning, info } from '../testing/fixtures';
import { enHost } from '../testing/host';
import type { Annotation, ChartBlock } from '../types';

const host = enHost();

const filled = (id: string): ChartBlock => chart({ id });
const empty = (id: string): ChartBlock => chart({ id, labels: [], series: [] });

const withBrand = (overrides = {}) =>
  searchBlock({
    segments: [segment('brand'), segment('non_brand')],
    total: segment('total'),
    clicks: filled('traffic_search_clicks'),
    impressions: filled('traffic_search_impressions'),
    ...overrides,
  });

const noBrand = (overrides = {}) =>
  searchBlock({
    segments: [],
    total: segment('total'),
    clicks: empty('traffic_search_clicks'),
    impressions: empty('traffic_search_impressions'),
    ...overrides,
  });

const anonymized: Annotation = { period: '2026-10', severity: 'info', text: { key: 'anonymizedQueries', args: { percent: 91.5, period: '2026-10' } } };

describe('trafficView', () => {
  it('is the two monthly charts, by year: visits from search engines, and all visits', () => {
    const view = trafficView(report());

    expect(view.blocks.map((block) => block.id)).toEqual(['traffic_organic', 'traffic_total']);
  });

  it('keeps a chart with nothing in it: its card says "No data yet." where the chart would be', () => {
    const view = trafficView(report({ traffic: { ...report().traffic, total: empty('traffic_total') } }));

    expect(view.blocks).toHaveLength(2);
  });

  it('has no charts without a report, and skips one an older report lacks', () => {
    expect(trafficView(null).blocks).toEqual([]);
    expect(trafficView(report({ traffic: { ...report().traffic, total: undefined as never } })).blocks.map((block) => block.id)).toEqual(['traffic_organic']);
  });
});

describe('comparisonView', () => {
  it('is the chart that sets GA4 visits against Search Console clicks', () => {
    expect(comparisonView(report()).block?.id).toBe('traffic_search_comparison');
  });

  it('has no chart without a report', () => {
    expect(comparisonView(null).block).toBeNull();
  });
});

describe('queriesView', () => {
  it('with brand terms is the table of brand, non-brand and total, the two charts, and the notes about the data', () => {
    const view = queriesView(host, report({}, withBrand({ annotations: [anonymized] })));

    expect(view.state).toBe('ready');
    expect(view.brand).toBe(true);
    expect(view.table?.title).toBe('Search queries: brand vs non-brand (Search Console)');
    expect(view.table?.subtitle).toBe('Clicks, impressions, CTR and average position');
    expect(view.table?.rows.map((row) => row.cells[0].text)).toEqual(['Brand', 'Non-brand', 'Total (unfiltered)']);
    expect(view.charts.map((block) => block.id)).toEqual(['traffic_search_clicks', 'traffic_search_impressions']);
    expect(view.notes.map((note) => note.severity)).toEqual(['info']);
    expect(view.hint).toBe('');
  });

  it('leaves the warnings to the To fix tab: a finding that points at a page belongs where the page is', () => {
    const view = queriesView(host, report({}, withBrand({ annotations: [warning(), anonymized, warning('pagesRankDeep')] })));

    expect(view.notes.map((note) => note.severity)).toEqual(['info']);
  });

  it('without brand terms hides the brand charts and the brand heading: they would be an empty plot under a title about a split that is not made', () => {
    const view = queriesView(host, report({}, noBrand()));

    expect(view.brand).toBe(false);
    expect(view.charts).toEqual([]);
    expect(view.table?.title).toBe('Search Console in total');
    expect(JSON.stringify(view)).not.toContain('brand vs non-brand');
  });

  it('without brand terms still shows the unfiltered total, and says why there is no split and where to set it up', () => {
    const view = queriesView(host, report({}, noBrand()));

    expect(view.state).toBe('ready');
    expect(view.table?.rows.map((row) => row.cells[0].text)).toEqual(['Total (unfiltered)']);
    expect(view.hint).toContain('no brand terms are set');
    expect(view.hint).toContain('Settings tab');
  });

  it('without brand terms shows no notes: they are about the curve of the split, which is not drawn', () => {
    const view = queriesView(host, report({}, noBrand({ annotations: [anonymized, info('outsideGscWindow')] })));

    expect(view.notes).toEqual([]);
  });

  it('is empty, with nothing to show, when there is no search block (Search Console is not part of the report)', () => {
    expect(queriesView(host, report({}, null)).state).toBe('empty');
    expect(queriesView(host, null).state).toBe('empty');
  });

  it('is empty without brand terms and without a total', () => {
    const view = queriesView(host, report({}, noBrand({ total: null })));

    expect(view.state).toBe('empty');
    expect(view.table).toBeNull();
    expect(view.hint).toBe('');
  });

  it('keeps the brand charts when they have no data: they are a chart of the split that is made, and their cards say so', () => {
    const view = queriesView(host, report({}, withBrand({ clicks: empty('traffic_search_clicks') })));

    expect(view.charts).toHaveLength(2);
  });

  it('writes the table for the language of the page', () => {
    const view = queriesView(enHost('sv'), report({}, withBrand({ segments: [segment('brand', { clicks: 12345 })] })));

    expect(view.table?.rows[0].cells[1].text).toBe('12 345');
  });
});

describe('topListsView', () => {
  const lists = (queries: number, pages: number) =>
    report({}, searchBlock({
      queries: Array.from({ length: queries }, (_, index) => query({ query: `query ${index}` })),
      pages: Array.from({ length: pages }, (_, index) => landingPage({ path: `/page-${index}` })),
    }));

  it('is the most clicked queries and the most clicked pages, cut at 25 rows each', () => {
    const view = topListsView(host, lists(40, 60));

    expect(view.state).toBe('ready');
    expect(view.queries?.rows).toHaveLength(25);
    expect(view.pages?.rows).toHaveLength(25);
    expect(view.queries?.title).toBe('Most clicked search queries');
    expect(view.pages?.title).toBe('Most clicked landing pages');
    expect(view.queries?.rows[0].cells[0].text).toBe('query 0');
    expect(view.pages?.rows[0].cells[0].text).toBe('/page-0');
  });

  it('says that the lists do not add up to the total, as a note with its icon and label', () => {
    const view = topListsView(host, lists(3, 3));

    expect(view.note).toMatchObject({ kind: 'annotation', severity: 'info', icon: 'icon-info', label: 'Note' });
    expect(view.note?.text).toContain('do not add up to the total');
  });

  it('shows only the list that is there', () => {
    const view = topListsView(host, report({}, searchBlock({ queries: [query()], pages: null })));

    expect(view.state).toBe('ready');
    expect(view.queries?.rows).toHaveLength(1);
    expect(view.pages).toBeNull();
  });

  it('is empty when neither list is there: an older report that has none, or no search block', () => {
    expect(topListsView(host, report({}, searchBlock())).state).toBe('empty');
    expect(topListsView(host, report({}, searchBlock({ queries: [], pages: [] }))).state).toBe('empty');
    expect(topListsView(host, report({}, null)).state).toBe('empty');
    expect(topListsView(host, null).state).toBe('empty');
    expect(topListsView(host, null).note).toBeNull();
  });
});

describe('toFixItems', () => {
  const pagesNotClicked: Annotation = { period: '2026-10', severity: 'warning', text: { key: 'pagesNotClicked', args: { count: 2, pages: '/a, /b', position: 5, impressions: 300, ctrPercent: 1 } } };
  const rankDeep: Annotation = { period: '2026-10', severity: 'warning', text: { key: 'pagesRankDeep', args: { count: 1, pages: '/c', impressions: 300, position: 20 } } };
  const noKeyEvents: Annotation = { period: null, severity: 'warning', text: { key: 'noKeyEvents' } };

  it('is the warnings of the search block, each with its title and its text, and a severity icon AND word', () => {
    const items = toFixItems(host, report({}, withBrand({ annotations: [pagesNotClicked] })));

    expect(items).toHaveLength(1);
    expect(items[0]).toMatchObject({ kind: 'annotation', severity: 'warning', icon: 'icon-alert', label: 'Warning', title: '2 pages seen but not clicked' });
    expect(items[0].text).toContain('/a, /b rank at position 5.0 or better');
  });

  it('keeps the order the report has: the list is not sorted (the findings come in the order the report writes them)', () => {
    const items = toFixItems(host, report({}, withBrand({ annotations: [rankDeep, noKeyEvents, pagesNotClicked] })));

    expect(items.map((item) => item.title)).toEqual(['1 page shown but ranking deep', 'No key events in GA4', '2 pages seen but not clicked']);
  });

  it('leaves the notes about the data to the other tabs: only a warning is a finding', () => {
    const items = toFixItems(host, report({}, withBrand({ annotations: [anonymized, pagesNotClicked, info('outsideGscWindow')] })));

    expect(items.map((item) => item.severity)).toEqual(['warning']);
  });

  it('has no insights: they are about a curve, not a finding', () => {
    const items = toFixItems(host, report({}, withBrand({ annotations: [pagesNotClicked], insights: [{ key: 'sessions' }] })));

    expect(items.every((item) => item.kind === 'annotation')).toBe(true);
  });

  it('is empty without findings, without a search block and without a report', () => {
    expect(toFixItems(host, report({}, withBrand({ annotations: [anonymized] })))).toEqual([]);
    expect(toFixItems(host, report({}, null))).toEqual([]);
    expect(toFixItems(host, null)).toEqual([]);
  });

  it('agrees with the tab strip: the tab exists exactly when this list has something', () => {
    for (const annotations of [[], [anonymized], [pagesNotClicked], [anonymized, rankDeep]]) {
      const given = report({}, withBrand({ annotations }));

      expect(hasWarnings(given)).toBe(toFixItems(host, given).length > 0);
    }
  });
});
