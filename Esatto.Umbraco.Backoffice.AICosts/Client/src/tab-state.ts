// Pure decisions the Costs tab makes about what to show.

import type { AICostBreakdown } from './api';

export type BreakdownState =
  | { kind: 'rows'; breakdown: AICostBreakdown }
  | { kind: 'error'; message: string }
  | { kind: 'loading' };

/**
 * What the "Cost by" card shows for a breakdown key (\`dimension:days\`). Errors are kept per key, so a
 * failed Profile load doesn't hide the User rows that already loaded.
 */
export function breakdownState(key: string, loaded: Map<string, AICostBreakdown>, errors: Map<string, string>): BreakdownState {
  const breakdown = loaded.get(key);
  if (breakdown) return { kind: 'rows', breakdown };
  const message = errors.get(key);
  return message ? { kind: 'error', message } : { kind: 'loading' };
}

/** The currency box's options: the configured list, plus the shown currency when it isn't in it
 * (without exchange rates amounts are in the base currency, which the list may not contain). */
export function currencyOptions(currencies: string[], shown: string): string[] {
  return currencies.includes(shown) ? currencies : [...currencies, shown];
}

/** The cost chart's (and its table's) message when there's nothing to draw; null when there is. */
export function costEmptyMessage(allZero: boolean, unpricedTokens: number): string | null {
  if (!allZero) return null;
  return unpricedTokens > 0 ? 'No cost to show — the models used in this period have no configured price.' : 'No AI usage in this period.';
}
