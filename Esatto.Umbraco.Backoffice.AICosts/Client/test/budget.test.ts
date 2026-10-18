import { describe, it, expect } from 'vitest';
import { budgetInDisplayCurrency } from '../src/budget';
import type { AICostReport } from '../src/api';

const report = (opts: { cost: number; projected: number; budget?: { amount: number; currency: string } | null; rates?: Record<string, number> | null }) =>
  ({
    baseCurrency: 'USD',
    rates: opts.rates === null ? null : { date: '2026-10-09', source: 'ECB', perBaseUnit: opts.rates ?? { USD: 1, SEK: 10 } },
    month: { from: '2026-10-01', to: '2026-10-12', cost: opts.cost, projectedCost: opts.projected, unpricedTokens: 0, budget: opts.budget ?? null },
  }) as unknown as AICostReport;

describe('budgetInDisplayCurrency', () => {
  it('no budget configured', () => expect(budgetInDisplayCurrency(report({ cost: 1, projected: 2 }), 'SEK')).toEqual({ kind: 'none' }));

  it('converts a SEK budget and compares in the display currency', () => {
    // 50 USD spent = 500 SEK of a 1000 SEK budget; projection 70 USD = 700 SEK.
    expect(budgetInDisplayCurrency(report({ cost: 50, projected: 70, budget: { amount: 1000, currency: 'SEK' } }), 'SEK'))
      .toEqual({ kind: 'set', amount: 1000, spentShare: 0.5, level: 'ok' });
  });

  it('warns at 80 % and when the projection goes over', () => {
    expect(budgetInDisplayCurrency(report({ cost: 80, projected: 90, budget: { amount: 1000, currency: 'SEK' } }), 'SEK')).toMatchObject({ level: 'warning' });
    expect(budgetInDisplayCurrency(report({ cost: 10, projected: 120, budget: { amount: 1000, currency: 'SEK' } }), 'SEK')).toMatchObject({ level: 'warning' });
  });

  it('is over once the cost passes the budget', () =>
    expect(budgetInDisplayCurrency(report({ cost: 101, projected: 150, budget: { amount: 1000, currency: 'SEK' } }), 'SEK')).toMatchObject({ level: 'over' }));

  // Review Focus 3.
  it('says why when there is no rate to convert the budget', () =>
    expect(budgetInDisplayCurrency(report({ cost: 5, projected: 9, budget: { amount: 1000, currency: 'SEK' }, rates: null }), 'USD'))
      .toEqual({ kind: 'unavailable', reason: 'Exchange rates are unavailable, so the SEK budget can’t be compared right now.' }));

  it('a budget in the base currency needs no rates', () =>
    expect(budgetInDisplayCurrency(report({ cost: 5, projected: 9, budget: { amount: 10, currency: 'USD' }, rates: null }), 'USD'))
      .toMatchObject({ kind: 'set', amount: 10, spentShare: 0.5 }));
});
