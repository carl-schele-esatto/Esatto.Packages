import type { NavigationOrderData } from '../core/types';

// The last order fetched from the server, so a reload shows the right order straight away instead of
// Umbraco's default order first. Per browser; the server stays the source of truth.

const KEY = 'esatto.navigationOrder.v1';

const isStringArray = (value: unknown): value is string[] => Array.isArray(value) && value.every((item) => typeof item === 'string');

/** True for `{ sections: string[] | null, tabs: { section, aliases }[] }`; extra fields are allowed. */
export function isNavigationOrderData(value: unknown): value is NavigationOrderData {
  if (typeof value !== 'object' || value === null) return false;
  const { sections, tabs } = value as Record<string, unknown>;
  return (
    (sections === null || isStringArray(sections)) &&
    Array.isArray(tabs) &&
    tabs.every((tab) => typeof tab === 'object' && tab !== null && typeof tab.section === 'string' && isStringArray(tab.aliases))
  );
}

/** The cached order, or null when there is none or it can't be read (private mode, blocked storage, junk). */
export function readCachedOrder(storage?: Pick<Storage, 'getItem'>): NavigationOrderData | null {
  try {
    const raw = (storage ?? globalThis.localStorage).getItem(KEY);
    if (!raw) return null;
    const value: unknown = JSON.parse(raw);
    return isNavigationOrderData(value) ? { sections: value.sections, tabs: value.tabs } : null;
  } catch {
    return null;
  }
}

/** Caches `order`; does nothing when storage refuses. */
export function writeCachedOrder(order: NavigationOrderData, storage?: Pick<Storage, 'setItem'>): void {
  try {
    (storage ?? globalThis.localStorage).setItem(KEY, JSON.stringify({ sections: order.sections, tabs: order.tabs }));
  } catch {
    // Private mode or storage disabled: the order still comes from the server.
  }
}
