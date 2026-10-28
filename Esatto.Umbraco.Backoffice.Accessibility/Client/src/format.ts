import type { ScanSummary, ScoreResult, WcagLevel, WcagTarget, WcagVersion } from './types';

export const versionLabel = (v: WcagVersion) => ({ V20: '2.0', V21: '2.1', V22: '2.2' })[v];

/** "WCAG 2.2 AA" */
export const targetLabel = (t: WcagTarget) => `WCAG ${versionLabel(t.version)} ${t.level}`;

export const levelLabel = (l: WcagLevel | null) => l ?? 'Best practice';

/**
 * A scan's status for people. 'running' is only certain while this tab is scanning; afterwards the scan
 * may have been interrupted (tab closed), so it reads 'not finished' until it's resumed or a new one starts.
 */
export const scanStatusLabel = (status: ScanSummary['status']) => (status === 'running' ? 'not finished' : status);

/** Server DateTimes come without an offset but are UTC. */
export function parseUtc(value: string | null): Date | null {
  if (!value) return null;
  return new Date(/[zZ]|[+-]\d\d:\d\d$/.test(value) ? value : `${value}Z`);
}

export function formatDate(value: string | null, withTime = true): string {
  const d = parseUtc(value);
  if (!d) return '';
  return new Intl.DateTimeFormat(undefined, withTime ? { dateStyle: 'medium', timeStyle: 'short' } : { dateStyle: 'medium' }).format(d);
}

export type Band = 'good' | 'warning' | 'critical';

/** Score band for the ring: below 50 critical, below 90 needs work, 90 and up good. */
export function band(percent: number | null): Band | null {
  if (percent === null) return null;
  if (percent < 50) return 'critical';
  if (percent < 90) return 'warning';
  return 'good';
}

export const bandLabel: Record<Band, string> = { good: 'Good', warning: 'Needs work', critical: 'Major gaps' };

/**
 * The "not verified" criteria in two parts: ones the scanner ran its checks on and found no violations
 * (a person only has to confirm them), and ones only a person can check.
 */
export function notVerifiedParts(score: Pick<ScoreResult, 'notVerified' | 'scannerChecked'>): { toConfirm: number; personOnly: number } {
  return { toConfirm: score.scannerChecked, personOnly: score.notVerified - score.scannerChecked };
}
