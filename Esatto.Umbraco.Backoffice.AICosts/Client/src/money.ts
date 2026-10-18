import type { AICostReport } from './api';

/** The currency amounts are shown in: the wanted one if it has a rate, otherwise the base currency. */
export function displayCurrency(report: AICostReport, wanted: string | null): string {
  if (wanted && (wanted === report.baseCurrency || report.rates?.perBaseUnit[wanted])) return wanted;
  return report.baseCurrency;
}

/** A base-currency amount in <currency> (the base currency itself needs no rate). */
export function convert(report: AICostReport, baseAmount: number, currency: string): number {
  if (currency === report.baseCurrency) return baseAmount;
  return baseAmount * (report.rates?.perBaseUnit[currency] ?? 1);
}

/** Money in the browser's locale; amounts under 1 get up to four decimals (cents hide them). */
export function formatMoney(value: number, currency: string, { compact = false } = {}): string {
  const small = Math.abs(value) > 0 && Math.abs(value) < 1;
  return new Intl.NumberFormat(undefined, {
    style: 'currency',
    currency,
    notation: compact ? 'compact' : 'standard',
    minimumFractionDigits: compact ? 0 : 2,
    maximumFractionDigits: compact ? 1 : small ? 4 : 2,
  }).format(value);
}

export function formatNumber(value: number, { compact = false } = {}): string {
  return new Intl.NumberFormat(undefined, { notation: compact ? 'compact' : 'standard', maximumFractionDigits: 1 }).format(value);
}
