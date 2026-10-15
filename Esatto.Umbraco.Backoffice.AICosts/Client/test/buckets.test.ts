import { describe, it, expect } from 'vitest';
import { toWeeks } from '../src/buckets';
import type { AICostDay } from '../src/api';

const day = (date: string, cost: number): AICostDay =>
  ({ date, requests: 1, inputTokens: 10, outputTokens: 1, cachedInputTokens: 0, totalTokens: 11, cost, unpricedTokens: 0 });

describe('toWeeks', () => {
  it('starts a new bucket every Monday, across a month end', () => {
    // 2026-09-27 is a Sunday, 2026-09-28 a Monday.
    const weeks = toWeeks([day('2026-09-27', 1), day('2026-09-28', 2), day('2026-09-30', 3), day('2026-10-01', 4)]);
    expect(weeks.map((w) => [w.start, w.end, w.cost])).toEqual([
      ['2026-09-27', '2026-09-27', 1],
      ['2026-09-28', '2026-10-01', 9],
    ]);
  });
});
