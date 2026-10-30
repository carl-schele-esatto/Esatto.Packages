// Hover-to-expand during a drag: resting on the middle of a collapsed row opens
// it, so an item can be dropped onto one of its children. Only the middle
// ("into") arms the timer. The top and bottom edges mean "next to this row",
// and pausing there to place an item above or below it must not open it.

import type { DropZone } from './drop-zone';

export class HoverExpand<T> {
  #expand: (target: T) => void;
  #delayMs: number;
  #timer: ReturnType<typeof setTimeout> | null = null;
  #target: T | null = null;

  constructor(expand: (target: T) => void, delayMs: number) {
    this.#expand = expand;
    this.#delayMs = delayMs;
  }

  // Called on every dragover of a legal target. Repeats on the middle of the
  // same row keep the running timer; an edge cancels it.
  over(target: T, zone: DropZone): void {
    if (zone !== 'into') {
      this.cancel();
      return;
    }
    if (this.#target === target) return;
    this.cancel();
    this.#target = target;
    this.#timer = setTimeout(() => this.#expand(target), this.#delayMs);
  }

  leave(target: T): void {
    if (this.#target === target) this.cancel();
  }

  cancel(): void {
    if (this.#timer) clearTimeout(this.#timer);
    this.#timer = null;
    this.#target = null;
  }
}
