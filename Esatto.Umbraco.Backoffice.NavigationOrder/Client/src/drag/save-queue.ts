import type { NavigationOrderData } from '../core/types';

/**
 * Saves moves one at a time. A move is applied at once; while a save is running, further moves wait
 * and only the latest is sent next. If a save fails, the last order the server confirmed is applied
 * again (the order from before the first unsaved move) and the pending moves are dropped.
 */
export class SaveQueue {
  readonly #save: (order: NavigationOrderData) => Promise<NavigationOrderData>;
  readonly #apply: (order: NavigationOrderData) => void;
  readonly #onError: (error: unknown) => void;
  #confirmed: NavigationOrderData | null = null;
  #pending: NavigationOrderData | null = null;
  #running: Promise<void> | null = null;

  constructor(
    save: (order: NavigationOrderData) => Promise<NavigationOrderData>,
    apply: (order: NavigationOrderData) => void,
    onError: (error: unknown) => void,
  ) {
    this.#save = save;
    this.#apply = apply;
    this.#onError = onError;
  }

  /** True while a save is running. */
  get busy(): boolean {
    return this.#running !== null;
  }

  /**
   * Applies `next` and queues it for saving. `current` is the order before this move; it becomes
   * the fallback when nothing is saving yet. Resolves when the queue is idle again.
   */
  submit(next: NavigationOrderData, current: NavigationOrderData): Promise<void> {
    if (!this.#running) this.#confirmed = current;
    this.#apply(next);
    this.#pending = next;
    this.#running ??= this.#drain().finally(() => {
      this.#running = null;
    });
    return this.#running;
  }

  async #drain(): Promise<void> {
    while (this.#pending) {
      const order = this.#pending;
      this.#pending = null;
      try {
        this.#confirmed = await this.#save(order);
      } catch (error) {
        this.#pending = null;
        if (this.#confirmed) this.#apply(this.#confirmed);
        this.#onError(error);
        return;
      }
    }
  }
}
