import { describe, it, expect, beforeEach } from "vitest";
import {
  swapDictionaryCollectionRepository,
  restoreDictionaryCollectionRepository,
  resetSwapStateForTest,
  DICTIONARY_COLLECTION_ALIAS,
  OUR_REPOSITORY_ALIAS,
  type ManifestLike,
  type CollectionRegistryLike,
} from "./entry-point.logic.js";

class FakeRegistry implements CollectionRegistryLike {
  #map = new Map<string, ManifestLike>();
  registerCalls: string[] = [];
  unregisterCalls: string[] = [];

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
    this.registerCalls.push(manifest.alias);
  }
  unregister(alias: string) {
    this.#map.delete(alias);
    this.unregisterCalls.push(alias);
  }
}

function builtInCollection(): ManifestLike {
  return {
    type: "collection",
    kind: "default",
    alias: DICTIONARY_COLLECTION_ALIAS,
    name: "Dictionary Collection",
    element: () => Promise.resolve({}),
    meta: { repositoryAlias: "Umb.Repository.Dictionary.Collection" },
  };
}

const repoAliasOf = (m: ManifestLike) => (m.meta as { repositoryAlias?: string }).repositoryAlias;

describe("swapDictionaryCollectionRepository", () => {
  beforeEach(() => resetSwapStateForTest());

  it("is a no-op (returns false) when the built-in collection is not registered yet", () => {
    const reg = new FakeRegistry();
    expect(swapDictionaryCollectionRepository(reg)).toBe(false);
    expect(reg.registerCalls).toEqual([]);
    expect(reg.unregisterCalls).toEqual([]);
  });

  it("registers our repository BEFORE repointing the collection", () => {
    const reg = new FakeRegistry();
    reg.seed(builtInCollection());

    expect(swapDictionaryCollectionRepository(reg)).toBe(true);

    expect(reg.registerCalls).toContain(OUR_REPOSITORY_ALIAS);
    expect(reg.registerCalls).toContain(DICTIONARY_COLLECTION_ALIAS);
    expect(reg.registerCalls.indexOf(OUR_REPOSITORY_ALIAS)).toBeLessThan(
      reg.registerCalls.indexOf(DICTIONARY_COLLECTION_ALIAS),
    );
  });

  it("repoints repositoryAlias to ours while preserving all other manifest fields", () => {
    const reg = new FakeRegistry();
    const original = builtInCollection();
    reg.seed(original);

    swapDictionaryCollectionRepository(reg);

    const swapped = reg.getByAlias(DICTIONARY_COLLECTION_ALIAS)!;
    expect(repoAliasOf(swapped)).toBe(OUR_REPOSITORY_ALIAS);
    expect(swapped.alias).toBe(original.alias);
    expect(swapped.type).toBe(original.type);
    expect(swapped.kind).toBe(original.kind);
    expect(swapped.element).toBe(original.element);
  });

  it("is idempotent — a second call changes nothing", () => {
    const reg = new FakeRegistry();
    reg.seed(builtInCollection());
    swapDictionaryCollectionRepository(reg);
    const callsAfterFirst = reg.registerCalls.length;

    expect(swapDictionaryCollectionRepository(reg)).toBe(true);
    expect(reg.registerCalls.length).toBe(callsAfterFirst);
  });

  it("does nothing if the collection already points at our repository", () => {
    const reg = new FakeRegistry();
    const already = builtInCollection();
    (already.meta as { repositoryAlias?: string }).repositoryAlias = OUR_REPOSITORY_ALIAS;
    reg.seed(already);

    expect(swapDictionaryCollectionRepository(reg)).toBe(true);
    expect(reg.registerCalls).toEqual([]);
    expect(reg.unregisterCalls).toEqual([]);
  });
});

describe("restoreDictionaryCollectionRepository", () => {
  beforeEach(() => resetSwapStateForTest());

  it("re-registers the original collection and removes our repository", () => {
    const reg = new FakeRegistry();
    reg.seed(builtInCollection());
    swapDictionaryCollectionRepository(reg);

    restoreDictionaryCollectionRepository(reg);

    const restored = reg.getByAlias(DICTIONARY_COLLECTION_ALIAS)!;
    expect(repoAliasOf(restored)).toBe("Umb.Repository.Dictionary.Collection");
    expect(reg.isRegistered(OUR_REPOSITORY_ALIAS)).toBe(false);
  });

  it("is a no-op when nothing was swapped", () => {
    const reg = new FakeRegistry();
    restoreDictionaryCollectionRepository(reg);
    expect(reg.registerCalls).toEqual([]);
    expect(reg.unregisterCalls).toEqual([]);
  });
});
