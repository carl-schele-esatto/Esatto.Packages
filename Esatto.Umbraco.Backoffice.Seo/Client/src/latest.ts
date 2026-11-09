// Keeps only the newest of overlapping loads: when filters change quickly, an older, slower response
// must not replace the newer one on screen.

export class Latest {
  #seq = 0;

  /** Runs `load`; resolves to its result, or to `undefined` when a newer run started meanwhile. */
  async run<T>(load: () => Promise<T>): Promise<T | undefined> {
    const mine = ++this.#seq;
    const result = await load();
    return mine === this.#seq ? result : undefined;
  }
}
