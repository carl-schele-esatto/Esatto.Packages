// Pure checks on an event's composed path (innermost first), deciding whether a
// key belongs to keyboard grab & place at all.

import { isTreeItem, readEntityType } from './tree-item';

type PathNode = { tagName?: string; id?: string; isContentEditable?: boolean };

// Controls rendered inside a row whose own Space/Enter must keep working.
const ROW_CONTROL_TAGS = new Set([
  'UMB-ENTITY-ACTIONS-BUNDLE',  // "…" button and the row's first action
  'UMB-ENTITY-ACTIONS-DROPDOWN', // the open actions menu
  'UUI-ACTION-BAR',
  'UUI-BUTTON',                 // e.g. "Load more"
  'INPUT',
  'TEXTAREA',
  'SELECT',
]);

function isRowControl(node: PathNode): boolean {
  return (node.tagName !== undefined && ROW_CONTROL_TAGS.has(node.tagName))
    || node.id === 'caret-button'
    || node.isContentEditable === true;
}

// Space grabs only when it lands on the row itself (its label), not on a
// control inside it: the expand caret, the entity actions, a button, or
// anything editable. Only the part of the path inside `row` is inspected.
export function isGrabStartPath(path: readonly unknown[], row: unknown): boolean {
  for (const node of path) {
    if (node === row) return true;
    if (isRowControl(node as PathNode)) return false;
  }
  return false;
}

// While grabbed, only keys pressed inside the grabbed tree belong to the grab.
// Anything else (a translation textarea, a modal) is left alone, and ends the grab.
export function isInsideTree(path: readonly unknown[], entityType: string): boolean {
  return path.some((node) => isTreeItem(node) && readEntityType(node) === entityType);
}
