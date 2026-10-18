// Pure: when a property change should re-render, and cancelling a superseded render request.

/**
 * Delay before re-rendering after a Lit property change; null when neither content nor settings
 * changed. A value arriving for the first time (previous value undefined) is the block loading,
 * not an edit, so it renders immediately; edits to existing data are debounced.
 */
export function renderDelay(changed: Map<PropertyKey, unknown>, debounceMs: number): number | null {
  const keys = ['content', 'settings'].filter((key) => changed.has(key));
  if (keys.length === 0) return null;
  return keys.some((key) => changed.get(key) !== undefined) ? debounceMs : 0;
}

/** Hands out one AbortSignal per render; starting a newer render or cancelling aborts the previous one. */
export class LatestRequest {
  #controller?: AbortController;

  next(): AbortSignal {
    this.#controller?.abort();
    this.#controller = new AbortController();
    return this.#controller.signal;
  }

  cancel(): void {
    this.#controller?.abort();
    this.#controller = undefined;
  }
}
