import { describe, it, expect } from 'vitest';
import { renderDelay, LatestRequest } from '../src/render-scheduling';

const DEBOUNCE = 400;

describe('renderDelay', () => {
  // Deferred minor (1.0.1): the block's data arriving for the first time counted as an edit, and its
  // debounce replaced the immediate first render, so every preview waited 400 ms.
  it('renders the first data immediately', () => {
    expect(renderDelay(new Map([['content', undefined]]), DEBOUNCE)).toBe(0);
  });

  it('debounces edits to existing content', () => {
    expect(renderDelay(new Map([['content', { title: 'old' }]]), DEBOUNCE)).toBe(DEBOUNCE);
  });

  it('debounces edits to existing settings', () => {
    expect(renderDelay(new Map<PropertyKey, unknown>([['settings', { fullWidth: false }]]), DEBOUNCE)).toBe(DEBOUNCE);
  });

  it('debounces when content is edited while settings arrive for the first time', () => {
    expect(renderDelay(new Map<PropertyKey, unknown>([['content', { title: 'old' }], ['settings', undefined]]), DEBOUNCE)).toBe(DEBOUNCE);
  });

  it('ignores changes to anything else', () => {
    expect(renderDelay(new Map([['label', 'Old label']]), DEBOUNCE)).toBeNull();
  });
});

describe('LatestRequest', () => {
  // Deferred minor (1.0.1): pending renders couldn't be cancelled.
  it('aborts the previous request when a newer one starts', () => {
    const requests = new LatestRequest();
    const first = requests.next();
    const second = requests.next();
    expect(first.aborted).toBe(true);
    expect(second.aborted).toBe(false);
  });

  it('cancel aborts the current request', () => {
    const requests = new LatestRequest();
    const signal = requests.next();
    requests.cancel();
    expect(signal.aborted).toBe(true);
  });

  it('cancel without a request does nothing', () => {
    expect(() => new LatestRequest().cancel()).not.toThrow();
  });
});
