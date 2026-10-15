import { describe, it, expect } from 'vitest';
import { breakdownState, costEmptyMessage, currencyOptions } from '../src/tab-state';

describe('breakdownState', () => {
  const rows = { dimension: 'user', rows: [] };
  // Deferred minor (fixed): an error for Profile must not hide the already loaded User rows.
  it('an error belongs to its own dimension and period', () => {
    const errors = new Map([['profile:30', 'Could not load the breakdown.']]);
    const loaded = new Map([['user:30', rows]]);
    expect(breakdownState('user:30', loaded, errors)).toEqual({ kind: 'rows', breakdown: rows });
    expect(breakdownState('profile:30', loaded, errors)).toEqual({ kind: 'error', message: 'Could not load the breakdown.' });
  });
  it('loading until it arrives', () => expect(breakdownState('user:90', new Map(), new Map())).toEqual({ kind: 'loading' }));
});

describe('currencyOptions', () => {
  // Deferred minor (fixed): without rates the tab shows the base currency, so the box must offer it.
  it('adds the shown currency when it is not in the list', () => expect(currencyOptions(['SEK', 'EUR'], 'USD')).toEqual(['SEK', 'EUR', 'USD']));
  it('leaves the list alone otherwise', () => expect(currencyOptions(['SEK', 'USD'], 'USD')).toEqual(['SEK', 'USD']));
});

describe('costEmptyMessage', () => {
  // Deferred minor (fixed): chart and table say the same thing when nothing is priced.
  it('usage without prices', () => expect(costEmptyMessage(true, 500)).toBe('No cost to show — the models used in this period have no configured price.'));
  it('no usage', () => expect(costEmptyMessage(true, 0)).toBe('No AI usage in this period.'));
  it('something to show', () => expect(costEmptyMessage(false, 500)).toBeNull());
});
