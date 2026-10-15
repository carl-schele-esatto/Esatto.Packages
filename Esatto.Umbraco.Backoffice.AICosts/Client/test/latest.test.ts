import { describe, it, expect } from 'vitest';
import { Latest } from '../src/latest';

// Final review, Important 2: click "12 months" then "30 days" — the slower 12-month response must not
// overwrite the 30-day one that arrived first.
describe('Latest', () => {
  it('only the newest ticket is current', () => {
    const latest = new Latest();
    const first = latest.next();
    const second = latest.next();
    expect(latest.isCurrent(first)).toBe(false);
    expect(latest.isCurrent(second)).toBe(true);
  });
});
