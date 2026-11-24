// What each report tab shows, decided without a DOM: which charts and tables, in which cases, with which notes. The tab elements only draw this, so
// every "is this shown?" rule is tested without a browser.
//
// THE REPORT TABS
//   Traffic                 the daily card (its own, daily-card.element.ts), then the two monthly charts by year
//   Queries                 brand and non-brand: a table of the segments and the unfiltered total, two charts, and the notes about the data
//   Most clicked            the top search queries and the top landing pages, and the note that they do not add up to the total
//   To fix                  the findings: the warnings of the search block, in the order the report lists them
//   Analytics vs Search Console   the one chart that sets GA4 visits against Search Console clicks, by year
// (The Analysis tab reads its own endpoint and is decided in analysis-view.ts, not here.)
//
// A chart with nothing to draw stays in its tab and says "No data yet." (its card does). The BRAND charts are different: without brand terms there is
// no split, so there are no charts of it and no heading about it; they are left out instead of being drawn empty.

import { idText, text, type Localizer } from '../localize';
import { noteViews, type NoteView } from '../notes-view';
import { brandChartsAvailable } from '../report-helpers';
import { pageColumns, pageRows, queryColumns, queryRows, segmentColumns, segmentRows, type TableColumn, type TableRow } from '../table-rows';
import type { ChartBlock, Report } from '../types';

export interface TableSection {
  icon: string;
  title: string;
  subtitle: string;
  /** What the columns mean. */
  help: string;
  columns: TableColumn[];
  rows: TableRow[];
}

// ---- Traffic and the comparison -----------------------------------------------------------------------------------------------

/** The monthly charts of the Traffic tab, after the daily card: visits from search engines by year, then all visits by year. */
export function trafficView(report: Report | null): { blocks: ChartBlock[] } {
  const traffic = report?.traffic;
  // An older report may lack a chart; one that is not there is not an empty card either.
  return { blocks: traffic ? [traffic.organic, traffic.total].filter((block): block is ChartBlock => Boolean(block)) : [] };
}

/** The chart of the comparison tab: GA4 visits against Search Console clicks, by year. */
export function comparisonView(report: Report | null): { block: ChartBlock | null } {
  return { block: report?.traffic?.search_comparison ?? null };
}

// ---- Queries ------------------------------------------------------------------------------------------------------------------

export interface QueriesView {
  /** `empty`: nothing to show at all (no Search Console block, or neither a split nor a total). */
  state: 'ready' | 'empty';
  /** The brand terms are set and the split is made. */
  brand: boolean;
  table: TableSection | null;
  /** Why there is no split, when there is a table without one. */
  hint: string;
  /** The brand charts: clicks, then impressions. None without a split. */
  charts: ChartBlock[];
  notes: NoteView[];
}

const NOTHING: QueriesView = { state: 'empty', brand: false, table: null, hint: '', charts: [], notes: [] };

export function queriesView(host: Localizer, report: Report | null): QueriesView {
  const search = report?.traffic?.search ?? null;
  if (!search) return NOTHING;

  const brand = brandChartsAvailable(search);
  const rows = segmentRows(host, search);

  const table: TableSection | null =
    rows.length === 0
      ? null
      : {
          icon: 'icon-search',
          // The block's own heading says "brand vs non-brand": without the split it would be wrong, so the table is a table of the total.
          title: brand ? idText(host, 'searchBlock', search.id, 'Title') : text(host, 'uiSearchTotalTitle'),
          subtitle: brand ? idText(host, 'searchBlock', search.id, 'Subtitle') : '',
          help: text(host, 'uiHelpSegmentsText'),
          columns: segmentColumns(host, search),
          rows,
        };

  if (!brand && !table) return NOTHING;

  return {
    state: 'ready',
    brand,
    table,
    hint: !brand && table ? text(host, 'uiBrandSplitOffText') : '',
    charts: brand ? [search.clicks, search.impressions] : [],
    // The notes are about the split and its curve; the warnings are the findings of the To fix tab.
    notes: brand ? noteViews(host, search.annotations, search.insights, 'exclude') : [],
  };
}

// ---- To fix -------------------------------------------------------------------------------------------------------------------

/**
 * The findings: the warnings among the search block's annotations, each with its title, its text and a severity icon and word. THEY KEEP THE ORDER
 * THE REPORT HAS: the list is not sorted, only filtered to the warnings, so the order is the one the
 * findings are written in. The notes about the data (the info annotations) belong to the other tabs, and an insight is about a curve, so
 * neither is a finding. The tab exists exactly when this list is not empty (visibleTabs, which asks hasWarnings), and the server withholds the
 * warnings from a user outside the findings groups, so for that user the list is empty and so is the tab.
 */
export function toFixItems(host: Localizer, report: Report | null): NoteView[] {
  return noteViews(host, report?.traffic?.search?.annotations, null, 'only');
}

// ---- Most clicked -------------------------------------------------------------------------------------------------------------

export interface TopListsView {
  state: 'ready' | 'empty';
  queries: TableSection | null;
  pages: TableSection | null;
  /** The lists do not add up to the total. */
  note: NoteView | null;
}

export function topListsView(host: Localizer, report: Report | null): TopListsView {
  const search = report?.traffic?.search ?? null;
  const queries = queryRows(host, search);
  const pages = pageRows(host, search);

  if (queries.length === 0 && pages.length === 0) return { state: 'empty', queries: null, pages: null, note: null };

  return {
    state: 'ready',
    queries: queries.length === 0 ? null : { icon: 'icon-search', title: text(host, 'uiTopQueriesTitle'), subtitle: '', help: text(host, 'uiHelpQueriesText'), columns: queryColumns(host), rows: queries },
    pages: pages.length === 0 ? null : { icon: 'icon-document', title: text(host, 'uiTopPagesTitle'), subtitle: '', help: text(host, 'uiHelpPagesText'), columns: pageColumns(host), rows: pages },
    note: {
      kind: 'annotation',
      severity: 'info',
      icon: 'icon-info',
      label: text(host, 'uiNoteInfoLabel'),
      title: '',
      text: text(host, 'uiTopListsNoteText'),
    },
  };
}
