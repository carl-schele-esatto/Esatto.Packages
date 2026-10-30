import { describe, it, expect, vi, beforeEach, afterEach } from 'vitest';
import { HoverExpand } from '../src/core/hover-expand';

const DELAY = 700;

describe('HoverExpand', () => {
  let expanded: string[];
  let hover: HoverExpand<string>;

  beforeEach(() => {
    vi.useFakeTimers();
    expanded = [];
    hover = new HoverExpand<string>((row) => expanded.push(row), DELAY);
  });

  afterEach(() => {
    vi.useRealTimers();
  });

  it('resting on the middle of a row expands it after the delay', () => {
    hover.over('LRF', 'into');
    vi.advanceTimersByTime(DELAY - 1);
    expect(expanded).toEqual([]);
    vi.advanceTimersByTime(1);
    expect(expanded).toEqual(['LRF']);
  });

  it('resting on the top or bottom edge never expands the row', () => {
    // Pausing on an edge to place an item above or below a row must not open it.
    hover.over('LRF', 'before');
    vi.advanceTimersByTime(DELAY * 3);
    hover.over('Dafo', 'after');
    vi.advanceTimersByTime(DELAY * 3);
    expect(expanded).toEqual([]);
  });

  it('moving from the middle to an edge of the same row cancels the pending expand', () => {
    hover.over('LRF', 'into');
    vi.advanceTimersByTime(DELAY / 2);
    hover.over('LRF', 'before');
    vi.advanceTimersByTime(DELAY * 3);
    expect(expanded).toEqual([]);
  });

  it('back on the middle after an edge starts the delay again', () => {
    hover.over('LRF', 'into');
    vi.advanceTimersByTime(DELAY / 2);
    hover.over('LRF', 'after');
    hover.over('LRF', 'into');
    vi.advanceTimersByTime(DELAY - 1);
    expect(expanded).toEqual([]);
    vi.advanceTimersByTime(1);
    expect(expanded).toEqual(['LRF']);
  });

  it('repeated dragovers on the middle of the same row keep the first timer', () => {
    hover.over('LRF', 'into');
    vi.advanceTimersByTime(DELAY / 2);
    hover.over('LRF', 'into');
    vi.advanceTimersByTime(DELAY / 2);
    expect(expanded).toEqual(['LRF']);
  });

  it('moving to another row expands only that row', () => {
    hover.over('LRF', 'into');
    vi.advanceTimersByTime(DELAY / 2);
    hover.over('Dafo', 'into');
    vi.advanceTimersByTime(DELAY * 3);
    expect(expanded).toEqual(['Dafo']);
  });

  it('leaving the armed row cancels; leaving another row does not', () => {
    hover.over('LRF', 'into');
    hover.leave('Dafo');
    vi.advanceTimersByTime(DELAY);
    expect(expanded).toEqual(['LRF']);

    hover.cancel();
    hover.over('Dafo', 'into');
    hover.leave('Dafo');
    vi.advanceTimersByTime(DELAY * 3);
    expect(expanded).toEqual(['LRF']);
  });

  it('cancel stops a pending expand', () => {
    hover.over('LRF', 'into');
    hover.cancel();
    vi.advanceTimersByTime(DELAY * 3);
    expect(expanded).toEqual([]);
  });
});
