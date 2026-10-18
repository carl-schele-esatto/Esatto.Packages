// Registry of tree adapters, keyed by entity type. Pure — no DOM.

import type { TreeAdapter } from './types';

const adapters = new Map<string, TreeAdapter>();

export function registerAdapter(adapter: TreeAdapter): void {
  adapters.set(adapter.entityType, adapter);
}

export function adapterForEntityType(entityType: string | null | undefined): TreeAdapter | undefined {
  return entityType ? adapters.get(entityType) : undefined;
}

/** Test hook: forget every registered adapter. */
export function clearAdapters(): void {
  adapters.clear();
}
