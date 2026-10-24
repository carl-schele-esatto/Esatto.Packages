export interface Focusable {
  focus(): void;
}

/**
 * What to focus to put keyboard focus on a tab. uui-tab doesn't delegate focus: its focusable
 * control is the inner #button (an <a> or <button>), so focusing the host does nothing.
 */
export function focusTargetOf(tab: Focusable & { shadowRoot?: { querySelector(selector: string): unknown } | null }): Focusable {
  return (tab.shadowRoot?.querySelector('#button') as Focusable | null) ?? tab;
}
