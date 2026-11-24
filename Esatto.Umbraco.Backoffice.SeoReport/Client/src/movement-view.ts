// The pages that gained and lost the most impressions, as tables: columns and rows of already-written cells (table-rows.ts), so what a table says
// is tested without a browser and the data-table element only draws it.
//
// BOTH COMPARISONS, ALWAYS (Movement.cs): every row is set against the previous month AND against the same month a year earlier, because the two
// answer different questions and can point different ways. A comparison month that is not archived is no column, never a column of zeros: the
// other basis must not creep in. The lists are sorted on one of them (`rankedBy`), and the sentence above them says which, or the reader cannot
// tell why the rows are in that order.
//
// The direction is a sign and a figure ("-250 (-25.0%)", "+300 (new)"), never a colour alone. A page that was not shown in the comparison month is
// "new" and has no percentage: there is nothing to divide by.

import { formatFixed, formatKpiValue, formatNumber, formatPercent } from './format';
import { culture, text, type Localizer } from './localize';
import type { TableCell, TableColumn, TableRow } from './table-rows';
import type { Move, MoveDelta, MovementResult } from './types';

export interface MoveList {
  kind: 'losses' | 'gains';
  title: string;
  columns: TableColumn[];
  rows: TableRow[];
}

export interface MovementView {
  /** `none`: no month to compare with; `empty`: compared, and nothing moved; `ready`: at least one list. */
  state: 'none' | 'empty' | 'ready';
  /** What the lists are sorted on, in a sentence. */
  ranked: string;
  /** Losses, then gains: only the lists that have rows. */
  lists: MoveList[];
}

const NONE: MovementView = { state: 'none', ranked: '', lists: [] };

const isNumber = (value: unknown): value is number => typeof value === 'number' && Number.isFinite(value);

/** One comparison month: the column it makes and how a row's figure against it is written. */
interface Comparison {
  period: string;
  of: (move: Move) => MoveDelta | null | undefined;
}

function deltaCell(host: Localizer, move: Move, against: MoveDelta | null | undefined): TableCell {
  if (!against || !isNumber(against.delta)) return { text: text(host, 'uiNoValueText'), missing: true };

  const c = culture(host);
  const change = formatKpiValue(against.delta, 'signed', c);
  const written = isNumber(against.changePercent)
    ? text(host, 'uiMoveDeltaText', { delta: change, percent: formatPercent(against.changePercent, c, { signed: true, fixed: true }) })
    : text(host, 'uiMoveNewText', { delta: change });

  const moved = isNumber(against.positionBefore) && isNumber(move.position);
  return {
    text: written,
    detail: moved ? text(host, 'uiMovePositionText', { before: formatFixed(against.positionBefore, c, 1), now: formatFixed(move.position, c, 1) }) : undefined,
  };
}

function listOf(host: Localizer, kind: MoveList['kind'], moves: readonly Move[], comparisons: readonly Comparison[]): MoveList {
  const c = culture(host);

  return {
    kind,
    title: text(host, kind === 'losses' ? 'uiMovementLossesTitle' : 'uiMovementGainsTitle'),
    columns: [
      { label: text(host, 'uiColumnPageLabel') },
      { label: text(host, 'uiColumnImpressionsLabel'), numeric: true },
      { label: text(host, 'uiColumnPositionLabel'), numeric: true },
      ...comparisons.map((comparison) => ({ label: text(host, 'uiMoveAgainstLabel', { period: comparison.period }) })),
    ],
    rows: moves.map((move) => ({
      cells: [
        { text: move.path || move.url, title: move.url, strong: true },
        isNumber(move.impressions) ? { text: formatNumber(move.impressions, c) } : { text: text(host, 'uiNoValueText'), missing: true },
        isNumber(move.position) ? { text: formatFixed(move.position, c, 1) } : { text: text(host, 'uiNoValueText'), missing: true },
        ...comparisons.map((comparison) => deltaCell(host, move, comparison.of(move))),
      ],
    })),
  };
}

export function moversView(host: Localizer, movement: MovementResult | null | undefined): MovementView {
  if (!movement) return NONE;

  // The previous month first, then the year: the order of the columns, whatever the lists are sorted on.
  const comparisons: Comparison[] = [
    ...(movement.previous ? [{ period: movement.previous.period, of: (move: Move) => move.vsPrevious }] : []),
    ...(movement.yearAgo ? [{ period: movement.yearAgo.period, of: (move: Move) => move.vsYearAgo }] : []),
  ];
  if (comparisons.length === 0) return NONE;

  const byYear = movement.rankedBy === 'yearAgo';
  const ranked = byYear
    ? text(host, 'uiMovementRankedYearAgoText', { period: movement.yearAgo?.period ?? null })
    : text(host, 'uiMovementRankedPreviousText', { period: movement.previous?.period ?? null });

  const lists = [
    ...(movement.losses?.length ? [listOf(host, 'losses', movement.losses, comparisons)] : []),
    ...(movement.gains?.length ? [listOf(host, 'gains', movement.gains, comparisons)] : []),
  ];

  return { state: lists.length === 0 ? 'empty' : 'ready', ranked, lists };
}
