import type { IssueFilter } from './types';

export type TabId = 'overview' | 'issues' | 'checklist' | 'settings';

export interface NavigateDetail { tab: TabId; filter?: IssueFilter }

export const NAVIGATE_EVENT = 'esatto-a11y-navigate';

/** Asks the dashboard to switch tab (optionally with an Issues filter). */
export function navigate(from: HTMLElement, tab: TabId, filter?: IssueFilter): void {
  from.dispatchEvent(new CustomEvent<NavigateDetail>(NAVIGATE_EVENT, { detail: { tab, filter }, bubbles: true, composed: true }));
}
