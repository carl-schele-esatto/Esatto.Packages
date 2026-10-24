export interface Draggable {
  draggable: boolean;
}

/**
 * Lets a press anywhere on a tab start a drag. Left alone, Chromium drags the tab's inner link, and
 * drops that drag when the press lands on label text slotted in from outside the link (section tabs'
 * labels are; header tabs' aren't). Dragging the tab element itself works wherever the press lands.
 */
export function makeTabDraggable(tab: Draggable & { shadowRoot?: { querySelector(selector: string): unknown } | null }): void {
  tab.draggable = true;
  const inner = tab.shadowRoot?.querySelector('#button') as Draggable | null | undefined;
  if (inner) inner.draggable = false;
}
