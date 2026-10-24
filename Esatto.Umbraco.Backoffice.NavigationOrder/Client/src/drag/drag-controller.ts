import { UmbControllerBase } from '@umbraco-cms/backoffice/class-api';
import type { UmbControllerHostElement } from '@umbraco-cms/backoffice/controller-api';
import { UMB_CURRENT_USER_CONTEXT } from '@umbraco-cms/backoffice/current-user';
import { UmbLocalizationController } from '@umbraco-cms/backoffice/localization-api';
import { UMB_NOTIFICATION_CONTEXT, type UmbNotificationContext } from '@umbraco-cms/backoffice/notification';
import { pathFolderName } from '@umbraco-cms/backoffice/utils';
import { saveNavigationOrder } from '../api';
import { writeCachedOrder } from '../apply/cache';
import { moveAlias, withList } from '../core/order';
import { navigationOrder } from '../navigation-order';
import { makeTabDraggable } from './draggable';
import { focusTargetOf } from './focus';
import { MoveOverlay } from './overlay';
import { SaveQueue } from './save-queue';
import { dragOverEffect, dropPosition, findTabTarget, keyboardStep, type PathNode, type TabTarget } from './tab-target';

const DRAG_TYPE = 'application/x-esatto-navigation-order';

const afterRender = () => new Promise<void>((resolve) => requestAnimationFrame(() => requestAnimationFrame(() => resolve())));

/**
 * Lets admins move Umbraco's top-bar sections and section tabs: drag one onto another tab of the same
 * group, or press Alt+Shift+←/→ on a focused tab. Listens on the document in the capture phase and
 * recognises tabs through composedPath(); the only change to Umbraco's elements is marking a tab an
 * admin presses as draggable.
 */
class NavigationOrderDragController extends UmbControllerBase {
  readonly #localize: UmbLocalizationController;
  readonly #overlay = new MoveOverlay();
  readonly #queue: SaveQueue;
  #isAdmin = false;
  #notifications?: UmbNotificationContext;
  #drag?: { source: TabTarget; element: HTMLElement };
  #drop?: { target: TabTarget; position: 'before' | 'after' };

  constructor(host: UmbControllerHostElement) {
    super(host, 'Esatto.NavigationOrder.Drag');
    this.#localize = new UmbLocalizationController(host);
    this.#queue = new SaveQueue(
      (order) =>
        saveNavigationOrder(host, order).then((saved) => {
          writeCachedOrder(saved);
          return saved;
        }),
      (order) => navigationOrder.setOrder(order),
      (error) => {
        console.warn('[Esatto NavigationOrder] Could not save the order.', error);
        this.#notifications?.peek('danger', { data: { message: this.#localize.term('esattoNavigationOrder_saveFailed') } });
      },
    );

    this.consumeContext(UMB_CURRENT_USER_CONTEXT, (context) => {
      this.observe(context?.isAdmin, (isAdmin) => (this.#isAdmin = isAdmin === true), 'isAdmin');
    });
    this.consumeContext(UMB_NOTIFICATION_CONTEXT, (context) => (this.#notifications = context));

    document.addEventListener('pointerdown', this.#onPointerDown, true);
    document.addEventListener('dragstart', this.#onDragStart, true);
    document.addEventListener('dragover', this.#onDragOver, true);
    document.addEventListener('drop', this.#onDrop, true);
    document.addEventListener('dragend', this.#onDragEnd, true);
    document.addEventListener('keydown', this.#onKeyDown, true);
  }

  override destroy(): void {
    document.removeEventListener('pointerdown', this.#onPointerDown, true);
    document.removeEventListener('dragstart', this.#onDragStart, true);
    document.removeEventListener('dragover', this.#onDragOver, true);
    document.removeEventListener('drop', this.#onDrop, true);
    document.removeEventListener('dragend', this.#onDragEnd, true);
    document.removeEventListener('keydown', this.#onKeyDown, true);
    super.destroy();
  }

  #targetOf(event: Event): TabTarget | null {
    return findTabTarget(event.composedPath() as PathNode[], navigationOrder.manifests(), pathFolderName);
  }

  // Before the browser decides what the press drags.
  #onPointerDown = (event: PointerEvent) => {
    if (!this.#isAdmin) return;
    const target = this.#targetOf(event);
    if (target) makeTabDraggable(target.tab as HTMLElement);
  };

  #onDragStart = (event: DragEvent) => {
    if (!this.#isAdmin || !event.dataTransfer) return;
    const source = this.#targetOf(event);
    if (!source) return;

    event.dataTransfer.effectAllowed = 'move';
    event.dataTransfer.setData(DRAG_TYPE, source.alias);
    const element = source.tab as HTMLElement;
    element.style.opacity = '0.4';
    this.#drag = { source, element };
  };

  #onDragOver = (event: DragEvent) => {
    const target = this.#drag ? this.#targetOf(event) : null;
    const effect = dragOverEffect(this.#drag?.source, target);
    if (effect === 'ignore') return;

    // Claimed even where it can't land: otherwise dropping there opens the tab's link.
    event.preventDefault();
    if (event.dataTransfer) event.dataTransfer.dropEffect = effect;
    if (effect === 'none' || !target) {
      this.#drop = undefined;
      this.#overlay.hideLine();
      return;
    }

    const rect = (target.tab as HTMLElement).getBoundingClientRect();
    const position = dropPosition(event.clientX, rect);
    this.#overlay.showLine(position === 'before' ? rect.left : rect.right, rect.top, rect.height);
    this.#drop = { target, position };
  };

  #onDrop = (event: DragEvent) => {
    if (!this.#drag) return;
    event.preventDefault(); // never navigate to the dragged tab's link
    event.stopPropagation();
    const { source } = this.#drag;
    const drop = this.#drop;
    this.#endDrag();
    if (drop) this.#move(source, drop.target.alias, drop.position);
  };

  #onDragEnd = () => this.#endDrag();

  #endDrag(): void {
    if (this.#drag) this.#drag.element.style.opacity = '';
    this.#drag = undefined;
    this.#drop = undefined;
    this.#overlay.hideLine();
  }

  #onKeyDown = (event: KeyboardEvent) => {
    const step = keyboardStep(event);
    if (step === 0 || !this.#isAdmin) return;
    const source = this.#targetOf(event);
    if (!source) return;

    const tabs = this.#tabsOf(source.group);
    const index = tabs.indexOf(source.tab as Element);
    const neighbour = tabs[index + step];
    const neighbourAlias = neighbour && findTabTarget([neighbour as PathNode, source.group], navigationOrder.manifests(), pathFolderName)?.alias;
    if (index < 0 || !neighbourAlias) return;

    event.preventDefault();
    event.stopPropagation();
    this.#move(source, neighbourAlias, step < 0 ? 'before' : 'after');

    const label = (source.tab as Element).getAttribute('label') ?? source.alias;
    this.#overlay.announce(this.#localize.term('esattoNavigationOrder_moved', label, index + step + 1, tabs.length));
    void afterRender().then(() => {
      const tab = this.#findTab(source);
      if (tab) focusTargetOf(tab).focus();
    });
  };

  #move(source: TabTarget, targetAlias: string, position: 'before' | 'after'): void {
    const { bar } = source;
    const full = navigationOrder.effectiveList(bar);
    const list = moveAlias(full, source.alias, targetAlias, position);
    if (list.join('\n') === full.join('\n')) return;

    const other = bar.kind === 'tabs' ? navigationOrder.effectiveList({ ...bar, type: bar.type === 'dashboard' ? 'sectionView' : 'dashboard' }) : [];
    const current = navigationOrder.order;
    const saving = this.#queue.submit(withList(current, bar, list, other), current);

    void afterRender().then(() => {
      const tab = this.#findTab(source);
      if (tab && this.#queue.busy) this.#overlay.showSpinner(tab.getBoundingClientRect(), this.#localize.term('esattoNavigationOrder_saving'));
    });
    void saving.finally(() => this.#overlay.hideSpinner());
  }

  /** The tabs of a group, in display order (they're its light-DOM children). */
  #tabsOf(group: PathNode): Element[] {
    return Array.from((group as Element).children ?? []).filter((child) => child.localName === 'uui-tab');
  }

  /** The tab now showing `source`'s alias in its group (bars redraw after a move). */
  #findTab(source: TabTarget): HTMLElement | undefined {
    return this.#tabsOf(source.group).find(
      (tab) => findTabTarget([tab as PathNode, source.group], navigationOrder.manifests(), pathFolderName)?.alias === source.alias,
    ) as HTMLElement | undefined;
  }
}

/** Starts drag-and-drop and keyboard moving; it only acts for admins. */
export function startDragging(host: UmbControllerHostElement): void {
  new NavigationOrderDragController(host);
}
