import type { Owner, Severity } from './types';

/** Server DateTimes come without an offset but are UTC. */
export function parseUtc(value: string | null): Date | null {
  if (!value) return null;
  return new Date(/[zZ]|[+-]\d\d:\d\d$/.test(value) ? value : `${value}Z`);
}

export function formatDate(value: string | null): string {
  const d = parseUtc(value);
  return d ? new Intl.DateTimeFormat(undefined, { dateStyle: 'medium', timeStyle: 'short' }).format(d) : '';
}

export type Band = 'good' | 'warning' | 'critical';

/** Score band: below 50 critical, below 90 needs work, 90 and up good. */
export function band(score: number | null): Band | null {
  if (score === null) return null;
  if (score < 50) return 'critical';
  if (score < 90) return 'warning';
  return 'good';
}

export const bandLabel: Record<Band, string> = { good: 'Good', warning: 'Needs work', critical: 'Serious problems' };

export const severityLabel: Record<Severity, string> = { critical: 'Critical', warning: 'Warning', notice: 'Notice' };
export const severityIcon: Record<Severity, string> = { critical: 'icon-wrong', warning: 'icon-alert', notice: 'icon-info' };
export const ownerLabel: Record<Owner, string> = { editor: 'Editor', developer: 'Developer' };

export const skipLabel: Record<string, string> = {
  protected: 'Members only',
  excluded: 'Excluded in Settings',
  'no-url': 'No address (no domain bound and no base URL)',
};

export const plural = (n: number, one: string, many: string) => `${n} ${n === 1 ? one : many}`;

/** Opens the content node in the backoffice (with its culture for variant content). */
export function editContentUrl(key: string, culture: string): string {
  const base = `/umbraco/section/content/workspace/document/edit/${key}`;
  return culture ? `${base}/${culture}` : base;
}

/** The URL if it's absolute http(s), otherwise undefined: stored values must never become a javascript: href. */
export function safeHref(url: string | null | undefined): string | undefined {
  try {
    const u = new URL(url ?? '');
    return u.protocol === 'http:' || u.protocol === 'https:' ? u.toString() : undefined;
  } catch {
    return undefined;
  }
}
