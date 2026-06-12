import { describe, it, expect, beforeEach } from "vitest";
import { onInit } from "./bundle.manifests.js";
import {
  resetSwapStateForTest,
  DICTIONARY_COLLECTION_ALIAS,
  OUR_REPOSITORY_ALIAS,
  type ManifestLike,
} from "./entry-point.logic.js";

class FakeSubscription {
  unsubscribed = false;
  unsubscribe() {
    this.unsubscribed = true;
  }
}

// A registry whose byAlias replays the current value synchronously on subscribe,
// like the real BehaviorSubject-backed observable — the case that broke the old
// `if (done && sub)` teardown.
class FakeRegistry {
  #map = new Map<string, ManifestLike>();
  lastSubscription?: FakeSubscription;

  seed(manifest: ManifestLike) {
    this.#map.set(manifest.alias, manifest);
  }
  getByAlias(alias: string) {
    return this.#map.get(alias);
  }
  isRegistered(alias: string) {
    return this.#map.has(alias);
  }
  register(manifest: ManifestLike) {
    this.#map.set(manifest.alias, manifest);
  }
  unregister(alias: string) {
    this.#map.delete(alias);
  }
  byAlias(alias: string) {
    const map = this.#map;
    const sub = new FakeSubscription();
    this.lastSubscription = sub;
    return {
      subscribe(cb: (manifest: ManifestLike | undefined) => void) {
        cb(map.get(alias));
        return sub;
      },
    };
  }
}

const callOnInit = (registry: FakeRegistry) =>
  (onInit as unknown as (host: unknown, reg: unknown) => void)(undefined, registry);

function builtInCollection(): ManifestLike {
  return {
    type: "collection",
    kind: "default",
    alias: DICTIONARY_COLLECTION_ALIAS,
    name: "Dictionary Collection",
    meta: { repositoryAlias: "Umb.Repository.Dictionary.Collection" },
  };
}

describe("onInit", () => {
  beforeEach(() => resetSwapStateForTest());

  it("swaps the repository AND unsubscribes on a synchronous first emit", () => {
    const reg = new FakeRegistry();
    reg.seed(builtInCollection());

    callOnInit(reg);

    const swapped = reg.getByAlias(DICTIONARY_COLLECTION_ALIAS)!;
    expect((swapped.meta as { repositoryAlias?: string }).repositoryAlias).toBe(OUR_REPOSITORY_ALIAS);
    expect(reg.isRegistered(OUR_REPOSITORY_ALIAS)).toBe(true);
    // The teardown must run even though the swap happened during subscribe().
    expect(reg.lastSubscription?.unsubscribed).toBe(true);
  });

  it("does not swap and stays subscribed when the collection is absent", () => {
    const reg = new FakeRegistry();

    callOnInit(reg);

    expect(reg.isRegistered(OUR_REPOSITORY_ALIAS)).toBe(false);
    expect(reg.lastSubscription?.unsubscribed).toBe(false);
  });
});
