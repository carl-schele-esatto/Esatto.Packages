# ContentTreeDnd → TS + Vite + Lit Migration — Design

**Status:** Approved 2026-06-13

**Goal:** Migrate `Backoffice.ContentTreeDnd` from a single hand-written 775-line vanilla-JS file to a TypeScript + Vite + Lit build that mirrors the `Esatto.Umbraco.Backoffice.CustomEditors` `Client/` convention — **preserving runtime behavior exactly** (faithful port), adding type safety on the Umbraco API surface, splitting into focused modules, and standing up unit tests for the pure logic.

**Package:** `Backoffice.ContentTreeDnd` (net9.0, Umbraco 17).

---

## Why

The package is one ~775-line JS file ([content-tree-drag-drop.js](../../../Backoffice.ContentTreeDnd/wwwroot/App_Plugins/Backoffice.ContentTreeDnd/content-tree-drag-drop.js)) served directly as a `backofficeEntryPoint`. It works, but:
- **No type safety** on exactly the version-fragile calls — `DocumentService.putDocumentByIdMove`, `putDocumentSort`, the `UmbRequestReloadChildrenOfEntityEvent` constructor, the consumed contexts.
- **No tests.** The trickiest logic (drop-zone math, reorder index computation, cycle guard) is untested.
- **One monolith.** The file is well-commented and sectioned, but all concerns live together.

Two sibling packages (`CustomEditors`, `DictionaryFilterValues`) already use TS + Vite + Lit with a `Client/` dir and an MSBuild build target. ContentTreeDnd is a holdout; this aligns it.

## Approach: faithful port first (locked)

Identical runtime behavior. Same `Element.prototype.attachShadow` patch, same composed-DOM traversal, same three-zone geometry, same optimistic-reorder/reload strategy, same drag state machine. **Comments are preserved** — they encode hard-won Bellissima DOM knowledge (`entitytype` vs `entity-type`, light-DOM vs shadow-DOM tree-item shape, the 8s sort API, why the host mounts inside `<umb-app>`). Aggressive cleanup (the `??`-fallback chains, DOM heuristics) is **deferred** until tests guard against regression. This gives a tested baseline before any behavior change.

## Output contract (must not change)

- Built entry file path stays **`/App_Plugins/Backoffice.ContentTreeDnd/content-tree-drag-drop.js`** — the exact path the manifest references. Vite `lib.fileName: "content-tree-drag-drop"`, `formats: ["es"]` → `content-tree-drag-drop.js` (+ `.map`).
- Manifest stays a `backofficeEntryPoint` with the `Umb.Condition.SectionAlias` = `Umb.Section.Content` condition. Content unchanged; only its source location moves to `Client/public/umbraco-package.json` (Vite copies `public/` → `wwwroot`).
- `@umbraco-cms/*` imports stay external (`rollupOptions.external: [/^@umbraco/]`) so they resolve against Umbraco's runtime import map exactly as today.

## Project layout (mirrors CustomEditors)

```
Backoffice.ContentTreeDnd/
  Client/
    package.json        # "build": tsc && vite build ; "test": vitest run
    tsconfig.json       # copied from CustomEditors (ES2020, decorators, strict, extension-types)
    vite.config.ts      # entry src/index.ts → ../wwwroot/App_Plugins/Backoffice.ContentTreeDnd/
    public/
      umbraco-package.json   # moved from wwwroot (content unchanged)
    src/
      constants.ts      # ATTACHED_FLAG, HOVER_EXPAND_MS, ENTITY_TYPE_DOCUMENT, DRAG_MIME, PATCH_FLAG
      dom.ts            # composedParent, walkDescendantsComposed
      tree-item.ts      # isTreeItem, readUnique, readParentUnique, propagateDraggable,
                        #   findVisualParentTreeItem, findEnclosingTreeItemComposed, findTreeItemByUnique
      drop-zone.ts      # getRowRect (DOM) + computeDropZone (PURE)
      cycle-guard.ts    # collectDescendantUniques (DOM) + isBlockedTarget (PURE predicate)
      siblings.ts       # listSiblingsInOrder (DOM) + computeReorder (PURE) + optimisticReorder (DOM)
      indicator.ts      # the fixed-position drop indicator element + render/hide
      observer.ts       # observedRoots/knownRoots, setHost, sweepSubtree, observeRoot, attachShadow patch
      dnd-host.ts       # BackofficeContentTreeDnd Lit element (drag state machine + handlers + move/sort/reload)
      index.ts          # entry wiring: import indicator+observer (side effects), define element, mount into <umb-app>
    test/
      drop-zone.test.ts
      reorder.test.ts
      cycle-guard.test.ts
  wwwroot/App_Plugins/Backoffice.ContentTreeDnd/   # BUILT output, committed (matches CustomEditors)
    content-tree-drag-drop.js (+ .map)
    umbraco-package.json
```

> **Deviation from plan:** the originally-planned `api.ts` (`move`/`sort`/`reload`) was folded back into `dnd-host.ts` as private methods — they need the controller-host `this` (for `tryExecute`) and the consumed action-event context, so keeping them on the element is cleaner and avoids guessing context type-import paths. `index.ts` imports `./indicator` before `./observer` so the indicator element is created before the document `MutationObserver` is installed, matching the original single-file side-effect order.

## Pure functions to extract + unit-test (Vitest, no jsdom)

1. **`computeDropZone(rectTop, rectHeight, clientY)` → `'before' | 'into' | 'after'`** — the ⅓/⅔ band math currently inline in `computeDropZone`. Tests: top third → before, middle → into, bottom third → after, exact boundary behavior.
2. **`computeReorder(siblings, sourceUnique, targetUnique, zone)` → `string[]`** — the insert-index/slice logic currently inline in `#onDrop` (`siblings.filter(≠source)`, `indexOf(target)`, `insertAt = before? idx : idx+1`, splice). Tests: insert at start/middle/end, before vs after, source removed first, target-not-found throws.
3. **`isBlockedTarget(targetUnique, sourceUnique, descendantUniques)` → `boolean`** — the self-or-descendant guard used in `#onDragOver`/`#onDrop`. Tests: self blocked, descendant blocked, unrelated allowed.

The DOM-touching functions (`getRowRect`, `collectDescendantUniques`, `listSiblingsInOrder`, the observer, the host) keep their behavior but delegate the decision math to these pure helpers.

## Types

- Use the real `@umbraco-cms/backoffice` types for `DocumentService`, `tryExecute`, `UmbRequestReloadChildrenOfEntityEvent`, `UMB_NOTIFICATION_CONTEXT`, `UMB_ACTION_EVENT_CONTEXT`, `LitElement`, `UmbElementMixin`.
- Define a small local interface for the **untyped internal DOM** we read (e.g. `interface TreeItemLike { props?: { item?: { unique?: string; parent?: { unique?: string | null } } }; api?: ...; _item?: ...; }`) rather than typing it as `any` everywhere — keeps the `??`-chains readable while acknowledging these are best-effort reads of Bellissima internals.
- `strict: true` (from the copied tsconfig). Expect to use targeted casts at the DOM boundary; that's the honest cost of reading untyped internals.

## Build integration (csproj)

Add to [Backoffice.ContentTreeDnd.csproj](../../../Backoffice.ContentTreeDnd/Backoffice.ContentTreeDnd.csproj), mirroring CustomEditors:
- `BuildBackofficeClient` target: `npm install` (only if `Client/node_modules` missing) + `npm run build`, `BeforeTargets="Build"`, `Condition="'$(DesignTimeBuild)' != 'true' And '$(BuildingProject)' == 'true'"`.
- `<Content Remove="Client\**" />` so client source isn't swept into static web assets.
- `<None Include="Client\public\umbraco-package.json" Pack="false" />` to keep it visible in the solution (built copy ships under wwwroot).
- Leave `PackageIcon`/README packing and the `AutoPush` target untouched. (Note: this package's `AutoPushToFeed` default is `true`; all verification uses `-p:AutoPushToFeed=false`.)

`.gitignore` already covers `**/node_modules/`, `**/.vite/`, `*.tsbuildinfo`. Built `wwwroot` JS is committed (matches CustomEditors).

## Toolchain (deps mirror CustomEditors)

`@umbraco-cms/backoffice ^17.4.2`, `typescript ^5.9.3`, `vite ^7.3.1`, `vitest ^2`. `package.json` scripts: `build` = `tsc && vite build`, `watch` = `tsc && vite build --watch`, `test` = `vitest run`.

## Verification

1. `npm install` + `npm run build` in `Client/` succeed; `content-tree-drag-drop.js` (+ `.map`) and `umbraco-package.json` land at the expected `wwwroot` path.
2. `npm test` — all three pure-logic suites green.
3. `dotnet pack -p:AutoPushToFeed=false` produces a valid `.nupkg` containing the built JS, manifest, icon, README (no push).
4. **Adversarial review pass** (parallel reviewers) checking the TS port against the original JS for behavior drift: missed lines/branches, altered event wiring, capture-phase listeners intact, attachShadow patch + re-sweep semantics intact, optimistic-reorder rollback intact.
5. **Manual** (flagged, not automated): in-browser drag/reorder/reparent smoke test in a real Umbraco 17 backoffice.

## Out of scope

- Any behavior change or cleanup of the `??`-chains / DOM heuristics (deferred to a follow-up once tests exist).
- `Backoffice.MediaTreeDnd` — identical-pattern sibling; obvious follow-up, not part of this change.
- Version bump and publishing to the feed.
