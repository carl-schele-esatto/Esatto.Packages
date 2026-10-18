import { describe, it, expect } from 'vitest';
import { quartiles, binOf, niceTicks } from '../src/scales';

describe('scales', () => {
  it('quartiles of the values', () => expect(quartiles([1, 2, 3, 4, 5, 6, 7, 8])).toEqual([3, 5, 7]));
  it('no values, no thresholds', () => expect(quartiles([])).toEqual([]));
  it('bins by threshold', () => {
    expect(binOf(1, [3, 5, 7])).toBe(0);
    expect(binOf(6, [3, 5, 7])).toBe(2);
    expect(binOf(9, [3, 5, 7])).toBe(3);
  });
  it('round ticks covering the max', () => expect(niceTicks(9)).toEqual([0, 2.5, 5, 7.5, 10]));
  it('zero max gets a 0–1 axis', () => expect(niceTicks(0)).toEqual([0, 1]));
});
