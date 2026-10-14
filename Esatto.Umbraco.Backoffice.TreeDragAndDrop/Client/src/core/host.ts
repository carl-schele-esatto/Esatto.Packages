// The controller host: a single hidden Lit element that owns the drag state,
// installs document-level (capture-phase) drag + keyboard listeners, and runs
// the adapter's API calls (move, and sort for trees that have it). Mounted
// inside <umb-app> so it can consume UMB_NOTIFICATION_CONTEXT /
// UMB_ACTION_EVENT_CONTEXT.
//
// Tree-agnostic: anything tree-specific comes from the TreeAdapter registered
// for the row's entity type. A drag only ever accepts targets from the same
// adapter as its source (no cross-tree drops).

import { LitElement } from '@umbraco-cms/backoffice/external/lit';
import { UmbElementMixin } from '@umbraco-cms/backoffice/element-api';
import { UMB_NOTIFICATION_CONTEXT } from '@umbraco-cms/backoffice/notification';
import { UMB_ACTION_EVENT_CONTEXT } from '@umbraco-cms/backoffice/action';
import { UmbRequestReloadChildrenOfEntityEvent } from '@umbraco-cms/backoffice/entity-action';

import { ATTACHED_FLAG, DRAG_MIME, HOVER_EXPAND_MS, LOG_PREFIX } from './constants';
import { setHost } from './observer';
import {
  adapterOf,
  expandRow,
  findTreeItemByUnique,
  listSiblings,
  isTreeItem,
  listTreeItemsInOrder,
  propagateDraggable,
  readName,
  readParentUnique,
  readUnique,
  type AnyTreeItem,
} from './tree-item';
import { getDropZone, type DropZone } from './drop-zone';
import { collectDescendantUniques, isBlockedTarget } from './cycle-guard';
import { canCommit, describePlan, planDrop, planSortedDrop, type ActionPlan, type DropPlan } from './drop-plan';
import { optimisticReorder } from './reorder-dom';
import { showPlan } from './plan-view';
import { hideIndicator } from './indicator';
import { showSpinner, hideSpinner } from './spinner';
import { reduceKey, type KbCandidate, type KbState } from './keyboard';
import { announce } from './announce';
import { isGrabStartPath, isInsideTree } from './key-path';
import type { TreeAdapter } from './types';

interface DragState {
  adapter: TreeAdapter;
  sourceUnique: string;
  sourceParentUnique: string | null;
  descendantUniques: Set<string>;
}

const KEYBOARD_KEYS = ['ArrowUp', 'ArrowDown', 'ArrowLeft', 'ArrowRight', ' ', 'Enter', 'Escape'];

export class EsattoTreeDnd extends UmbElementMixin(LitElement) {
  #notifications?: typeof UMB_NOTIFICATION_CONTEXT.TYPE;
  #actionEvents?: typeof UMB_ACTION_EVENT_CONTEXT.TYPE;
  #dragState: DragState | null = null;
  #kbState: KbState | null = null;
  #movePending = false;
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
    this.#clearHoverTimer();
  }

  attachItem(el: Element): void {
    // Listeners live at document level (capture phase); attachItem only makes
    // the wrapper and its shadow descendants draggable.
    const html = el as HTMLElement;
    if (html.dataset[ATTACHED_FLAG]) return;
    html.dataset[ATTACHED_FLAG] = '1';
    el.setAttribute('draggable', 'true');
    propagateDraggable(el);

    // Lit renders the inner kind-element + uui-menu-item async, so
    // propagateDraggable above may run before the row markup exists. Watch
    // the wrapper's subtree and re-propagate on any change. Idempotent.
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
      if (el) this.#onDragStart(e, el);
    }, true);
    document.addEventListener('dragenter', (e) => {
      const el = this.#findTreeItemInPath(e);
      if (!el || !this.#dragState) return;
      // preventDefault on dragenter marks the element as a potential drop
      // target; dragover decides whether it really is one.
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
      if (el) this.#onDragLeave(e, el);
    }, true);
    document.addEventListener('drop', (e) => {
      const el = this.#findTreeItemInPath(e);
      if (el) void this.#onDrop(e, el);
    }, true);
    document.addEventListener('dragend', () => this.#onDragEnd(), true);
    // Keyboard "grab & place" (ARIA APG). Capture phase so the tree's own key
    // handling (open node, expand/collapse, roving tabindex) doesn't fire for
    // the keys we own while grabbed.
    document.addEventListener('keydown', (e) => this.#onKeyDown(e), true);
    // A click anywhere outside the grabbed tree ends a keyboard grab, so a
    // forgotten grab can't capture keys typed later in, say, a textarea.
    document.addEventListener('pointerdown', (e) => {
      if (this.#kbState && this.#dragState && !isInsideTree(e.composedPath(), this.#dragState.adapter.entityType)) {
        this.#endKeyboardGrab();
        announce('Move cancelled.');
      }
    }, true);
  }

  #findTreeItemInPath(event: Event): AnyTreeItem | null {
    // The browser may retarget event.target to a shadow host (umb-app); the
    // composed path gives us the full path through shadow boundaries.
    for (const node of event.composedPath?.() ?? []) {
      if (isTreeItem(node)) return node;
    }
    return null;
  }

  #beginDrag(el: AnyTreeItem): DragState | null {
    const adapter = adapterOf(el);
    const sourceUnique = readUnique(el);
    if (!adapter || !sourceUnique) return null;
    return {
      adapter,
      sourceUnique,
      sourceParentUnique: readParentUnique(el),
      descendantUniques: collectDescendantUniques(el),
    };
  }

  // What dropping on `el` in `zone` would do, or null if `el` is not a legal
  // target for the current drag (other tree, itself, or one of its descendants).
  #planFor(el: AnyTreeItem, zone: DropZone): DropPlan | null {
    const s = this.#dragState;
    if (!s || adapterOf(el) !== s.adapter) return null;
    const targetUnique = readUnique(el);
    if (!targetUnique) return null;
    if (isBlockedTarget(targetUnique, s.sourceUnique, s.descendantUniques)) return null;
    const ctx = {
      zone,
      sourceParentUnique: s.sourceParentUnique,
      targetUnique,
      targetParentUnique: readParentUnique(el),
    };
    if (!s.adapter.sort) return planDrop(ctx);
    // Siblings are only needed for edge zones; skip the DOM walk for "into".
    const siblings = zone === 'into' ? [] : listSiblings(ctx.targetParentUnique, s.adapter.entityType);
    return planSortedDrop({ ...ctx, sourceUnique: s.sourceUnique, siblings });
  }

  #onDragStart(event: DragEvent, el: AnyTreeItem): void {
    if (this.#movePending) { event.preventDefault(); return; }
    const state = this.#beginDrag(el);
    if (!state) { event.preventDefault(); return; }
    event.dataTransfer!.effectAllowed = 'move';
    event.dataTransfer!.setData(DRAG_MIME, state.sourceUnique);
    this.#dragState = state;
  }

  #onDragOver(event: DragEvent, el: AnyTreeItem): void {
    if (!this.#dragState) return;
    const zone = getDropZone(el, event.clientY);
    const plan = this.#planFor(el, zone);
    if (plan === null) {
      hideIndicator();
      return;
    }
    // Hover-to-expand runs for no-op rows too: hovering a sibling is how you
    // open it to drop onto one of its children.
    this.#scheduleHoverExpand(el);
    if (plan.kind === 'noop') {
      // Not calling preventDefault leaves the browser's "no drop" cursor.
      hideIndicator();
      return;
    }
    event.preventDefault();
    event.dataTransfer!.dropEffect = 'move';
    showPlan(plan, this.#dragState.adapter, el, zone);
  }

  #scheduleHoverExpand(el: AnyTreeItem): void {
    if (this.#lastHoverTarget === el) return;
    this.#clearHoverTimer();
    this.#lastHoverTarget = el;
    this.#hoverTimer = setTimeout(() => expandRow(el), HOVER_EXPAND_MS);
  }

  #clearHoverTimer(): void {
    if (this.#hoverTimer) clearTimeout(this.#hoverTimer);
    this.#hoverTimer = null;
    this.#lastHoverTarget = null;
  }

  #onDragLeave(event: DragEvent, el: AnyTreeItem): void {
    if (event.relatedTarget && el.contains(event.relatedTarget as Node)) return;
    hideIndicator();
    if (this.#lastHoverTarget === el) this.#clearHoverTimer();
  }

  async #onDrop(event: DragEvent, el: AnyTreeItem): Promise<void> {
    const zone = getDropZone(el, event.clientY);
    const plan = this.#planFor(el, zone);
    if (!canCommit(plan)) return;
    event.preventDefault();
    hideIndicator();
    await this.#execute(plan, el, zone);
  }

  #onDragEnd(): void {
    this.#dragState = null;
    hideIndicator();
    this.#clearHoverTimer();
  }

  // Run a plan for the current #dragState. Returns whether it fully succeeded.
  //   move       → move; reload both branches (chevrons come from the data
  //                store; Dictionary re-sorts alphabetically).
  //   reorder    → optimistic DOM move first (the sort API is slow), then sort;
  //                roll the DOM back if it fails. No reload on success.
  //   move+sort  → move, then sort. If only the sort fails the item stays moved
  //                (a completed move beats a rollback) and a warning says so.
  // Any failure reloads both branches so the tree matches the server again
  // (e.g. a cycle through a collapsed branch the client couldn't see).
  async #execute(plan: ActionPlan, targetEl: AnyTreeItem, zone: DropZone): Promise<boolean> {
    const s = this.#dragState;
    // In-flight lock: a second drop / Space while a move is running is ignored.
    if (!s || this.#movePending) return false;
    this.#movePending = true;

    const { adapter, sourceUnique, sourceParentUnique } = s;
    const sourceEl = findTreeItemByUnique(sourceUnique, adapter.entityType);
    if (sourceEl) {
      (sourceEl as HTMLElement).style.opacity = '0.4';
      showSpinner(sourceEl);
    }

    let ok = false;
    let reload = true;
    try {
      if (plan.kind === 'move') {
        await adapter.move(this, sourceUnique, plan.parentUnique);
        ok = true;
      } else if (plan.kind === 'reorder') {
        const rollbackParent = sourceEl?.parentNode ?? null;
        const rollbackBefore = sourceEl?.nextSibling ?? null;
        optimisticReorder(sourceEl, targetEl, zone);
        // Re-pin the spinner over the row's NEW position.
        if (sourceEl) showSpinner(sourceEl);
        try {
          await adapter.sort!(this, plan.parentUnique, plan.order);
        } catch (err) {
          if (sourceEl && rollbackParent) rollbackParent.insertBefore(sourceEl, rollbackBefore);
          throw err;
        }
        ok = true;
        reload = false;
      } else {
        await adapter.move(this, sourceUnique, plan.parentUnique);
        try {
          await adapter.sort!(this, plan.parentUnique, plan.order);
          ok = true;
        } catch (sortErr) {
          this.#showWarn(`Moved, but couldn't set the position — it's at the bottom of the new parent. (${(sortErr as Error)?.message ?? sortErr})`);
        }
      }
    } catch (err) {
      this.#showError((err as Error)?.message ?? String(err));
    } finally {
      if (reload) {
        this.#reload(adapter, sourceParentUnique);
        this.#reload(adapter, plan.parentUnique);
      }
      hideSpinner();
      if (sourceEl) (sourceEl as HTMLElement).style.opacity = '';
      this.#movePending = false;
    }
    return ok;
  }

  // --- Keyboard "grab & place" ------------------------------------------------

  // All rendered rows of the grabbed item's tree, in visual order, with a
  // parallel element array so a reducer index maps back to a DOM row.
  #buildCandidates(): { candidates: KbCandidate[]; els: AnyTreeItem[] } {
    const s = this.#dragState;
    const candidates: KbCandidate[] = [];
    const els: AnyTreeItem[] = [];
    if (!s) return { candidates, els };
    for (const el of listTreeItemsInOrder(s.adapter.entityType)) {
      const unique = readUnique(el);
      if (!unique) continue;
      candidates.push({
        unique,
        parentUnique: readParentUnique(el),
        blocked: isBlockedTarget(unique, s.sourceUnique, s.descendantUniques),
      });
      els.push(el);
    }
    return { candidates, els };
  }

  #describe(plan: DropPlan, zone: DropZone, targetEl: AnyTreeItem): string {
    const adapter = this.#dragState!.adapter;
    const parentEl = plan.kind === 'move' && plan.parentUnique !== null
      ? findTreeItemByUnique(plan.parentUnique, adapter.entityType)
      : null;
    return describePlan(plan, zone, readName(targetEl), parentEl ? readName(parentEl) : null);
  }

  #endKeyboardGrab(): void {
    hideIndicator();
    this.#kbState = null;
    this.#dragState = null;
  }

  #onKeyDown(e: KeyboardEvent): void {
    if (this.#kbState === null) {
      // Not grabbed: only Space starts a grab. Enter keeps its normal
      // "open node" behaviour; other keys pass through untouched.
      if (e.key !== ' ' || this.#movePending) return;
      const el = this.#findTreeItemInPath(e);
      // Only the row itself grabs; Space on its caret, actions or buttons keeps
      // its normal meaning.
      if (!el || !isGrabStartPath(e.composedPath(), el)) return;
      const state = this.#beginDrag(el);
      if (!state) return;
      e.preventDefault();
      e.stopPropagation();

      this.#dragState = state;
      const { candidates } = this.#buildCandidates();
      const sourceIndex = candidates.findIndex((c) => c.unique === state.sourceUnique);
      this.#kbState = { sourceUnique: state.sourceUnique, targetIndex: Math.max(sourceIndex, 0), zone: 'before' };
      hideIndicator();
      announce(`Grabbed ${readName(el)}. Use arrow keys to choose where to move it, space to drop, escape to cancel.`);
      return;
    }

    if (!KEYBOARD_KEYS.includes(e.key)) return;
    // A key outside the grabbed tree (focus moved to a textarea, a modal…)
    // isn't ours: end the grab and let the key do its normal job.
    if (!isInsideTree(e.composedPath(), this.#dragState!.adapter.entityType)) {
      this.#endKeyboardGrab();
      announce('Move cancelled.');
      return;
    }
    e.preventDefault();
    e.stopPropagation();

    // Rebuild candidates fresh (the tree may have expanded) and clamp the
    // possibly stale index before running the pure reducer.
    const { candidates, els } = this.#buildCandidates();
    if (candidates.length === 0) {
      this.#endKeyboardGrab();
      return;
    }
    const targetIndex = Math.min(Math.max(this.#kbState.targetIndex, 0), candidates.length - 1);
    const r = reduceKey({ ...this.#kbState, targetIndex }, e.key, candidates);

    if (r.type === 'cancel') {
      this.#endKeyboardGrab();
      announce('Move cancelled.');
      return;
    }

    if (r.type === 'blocked') {
      // Stay grabbed: the position is the item itself or one of its descendants.
      announce('Can\'t move an item into itself or its own children. Choose another position, or press escape to cancel.');
      return;
    }

    const targetEl = els[r.state.targetIndex];
    const plan = targetEl ? this.#planFor(targetEl, r.state.zone) : null;

    if (r.type === 'none') {
      this.#kbState = r.state;
      if (!targetEl || !plan) {
        hideIndicator();
        return;
      }
      showPlan(plan, this.#dragState!.adapter, targetEl, r.state.zone);
      announce(this.#describe(plan, r.state.zone, targetEl));
      return;
    }

    // r.type === 'commit'
    if (!canCommit(plan)) {
      // Stay grabbed: nothing would change here.
      announce('Nothing to move here. Choose another position, or press escape to cancel.');
      return;
    }
    const { sourceUnique, adapter } = this.#dragState!;
    const sourceName = readName(findTreeItemByUnique(sourceUnique, adapter.entityType));
    const zone = r.state.zone;
    // Clear #kbState synchronously so the grabbed branch can't re-enter
    // mid-flight; #dragState stays set until the move resolves (#execute
    // reads it) and the #movePending lock blocks a second #execute.
    this.#kbState = null;
    hideIndicator();
    void (async () => {
      const ok = await this.#execute(plan, targetEl, zone);
      announce(ok ? `Moved ${sourceName}.` : `Could not move ${sourceName}.`);
      // Best-effort refocus of the moved row so keyboard users keep their place.
      // The reload is async, so the row may not be re-rendered yet; then we
      // simply leave focus where it is.
      const found = findTreeItemByUnique(sourceUnique, adapter.entityType);
      const inner = found?.querySelector('[tabindex],a,button') as HTMLElement | null;
      if (inner?.focus) inner.focus();
      else (found as HTMLElement | null)?.focus?.();
      this.#dragState = null;
    })();
  }

  // --- Server sync -------------------------------------------------------------

  #reload(adapter: TreeAdapter, parentUnique: string | null): void {
    if (!this.#actionEvents) {
      console.warn(`${LOG_PREFIX} reload: action-event context not available — tree won't auto-refresh`);
      return;
    }
    // Top-level rows report parent { unique: null, entityType: '<tree>-root' },
    // so a root reload uses the adapter's root entity type.
    this.#actionEvents.dispatchEvent(
      new UmbRequestReloadChildrenOfEntityEvent({
        unique: parentUnique,
        entityType: parentUnique ? adapter.entityType : adapter.rootEntityType,
      }),
    );
  }

  #showWarn(message: string): void {
    console.warn(LOG_PREFIX, message);
    this.#notifications?.peek('warning', { data: { message } });
  }

  #showError(message: string): void {
    console.error(LOG_PREFIX, message);
    this.#notifications?.peek('danger', { data: { message } });
  }

  render() { return null; }
}
