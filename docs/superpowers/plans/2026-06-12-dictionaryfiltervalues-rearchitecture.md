# DictionaryFilterValues Rearchitecture — Implementation Plan

> **For agentic workers:** Execute task-by-task. Each task: implement → build/test → review. Mirror existing sibling packages (`Esatto.Umbraco.Backoffice.CustomEditors` for the TS toolchain + test project; `Backoffice.Redirects` for the Management API server pattern) rather than inventing structure.

**Goal:** Replace the anonymous `window.fetch` shim with a supported authenticated Umbraco 17 collection data-source/repository override + cached Management API endpoint.

**Spec:** [2026-06-12-dictionaryfiltervalues-rearchitecture-design.md](../specs/2026-06-12-dictionaryfiltervalues-rearchitecture-design.md)

**Root:** `C:\src\Esatto.Packages\Esatto.Umbraco.Backoffice.DictionaryFilterValues\` (the package), `…DictionaryFilterValues.Tests\` (new).

---

## Task 1 — csproj + TS toolchain scaffold

**Files:**
- Modify: `Esatto.Umbraco.Backoffice.DictionaryFilterValues.csproj`
- Create: `Client/package.json`, `Client/tsconfig.json`, `Client/vite.config.ts`, `Client/.gitignore`, `Client/public/umbraco-package.json`

- [ ] Flip `AutoPushToFeed` default to `false`; update the comment to match CustomEditors ("Off by default so a local `dotnet pack` never publishes. CI opts in with `-p:AutoPushToFeed=true`").
- [ ] Change `<PackageReference Include="Umbraco.Cms.Core" Version="17.0.0" />` → `<PackageReference Include="Umbraco.Cms.Web.Common" Version="17.0.0" />` (min-only; pulls Management API + auth surface). Keep `FrameworkReference Microsoft.AspNetCore.App`.
- [ ] Add `BuildBackofficeClient` target + `<Content Remove="Client\**" />` + `<None Include="Client\public\umbraco-package.json" Pack="false" />`, copied verbatim from `CustomEditors.csproj`. Add `NoWarn` `CS1591` if needed.
- [ ] Scaffold `Client/` config by copying CustomEditors' `package.json`/`tsconfig.json`/`vite.config.ts`/`.gitignore`; rename to `esatto-umbraco-backoffice-dictionary-filter-values`; set vite `lib.entry=src/bundle.manifests.ts`, `lib.fileName=esatto-umbraco-backoffice-dictionary-filter-values`, `outDir=../wwwroot/App_Plugins/Esatto.Umbraco.Backoffice.DictionaryFilterValues`, `emptyOutDir:true`, `rollupOptions.external:[/^@umbraco/]`.
- [ ] `Client/public/umbraco-package.json`: one `backofficeEntryPoint` (alias `Esatto.Umbraco.Backoffice.DictionaryFilterValues.EntryPoint`, `js` → built bundle). No SectionAlias condition.
- [ ] Verify: `cd Client && npm install` succeeds (deferred build until Task 7 has source).

## Task 2 — Server: cache

**Files:** Create `src/IDictionaryFilterValuesCache.cs`, `src/DictionaryFilterValuesCache.cs`

- [ ] `CachedDictionaryItem` record: `Guid Id, Guid? ParentId, string Name, IReadOnlyList<DictionaryTranslation> Translations, string[] TranslatedIsoCodes` (translation = `record DictionaryTranslation(string Iso, string Value)`).
- [ ] `interface IDictionaryFilterValuesCache { Task<IReadOnlyList<CachedDictionaryItem>> GetAllAsync(); void Invalidate(); }`.
- [ ] Impl: singleton; build once under `SemaphoreSlim` via `IDictionaryItemService.GetDescendantsAsync(null)`; map `IDictionaryItem` → `CachedDictionaryItem` (Name = `ItemKey`; translations where `Value` non-empty; `TranslatedIsoCodes` = isos of non-empty translations). `Invalidate()` nulls the cached list.
- [ ] Verify: `dotnet build` (will fail until controller compiles — defer to Task 3, or stub).

## Task 3 — Server: controller rewrite

**Files:** Rewrite `src/DictionaryFilterValuesController.cs`; create `src/DictionaryFilterValuesFilter.cs` (pure static helper).

- [ ] `DictionaryFilterValuesFilter.Apply(IReadOnlyList<CachedDictionaryItem> items, string? filter, int skip, int take)` → `(IReadOnlyList<resultItem>, long total)`: case-insensitive (`OrdinalIgnoreCase`) match on `Name` OR any translation `Value`; empty filter = all; page after filter.
- [ ] Controller `: ManagementApiControllerBase`, `[VersionedApiBackOfficeRoute("backoffice/dictionary-filter-values")]`, `[ApiExplorerSettings(GroupName="...")]`, `[Authorize(Policy = AuthorizationPolicies.TreeAccessDictionary)]`. Inject `IDictionaryFilterValuesCache`. `[HttpGet] Search(string? filter, int skip=0, int take=100)` → `Ok(new { items = …{ id, name, parent = parentId==null?null:new{id}, translatedIsoCodes }, total })`. Remove `[AllowAnonymous]`, the old route, the recursive walk, and the dead server-filter branch.
- [ ] Verify: `dotnet build` green (0 warnings/errors).

## Task 4 — Server: invalidation + composer

**Files:** Create `src/DictionaryCacheInvalidationHandler.cs`, `src/DictionaryFilterValuesComposer.cs`

- [ ] Handler implements `INotificationHandler<DictionaryItemSavedNotification>` + `<DictionaryItemDeletedNotification>`; both `Handle` call `_cache.Invalidate()`.
- [ ] Composer (`IComposer`): `builder.Services.AddSingleton<IDictionaryFilterValuesCache, DictionaryFilterValuesCache>()`; `builder.AddNotificationHandler<DictionaryItemSavedNotification, DictionaryCacheInvalidationHandler>()` + deleted variant. Mirror `Backoffice.Redirects` composer.
- [ ] Verify: `dotnet build` green.

## Task 5 — Client: types + data source

**Files:** Create `Client/src/types.ts`, `Client/src/map-item.logic.ts`, `Client/src/dictionary-filter-values-collection.server.data-source.ts`, `Client/src/api/dictionary-filter-values.service.ts`

- [ ] `api/dictionary-filter-values.service.ts`: hand-written service calling `/umbraco/management/api/v1/backoffice/dictionary-filter-values` via the shared `umbHttpClient`, declaring `security:[{scheme:'bearer',type:'http'}]`, `query:{filter,skip,take}`.
- [ ] `map-item.logic.ts`: pure `mapItem(m)` → `{ entityType:'dictionary', unique:m.id, name:m.name ?? '', parentUnique:m.parent?.id ?? null, translatedIsoCodes:m.translatedIsoCodes ?? [] }`.
- [ ] Data source implements `UmbCollectionDataSource`; `getCollection(query)` → `tryExecute(host, service.search({query}))`; on data → `{ data:{ items:data.items.map(mapItem), total:data.total } }`; else `{ error }`.
- [ ] Verify: `cd Client && npx tsc --noEmit` (after Task 6/7 supply remaining imports).

## Task 6 — Client: repository + manifest

**Files:** Create `Client/src/dictionary-filter-values-collection.repository.ts`, `Client/src/manifest.ts`

- [ ] Repository `extends UmbRepositoryBase` (`@umbraco-cms/backoffice/repository`); ctor news up the data source; `async requestCollection(filter){ return this.#source.getCollection(filter); }`.
- [ ] `manifest.ts`: repository manifest `{ type:'repository', alias:'Esatto.Repository.DictionaryFilterValues.Collection', name, api:()=>import('./dictionary-filter-values-collection.repository.js') }`.

## Task 7 — Client: entry-point swap + bundle; delete old shim

**Files:** Create `Client/src/entry-point.logic.ts`, `Client/src/bundle.manifests.ts`; delete `wwwroot/App_Plugins/Esatto.Umbraco.Backoffice.DictionaryFilterValues/dictionary-filter-values.js` and the hand-written `wwwroot/.../umbraco-package.json` (now emitted by vite).

- [ ] `entry-point.logic.ts`: pure `swapDictionaryCollectionRepository(registry, ourRepoAlias)` — idempotent guard; ensure our repo registered; observe/`getByAlias('Umb.Collection.Dictionary')`; on present: capture original, `unregister`, `register({...original, meta:{...original.meta, repositoryAlias: ourRepoAlias}})`. `restore(registry)` for onUnload. No-op if never present.
- [ ] `bundle.manifests.ts`: `export const manifests = [...repository manifest]`; `export const onInit = (host, registry) => swapDictionaryCollectionRepository(registry, 'Esatto.Repository.DictionaryFilterValues.Collection')`; `export const onUnload = (host, registry) => restore(registry)`.
- [ ] `cd Client && npm run build` → emits bundle + `umbraco-package.json` into wwwroot. Verify old `dictionary-filter-values.js` is gone.

## Task 8 — C# tests

**Files:** Create `…DictionaryFilterValues.Tests/…csproj` (copy CustomEditors.Tests), `DictionaryFilterValuesFilterTests.cs`, `DictionaryFilterValuesCacheTests.cs`, `DictionaryCacheInvalidationHandlerTests.cs`

- [ ] Filter tests: name match, **translation-value match**, case-insensitivity, empty-filter dump, paging + total.
- [ ] Cache tests: stub `IDictionaryItemService` returning a small tree → flatten correctness, parentId/translation capture, `Invalidate()` forces rebuild (stub call count).
- [ ] Handler tests: both notifications call `Invalidate()`; idempotent.
- [ ] Verify: `dotnet test …DictionaryFilterValues.Tests` green.

## Task 9 — vitest tests

**Files:** Create `Client/src/entry-point.logic.test.ts`, `Client/src/map-item.logic.test.ts`

- [ ] entry-point: fake registry (spies) → idempotency; repo registered before swap; swapped manifest carries our `repositoryAlias` + otherwise-identical meta; `restore` re-registers original.
- [ ] map-item: parent→parentUnique null handling; iso passthrough; entityType constant.
- [ ] Verify: `cd Client && npm test` green; `npx tsc --noEmit` exit 0.

## Task 10 — Full verify + README

**Files:** Modify `README.md`

- [ ] Update "How it works" to describe the authenticated collection-repository override (remove fetch-patch/anonymous language). Keep compatibility table 17.x. Leave an images placeholder section (assets pending from user).
- [ ] `dotnet build` (package, runs npm build) green; `dotnet test` green; `npm test` + `npx tsc --noEmit` green.
- [ ] Final adversarial review pass over the new code (workflow): auth present, no `window.fetch`/anonymous left, swap idempotent + restores, response shape matches the built-in mapper, no dead code.

## Notes
- No git commit/tag/push/pack until the user approves a release (standing rule).
- The package stays net10/Umbraco 17+; this is intentional (Management API base).
