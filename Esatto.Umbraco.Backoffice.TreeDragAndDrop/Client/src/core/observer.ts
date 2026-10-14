// Shadow-root observation.
//
// Bellissima nests every UI element in shadow DOM, so plain
// document.querySelectorAll never sees the tree. installObserver() patches
// Element.prototype.attachShadow to capture every shadow root created from
// then on (open OR closed) and observes each for tree-item insertions.
//
// Unlike the 2.0 content script this does not run at module load: the first
// registerTree() call installs it, so a page with every tree disabled never
// loads this module at all.

import { walkDescendantsComposed } from './dom';
import { isTreeItem, propagateDraggable, findEnclosingTreeItemComposed } from './tree-item';
import { LOG_PREFIX, PATCH_FLAG } from './constants';

// The host (the Lit element) implements this; observer only needs attachItem.
export interface DndHost {
  attachItem(el: Element): void;
}

// Weak on purpose: the script now loads for every backoffice user at login, and
// a strong list of every shadow root ever created (every uui-button, every
// workspace navigated away from) would grow for the whole session.
const observedRoots = new WeakSet<object>();

let host: DndHost | null = null;

export function setHost(h: DndHost | null): void {
  host = h;
  if (h) resweep();
}

// Re-walk the live document. Called when the host mounts, and when a tree
// registers after rows of that tree are already rendered (isTreeItem only
// matches rows whose adapter is registered). Bellissima's shadow roots are
// open, so walkDescendantsComposed reaches every connected row from here.
export function resweep(): void {
  sweepSubtree(document);
}

function sweepSubtree(root: Node | null): void {
  if (!root) return;
  const start: Node = root.nodeType === 9 ? (root as Document).documentElement : root;
  walkDescendantsComposed(start, (el) => {
    if (isTreeItem(el)) {
      host?.attachItem(el);
      // Re-run draggable propagation on every sweep — Lit re-renders the
      // wrapper's shadow tree async, so the inner kind-element / uui-menu-item
      // may not have existed at first attachItem. propagateDraggable is
      // idempotent (setAttribute on already-set attribute is a no-op).
      propagateDraggable(el);
    }
    if (el.shadowRoot && !observedRoots.has(el.shadowRoot)) {
      observeRoot(el.shadowRoot);
    }
  });
}

function observeRoot(root: Document | ShadowRoot | null): void {
  if (!root || observedRoots.has(root)) return;
  observedRoots.add(root);

  const observer = new MutationObserver((mutations) => {
    for (const m of mutations) {
      // Watch attribute changes on existing tree-items in case props/entity-type
      // gets bound after initial mount.
      if (m.type === 'attributes' && isTreeItem(m.target)) {
        host?.attachItem(m.target);
        continue;
      }
      for (const node of m.addedNodes) {
        if (!(node instanceof Element)) continue;
        if (isTreeItem(node)) host?.attachItem(node);
        sweepSubtree(node);
        // If the added node lives inside an existing attached wrapper's
        // shadow tree, re-propagate draggable on that wrapper. Lit renders
        // the inner kind-element + uui-menu-item async, so elements that
        // need draggable=true frequently appear AFTER the wrapper's initial
        // attachItem.
        // attachItem too: isTreeItem needs props.isMenu, which may only be
        // readable once the row starts rendering its inner element.
        const enclosing = findEnclosingTreeItemComposed(node);
        if (enclosing) {
          host?.attachItem(enclosing);
          propagateDraggable(enclosing);
        }
      }
    }
  });
  observer.observe(root, {
    childList: true,
    subtree: true,
    attributes: true,
    // Lit reflects `entityType` to lowercase `entitytype` (no hyphen) in 17.0.0.
    // Watch both forms in case of version drift.
    attributeFilter: ['entitytype', 'entity-type'],
  });

  sweepSubtree(root);
}

// (1) Patch attachShadow. Captures every shadow root from here on, open OR
// closed (returned reference is the same regardless of {mode}).
// (2) Begin observing the document and walk what already exists.
//
// Cross-package note: the legacy ContentTreeDragAndDrop / MediaTreeDragAndDrop
// packages patch attachShadow too. Each wrapper calls through to the previous
// one, so the chain is benign in any load order.
export function installObserver(): void {
  const flags = window as unknown as Record<string, boolean>;
  if (flags[PATCH_FLAG]) return;
  flags[PATCH_FLAG] = true;

  const origAttachShadow = Element.prototype.attachShadow;
  Element.prototype.attachShadow = function (this: Element, opts: ShadowRootInit): ShadowRoot {
    const root = origAttachShadow.call(this, opts);
    try { observeRoot(root); }
    catch (err) { console.warn(`${LOG_PREFIX} observeRoot failed:`, err); }
    return root;
  };

  observeRoot(document);
}
