// Show a DropPlan on screen: outline the destination parent's row, or for the
// root draw a line above the first top-level row (the dictionary tree hides
// its root row, so there is nothing else to point at). If the row that stands
// for the destination isn't rendered (scrolled away / collapsed), hide rather
// than point somewhere misleading.

import type { DropPlan } from './drop-plan';
import type { TreeAdapter } from './types';
import { findTreeItemByUnique, listTreeItemsInOrder, readParentUnique } from './tree-item';
import { renderIndicator, hideIndicator } from './indicator';

export function showPlan(plan: DropPlan, adapter: TreeAdapter): void {
  if (plan.kind === 'noop') {
    hideIndicator();
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
