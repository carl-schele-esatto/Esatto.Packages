// The Costs tab's two endpoints (AICostsController) and their response shapes — camelCase mirrors of
// the C# records in src/AICostReport.cs and src/AICostBreakdown.cs. Amounts are in the base currency.

import { umbHttpClient } from '@umbraco-cms/backoffice/http-client';

export interface AICostDay {
  date: string;
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

export interface AICostModel {
  modelId: string;
  requests: number;
  inputTokens: number;
  outputTokens: number;
  cachedInputTokens: number;
  totalTokens: number;
  cost: number | null;
}

export interface AICostTotals {
  requests: number;
  totalTokens: number;
  cost: number;
  unpricedTokens: number;
}

export interface ExchangeRates {
  date: string;
  source: string;
  perBaseUnit: Record<string, number>;
}

export interface AICostBudget {
  amount: number;
  currency: string;
}

export interface AICostMonth {
  from: string;
  to: string;
  cost: number;
  projectedCost: number;
  unpricedTokens: number;
  budget: AICostBudget | null;
  days: { date: string; cost: number }[];
}

/** A priced model's cost per day; costs[i] belongs to the report's days[i]. */
export interface AICostModelDays {
  modelId: string;
  costs: number[];
}

export interface AICostPrevious {
  from: string;
  to: string;
  totals: AICostTotals;
}

export interface AICostReport {
  from: string;
  to: string;
  baseCurrency: string;
  defaultCurrency: string;
  currencies: string[];
  rates: ExchangeRates | null;
  pricesSource: string | null;
  days: AICostDay[];
  models: AICostModel[];
  totals: AICostTotals;
  month: AICostMonth;
  previous: AICostPrevious | null;
  modelDays: AICostModelDays[];
  cachingSavings: number;
  averageDurationMs: number;
}

export interface AICostBreakdownRow {
  id: string;
  name: string;
  requests: number;
  inputTokens: number;
  cachedInputTokens: number;
  outputTokens: number;
  totalTokens: number;
  cost: number | null;
  unpricedTokens: number;
}

export interface AICostBreakdown {
  dimension: string;
  rows: AICostBreakdownRow[];
}

export type Dimension = 'model' | 'profile' | 'user' | 'provider';

const BASE = '/umbraco/management/api/v1/esatto/ai-costs';

// `security: [bearer]` makes the shared backoffice client attach the user's token.
const security = () => [{ scheme: 'bearer' as const, type: 'http' as const }];

export const getReport = (days: number) =>
  umbHttpClient.get<{ 200: AICostReport }>({ url: `${BASE}/report?days=${days}`, security: security() });

export const getBreakdown = (dimension: Exclude<Dimension, 'model'>, days: number) =>
  umbHttpClient.get<{ 200: AICostBreakdown }>({ url: `${BASE}/breakdown?dimension=${dimension}&days=${days}`, security: security() });

/** Currency codes are dictionary keys; the API serializer may camel-case them ("SEK" → "sek"). */
export function normalizeRates(report: AICostReport): AICostReport {
  if (!report.rates) return report;
  const perBaseUnit = Object.fromEntries(
    Object.entries(report.rates.perBaseUnit).map(([code, rate]) => [code.toUpperCase(), rate]),
  );
  return { ...report, rates: { ...report.rates, perBaseUnit } };
}
