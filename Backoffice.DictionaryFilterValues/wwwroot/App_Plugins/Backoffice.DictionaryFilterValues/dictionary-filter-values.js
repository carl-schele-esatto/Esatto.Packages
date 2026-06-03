// Backoffice.DictionaryFilterValues — Dictionary filter that matches translation VALUES.
//
// The built-in Bellissima dictionary collection filter ("Type to filter...")
// only matches against the dictionary item NAME (e.g. "BlocksPageListBlockShowMore").
// Editors looking for the entry that holds "Visa fler" have to know its name —
// not the value they remember. This shim makes the filter ALSO match against
// every translation value (Swedish, English, any other configured language).
//
// Implementation:
//   `UmbDictionaryCollectionRepository` is not part of the public exports of
//   @umbraco-cms/backoffice/dictionary in Umbraco 17.0.0 (only detail/item/
//   import/export/tree repositories are). So we can't patch the prototype via
//   import. Instead we intercept at the fetch layer — Bellissima's repository
//   ultimately calls `GET /umbraco/management/api/v1/dictionary?filter=...`,
//   and we patch `window.fetch` to redirect non-empty-filter calls to our
//   custom server endpoint (/umbraco/api/backoffice-dictionary-filter-values/
//   search) which matches across name + translation values via
//   IDictionaryItemService. The response is reformatted to the shape the
//   OpenAPI-generated client expects, so the rest of Bellissima just works.
//
// Empty filter / non-dictionary calls fall through to the original fetch
// untouched. Server failures fall back to the original endpoint, so an
// outage degrades gracefully to name-only filtering.

const SEARCH_ENDPOINT = '/umbraco/api/backoffice-dictionary-filter-values/search';
const DICTIONARY_LIST_PATH = '/umbraco/management/api/v1/dictionary';
const PATCH_FLAG = '__backofficeDictionaryFilterValuesFetchPatched';
const ORIGINAL_FETCH_KEY = '__backofficeDictionaryFilterValuesOriginalFetch';

function isDictionaryListRequest(url) {
  if (typeof url !== 'string') return null;
  let parsed;
  try {
    parsed = new URL(url, window.location.origin);
  } catch {
    return null;
  }
  // Match the list endpoint exactly — not /v1/dictionary/{id} or /import etc.
  if (parsed.pathname !== DICTIONARY_LIST_PATH) return null;
  const filter = parsed.searchParams.get('filter');
  if (!filter || !filter.trim()) return null;
  return { parsed, filter: filter.trim() };
}

// One-shot cache: first filter request fetches every dictionary item with its
// translation TEXT, subsequent keystrokes filter the cache in-memory. The
// dictionary doesn't change while the editor is typing, so there's nothing
// to gain from re-fetching per keystroke. Cache lives until page reload —
// editing a dictionary entry is itself a navigation away and back, so the
// stale window is naturally bounded.
let allItemsCache = null;
let allItemsCachePromise = null;

async function ensureCache() {
  if (allItemsCache) return allItemsCache;
  if (allItemsCachePromise) return allItemsCachePromise;
  allItemsCachePromise = (async () => {
    // Empty `q` triggers dump mode in the controller (returns all items with
    // translation text + iso codes).
    const res = await window[ORIGINAL_FETCH_KEY](SEARCH_ENDPOINT, { credentials: 'include' });
    if (!res.ok) throw new Error(`backoffice-dictionary-filter-values: server returned ${res.status}`);
    const payload = await res.json();
    allItemsCache = payload?.items ?? [];
    return allItemsCache;
  })();
  return allItemsCachePromise;
}

function filterCacheLocally(items, needle) {
  const lower = needle.toLowerCase();
  return items.filter((item) => {
    const name = item.name ?? '';
    if (name.toLowerCase().includes(lower)) return true;
    const translations = item.translations ?? [];
    return translations.some((value) => value && value.toLowerCase().includes(lower));
  });
}

async function fetchSearchResults(filter) {
  const items = await ensureCache();
  const matched = filterCacheLocally(items, filter);
  return { items: matched, total: matched.length };
}

function reshapeResponse(payload) {
  // Match the shape Umbraco's `DictionaryService.getDictionary` returns. The
  // OpenAPI-generated client maps response items into the collection
  // repository's item shape (name, parent.id, translatedIsoCodes, id), so we
  // emit that shape directly.
  const items = (payload?.items ?? []).map((m) => ({
    id: m.id,
    name: m.name ?? '',
    parent: m.parentId ? { id: m.parentId } : null,
    translatedIsoCodes: m.translatedIsoCodes ?? [],
  }));
  return { items, total: payload?.total ?? items.length };
}

function makeJsonResponse(body) {
  return new Response(JSON.stringify(body), {
    status: 200,
    headers: { 'Content-Type': 'application/json' },
  });
}

// --- Pre-warm cache when user enters the Translation section --------------
//
// The first filter keystroke would otherwise trigger a one-time ~1s server
// walk to build the cache. Detecting navigation INTO the section lets us
// build that cache in the background while the user still has the tree in
// front of them — by the time they click the filter input, the cache is
// ready and even the first keystroke is instant.
//
// Bellissima 17 navigates via history.pushState (no native events), so we
// patch pushState/replaceState to fire a synthetic
// 'backoffice-dictionary-filter-values:locationchange' event. The patch is
// idempotent within this shim, and harmless if another shim also patches
// pushState (each fires its own event name; this shim only listens for its
// own).

const TRANSLATION_SECTION_PATH = '/section/translation';
const PUSH_PATCH_FLAG = '__backofficeDictionaryFilterValuesPushStatePatched';
const LOCATION_EVENT = 'backoffice-dictionary-filter-values:locationchange';

function isOnTranslationSection() {
  const haystack = `${window.location.hash} ${window.location.pathname}`;
  return haystack.includes(TRANSLATION_SECTION_PATH);
}

function maybeWarmCache() {
  if (!isOnTranslationSection()) return;
  ensureCache().catch((err) => {
    console.warn('[backoffice-dictionary-filter-values] pre-warm failed:', err);
  });
}

function installLocationListeners() {
  if (!window[PUSH_PATCH_FLAG]) {
    window[PUSH_PATCH_FLAG] = true;
    for (const name of ['pushState', 'replaceState']) {
      const orig = history[name];
      history[name] = function (...args) {
        const result = orig.apply(this, args);
        window.dispatchEvent(new Event(LOCATION_EVENT));
        return result;
      };
    }
  }
  window.addEventListener('hashchange', maybeWarmCache);
  window.addEventListener('popstate', maybeWarmCache);
  window.addEventListener(LOCATION_EVENT, maybeWarmCache);
  // Initial check — user may have landed directly on Translation section.
  maybeWarmCache();
}

function installFetchPatch() {
  if (window[PATCH_FLAG]) return;
  window[PATCH_FLAG] = true;

  const origFetch = window.fetch.bind(window);
  window[ORIGINAL_FETCH_KEY] = origFetch;

  window.fetch = async function (...args) {
    const [resource, init] = args;
    // resource may be a Request object or string. The OpenAPI client uses
    // strings; Request objects we pass through untouched (no risk of false
    // positive on dictionary list).
    const url = typeof resource === 'string' ? resource : resource?.url;
    const match = isDictionaryListRequest(url);
    if (!match) return origFetch(...args);

    try {
      const payload = await fetchSearchResults(match.filter);
      return makeJsonResponse(reshapeResponse(payload));
    } catch (err) {
      console.warn('[backoffice-dictionary-filter-values] fetch redirect failed — falling back to original:', err);
      return origFetch(...args);
    }
  };
}

installFetchPatch();
installLocationListeners();
