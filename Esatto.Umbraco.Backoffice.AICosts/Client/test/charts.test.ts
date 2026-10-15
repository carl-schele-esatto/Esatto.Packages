import { describe, it, expect } from 'vitest';
import { burnUp, modelSeries, stack, failureRate, formatDuration } from '../src/charts';
import { toDays, toWeeks } from '../src/buckets';
import type { AICostDay, AICostMonth, AICostReport } from '../src/api';

const month = (costs: number[], projectedCost: number): AICostMonth => ({
  from: '2026-10-01', to: `2026-10-${String(costs.length).padStart(2, '0')}`, cost: costs.reduce((a, b) => a + b, 0),
  projectedCost, unpricedTokens: 0, budget: null,
  days: costs.map((cost, i) => ({ date: `2026-10-${String(i + 1).padStart(2, '0')}`, cost })),
});

describe('burnUp', () => {
  it('accumulates the days and projects from today to month end', () => {
    const b = burnUp(month([1, 2, 3], 31), 31);
    expect(b.actual).toEqual([{ day: 1, value: 1 }, { day: 2, value: 3 }, { day: 3, value: 6 }]);
    expect(b.projection).toEqual([{ day: 3, value: 6 }, { day: 31, value: 31 }]);
    expect(b.max).toBe(31);
  });
  it('a budget above the projection raises the axis', () => expect(burnUp(month([1], 31), 31, 50).max).toBe(50));
});

const day = (date: string): AICostDay => ({
  date, requests: 1, inputTokens: 1, outputTokens: 1, cachedInputTokens: 0, totalTokens: 2, cost: 0, unpricedTokens: 0,
  succeededRequests: 1, failedRequests: 0,
});

describe('modelSeries', () => {
  const days = ['2026-09-27', '2026-09-28', '2026-09-29'].map(day);

  it('orders models by total cost and folds the rest into Other beyond the limit', () => {
    const modelDays = Array.from({ length: 9 }, (_, m) => ({ modelId: `m${m}`, costs: [m, 0, 0] }));
    const s = modelSeries({ days, modelDays } as unknown as AICostReport, toDays(days), 8);
    expect(s.ids).toEqual(['m8', 'm7', 'm6', 'm5', 'm4', 'm3', 'm2', 'Other']);
    expect(s.values[7][0]).toBe(1); // m1 + m0
  });

  it('sums a model across the days of each week', () => {
    // 2026-09-27 is a Sunday: week 1 = [27], week 2 = [28, 29].
    const s = modelSeries({ days, modelDays: [{ modelId: 'a', costs: [1, 2, 3] }] } as unknown as AICostReport, toWeeks(days));
    expect(s.values).toEqual([[1, 5]]);
  });
});

describe('stack', () => {
  it('stacks series bottom to top per bucket', () =>
    expect(stack([[1, 2], [3, 0]])).toEqual([
      [{ y0: 0, y1: 1 }, { y0: 0, y1: 2 }],
      [{ y0: 1, y1: 4 }, { y0: 2, y1: 2 }],
    ]));
});

describe('reliability', () => {
  it('failure rate', () => {
    expect(failureRate(99, 1)).toBeCloseTo(0.01);
    expect(failureRate(0, 0)).toBe(0);
  });
  it('durations', () => {
    expect(formatDuration(850)).toBe('850 ms');
    expect(formatDuration(1234)).toMatch(/^1[.,]2 s$/);
  });
});

// Review 2 (re-graded): red is reserved for "Failed"; "Other" is neutral and slot 8 (red) is never used.
describe('seriesColor', () => {
  it('categorical slots in order, Other neutral', async () => {
    const { seriesColor } = await import('../src/charts');
    expect(seriesColor('a', 0)).toBe('var(--viz-cat-1)');
    expect(seriesColor('Other', 6)).toBe('var(--viz-other)');
  });
  it('at most 7 named models, so the red slot 8 never appears', () => {
    const many = ['2026-09-28'].map(day);
    const modelDays = Array.from({ length: 8 }, (_, m) => ({ modelId: `m${m}`, costs: [m + 1] }));
    const s = modelSeries({ days: many, modelDays } as unknown as AICostReport, toDays(many));
    expect(s.ids).toHaveLength(7);
    expect(s.ids[6]).toBe('Other');
  });
});

// Review 2 (re-graded): the chart's blue series is uncached input, and says so; never negative.
describe('tokenSeries', () => {
  it('labels uncached input and clamps it at zero', async () => {
    const { tokenSeries } = await import('../src/charts');
    const b = { ...toDays([day('2026-09-28')])[0], inputTokens: 10, cachedInputTokens: 25, outputTokens: 3 };
    const s = tokenSeries([b]);
    expect(s.map((x) => x.label)).toEqual(['Uncached input', 'Cached input', 'Output']);
    expect(s.map((x) => x.values[0])).toEqual([0, 25, 3]);
  });
});
