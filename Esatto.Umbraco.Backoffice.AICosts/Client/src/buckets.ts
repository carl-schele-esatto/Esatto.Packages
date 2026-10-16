import type { AICostDay } from './api';
import { parseDate } from './dates';

/** A day or a week of usage, for the cost chart, its table and the CSV. */
export interface Bucket {
  start: string;
  end: string;
  requests: number;
  inputTokens: number;
  outputTokens: number;
  cachedInputTokens: number;
  totalTokens: number;
  cost: number;
  unpricedTokens: number;
  succeededRequests: number;
  failedRequests: number;
}

const bucket = (d: AICostDay): Bucket => ({
  start: d.date,
  end: d.date,
  requests: d.requests,
  inputTokens: d.inputTokens,
  outputTokens: d.outputTokens,
  cachedInputTokens: d.cachedInputTokens,
  totalTokens: d.totalTokens,
  cost: d.cost,
  unpricedTokens: d.unpricedTokens,
  succeededRequests: d.succeededRequests,
  failedRequests: d.failedRequests,
});

export const toDays = (days: AICostDay[]): Bucket[] => days.map(bucket);

/** Monday-start weekly buckets for long ranges. */
export function toWeeks(days: AICostDay[]): Bucket[] {
  const weeks: Bucket[] = [];
  for (const d of days) {
    const current = weeks[weeks.length - 1];
    if (!current || parseDate(d.date).getUTCDay() === 1) {
      weeks.push(bucket(d));
      continue;
    }
    current.end = d.date;
    current.requests += d.requests;
    current.inputTokens += d.inputTokens;
    current.outputTokens += d.outputTokens;
    current.cachedInputTokens += d.cachedInputTokens;
    current.totalTokens += d.totalTokens;
    current.cost += d.cost;
    current.unpricedTokens += d.unpricedTokens;
    current.succeededRequests += d.succeededRequests;
    current.failedRequests += d.failedRequests;
  }
  return weeks;
}
