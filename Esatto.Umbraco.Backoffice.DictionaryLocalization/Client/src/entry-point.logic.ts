// The supported side-effect: register content Dictionary entries into
// umbLocalizationManager so `#Key` resolves in property labels. All I/O is behind
// small collaborator interfaces so the logic can be unit-tested without a browser.
// The module keeps one `registered` flag so a double `onInit` in a session is a no-op
// after the first success; a failed fetch leaves the flag false, letting a subsequent
// onInit retry.

import {
  mapResponseToLocalizationSets,
  type DictionaryLocalizationResponse,
  type LocalizationSet,
} from "./map-item.logic.js";

/** The slice of umbLocalizationManager we call. Matches the 17.3.0 surface. */
export interface LocalizationManagerLike {
  registerManyLocalizations(sets: LocalizationSet[]): void;
}

/** The slice of the http client we call — a promise of `{ data, error }`. */
export type FetchLocalizationsFn = () => Promise<{
  data?: unknown;
  error?: unknown;
}>;

// Module-level guard so a repeat `onInit` skips the fetch after we've already
// registered. Exposed via `resetRegisteredStateForTest` for the vitest suite.
let registered = false;

/**
 * Fetches the whole content dictionary and registers each culture as a
 * LocalizationSet into `manager`. Idempotent within a session; returns true iff a
 * fresh registration happened (i.e. the first successful call).
 *
 * On fetch error, logs to console (visible in DevTools during development) and
 * returns false, leaving the guard unset so a later onInit can retry.
 */
export async function fetchAndRegisterDictionaryLocalizations(
  manager: LocalizationManagerLike,
  fetchFn: FetchLocalizationsFn,
): Promise<boolean> {
  if (registered) {
    return false;
  }

  const { data, error } = await fetchFn();
  if (error || !data) {
    // Non-throwing: the backoffice must not fail to load if our bridge doesn't.
    // eslint-disable-next-line no-console
    console.warn("[Esatto.DictionaryLocalization] fetch failed; #Key labels will not resolve until next reload.", error);
    return false;
  }

  const response = data as DictionaryLocalizationResponse;
  const sets = mapResponseToLocalizationSets(response);
  manager.registerManyLocalizations(sets);

  // Only flip the guard once we've committed a successful registration, so a partial
  // failure earlier in this function doesn't lock us out of retrying.
  registered = true;
  return true;
}

/** Test-only: clears the module-level guard between cases. */
export function resetRegisteredStateForTest(): void {
  registered = false;
}
