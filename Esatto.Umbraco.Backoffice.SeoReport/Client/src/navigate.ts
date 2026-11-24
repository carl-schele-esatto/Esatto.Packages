// Asking the dashboard to switch tab. A child element (the not-connected card, a finding that points at another tab) cannot reach the shell
// directly, so it dispatches an event that bubbles up through the shadow roots to it, as SEO Health's tabs do.

export type TabId = 'analysis' | 'traffic' | 'queries' | 'topPages' | 'toFix' | 'comparison' | 'settings';

/** Every tab, in the order of the tab strip. Which of them a user sees is decided by view-state.ts. */
export const TAB_IDS: readonly TabId[] = ['analysis', 'traffic', 'queries', 'topPages', 'toFix', 'comparison', 'settings'];

/** The key of each tab's label in en.ts. */
export const TAB_LABEL_KEYS: Record<TabId, string> = {
  analysis: 'uiTabAnalysisLabel',
  traffic: 'uiTabTrafficLabel',
  queries: 'uiTabQueriesLabel',
  topPages: 'uiTabTopPagesLabel',
  toFix: 'uiTabToFixLabel',
  comparison: 'uiTabComparisonLabel',
  settings: 'uiTabSettingsLabel',
};

export interface NavigateDetail {
  tab: TabId;
}

export const NAVIGATE_EVENT = 'esatto-seo-report-navigate';

/** Asks the dashboard to switch to a tab. */
export function navigate(from: EventTarget, tab: TabId): void {
  from.dispatchEvent(new CustomEvent<NavigateDetail>(NAVIGATE_EVENT, { detail: { tab }, bubbles: true, composed: true }));
}
