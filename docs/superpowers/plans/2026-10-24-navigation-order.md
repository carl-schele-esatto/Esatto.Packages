# NavigationOrder Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** Ship `Esatto.Umbraco.Backoffice.NavigationOrder` 1.0.0. Admins drag the backoffice top-bar sections and each section's tabs into a new order, which is saved on drop and applied for every user.

**Architecture:** A Management API controller stores one order (a sections list plus one tab list per section) in its own NPoco table. A `backofficeEntryPoint` turns the order into manifest weights and re-registers the affected `section`/`dashboard`/`sectionView` manifests. Document-level, capture-phase drag and keyboard listeners recognise Umbraco's `uui-tab`s through `composedPath()` and save each move.

**Tech Stack:** .NET 10, Umbraco 18 (Api.Management, Infrastructure/NPoco), xUnit + NSubstitute; TypeScript + Vite + Lit (`@umbraco-cms/backoffice` ^18), vitest.

**Spec:** `docs/superpowers/specs/2026-10-24-navigation-order-design.md`

## Global Constraints

- Package ID / folder / RootNamespace / AssemblyName: `Esatto.Umbraco.Backoffice.NavigationOrder`. Tests: `Esatto.Umbraco.Backoffice.NavigationOrder.Tests`.
- Mirror `Esatto.Umbraco.Backoffice.AICosts` for csproj shape (Razor SDK, `StaticWebAssetBasePath=/`, client build target, AutoPush block, security overrides, source-code link: `PackageProjectUrl` = `https://github.com/carl-schele-esatto/Esatto.Packages/tree/main/Esatto.Umbraco.Backoffice.NavigationOrder`, `RepositoryUrl` = repo root, `RepositoryType` git), and for `Client/` (package.json scripts, tsconfig, vite lib build into `../wwwroot/App_Plugins/Esatto.Umbraco.Backoffice.NavigationOrder`, `public/umbraco-package.json`).
- Umbraco references: `Umbraco.Cms.Api.Management`, `Umbraco.Cms.Infrastructure`, `Umbraco.Cms.Web.Common` at `18.0.0`.
- App_Plugins path `/App_Plugins/Esatto.Umbraco.Backoffice.NavigationOrder/`; entry file `navigation-order.js`; texts `lang/en.js`, `lang/sv.js`.
- Extension aliases: `Esatto.Umbraco.Backoffice.NavigationOrder.EntryPoint`, `….Localization.En`, `….Localization.Sv`.
- API route `esatto/navigation-order` → `/umbraco/management/api/v1/esatto/navigation-order`. GET: `AuthorizationPolicies.BackOfficeAccess`; PUT: `AuthorizationPolicies.RequireAdminAccess`. No JSON object keyed by alias.
- Validation limits: alias/section ≤ 255 chars and non-blank; ≤ 200 aliases per list; ≤ 100 tab lists.
- Weights for a saved list: `(n - index) * 10`. Unsaved lists: original weight.
- Keyboard: Alt+Shift+ArrowLeft / Alt+Shift+ArrowRight. Texts and their Swedish translations: exactly as in the spec's "Texts" table, keys `esattoNavigationOrder_moved|saving|saveFailed`.
- Modules under vitest (`src/core/**`, `src/apply/applier.ts`, `src/apply/cache.ts`, `src/drag/tab-target.ts`, `src/drag/save-queue.ts`) must not import from `@umbraco-cms/*` at runtime (type-only imports are fine). Umbraco-dependent glue lives in other files.
- **Commits need Carl's go-ahead** (repo CLAUDE.md). Commit steps below assume it was given for this branch, `feature/navigation-order`; never push, tag or publish.

## Review Focus

1. **An uninstalled package's alias in a saved list:** skipped when applying, kept in storage, and back in its saved place when the package returns. Tested in Task 4 (`effectiveOrder`).
2. **An admin without access to some sections drags among the ones they see:** the hidden sections keep their relative places, because the move works on the full effective list, not the visible tabs. Tested in Task 4 (`moveAlias` on the full list) and Task 5 (`effectiveList`).
3. **Two quick drops, or a failed save:** saves run one at a time, only the latest pending order is sent, and a failure restores the last *saved* order, not the one before the last drop. Tested in Task 6 (`SaveQueue`).
4. **`localStorage` unavailable, or holding junk:** the order still comes from the server and nothing throws. Tested in Task 5 (`cache`).
5. **A dashboard in several sections (`oneOf`, or no section condition):** gets one deterministic weight and applying settles — `replace` isn't called again on the next registry change. Tested in Task 4 (`targetWeights`) and Task 5 (applier convergence).

---

### Task 1: Package scaffold, order model and validation

**Files:**
- Create: `Esatto.Umbraco.Backoffice.NavigationOrder/Esatto.Umbraco.Backoffice.NavigationOrder.csproj` (mirror AICosts; Description: "Drag the Umbraco backoffice sections and each section's tabs into the order you want. Admins drag them right in the top bar and tab bars; the order is saved on drop and applies to every user."; PackageTags `umbraco;umbraco-marketplace;umbraco-18;backoffice;sections;dashboards;tabs;navigation;order;drag-and-drop`; drop the Umbraco.AI references and the ImageSharp audit block unless restore warns).
- Create: `Esatto.Umbraco.Backoffice.NavigationOrder/icon.png` (copy of `Esatto.Umbraco.Backoffice.TreeDragAndDrop/icon.png`), `README.md` (one-line stub; written in Task 8).
- Create: `Esatto.Umbraco.Backoffice.NavigationOrder/src/NavigationOrderSettings.cs`, `src/NavigationOrderValidator.cs`
- Create: `Esatto.Umbraco.Backoffice.NavigationOrder.Tests/Esatto.Umbraco.Backoffice.NavigationOrder.Tests.csproj` (mirror AICosts.Tests, plus `Microsoft.Extensions.TimeProvider.Testing` 9.10.0), `NavigationOrderValidatorTests.cs`

**Interfaces:**
- Produces:
  - `public sealed record TabOrder(string Section, IReadOnlyList<string> Aliases);`
  - `public sealed record NavigationOrderSettings(IReadOnlyList<string>? Sections, IReadOnlyList<TabOrder> Tabs) { public static NavigationOrderSettings Empty { get; } }` (`Empty` = `new(null, [])`)
  - `public sealed record StoredNavigationOrder(NavigationOrderSettings Settings, DateTime? UpdatedUtc, string? UpdatedBy) { public static StoredNavigationOrder Empty { get; } }`
  - `public static class NavigationOrderValidator { public const int MaxAliasLength = 255; public const int MaxAliasesPerList = 200; public const int MaxTabLists = 100; public static IReadOnlyList<string> Validate(NavigationOrderSettings settings); }`

- [ ] **Step 1: Write the failing tests** in `NavigationOrderValidatorTests`:

```csharp
[Fact] public void Empty_settings_are_valid() => Assert.Empty(NavigationOrderValidator.Validate(NavigationOrderSettings.Empty));

[Fact] public void A_full_order_is_valid() => Assert.Empty(NavigationOrderValidator.Validate(new(
    ["Umb.Section.Content", "Umb.Section.Forms"],
    [new TabOrder("Umb.Section.Settings", ["Umb.Dashboard.Welcome", "Esatto.Umbraco.Backoffice.ScheduledTasks.Dashboard"])])));

[Fact] public void Every_problem_is_reported()
{
    var errors = NavigationOrderValidator.Validate(new(
        ["Umb.Section.Content", " ", "Umb.Section.Content", new string('a', 256)],
        [new TabOrder("Umb.Section.Settings", ["A", "A"]), new TabOrder("Umb.Section.Settings", ["B"]), new TabOrder("", ["C"])]));
    Assert.Contains("sections[1] is blank.", errors);
    Assert.Contains("Duplicate alias 'Umb.Section.Content' in sections.", errors);
    Assert.Contains("sections[3] is longer than 255 characters.", errors);
    Assert.Contains("Duplicate alias 'A' in tabs[0].", errors);
    Assert.Contains("Duplicate section 'Umb.Section.Settings' in tabs.", errors);
    Assert.Contains("tabs[2].section is blank.", errors);
}

[Fact] public void Lists_and_tab_lists_are_capped()
{
    var tooMany = Enumerable.Range(0, 201).Select(i => $"A{i}").ToArray();
    var tabLists = Enumerable.Range(0, 101).Select(i => new TabOrder($"S{i}", ["A"])).ToArray();
    var errors = NavigationOrderValidator.Validate(new(tooMany, tabLists));
    Assert.Contains("sections has more than 200 aliases.", errors);
    Assert.Contains("tabs has more than 100 sections.", errors);
}
```

- [ ] **Step 2: Run to see them fail:** `dotnet test Esatto.Umbraco.Backoffice.NavigationOrder.Tests` → compile errors (types missing).
- [ ] **Step 3: Implement** the records and `Validate` (error strings exactly as asserted; a null alias counts as blank).
- [ ] **Step 4: Run tests** → all pass. `dotnet build Esatto.Umbraco.Backoffice.NavigationOrder` succeeds (there's no `Client/package.json` yet, so the npm target is skipped).
- [ ] **Step 5: Commit** `NavigationOrder: scaffold package, order model and validation`.

---

### Task 2: Storage — table, migration, store

**Files:**
- Create: `src/NavigationOrderEntity.cs`, `src/NavigationOrderRows.cs`, `src/NavigationOrderStore.cs`, `src/NavigationOrderMigrations.cs`
- Test: `NavigationOrderRowsTests.cs`, `NavigationOrderStoreTests.cs`

**Interfaces:**
- Consumes: Task 1 records.
- Produces:
  - `NavigationOrderEntity` — `[TableName("NavigationOrder")]`, `[PrimaryKey("id", AutoIncrement = true)]`, `[ExplicitColumns]`; `Id` (`id`, identity), `ListKey` (`listKey`, `[Length(255)]`, unique index `IX_NavigationOrder_listKey`), `AliasesJson` (`aliasesJson`, `SpecialDbTypes.NVARCHARMAX`), `UpdatedUtc` (`updatedUtc`), `UpdatedBy` (`updatedBy`, `[Length(255)]`, nullable). `public const string TableName = "NavigationOrder";`
  - `public static class NavigationOrderRows { public const string SectionsKey = "sections"; public const string TabsKeyPrefix = "tabs:"; public static StoredNavigationOrder ToStored(IEnumerable<NavigationOrderEntity> rows); public static IReadOnlyList<NavigationOrderEntity> ToEntities(NavigationOrderSettings settings, DateTime updatedUtc, string? updatedBy); }`
  - `public interface INavigationOrderStore { StoredNavigationOrder Get(); StoredNavigationOrder Save(NavigationOrderSettings settings, string? updatedBy); }`
  - `public sealed class NavigationOrderStore(IScopeProvider scopes, TimeProvider time) : INavigationOrderStore` (`Umbraco.Cms.Infrastructure.Scoping.IScopeProvider`)
  - `NavigationOrderMigrationPlan` (`MigrationPlan("Esatto.Umbraco.Backoffice.NavigationOrder")`, `From(string.Empty).To<AddNavigationOrderTableMigration>("add-navigation-order-table")`), `AddNavigationOrderTableMigration : AsyncMigrationBase` (creates the table if missing), `RunNavigationOrderMigration : INotificationAsyncHandler<UmbracoApplicationStartingNotification>` (copy of `RunRedirectsMigration`).

- [ ] **Step 1: Write the failing tests.**

`NavigationOrderRowsTests`:
- `Rows_round_trip`: `ToEntities(settings, t, "Carl")` then `ToStored(...)` returns equal sections and tab lists (compare with `Assert.Equal` on sequences), `UpdatedUtc == t`, `UpdatedBy == "Carl"`; the entity keys are `"sections"` and `"tabs:Umb.Section.Settings"`.
- `Default_sections_write_no_row`: `ToEntities(new(null, [tab]), …)` has exactly one row, key `"tabs:…"`; `ToStored` of it gives `Settings.Sections == null`.
- `Unreadable_rows_are_skipped`: rows with `AliasesJson = "not json"`, `ListKey = "other"`, and `ListKey = "tabs:"` (empty section) are ignored; a valid sections row next to them still loads.
- `Latest_row_names_who_saved`: two rows with different `UpdatedUtc`; `ToStored` reports the later `UpdatedUtc` and its `UpdatedBy`. No rows → `StoredNavigationOrder.Empty`.

`NavigationOrderStoreTests` (substitute `IUmbracoDatabase`/`IScope`/`IScopeProvider` exactly like `ScheduledTaskSettingsStoreTests`; `FakeTimeProvider` at 2026-10-24 08:00 UTC):
- `Save_replaces_every_row_in_one_scope`: `Save(settings, "Carl")` → `Database.Execute` receives one `Sql` whose text starts with `DELETE` and names `NavigationOrder`, then one `Insert` per list with `UpdatedUtc` = the fake time and `UpdatedBy` "Carl"; `scope.Complete()` called once; the returned value equals `ToStored` of the inserted rows.
- `Get_reads_the_rows`: `Fetch<NavigationOrderEntity>(Arg.Any<Sql>())` returns two rows → `Get()` equals `NavigationOrderRows.ToStored(rows)`.

- [ ] **Step 2: Run** `dotnet test Esatto.Umbraco.Backoffice.NavigationOrder.Tests` → fails to compile.
- [ ] **Step 3: Implement.** JSON via `System.Text.Json` (`string[]`). Read with `Fetch<NavigationOrderEntity>(Sql().SelectAll().From<NavigationOrderEntity>())`; delete with a `DELETE` `Sql` built from `scope.SqlContext` and passed to `Database.Execute`.
- [ ] **Step 4: Run tests** → pass.
- [ ] **Step 5: Commit** `NavigationOrder: table, migration and store`.

---

### Task 3: API controller and registration

**Files:**
- Create: `src/NavigationOrderController.cs`, `src/NavigationOrderDtos.cs`, `src/NavigationOrderComposer.cs`, `src/NavigationOrderServiceCollectionExtensions.cs`
- Test: `ControllerTests.cs`, `RegistrationTests.cs`

**Interfaces:**
- Consumes: `INavigationOrderStore`, `NavigationOrderValidator`, records from Task 1.
- Produces:
  - `public sealed record TabOrderDto(string Section, IReadOnlyList<string> Aliases);`
  - `public sealed record NavigationOrderRequest(IReadOnlyList<string>? Sections, IReadOnlyList<TabOrderDto>? Tabs) { public NavigationOrderSettings ToSettings(); }` (null `Tabs` → empty)
  - `public sealed record NavigationOrderResponse(IReadOnlyList<string>? Sections, IReadOnlyList<TabOrderDto> Tabs, DateTime? UpdatedUtc, string? UpdatedBy) { public static NavigationOrderResponse From(StoredNavigationOrder stored); }`
  - `NavigationOrderController : ManagementApiControllerBase` — `[ApiController]`, `[VersionedApiBackOfficeRoute("esatto/navigation-order")]`, `[ApiExplorerSettings(GroupName = "Esatto navigation order")]`, `[Authorize(Policy = AuthorizationPolicies.BackOfficeAccess)]`; ctor `(INavigationOrderStore store, IBackOfficeSecurityAccessor security)`; `[HttpGet] IActionResult Get()`; `[HttpPut] [Authorize(Policy = AuthorizationPolicies.RequireAdminAccess)] IActionResult Save([FromBody] NavigationOrderRequest request)`.
  - `public static IServiceCollection AddNavigationOrder(this IServiceCollection services)` — idempotent; `TryAddSingleton(TimeProvider.System)`, `TryAddSingleton<INavigationOrderStore, NavigationOrderStore>()`.
  - `NavigationOrderComposer : IComposer` — `builder.Services.AddNavigationOrder();` and `builder.AddNotificationAsyncHandler<UmbracoApplicationStartingNotification, RunNavigationOrderMigration>();`

- [ ] **Step 1: Write the failing tests.**

`ControllerTests`:
- `Get_returns_the_stored_order`: store returns a stored order → `OkObjectResult` whose value equals `NavigationOrderResponse.From(stored)`.
- `Save_rejects_an_invalid_order_without_storing`: request with a duplicate section alias → `BadRequestObjectResult` holding a `ProblemDetails` whose `Detail` contains `"Duplicate alias"`; `store.DidNotReceiveWithAnyArgs().Save(default!, default)`.
- `Save_stores_with_the_current_user`: security accessor's current user name "Carl" → `store.Received(1).Save(Arg.Is<NavigationOrderSettings>(s => s.Sections!.SequenceEqual(new[] { "B", "A" })), "Carl")`; result is OK with the response built from the store's return value.
- `Only_admins_can_save`: reflection — `Save` has `AuthorizeAttribute` with `Policy == AuthorizationPolicies.RequireAdminAccess`; the class has one with `AuthorizationPolicies.BackOfficeAccess`.

`RegistrationTests`:
- `AddNavigationOrder_is_idempotent`: called twice → exactly one `INavigationOrderStore` descriptor, and it resolves (with a substituted `IScopeProvider` registered) to `NavigationOrderStore`.

- [ ] **Step 2: Run** → fails to compile.
- [ ] **Step 3: Implement.** Bad request: `BadRequest(new ProblemDetails { Title = "Invalid navigation order", Detail = string.Join(" ", errors), Status = 400 })`.
- [ ] **Step 4: Run tests** → pass; `dotnet build Esatto.Umbraco.Backoffice.NavigationOrder` → 0 warnings from this project.
- [ ] **Step 5: Commit** `NavigationOrder: API and composer`.

---

### Task 4: Client scaffold and ordering core

**Files:**
- Create: `Client/package.json` (name `esatto-umbraco-backoffice-navigation-order`, devDependencies as AICosts), `Client/tsconfig.json`, `Client/vite.config.ts` (entries: `navigation-order` → `src/entry-point.ts`, `lang/en` → `src/lang/en.ts`, `lang/sv` → `src/lang/sv.ts`), `Client/public/umbraco-package.json` (id `Esatto.Umbraco.Backoffice.NavigationOrder`, name "Esatto Umbraco Backoffice Navigation Order", version 1.0.0, `allowTelemetry: false`; the entry point, plus the two `localization` extensions shaped like ScheduledTasks'), and a placeholder `src/entry-point.ts` exporting an empty `onInit` so the build runs.
- Create: `Client/src/core/types.ts`, `Client/src/core/groups.ts`, `Client/src/core/order.ts`
- Test: `Client/test/groups.test.ts`, `Client/test/order.test.ts`

**Interfaces:**
- Produces (`core/types.ts`):
  - `type ManagedType = 'section' | 'dashboard' | 'sectionView'`; `const MANAGED_TYPES: readonly ManagedType[]`
  - `interface ConditionConfig { alias: string; match?: string; oneOf?: string[] }`
  - `interface ManagedManifest { type: ManagedType; alias: string; name?: string; weight?: number; conditions?: ConditionConfig[]; meta?: { label?: string; pathname?: string }; [key: string]: unknown }`
  - `interface TabOrder { section: string; aliases: string[] }`; `interface NavigationOrderData { sections: string[] | null; tabs: TabOrder[] }`; `const EMPTY_ORDER: NavigationOrderData`
  - `type TabType = 'dashboard' | 'sectionView'`; `type Bar = { kind: 'sections' } | { kind: 'tabs'; section: string; type: TabType }`
  - `type WeightLookup = (alias: string) => number`
- Produces (`core/groups.ts`): `const SECTION_ALIAS_CONDITION = 'Umb.Condition.SectionAlias'`; `sectionsOfTab(manifest: ManagedManifest): string[] | 'all'`; `membersOf(manifests: ManagedManifest[], bar: Bar): ManagedManifest[]` (sections bar → all `section` manifests; tabs bar → manifests of `bar.type` whose `sectionsOfTab` is `'all'` or includes `bar.section`).
- Produces (`core/order.ts`):
  - `defaultOrder(members: ManagedManifest[], original: WeightLookup): string[]` — original weight desc, input order on ties.
  - `effectiveOrder(members: ManagedManifest[], saved: readonly string[] | null | undefined, original: WeightLookup): string[]`
  - `savedListOf(order: NavigationOrderData, bar: Bar): string[] | null`
  - `targetWeights(manifests: ManagedManifest[], order: NavigationOrderData, original: WeightLookup): Map<string, number>` — one entry per manifest (spec: "Weights", "Tab lists").
  - `moveAlias(list: readonly string[], alias: string, target: string, position: 'before' | 'after'): string[]` — returns `list` unchanged (a copy) when `alias === target` or either is missing.
  - `withList(order: NavigationOrderData, bar: Bar, list: string[], otherTypeList: string[] = []): NavigationOrderData` — returns a new order. A sections bar sets `sections = list`. A tabs bar sets that section's tab list to dashboards then views: `[...list, ...otherTypeList]` for a dashboard bar, `[...otherTypeList, ...list]` for a view bar (the caller passes the other type's effective list).

- [ ] **Step 1: Write the failing tests** (fixtures: `section(alias, weight?)`, `dashboard(alias, sections: string | string[] | null, weight?)` helpers in the test file).

`groups.test.ts`:
- `match`, `oneOf` and both together give the union; no SectionAlias condition gives `'all'`; other conditions are ignored.
- `membersOf` for `{ kind: 'tabs', section: 'S', type: 'dashboard' }` returns S-dashboards and `'all'` dashboards, not section views or other sections' dashboards.

`order.test.ts`:
- `defaultOrder`: weights `[10, undefined, 1000, 10]` → highest first, the two 10s in input order, undefined last as 0.
- `effectiveOrder` with saved `['Forms', 'Gone', 'Content']` over members `Content(1000), Media(900), Forms(0)` → `['Forms', 'Content', 'Media']` (Review Focus 1: `'Gone'` skipped; when a `Gone` member is added back it lands between Forms and Content).
- `targetWeights` with only `sections` saved: every section gets `(n - index) * 10` in effective order; dashboards keep the lookup's original weight.
- `targetWeights` with nothing saved returns every manifest's original weight (restoring after a reset).
- `targetWeights` for a dashboard with no section condition, when Settings and Content both have saved tab lists: its weight comes from the section that comes first in the effective top-bar order (Review Focus 5), and calling it twice gives the same map.
- `targetWeights` orders dashboards and section views of one section independently from the same saved list.
- `moveAlias(['A','B','C','D'], 'D', 'B', 'before')` → `['A','D','B','C']`; `(…, 'A', 'C', 'after')` → `['B','C','A','D']`; with a hidden member: full list `['A','H','B','C']`, move `'C'` before `'B'` → `['A','H','C','B']` (Review Focus 2).
- `withList` for a dashboards bar keeps the views after the new dashboard order.

- [ ] **Step 2: Run** `cd Esatto.Umbraco.Backoffice.NavigationOrder/Client && npm install && npm test` → fails (modules missing).
- [ ] **Step 3: Implement** `types.ts`, `groups.ts`, `order.ts`. Algorithm for `targetWeights`:

```
result = new Map()
sections = membersOf(manifests, {kind:'sections'})
headerOrder = effectiveOrder(sections, order.sections, original)
if order.sections: headerOrder.forEach((a, i) => result.set(a, (n - i) * 10))
for section of headerOrder (then any order.tabs sections not in headerOrder are ignored):
  saved = savedListOf(order, {kind:'tabs', section, type:'dashboard'})   // same list for both types
  if !saved: continue
  for type of ['dashboard', 'sectionView']:
    members = membersOf(manifests, {kind:'tabs', section, type})
    list = effectiveOrder(members, saved, original)
    list.forEach((a, i) => { if (!result.has(a)) result.set(a, (list.length - i) * 10) })
for every manifest without an entry: result.set(alias, original(alias))
```

- [ ] **Step 4: Run** `npm test` → pass; `npm run build` → writes `navigation-order.js` and `umbraco-package.json` to wwwroot.
- [ ] **Step 5: Commit** `NavigationOrder: client scaffold and ordering core`.

---

### Task 5: Applying the order at startup

**Files:**
- Create: `Client/src/apply/applier.ts`, `Client/src/apply/registry-source.ts`, `Client/src/apply/cache.ts`, `Client/src/api.ts`, `Client/src/navigation-order.ts` (the shared singleton)
- Modify: `Client/src/entry-point.ts`
- Test: `Client/test/applier.test.ts`, `Client/test/cache.test.ts`

**Interfaces:**
- Consumes: Task 4 core.
- Produces:
  - `interface ManifestSource { current(): ManagedManifest[]; subscribe(listener: () => void): () => void; replace(manifest: ManagedManifest, weight: number): void }`
  - `class NavigationOrderApplier { constructor(source: ManifestSource, schedule?: (task: () => void) => void /* default queueMicrotask */); start(): void; get order(): NavigationOrderData; setOrder(order: NavigationOrderData): void /* applies synchronously */; originalWeight(alias: string): number; effectiveList(bar: Bar): string[]; manifests(): ManagedManifest[] }` (`effectiveList` = `effectiveOrder(membersOf(manifests(), bar), savedListOf(order, bar), originalWeight)`)
  - `createRegistrySource(): ManifestSource` — subscribes to `umbExtensionsRegistry.extensions`, keeps the latest list filtered to `MANAGED_TYPES`; `replace` = `unregister(alias)` then `register({ ...manifest, weight })`.
  - `readCachedOrder(storage?: Pick<Storage, 'getItem'>): NavigationOrderData | null`, `writeCachedOrder(order: NavigationOrderData, storage?: Pick<Storage, 'setItem'>): void`, `isNavigationOrderData(value: unknown): value is NavigationOrderData`; key `'esatto.navigationOrder.v1'`; default storage `globalThis.localStorage` read inside try/catch.
  - `fetchNavigationOrder(): Promise<NavigationOrderData>` and `saveNavigationOrder(order: NavigationOrderData): Promise<NavigationOrderData>` via `umbHttpClient` with `security: [{ scheme: 'bearer', type: 'http' }]` (as AICosts' `api.ts`); both throw on a non-2xx response.
  - `navigation-order.ts`: `export const navigationOrder = new NavigationOrderApplier(createRegistrySource());`

- [ ] **Step 1: Write the failing tests** (`applier.test.ts` uses a fake source: an array of manifests, a listener set, and a `replace` that records calls, swaps in `{ ...m, weight }`, moves it to the end like the registry does, and notifies listeners synchronously).
- `captures originals before changing anything`: start, `setOrder({ sections: ['B','A'], tabs: [] })` → `replace` called for both; `originalWeight('A')` still returns A's registered weight.
- `applies to manifests registered later`: after `setOrder`, add section `C` to the fake and notify; after `await Promise.resolve()` C has been replaced with weight 10 (last).
- `settles` (Review Focus 5): after `setOrder`, notifying again without changes makes no further `replace` calls; a manifest whose weight already equals its target isn't replaced.
- `setOrder(EMPTY_ORDER) restores original weights`.
- `effectiveList({kind:'sections'})` lists every section, including ones with no permission (the fake knows nothing of permissions) — Review Focus 2.
- Re-entrancy: listeners fired from inside `replace` don't start a nested apply (recorded `replace` calls equal the number of changed manifests).

`cache.test.ts` (Review Focus 4):
- round trip through a fake storage;
- `getItem` that throws → `null`; stored `"{"` or `{"sections":5}` → `null`; `setItem` that throws → no exception.

- [ ] **Step 2: Run** `npm test` → fails.
- [ ] **Step 3: Implement**, then wire `entry-point.ts`:

```ts
export const onInit: UmbEntryPointOnInit = (host) => {
  navigationOrder.start();
  const cached = readCachedOrder();
  if (cached) navigationOrder.setOrder(cached);
  fetchNavigationOrder()
    .then((order) => { navigationOrder.setOrder(order); writeCachedOrder(order); })
    .catch((error) => console.warn('[Esatto NavigationOrder] Could not load the saved order.', error));
  startDragging(host); // Task 7; leave out until then
};
```

- [ ] **Step 4: Run** `npm test` and `npm run build` → pass.
- [ ] **Step 5: Commit** `NavigationOrder: apply the saved order at startup`.

---

### Task 6: Recognising tabs, and the save queue

**Files:**
- Create: `Client/src/drag/tab-target.ts`, `Client/src/drag/save-queue.ts`
- Test: `Client/test/tab-target.test.ts`, `Client/test/save-queue.test.ts`

**Interfaces:**
- Consumes: Task 4 types, `ManagedManifest`.
- Produces:
  - `interface PathNode { localName?: string; id?: string; getAttribute?(name: string): string | null }`
  - `interface TabTarget { tab: PathNode; bar: Bar; alias: string }`
  - `findTabTarget(path: readonly PathNode[], manifests: ManagedManifest[], folderName: (name: string) => string): TabTarget | null` — first `uui-tab` in `path`, then the first `uui-tab-group` after it. Group id `tabs` + tab `data-mark` `section-link:<alias>` → sections bar (the alias must be a known section). Group id `dashboards`/`views` → parse the tab's `href` with `/section/([^/]+)/(dashboard|view)/([^/?#]+)/?$`, find the section whose `meta.pathname` equals group 1, then the member of `{kind:'tabs', section, type}` whose `meta.pathname || folderName(meta.label ?? name ?? alias)` equals group 3. Anything else → `null`.
  - `dropPosition(clientX: number, rect: { left: number; width: number }): 'before' | 'after'` (in `tab-target.ts`; `< left + width / 2` → before).
  - `keyboardStep(event: { key: string; altKey: boolean; shiftKey: boolean; ctrlKey: boolean; metaKey: boolean }): -1 | 0 | 1` (Alt+Shift+ArrowLeft → -1, Alt+Shift+ArrowRight → 1, with Ctrl/Meta held or any other key → 0).
  - `class SaveQueue { constructor(save: (order: NavigationOrderData) => Promise<NavigationOrderData>, apply: (order: NavigationOrderData) => void, onError: (error: unknown) => void); submit(next: NavigationOrderData, current: NavigationOrderData): Promise<void>; get busy(): boolean }`

- [ ] **Step 1: Write the failing tests.**

`tab-target.test.ts` (path nodes are plain objects; `folderName` stub lower-cases and strips spaces):
- a header tab `{ localName: 'uui-tab', getAttribute: data-mark → 'section-link:Umb.Section.Forms' }` inside group `{ localName: 'uui-tab-group', id: 'tabs' }` → `{ bar: {kind:'sections'}, alias: 'Umb.Section.Forms' }`; a path that starts with the tab's inner `<a>` still works.
- a Settings dashboard tab with `href="/umbraco/section/settings/dashboard/scheduled-tasks"` resolves to the dashboard whose `meta.pathname` is `scheduled-tasks` and is in `Umb.Section.Settings`; one without `pathname` matches through `folderName(label)`.
- a `views` group tab with `/view/` resolves to a `sectionView`.
- an unknown `data-mark` alias, an unknown pathname, a tab outside any group, and a `uui-tab` in a group with another id all → `null`.
- `dropPosition(149, {left: 100, width: 100})` → before; `150` → after.
- `keyboardStep` for each combination listed above.

`save-queue.test.ts` (Review Focus 3; `save` returns promises the test resolves or rejects):
- `submit(n1, c0)` calls `apply(n1)` at once and `save(n1)`.
- while `n1` is saving, `submit(n2)` and `submit(n3)`: both applied at once; after `n1` resolves, `save` is called once more, with `n3` only.
- `save(n1)` rejects → `apply(c0)` (the order before the first unsaved change) and `onError` called once; pending `n2`/`n3` are dropped.
- after `n1` succeeds and `n3` fails → `apply` is called with the server's response to `n1`.

- [ ] **Step 2: Run** `npm test` → fails.
- [ ] **Step 3: Implement.**
- [ ] **Step 4: Run** `npm test` → pass.
- [ ] **Step 5: Commit** `NavigationOrder: recognise backoffice tabs; serialised saves`.

---

### Task 7: Drag, drop and keyboard in the backoffice

**Files:**
- Create: `Client/src/drag/drag-controller.ts`, `Client/src/drag/overlay.ts` (insertion line, saving spinner, `aria-live` announcer), `Client/src/lang/en.ts`, `Client/src/lang/sv.ts`
- Modify: `Client/src/entry-point.ts` (call `startDragging(host)`)

**Interfaces:**
- Consumes: `navigationOrder` (Task 5), `findTabTarget`, `dropPosition`, `keyboardStep`, `SaveQueue` (Task 6), `moveAlias`, `withList` (Task 4), `saveNavigationOrder`, `writeCachedOrder`.
- Produces: `startDragging(host: UmbControllerHostElement): void` in `drag-controller.ts`.

Behaviour (spec "Dragging"): consume `UMB_CURRENT_USER_CONTEXT` (`isAdmin`) and `UMB_NOTIFICATION_CONTEXT` from `host`; texts via `new UmbLocalizationController(host)`; `folderName` = `pathFolderName` from `@umbraco-cms/backoffice/utils`. Document listeners in the capture phase: `dragstart` (only when admin and `findTabTarget(event.composedPath(), …)` matches; set `effectAllowed = 'move'`, set data type `application/x-esatto-navigation-order`, dim the tab with inline `opacity: 0.4`), `dragover` (same bar only: `preventDefault()`, `dropEffect = 'move'`, show the insertion line at the target's left/right edge with `position: fixed`), `drop` (compute `moveAlias(navigationOrder.effectiveList(bar), …)`, `withList`, then `queue.submit`), `dragend`/Esc (clean up). `keydown`: when `keyboardStep` is non-zero on a focused recognised tab, take its visible neighbour in the same `uui-tab-group` (DOM order; -1 = previous, 1 = next), and submit `moveAlias(effectiveList(bar), alias, neighbour, step < 0 ? 'before' : 'after')` the same way as a drop. Then it announces `esattoNavigationOrder_moved` with name, 1-based position among the visible tabs, and their count, and gives focus back to the tab after the re-render. Spinner: a fixed-position `uui-loader-circle` (with `esattoNavigationOrder_saving` as its label) over the moved tab while `queue.busy`. On save success: `writeCachedOrder(saved)`. `onError`: `peek('danger', { data: { message: localize.term('esattoNavigationOrder_saveFailed') } })`.

- [ ] **Step 1: Write `lang/en.ts` and `lang/sv.ts`** — `export default { esattoNavigationOrder: { moved: (name, position, total) => …, saving: '…', saveFailed: '…' } }` with the spec's exact texts.
- [ ] **Step 2: Implement `overlay.ts` and `drag-controller.ts`; wire the entry point.**
- [ ] **Step 3: Run** `npm test` (unchanged suites still pass) and `npm run build` (tsc clean).
- [ ] **Step 4: Commit** `NavigationOrder: drag, drop and keyboard moving in the bars`.

---

### Task 8: Package, smoke test, README

**Files:**
- Modify: `Esatto.Umbraco.Backoffice.NavigationOrder/README.md` (sibling layout: intro, screenshot(s) under `docs/`, "How it works", "Who can change the order", "Keyboard", "Install", "Compatibility" (Umbraco 18.2.1 verified), "Limitations" (spec "Out of scope" + "Risks")).
- Create: `Esatto.Umbraco.Backoffice.NavigationOrder/docs/*.png` (screenshots)
- Test site (local only, not committed here): `c:\src\umbracokalaset26`

- [ ] **Step 1: Full verification:** `dotnet test Esatto.Umbraco.Backoffice.NavigationOrder.Tests`, `npm test` in `Client/`, `dotnet build -c Release Esatto.Umbraco.Backoffice.NavigationOrder` → all green.
- [ ] **Step 2: Pack a preview** to the local feed: `dotnet pack Esatto.Umbraco.Backoffice.NavigationOrder -c Release -o artifacts` → `Esatto.Umbraco.Backoffice.NavigationOrder.1.0.0-preview.0.N.nupkg`.
- [ ] **Step 3: Install in the test site:** add `PackageVersion`/`PackageReference` for the preview, `dotnet restore --source https://api.nuget.org/v3/index.json --source C:\src\Esatto.Packages\artifacts`, run the site. Check the log for the `Esatto.Umbraco.Backoffice.NavigationOrder` migration and that the `NavigationOrder` table exists.
- [ ] **Step 4: Manual checks (Carl, in a browser) — record the results in the PR/hand-over:**
  1. As admin: drag Forms before Media in the top bar; it moves, the spinner shows briefly, reload keeps it, and a non-admin user sees the same order.
  2. Settings: drag Scheduled tasks to first; reload keeps it.
  3. Alt+Shift+←/→ on a focused section tab moves it and is announced (screen reader or the live region in DevTools).
  4. As a non-admin: dragging a tab does nothing special; `PUT` returns 403.
  5. Stop the site mid-drag-session (or block the request in DevTools) and drop: the order returns and the danger notification shows.
- [ ] **Step 5: Screenshots** (top bar mid-drag with the insertion line; Settings tabs) into `docs/`, referenced from the README with raw GitHub URLs like AICosts.
- [ ] **Step 6: Commit** `NavigationOrder 1.0.0: README and screenshots`. Tag and push are Carl's.
