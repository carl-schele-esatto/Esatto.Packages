// What the dashboard shows, decided from what the server said. Pure functions, so the rules are tested without a browser.
//
// The shell holds the status and the report and asks these what to put on the screen. Nothing here fetches anything.

import { hasWarnings } from './report-helpers';
import type { TabId } from './navigate';
import type { FetchStatus, Report, StatusResponse } from './types';

/**
 * - notConnected  the package is not set up (no key, no property) or the saved key cannot be read: the not-connected card
 * - progress      a fetch is running, or the archive is behind and there is nothing to show yet: the progress card. While a run is going the
 *                 card leads and a report that is already archived stays below it; with no report it is never empty charts
 * - error         the report could not be READ (the archive answered with a failure): only the problem is shown, with the month controls and
 *                 Try again. It is never "no report" or "nothing fetched yet", which would call for an action that cannot be followed
 * - noData        connected, but there is nothing to chart: the report says neither source returned anything, or the month has no report
 * - ready         a report to show
 */
export type ViewState = 'notConnected' | 'progress' | 'error' | 'noData' | 'ready';

/** Whether there is a report with something to chart (not absent, not the "no data" report). */
export function hasContent(report: Report | null): report is Report {
  return report !== null && report.data_status !== 'noData';
}

/**
 * What to show. <c>readFailed</c> says the read of the report failed and there is no report to show instead (the model sets it); it only
 * counts while there is none, so a report that is held is never hidden by it.
 */
export function viewStateOf(status: StatusResponse, report: Report | null, readFailed = false): ViewState {
  if (status.setup !== 'ready') return 'notConnected';
  if (status.fetch.running) return 'progress';
  if (report === null && readFailed) return 'error';
  if (report === null && !status.upToDate) return 'progress';
  return hasContent(report) ? 'ready' : 'noData';
}

/**
 * The tabs of a report, in the order of the strip: all but "To fix" are always there (a tab with nothing to show says so itself);
 * "To fix" only when the report carries findings (warnings), and Settings only for a user who may open it.
 */
export function visibleTabs(report: Report | null, hasSettingsAccess: boolean): TabId[] {
  const tabs: TabId[] = ['analysis', 'traffic', 'queries', 'topPages'];
  if (hasWarnings(report)) tabs.push('toFix');
  tabs.push('comparison');
  if (hasSettingsAccess) tabs.push('settings');
  return tabs;
}

/**
 * The strip the dashboard shows: the report's tabs when there is something to show, and otherwise only Settings (for a user who may
 * open it), because nothing but the connection can be worked on until a report exists.
 */
export function tabsFor(report: Report | null, hasSettingsAccess: boolean): TabId[] {
  return hasContent(report) ? visibleTabs(report, hasSettingsAccess) : hasSettingsAccess ? ['settings'] : [];
}

/**
 * The tab to show: Settings while it is pinned (the person is in the middle of something on it) and on the strip; otherwise the selected one when
 * it is on the strip, otherwise the first (a selected "To fix" can lose its tab when the month changes). While the package is not connected, or a
 * read failed, nothing is shown until the person has asked for Settings.
 */
export function activeTab(selected: TabId, tabs: readonly TabId[], state: ViewState, pinned = false): TabId | null {
  if (pinned && tabs.includes('settings')) return 'settings';
  if (tabs.includes(selected)) return selected;
  return state === 'notConnected' || state === 'error' ? null : (tabs[0] ?? null);
}

/**
 * The tab the person chose, and whether Settings is pinned. Settings can be on the screen without being chosen (the only tab during a first
 * backfill, or for a month without data), and would then give way to the report when the strip grows, or to nothing when a removal makes the
 * package not connected. Once the person works on it, it is pinned instead, so it stays with what they typed and where their focus is.
 */
export interface TabChoice {
  tab: TabId;
  pinned: boolean;
}

/** The person chose a tab: choosing any tab but Settings releases a pin. */
export function chooseTab(choice: TabChoice, tab: TabId): TabChoice {
  return { tab, pinned: tab === 'settings' && choice.pinned };
}

/** The person is working on the Settings tab that is shown: it is the chosen tab now, and pinned. */
export function pinSettings(choice: TabChoice): TabChoice {
  return choice.tab === 'settings' && choice.pinned ? choice : { tab: 'settings', pinned: true };
}

export interface ProgressView {
  /** `starting`: running but no month counted yet; `running`: counting; `idle`: not running. */
  phase: 'starting' | 'running' | 'idle';
  done: number;
  total: number;
  /** The share of the months done, 0 to 100; null when it is not known (starting) or nothing runs. */
  percent: number | null;
  currentPeriod: string | null;
}

/**
 * The backfill's progress for the card. A run that is running with `total` 0 is "starting" (the server counts the months a moment after
 * it has answered the start), and must not be read as "nothing to fetch".
 */
export function progressOf(fetch: FetchStatus): ProgressView {
  const { done, total } = fetch;
  if (!fetch.running) return { phase: 'idle', done, total, percent: null, currentPeriod: null };
  if (total <= 0) return { phase: 'starting', done, total, percent: null, currentPeriod: null };

  return { phase: 'running', done, total, percent: Math.min(100, Math.round((done / total) * 100)), currentPeriod: fetch.currentPeriod };
}

/**
 * How a run that has ended went (the progress read after `running` turned false): it failed (it ended with a code, even part of the way),
 * there was nothing to fetch (it counted no months), or the months were fetched.
 */
export function runOutcomeOf(fetch: FetchStatus): 'failed' | 'nothingToFetch' | 'fetched' {
  if (fetch.errorCode) return 'failed';
  return fetch.total <= 0 ? 'nothingToFetch' : 'fetched';
}

/**
 * Why the last fetch did not finish, for the "nothing fetched" card and the "archive is behind" banner: the code the latest run in this
 * process ended with, else the last recorded failure, but only while nothing has worked since (a failure that was fixed long ago is not the
 * reason; at the same moment the success wins). The server already leaves out a failure that a success followed; this holds whatever it sends.
 */
export function lastErrorOf(fetch: FetchStatus): string | null {
  if (fetch.errorCode) return fetch.errorCode;

  const failed = fetch.lastFailureUtc ? Date.parse(fetch.lastFailureUtc) : Number.NaN;
  if (!fetch.lastErrorCode || Number.isNaN(failed)) return null;

  const worked = fetch.lastSuccessUtc ? Date.parse(fetch.lastSuccessUtc) : Number.NaN;
  return Number.isNaN(worked) || failed > worked ? fetch.lastErrorCode : null;
}

/** The archived months newest first, once each, for the month picker (the server lists them oldest first). "YYYY-MM" sorts as text. */
export function periodsNewestFirst(periods: readonly string[]): string[] {
  return [...new Set(periods)].sort().reverse();
}

/**
 * Whether the month picker should act on a change to <c>next</c>. It stays focusable while a read or a refresh is going on (a control that
 * disables itself loses the keyboard's place, and on some platforms an arrow key on a closed list fires a change each press), so instead of
 * being disabled it ignores the change. A month that is already selected or not archived is ignored too.
 */
export function shouldAcceptPeriodChange(busy: boolean, current: string, next: string, periods: readonly string[]): boolean {
  return !busy && next !== current && periods.includes(next);
}

/** The month the picker stands on: the selected one, or the newest archived when none has been read yet (a read that failed). */
export function selectedPeriod(period: string, periods: readonly string[]): string {
  return period || periodsNewestFirst(periods)[0] || '';
}
