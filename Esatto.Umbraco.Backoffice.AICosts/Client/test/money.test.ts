import { describe, it, expect } from 'vitest';
import { displayCurrency, convert, formatMoney } from '../src/money';
import type { AICostReport } from '../src/api';

const report = (perBaseUnit: Record<string, number> | null) =>
  ({ baseCurrency: 'USD', rates: perBaseUnit ? { date: '2026-10-09', source: 'ECB', perBaseUnit } : null }) as unknown as AICostReport;

describe('money', () => {
  it('converts with the rate of the display currency', () => {
    expect(convert(report({ USD: 1, SEK: 10 }), 2, 'SEK')).toBe(20);
  });
  it('falls back to the base currency without rates', () => {
    expect(displayCurrency(report(null), 'SEK')).toBe('USD');
    expect(convert(report(null), 2, 'USD')).toBe(2);
  });
  it('falls back to the base currency when the wanted one has no rate', () => {
    expect(displayCurrency(report({ USD: 1 }), 'SEK')).toBe('USD');
  });
  it('shows small amounts with four decimals', () => {
    expect(formatMoney(0.0123, 'USD')).toMatch(/0[.,]0123/);
    expect(formatMoney(12.5, 'USD')).toMatch(/12[.,]50/);
  });
});
