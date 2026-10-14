// Optimistic DOM reorder for same-parent drops: move the source row next to
// the target before the (slow — ~8 s for ~26 children locally) sort API
// returns, and roll back if it fails. Cross-parent moves can't do this:
// Bellissima's chevron and indentation come from the target's hasChildren in
// its data store, so those reload instead.

import type { DropZone } from './drop-zone';

export function optimisticReorder(sourceEl: Element | null, targetEl: Element | null, zone: DropZone): void {
  if (!sourceEl || !targetEl || sourceEl === targetEl) return;
  const parent = targetEl.parentNode;
  if (!parent) return;
  parent.insertBefore(sourceEl, zone === 'before' ? targetEl : targetEl.nextSibling);
}
