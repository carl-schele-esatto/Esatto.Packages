// The tables of the search tabs as data: columns and rows of already-written cells, so what a table says (which rows, how many, how a figure
// is written, what a missing figure reads) is tested without a browser and the data-table element only draws it.
//
// THE TOP LISTS ARE CUT. The report carries more rows than a top list shows (the archive keeps them for the analysis of target queries), so the
// tables show the first TOP_LIST_ROWS in the order the report has them, which is most clicked first. They are not sortable: they are a ranking,
// shown in the order the report gives.
//
// A figure that is missing reads "No data", never a dash that reads as a value and never a zero that reads as a measurement. The
// click-through rate is already a percentage (2.17 is 2.17 %), kept to two decimals; the average position has one, always, so a column lines up.

import { formatFixed, formatKpiValue, formatNumber, formatPercent } from './format';
import { trendText, type TrendText } from './kpi-view';
import { chartSeriesLabel } from './chart-view';
import { culture, idText, text, type Localizer } from './localize';
import type { Kpi, SearchBlock } from './types';

/** How many rows of a top list a tab shows. */
export const TOP_LIST_ROWS = 25;

export interface TableColumn {
  label: string;
  numeric?: boolean;
  /** A column of running text (advice, a sentence): it gets room and wraps, instead of being squeezed beside the figures. */
  wide?: boolean;
}

export interface TableCell {
  text: string;
  /** Shown on hover: the whole address of a shortened path. */
  title?: string;
  /** The name of the row (the query, the page). */
  strong?: boolean;
  /** The figure is not there: it is written as words and muted. */
  missing?: boolean;
  /** A change year on year, drawn as a pill beside the figure. */
  trend?: TrendText | null;
  /** A glyph before the text (a status): decoration, the text carries the meaning. */
  icon?: string;
  /** A second, muted line under the text (the position a page came from, the largest search a target query matched). */
  detail?: string;
}

export interface TableRow {
  cells: TableCell[];
}

const isNumber = (value: unknown): value is number => typeof value === 'number' && Number.isFinite(value);

function figure(host: Localizer, value: number | null | undefined, write: (value: number) => string): TableCell {
  return isNumber(value) ? { text: write(value) } : { text: text(host, 'uiNoValueText'), missing: true };
}

function label(host: Localizer, value: string | null | undefined): TableCell {
  return value ? { text: value } : { text: text(host, 'uiNoValueText'), missing: true };
}

// The four figures every search row has, in the order of its columns.
function measures(
  host: Localizer,
  row: { clicks: number; impressions: number; ctr_percent: number; position: number },
): TableCell[] {
  const c = culture(host);
  return [
    figure(host, row.clicks, (value) => formatNumber(value, c)),
    figure(host, row.impressions, (value) => formatNumber(value, c)),
    figure(host, row.ctr_percent, (value) => formatPercent(value, c, { digits: 2 })),
    figure(host, row.position, (value) => formatFixed(value, c, 1)),
  ];
}

function measureColumns(host: Localizer): TableColumn[] {
  return [
    { label: text(host, 'uiColumnClicksLabel'), numeric: true },
    { label: text(host, 'uiColumnImpressionsLabel'), numeric: true },
    { label: text(host, 'uiColumnCtrLabel'), numeric: true },
    { label: text(host, 'uiColumnPositionLabel'), numeric: true },
  ];
}

// ---- Search queries and landing pages ----------------------------------------------------------------------------------------

export const queryColumns = (host: Localizer): TableColumn[] => [{ label: text(host, 'uiColumnQueryLabel') }, ...measureColumns(host)];

/** The most clicked search queries of the month, cut at `cap`. A query carries its full text as the title: a long one is cut by the table. */
export function queryRows(host: Localizer, search: SearchBlock | null | undefined, cap = TOP_LIST_ROWS): TableRow[] {
  return (search?.queries ?? []).slice(0, Math.max(0, cap)).map((row) => ({
    cells: [{ ...label(host, row.query), title: row.query || undefined, strong: true }, ...measures(host, row)],
  }));
}

export const pageColumns = (host: Localizer): TableColumn[] => [{ label: text(host, 'uiColumnPageLabel') }, ...measureColumns(host)];

/**
 * The most clicked landing pages of the month, cut at `cap`. A page is its shortened path (the report computes it, so no view needs to know
 * the site's domain), with the whole address as its title; text, not a link: this is a report, not a way out of it.
 */
export function pageRows(host: Localizer, search: SearchBlock | null | undefined, cap = TOP_LIST_ROWS): TableRow[] {
  return (search?.pages ?? []).slice(0, Math.max(0, cap)).map((row) => ({
    cells: [{ ...label(host, row.path || row.url), title: row.url, strong: true }, ...measures(host, row)],
  }));
}

// ---- Brand and non-brand -----------------------------------------------------------------------------------------------------

// The measures the columns are named by: the first segment's, or the total's when there are no segments (no brand terms).
function leading(search: SearchBlock | null | undefined): Kpi[] {
  return search?.segments?.[0]?.metrics ?? search?.total?.metrics ?? [];
}

export function segmentColumns(host: Localizer, search: SearchBlock | null | undefined): TableColumn[] {
  return [
    { label: text(host, 'uiColumnSegmentLabel') },
    ...leading(search).map((metric) => ({ label: idText(host, 'segmentMetric', metric.key, 'Label'), numeric: true })),
  ];
}

// A measure by its format, except that a rate keeps its two decimals (the report's rates have them; the overview's one-decimal rule is for
// the engagement figure).
const measure = (host: Localizer, metric: Kpi): string =>
  metric.format === 'percent' ? formatPercent(metric.value, culture(host), { digits: 2 }) : formatKpiValue(metric.value, metric.format, culture(host));

/** The segments of the search block (brand, non-brand), then the unfiltered total, which they never add up to. */
export function segmentRows(host: Localizer, search: SearchBlock | null | undefined): TableRow[] {
  const width = leading(search).length;
  const segments = [...(search?.segments ?? []), ...(search?.total ? [search.total] : [])];

  return segments.map((segment) => {
    const cells: TableCell[] = Array.from({ length: width }, (_, index) => {
      const metric = segment.metrics?.[index];
      const written = metric ? measure(host, metric) : '';
      return written ? { text: written, trend: trendText(host, metric!.trend) } : { text: text(host, 'uiNoValueText'), missing: true };
    });

    return { cells: [{ text: chartSeriesLabel(host, segment), strong: true }, ...cells] };
  });
}

// ---- Where a day's visits came from ------------------------------------------------------------------------------------------

export const sourceColumns = (host: Localizer): TableColumn[] => [
  { label: text(host, 'uiColumnChannelLabel') },
  { label: text(host, 'uiColumnSourceLabel') },
  { label: text(host, 'uiColumnVisitsLabel'), numeric: true },
];

/** One row per channel and source, as GA4 names them (the names are data from Google, shown as text), most visits first as the server sends them. */
export function sourceRows(host: Localizer, sources: readonly { channel: string; source: string; sessions: number }[]): TableRow[] {
  return sources.map((row) => ({
    cells: [label(host, row.channel), label(host, row.source), figure(host, row.sessions, (value) => formatNumber(value, culture(host)))],
  }));
}
