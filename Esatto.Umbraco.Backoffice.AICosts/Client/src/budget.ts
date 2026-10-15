import type { AICostReport } from './api';
import { convert } from './money';

export type BudgetState =
  | { kind: 'none' }
  | { kind: 'unavailable'; reason: string }
  | { kind: 'set'; amount: number; spentShare: number; level: 'ok' | 'warning' | 'over' };

/**
 * The month's budget in the display currency, and how far the month is into it: "over" once the cost
 * so far passes the budget, "warning" from 80 % or when the projection goes over, otherwise "ok".
 */
export function budgetInDisplayCurrency(report: AICostReport, currency: string): BudgetState {
  const budget = report.month.budget;
  if (!budget) return { kind: 'none' };

  // Budget → base currency (the month's cost is in base), via the budget currency's rate.
  const budgetRate = budget.currency === report.baseCurrency ? 1 : report.rates?.perBaseUnit[budget.currency];
  if (!budgetRate) {
    return { kind: 'unavailable', reason: `Exchange rates are unavailable, so the ${budget.currency} budget can’t be compared right now.` };
  }
  const budgetInBase = budget.amount / budgetRate;
  const spentShare = budgetInBase > 0 ? report.month.cost / budgetInBase : 0;
  const level =
    report.month.cost > budgetInBase ? 'over' : spentShare >= 0.8 || report.month.projectedCost > budgetInBase ? 'warning' : 'ok';
  return { kind: 'set', amount: convert(report, budgetInBase, currency), spentShare, level };
}
