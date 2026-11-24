import { describe, expect, it, vi } from 'vitest';
import { analysisView, isAnalysis, opportunitiesView, statementsView } from './analysis-view';
import { LIGHT_THEME } from './chart-colors';
import { chartConfig, legendItems } from './chart-config';
import { chartAriaLabel, chartLabels, chartSeriesLabel, chartTableRows } from './chart-view';
import { DailyModel } from './daily-model';
import { dailyMonthOptions, dailyTextArgs, dailyView } from './daily-view';
import { kpiTileView, trendText } from './kpi-view';
import en from './lang/en';
import { errorTexts, resolveId, resolveTextRef, seriesText, text, type IdLocation, type TextLocation } from './localize';
import { moversView } from './movement-view';
import { noteViews } from './notes-view';
import { heroKpis, tileKpis, warningCount } from './report-helpers';
import { formOf, groupOptions, keyModeOf, responseOf, testRows } from './settings-view';
import { pageRows, queryRows, segmentColumns, segmentRows, sourceRows } from './table-rows';
import { historyView, statusLabelKey, targetRows, targetsView } from './target-view';
import { enHost } from './testing/host';
import * as server from './testing/server-responses';
import type { ChartBlock, Report, SearchBlock, TextArgs, TextRef } from './types';
import { lastErrorOf, tabsFor, viewStateOf, visibleTabs } from './view-state';

// The server's REAL answers (testing/server-responses.ts, written by the C# ServerResponsesFileTests from the controller's own output; tsc
// already holds them to types.ts) run through the client's own modules and texts. Nothing here is hand-written data: a key the server starts to
// send that en.ts has no words for, an argument a text cannot write, or a shape a module trips over fails here and not in a browser.

const host = enHost();
const english = en.esattoSeoReport as Record<string, unknown>;

// What a text shows when it printed a missing value instead of leaving it out.
const leaked = /undefined|NaN|Invalid Date|\[object|\bnull\b/;

interface Use {
  where: string;
  key: string;
  args: TextArgs;
}

const refUses = (where: string, location: TextLocation, ref: TextRef | null | undefined): Use[] => {
  if (!ref) return [];
  const { keys, args } = resolveTextRef(location, ref);
  return Object.values(keys).map((key) => ({ where, key: key!, args }));
};

const idUses = (where: string, location: IdLocation, id: string, args: TextArgs = {}): Use[] =>
  Object.values(resolveId(location, id)).map((key) => ({ where, key: key!, args }));

const seriesUse = (where: string, key: string): Use => {
  const resolved = seriesText(key);
  return { where, key: resolved.key, args: resolved.args ?? {} };
};

function chartUses(where: string, chart: ChartBlock, args: TextArgs = {}): Use[] {
  return [
    ...idUses(`${where}.id`, 'chart', chart.id, args),
    ...chart.series.map((series, index) => seriesUse(`${where}.series[${index}]`, series.key)),
    ...chart.annotations.flatMap((annotation, index) => refUses(`${where}.annotations[${index}]`, 'annotation', annotation.text)),
    ...chart.insights.flatMap((insight, index) => refUses(`${where}.insights[${index}]`, 'insight', insight)),
  ];
}

function searchUses(search: SearchBlock): Use[] {
  const segments = [...search.segments, ...(search.total ? [search.total] : [])];
  return [
    ...idUses('search.id', 'searchBlock', search.id),
    ...chartUses('search.clicks', search.clicks),
    ...chartUses('search.impressions', search.impressions),
    ...search.annotations.flatMap((annotation, index) => refUses(`search.annotations[${index}]`, 'annotation', annotation.text)),
    ...search.insights.flatMap((insight, index) => refUses(`search.insights[${index}]`, 'insight', insight)),
    ...segments.flatMap((segment, index) => [
      seriesUse(`search.segments[${index}]`, segment.key),
      ...segment.metrics.flatMap((metric, at) => [
        ...idUses(`search.segments[${index}].metrics[${at}]`, 'segmentMetric', metric.key),
        ...refUses(`search.segments[${index}].metrics[${at}].trend`, 'trend', metric.trend?.text),
      ]),
    ]),
  ];
}

function reportUses(report: Report): Use[] {
  const traffic = report.traffic;
  return [
    ...refUses('data_status_note', 'dataStatusNote', report.data_status_note),
    ...report.sources.flatMap((source, index) => [
      ...idUses(`sources[${index}].id`, 'source', source.id),
      ...refUses(`sources[${index}].note`, 'sourceNote', source.note),
    ]),
    ...traffic.kpis.flatMap((kpi, index) => [...idUses(`kpis[${index}]`, 'kpi', kpi.key), ...refUses(`kpis[${index}].trend`, 'trend', kpi.trend?.text)]),
    ...chartUses('organic', traffic.organic),
    ...chartUses('total', traffic.total),
    ...chartUses('search_comparison', traffic.search_comparison),
    ...(traffic.search ? searchUses(traffic.search) : []),
  ];
}

function analysisUses(): Use[] {
  const analysis = server.analysis;
  return [
    ...analysis.summary.flatMap((statement, index) => refUses(`summary[${index}]`, 'narrative', statement.text)),
    ...(analysis.opportunities ?? []).flatMap((opportunity, index) => refUses(`opportunities[${index}]`, 'opportunity', opportunity.text)),
    ...(analysis.targets?.rows ?? []).flatMap((row, index) => [
      ...refUses(`targets.rows[${index}].advice`, 'targetAdvice', row.advice),
      { where: `targets.rows[${index}].status`, key: statusLabelKey(row.status) ?? `<no label for ${row.status}>`, args: {} },
    ]),
  ];
}

function expectEnglish(uses: Use[]) {
  expect(uses.length).toBeGreaterThan(0);
  for (const use of uses) {
    expect(english, `${use.where}: ${use.key} is not in en.ts`).toHaveProperty(use.key);

    const written = text(host, use.key, use.args);
    expect(written.trim(), `${use.where}: ${use.key} is empty`).not.toBe('');
    expect(written, `${use.where}: ${use.key} with ${JSON.stringify(use.args)}`).not.toMatch(leaked);
    expect(written, `${use.where}: ${use.key} printed its own key`).not.toContain(use.key);
  }
}

describe('the real answers name only texts the client has, and every one is written with its real arguments', () => {
  const report = server.report.report!;

  it('the report: a brand split and warnings, as the golden scenario has them', () => {
    expect(report.traffic.search?.segments.map((segment) => segment.key)).toEqual(['brand', 'non_brand']);
    expect(warningCount(report)).toBeGreaterThan(0);

    expectEnglish(reportUses(report));
  });

  it('the analysis: the summary, the quick wins and the target queries', () => {
    expect(server.analysis.summary.length).toBeGreaterThan(0);
    expect(server.analysis.opportunities?.length).toBeGreaterThan(0);
    expect(server.analysis.targets?.rows.length).toBeGreaterThan(0);

    expectEnglish(analysisUses());
  });

  it('the daily card: its chart with the arguments the card writes it with', () => {
    expectEnglish(chartUses('daily.chart', server.daily.chart, dailyTextArgs(server.daily)));
  });

  it('the problems: the last failure on the status, and the code Test connection gave a source', () => {
    const codes = [lastErrorOf(server.status.fetch), server.connectionTest.searchConsole.errorCode];
    expect(codes).toEqual(['noAccess', 'apiDisabled']);

    for (const code of codes) {
      const { title, text: body } = errorTexts(host, code, server.connectionTest.searchConsole.args ?? null);
      expect(title).not.toMatch(leaked);
      expect(body).not.toMatch(leaked);
      expect(body).not.toContain('esattoSeoReport_');
    }
  });
});

describe('the real answers go through every module the dashboard draws them with', () => {
  const report = server.report.report!;
  const search = report.traffic.search!;
  const charts = [report.traffic.organic, report.traffic.total, report.traffic.search_comparison, search.clicks, search.impressions, server.daily.chart];

  it('the shell: the status and the report make a view state, a strip of tabs and a last failure', () => {
    expect(viewStateOf(server.status, report)).toBe('ready');
    expect(visibleTabs(report, true)).toContain('toFix');
    expect(tabsFor(report, false)).not.toContain('settings');
    expect(lastErrorOf(server.status.fetch)).toBe('noAccess');
  });

  it('the overview and the tables: tiles, trends, rows', () => {
    const { main, ring } = heroKpis(report.traffic.kpis);
    expect(main).not.toBeNull();
    expect(ring).not.toBeNull();

    for (const kpi of [...tileKpis(report.traffic.kpis), ...report.traffic.kpis]) {
      const tile = kpiTileView(host, kpi);
      expect(`${tile.label} ${tile.value} ${tile.description}`).not.toMatch(leaked);
      expect(JSON.stringify(trendText(host, kpi.trend))).not.toMatch(/undefined|NaN/);
    }

    for (const rows of [queryRows(host, search), pageRows(host, search), segmentRows(host, search)]) {
      expect(rows.length).toBeGreaterThan(0);
      for (const row of rows) for (const cell of row.cells) expect(cell.text).not.toMatch(leaked);
    }

    expect(segmentColumns(host, search).length).toBeGreaterThan(1);
    expect(sourceRows(host, server.dailySources.sources).length).toBe(server.dailySources.sources.length);
  });

  it('the charts: a configuration, a legend, labels, a table and a description for each', () => {
    for (const chart of charts) {
      const config = chartConfig(chart, { host, theme: LIGHT_THEME, reducedMotion: true });
      expect(config.data.datasets).toHaveLength(chart.series.length);
      expect(legendItems(chart, { host, theme: LIGHT_THEME }).map((item) => item.label).join(' ')).not.toMatch(leaked);
      expect(chartLabels(chart, 'en-gb', 'axis')).toHaveLength(chart.labels.length);
      expect(chartTableRows(host, chart).rows.length).toBe(chart.labels.length);
      expect(chartAriaLabel(host, chart)).not.toMatch(leaked);
      for (const series of chart.series) expect(chartSeriesLabel(host, series)).not.toMatch(leaked);
    }
  });

  it('the notes of the charts and of the search block', () => {
    for (const block of [report.traffic.organic, report.traffic.total, search]) {
      for (const note of noteViews(host, block.annotations, block.insights)) expect(`${note.title ?? ''} ${note.text}`).not.toMatch(leaked);
    }
  });

  it('the analysis: statements, quick wins, page movement, target queries and their history', () => {
    expect(isAnalysis(server.analysis)).toBe(true);

    const view = analysisView(host, server.analysis);
    expect(view.statements).toHaveLength(server.analysis.summary.length);
    expect(statementsView(host, server.analysis.summary).every((statement) => statement.text && !leaked.test(statement.text))).toBe(true);
    expect(opportunitiesView(host, server.analysis.opportunities).length).toBe(server.analysis.opportunities!.length);
    expect(moversView(host, server.analysis.movement)).toBeDefined();
    expect(targetsView(host, server.analysis.targets)).toBeDefined();
    expect(targetRows(host, server.analysis.targets!.rows)).toHaveLength(server.analysis.targets!.rows.length);
    expect(historyView(host, server.analysis.targetHistory)).not.toBeNull();
  });

  it('the daily card: its view, the month picker, and the model reading the curve and a day', async () => {
    const view = dailyView(host, server.daily);
    expect(JSON.stringify(view)).not.toMatch(/undefined|NaN/);
    const options = dailyMonthOptions(host, server.daily.months, server.daily.windowDays);
    expect(options.map((option) => option.value)).toEqual(server.daily.months);
    expect(options.map((option) => option.label).join(' ')).not.toMatch(leaked);

    const model = new DailyModel(
      { getDaily: vi.fn(async () => server.daily), getDailySources: vi.fn(async () => server.dailySources) },
      () => {},
    );
    await model.connect();
    await model.selectDay(server.dailySources.date);
    expect(model.state.daily).toBe(server.daily);
    expect(model.state.sources).toBe(server.dailySources);
    expect(model.state.problem).toBeNull();
    model.disconnect();
  });

  it('the Settings tab: the form, the key, the user groups and the connection test', () => {
    const settings = responseOf(server.settings);
    expect(formOf(settings.settings).displayName).toBe('Example site');
    expect(keyModeOf(settings.key, false)).toBe('saved');
    expect(groupOptions([], settings.settings.findingsUserGroups)).toEqual([]);

    const rows = testRows(host, server.connectionTest, settings.key.serviceAccountEmail);
    expect(rows.map((row) => row.kind)).toEqual(['passed', 'passed', 'failed']);
    for (const row of rows) expect(JSON.stringify(row)).not.toMatch(/undefined|NaN/);
  });
});
