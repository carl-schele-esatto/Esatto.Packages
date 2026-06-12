// The supported override: re-point the built-in dictionary collection at our own
// repository. Umbraco's registry rejects duplicate aliases and resolves a
// collection's repository by FIRST match (no weight tiebreak), so the only safe
// same-alias replacement is unregister-then-register. We register our repository
// under our own alias, then clone the built-in collection manifest with its
// meta.repositoryAlias pointed at ours. Pure + idempotent so it is unit-testable
// and safe to call reactively (the registry re-emits after our changes).

/** Minimal manifest shape we read/clone — avoids importing the full manifest union. */
export interface ManifestLike {
  alias: string;
  meta?: Record<string, unknown>;
  [key: string]: unknown;
}

/** The slice of the extension registry the swap needs. */
export interface CollectionRegistryLike {
  getByAlias(alias: string): ManifestLike | undefined;
  isRegistered(alias: string): boolean;
  register(manifest: ManifestLike): void;
  unregister(alias: string): void;
}

export const DICTIONARY_COLLECTION_ALIAS = "Umb.Collection.Dictionary";
export const OUR_REPOSITORY_ALIAS = "Esatto.Repository.DictionaryFilterValues.Collection";

/** Our collection repository, registered programmatically in the entry point. */
export const repositoryManifest: ManifestLike = {
  type: "repository",
  alias: OUR_REPOSITORY_ALIAS,
  name: "Esatto Dictionary Filter Values Collection Repository",
  api: () => import("./dictionary-filter-values-collection.repository.js"),
};

// Holds the built-in manifest we displaced, so onUnload can put it back.
let originalCollectionManifest: ManifestLike | undefined;

/**
 * Swaps the dictionary collection's repository to ours.
 * @returns true when the swap is in place (or already was); false if the built-in
 *          collection manifest is not registered yet (caller should retry later).
 */
export function swapDictionaryCollectionRepository(registry: CollectionRegistryLike): boolean {
  if (originalCollectionManifest) {
    return true;
  }

  const current = registry.getByAlias(DICTIONARY_COLLECTION_ALIAS);
  if (!current) {
    return false;
  }

  const currentMeta = current.meta as { repositoryAlias?: string } | undefined;
  if (currentMeta?.repositoryAlias === OUR_REPOSITORY_ALIAS) {
    // Already pointing at us (e.g. swapped by an earlier load); leave it untouched.
    return true;
  }

  if (!registry.isRegistered(OUR_REPOSITORY_ALIAS)) {
    registry.register(repositoryManifest);
  }

  originalCollectionManifest = current;
  registry.unregister(DICTIONARY_COLLECTION_ALIAS);
  registry.register({
    ...current,
    meta: { ...current.meta, repositoryAlias: OUR_REPOSITORY_ALIAS },
  });

  return true;
}

/** Restores the built-in collection repository and removes ours. */
export function restoreDictionaryCollectionRepository(registry: CollectionRegistryLike): void {
  if (!originalCollectionManifest) {
    return;
  }

  if (registry.isRegistered(DICTIONARY_COLLECTION_ALIAS)) {
    registry.unregister(DICTIONARY_COLLECTION_ALIAS);
  }
  registry.register(originalCollectionManifest);

  if (registry.isRegistered(OUR_REPOSITORY_ALIAS)) {
    registry.unregister(OUR_REPOSITORY_ALIAS);
  }

  originalCollectionManifest = undefined;
}

/** Test-only: clears the module-level swap state between cases. */
export function resetSwapStateForTest(): void {
  originalCollectionManifest = undefined;
}
