// The target queries as tables: the editor's terms held up against the month (status, figures, advice) and their positions month by month.
// Columns and rows of already-written cells (table-rows.ts), so what a table says is tested without a browser and the data-table element only
// draws it.
//
// THE STATUS IS A CODE, AND ITS WORDS ARE A KEY. TargetAnalysis sends missing | deep | near | title | performing; the label is area `target`, part
// Label (targetMissingLabel, ...), looked up with text(), and the advice is its own text reference (area `target`, part Text). Shown as an icon AND
// the label: the glyph repeats the word for the eye and never replaces it.
//
// A TERM NOTHING MATCHED has no figures: the server sends zeros and no position, and a row of zeros would read as "measured, and nothing came".
// Those cells say "No data" instead. The count of matching searches stays a count (0), which is a fact.

import { formatFixed, formatNumber, formatPercent, formatPeriodShort } from './format';
import { culture, refText, text, type Localizer } from './localize';
import type { TableCell, TableColumn, TableRow } from './table-rows';
import type { TargetAnalysisResponse, TargetGrid, TargetRow } from './types';

interface StatusInfo {
  labelKey: string;
  icon: string;
}

// In the order the server lists them (TargetAnalysis.Statuses), most serious first.
const STATUS = new Map<string, StatusInfo>([
  ['missing', { labelKey: 'targetMissingLabel', icon: 'icon-block' }],
  ['deep', { labelKey: 'targetDeepLabel', icon: 'icon-arrow-down' }],
  ['near', { labelKey: 'targetNearLabel', icon: 'icon-arrow-up' }],
  ['title', { labelKey: 'targetTitleLabel', icon: 'icon-mouse-cursor' }],
  ['performing', { labelKey: 'targetPerformingLabel', icon: 'icon-check' }],
]);

/** The status codes the server sends. */
export const TARGET_STATUSES: readonly string[] = [...STATUS.keys()];

/** The translation key of a status code's label, or null for a code this client has no words for. */
export function statusLabelKey(code: string): string | null {
  return STATUS.get(code)?.labelKey ?? null;
}

const isNumber = (value: unknown): value is number => typeof value === 'number' && Number.isFinite(value);

const missingCell = (host: Localizer): TableCell => ({ text: text(host, 'uiNoValueText'), missing: true });

function figure(host: Localizer, value: number | null | undefined, write: (value: number) => string): TableCell {
  return isNumber(value) ? { text: write(value) } : missingCell(host);
}

function statusCell(host: Localizer, code: string): TableCell {
  const info = STATUS.get(code);
  return info ? { text: text(host, info.labelKey), icon: info.icon } : { text: text(host, 'uiTargetUnknownLabel'), icon: 'icon-info' };
}

// ---- The terms against the month ----------------------------------------------------------------------------------------------

export function targetColumns(host: Localizer): TableColumn[] {
  return [
    { label: text(host, 'uiColumnQueryLabel') },
    { label: text(host, 'uiColumnStatusLabel') },
    { label: text(host, 'uiColumnImpressionsLabel'), numeric: true },
    { label: text(host, 'uiColumnClicksLabel'), numeric: true },
    { label: text(host, 'uiColumnCtrLabel'), numeric: true },
    { label: text(host, 'uiColumnPositionLabel'), numeric: true },
    { label: text(host, 'uiColumnMatchesLabel'), numeric: true },
    { label: text(host, 'uiColumnAdviceLabel'), wide: true },
  ];
}

/** One row per target query, in the editor's order (the server's: the editor puts what matters first). */
export function targetRows(host: Localizer, rows: readonly TargetRow[]): TableRow[] {
  const c = culture(host);

  return rows.map((row) => {
    const matched = row.matches > 0;
    // Nothing matched: nothing was measured. The counts below would be zeros that read as a measurement.
    const measured = (value: number | null | undefined, write: (value: number) => string): TableCell => (matched ? figure(host, value, write) : missingCell(host));

    return {
      cells: [
        {
          text: row.term,
          title: row.term,
          strong: true,
          detail: matched && row.example ? text(host, 'uiTargetExampleText', { example: row.example }) : undefined,
        },
        statusCell(host, row.status),
        measured(row.impressions, (value) => formatNumber(value, c)),
        measured(row.clicks, (value) => formatNumber(value, c)),
        measured(row.ctrPercent, (value) => formatPercent(value, c, { digits: 2 })),
        measured(row.position, (value) => formatFixed(value, c, 1)),
        figure(host, row.matches, (value) => formatNumber(value, c)),
        { text: refText(host, 'targetAdvice', row.advice) },
      ],
    };
  });
}

export interface TargetsView {
  /** `noTerms`: the list is empty, so there is no table, and the card says how to start. */
  state: 'noTerms' | 'ready';
  /** The terms, in the editor's order. */
  terms: string[];
  /** How many search queries the month holds: "not visible" means "not among these". */
  rowsFetched: number;
  columns: TableColumn[];
  rows: TableRow[];
}

/** The terms of an analysis: what the editor starts from. */
export const termsOf = (targets: TargetAnalysisResponse | null | undefined): string[] => (targets?.rows ?? []).map((row) => row.term);

export function targetsView(host: Localizer, targets: TargetAnalysisResponse | null | undefined): TargetsView {
  const rows = targets?.rows ?? [];
  if (rows.length === 0) return { state: 'noTerms', terms: [], rowsFetched: targets?.rowsFetched ?? 0, columns: [], rows: [] };

  return { state: 'ready', terms: termsOf(targets), rowsFetched: targets?.rowsFetched ?? 0, columns: targetColumns(host), rows: targetRows(host, rows) };
}

// ---- The positions month by month ---------------------------------------------------------------------------------------------

export interface HistoryView {
  columns: TableColumn[];
  rows: TableRow[];
}

/**
 * The grid of the terms' positions across the archive: one column per month (oldest first, as the server has them), one row per term. Null
 * without terms or without months. A month without a match is "No data", muted: an empty cell would read as a position.
 */
export function historyView(host: Localizer, grid: TargetGrid | null | undefined): HistoryView | null {
  const months = grid?.months ?? [];
  const terms = grid?.rows ?? [];
  if (months.length === 0 || terms.length === 0) return null;

  const c = culture(host);

  return {
    columns: [
      { label: text(host, 'uiColumnQueryLabel') },
      ...months.map((month) => ({ label: formatPeriodShort(month.period, c), numeric: true })),
    ],
    rows: terms.map((row) => ({
      cells: [
        { text: row.term, title: row.term, strong: true },
        ...months.map((_, index) => figure(host, row.positions?.[index], (value) => formatFixed(value, c, 1))),
      ],
    })),
  };
}
