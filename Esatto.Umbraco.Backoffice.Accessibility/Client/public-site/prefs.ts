// Visitor preferences → attributes on <html>. Shared by a11y-boot.ts and a11y-panel.ts (each is built
// as its own self-contained file). The data-a11y-* attributes are the public contract site CSS can use.

export type PrefKey = 'text' | 'spacing' | 'contrast' | 'motion' | 'font' | 'links' | 'guide';
export type Prefs = Partial<Record<PrefKey, boolean>>;

export const STORAGE_KEY = 'esatto-a11y';

/** Preference → [attribute, value] on <html>. Documented in the README; don't rename. */
export const ATTRIBUTES: Record<PrefKey, [string, string]> = {
  text: ['data-a11y-text', 'large'],
  spacing: ['data-a11y-spacing', 'wide'],
  contrast: ['data-a11y-contrast', 'high'],
  motion: ['data-a11y-motion', 'reduce'],
  font: ['data-a11y-font', 'readable'],
  links: ['data-a11y-links', 'highlight'],
  guide: ['data-a11y-guide', 'on'],
};

const KEYS = Object.keys(ATTRIBUTES) as PrefKey[];

export interface OsPrefs { reducedMotion: boolean; moreContrast: boolean }
export interface Follow { motion: boolean; contrast: boolean }

/**
 * The attributes to set. A visitor's explicit choice (true or false) wins, but only for options the panel
 * currently offers; otherwise motion and contrast follow the OS when the site has switched that on.
 */
export function attributesFor(prefs: Prefs, os: OsPrefs, follow: Follow, offered: PrefKey[]): Record<string, string> {
  const attrs: Record<string, string> = {};
  for (const key of KEYS) {
    const explicit = offered.includes(key) ? prefs[key] : undefined;
    const fromOs = (key === 'motion' && follow.motion && os.reducedMotion) || (key === 'contrast' && follow.contrast && os.moreContrast);
    if (explicit ?? fromOs) {
      const [name, value] = ATTRIBUTES[key];
      attrs[name] = value;
    }
  }
  return attrs;
}

/** Sets exactly these data-a11y-* attributes on the element, removing the others. */
export function applyAttributes(el: Element, attrs: Record<string, string>): void {
  for (const [name] of Object.values(ATTRIBUTES)) {
    if (name in attrs) el.setAttribute(name, attrs[name]);
    else el.removeAttribute(name);
  }
}

export function readPrefs(storage: Pick<Storage, 'getItem'> | null): Prefs {
  try {
    const raw = storage?.getItem(STORAGE_KEY);
    if (!raw) return {};
    const parsed: unknown = JSON.parse(raw);
    if (!parsed || typeof parsed !== 'object') return {};
    const prefs: Prefs = {};
    for (const key of KEYS) {
      const value = (parsed as Record<string, unknown>)[key];
      if (typeof value === 'boolean') prefs[key] = value;
    }
    return prefs;
  } catch {
    // Private mode, blocked storage or a corrupt value: behave as if nothing was saved.
    return {};
  }
}

export function writePrefs(storage: Pick<Storage, 'setItem' | 'removeItem'> | null, prefs: Prefs): void {
  try {
    if (Object.keys(prefs).length === 0) storage?.removeItem(STORAGE_KEY);
    else storage?.setItem(STORAGE_KEY, JSON.stringify(prefs));
  } catch {
    // Storage unavailable: the choice still applies for this page view.
  }
}

/** "text,guide" (from data-options) → the offered keys. */
export function parseOptions(value: string | undefined): PrefKey[] {
  return (value ?? '').split(',').map((s) => s.trim()).filter((s): s is PrefKey => (KEYS as string[]).includes(s));
}

export function safeStorage(): Storage | null {
  try {
    return window.localStorage;
  } catch {
    return null;
  }
}
