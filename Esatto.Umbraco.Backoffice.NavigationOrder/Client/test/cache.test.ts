import { describe, expect, it } from 'vitest';
import { isNavigationOrderData, readCachedOrder, writeCachedOrder } from '../src/apply/cache';
import type { NavigationOrderData } from '../src/core/types';

function memoryStorage(initial: Record<string, string> = {}) {
  const items = new Map(Object.entries(initial));
  return {
    getItem: (key: string) => items.get(key) ?? null,
    setItem: (key: string, value: string) => void items.set(key, value),
    items,
  };
}

const order: NavigationOrderData = { sections: ['Umb.Section.Forms'], tabs: [{ section: 'Umb.Section.Settings', aliases: ['A', 'B'] }] };

describe('the cached order', () => {
  it('round-trips through storage', () => {
    const storage = memoryStorage();
    writeCachedOrder(order, storage);
    expect(readCachedOrder(storage)).toEqual(order);
  });

  it('is null when nothing is cached', () => {
    expect(readCachedOrder(memoryStorage())).toBeNull();
  });

  it('is null when storage throws or is missing', () => {
    expect(readCachedOrder({ getItem: () => { throw new Error('SecurityError'); } })).toBeNull();
    expect(readCachedOrder()).toBeNull(); // no localStorage under node
  });

  it('is null when the cached value is junk', () => {
    expect(readCachedOrder(memoryStorage({ 'esatto.navigationOrder.v1': '{' }))).toBeNull();
    expect(readCachedOrder(memoryStorage({ 'esatto.navigationOrder.v1': '{"sections":5,"tabs":[]}' }))).toBeNull();
    expect(readCachedOrder(memoryStorage({ 'esatto.navigationOrder.v1': '{"sections":null,"tabs":[{"section":"S","aliases":[1]}]}' }))).toBeNull();
  });

  it('ignores a storage that refuses writes', () => {
    expect(() => writeCachedOrder(order, { setItem: () => { throw new Error('QuotaExceededError'); } })).not.toThrow();
    expect(() => writeCachedOrder(order)).not.toThrow();
  });

  it('recognises the API shape, extra fields included', () => {
    expect(isNavigationOrderData({ sections: null, tabs: [], updatedUtc: null })).toBe(true);
    expect(isNavigationOrderData({ sections: [], tabs: null })).toBe(false);
    expect(isNavigationOrderData(null)).toBe(false);
  });
});
