import { describe, it, expect } from 'vitest';
import { change } from '../src/compare';

describe('change', () => {
  it('percentage vs previous', () => expect(change(112, 100)).toEqual({ kind: 'change', percent: 12 }));
  it('down', () => expect(change(50, 100)).toEqual({ kind: 'change', percent: -50 }));
  it('previous zero is new', () => expect(change(5, 0)).toEqual({ kind: 'new' }));
  it('both zero is no change', () => expect(change(0, 0)).toEqual({ kind: 'change', percent: 0 }));
  it('no previous period', () => expect(change(5, null)).toEqual({ kind: 'none' }));
});
