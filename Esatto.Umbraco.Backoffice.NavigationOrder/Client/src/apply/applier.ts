import { membersOf } from '../core/groups';
import { effectiveOrder, savedListOf, targetWeights } from '../core/order';
import { EMPTY_ORDER, type Bar, type ManagedManifest, type NavigationOrderData } from '../core/types';

/** The extension registry as the applier sees it (see registry-source.ts). */
export interface ManifestSource {
  /** Raw section, dashboard and sectionView manifests, as registered now. */
  current(): ManagedManifest[];
  /** Calls `listener` whenever the registered manifests change. Returns an unsubscribe function. */
  subscribe(listener: () => void): () => void;
  /** Re-registers `manifest` with a new weight. */
  replace(manifest: ManagedManifest, weight: number): void;
}

/**
 * Turns the saved order into manifest weights. Umbraco sorts sections and tabs by weight, highest
 * first, so a manifest whose weight is off is re-registered with the right one (changing the weight
 * in place doesn't re-sort the bars). Original weights are captured the first time a manifest is
 * seen, so clearing a list restores them.
 */
export class NavigationOrderApplier {
  readonly #source: ManifestSource;
  readonly #schedule: (task: () => void) => void;
  readonly #originals = new Map<string, number>();
  #order: NavigationOrderData = EMPTY_ORDER;
  #applying = false;
  #scheduled = false;
  #unsubscribe?: () => void;

  constructor(source: ManifestSource, schedule: (task: () => void) => void = queueMicrotask) {
    this.#source = source;
    this.#schedule = schedule;
  }

  /** Starts following the registry, so manifests registered later get their weight too. Idempotent. */
  start(): void {
    if (this.#unsubscribe) return;
    this.#unsubscribe = this.#source.subscribe(() => {
      // Our own re-registrations notify too; they're already accounted for.
      if (this.#applying || this.#scheduled) return;
      this.#scheduled = true;
      this.#schedule(() => {
        this.#scheduled = false;
        this.#apply();
      });
    });
    this.#apply();
  }

  get order(): NavigationOrderData {
    return this.#order;
  }

  /** Makes `order` the current order and applies it now. */
  setOrder(order: NavigationOrderData): void {
    this.#order = order;
    this.#apply();
  }

  /** The weight a manifest had before this package changed it. */
  originalWeight = (alias: string): number =>
    this.#originals.get(alias) ?? this.#source.current().find((m) => m.alias === alias)?.weight ?? 0;

  /** Every member of a bar in the order it shows, including members the current user can't see. */
  effectiveList(bar: Bar): string[] {
    return effectiveOrder(membersOf(this.manifests(), bar), savedListOf(this.#order, bar), this.originalWeight);
  }

  manifests(): ManagedManifest[] {
    return this.#source.current();
  }

  #apply(): void {
    if (this.#applying) return;
    const manifests = this.#source.current();
    for (const m of manifests) {
      if (!this.#originals.has(m.alias)) this.#originals.set(m.alias, m.weight ?? 0);
    }

    const targets = targetWeights(manifests, this.#order, this.originalWeight);
    const changes = manifests.filter((m) => (m.weight ?? 0) !== targets.get(m.alias));
    if (changes.length === 0) return;

    this.#applying = true;
    try {
      for (const m of changes) this.#source.replace(m, targets.get(m.alias)!);
    } finally {
      this.#applying = false;
    }
  }
}
