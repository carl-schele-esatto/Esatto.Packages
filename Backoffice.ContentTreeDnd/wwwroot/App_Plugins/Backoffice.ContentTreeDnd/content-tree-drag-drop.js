// Backoffice.ContentTreeDnd — Content tree drag-and-drop for Umbraco 17.
//
// Adds native HTML5 drag-and-drop to the Umbraco 17 backoffice Content tree.
// Supports reorder (within one parent) and reparent (drop onto a different
// parent), with three-zone drop targets per row: above sibling / into as
// child / below sibling.
//
// Architecture:
//
//   Bellissima nests every UI element in shadow DOM, so plain
//   document.querySelectorAll never sees the tree. The shim patches
//   Element.prototype.attachShadow at module load time to capture every
//   shadow root created from then on (open OR closed) and observes each
//   for tree-item element insertions.
//
//   A single hidden <backoffice-content-tree-dnd> Lit element on document.body
//   acts as the controller host — it consumes UMB_NOTIFICATION_CONTEXT and
//   UMB_ACTION_EVENT_CONTEXT, owns the drag state, and runs the API calls
//   via DocumentService (which auto-attaches the bearer token).
//
//   Observation runs at module level so it works even before the Lit host
//   is constructed; when the host mounts it registers itself and re-sweeps
//   already-captured roots to attach handlers retroactively.

import { LitElement } from '@umbraco-cms/backoffice/external/lit';
import { UmbElementMixin } from '@umbraco-cms/backoffice/element-api';
import { UMB_NOTIFICATION_CONTEXT } from '@umbraco-cms/backoffice/notification';
import { UMB_ACTION_EVENT_CONTEXT } from '@umbraco-cms/backoffice/action';
import { UmbRequestReloadChildrenOfEntityEvent } from '@umbraco-cms/backoffice/entity-action';
import { DocumentService } from '@umbraco-cms/backoffice/external/backend-api';
import { tryExecute } from '@umbraco-cms/backoffice/resources';

const ATTACHED_FLAG = 'backofficeContentTreeDndAttached';
const HOVER_EXPAND_MS = 700;
const ENTITY_TYPE_DOCUMENT = 'document';
const DRAG_MIME = 'application/x-backoffice-content-tree-dnd';

// --- Tree-item identity ------------------------------------------------

function isTreeItem(el) {
  if (!el || el.nodeType !== 1) return false;
  if (el.tagName !== 'UMB-TREE-ITEM') return false;
  // Lit's default @property() reflection lowercases the property name without
  // inserting hyphens, so `entityType` reflects to `entitytype`. Some
  // versions / explicit annotations may use kebab-case `entity-type`. Match
  // both. The kebab variant doesn't exist in 17.0.0 and was the bug that
  // made an early version of this script find zero tree items.
  const et = el.getAttribute('entitytype') ?? el.getAttribute('entity-type');
  return et === ENTITY_TYPE_DOCUMENT;
}

function readUnique(el) {
  // Primary: el.props.item.unique. The wrapper's `props` is the props
  // object passed to the inner kind-element; `item` is the nested tree-item
  // descriptor that holds .unique, .name, .entityType, .parent.
  const u = el.props?.item?.unique
    ?? el.api?.unique
    ?? el._item?.unique
    ?? el.getAttribute?.('data-unique');
  return u ?? null;
}

function readParentUnique(el) {
  // Top-level items have parent.unique === null (entityType: 'document-root').
  // Property is `parent.unique`, regardless of whether parent is null or a guid.
  if (el.props?.item?.parent !== undefined) {
    return el.props.item.parent.unique ?? null;
  }
  // Fallback: walk up the composed tree.
  let cur = composedParent(el);
  while (cur) {
    if (isTreeItem(cur)) return readUnique(cur);
    cur = composedParent(cur);
  }
  return null;
}

function composedParent(el) {
  // Walk through shadow boundaries: an element's host's parent is its
  // composed parent.
  if (!el) return null;
  if (el.parentElement) return el.parentElement;
  const root = el.getRootNode?.();
  if (root && root.host) return root.host;
  return null;
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
function propagateDraggable(wrapperEl) {
  function visit(node) {
    if (!node || node.nodeType !== 1) return;
    if (node.tagName !== 'SLOT') {
      try { node.setAttribute('draggable', 'true'); } catch { /* readonly host attribute */ }
    }
    for (const c of node.children ?? []) visit(c);
    if (node.shadowRoot) {
      for (const c of node.shadowRoot.children) visit(c);
    }
  }
  // <umb-tree-item> in 17.0.0 doesn't use shadow DOM — it renders the inner
  // <umb-document-tree-item> as a LIGHT-DOM child. Start from light children
  // first; fall through to shadow if a future Bellissima version puts it there.
  for (const c of wrapperEl.children ?? []) visit(c);
  if (wrapperEl.shadowRoot) {
    for (const c of wrapperEl.shadowRoot.children) visit(c);
  }
}

// --- Drop-zone geometry -------------------------------------------------

function getRowRect(el) {
  // The wrapper's bounding rect includes nested descendants when expanded,
  // skewing the third-of-height calc. Drill in to find the visible row strip.
  //
  // 17.0.0 structure: <umb-tree-item> has <umb-document-tree-item> as a
  // LIGHT-DOM child; that has its own shadow with a <uui-menu-item>;
  // uui-menu-item's shadow contains the row body.
  const inner = el.querySelector(':scope > umb-document-tree-item, :scope > umb-default-tree-item')
    ?? el.shadowRoot?.querySelector('umb-document-tree-item, umb-default-tree-item');
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

function computeDropZone(targetEl, clientY) {
  const rect = getRowRect(targetEl);
  const offset = clientY - rect.top;
  const third = rect.height / 3;
  if (offset < third) return 'before';
  if (offset > rect.height - third) return 'after';
  return 'into';
}

// --- Cycle guard --------------------------------------------------------

function collectDescendantUniques(rootEl) {
  // Walk descendants in BOTH light DOM and shadow DOM. Tree-items in
  // currently-collapsed branches aren't rendered into the DOM, so a cycle
  // through them won't be caught here — that's a known limitation. Server
  // is the authoritative check.
  const out = new Set();
  walkDescendantsComposed(rootEl, (el) => {
    if (el !== rootEl && isTreeItem(el)) {
      const u = readUnique(el);
      if (u) out.add(u);
    }
  });
  return out;
}

function walkDescendantsComposed(root, visit) {
  if (!root || root.nodeType !== 1) return;
  visit(root);
  for (const child of root.children ?? []) {
    walkDescendantsComposed(child, visit);
  }
  if (root.shadowRoot) {
    for (const child of root.shadowRoot.children) {
      walkDescendantsComposed(child, visit);
    }
  }
}

// --- Sibling listing ----------------------------------------------------

function listSiblingsInOrder(parentTreeItem) {
  // Returns the tree-children of parentTreeItem (or the tree root, if
  // parentTreeItem is null) in their current visual order.
  // Children may live anywhere inside the parent's composed tree (shadow
  // roots etc.), filtered by parent-unique match.
  const parentUnique = parentTreeItem ? readUnique(parentTreeItem) : null;
  const out = [];
  const rootToWalk = parentTreeItem ?? document;
  walkDescendantsComposed(rootToWalk, (el) => {
    if (el !== parentTreeItem && isTreeItem(el)) {
      const itemParent = readParentUnique(el);
      if (itemParent === parentUnique) {
        const u = readUnique(el);
        if (u) out.push(u);
      }
    }
  });
  return out;
}

function findVisualParentTreeItem(itemEl) {
  // Returns the enclosing tree-item element (if at depth ≥ 1) or null (if
  // the item is at tree root).
  let cur = composedParent(itemEl);
  while (cur) {
    if (isTreeItem(cur)) return cur;
    cur = composedParent(cur);
  }
  return null;
}

function findEnclosingTreeItemComposed(itemEl) {
  // Returns the tree-item wrapper that ENCLOSES itemEl, walking up through
  // shadow boundaries. Includes itemEl itself if it is one.
  let cur = itemEl;
  while (cur) {
    if (isTreeItem(cur)) return cur;
    cur = composedParent(cur);
  }
  return null;
}

function findTreeItemByUnique(unique) {
  // Walk the composed tree (across shadow boundaries) looking for the
  // wrapper whose props.item.unique matches.
  let found = null;
  walkDescendantsComposed(document.documentElement, (el) => {
    if (found) return;
    if (isTreeItem(el) && readUnique(el) === unique) found = el;
  });
  return found;
}

// --- Optimistic DOM mutation -------------------------------------------
// For SAME-PARENT REORDER we move the source wrapper directly in the DOM
// rather than dispatching a Bellissima reload event. Reload rebuilds the
// entire affected branch (slow, flashes the whole tree). Direct DOM
// re-ordering is instant and visually identical.
//
// Cross-parent moves (into / cross-parent slot) cannot use optimistic DOM
// because Bellissima's expand-chevron and child indentation are driven by
// `props.item.hasChildren` in its data store — DOM mutation alone doesn't
// change that. Those cases dispatch reload events instead.

function optimisticReorder(sourceEl, targetEl, zone) {
  if (!sourceEl || !targetEl) return;
  if (sourceEl === targetEl) return;
  const parent = targetEl.parentNode;
  if (!parent) return;
  if (zone === 'before') {
    parent.insertBefore(sourceEl, targetEl);
  } else {
    parent.insertBefore(sourceEl, targetEl.nextSibling);
  }
}

// --- Drop indicator -----------------------------------------------------

const indicator = (() => {
  const el = document.createElement('div');
  el.id = 'backoffice-content-tree-dnd-indicator';
  el.style.cssText = `
    position: fixed;
    pointer-events: none;
    z-index: 99999;
    display: none;
    box-sizing: border-box;
  `;
  document.body.appendChild(el);
  return el;
})();

function renderIndicator(targetEl, zone) {
  const rect = getRowRect(targetEl);
  const accent = 'var(--uui-color-selected, #3879ff)';
  const tint = 'rgba(56, 121, 255, 0.08)';
  const common = `
    position: fixed;
    pointer-events: none;
    z-index: 99999;
    display: block;
    box-sizing: border-box;
    left: ${rect.left}px;
    width: ${rect.width}px;
  `;
  if (zone === 'before') {
    indicator.style.cssText = `${common}
      top: ${rect.top - 1}px;
      height: 2px;
      background: ${accent};
    `;
  } else if (zone === 'after') {
    indicator.style.cssText = `${common}
      top: ${rect.bottom - 1}px;
      height: 2px;
      background: ${accent};
    `;
  } else {
    indicator.style.cssText = `${common}
      top: ${rect.top}px;
      height: ${rect.height}px;
      border: 2px solid ${accent};
      background: ${tint};
    `;
  }
}

function hideIndicator() {
  indicator.style.display = 'none';
}

// --- Module-level shadow-root observation ------------------------------

const observedRoots = new WeakSet();
const knownRoots = []; // includes document; for re-sweep when host mounts

// Singleton host registered by the Lit element on connectedCallback. When
// the observer finds a tree-item before the host is up, it queues; when the
// host registers, it re-sweeps known roots.
let host = null;

function setHost(h) {
  host = h;
  if (h) {
    // Re-sweep all known roots so already-rendered tree items get bound.
    for (const root of knownRoots) {
      sweepSubtree(root);
    }
  }
}

function sweepSubtree(root) {
  if (!root) return;
  walkDescendantsComposed(root.nodeType === 9 ? root.documentElement : root, (el) => {
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

function observeRoot(root) {
  if (!root || observedRoots.has(root)) return;
  observedRoots.add(root);
  knownRoots.push(root);

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
        const enclosing = findEnclosingTreeItemComposed(node);
        if (enclosing) propagateDraggable(enclosing);
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

// Guard against re-evaluation. If anything ever causes a backofficeEntryPoint
// to load twice (Bellissima remount, auth refresh, manifest re-eval), every
// additional load would add another wrapper layer onto attachShadow — every
// shadow root would then get N MutationObservers.
const PATCH_FLAG = '__backofficeContentTreeDndPatched';
if (!window[PATCH_FLAG]) {
  window[PATCH_FLAG] = true;

  // (1) Patch attachShadow at module load. Captures every shadow root from
  // here on, open OR closed (returned reference is the same regardless of
  // {mode}).
  const origAttachShadow = Element.prototype.attachShadow;
  Element.prototype.attachShadow = function (opts) {
    const root = origAttachShadow.call(this, opts);
    try { observeRoot(root); }
    catch (err) { console.warn('[backoffice-content-tree-dnd] observeRoot failed:', err); }
    return root;
  };

  // (2) Begin observing the document. Existing open shadow roots created
  // before this script loaded are unreachable (closed) or already-observed
  // (open, attached as descendants), so we walk now to catch them.
  observeRoot(document);
}

// --- Lit element host --------------------------------------------------

class BackofficeContentTreeDnd extends UmbElementMixin(LitElement) {
  #notifications;
  #actionEvents;
  #dragState = null;
  #hoverTimer = null;
  #lastHoverTarget = null;

  constructor() {
    super();
    this.consumeContext(UMB_NOTIFICATION_CONTEXT, (ctx) => { this.#notifications = ctx; });
    this.consumeContext(UMB_ACTION_EVENT_CONTEXT, (ctx) => { this.#actionEvents = ctx; });
  }

  #globalListenersInstalled = false;

  connectedCallback() {
    super.connectedCallback();
    setHost(this);
    this.#installGlobalListeners();
  }

  disconnectedCallback() {
    super.disconnectedCallback();
    setHost(null);
    if (this.#hoverTimer) clearTimeout(this.#hoverTimer);
  }

  attachItem(el) {
    // Listeners are installed at document level (see #installGlobalListeners),
    // capture phase, so we can't be defeated by stopPropagation upstream.
    // attachItem now only manages the visual state of the wrapper and its
    // shadow descendants — sets draggable=true so drag can initiate.
    if (el.dataset[ATTACHED_FLAG]) return;
    el.dataset[ATTACHED_FLAG] = '1';
    el.setAttribute('draggable', 'true');
    propagateDraggable(el);

    // Lit renders the inner <umb-document-tree-item> + uui-menu-item async,
    // so propagateDraggable above may run before the row markup exists. Watch
    // the wrapper's subtree and re-propagate on any change. Idempotent —
    // setAttribute on already-set attribute is a no-op.
    const obs = new MutationObserver(() => propagateDraggable(el));
    obs.observe(el, { childList: true, subtree: true });
  }

  #installGlobalListeners() {
    if (this.#globalListenersInstalled) return;
    this.#globalListenersInstalled = true;
    // Capture phase (third arg = true) ensures we run before any descendant
    // listener can stopPropagation. Bellissima / UUI dispatches its own drag
    // logic in some places (block lists, sorter) that swallows bubble-phase
    // events; capture is the only reliable surface for tree DnD.
    document.addEventListener('dragstart', (e) => {
      const el = this.#findTreeItemInPath(e);
      if (!el) return;
      this.#onDragStart(e, el);
    }, true);
    document.addEventListener('dragenter', (e) => {
      const el = this.#findTreeItemInPath(e);
      if (!el || !this.#dragState) return;
      // preventDefault on dragenter to mark the element as a valid drop target.
      e.preventDefault();
    }, true);
    document.addEventListener('dragover', (e) => {
      const el = this.#findTreeItemInPath(e);
      if (!el) {
        hideIndicator();
        return;
      }
      this.#onDragOver(e, el);
    }, true);
    document.addEventListener('dragleave', (e) => {
      const el = this.#findTreeItemInPath(e);
      if (!el) return;
      this.#onDragLeave(e, el);
    }, true);
    document.addEventListener('drop', (e) => {
      const el = this.#findTreeItemInPath(e);
      if (!el) return;
      this.#onDrop(e, el);
    }, true);
    document.addEventListener('dragend', () => this.#onDragEnd(), true);
  }

  #findTreeItemInPath(event) {
    // Walk the composed path looking for the closest tree-item wrapper. The
    // browser may retarget event.target to the shadow host (umb-app); the
    // composedPath gives us the full path through shadow boundaries.
    const path = event.composedPath?.() ?? [];
    for (const node of path) {
      if (isTreeItem(node)) return node;
    }
    return null;
  }

  #onDragStart(event, el) {
    const sourceUnique = readUnique(el);
    if (!sourceUnique) {
      event.preventDefault();
      return;
    }
    const sourceParentUnique = readParentUnique(el);
    const descendantUniques = collectDescendantUniques(el);

    event.dataTransfer.effectAllowed = 'move';
    event.dataTransfer.setData(DRAG_MIME, sourceUnique);

    this.#dragState = { sourceUnique, sourceParentUnique, descendantUniques };
  }

  #onDragOver(event, el) {
    if (!this.#dragState) return;
    const targetUnique = readUnique(el);
    if (!targetUnique) return;

    if (targetUnique === this.#dragState.sourceUnique
        || this.#dragState.descendantUniques.has(targetUnique)) {
      hideIndicator();
      return;
    }

    const zone = computeDropZone(el, event.clientY);
    event.preventDefault();
    event.dataTransfer.dropEffect = 'move';
    renderIndicator(el, zone);

    if (this.#lastHoverTarget !== el) {
      if (this.#hoverTimer) clearTimeout(this.#hoverTimer);
      this.#lastHoverTarget = el;
      this.#hoverTimer = setTimeout(() => {
        if (!el.hasAttribute('show-children')
            && !el.hasAttribute('is-expanded')
            && !el.hasAttribute('open')) {
          if (typeof el.toggleChildren === 'function') {
            el.toggleChildren();
          } else {
            const chevron = el.shadowRoot?.querySelector('[name="caret"], [data-mark="chevron"]');
            chevron?.click?.();
          }
        }
      }, HOVER_EXPAND_MS);
    }
  }

  #onDragLeave(event, el) {
    if (event.relatedTarget && el.contains(event.relatedTarget)) return;
    hideIndicator();
    if (this.#lastHoverTarget === el && this.#hoverTimer) {
      clearTimeout(this.#hoverTimer);
      this.#hoverTimer = null;
      this.#lastHoverTarget = null;
    }
  }

  async #onDrop(event, el) {
    if (!this.#dragState) return;
    const targetUnique = readUnique(el);
    if (!targetUnique) return;
    if (targetUnique === this.#dragState.sourceUnique
        || this.#dragState.descendantUniques.has(targetUnique)) return;

    const zone = computeDropZone(el, event.clientY);
    event.preventDefault();
    hideIndicator();

    const sourceUnique = this.#dragState.sourceUnique;
    const sourceParentUnique = this.#dragState.sourceParentUnique;
    const targetParentUnique = readParentUnique(el);

    // Find the source DOM element so we can do an optimistic move on success.
    const sourceEl = findTreeItemByUnique(sourceUnique);

    // Visual feedback: dim the source while the API call is in flight.
    if (sourceEl) sourceEl.style.opacity = '0.4';

    try {
      if (zone === 'into') {
        if (sourceParentUnique === targetUnique) return;
        await this.#move(sourceUnique, targetUnique);
        // Cross-parent reparent into a target: optimistic DOM doesn't work
        // here because Bellissima's expand-chevron and indentation are
        // driven by the target's `props.item.hasChildren` in its data store
        // — DOM mutation alone doesn't change that. Fall back to reload
        // events so the data store updates and the chevron appears.
        await this.#reload(sourceParentUnique);
        await this.#reload(targetUnique);
        return;
      }

      if (sourceParentUnique === targetParentUnique) {
        const parentEl = findVisualParentTreeItem(el);
        const siblings = listSiblingsInOrder(parentEl).filter((u) => u !== sourceUnique);
        const targetIdx = siblings.indexOf(targetUnique);
        if (targetIdx === -1) {
          throw new Error('Target sibling not found in parent\'s rendered children');
        }
        const insertAt = zone === 'before' ? targetIdx : targetIdx + 1;
        const newOrder = [...siblings.slice(0, insertAt), sourceUnique, ...siblings.slice(insertAt)];

        // Optimistic-first: move the DOM element BEFORE awaiting the API.
        // The sort API is slow (~8s for ~26 children locally) — awaiting
        // first means the user sees a dimmed row for 8s before the move
        // visually completes. Moving first gives instant feedback; if the
        // API later fails, we restore the original position.
        const rollbackBefore = sourceEl?.nextSibling ?? null;
        const rollbackParent = sourceEl?.parentNode ?? null;
        optimisticReorder(sourceEl, el, zone);

        try {
          await this.#sort(targetParentUnique, newOrder);
        } catch (err) {
          // Roll back the optimistic move so the visible tree matches server.
          if (sourceEl && rollbackParent) {
            rollbackParent.insertBefore(sourceEl, rollbackBefore);
          }
          throw err; // outer catch shows toast
        }
        return;
      }

      // Cross-parent slot: move + sort.
      await this.#move(sourceUnique, targetParentUnique);
      const parentEl = findVisualParentTreeItem(el);
      const siblings = listSiblingsInOrder(parentEl).filter((u) => u !== sourceUnique);
      const targetIdx = siblings.indexOf(targetUnique);
      if (targetIdx === -1) {
        // Move succeeded but we can't compute the slot; reload for accuracy
        // (this is a rare edge case).
        await this.#reload(sourceParentUnique);
        await this.#reload(targetParentUnique);
        this.#showWarn(`Moved, but couldn't set the position — it's at the bottom of the new parent.`);
        return;
      }
      const insertAt = zone === 'before' ? targetIdx : targetIdx + 1;
      const newOrder = [...siblings.slice(0, insertAt), sourceUnique, ...siblings.slice(insertAt)];

      try {
        await this.#sort(targetParentUnique, newOrder);
        // Cross-parent slot: same data-store issue as `into` (target needs
        // its hasChildren updated). Reload both branches.
        await this.#reload(sourceParentUnique);
        await this.#reload(targetParentUnique);
      } catch (sortErr) {
        await this.#reload(sourceParentUnique);
        await this.#reload(targetParentUnique);
        this.#showWarn(`Moved, but couldn't set the position — it's at the bottom of the new parent. (${sortErr.message ?? sortErr})`);
      }
    } catch (err) {
      console.error('[backoffice-content-tree-dnd] drop failed', err);
      this.#showError(err.message ?? String(err));
      // On error: reload to re-sync visual state with server reality.
      await this.#reload(sourceParentUnique).catch(() => {});
      await this.#reload(targetParentUnique).catch(() => {});
    } finally {
      if (sourceEl) sourceEl.style.opacity = '';
    }
  }

  #onDragEnd() {
    this.#dragState = null;
    hideIndicator();
    if (this.#hoverTimer) clearTimeout(this.#hoverTimer);
    this.#hoverTimer = null;
    this.#lastHoverTarget = null;
  }

  async #move(unique, targetParentUnique) {
    const { error } = await tryExecute(
      this,
      DocumentService.putDocumentByIdMove({
        path: { id: unique },
        body: { target: targetParentUnique ? { id: targetParentUnique } : null },
      }),
      { disableNotifications: true },
    );
    if (error) throw error;
  }

  async #sort(parentUnique, orderedChildUniques) {
    const { error } = await tryExecute(
      this,
      DocumentService.putDocumentSort({
        body: {
          parent: parentUnique ? { id: parentUnique } : null,
          sorting: orderedChildUniques.map((id, sortOrder) => ({ id, sortOrder })),
        },
      }),
      { disableNotifications: true },
    );
    if (error) throw error;
  }

  async #reload(parentUnique) {
    if (!this.#actionEvents) {
      console.warn('[backoffice-content-tree-dnd] reload: action-event context not available — tree won\'t auto-refresh');
      return;
    }
    // The conceptual document-root has entityType 'document-root' (per the
    // tree-item probe: top-level docs report `parent: { unique: null,
    // entityType: 'document-root' }`). For child reloads dispatch with the
    // parent's actual entity type.
    const entityType = parentUnique ? ENTITY_TYPE_DOCUMENT : 'document-root';
    this.#actionEvents.dispatchEvent(
      new UmbRequestReloadChildrenOfEntityEvent({
        unique: parentUnique ?? null,
        entityType,
      }),
    );
  }

  #showError(message) {
    console.error('[backoffice-content-tree-dnd]', message);
    this.#notifications?.peek('danger', { data: { message } });
  }

  #showWarn(message) {
    console.warn('[backoffice-content-tree-dnd]', message);
    this.#notifications?.peek('warning', { data: { message } });
  }

  render() { return null; }
}

if (!customElements.get('backoffice-content-tree-dnd')) {
  customElements.define('backoffice-content-tree-dnd', BackofficeContentTreeDnd);
}

// Mount the host INSIDE <umb-app> so Lit contexts (UMB_NOTIFICATION_CONTEXT,
// UMB_ACTION_EVENT_CONTEXT) provided by ancestors of the section roots can
// propagate down to it. Mounting on document.body as a sibling of umb-app
// means consumeContext silently fails — the action-event context (needed to
// trigger tree reloads after move/sort) never resolves.
(() => {
  if (document.querySelector('backoffice-content-tree-dnd')) return;
  const host = document.createElement('backoffice-content-tree-dnd');
  host.style.display = 'none';

  function mountInto(parent) {
    if (host.parentElement !== parent) parent.appendChild(host);
  }

  const umbApp = document.querySelector('umb-app');
  if (umbApp) {
    mountInto(umbApp);
    return;
  }

  // umb-app not yet in the DOM — start mounted on body so the entry-point's
  // sweep/observe machinery is live, and re-parent into umb-app when it shows
  // up. The disconnect/reconnect on re-parent re-runs setHost → re-sweep.
  document.body.appendChild(host);
  const obs = new MutationObserver(() => {
    const ua = document.querySelector('umb-app');
    if (ua) {
      mountInto(ua);
      obs.disconnect();
    }
  });
  obs.observe(document.body, { childList: true });
})();
