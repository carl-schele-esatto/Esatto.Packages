// Backoffice.MediaTreeDnd — Media tree drag-and-drop for Umbraco 17.
//
// Adds native HTML5 drag-and-drop to the Umbraco 17 backoffice Media tree.
// Supports reorder (within one parent) and reparent (drop onto a different
// parent), with three-zone drop targets per row: above sibling / into as
// child / below sibling.
//
// This is the media-tree variant of Backoffice.ContentTreeDnd (which handles the
// Content tree). Architecture is identical; differences are entity type
// 'media' (vs 'document'), MediaService APIs (vs DocumentService), and the
// inner kind-element <umb-media-tree-item> (vs <umb-document-tree-item>).

import { LitElement } from '@umbraco-cms/backoffice/external/lit';
import { UmbElementMixin } from '@umbraco-cms/backoffice/element-api';
import { UMB_NOTIFICATION_CONTEXT } from '@umbraco-cms/backoffice/notification';
import { UMB_ACTION_EVENT_CONTEXT } from '@umbraco-cms/backoffice/action';
import { UmbRequestReloadChildrenOfEntityEvent } from '@umbraco-cms/backoffice/entity-action';
import { MediaService } from '@umbraco-cms/backoffice/external/backend-api';
import { tryExecute } from '@umbraco-cms/backoffice/resources';

const ATTACHED_FLAG = 'backofficeMediaTreeDndAttached';
const HOVER_EXPAND_MS = 700;
const ENTITY_TYPE_MEDIA = 'media';
const DRAG_MIME = 'application/x-backoffice-media-tree-dnd';

// --- Tree-item identity ------------------------------------------------

function isTreeItem(el) {
  if (!el || el.nodeType !== 1) return false;
  if (el.tagName !== 'UMB-TREE-ITEM') return false;
  const et = el.getAttribute('entitytype') ?? el.getAttribute('entity-type');
  return et === ENTITY_TYPE_MEDIA;
}

function readUnique(el) {
  const u = el.props?.item?.unique
    ?? el.api?.unique
    ?? el._item?.unique
    ?? el.getAttribute?.('data-unique');
  return u ?? null;
}

function readParentUnique(el) {
  // Top-level items have parent.unique === null (entityType: 'media-root').
  if (el.props?.item?.parent !== undefined) {
    return el.props.item.parent.unique ?? null;
  }
  let cur = composedParent(el);
  while (cur) {
    if (isTreeItem(cur)) return readUnique(cur);
    cur = composedParent(cur);
  }
  return null;
}

function composedParent(el) {
  if (!el) return null;
  if (el.parentElement) return el.parentElement;
  const root = el.getRootNode?.();
  if (root && root.host) return root.host;
  return null;
}

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
  for (const c of wrapperEl.children ?? []) visit(c);
  if (wrapperEl.shadowRoot) {
    for (const c of wrapperEl.shadowRoot.children) visit(c);
  }
}

// --- Drop-zone geometry -------------------------------------------------

function getRowRect(el) {
  // Inner kind-element for media is <umb-media-tree-item>; <umb-default-tree-item>
  // is the generic fallback Bellissima uses when a tree doesn't specify its own.
  const inner = el.querySelector(':scope > umb-media-tree-item, :scope > umb-default-tree-item')
    ?? el.shadowRoot?.querySelector('umb-media-tree-item, umb-default-tree-item');
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
  let cur = composedParent(itemEl);
  while (cur) {
    if (isTreeItem(cur)) return cur;
    cur = composedParent(cur);
  }
  return null;
}

function findEnclosingTreeItemComposed(itemEl) {
  let cur = itemEl;
  while (cur) {
    if (isTreeItem(cur)) return cur;
    cur = composedParent(cur);
  }
  return null;
}

function findTreeItemByUnique(unique) {
  let found = null;
  walkDescendantsComposed(document.documentElement, (el) => {
    if (found) return;
    if (isTreeItem(el) && readUnique(el) === unique) found = el;
  });
  return found;
}

// --- Optimistic DOM mutation -------------------------------------------

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
  el.id = 'backoffice-media-tree-dnd-indicator';
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
const knownRoots = [];

let host = null;

function setHost(h) {
  host = h;
  if (h) {
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
      if (m.type === 'attributes' && isTreeItem(m.target)) {
        host?.attachItem(m.target);
        continue;
      }
      for (const node of m.addedNodes) {
        if (!(node instanceof Element)) continue;
        if (isTreeItem(node)) host?.attachItem(node);
        sweepSubtree(node);
        const enclosing = findEnclosingTreeItemComposed(node);
        if (enclosing) propagateDraggable(enclosing);
      }
    }
  });
  observer.observe(root, {
    childList: true,
    subtree: true,
    attributes: true,
    attributeFilter: ['entitytype', 'entity-type'],
  });

  sweepSubtree(root);
}

// Guard against re-evaluation.
//
// Cross-shim note: Backoffice.ContentTreeDnd performs the equivalent attachShadow
// patch. The first one to load wins; the second wraps it. Both wrappers
// eventually call origAttachShadow and observe the resulting root, so the
// chain is benign across shims.
const PATCH_FLAG = '__backofficeMediaTreeDndPatched';
if (!window[PATCH_FLAG]) {
  window[PATCH_FLAG] = true;

  const origAttachShadow = Element.prototype.attachShadow;
  Element.prototype.attachShadow = function (opts) {
    const root = origAttachShadow.call(this, opts);
    try { observeRoot(root); }
    catch (err) { console.warn('[backoffice-media-tree-dnd] observeRoot failed:', err); }
    return root;
  };

  observeRoot(document);
}

// --- Lit element host --------------------------------------------------

class BackofficeMediaTreeDnd extends UmbElementMixin(LitElement) {
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
    if (el.dataset[ATTACHED_FLAG]) return;
    el.dataset[ATTACHED_FLAG] = '1';
    el.setAttribute('draggable', 'true');
    propagateDraggable(el);

    const obs = new MutationObserver(() => propagateDraggable(el));
    obs.observe(el, { childList: true, subtree: true });
  }

  #installGlobalListeners() {
    if (this.#globalListenersInstalled) return;
    this.#globalListenersInstalled = true;
    document.addEventListener('dragstart', (e) => {
      const el = this.#findTreeItemInPath(e);
      if (!el) return;
      this.#onDragStart(e, el);
    }, true);
    document.addEventListener('dragenter', (e) => {
      const el = this.#findTreeItemInPath(e);
      if (!el || !this.#dragState) return;
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

    const sourceEl = findTreeItemByUnique(sourceUnique);

    if (sourceEl) sourceEl.style.opacity = '0.4';

    try {
      if (zone === 'into') {
        if (sourceParentUnique === targetUnique) return;
        await this.#move(sourceUnique, targetUnique);
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

        const rollbackBefore = sourceEl?.nextSibling ?? null;
        const rollbackParent = sourceEl?.parentNode ?? null;
        optimisticReorder(sourceEl, el, zone);

        try {
          await this.#sort(targetParentUnique, newOrder);
        } catch (err) {
          if (sourceEl && rollbackParent) {
            rollbackParent.insertBefore(sourceEl, rollbackBefore);
          }
          throw err;
        }
        return;
      }

      // Cross-parent slot: move + sort.
      await this.#move(sourceUnique, targetParentUnique);
      const parentEl = findVisualParentTreeItem(el);
      const siblings = listSiblingsInOrder(parentEl).filter((u) => u !== sourceUnique);
      const targetIdx = siblings.indexOf(targetUnique);
      if (targetIdx === -1) {
        await this.#reload(sourceParentUnique);
        await this.#reload(targetParentUnique);
        this.#showWarn(`Moved, but couldn't set the position — it's at the bottom of the new parent.`);
        return;
      }
      const insertAt = zone === 'before' ? targetIdx : targetIdx + 1;
      const newOrder = [...siblings.slice(0, insertAt), sourceUnique, ...siblings.slice(insertAt)];

      try {
        await this.#sort(targetParentUnique, newOrder);
        await this.#reload(sourceParentUnique);
        await this.#reload(targetParentUnique);
      } catch (sortErr) {
        await this.#reload(sourceParentUnique);
        await this.#reload(targetParentUnique);
        this.#showWarn(`Moved, but couldn't set the position — it's at the bottom of the new parent. (${sortErr.message ?? sortErr})`);
      }
    } catch (err) {
      console.error('[backoffice-media-tree-dnd] drop failed', err);
      this.#showError(err.message ?? String(err));
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
      MediaService.putMediaByIdMove({
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
      MediaService.putMediaSort({
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
      console.warn('[backoffice-media-tree-dnd] reload: action-event context not available — tree won\'t auto-refresh');
      return;
    }
    // Conceptual root entityType is 'media-root' (per the tree-item probe:
    // top-level media report parent: { unique: null, entityType: 'media-root' }).
    const entityType = parentUnique ? ENTITY_TYPE_MEDIA : 'media-root';
    this.#actionEvents.dispatchEvent(
      new UmbRequestReloadChildrenOfEntityEvent({
        unique: parentUnique ?? null,
        entityType,
      }),
    );
  }

  #showError(message) {
    console.error('[backoffice-media-tree-dnd]', message);
    this.#notifications?.peek('danger', { data: { message } });
  }

  #showWarn(message) {
    console.warn('[backoffice-media-tree-dnd]', message);
    this.#notifications?.peek('warning', { data: { message } });
  }

  render() { return null; }
}

if (!customElements.get('backoffice-media-tree-dnd')) {
  customElements.define('backoffice-media-tree-dnd', BackofficeMediaTreeDnd);
}

// Mount the host INSIDE <umb-app> so Lit contexts (UMB_NOTIFICATION_CONTEXT,
// UMB_ACTION_EVENT_CONTEXT) provided by ancestors of the section roots can
// propagate down to it.
(() => {
  if (document.querySelector('backoffice-media-tree-dnd')) return;
  const host = document.createElement('backoffice-media-tree-dnd');
  host.style.display = 'none';

  function mountInto(parent) {
    if (host.parentElement !== parent) parent.appendChild(host);
  }

  const umbApp = document.querySelector('umb-app');
  if (umbApp) {
    mountInto(umbApp);
    return;
  }

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
