// The period, currency and heatmap metric, remembered per browser. Same key as Esatto.Web's tab, so
// preferences carry over. Storage can be unavailable (private mode, blocked): then nothing persists.

export const STORAGE_KEY = 'esatto.aiCosts.prefs';

export interface Prefs {
  days?: number;
  currency?: string;
  metric?: string;
}

const defaultStorage = (): Storage | undefined => {
  try {
    return globalThis.localStorage;
  } catch {
    return undefined;
  }
};

export function readPrefs(storage: Storage | undefined = defaultStorage()): Prefs {
  try {
    const value = JSON.parse(storage?.getItem(STORAGE_KEY) ?? '{}');
    return value && typeof value === 'object' && !Array.isArray(value) ? (value as Prefs) : {};
  } catch {
    return {};
  }
}

export function writePrefs(prefs: Prefs, storage: Storage | undefined = defaultStorage()): void {
  try {
    storage?.setItem(STORAGE_KEY, JSON.stringify(prefs));
  } catch {
    /* storage unavailable — preferences just don't persist */
  }
}
