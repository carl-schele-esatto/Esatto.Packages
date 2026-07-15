# DictionaryLocalization — Design

**Status:** Approved 2026-07-15

**Goal:** Bridge Umbraco 17 **content Dictionary items** into the backoffice **UI localization system** so that any `#Key` in a property label (or anywhere `localize.string()` is called) resolves to the current backoffice user's culture value.

**Package:** `Esatto.Umbraco.Backoffice.DictionaryLocalization` (net10.0, Umbraco 17+).

---

## Why

Umbraco has two independent translation systems:

1. **Content Dictionary** — items in the Translation section, addressed via `@Umbraco.GetDictionaryValue("Key")` in Razor. Runtime-culture-aware but front-end only.
2. **Backoffice UI localization** — translations registered via `localization` manifest extensions, consumed by `umbLocalizationManager` / `localize.string()` / `localize.term()`. Backoffice-only.

The `#Key` syntax editors expect to work in property labels ("Name" field on a content type property) is resolved by `localize.string()` — which only reads system 2, so `#TestTag` displays as literal `#TestTag` even when a matching content-dictionary key exists.

Verified in `backoffice/libs/localization-api/index.js` at line 239 (17.3.0):

```js
string(t, ...e) {
  if (typeof t != "string") return "";
  const n = /#\w+/g;
  return t.replace(n, (i) => {
    const o = i.slice(1), r = this.#s(o);
    return r === null ? i : this.#o(r, e);
  });
}
```

The regex captures `#\w+` and looks up `umbLocalizationManager`. No path exists from content-Dictionary → `umbLocalizationManager` in stock Umbraco 17.

## Verified mechanism (Umbraco 17.3.0, `libs/localization-api/index.js`)

- `umbLocalizationManager.registerManyLocalizations(sets)` iterates → `registerLocalization(t)`. The manager **merges** on re-registration (`{ ...existing, ...t }` at line 36), so calling it twice for the same `$code` is safe.
- `$code` is lowercased via `t.$code.toLowerCase()` on registration — pass any case; internally normalized.
- **`localize.string()` fallback order** (line 108-109): primary `region` (`sv-se`) → secondary `language` (`sv`) → hard fallback `en`. So a user on `sv-SE` sees `sv-se` if registered, else `sv`, else `en`. This is DIFFERENT from the content-Dictionary lookup lesson (which requires exact-match on the front end).
- **There is no `removeLocalization` / `unregister` API** on the manager. `onUnload` cannot cleanly delete our entries; we skip cleanup (harmless — the manager lives at document lifetime, and re-registration is idempotent via merge).
- `backofficeEntryPoint.onInit(host, extensionRegistry)` runs after auth, correct hook for fetch + register.
- `IDictionaryItemService.GetDescendantsAsync(null)` returns the whole dictionary flat in one call, `Translations` populated with `LanguageIsoCode` + `Value`.
- `\w` in JS regex is `[A-Za-z0-9_]` — dotted keys like `ContactForm.Company` are broken by the `#` capture (matches only `#ContactForm`).

## Architecture — three layers, mirrors `DictionaryFilterValues`

### 1. Client (TypeScript, `Client/`)

- `backofficeEntryPoint` `onInit` fetches `/umbraco/management/api/v1/backoffice/dictionary-localization/all`, transforms the payload into `UmbLocalizationSetBase[]` (one per culture), calls `umbLocalizationManager.registerManyLocalizations(sets)`. Module-level `registered` flag so double-init in a single session is a no-op (the manager itself merges safely, but the guard avoids the wasted work).
- `onUnload` is a no-op — the manager exposes no removal API, and its lifetime is document-scoped anyway.
- Pure logic split out of the entry point (`map-item.logic.ts`) so it is unit-testable without a browser: input `CulturePayload[]`, output `UmbLocalizationSetBase[]` with:
  - Original alias entries (`"ContactForm.Company": "Företag"`) so front-end / programmatic lookups round-trip.
  - Underscore-normalized alias entries (`"ContactForm_Company": "Företag"`) so the `/#\w+/g` regex captures cleanly. Both variants point to the same string.
- Uses `umbHttpClient.get({ security: [{ scheme: "bearer", type: "http" }], url })` — the built-in auto-bearer path. No `window.fetch` patching.

### 2. Server (C#, `src/`)

- `DictionaryLocalizationController : ManagementApiControllerBase`
- `[VersionedApiBackOfficeRoute("backoffice/dictionary-localization")]` → `/umbraco/management/api/v1/backoffice/dictionary-localization/all`.
- `[Authorize(Policy = AuthorizationPolicies.BackOfficeAccess)]` — the payload is not sensitive but every backoffice user should be able to render property labels.
- One `[HttpGet("all")] GetAll()` returning `{ cultures: { "sv-SE": { key: value, ... }, "en": { ... }, ... } }`. Reads only from the cache.

### 3. Cache + invalidation

- Singleton `IDictionaryLocalizationCache` — flattened per-culture snapshot, built once via `IDictionaryItemService.GetDescendantsAsync(null)`. `Invalidate()` clears; next request rebuilds.
- `INotificationHandler<DictionaryItemSavedNotification>` + `<DictionaryItemDeletedNotification>` → `Invalidate()`.
- `IComposer` registers the cache singleton + both notification handlers.

## Refresh model

- **Server:** invalidates on every dictionary save/delete. Next fetch is fresh.
- **Client:** registers once on backoffice startup. Editors see new dictionary values after a browser reload — matches the pattern that schema-shaped UI (property labels) doesn't hot-reload mid-session in Umbraco anyway. No mutation-observer / event-listener wiring.

## Key naming

Given `#Key` uses regex `/#\w+/g`, keys with `.` or `-` are captured incompletely by the backoffice regex. The transform emits **both** the original alias and an underscore-normalized alias:

- `ContactForm.Company` → registers `ContactForm.Company` **and** `ContactForm_Company` (same value).
- `header.contact-cta.label` → registers both `header.contact-cta.label` **and** `header_contact_cta_label`.
- `TestTag` (already flat) → registers only `TestTag` (no duplicate).

Existing `@Umbraco.GetDictionaryValue("ContactForm.Company")` front-end calls are unaffected — this package doesn't touch server-side content lookups.

## Decisions (locked)

1. **Register on backoffice startup only** — no live refresh listener. Server cache handles data freshness; browser reload picks up the new registrations.
2. **Both dotted and underscore variants** in the client-registered map — solves the regex incompatibility with zero coupling to Umbraco internals.
3. **`AuthorizationPolicies.BackOfficeAccess`** — anyone who can see the backoffice can render translated property labels. Dictionary content isn't sensitive; scoping tighter (e.g. `TreeAccessDictionary`) would break the endpoint for users who can see content but not the Translation section.
4. **Register under the exact ISO codes the dictionary stores** — `umbLocalizationManager` handles region → language → `en` fallback internally, so a dictionary saved under `sv-SE` will resolve for a backoffice user on plain `sv` via the manager's secondary-lookup path. No manual duplication under bare language codes.
5. **Idempotency guard** on `onInit` — a module-level `registered` flag prevents double-registration if entry-point onInit fires more than once per session.

## Toolchain

- `Client/` mirrors `DictionaryFilterValues` (npm + Vite + vitest + `@umbraco-cms/backoffice ^17.4.2`).
- csproj mirrors `DictionaryFilterValues`: Razor SDK, `net10.0`, `StaticWebAssetBasePath=/`, PackageId / RepositoryUrl / PackageProjectUrl, `BuildBackofficeClient` MSBuild target, `AutoPushToFeed` off by default, MessagePack + Microsoft.OpenApi security overrides.
- `Esatto.Umbraco.Backoffice.DictionaryLocalization.Tests` (xUnit, net10) mirrors the sibling tests project.

## Testing

- **C#:** cache flatten (translations captured, empty translation entries skipped) + `Invalidate()` rebuild path; controller shape (culture map keyed by ISO code, values by alias); invalidation handler idempotency under batch notifications.
- **TS (vitest):**
  - `map-item.logic`: emits underscore variants for dotted/hyphenated keys; skips duplicate emission when alias is already flat; groups by culture code; skips empty translation values.
  - `entry-point.logic`: idempotency guard (second `onInit` is a no-op after first success); fetch error path leaves `registered` false so a subsequent `onInit` retries.

## Out of scope

- Umbraco < 17 (Bellissima's `umbLocalizationManager` API + Management API auth model are v17).
- Overriding built-in Umbraco UI keys (e.g. `general_name`) — dictionary keys are additive; a name collision would give the dictionary value the last-registered slot at higher weight, which is safer left as user-configurable if a real conflict arises.
- README screenshots — added after end-to-end verification.

## Risks

- **Payload growth** with very large dictionaries. Mitigation: one-shot bulk fetch, `HttpCache`-friendly if we add ETag later; typical Umbraco dictionaries are < 500 entries × ~5 languages < 100KB.
- **Registration order vs first render**: if `localize.string()` runs before `onInit` completes the async fetch, the first render shows literal `#Key`. The manager's `#changedKeys` set + `keysChanged` broadcast (line 75-77) triggers a `requestUpdate()` on every subscribed controller after `registerLocalization`, so labels re-render once we register — a brief flicker on first paint only. Accept as a trade-off vs the complexity of a sync-loaded registration path.
- **Payload size at scale**: a dictionary with hundreds of items × many languages × doubled aliases (dotted + underscored) can grow. Mitigate via bulk fetch (already planned) and consider `Cache-Control` + ETag on a follow-up if consumers hit noticeable startup lag.
