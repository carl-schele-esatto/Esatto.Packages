import type { UmbEntryPointOnInit, UmbEntryPointOnUnload } from "@umbraco-cms/backoffice/extension-api";
import {
  swapDictionaryCollectionRepository,
  restoreDictionaryCollectionRepository,
  DICTIONARY_COLLECTION_ALIAS,
  type CollectionRegistryLike,
} from "./entry-point.logic.js";

/**
 * Entry point: swap the dictionary collection's repository to ours. We do it
 * reactively — swap immediately if the built-in collection is already registered,
 * otherwise when it appears. The registry re-emits after our swap, which lets us
 * unsubscribe once done. No `window.fetch` patching.
 */
export const onInit: UmbEntryPointOnInit = (_host, extensionRegistry) => {
  const registry = extensionRegistry as unknown as CollectionRegistryLike;

  // The built-in collection may already be registered (the registry's
  // BehaviorSubject replays synchronously during subscribe, before `sub` is
  // assigned) or appear later. A `done` flag drives teardown so we never depend
  // on `sub` being set inside a synchronous emit.
  let sub: { unsubscribe(): void } | undefined;
  let done = false;
  const teardown = () => {
    if (done && sub) {
      sub.unsubscribe();
    }
  };

  sub = extensionRegistry.byAlias(DICTIONARY_COLLECTION_ALIAS).subscribe((manifest) => {
    if (done || !manifest) {
      return;
    }
    done = swapDictionaryCollectionRepository(registry);
    teardown(); // deferred case: `sub` is already assigned by now
  });

  teardown(); // synchronous-first-emit case: `sub` is assigned now
};

export const onUnload: UmbEntryPointOnUnload = (_host, extensionRegistry) => {
  restoreDictionaryCollectionRepository(extensionRegistry as unknown as CollectionRegistryLike);
};
