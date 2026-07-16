// A process-wide registry of the dictionary keys THIS package registered (populated after
// the fetch). The surface-aware patches use it to gate ONLY our content-dictionary keys:
// Umbraco's own UI keys (e.g. `buttons_save`) are never in this set, so they always resolve
// everywhere and the backoffice chrome is never shown raw.

const knownKeys = new Set<string>();

/** Adds keys (both dotted and underscore-normalized forms) to the registry. */
export function registerKnownKeys(keys: Iterable<string>): void {
  for (const key of keys) {
    knownKeys.add(key);
  }
}

/** True when `key` is one of our registered content-dictionary keys. */
export function isKnownDictionaryKey(key: string): boolean {
  return knownKeys.has(key);
}

/** Test-only: empties the registry between cases. */
export function clearKnownKeysForTest(): void {
  knownKeys.clear();
}
