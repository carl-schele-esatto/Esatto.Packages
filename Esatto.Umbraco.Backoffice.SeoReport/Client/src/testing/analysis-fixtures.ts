// Small synthetic analyses for the tests. Invented figures only; nothing here comes from a real site.

import type {
  AnalysisResponse,
  Move,
  MoveDelta,
  MovementResult,
  Opportunity,
  Statement,
  TargetAnalysisResponse,
  TargetGrid,
  TargetPage,
  TargetRow,
} from '../types';

export function statement(key = 'nonBrand', args?: Record<string, string | number | boolean | null>): Statement {
  return { text: args ? { key, args } : { key } };
}

export function opportunity(overrides: Partial<Opportunity> = {}): Opportunity {
  return {
    kind: 'near',
    text: { key: 'near', args: { query: 'blue widgets', position: 12.5, impressions: 480 } },
    effort: 2,
    ...overrides,
  };
}

export function targetPage(overrides: Partial<TargetPage> = {}): TargetPage {
  return { path: '/products/widgets', position: 14.5, ctrPercent: 0.8, impressions: 900, clicks: 7, queries: 2, ...overrides };
}

/** A target query that has a match: ranks at 14.5 with 1,200 impressions and 30 clicks. */
export function targetRow(overrides: Partial<TargetRow> = {}): TargetRow {
  return {
    term: 'blue widgets',
    status: 'near',
    impressions: 1200,
    clicks: 30,
    ctrPercent: 2.5,
    position: 14.5,
    matches: 3,
    example: 'buy blue widgets online',
    advice: { key: 'near' },
    page: null,
    ...overrides,
  };
}

/** A target query nothing matched: the server sends zeros and no position. */
export function missingRow(overrides: Partial<TargetRow> = {}): TargetRow {
  return targetRow({
    term: 'widget insurance',
    status: 'missing',
    impressions: 0,
    clicks: 0,
    ctrPercent: 0,
    position: null,
    matches: 0,
    example: null,
    advice: { key: 'missing', args: { rowsFetched: 250 } },
    ...overrides,
  });
}

export function targets(rows: TargetRow[], rowsFetched = 250): TargetAnalysisResponse {
  return { rowsFetched, rows };
}

export function delta(overrides: Partial<MoveDelta> = {}): MoveDelta {
  return { before: 1000, delta: -250, changePercent: -25, positionBefore: 8.2, ...overrides };
}

export function move(overrides: Partial<Move> = {}): Move {
  return {
    path: '/products/widgets',
    url: 'https://www.example.com/products/widgets',
    impressions: 750,
    position: 9.4,
    vsPrevious: delta(),
    vsYearAgo: delta({ before: 700, delta: 50, changePercent: 7.1, positionBefore: 10.1 }),
    ...overrides,
  };
}

export function movement(overrides: Partial<MovementResult> = {}): MovementResult {
  return {
    period: '2026-10',
    previous: { period: '2026-09' },
    yearAgo: { period: '2025-10' },
    rankedBy: 'previous',
    losses: [move()],
    gains: [move({ path: '/blog/widget-care', url: 'https://www.example.com/blog/widget-care', impressions: 400, vsPrevious: delta({ before: 100, delta: 300, changePercent: 300, positionBefore: 20 }) })],
    ...overrides,
  };
}

export function history(overrides: Partial<TargetGrid> = {}): TargetGrid {
  return {
    // A month without search queries is left out of the grid by the server, so the months are not always consecutive.
    months: [{ period: '2026-07' }, { period: '2026-08' }, { period: '2026-10' }],
    rows: [
      { term: 'blue widgets', positions: [null, 18.25, 14.5] },
      { term: 'widget repair', positions: [4, 3.5, 3] },
    ],
    ...overrides,
  };
}

/** A full analysis of October 2026. */
export function analysis(overrides: Partial<AnalysisResponse> = {}): AnalysisResponse {
  return {
    hasAnalysis: true,
    period: '2026-10',
    summary: [statement('nonBrand', { clicks: 300, total: 900, percent: 33.3 })],
    note: null,
    targetHistory: history(),
    movement: movement(),
    opportunities: [opportunity()],
    targets: targets([targetRow(), missingRow()]),
    ...overrides,
  };
}
