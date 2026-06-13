// The controller host: a single hidden Lit element that owns the drag state,
// installs document-level (capture-phase) drag listeners, and runs the API
// calls via DocumentService. Mounted inside <umb-app> so it can consume
// UMB_NOTIFICATION_CONTEXT / UMB_ACTION_EVENT_CONTEXT.

import { LitElement } from '@umbraco-cms/backoffice/external/lit';
import { UmbElementMixin } from '@umbraco-cms/backoffice/element-api';
import { UMB_NOTIFICATION_CONTEXT } from '@umbraco-cms/backoffice/notification';
import { UMB_ACTION_EVENT_CONTEXT } from '@umbraco-cms/backoffice/action';
import { UmbRequestReloadChildrenOfEntityEvent } from '@umbraco-cms/backoffice/entity-action';
import { DocumentService } from '@umbraco-cms/backoffice/external/backend-api';
import { tryExecute } from '@umbraco-cms/backoffice/resources';

import { ATTACHED_FLAG, HOVER_EXPAND_MS, ENTITY_TYPE_DOCUMENT, DRAG_MIME } from './constants';
import { setHost } from './observer';
import {
  isTreeItem,
  readUnique,
  readParentUnique,
  propagateDraggable,
  findVisualParentTreeItem,
  findTreeItemByUnique,
  type AnyTreeItem,
} from './tree-item';
import { getDropZone } from './drop-zone';
import { collectDescendantUniques, isBlockedTarget } from './cycle-guard';
import { listSiblingsInOrder, computeReorder, optimisticReorder } from './siblings';
import { renderIndicator, hideIndicator } from './indicator';

interface DragState {
  sourceUnique: string;
  sourceParentUnique: string | null;
  descendantUniques: Set<string>;
}

export class BackofficeContentTreeDnd extends UmbElementMixin(LitElement) {
  #notifications?: typeof UMB_NOTIFICATION_CONTEXT.TYPE;
  #actionEvents?: typeof UMB_ACTION_EVENT_CONTEXT.TYPE;
  #dragState: DragState | null = null;
  #hoverTimer: ReturnType<typeof setTimeout> | null = null;
  #lastHoverTarget: AnyTreeItem | null = null;
  #globalListenersInstalled = false;

  constructor() {
    super();
    this.consumeContext(UMB_NOTIFICATION_CONTEXT, (ctx) => { this.#notifications = ctx; });
    this.consumeContext(UMB_ACTION_EVENT_CONTEXT, (ctx) => { this.#actionEvents = ctx; });
  }

  connectedCallback(): void {
    super.connectedCallback();
    setHost(this);
    this.#installGlobalListeners();
  }

  disconnectedCallback(): void {
    super.disconnectedCallback();
    setHost(null);
    if (this.#hoverTimer) clearTimeout(this.#hoverTimer);
  }

  attachItem(el: Element): void {
    // Listeners are installed at document level (see #installGlobalListeners),
    // capture phase, so we can't be defeated by stopPropagation upstream.
    // attachItem now only manages the visual state of the wrapper and its
    // shadow descendants — sets draggable=true so drag can initiate.
    const html = el as HTMLElement;
    if (html.dataset[ATTACHED_FLAG]) return;
    html.dataset[ATTACHED_FLAG] = '1';
    el.setAttribute('draggable', 'true');
    propagateDraggable(el);

    // Lit renders the inner <umb-document-tree-item> + uui-menu-item async,
    // so propagateDraggable above may run before the row markup exists. Watch
    // the wrapper's subtree and re-propagate on any change. Idempotent —
    // setAttribute on already-set attribute is a no-op.
    const obs = new MutationObserver(() => propagateDraggable(el));
    obs.observe(el, { childList: true, subtree: true });
  }

  #installGlobalListeners(): void {
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
      void this.#onDrop(e, el);
    }, true);
    document.addEventListener('dragend', () => this.#onDragEnd(), true);
  }

  #findTreeItemInPath(event: Event): AnyTreeItem | null {
    // Walk the composed path looking for the closest tree-item wrapper. The
    // browser may retarget event.target to the shadow host (umb-app); the
    // composedPath gives us the full path through shadow boundaries.
    const path = event.composedPath?.() ?? [];
    for (const node of path) {
      if (isTreeItem(node)) return node;
    }
    return null;
  }

  #onDragStart(event: DragEvent, el: AnyTreeItem): void {
    const sourceUnique = readUnique(el);
    if (!sourceUnique) {
      event.preventDefault();
      return;
    }
    const sourceParentUnique = readParentUnique(el);
    const descendantUniques = collectDescendantUniques(el);

    event.dataTransfer!.effectAllowed = 'move';
    event.dataTransfer!.setData(DRAG_MIME, sourceUnique);

    this.#dragState = { sourceUnique, sourceParentUnique, descendantUniques };
  }

  #onDragOver(event: DragEvent, el: AnyTreeItem): void {
    if (!this.#dragState) return;
    const targetUnique = readUnique(el);
    if (!targetUnique) return;

    if (isBlockedTarget(targetUnique, this.#dragState.sourceUnique, this.#dragState.descendantUniques)) {
      hideIndicator();
      return;
    }

    const zone = getDropZone(el, event.clientY);
    event.preventDefault();
    event.dataTransfer!.dropEffect = 'move';
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
            (chevron as HTMLElement | null | undefined)?.click?.();
          }
        }
      }, HOVER_EXPAND_MS);
    }
  }

  #onDragLeave(event: DragEvent, el: AnyTreeItem): void {
    if (event.relatedTarget && el.contains(event.relatedTarget as Node)) return;
    hideIndicator();
    if (this.#lastHoverTarget === el && this.#hoverTimer) {
      clearTimeout(this.#hoverTimer);
      this.#hoverTimer = null;
      this.#lastHoverTarget = null;
    }
  }

  async #onDrop(event: DragEvent, el: AnyTreeItem): Promise<void> {
    if (!this.#dragState) return;
    const targetUnique = readUnique(el);
    if (!targetUnique) return;
    if (isBlockedTarget(targetUnique, this.#dragState.sourceUnique, this.#dragState.descendantUniques)) return;

    const zone = getDropZone(el, event.clientY);
    event.preventDefault();
    hideIndicator();

    const sourceUnique = this.#dragState.sourceUnique;
    const sourceParentUnique = this.#dragState.sourceParentUnique;
    const targetParentUnique = readParentUnique(el);

    // Find the source DOM element so we can do an optimistic move on success.
    const sourceEl = findTreeItemByUnique(sourceUnique);

    // Visual feedback: dim the source while the API call is in flight.
    if (sourceEl) (sourceEl as HTMLElement).style.opacity = '0.4';

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
        const siblings = listSiblingsInOrder(parentEl);
        const newOrder = computeReorder(siblings, sourceUnique, targetUnique, zone);
        if (!newOrder) {
          throw new Error('Target sibling not found in parent\'s rendered children');
        }

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
      const siblings = listSiblingsInOrder(parentEl);
      const newOrder = computeReorder(siblings, sourceUnique, targetUnique, zone);
      if (!newOrder) {
        // Move succeeded but we can't compute the slot; reload for accuracy
        // (this is a rare edge case).
        await this.#reload(sourceParentUnique);
        await this.#reload(targetParentUnique);
        this.#showWarn(`Moved, but couldn't set the position — it's at the bottom of the new parent.`);
        return;
      }

      try {
        await this.#sort(targetParentUnique, newOrder);
        // Cross-parent slot: same data-store issue as `into` (target needs
        // its hasChildren updated). Reload both branches.
        await this.#reload(sourceParentUnique);
        await this.#reload(targetParentUnique);
      } catch (sortErr) {
        await this.#reload(sourceParentUnique);
        await this.#reload(targetParentUnique);
        this.#showWarn(`Moved, but couldn't set the position — it's at the bottom of the new parent. (${(sortErr as Error)?.message ?? sortErr})`);
      }
    } catch (err) {
      console.error('[backoffice-content-tree-dnd] drop failed', err);
      this.#showError((err as Error)?.message ?? String(err));
      // On error: reload to re-sync visual state with server reality.
      await this.#reload(sourceParentUnique).catch(() => {});
      await this.#reload(targetParentUnique).catch(() => {});
    } finally {
      if (sourceEl) (sourceEl as HTMLElement).style.opacity = '';
    }
  }

  #onDragEnd(): void {
    this.#dragState = null;
    hideIndicator();
    if (this.#hoverTimer) clearTimeout(this.#hoverTimer);
    this.#hoverTimer = null;
    this.#lastHoverTarget = null;
  }

  async #move(unique: string, targetParentUnique: string | null): Promise<void> {
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

  async #sort(parentUnique: string | null, orderedChildUniques: string[]): Promise<void> {
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

  async #reload(parentUnique: string | null): Promise<void> {
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

  #showError(message: string): void {
    console.error('[backoffice-content-tree-dnd]', message);
    this.#notifications?.peek('danger', { data: { message } });
  }

  #showWarn(message: string): void {
    console.warn('[backoffice-content-tree-dnd]', message);
    this.#notifications?.peek('warning', { data: { message } });
  }

  render() { return null; }
}
