# TreeDragAndDrop — Design

**Date:** 2026-10-14
**Package:** `Esatto.Umbraco.Backoffice.TreeDragAndDrop` (new, public, nuget.org)
**Repo dir:** `Esatto.Umbraco.Backoffice.TreeDragAndDrop/` + `Esatto.Umbraco.Backoffice.TreeDragAndDrop.Tests/`
**Supersedes:** `Esatto.Umbraco.Backoffice.ContentTreeDragAndDrop` (2.0.0), `Esatto.Umbraco.Backoffice.MediaTreeDragAndDrop` (1.0.1)
**Test site:** `c:\src\umbracokalaset26` (Umbraco 18; already has both legacy packages installed)

## Summary

One package that adds drag-and-drop and keyboard "grab & place" to the Umbraco 17/18 backoffice
**Content**, **Media** and **Dictionary** (Translation section) trees. Each tree can be switched
off in `appsettings.json`. A shared client core replaces the two near-identical legacy packages,
and Dictionary support is new.

Delivered in four independently releasable steps:

| Step | Release | Scope |
|------|---------|-------|
| 1 | **1.0.0** | Shared core + Dictionary adapter + appsettings toggles + manifest reader |
| 2 | **1.1.0** | Content adapter (parity with ContentTreeDragAndDrop 2.0) |
| 3 | **1.2.0** | Media adapter (Media gains keyboard + spinner) |
| 4 | — | Final deprecation releases of the two legacy packages; consumers switched |

Step 1 is designed in full. Steps 2–4 are designed at the level needed to keep step 1's
abstractions honest; each gets its own plan.

### Out of scope

- A backoffice settings UI (appsettings only; restart to apply).
- Sorting dictionary items — the Management API has no dictionary sort endpoint and Umbraco
  always lists dictionary siblings alphabetically.
- Multi-node drag, cross-tree drag (e.g. media → content).
- Extracting the core into its own npm/NuGet package.

## Background

- **ContentTreeDragAndDrop 2.0** (TS + Vite + Lit, `Client/`, vitest) is the reference
  implementation: `attachShadow` patch + observer, a hidden host element mounted in `<umb-app>`,
  document-level capture-phase drag listeners, three-zone drop geometry, cycle guard,
  optimistic same-parent reorder with rollback, move spinner, keyboard reducer, `aria-live`
  announcements.
- **MediaTreeDragAndDrop 1.0.1** is the pre-2.0 single-file JS port of the same design with
  `document` → `media`. The 2026-06-13 TS-migration spec named it the "obvious follow-up"; it
  was never migrated.
- **Dictionary tree** (verified in `@umbraco-cms/backoffice` 17.4.2):
  - Tree items are `<umb-tree-item entitytype="dictionary">` rendering the generic
    `umb-default-tree-item` (`kind: 'default'`).
  - Root entity type `dictionary-root`; the menu item sets `hideTreeRoot: true`, so **there is
    no root row** to drop onto.
  - API: `DictionaryService.putDictionaryByIdMove` exists; there is **no sort endpoint**.
  - Section alias: `Umb.Section.Translation`.
- **Manifest registration** (verified in Umbraco.Infrastructure 17.0.0 / 18.0.0):
  `PackageManifestService` takes `IEnumerable<IPackageManifestReader>`, so a package can
  contribute manifests from C#. `PackagingService` takes a *single* `IPackageManifestReader` —
  see Risks.

## Behaviour

### Trees with sort (Content, Media) — unchanged from ContentTreeDragAndDrop 2.0

Top third of a row = insert above, middle = into as child, bottom third = insert below.
Same-parent drops reorder optimistically; cross-parent drops move then sort. Everything in
the 2.0 README (spinner, keyboard table, hover-to-expand, cycle guard) applies.

### Trees without sort (Dictionary) — "destination parent" semantics

Because order is alphabetical, a drop only ever chooses a **destination parent**:

| Zone on target row | Destination parent | Indicator |
|---|---|---|
| middle (`into`) | the target | outline the target row |
| top / bottom edge (`before` / `after`) | the target's parent | outline the parent's row; if the parent is the root, a 2px bar above the first top-level row |

- If the destination equals the source's current parent, the drop is a **no-op**: no
  indicator, `dropEffect = 'none'`.
- Moving to the root = drop on the edge of any top-level item.
- After a successful move: reload the old parent's and new parent's children (the tree
  re-sorts them alphabetically). No optimistic DOM change.
- Keyboard: same keys as Content. ↑/↓ move between rows (zone `before`, meaning "next to this
  row"), → targets `into`, ← jumps to the parent row. Announcements name the destination:
  "Move into Frukt", "Move next to Grönsaker (under Frukt)", "Move to root".

### Settings

```json
"Esatto": {
  "TreeDragAndDrop": {
    "Content": true,
    "Media": true,
    "Dictionary": true
  }
}
```

All default `true`; a missing section means everything is on. A disabled tree's entry point is
not in the manifest, so its JavaScript never loads. Changes need an app restart (Umbraco caches
manifests).

In 1.0.0 the options class has only `Dictionary`; `Content` and `Media` are added in the release
that ships their adapter, so a switch never exists without something to switch.

**Legacy side-by-side guard (from step 2):** if the legacy `Esatto.Umbraco.Backoffice.ContentTreeDragAndDrop`
or `…MediaTreeDragAndDrop` package is installed, the matching tree is left out of the manifest and
a warning is logged telling the developer to remove the legacy package. This prevents double
handlers on one tree during migration. Detection checks for the legacy
`/App_Plugins/<legacy id>/umbraco-package.json` through `IPackageManifestFileProviderFactory`,
the same way Umbraco's `AppPluginsPackageManifestReader` finds manifests. An "is the assembly
loaded" check would not work, because a static-assets-only package's DLL is never loaded. Step 1
has no legacy guard, since nothing can collide with Dictionary.

## Architecture

### Project layout

Mirrors `DictionaryFilterValues` (C# + `Client/`) and `ContentTreeDragAndDrop` (client build):

```
Esatto.Umbraco.Backoffice.TreeDragAndDrop/
  Esatto.Umbraco.Backoffice.TreeDragAndDrop.csproj   Razor SDK, net10.0, StaticWebAssetBasePath=/,
                                                      npm build target, MinVer, source-code links,
                                                      umbraco-marketplace tag, icon, README
  src/
    TreeDragAndDropOptions.cs        bool Content/Media/Dictionary = true
    TreeDragAndDropManifestReader.cs IPackageManifestReader → one PackageManifest
    TreeDragAndDropComposer.cs       options binding + reader registration (legacy guard joins in step 2)
  Client/
    package.json, tsconfig.json, vite.config.ts (multi-entry lib)
    src/core/…                       generic tree DnD (from ContentTreeDragAndDrop 2.0)
    src/adapters/dictionary.ts       (step 1)   content.ts (step 2)   media.ts (step 3)
    src/entries/dictionary.ts        (step 1)   content.ts (step 2)   media.ts (step 3)
    test/…                           vitest
  docs/images/                       demo GIF(s)
Esatto.Umbraco.Backoffice.TreeDragAndDrop.Tests/     xUnit, NSubstitute
```

A static `Client/public/umbraco-package.json` with `"extensions": []` gives the package its
Installed-list entry; the C# reader is the only source of entry points (see Risk 1).

### Client: core + adapters

```ts
interface TreeAdapter {
  entityType: string;          // 'document' | 'media' | 'dictionary'
  rootEntityType: string;      // 'document-root' | 'media-root' | 'dictionary-root'
  innerTag: string;            // 'umb-document-tree-item' | 'umb-media-tree-item' | 'umb-default-tree-item'
  move(host: UmbControllerHost, unique: string, parentUnique: string | null): Promise<void>;
  sort?(host: UmbControllerHost, parentUnique: string | null, ordered: string[]): Promise<void>;
}
```

- `core/registry.ts`: `registerTree(adapter)` adds the adapter, then on first call installs the
  `attachShadow` patch, observer and host (a single `<esatto-tree-dnd>` element). Later calls only
  register. Each entry module is one line: `registerTree(dictionaryAdapter)`.
- `isTreeItem(el)` matches `umb-tree-item` whose entity type has a registered adapter;
  `adapterFor(el)` returns it. A drag only accepts targets with the **same** adapter as the
  source (no cross-tree drops).
- `core/drop-plan.ts` (pure): `planDrop({ zone, source, target, hasSort }) → DropPlan`, where
  `DropPlan` is `noop | { kind: 'move', parent } | { kind: 'reorder', parent, order } |
  { kind: 'move+sort', parent, order }`. With `hasSort = false` it applies the destination-parent
  rules above and only ever returns `noop` or `move`. Both the pointer and keyboard paths call
  `planDrop`; `dnd-host` only executes the plan.
- Indicator/announce/spinner/cycle-guard/keyboard reducer/observer/dom helpers move into `core/`
  from ContentTreeDragAndDrop 2.0 with IDs and log prefixes renamed to `esatto-tree-dnd`. Their
  comments (Bellissima DOM knowledge) are kept.
- `getRowRect` uses the adapter's `innerTag` (plus `umb-default-tree-item` fallback).
- Reload uses `UmbRequestReloadChildrenOfEntityEvent` with `adapter.entityType` /
  `adapter.rootEntityType`.

**Vite:** `build.lib.entry = { dictionary: 'src/entries/dictionary.ts', … }`, ES format,
`@umbraco` external. Output goes to `wwwroot/App_Plugins/Esatto.Umbraco.Backoffice.TreeDragAndDrop/`
as `<tree>-tree-drag-drop.js` plus a shared core chunk. The browser loads that chunk once
because all entries import it by the same URL, so there is one patch and one host no matter how
many trees are enabled.

### Server: options + manifest reader

- `TreeDragAndDropComposer` calls `services.AddTreeDragAndDrop()`, which does
  `AddOptions<TreeDragAndDropOptions>().BindConfiguration("Esatto:TreeDragAndDrop")` and inserts
  the manifest reader before Umbraco's readers (Risk 1). It is idempotent.
- `TreeDragAndDropManifestReader.ReadPackageManifestsAsync()` returns one `PackageManifest`, or
  none when every tree is disabled:
  - `Name` = `Esatto.Umbraco.Backoffice.TreeDragAndDrop.EntryPoints`, no `Id`,
    `Version` = assembly informational version (MinVer, without `+sha`), `AllowTelemetry = false`.
  - One `backofficeEntryPoint` per tree that is enabled **and** not taken over by a loaded legacy
    package. Alias `Esatto.Umbraco.Backoffice.TreeDragAndDrop.<Tree>`, `js`
    `/App_Plugins/Esatto.Umbraco.Backoffice.TreeDragAndDrop/<tree>-tree-drag-drop.js`, condition
    `Umb.Condition.SectionAlias` = `Umb.Section.Content` / `.Media` / `.Translation`.
  - Trees not yet implemented in the current release (Content/Media in 1.0.0) are never emitted,
    whatever the settings say.
- csproj references: `Umbraco.Cms.Core` and `Umbraco.Cms.Infrastructure` at min-only `17.0.0`
  (the reader interface lives in Infrastructure); `FrameworkReference Microsoft.AspNetCore.App`;
  the same security-override pins as `DictionaryFilterValues` if restore flags them.

## Error handling

- Move/sort API failures: danger toast with the API message, reload both affected parents to
  re-sync (as in 2.0). Same-parent reorder rolls back the optimistic DOM change first.
- Server-side cycle rejection (a descendant in a collapsed branch the client couldn't see) is
  caught by the same path.
- Missing `UMB_ACTION_EVENT_CONTEXT`: warn in the console, skip the reload (as in 2.0).
- Manifest reader never throws: bad config falls back to defaults; a legacy assembly is logged at
  `Warning` once per startup.

## Testing

- **vitest** (`Client/test/`): `planDrop` (all zones × same/cross parent × sort/no-sort ×
  root/non-root × no-op), drop-zone geometry, cycle guard, keyboard reducer, reorder math,
  adapter registry (same-adapter rule, idempotent install).
- **xUnit** (`.Tests`, public surface): options defaults and binding; invalid config falls back
  to defaults without throwing; reader emits only enabled trees; manifest fields (alias, js path,
  section condition, version); reader inserted before Umbraco's readers. Legacy detection tests
  join in step 2.
- **Live** (umbracokalaset26, Umbraco 18; spot-check on 17): Dictionary: into, beside, to root,
  no-op, cycle blocked, keyboard path, error toast (DevTools offline); a disabled setting removes
  the entry point; Packages → Installed still lists other packages.
  Steps 2/3: a regression run against the 2.0 behaviour list on Content and Media.

## Step 2–4 notes

- **Step 2 (Content, 1.1.0):** `adapters/content.ts` = `DocumentService.putDocumentByIdMove` /
  `putDocumentSort`. Sort-only code from 2.0 (`siblings.ts`, optimistic reorder, rollback) comes
  into `core/` behind `hasSort`. Keep the 2.0 comment on the root-level reorder quirk
  (`listSiblingsInOrder(null)` walks `document` and yields nothing) and preserve its behaviour.
  Don't fix it in the same step.
- **Step 3 (Media, 1.2.0):** `adapters/media.ts` = `MediaService.putMediaByIdMove` /
  `putMediaSort`, inner tag `umb-media-tree-item`.
- **Step 4:** ContentTreeDragAndDrop 2.0.1 and MediaTreeDragAndDrop 1.0.2: README banner
  pointing to TreeDragAndDrop and a csproj `Description` note. Carl marks both deprecated on
  nuget.org with the alternate package. Switch `umbracokalaset26` and any other consumers found
  by grepping `c:\src` for the legacy IDs.

## Risks

1. **Reader registration vs `PackagingService` — resolved 2026-10-14 by decompiling
   Umbraco.Infrastructure 17.0.0 and 18.0.0.** Umbraco registers `BackOfficePackageManifestReader`
   then `AppPluginsPackageManifestReader` with plain `AddSingleton`. `PackageManifestService`
   enumerates all readers without deduplicating. `PackagingService` takes the single, last-registered
   reader for its installed-package queries (`GetAllInstalledPackagesAsync`, used by package
   migrations and telemetry). Composers run after those registrations, so a plain `AddSingleton`
   (or `TryAddEnumerable`, which still appends) would make ours the last one and hide every
   App_Plugins package from those queries. **Decision:** insert our descriptor *before* the first
   existing `IPackageManifestReader` descriptor, so Umbraco's App_Plugins reader stays last. Also
   ship a static `umbraco-package.json` with `"extensions": []`, which gives the package its identity.
   **Correction (final review, 2026-10-14):** the Packages → Installed *view* does not use
   `PackagingService`. It lists every manifest with a name from `ManifestService` (all readers), so the
   reader's manifest has an **empty `Name`** as well as no `Id` and `AllowTelemetry = false`. The
   backoffice still registers its extensions, because `UmbServerExtensionRegistrator` ignores the name.
2. **Dictionary tree DOM.** Only verified from the bundle. `props.item` shape, `toggleChildren`
   and the row-rect selectors need live confirmation; budget an extra hour.
3. **Shared chunk singleton.** Relies on all entries importing the core chunk by the same URL.
   Verify in the browser that two enabled trees produce one `attachShadow` patch (the patch
   flag is set once).
4. **Content parity (step 2).** The adapter refactor must not change 2.0 behaviour; the
   regression list above is the gate.
