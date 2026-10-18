// Show a DropPlan on screen.
//
// Sorted trees (Content, Media): a line above/below the target where the item
// will land, or the target outlined for a drop "into" it.
// Unsorted trees (Dictionary): an edge drop only picks a parent, so outline the
// destination parent's row — or for the root draw a line above the first
// top-level row (the dictionary tree hides its root row). If the row that
// stands for the destination isn't rendered, hide rather than point somewhere
// misleading.

import type { DropPlan } from './drop-plan';
import type { DropZone } from './drop-zone';
import type { TreeAdapter } from './types';
import { findTreeItemByUnique, listTreeItemsInOrder, readParentUnique } from './tree-item';
import { renderIndicator, hideIndicator } from './indicator';

export function showPlan(plan: DropPlan, adapter: TreeAdapter, targetEl: Element, zone: DropZone): void {
  if (plan.kind === 'noop') {
    hideIndicator();
    return;
  }
  if (plan.kind === 'reorder' || plan.kind === 'move+sort') {
    renderIndicator(targetEl, zone === 'before' ? 'line-above' : 'line-below');
    return;
  }
  if (zone === 'into' || adapter.sort) {
    renderIndicator(targetEl, 'outline');
    return;
  }
  if (plan.parentUnique !== null) {
    const parentEl = findTreeItemByUnique(plan.parentUnique, adapter.entityType);
    if (parentEl) renderIndicator(parentEl, 'outline');
    else hideIndicator();
    return;
  }
  const firstTopLevel = listTreeItemsInOrder(adapter.entityType).find((el) => readParentUnique(el) === null);
  if (firstTopLevel) renderIndicator(firstTopLevel, 'line-above');
  else hideIndicator();
}
