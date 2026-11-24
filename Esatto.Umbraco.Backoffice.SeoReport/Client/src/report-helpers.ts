// Decisions about a report that need no DOM: what is empty, what is shown where. Pure functions, tested without a browser.

import type { ChartBlock, Kpi, Report, SearchBlock, Trend } from './types';

/**
 * Whether a chart has nothing to draw: no chart, no series, or no value that is not null (a series of gaps). A chart with no series is
 * how the contract says "no data" (Report.cs, ChartBlock); the client says so instead of drawing it. Zero is a value: a day without
 * visits is zero visits, which is not the same as having no data.
 */
export function isEmptyChart(block: ChartBlock | null | undefined): boolean {
  return !block?.series?.some((series) => series.values.some((value) => value !== null && value !== undefined));
}

/** Whether the brand and non-brand charts can be drawn: the search block exists and has its segments (an older report lacks both). */
export function brandChartsAvailable(search: SearchBlock | null | undefined): boolean {
  return (search?.segments?.length ?? 0) > 0;
}

/**
 * How many findings the report carries: the warnings among the search block's annotations. A user outside the findings groups gets a
 * report without them (the server strips them), so this is also what decides whether the "To fix" tab exists for that user.
 */
export function warningCount(report: Report | null | undefined): number {
  return report?.traffic?.search?.annotations?.filter((annotation) => annotation.severity === 'warning').length ?? 0;
}

export const hasWarnings = (report: Report | null | undefined): boolean => warningCount(report) > 0;

const MAIN_KPI = 'organic_sessions';
const RING_KPI = 'organic_engagement_rate';

/**
 * The two figures the overview card leads with: the main one (visits from search engines, else the first), and the share for the ring.
 * A ring is a share of a whole, so it is only drawn for a percentage between 0 and 100; anything else (no value, 250) has no ring.
 */
export function heroKpis(kpis: readonly Kpi[]): { main: Kpi | null; ring: Kpi | null } {
  const main = kpis.find((kpi) => kpi.key === MAIN_KPI) ?? kpis[0] ?? null;
  const share = kpis.find((kpi) => kpi.key === RING_KPI && kpi.format === 'percent');
  const ring = share && typeof share.value === 'number' && share.value >= 0 && share.value <= 100 ? share : null;
  return { main, ring };
}

/** The figures that become tiles: all but the main one and the one the ring shows (a figure without a ring stays a tile). */
export function tileKpis(kpis: readonly Kpi[]): Kpi[] {
  const { main, ring } = heroKpis(kpis);
  return kpis.filter((kpi) => kpi !== main && kpi !== ring);
}

export interface TrendPill {
  direction: 'up' | 'down' | 'flat';
  arrow: string;
  /**
   * The size of the change in percent, one decimal. For up and down it has no sign (the arrow carries the direction); for `flat`, which has
   * no direction of its own (a note with a figure, such as a comparison with an anomalous baseline), it keeps its sign, so a fall still reads
   * as a fall.
   */
  change: number;
}

/**
 * The pill of a KPI's trend, or null when it has no figure to show (a note, a missing comparison). An arrow and the size of the change,
 * so that direction is never carried by colour alone; the arrow follows the sign of the figure.
 */
export function trendView(trend: Trend | null | undefined): TrendPill | null {
  const percent = trend?.change_percent;
  if (typeof percent !== 'number' || !Number.isFinite(percent)) return null;

  const rounded = Math.round(percent * 10) / 10;
  // Up or down is read from the SIGN of the figure as it is shown (one decimal): the arrow can never contradict the number beside it, and a
  // change that rounds to nothing is flat. A trend the server calls a note (no direction of its own) is flat whatever its sign.
  const directed = trend!.direction === 'up' || trend!.direction === 'down';
  const direction = directed && rounded !== 0 ? (rounded > 0 ? 'up' : 'down') : 'flat';
  const arrow = direction === 'up' ? '↑' : direction === 'down' ? '↓' : '→';
  // + 0: a rounded -0.04 is -0, which would print as "-0.0".
  return { direction, arrow, change: (direction === 'flat' ? rounded : Math.abs(rounded)) + 0 };
}
