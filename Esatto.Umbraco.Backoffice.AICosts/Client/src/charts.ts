// Data shaping for the charts: the month's burn-up, cost stacked by model, stacking, reliability figures.

import type { AICostMonth, AICostReport } from './api';
import type { Bucket } from './buckets';

/** One series of a column chart; values are in display units (currency or count). */
export interface ChartSeries {
  id: string;
  label: string;
  color: string;
  values: number[];
  /** Shown instead of a swatch in the legend (status series carry an icon, never colour alone). */
  icon?: string;
}

export interface Point {
  day: number;
  value: number;
}

/**
 * Cumulative cost per day of the month (day = day of month), and the projection from today's total to
 * the projected month-end cost. `max` covers the line, the projection and the budget (all in base).
 */
export function burnUp(month: AICostMonth, daysInMonth: number, budgetInBase?: number): { actual: Point[]; projection: Point[]; max: number } {
  let sum = 0;
  const actual = month.days.map((d, i) => ({ day: i + 1, value: (sum += d.cost) }));
  const today = actual[actual.length - 1] ?? { day: 0, value: 0 };
  const projection = [today, { day: daysInMonth, value: month.projectedCost }];
  return { actual, projection, max: Math.max(month.projectedCost, today.value, budgetInBase ?? 0) };
}

/**
 * Cost per model per bucket, models by total cost (most expensive first). Beyond `limit` series the
 * cheapest fold into "Other", so the palette's fixed slots are never cycled. 7 by default: slot 8 is
 * red, which is reserved for "Failed" on this tab.
 */
export function modelSeries(report: AICostReport, buckets: Bucket[], limit = 7): { ids: string[]; values: number[][] } {
  const perBucket = (costs: number[]) =>
    buckets.map((b) => report.days.reduce((s, d, i) => (d.date >= b.start && d.date <= b.end ? s + costs[i] : s), 0));
  const series = report.modelDays
    .map((m) => ({ id: m.modelId, values: perBucket(m.costs), total: m.costs.reduce((a, c) => a + c, 0) }))
    .sort((a, b) => b.total - a.total);

  if (series.length <= limit) return { ids: series.map((s) => s.id), values: series.map((s) => s.values) };

  const kept = series.slice(0, limit - 1);
  const rest = series.slice(limit - 1);
  const other = buckets.map((_, i) => rest.reduce((s, r) => s + r.values[i], 0));
  return { ids: [...kept.map((s) => s.id), 'Other'], values: [...kept.map((s) => s.values), other] };
}

/** Stacks series bottom → top: result[s][i] is series s's segment in bucket i. */
export function stack(values: number[][]): { y0: number; y1: number }[][] {
  const base = new Array(values[0]?.length ?? 0).fill(0);
  return values.map((series) =>
    series.map((v, i) => {
      const y0 = base[i];
      base[i] += v;
      return { y0, y1: base[i] };
    }),
  );
}

export function failureRate(succeeded: number, failed: number): number {
  const total = succeeded + failed;
  return total === 0 ? 0 : failed / total;
}

export function formatDuration(ms: number): string {
  return ms < 1000 ? `${Math.round(ms)} ms` : `${new Intl.NumberFormat(undefined, { maximumFractionDigits: 1 }).format(ms / 1000)} s`;
}

/** Fixed categorical slot by position; "Other" is neutral, never a hue of its own. */
export function seriesColor(id: string, index: number): string {
  return id === 'Other' ? 'var(--viz-other)' : `var(--viz-cat-${index + 1})`;
}

/** Tokens by type. "Uncached input" is input minus cached (cached is part of input); never negative. */
export function tokenSeries(buckets: Bucket[]): ChartSeries[] {
  return [
    { id: 'uncached', label: 'Uncached input', color: seriesColor('uncached', 0), values: buckets.map((b) => Math.max(0, b.inputTokens - b.cachedInputTokens)) },
    { id: 'cached', label: 'Cached input', color: seriesColor('cached', 1), values: buckets.map((b) => b.cachedInputTokens) },
    { id: 'output', label: 'Output', color: seriesColor('output', 2), values: buckets.map((b) => b.outputTokens) },
  ];
}
