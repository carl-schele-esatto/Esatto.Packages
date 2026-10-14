// Tree-item identity and best-effort reads of Bellissima's (untyped) internals.
// A row is "ours" when its entity type has a registered TreeAdapter.

import { composedParent, walkDescendantsComposed } from './dom';
import { adapterForEntityType } from './adapters';
import type { TreeAdapter } from './types';

// Bellissima tree-item wrappers expose their data via these properties. The
// shape is NOT part of any public contract — we read it best-effort. Modelled
// as an intersection with Element so the helpers stay DOM-typed.
export type AnyTreeItem = Element & {
  props?: { item?: { unique?: string | null; name?: string; parent?: { unique?: string | null } } };
  api?: { unique?: string | null };
  _item?: { unique?: string | null };
  toggleChildren?: () => void;
};

export function readEntityType(el: Element): string | null {
  // Lit's default @property() reflection lowercases the property name without
  // inserting hyphens, so `entityType` reflects to `entitytype`. Some
  // versions / explicit annotations may use kebab-case `entity-type`. Match
  // both. The kebab variant doesn't exist in 17.0.0 and was the bug that
  // made an early version of the content script find zero tree items.
  return el.getAttribute('entitytype') ?? el.getAttribute('entity-type');
}

export function isTreeItem(el: unknown): el is AnyTreeItem {
  const node = el as Element | null;
  if (!node || node.nodeType !== 1) return false;
  if (node.tagName !== 'UMB-TREE-ITEM') return false;
  return adapterForEntityType(readEntityType(node)) !== undefined;
}

export function adapterOf(el: Element): TreeAdapter | undefined {
  return adapterForEntityType(readEntityType(el));
}

export function readUnique(el: AnyTreeItem): string | null {
  // Primary: el.props.item.unique. The wrapper's `props` is the props
  // object passed to the inner kind-element; `item` is the nested tree-item
  // descriptor that holds .unique, .name, .entityType, .parent.
  const u = el.props?.item?.unique
    ?? el.api?.unique
    ?? el._item?.unique
    ?? el.getAttribute?.('data-unique');
  return u ?? null;
}

export function readParentUnique(el: AnyTreeItem): string | null {
  // Top-level items have parent.unique === null (entityType: '<tree>-root').
  const parent = el.props?.item?.parent;
  if (parent !== undefined) {
    return parent.unique ?? null;
  }
  // Fallback: walk up the composed tree.
  let cur = composedParent(el);
  while (cur) {
    if (isTreeItem(cur)) return readUnique(cur);
    cur = composedParent(cur);
  }
  return null;
}

export function readName(el: AnyTreeItem | null | undefined): string {
  return el?.props?.item?.name ?? 'item';
}

// Walk the wrapper's nested shadow + light DOM and set draggable=true on
// every element descendant. The visible row is composed from many nested
// elements (kind-element → uui-menu-item → its shadow row markup with
// slots, anchors, buttons, etc.). Without draggable=true ALL the way down,
// pointerdown over the label area lands on a non-draggable inner element
// and the browser's drag heuristic doesn't always ascend cleanly past it —
// resulting in "drag only works from the empty left margin" symptom.
//
// Sledgehammer is the right tool: draggable on a non-row element is
// harmless (events still bubble to the same handlers via composed path);
// draggable missing is the failure mode we want to avoid.
export function propagateDraggable(wrapperEl: Element): void {
  function visit(node: Element): void {
    if (!node || node.nodeType !== 1) return;
    if (node.tagName !== 'SLOT') {
      try { node.setAttribute('draggable', 'true'); } catch { /* readonly host attribute */ }
    }
    for (const c of node.children) visit(c);
    if (node.shadowRoot) {
      for (const c of node.shadowRoot.children) visit(c);
    }
  }
  // <umb-tree-item> in 17.0.0 doesn't use shadow DOM — it renders the inner
  // kind-element as a LIGHT-DOM child. Start from light children first; fall
  // through to shadow if a future Bellissima version puts it there.
  for (const c of wrapperEl.children) visit(c);
  if (wrapperEl.shadowRoot) {
    for (const c of wrapperEl.shadowRoot.children) visit(c);
  }
}

export function findEnclosingTreeItemComposed(itemEl: Element | null): AnyTreeItem | null {
  // Returns the tree-item wrapper that ENCLOSES itemEl, walking up through
  // shadow boundaries. Includes itemEl itself if it is one.
  let cur: Element | null = itemEl;
  while (cur) {
    if (isTreeItem(cur)) return cur;
    cur = composedParent(cur);
  }
  return null;
}

export function findTreeItemByUnique(unique: string, entityType: string): AnyTreeItem | null {
  let found: AnyTreeItem | null = null;
  walkDescendantsComposed(document.documentElement, (el) => {
    if (found) return;
    if (isTreeItem(el) && readEntityType(el) === entityType && readUnique(el) === unique) found = el;
  });
  return found;
}

export function listTreeItemsInOrder(entityType: string): AnyTreeItem[] {
  // All rendered rows of one tree in composed DOM order, which matches their
  // visual top-to-bottom order. Used by keyboard grab & place and to find the
  // first top-level row for the "move to root" indicator.
  const out: AnyTreeItem[] = [];
  walkDescendantsComposed(document.documentElement, (el) => {
    if (isTreeItem(el) && readEntityType(el) === entityType) out.push(el);
  });
  return out;
}
