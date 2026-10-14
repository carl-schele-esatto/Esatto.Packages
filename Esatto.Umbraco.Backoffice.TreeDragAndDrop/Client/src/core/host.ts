// The controller host: a single hidden Lit element that owns the drag state,
// installs document-level (capture-phase) drag + keyboard listeners, and runs
// the adapter's API calls. Mounted inside <umb-app> so it can consume
// UMB_NOTIFICATION_CONTEXT / UMB_ACTION_EVENT_CONTEXT.
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
  findTreeItemByUnique,
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
import { canCommit, describePlan, planDrop, type DropPlan, type MovePlan } from './drop-plan';
import { showPlan } from './plan-view';
import { hideIndicator } from './indicator';
import { showSpinner, hideSpinner } from './spinner';
import { reduceKey, type KbCandidate, type KbState } from './keyboard';
import { announce } from './announce';
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
    return planDrop({
      zone,
      sourceParentUnique: s.sourceParentUnique,
      targetUnique,
      targetParentUnique: readParentUnique(el),
    });
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
    const plan = this.#planFor(el, getDropZone(el, event.clientY));
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
    showPlan(plan, this.#dragState.adapter);
  }

  #scheduleHoverExpand(el: AnyTreeItem): void {
    if (this.#lastHoverTarget === el) return;
    this.#clearHoverTimer();
    this.#lastHoverTarget = el;
    this.#hoverTimer = setTimeout(() => {
      if (el.hasAttribute('show-children') || el.hasAttribute('is-expanded') || el.hasAttribute('open')) return;
      if (typeof el.toggleChildren === 'function') {
        el.toggleChildren();
      } else {
        const chevron = el.shadowRoot?.querySelector('[name="caret"], [data-mark="chevron"]');
        (chevron as HTMLElement | null | undefined)?.click?.();
      }
    }, HOVER_EXPAND_MS);
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
    const plan = this.#planFor(el, getDropZone(el, event.clientY));
    if (!canCommit(plan)) return;
    event.preventDefault();
    hideIndicator();
    await this.#execute(plan);
  }

  #onDragEnd(): void {
    this.#dragState = null;
    hideIndicator();
    this.#clearHoverTimer();
  }

  // Run a move plan for the current #dragState. Returns whether it succeeded.
  // Both affected branches are reloaded either way: on success the tree
  // re-sorts them; on failure it re-syncs with the server (e.g. a cycle
  // through a collapsed branch the client couldn't see).
  async #execute(plan: MovePlan): Promise<boolean> {
    const s = this.#dragState;
    // In-flight lock: a second drop / Space while a move is running is ignored.
    if (!s || this.#movePending) return false;
    this.#movePending = true;

    const sourceEl = findTreeItemByUnique(s.sourceUnique, s.adapter.entityType);
    if (sourceEl) {
      (sourceEl as HTMLElement).style.opacity = '0.4';
      showSpinner(sourceEl);
    }

    let ok = false;
    try {
      await s.adapter.move(this, s.sourceUnique, plan.parentUnique);
      ok = true;
    } catch (err) {
      this.#showError((err as Error)?.message ?? String(err));
    } finally {
      this.#reload(s.adapter, s.sourceParentUnique);
      this.#reload(s.adapter, plan.parentUnique);
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
      if (!el) return;
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

    const targetEl = els[r.state.targetIndex];
    const plan = targetEl ? this.#planFor(targetEl, r.state.zone) : null;

    if (r.type === 'none') {
      this.#kbState = r.state;
      if (!targetEl || !plan) {
        hideIndicator();
        return;
      }
      showPlan(plan, this.#dragState!.adapter);
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
    // Clear #kbState synchronously so the grabbed branch can't re-enter
    // mid-flight; #dragState stays set until the move resolves (#execute
    // reads it) and the #movePending lock blocks a second #execute.
    this.#kbState = null;
    hideIndicator();
    void (async () => {
      const ok = await this.#execute(plan);
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

  #showError(message: string): void {
    console.error(LOG_PREFIX, message);
    this.#notifications?.peek('danger', { data: { message } });
  }

  render() { return null; }
}
