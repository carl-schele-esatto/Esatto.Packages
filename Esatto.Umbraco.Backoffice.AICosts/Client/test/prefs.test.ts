import { describe, it, expect } from 'vitest';
import { readPrefs, writePrefs } from '../src/prefs';

const memory = () => {
  const data = new Map<string, string>();
  return { getItem: (k: string) => data.get(k) ?? null, setItem: (k: string, v: string) => void data.set(k, v) } as unknown as Storage;
};
const broken = { getItem: () => { throw new Error('denied'); }, setItem: () => { throw new Error('denied'); } } as unknown as Storage;

describe('prefs', () => {
  it('round-trips', () => {
    const s = memory();
    writePrefs({ days: 90, currency: 'SEK', metric: 'tokens' }, s);
    expect(readPrefs(s)).toEqual({ days: 90, currency: 'SEK', metric: 'tokens' });
  });
  it('ignores unavailable storage', () => {
    expect(readPrefs(broken)).toEqual({});
    expect(() => writePrefs({ days: 30 }, broken)).not.toThrow();
  });
  it('ignores corrupt values', () => {
    const s = memory();
    s.setItem('esatto.aiCosts.prefs', '{not json');
    expect(readPrefs(s)).toEqual({});
  });
});
