// Drop-zone geometry: which third of a row the cursor is over.

import { adapterOf } from './tree-item';

export type DropZone = 'before' | 'into' | 'after';

// Generic kind-element Bellissima uses when a tree doesn't specify its own
// (Dictionary does exactly this). Always tried after the adapter's own tag.
const DEFAULT_INNER_TAG = 'umb-default-tree-item';

export function getRowRect(el: Element): DOMRect {
  // The wrapper's bounding rect includes nested descendants when expanded,
  // skewing the third-of-height calc. Drill in to find the visible row strip.
  //
  // 17.0.0 structure: <umb-tree-item> has the inner kind-element as a
  // LIGHT-DOM child; that has its own shadow with a <uui-menu-item>;
  // uui-menu-item's shadow contains the row body.
  const tags = [...new Set([adapterOf(el)?.innerTag, DEFAULT_INNER_TAG].filter((t): t is string => !!t))];
  const inner = el.querySelector(tags.map((t) => `:scope > ${t}`).join(', '))
    ?? el.shadowRoot?.querySelector(tags.join(', '));
  const menuItem = inner?.shadowRoot?.querySelector('uui-menu-item');
  const rowBody = menuItem?.shadowRoot?.querySelector('#menu-item, [id="menu-item"], button, .menu-item-body');
  if (rowBody) return rowBody.getBoundingClientRect();
  if (menuItem) {
    const r = menuItem.getBoundingClientRect();
    return new DOMRect(r.left, r.top, r.width, Math.min(r.height, 32));
  }
  const r = el.getBoundingClientRect();
  return new DOMRect(r.left, r.top, r.width, Math.min(r.height, 32));
}

// Pure: classify the cursor position within a row into one of the three zones.
// Top third → 'before', bottom third → 'after', middle → 'into'.
export function computeDropZone(rectTop: number, rectHeight: number, clientY: number): DropZone {
  const offset = clientY - rectTop;
  const third = rectHeight / 3;
  if (offset < third) return 'before';
  if (offset > rectHeight - third) return 'after';
  return 'into';
}

export function getDropZone(targetEl: Element, clientY: number): DropZone {
  const rect = getRowRect(targetEl);
  return computeDropZone(rect.top, rect.height, clientY);
}
