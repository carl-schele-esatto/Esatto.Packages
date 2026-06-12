# DictionaryFilterValues Rearchitecture — Design

**Status:** Approved 2026-06-12

**Goal:** Replace the fragile, anonymous, `window.fetch`-monkey-patching dictionary-value filter with the supported Umbraco 17 collection data-source/repository override, backed by a single **authenticated** Management API endpoint with a server-side cache invalidated on dictionary edits.

**Package:** `Esatto.Umbraco.Backoffice.DictionaryFilterValues` (net10, Umbraco 17+).

---

## Why

A code review (29 confirmed findings) showed the original design rests on a disproven premise:
- The `[AllowAnonymous]` endpoint publishes the entire dictionary (keys, GUIDs, parent tree, all translation values) to any unauthenticated caller. The justification — "bearer tokens aren't auto-attached to fetch from Lit repos" and "dictionary values are public" — is false: the built-in dictionary collection data source calls `DictionaryService.getDictionary` through the OpenAPI client, which declares `security:[bearer]` and auto-attaches the token.
- The global `window.fetch` patch is a fragile side effect; the client cache never invalidates within a SPA session (stale after edits) and poisons permanently on a single failed fetch; the server walk is N+1 with no cache; match logic is duplicated (C#/JS) and already divergent.

## Verified mechanism (Umbraco 17, from `node_modules/@umbraco-cms/backoffice/dist-cms`)

- A collection resolves its repository purely from the collection manifest's `meta.repositoryAlias` (`collection-default.context.js` `#observeRepository`).
- `byAlias()` is first-match (no weight tiebreak); `register()` rejects duplicate aliases; `exclude()` is permanent; **`unregister(alias)` then `register(sameAlias)` is the only supported same-alias replacement.**
- `backofficeEntryPoint.onInit(host, extensionRegistry)` runs after backoffice load + auth — the correct, auth-safe hook for registry surgery.

**Override:** In `onInit`, register our repository (own alias), then reactively (observe `byAlias('Umb.Collection.Dictionary')`, idempotent guard) `unregister('Umb.Collection.Dictionary')` and re-register it with identical content except `meta.repositoryAlias` → our repository. Built-in element/views/paging/workspace keep working because the alias and item-model shape are unchanged. `onUnload` restores the original.

**Caveat:** the swap assumes the built-in manifest is present when our entry point runs (it is, post-auth), but ordering isn't contractually guaranteed across minors → do it reactively with a safe no-op fallback if it never appears.

## Architecture — three layers

### 1. Client (new TypeScript build, mirrors CustomEditors `Client/`)
- `backofficeEntryPoint` (`onInit`/`onUnload`) performs the reactive repository swap (pure, unit-tested logic).
- Repository extends `UmbRepositoryBase`; `requestCollection(filter)` delegates to the data source.
- Data source implements `UmbCollectionDataSource.getCollection({ filter, skip, take })`; calls our authenticated endpoint via a hand-written OpenAPI service method declaring `security:[{scheme:'bearer',type:'http'}]` + `tryExecute`; maps response 1:1 to `{ entityType:'dictionary', unique, name, parentUnique, translatedIsoCodes }` (`entityType` hard-coded `'dictionary'` to avoid a non-exported import).
- **No** `window.fetch` patch, **no** client cache, **no** `pushState` hack.

### 2. Server (rewrite `DictionaryFilterValuesController`)
- `: ManagementApiControllerBase` (Umbraco.Cms.Api.Management.Controllers) — inherits `BackOfficeAccess` + `UmbracoFeatureEnabled`.
- `[VersionedApiBackOfficeRoute("backoffice/dictionary-filter-values")]` → `/umbraco/management/api/v1/backoffice/dictionary-filter-values`.
- `[Authorize(Policy = AuthorizationPolicies.TreeAccessDictionary)]` (Umbraco.Cms.Web.Common.Authorization).
- One `[HttpGet] Search(string? filter, int skip = 0, int take = 100)`: case-insensitive match of `filter` against name **OR any translation value**; paging after filtering; returns `{ items:[{ id, name, parent:{id}|null, translatedIsoCodes[] }], total }` (matches the built-in `getDictionary` shape so the client maps 1:1). Reads only from the cache.

### 3. Cache + invalidation
- Singleton `IDictionaryFilterValuesCache` holding the flattened set, built once via `IDictionaryItemService.GetDescendantsAsync(null)` (bulk; `Translations` populated). `Invalidate()` clears; next request rebuilds.
- `INotificationHandler<DictionaryItemSavedNotification>` + `<DictionaryItemDeletedNotification>` (Umbraco.Cms.Core.Notifications) call `Invalidate()` (idempotent / batch-safe).
- `IComposer` registers the cache singleton + both notification handlers.

## Decisions (locked)
1. **Transparent reactive swap with safe fallback** (no new nav; no-op if swap can't run).
2. **Hand-write** the single OpenAPI service method (YAGNI vs codegen).
3. **Global singleton cache** (standard Umbraco dictionary has no row-level ACL).
4. **Drop** the `SectionAlias=Translation` entry-point condition (reactive swap is idempotent/section-agnostic).
5. Server dep: **`Umbraco.Cms.Web.Common >=17.0.0`** (matches `Backoffice.Redirects`); confirm it provides `ManagementApiControllerBase` + `VersionedApiBackOfficeRoute` on the restored version.

## Toolchain
- New `Client/` (package.json/tsconfig/vite.config/public mirror CustomEditors; deps `@umbraco-cms/backoffice ^17.4.2`, `typescript ^5.9.3`, `vite ^7.3.1`, `vitest ^2`).
- Add `BuildBackofficeClient` MSBuild target + `<Content Remove="Client\**" />` to the csproj. Flip `AutoPushToFeed` default to `false`.
- New `Esatto.Umbraco.Backoffice.DictionaryFilterValues.Tests` (xUnit, net10) mirroring `CustomEditors.Tests`.

## Testing
- **C#:** cache flatten/parent/translation capture + `Invalidate()` rebuild; pure `Filter.Apply(items, filter, skip, take)` (name match, **value match**, case-insensitivity, empty-filter dump, paging/total); invalidation handler idempotency.
- **TS (vitest):** entry-point swap logic (idempotency, repo-registered-before-swap, swapped manifest carries our `repositoryAlias` + otherwise-identical meta, `onUnload` restores); `mapItem` (parent→parentUnique null handling, iso passthrough, entityType constant).

## Out of scope
- Supporting Umbraco < 17 (Management API base + Bellissima internals are v17). Compatibility table stays 17.x.
- The README "how it works" + images (images await user-provided assets).

## Risks
- Swap ordering (mitigated: reactive + fallback).
- Response shape must match the built-in mapper exactly (pinned by a `mapItem` test + a C# shape test).
- Behavior change: users without dictionary tree access now get 401/403 (correct posture); data source surfaces `tryExecute` errors rather than silently empty.
- `GetDescendantsAsync(null)` first-call cost on huge dictionaries — amortized by the cache (once per invalidation, not per request).
