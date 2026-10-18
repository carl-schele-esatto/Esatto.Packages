# TreeDragAndDrop 1.0.0 (Step 1: core + Dictionary) Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** Ship `Esatto.Umbraco.Backoffice.TreeDragAndDrop` 1.0.0, which adds drag-and-drop and keyboard "grab & place" moving to the Umbraco 17/18 Dictionary tree. It has an `appsettings.json` switch and a tree-agnostic client core that Content and Media plug into later.

**Architecture:** The client is TS + Vite + Lit in `Client/`. A generic core, extracted from ContentTreeDragAndDrop 2.0, drives every tree through a `TreeAdapter`. Each tree has a one-line entry module. Dictionary has no sort endpoint, so a pure `planDrop` turns every drop into "move under parent P" or "no-op". The server side is a C# `IPackageManifestReader`, inserted *before* Umbraco's readers. It emits the entry point only when `Esatto:TreeDragAndDrop:Dictionary` is true.

**Tech Stack:** TypeScript 5.9, Vite 7, vitest 2, `@umbraco-cms/backoffice` ^17.4.2 (externals), .NET 10 Razor SDK, Umbraco.Cms.Core/Infrastructure 17.0.0 (min-only), xUnit 2.9, NSubstitute.

**Spec:** `docs/superpowers/specs/2026-10-14-tree-drag-and-drop-design.md`

## Global Constraints

- Package ID / folder / RootNamespace / AssemblyName: `Esatto.Umbraco.Backoffice.TreeDragAndDrop`. Tests: `Esatto.Umbraco.Backoffice.TreeDragAndDrop.Tests`.
- App_Plugins path: `/App_Plugins/Esatto.Umbraco.Backoffice.TreeDragAndDrop/`. Dictionary entry file: `dictionary-tree-drag-drop.js`.
- Entry-point alias: `Esatto.Umbraco.Backoffice.TreeDragAndDrop.Dictionary`. Condition: `Umb.Condition.SectionAlias` = `Umb.Section.Translation`.
- Config section: `Esatto:TreeDragAndDrop`. 1.0.0 has only `Dictionary` (bool, default `true`).
- Client-side IDs and flags are prefixed `esatto-tree-dnd` / `esattoTreeDnd` / `__esattoTreeDndPatched`. They must not collide with the legacy packages (`backoffice-content-tree-dnd`, `backoffice-media-tree-dnd`).
- Umbraco references are min-only `17.0.0`. Target `net10.0`.
- Every csproj carries `RepositoryUrl` = `https://github.com/carl-schele-esatto/Esatto.Packages`, `PackageProjectUrl` = `https://github.com/carl-schele-esatto/Esatto.Packages/tree/main/Esatto.Umbraco.Backoffice.TreeDragAndDrop`, `RepositoryType` = `git`, and tags including `umbraco-marketplace`.
- README images use absolute `https://raw.githubusercontent.com/carl-schele-esatto/Esatto.Packages/main/Esatto.Umbraco.Backoffice.TreeDragAndDrop/docs/images/...` URLs.
- `AutoPushToFeed` defaults to **false**.
- **Git:** work on branch `feature/tree-drag-and-drop`. Local commits per task are approved as part of approving this plan. **No `git push`, no tag, no `dotnet nuget push`** without Carl's explicit OK. Carl runs the nuget push.
- In C# files under `namespace Esatto.Umbraco...`, put `using Umbraco...;` directives at the top of the file and use short type names. Never write inline `Umbraco.Cms...` (it binds to `Esatto.Umbraco`).
- Keep the Bellissima DOM comments when copying from ContentTreeDragAndDrop 2.0. They encode hard-won knowledge.

## Review Focus

1. **Invalid config value** (e.g. `"Dictionary": "nope"`). The backoffice must still load, with the defaults applied and a warning logged. The manifest endpoint must never throw. Pinned in Task 3 (`Invalid_config_value_falls_back_to_defaults`).
2. **Missing `Esatto` section entirely.** Dictionary DnD is on. Pinned in Task 3 (`Missing_section_enables_dictionary`).
3. **Edge-drop where the item already is** (beside a sibling, or beside a top-level row when the source is top-level). Nothing is shown and nothing is called. Pinned in Task 1 (`planDrop` no-op cases).
4. **Keyboard commit on a no-op or blocked position.** No API call, and the item stays grabbed with an explanation. Pinned in Task 1 (`canCommit`), used in Task 2's host.
5. **The same adapter registered twice** (entry evaluated twice by a Bellissima remount). There is one adapter, one patch and one host. Pinned in Task 1 (adapter registry), Task 2 (idempotent `registerTree`).

---

## File map

```
Esatto.Umbraco.Backoffice.TreeDragAndDrop/
  Esatto.Umbraco.Backoffice.TreeDragAndDrop.csproj   Task 3
  README.md, icon.png, docs/images/                  Task 5
  src/TreeDragAndDropOptions.cs                      Task 3  options POCO
  src/EntryPointExtension.cs                         Task 3  manifest extension records
  src/TreeDefinitions.cs                             Task 3  which trees exist (Dictionary only)
  src/TreeDragAndDropManifestReader.cs               Task 3  IPackageManifestReader
  src/TreeDragAndDropServiceCollectionExtensions.cs  Task 3  AddTreeDragAndDrop()
  src/TreeDragAndDropComposer.cs                     Task 3  IComposer
  Client/package.json, tsconfig.json, vite.config.ts Task 1
  Client/public/umbraco-package.json                 Task 1  Installed-list entry, no extensions
  Client/src/core/types.ts                           Task 1  TreeAdapter
  Client/src/core/constants.ts                       Task 1
  Client/src/core/adapters.ts                        Task 1  adapter registry (pure)
  Client/src/core/dom.ts                             Task 1  copied
  Client/src/core/tree-item.ts                       Task 1  adapter-aware row identity
  Client/src/core/drop-zone.ts                       Task 1  geometry
  Client/src/core/cycle-guard.ts                     Task 1  copied
  Client/src/core/keyboard.ts                        Task 1  copied reducer
  Client/src/core/drop-plan.ts                       Task 1  planDrop / canCommit / describePlan (pure)
  Client/src/core/indicator.ts                       Task 2
  Client/src/core/plan-view.ts                       Task 2  plan → indicator
  Client/src/core/announce.ts, spinner.ts            Task 2
  Client/src/core/observer.ts                        Task 2
  Client/src/core/host.ts                            Task 2  Lit host
  Client/src/core/register.ts                        Task 2  registerTree()
  Client/src/adapters/dictionary.ts                  Task 2
  Client/src/entries/dictionary.ts                   Task 2
  Client/test/*.test.ts                              Task 1
Esatto.Umbraco.Backoffice.TreeDragAndDrop.Tests/     Task 3
```

Paths below are relative to `c:\src\Esatto.Packages`. `CT` = `Esatto.Umbraco.Backoffice.ContentTreeDragAndDrop/Client`, `TD` = `Esatto.Umbraco.Backoffice.TreeDragAndDrop/Client`.

---

### Task 1: Client scaffold + pure core logic

**Files:**
- Create: `TD/package.json`, `TD/tsconfig.json`, `TD/vite.config.ts`, `TD/public/umbraco-package.json`
- Create: `TD/src/core/{types,constants,adapters,dom,tree-item,drop-zone,cycle-guard,keyboard,drop-plan}.ts`
- Test: `TD/test/{adapters,drop-plan,drop-zone,cycle-guard,keyboard}.test.ts`

**Interfaces:**
- Produces (used by Task 2):
  - `types.ts`: `interface TreeAdapter { entityType; rootEntityType; innerTag; move(host, unique, parentUnique): Promise<void>; sort?(…) }`
  - `adapters.ts`: `registerAdapter(a: TreeAdapter): void`, `adapterForEntityType(et: string | null | undefined): TreeAdapter | undefined`, `clearAdapters(): void`
  - `tree-item.ts`: `type AnyTreeItem`, `readEntityType(el)`, `isTreeItem(el)`, `adapterOf(el)`, `readUnique(el)`, `readParentUnique(el)`, `readName(el)`, `propagateDraggable(el)`, `findEnclosingTreeItemComposed(el)`, `findTreeItemByUnique(unique, entityType)`, `listTreeItemsInOrder(entityType)`
  - `drop-zone.ts`: `type DropZone = 'before' | 'into' | 'after'`, `getRowRect(el)`, `computeDropZone(top, height, y)`, `getDropZone(el, y)`
  - `cycle-guard.ts`: `collectDescendantUniques(el)`, `isBlockedTarget(target, source, descendants)`
  - `keyboard.ts`: `KbCandidate`, `KbState`, `KbAction`, `reduceKey(state, key, candidates)`
  - `drop-plan.ts`: `DropContext`, `MovePlan`, `DropPlan`, `planDrop(ctx)`, `canCommit(plan)`, `describePlan(plan, zone, targetName, parentName)`
  - `constants.ts`: `ATTACHED_FLAG`, `HOVER_EXPAND_MS`, `DRAG_MIME`, `PATCH_FLAG`, `HOST_TAG`, `LOG_PREFIX`

- [ ] **Step 1: Create the branch and the client tooling**

```bash
cd /c/src/Esatto.Packages && git switch -c feature/tree-drag-and-drop
mkdir -p Esatto.Umbraco.Backoffice.TreeDragAndDrop/Client/{public,src/core,src/adapters,src/entries,test}
```

`TD/package.json`:
```json
{
  "name": "esatto-umbraco-backoffice-tree-drag-and-drop",
  "private": true,
  "version": "0.0.0",
  "type": "module",
  "scripts": {
    "watch": "tsc && vite build --watch",
    "build": "tsc && vite build",
    "test": "vitest run"
  },
  "devDependencies": {
    "@umbraco-cms/backoffice": "^17.4.2",
    "typescript": "^5.9.3",
    "vite": "^7.3.1",
    "vitest": "^2"
  }
}
```

`TD/tsconfig.json`: copy `CT/tsconfig.json` unchanged (`cp CT/tsconfig.json TD/tsconfig.json`).

`TD/vite.config.ts`:
```ts
import { defineConfig } from "vite";

export default defineConfig({
  build: {
    lib: {
      // One side-effecting backofficeEntryPoint per tree. Shared core code is
      // emitted as a chunk that every entry imports by the same URL, so the
      // browser evaluates it once: one attachShadow patch, one host.
      entry: {
        dictionary: "src/entries/dictionary.ts",
      },
      formats: ["es"],
      // Must match the js path the C# manifest reader emits:
      // /App_Plugins/Esatto.Umbraco.Backoffice.TreeDragAndDrop/<tree>-tree-drag-drop.js
      fileName: (_format, entryName) => `${entryName}-tree-drag-drop.js`,
    },
    outDir: "../wwwroot/App_Plugins/Esatto.Umbraco.Backoffice.TreeDragAndDrop",
    emptyOutDir: true,
    sourcemap: true,
    rollupOptions: {
      // Keep @umbraco-cms/* as bare specifiers so they resolve against
      // Umbraco's runtime import map (never bundled).
      external: [/^@umbraco/],
      output: { chunkFileNames: "core-[hash].js" },
    },
  },
});
```

`TD/public/umbraco-package.json` (gives the package its Packages → Installed entry; entry points come from the C# reader, see spec Risk 1):
```json
{
  "$schema": "../umbraco-package-schema.json",
  "id": "Esatto.Umbraco.Backoffice.TreeDragAndDrop",
  "name": "Esatto Umbraco Backoffice Tree Drag and Drop",
  "version": "1.0.0",
  "extensions": []
}
```

Run: `cd Esatto.Umbraco.Backoffice.TreeDragAndDrop/Client && npm install`
Expected: `package-lock.json` created, no errors. Confirm `git check-ignore -q node_modules && echo ignored` prints `ignored`.

- [ ] **Step 2: Copy the unchanged modules and their tests**

```bash
cd /c/src/Esatto.Packages
CT=Esatto.Umbraco.Backoffice.ContentTreeDragAndDrop/Client
TD=Esatto.Umbraco.Backoffice.TreeDragAndDrop/Client
cp $CT/src/dom.ts $CT/src/cycle-guard.ts $CT/src/keyboard.ts $TD/src/core/
for t in drop-zone cycle-guard keyboard; do
  sed "s#'../src/#'../src/core/#" $CT/test/$t.test.ts > $TD/test/$t.test.ts
done
```

`cycle-guard.ts` imports `./dom` and `./tree-item`, which exist side by side in `core/`, so no edits are needed.

- [ ] **Step 3: Write the failing tests for the new pure modules**

`TD/test/adapters.test.ts`:
```ts
import { describe, it, expect, beforeEach } from 'vitest';
import { registerAdapter, adapterForEntityType, clearAdapters } from '../src/core/adapters';
import type { TreeAdapter } from '../src/core/types';

const fake = (entityType: string): TreeAdapter => ({
  entityType,
  rootEntityType: `${entityType}-root`,
  innerTag: 'umb-default-tree-item',
  move: async () => {},
});

describe('adapter registry', () => {
  beforeEach(() => clearAdapters());

  it('finds a registered adapter by entity type', () => {
    const dictionary = fake('dictionary');
    registerAdapter(dictionary);
    expect(adapterForEntityType('dictionary')).toBe(dictionary);
  });

  it('returns undefined for unregistered or missing entity types', () => {
    registerAdapter(fake('dictionary'));
    expect(adapterForEntityType('media')).toBeUndefined();
    expect(adapterForEntityType(null)).toBeUndefined();
    expect(adapterForEntityType(undefined)).toBeUndefined();
  });

  // Review Focus 5: an entry evaluated twice must not leave two adapters behind.
  it('registering the same entity type twice keeps exactly one adapter', () => {
    const first = fake('dictionary');
    const second = fake('dictionary');
    registerAdapter(first);
    registerAdapter(second);
    expect(adapterForEntityType('dictionary')).toBe(second);
  });
});
```

`TD/test/drop-plan.test.ts`:
```ts
import { describe, it, expect } from 'vitest';
import { planDrop, canCommit, describePlan } from '../src/core/drop-plan';

// Tree used throughout:
//   Frukt        (root)
//   ├ Grönsaker
//   │ └ Test
//   └ Päron
//   Äpple        (root)
describe('planDrop (tree without sort)', () => {
  it('middle of a row moves into that row', () => {
    expect(planDrop({ zone: 'into', sourceParentUnique: null, targetUnique: 'apple', targetParentUnique: null }))
      .toEqual({ kind: 'move', parentUnique: 'apple' });
  });

  it('top or bottom edge moves under the target row\'s parent', () => {
    const ctx = { sourceParentUnique: 'gronsaker', targetUnique: 'paron', targetParentUnique: 'frukt' };
    expect(planDrop({ ...ctx, zone: 'before' })).toEqual({ kind: 'move', parentUnique: 'frukt' });
    expect(planDrop({ ...ctx, zone: 'after' })).toEqual({ kind: 'move', parentUnique: 'frukt' });
  });

  it('edge of a top-level row moves to the root', () => {
    expect(planDrop({ zone: 'before', sourceParentUnique: 'gronsaker', targetUnique: 'apple', targetParentUnique: null }))
      .toEqual({ kind: 'move', parentUnique: null });
  });

  // Review Focus 3: dropping where the item already is does nothing.
  it('edge of a sibling is a no-op', () => {
    expect(planDrop({ zone: 'after', sourceParentUnique: 'frukt', targetUnique: 'paron', targetParentUnique: 'frukt' }))
      .toEqual({ kind: 'noop' });
  });

  it('edge of a top-level row is a no-op for a top-level source', () => {
    expect(planDrop({ zone: 'before', sourceParentUnique: null, targetUnique: 'apple', targetParentUnique: null }))
      .toEqual({ kind: 'noop' });
  });

  it('into the current parent is a no-op', () => {
    expect(planDrop({ zone: 'into', sourceParentUnique: 'gronsaker', targetUnique: 'gronsaker', targetParentUnique: 'frukt' }))
      .toEqual({ kind: 'noop' });
  });
});

// Review Focus 4: keyboard commit only fires for a real move.
describe('canCommit', () => {
  it('rejects a missing plan (blocked or foreign target)', () => {
    expect(canCommit(null)).toBe(false);
  });
  it('rejects a no-op', () => {
    expect(canCommit({ kind: 'noop' })).toBe(false);
  });
  it('accepts a move, including a move to the root', () => {
    expect(canCommit({ kind: 'move', parentUnique: 'frukt' })).toBe(true);
    expect(canCommit({ kind: 'move', parentUnique: null })).toBe(true);
  });
});

describe('describePlan', () => {
  it('into', () => {
    expect(describePlan({ kind: 'move', parentUnique: 'frukt' }, 'into', 'Frukt', 'Frukt')).toBe('Move into Frukt');
  });
  it('next to an item under a parent', () => {
    expect(describePlan({ kind: 'move', parentUnique: 'frukt' }, 'before', 'Päron', 'Frukt'))
      .toBe('Move next to Päron (under Frukt)');
  });
  it('to the root', () => {
    expect(describePlan({ kind: 'move', parentUnique: null }, 'after', 'Äpple', null))
      .toBe('Move to root (next to Äpple)');
  });
  it('no-op', () => {
    expect(describePlan({ kind: 'noop' }, 'before', 'Päron', null)).toBe('No change: already here');
  });
});
```

- [ ] **Step 4: Run the tests to verify the new ones fail**

Run: `cd Esatto.Umbraco.Backoffice.TreeDragAndDrop/Client && npx vitest run`
Expected: FAIL. `adapters.test.ts` / `drop-plan.test.ts` can't resolve `../src/core/adapters` / `../src/core/drop-plan`. `cycle-guard.test.ts` and `drop-zone.test.ts` also fail to resolve (`tree-item`, `drop-zone` don't exist yet). `keyboard.test.ts` fails on `./drop-zone` (type-only import, so it may pass; either is fine).

- [ ] **Step 5: Implement `types.ts`, `constants.ts`, `adapters.ts`**

`TD/src/core/types.ts`:
```ts
// The one seam between the generic tree DnD core and a specific tree.

import type { UmbControllerHost } from '@umbraco-cms/backoffice/controller-api';

export interface TreeAdapter {
  /** Entity type reflected on <umb-tree-item entitytype="…">. */
  entityType: string;
  /** Entity type of the (hidden) tree root, used for top-level reload events. */
  rootEntityType: string;
  /** Inner kind-element <umb-tree-item> renders; used to locate the visible row. */
  innerTag: string;
  /** Re-parent `unique` under `parentUnique` (null = root). Throws on API error. */
  move(host: UmbControllerHost, unique: string, parentUnique: string | null): Promise<void>;
  /**
   * Persist a child order. Only for trees whose children can be ordered
   * (Content, Media). Absent for Dictionary, so row edges mean "next to".
   */
  sort?(host: UmbControllerHost, parentUnique: string | null, orderedUniques: string[]): Promise<void>;
}
```

`TD/src/core/constants.ts`:
```ts
// Esatto.Umbraco.Backoffice.TreeDragAndDrop — shared constants.
//
// Every identifier is distinct from the legacy ContentTreeDragAndDrop /
// MediaTreeDragAndDrop packages so all of them can coexist during migration.

export const ATTACHED_FLAG = 'esattoTreeDndAttached';
export const HOVER_EXPAND_MS = 700;
export const DRAG_MIME = 'application/x-esatto-tree-dnd';
export const HOST_TAG = 'esatto-tree-dnd';
export const LOG_PREFIX = '[esatto-tree-dnd]';

// Guard against re-evaluation. If anything ever causes an entry point to load
// twice (Bellissima remount, auth refresh, manifest re-eval), every additional
// load would add another wrapper layer onto attachShadow — every shadow root
// would then get N MutationObservers.
export const PATCH_FLAG = '__esattoTreeDndPatched';
```

`TD/src/core/adapters.ts`:
```ts
// Registry of tree adapters, keyed by entity type. Pure — no DOM.

import type { TreeAdapter } from './types';

const adapters = new Map<string, TreeAdapter>();

export function registerAdapter(adapter: TreeAdapter): void {
  adapters.set(adapter.entityType, adapter);
}

export function adapterForEntityType(entityType: string | null | undefined): TreeAdapter | undefined {
  return entityType ? adapters.get(entityType) : undefined;
}

/** Test hook: forget every registered adapter. */
export function clearAdapters(): void {
  adapters.clear();
}
```

- [ ] **Step 6: Implement `tree-item.ts` (adapter-aware port of 2.0)**

`TD/src/core/tree-item.ts`:
```ts
// Tree-item identity and best-effort reads of Bellissima's (untyped) internals.
// A row is "ours" when its entity type has a registered TreeAdapter.

import { composedParent, walkDescendantsComposed } from './dom';
import { adapterForEntityType } from './adapters';
import type { TreeAdapter } from './types';

// Bellissima tree-item wrappers expose their data via these properties. The
// shape is NOT part of any public contract — we read it best-effort. Modelled
// as an intersection with Element so the helpers stay DOM-typed.
export type AnyTreeItem = Element & {
  props?: { item?: { unique?: string | null; name?: string; parent?: { unique?: string | null } } };
  api?: { unique?: string | null };
  _item?: { unique?: string | null };
  toggleChildren?: () => void;
};

export function readEntityType(el: Element): string | null {
  // Lit's default @property() reflection lowercases the property name without
  // inserting hyphens, so `entityType` reflects to `entitytype`. Some
  // versions / explicit annotations may use kebab-case `entity-type`. Match
  // both. The kebab variant doesn't exist in 17.0.0 and was the bug that
  // made an early version of the content script find zero tree items.
  return el.getAttribute('entitytype') ?? el.getAttribute('entity-type');
}

export function isTreeItem(el: unknown): el is AnyTreeItem {
  const node = el as Element | null;
  if (!node || node.nodeType !== 1) return false;
  if (node.tagName !== 'UMB-TREE-ITEM') return false;
  return adapterForEntityType(readEntityType(node)) !== undefined;
}

export function adapterOf(el: Element): TreeAdapter | undefined {
  return adapterForEntityType(readEntityType(el));
}

export function readUnique(el: AnyTreeItem): string | null {
  // Primary: el.props.item.unique. The wrapper's `props` is the props
  // object passed to the inner kind-element; `item` is the nested tree-item
  // descriptor that holds .unique, .name, .entityType, .parent.
  const u = el.props?.item?.unique
    ?? el.api?.unique
    ?? el._item?.unique
    ?? el.getAttribute?.('data-unique');
  return u ?? null;
}

export function readParentUnique(el: AnyTreeItem): string | null {
  // Top-level items have parent.unique === null (entityType: '<tree>-root').
  const parent = el.props?.item?.parent;
  if (parent !== undefined) {
    return parent.unique ?? null;
  }
  // Fallback: walk up the composed tree.
  let cur = composedParent(el);
  while (cur) {
    if (isTreeItem(cur)) return readUnique(cur);
    cur = composedParent(cur);
  }
  return null;
}

export function readName(el: AnyTreeItem | null | undefined): string {
  return el?.props?.item?.name ?? 'item';
}

// Walk the wrapper's nested shadow + light DOM and set draggable=true on
// every element descendant. The visible row is composed from many nested
// elements (kind-element → uui-menu-item → its shadow row markup with
// slots, anchors, buttons, etc.). Without draggable=true ALL the way down,
// pointerdown over the label area lands on a non-draggable inner element
// and the browser's drag heuristic doesn't always ascend cleanly past it —
// resulting in "drag only works from the empty left margin" symptom.
//
// Sledgehammer is the right tool: draggable on a non-row element is
// harmless (events still bubble to the same handlers via composed path);
// draggable missing is the failure mode we want to avoid.
export function propagateDraggable(wrapperEl: Element): void {
  function visit(node: Element): void {
    if (!node || node.nodeType !== 1) return;
    if (node.tagName !== 'SLOT') {
      try { node.setAttribute('draggable', 'true'); } catch { /* readonly host attribute */ }
    }
    for (const c of node.children) visit(c);
    if (node.shadowRoot) {
      for (const c of node.shadowRoot.children) visit(c);
    }
  }
  // <umb-tree-item> in 17.0.0 doesn't use shadow DOM — it renders the inner
  // kind-element as a LIGHT-DOM child. Start from light children first; fall
  // through to shadow if a future Bellissima version puts it there.
  for (const c of wrapperEl.children) visit(c);
  if (wrapperEl.shadowRoot) {
    for (const c of wrapperEl.shadowRoot.children) visit(c);
  }
}

export function findEnclosingTreeItemComposed(itemEl: Element | null): AnyTreeItem | null {
  // Returns the tree-item wrapper that ENCLOSES itemEl, walking up through
  // shadow boundaries. Includes itemEl itself if it is one.
  let cur: Element | null = itemEl;
  while (cur) {
    if (isTreeItem(cur)) return cur;
    cur = composedParent(cur);
  }
  return null;
}

export function findTreeItemByUnique(unique: string, entityType: string): AnyTreeItem | null {
  let found: AnyTreeItem | null = null;
  walkDescendantsComposed(document.documentElement, (el) => {
    if (found) return;
    if (isTreeItem(el) && readEntityType(el) === entityType && readUnique(el) === unique) found = el;
  });
  return found;
}

export function listTreeItemsInOrder(entityType: string): AnyTreeItem[] {
  // All rendered rows of one tree in composed DOM order, which matches their
  // visual top-to-bottom order. Used by keyboard grab & place and to find the
  // first top-level row for the "move to root" indicator.
  const out: AnyTreeItem[] = [];
  walkDescendantsComposed(document.documentElement, (el) => {
    if (isTreeItem(el) && readEntityType(el) === entityType) out.push(el);
  });
  return out;
}
```

- [ ] **Step 7: Implement `drop-zone.ts`**

`TD/src/core/drop-zone.ts`:
```ts
// Drop-zone geometry: which third of a row the cursor is over.

import { adapterOf } from './tree-item';

export type DropZone = 'before' | 'into' | 'after';

// Generic kind-element Bellissima uses when a tree doesn't specify its own
// (Dictionary does exactly this). Always tried after the adapter's own tag.
const DEFAULT_INNER_TAG = 'umb-default-tree-item';

export function getRowRect(el: Element): DOMRect {
  // The wrapper's bounding rect includes nested descendants when expanded,
  // skewing the third-of-height calc. Drill in to find the visible row strip.
  //
  // 17.0.0 structure: <umb-tree-item> has the inner kind-element as a
  // LIGHT-DOM child; that has its own shadow with a <uui-menu-item>;
  // uui-menu-item's shadow contains the row body.
  const tags = [...new Set([adapterOf(el)?.innerTag, DEFAULT_INNER_TAG].filter((t): t is string => !!t))];
  const inner = el.querySelector(tags.map((t) => `:scope > ${t}`).join(', '))
    ?? el.shadowRoot?.querySelector(tags.join(', '));
  const menuItem = inner?.shadowRoot?.querySelector('uui-menu-item');
  const rowBody = menuItem?.shadowRoot?.querySelector('#menu-item, [id="menu-item"], button, .menu-item-body');
  if (rowBody) return rowBody.getBoundingClientRect();
  if (menuItem) {
    const r = menuItem.getBoundingClientRect();
    return new DOMRect(r.left, r.top, r.width, Math.min(r.height, 32));
  }
  const r = el.getBoundingClientRect();
  return new DOMRect(r.left, r.top, r.width, Math.min(r.height, 32));
}

// Pure: classify the cursor position within a row into one of the three zones.
// Top third → 'before', bottom third → 'after', middle → 'into'.
export function computeDropZone(rectTop: number, rectHeight: number, clientY: number): DropZone {
  const offset = clientY - rectTop;
  const third = rectHeight / 3;
  if (offset < third) return 'before';
  if (offset > rectHeight - third) return 'after';
  return 'into';
}

export function getDropZone(targetEl: Element, clientY: number): DropZone {
  const rect = getRowRect(targetEl);
  return computeDropZone(rect.top, rect.height, clientY);
}
```

- [ ] **Step 8: Implement `drop-plan.ts`**

`TD/src/core/drop-plan.ts`:
```ts
// Pure: what a drop does. Both the pointer and keyboard paths resolve a
// (zone, target) to a DropPlan here; the host only executes plans.
//
// 1.0.0 covers trees WITHOUT sort (Dictionary): a drop only chooses a
// destination parent, because the tree lists children alphabetically.
// Step 2 (Content) extends DropPlan with reorder / move+sort for trees
// whose adapter has `sort`.

import type { DropZone } from './drop-zone';

export interface DropContext {
  zone: DropZone;
  sourceParentUnique: string | null;
  targetUnique: string;
  targetParentUnique: string | null;
}

export type MovePlan = { kind: 'move'; parentUnique: string | null };
export type DropPlan = { kind: 'noop' } | MovePlan;

// Middle of a row → into the target. Top/bottom edge → next to the target,
// i.e. under the target's parent (null = root). Landing under the parent the
// source already has is a no-op.
export function planDrop(ctx: DropContext): DropPlan {
  const parentUnique = ctx.zone === 'into' ? ctx.targetUnique : ctx.targetParentUnique;
  return parentUnique === ctx.sourceParentUnique ? { kind: 'noop' } : { kind: 'move', parentUnique };
}

// A plan is worth committing only if it actually moves something. `null`
// means the target was illegal (blocked by the cycle guard or another tree).
export function canCommit(plan: DropPlan | null): plan is MovePlan {
  return plan?.kind === 'move';
}

// Screen-reader text for a plan. `parentName` is the destination parent's
// name (null when the destination is the root).
export function describePlan(plan: DropPlan, zone: DropZone, targetName: string, parentName: string | null): string {
  if (plan.kind === 'noop') return 'No change: already here';
  if (zone === 'into') return `Move into ${targetName}`;
  return parentName === null
    ? `Move to root (next to ${targetName})`
    : `Move next to ${targetName} (under ${parentName})`;
}
```

- [ ] **Step 9: Run the tests to verify they pass**

Run: `cd Esatto.Umbraco.Backoffice.TreeDragAndDrop/Client && npx vitest run`
Expected: PASS. 5 files, all tests green (the copied `drop-zone`, `cycle-guard`, `keyboard` suites plus the new `adapters` and `drop-plan` suites).

- [ ] **Step 10: Commit**

```bash
cd /c/src/Esatto.Packages
git add Esatto.Umbraco.Backoffice.TreeDragAndDrop/Client
git commit -m "TreeDragAndDrop: client scaffold and pure core (adapters, drop plan, geometry, keyboard)"
```

---

### Task 2: DOM core, host, Dictionary adapter, entry

**Files:**
- Create: `TD/src/core/{indicator,plan-view,announce,spinner,observer,host,register}.ts`
- Create: `TD/src/adapters/dictionary.ts`, `TD/src/entries/dictionary.ts`
- Build output: `Esatto.Umbraco.Backoffice.TreeDragAndDrop/wwwroot/App_Plugins/Esatto.Umbraco.Backoffice.TreeDragAndDrop/` (committed, as for ContentTreeDragAndDrop)

**Interfaces:**
- Consumes: everything Task 1 produces.
- Produces: `registerTree(adapter: TreeAdapter): void` (used by every entry, and by Steps 2–3 of the spec), `dictionaryAdapter: TreeAdapter`, and the built file `dictionary-tree-drag-drop.js` (referenced by Task 3's manifest reader).

These modules touch `document` at import time, so they are verified by `tsc` + build here and live in Task 4 (same approach as ContentTreeDragAndDrop 2.0).

- [ ] **Step 1: `indicator.ts`, `announce.ts`, `spinner.ts`**

`TD/src/core/indicator.ts`:
```ts
// The drop indicator — a single fixed-position element reused across drags.
//
// 'outline'    boxes a row: the destination parent (or the target itself for
//              a drop "into" it).
// 'line-above' a 2px bar above a row: used above the first top-level row to
//              mean "the root", since the dictionary tree hides its root row.

import { getRowRect } from './drop-zone';

export type IndicatorShape = 'outline' | 'line-above';

const ACCENT = 'var(--uui-color-selected, #3879ff)';
const TINT = 'rgba(56, 121, 255, 0.08)';

const indicator: HTMLDivElement = (() => {
  const el = document.createElement('div');
  el.id = 'esatto-tree-dnd-indicator';
  el.style.cssText = `
    position: fixed;
    pointer-events: none;
    z-index: 99999;
    display: none;
    box-sizing: border-box;
  `;
  document.body.appendChild(el);
  return el;
})();

export function renderIndicator(targetEl: Element, shape: IndicatorShape): void {
  const rect = getRowRect(targetEl);
  const common = `
    position: fixed;
    pointer-events: none;
    z-index: 99999;
    display: block;
    box-sizing: border-box;
    left: ${rect.left}px;
    width: ${rect.width}px;
  `;
  indicator.style.cssText = shape === 'line-above'
    ? `${common}
      top: ${rect.top - 1}px;
      height: 2px;
      background: ${ACCENT};
    `
    : `${common}
      top: ${rect.top}px;
      height: ${rect.height}px;
      border: 2px solid ${ACCENT};
      background: ${TINT};
    `;
}

export function hideIndicator(): void {
  indicator.style.display = 'none';
}
```

`TD/src/core/announce.ts`: copy `CT/src/announce.ts` and change the id line to `el.id = 'esatto-tree-dnd-live';`.

`TD/src/core/spinner.ts`: copy `CT/src/spinner.ts` and change the id line to `el.id = 'esatto-tree-dnd-spinner';`.

```bash
cd /c/src/Esatto.Packages
CT=Esatto.Umbraco.Backoffice.ContentTreeDragAndDrop/Client; TD=Esatto.Umbraco.Backoffice.TreeDragAndDrop/Client
sed "s/backoffice-content-tree-dnd-live/esatto-tree-dnd-live/" $CT/src/announce.ts > $TD/src/core/announce.ts
sed "s/backoffice-content-tree-dnd-spinner/esatto-tree-dnd-spinner/" $CT/src/spinner.ts > $TD/src/core/spinner.ts
grep -n "el.id" $TD/src/core/announce.ts $TD/src/core/spinner.ts
```
Expected: the two ids printed are `esatto-tree-dnd-live` and `esatto-tree-dnd-spinner`.

- [ ] **Step 2: `plan-view.ts`**

`TD/src/core/plan-view.ts`:
```ts
// Show a DropPlan on screen: outline the destination parent's row, or for the
// root draw a line above the first top-level row (the dictionary tree hides
// its root row, so there is nothing else to point at). If the row that stands
// for the destination isn't rendered (scrolled away / collapsed), hide rather
// than point somewhere misleading.

import type { DropPlan } from './drop-plan';
import type { TreeAdapter } from './types';
import { findTreeItemByUnique, listTreeItemsInOrder, readParentUnique } from './tree-item';
import { renderIndicator, hideIndicator } from './indicator';

export function showPlan(plan: DropPlan, adapter: TreeAdapter): void {
  if (plan.kind === 'noop') {
    hideIndicator();
    return;
  }
  if (plan.parentUnique !== null) {
    const parentEl = findTreeItemByUnique(plan.parentUnique, adapter.entityType);
    if (parentEl) renderIndicator(parentEl, 'outline');
    else hideIndicator();
    return;
  }
  const firstTopLevel = listTreeItemsInOrder(adapter.entityType).find((el) => readParentUnique(el) === null);
  if (firstTopLevel) renderIndicator(firstTopLevel, 'line-above');
  else hideIndicator();
}
```

- [ ] **Step 3: `observer.ts` (explicit install instead of module-load side effect)**

`TD/src/core/observer.ts`:
```ts
// Shadow-root observation.
//
// Bellissima nests every UI element in shadow DOM, so plain
// document.querySelectorAll never sees the tree. installObserver() patches
// Element.prototype.attachShadow to capture every shadow root created from
// then on (open OR closed) and observes each for tree-item insertions.
//
// Unlike the 2.0 content script this does not run at module load: the first
// registerTree() call installs it, so a page with every tree disabled never
// loads this module at all.

import { walkDescendantsComposed } from './dom';
import { isTreeItem, propagateDraggable, findEnclosingTreeItemComposed } from './tree-item';
import { LOG_PREFIX, PATCH_FLAG } from './constants';

// The host (the Lit element) implements this; observer only needs attachItem.
export interface DndHost {
  attachItem(el: Element): void;
}

const observedRoots = new WeakSet<object>();
const knownRoots: Array<Document | ShadowRoot> = []; // includes document; for re-sweep

let host: DndHost | null = null;

export function setHost(h: DndHost | null): void {
  host = h;
  if (h) resweep();
}

// Re-walk every captured root. Called when the host mounts, and when a tree
// registers after rows of that tree are already rendered (isTreeItem only
// matches rows whose adapter is registered).
export function resweep(): void {
  for (const root of knownRoots) {
    sweepSubtree(root);
  }
}

function sweepSubtree(root: Node | null): void {
  if (!root) return;
  const start: Node = root.nodeType === 9 ? (root as Document).documentElement : root;
  walkDescendantsComposed(start, (el) => {
    if (isTreeItem(el)) {
      host?.attachItem(el);
      // Re-run draggable propagation on every sweep — Lit re-renders the
      // wrapper's shadow tree async, so the inner kind-element / uui-menu-item
      // may not have existed at first attachItem. propagateDraggable is
      // idempotent (setAttribute on already-set attribute is a no-op).
      propagateDraggable(el);
    }
    if (el.shadowRoot && !observedRoots.has(el.shadowRoot)) {
      observeRoot(el.shadowRoot);
    }
  });
}

function observeRoot(root: Document | ShadowRoot | null): void {
  if (!root || observedRoots.has(root)) return;
  observedRoots.add(root);
  knownRoots.push(root);

  const observer = new MutationObserver((mutations) => {
    for (const m of mutations) {
      // Watch attribute changes on existing tree-items in case props/entity-type
      // gets bound after initial mount.
      if (m.type === 'attributes' && isTreeItem(m.target)) {
        host?.attachItem(m.target);
        continue;
      }
      for (const node of m.addedNodes) {
        if (!(node instanceof Element)) continue;
        if (isTreeItem(node)) host?.attachItem(node);
        sweepSubtree(node);
        // If the added node lives inside an existing attached wrapper's
        // shadow tree, re-propagate draggable on that wrapper. Lit renders
        // the inner kind-element + uui-menu-item async, so elements that
        // need draggable=true frequently appear AFTER the wrapper's initial
        // attachItem.
        const enclosing = findEnclosingTreeItemComposed(node);
        if (enclosing) propagateDraggable(enclosing);
      }
    }
  });
  observer.observe(root, {
    childList: true,
    subtree: true,
    attributes: true,
    // Lit reflects `entityType` to lowercase `entitytype` (no hyphen) in 17.0.0.
    // Watch both forms in case of version drift.
    attributeFilter: ['entitytype', 'entity-type'],
  });

  sweepSubtree(root);
}

// (1) Patch attachShadow. Captures every shadow root from here on, open OR
// closed (returned reference is the same regardless of {mode}).
// (2) Begin observing the document and walk what already exists.
//
// Cross-package note: the legacy ContentTreeDragAndDrop / MediaTreeDragAndDrop
// packages patch attachShadow too. Each wrapper calls through to the previous
// one, so the chain is benign in any load order.
export function installObserver(): void {
  const flags = window as unknown as Record<string, boolean>;
  if (flags[PATCH_FLAG]) return;
  flags[PATCH_FLAG] = true;

  const origAttachShadow = Element.prototype.attachShadow;
  Element.prototype.attachShadow = function (this: Element, opts: ShadowRootInit): ShadowRoot {
    const root = origAttachShadow.call(this, opts);
    try { observeRoot(root); }
    catch (err) { console.warn(`${LOG_PREFIX} observeRoot failed:`, err); }
    return root;
  };

  observeRoot(document);
}
```

- [ ] **Step 4: `host.ts`**

`TD/src/core/host.ts`:
```ts
// The controller host: a single hidden Lit element that owns the drag state,
// installs document-level (capture-phase) drag + keyboard listeners, and runs
// the adapter's API calls. Mounted inside <umb-app> so it can consume
// UMB_NOTIFICATION_CONTEXT / UMB_ACTION_EVENT_CONTEXT.
//
// Tree-agnostic: anything tree-specific comes from the TreeAdapter registered
// for the row's entity type. A drag only ever accepts targets from the same
// adapter as its source (no cross-tree drops).

import { LitElement } from '@umbraco-cms/backoffice/external/lit';
import { UmbElementMixin } from '@umbraco-cms/backoffice/element-api';
import { UMB_NOTIFICATION_CONTEXT } from '@umbraco-cms/backoffice/notification';
import { UMB_ACTION_EVENT_CONTEXT } from '@umbraco-cms/backoffice/action';
import { UmbRequestReloadChildrenOfEntityEvent } from '@umbraco-cms/backoffice/entity-action';

import { ATTACHED_FLAG, DRAG_MIME, HOVER_EXPAND_MS, LOG_PREFIX } from './constants';
import { setHost } from './observer';
import {
  adapterOf,
  findTreeItemByUnique,
  isTreeItem,
  listTreeItemsInOrder,
  propagateDraggable,
  readName,
  readParentUnique,
  readUnique,
  type AnyTreeItem,
} from './tree-item';
import { getDropZone, type DropZone } from './drop-zone';
import { collectDescendantUniques, isBlockedTarget } from './cycle-guard';
import { canCommit, describePlan, planDrop, type DropPlan, type MovePlan } from './drop-plan';
import { showPlan } from './plan-view';
import { hideIndicator } from './indicator';
import { showSpinner, hideSpinner } from './spinner';
import { reduceKey, type KbCandidate, type KbState } from './keyboard';
import { announce } from './announce';
import type { TreeAdapter } from './types';

interface DragState {
  adapter: TreeAdapter;
  sourceUnique: string;
  sourceParentUnique: string | null;
  descendantUniques: Set<string>;
}

const KEYBOARD_KEYS = ['ArrowUp', 'ArrowDown', 'ArrowLeft', 'ArrowRight', ' ', 'Enter', 'Escape'];

export class EsattoTreeDnd extends UmbElementMixin(LitElement) {
  #notifications?: typeof UMB_NOTIFICATION_CONTEXT.TYPE;
  #actionEvents?: typeof UMB_ACTION_EVENT_CONTEXT.TYPE;
  #dragState: DragState | null = null;
  #kbState: KbState | null = null;
  #movePending = false;
  #hoverTimer: ReturnType<typeof setTimeout> | null = null;
  #lastHoverTarget: AnyTreeItem | null = null;
  #globalListenersInstalled = false;

  constructor() {
    super();
    this.consumeContext(UMB_NOTIFICATION_CONTEXT, (ctx) => { this.#notifications = ctx; });
    this.consumeContext(UMB_ACTION_EVENT_CONTEXT, (ctx) => { this.#actionEvents = ctx; });
  }

  connectedCallback(): void {
    super.connectedCallback();
    setHost(this);
    this.#installGlobalListeners();
  }

  disconnectedCallback(): void {
    super.disconnectedCallback();
    setHost(null);
    this.#clearHoverTimer();
  }

  attachItem(el: Element): void {
    // Listeners live at document level (capture phase); attachItem only makes
    // the wrapper and its shadow descendants draggable.
    const html = el as HTMLElement;
    if (html.dataset[ATTACHED_FLAG]) return;
    html.dataset[ATTACHED_FLAG] = '1';
    el.setAttribute('draggable', 'true');
    propagateDraggable(el);

    // Lit renders the inner kind-element + uui-menu-item async, so
    // propagateDraggable above may run before the row markup exists. Watch
    // the wrapper's subtree and re-propagate on any change. Idempotent.
    const obs = new MutationObserver(() => propagateDraggable(el));
    obs.observe(el, { childList: true, subtree: true });
  }

  #installGlobalListeners(): void {
    if (this.#globalListenersInstalled) return;
    this.#globalListenersInstalled = true;
    // Capture phase (third arg = true) ensures we run before any descendant
    // listener can stopPropagation. Bellissima / UUI dispatches its own drag
    // logic in some places (block lists, sorter) that swallows bubble-phase
    // events; capture is the only reliable surface for tree DnD.
    document.addEventListener('dragstart', (e) => {
      const el = this.#findTreeItemInPath(e);
      if (el) this.#onDragStart(e, el);
    }, true);
    document.addEventListener('dragenter', (e) => {
      const el = this.#findTreeItemInPath(e);
      if (!el || !this.#dragState) return;
      // preventDefault on dragenter marks the element as a potential drop
      // target; dragover decides whether it really is one.
      e.preventDefault();
    }, true);
    document.addEventListener('dragover', (e) => {
      const el = this.#findTreeItemInPath(e);
      if (!el) {
        hideIndicator();
        return;
      }
      this.#onDragOver(e, el);
    }, true);
    document.addEventListener('dragleave', (e) => {
      const el = this.#findTreeItemInPath(e);
      if (el) this.#onDragLeave(e, el);
    }, true);
    document.addEventListener('drop', (e) => {
      const el = this.#findTreeItemInPath(e);
      if (el) void this.#onDrop(e, el);
    }, true);
    document.addEventListener('dragend', () => this.#onDragEnd(), true);
    // Keyboard "grab & place" (ARIA APG). Capture phase so the tree's own key
    // handling (open node, expand/collapse, roving tabindex) doesn't fire for
    // the keys we own while grabbed.
    document.addEventListener('keydown', (e) => this.#onKeyDown(e), true);
  }

  #findTreeItemInPath(event: Event): AnyTreeItem | null {
    // The browser may retarget event.target to a shadow host (umb-app); the
    // composed path gives us the full path through shadow boundaries.
    for (const node of event.composedPath?.() ?? []) {
      if (isTreeItem(node)) return node;
    }
    return null;
  }

  #beginDrag(el: AnyTreeItem): DragState | null {
    const adapter = adapterOf(el);
    const sourceUnique = readUnique(el);
    if (!adapter || !sourceUnique) return null;
    return {
      adapter,
      sourceUnique,
      sourceParentUnique: readParentUnique(el),
      descendantUniques: collectDescendantUniques(el),
    };
  }

  // What dropping on `el` in `zone` would do, or null if `el` is not a legal
  // target for the current drag (other tree, itself, or one of its descendants).
  #planFor(el: AnyTreeItem, zone: DropZone): DropPlan | null {
    const s = this.#dragState;
    if (!s || adapterOf(el) !== s.adapter) return null;
    const targetUnique = readUnique(el);
    if (!targetUnique) return null;
    if (isBlockedTarget(targetUnique, s.sourceUnique, s.descendantUniques)) return null;
    return planDrop({
      zone,
      sourceParentUnique: s.sourceParentUnique,
      targetUnique,
      targetParentUnique: readParentUnique(el),
    });
  }

  #onDragStart(event: DragEvent, el: AnyTreeItem): void {
    if (this.#movePending) { event.preventDefault(); return; }
    const state = this.#beginDrag(el);
    if (!state) { event.preventDefault(); return; }
    event.dataTransfer!.effectAllowed = 'move';
    event.dataTransfer!.setData(DRAG_MIME, state.sourceUnique);
    this.#dragState = state;
  }

  #onDragOver(event: DragEvent, el: AnyTreeItem): void {
    if (!this.#dragState) return;
    const plan = this.#planFor(el, getDropZone(el, event.clientY));
    if (plan === null) {
      hideIndicator();
      return;
    }
    // Hover-to-expand runs for no-op rows too: hovering a sibling is how you
    // open it to drop onto one of its children.
    this.#scheduleHoverExpand(el);
    if (plan.kind === 'noop') {
      // Not calling preventDefault leaves the browser's "no drop" cursor.
      hideIndicator();
      return;
    }
    event.preventDefault();
    event.dataTransfer!.dropEffect = 'move';
    showPlan(plan, this.#dragState.adapter);
  }

  #scheduleHoverExpand(el: AnyTreeItem): void {
    if (this.#lastHoverTarget === el) return;
    this.#clearHoverTimer();
    this.#lastHoverTarget = el;
    this.#hoverTimer = setTimeout(() => {
      if (el.hasAttribute('show-children') || el.hasAttribute('is-expanded') || el.hasAttribute('open')) return;
      if (typeof el.toggleChildren === 'function') {
        el.toggleChildren();
      } else {
        const chevron = el.shadowRoot?.querySelector('[name="caret"], [data-mark="chevron"]');
        (chevron as HTMLElement | null | undefined)?.click?.();
      }
    }, HOVER_EXPAND_MS);
  }

  #clearHoverTimer(): void {
    if (this.#hoverTimer) clearTimeout(this.#hoverTimer);
    this.#hoverTimer = null;
    this.#lastHoverTarget = null;
  }

  #onDragLeave(event: DragEvent, el: AnyTreeItem): void {
    if (event.relatedTarget && el.contains(event.relatedTarget as Node)) return;
    hideIndicator();
    if (this.#lastHoverTarget === el) this.#clearHoverTimer();
  }

  async #onDrop(event: DragEvent, el: AnyTreeItem): Promise<void> {
    const plan = this.#planFor(el, getDropZone(el, event.clientY));
    if (!canCommit(plan)) return;
    event.preventDefault();
    hideIndicator();
    await this.#execute(plan);
  }

  #onDragEnd(): void {
    this.#dragState = null;
    hideIndicator();
    this.#clearHoverTimer();
  }

  // Run a move plan for the current #dragState. Returns whether it succeeded.
  // Both affected branches are reloaded either way: on success the tree
  // re-sorts them; on failure it re-syncs with the server (e.g. a cycle
  // through a collapsed branch the client couldn't see).
  async #execute(plan: MovePlan): Promise<boolean> {
    const s = this.#dragState;
    // In-flight lock: a second drop / Space while a move is running is ignored.
    if (!s || this.#movePending) return false;
    this.#movePending = true;

    const sourceEl = findTreeItemByUnique(s.sourceUnique, s.adapter.entityType);
    if (sourceEl) {
      (sourceEl as HTMLElement).style.opacity = '0.4';
      showSpinner(sourceEl);
    }

    let ok = false;
    try {
      await s.adapter.move(this, s.sourceUnique, plan.parentUnique);
      ok = true;
    } catch (err) {
      this.#showError((err as Error)?.message ?? String(err));
    } finally {
      this.#reload(s.adapter, s.sourceParentUnique);
      this.#reload(s.adapter, plan.parentUnique);
      hideSpinner();
      if (sourceEl) (sourceEl as HTMLElement).style.opacity = '';
      this.#movePending = false;
    }
    return ok;
  }

  // --- Keyboard "grab & place" ------------------------------------------------

  // All rendered rows of the grabbed item's tree, in visual order, with a
  // parallel element array so a reducer index maps back to a DOM row.
  #buildCandidates(): { candidates: KbCandidate[]; els: AnyTreeItem[] } {
    const s = this.#dragState;
    const candidates: KbCandidate[] = [];
    const els: AnyTreeItem[] = [];
    if (!s) return { candidates, els };
    for (const el of listTreeItemsInOrder(s.adapter.entityType)) {
      const unique = readUnique(el);
      if (!unique) continue;
      candidates.push({
        unique,
        parentUnique: readParentUnique(el),
        blocked: isBlockedTarget(unique, s.sourceUnique, s.descendantUniques),
      });
      els.push(el);
    }
    return { candidates, els };
  }

  #describe(plan: DropPlan, zone: DropZone, targetEl: AnyTreeItem): string {
    const adapter = this.#dragState!.adapter;
    const parentEl = plan.kind === 'move' && plan.parentUnique !== null
      ? findTreeItemByUnique(plan.parentUnique, adapter.entityType)
      : null;
    return describePlan(plan, zone, readName(targetEl), parentEl ? readName(parentEl) : null);
  }

  #endKeyboardGrab(): void {
    hideIndicator();
    this.#kbState = null;
    this.#dragState = null;
  }

  #onKeyDown(e: KeyboardEvent): void {
    if (this.#kbState === null) {
      // Not grabbed: only Space starts a grab. Enter keeps its normal
      // "open node" behaviour; other keys pass through untouched.
      if (e.key !== ' ' || this.#movePending) return;
      const el = this.#findTreeItemInPath(e);
      if (!el) return;
      const state = this.#beginDrag(el);
      if (!state) return;
      e.preventDefault();
      e.stopPropagation();

      this.#dragState = state;
      const { candidates } = this.#buildCandidates();
      const sourceIndex = candidates.findIndex((c) => c.unique === state.sourceUnique);
      this.#kbState = { sourceUnique: state.sourceUnique, targetIndex: Math.max(sourceIndex, 0), zone: 'before' };
      hideIndicator();
      announce(`Grabbed ${readName(el)}. Use arrow keys to choose where to move it, space to drop, escape to cancel.`);
      return;
    }

    if (!KEYBOARD_KEYS.includes(e.key)) return;
    e.preventDefault();
    e.stopPropagation();

    // Rebuild candidates fresh (the tree may have expanded) and clamp the
    // possibly stale index before running the pure reducer.
    const { candidates, els } = this.#buildCandidates();
    if (candidates.length === 0) {
      this.#endKeyboardGrab();
      return;
    }
    const targetIndex = Math.min(Math.max(this.#kbState.targetIndex, 0), candidates.length - 1);
    const r = reduceKey({ ...this.#kbState, targetIndex }, e.key, candidates);

    if (r.type === 'cancel') {
      this.#endKeyboardGrab();
      announce('Move cancelled.');
      return;
    }

    const targetEl = els[r.state.targetIndex];
    const plan = targetEl ? this.#planFor(targetEl, r.state.zone) : null;

    if (r.type === 'none') {
      this.#kbState = r.state;
      if (!targetEl || !plan) {
        hideIndicator();
        return;
      }
      showPlan(plan, this.#dragState!.adapter);
      announce(this.#describe(plan, r.state.zone, targetEl));
      return;
    }

    // r.type === 'commit'
    if (!canCommit(plan)) {
      // Stay grabbed: nothing would change here.
      announce('Nothing to move here. Choose another position, or press escape to cancel.');
      return;
    }
    const { sourceUnique, adapter } = this.#dragState!;
    const sourceName = readName(findTreeItemByUnique(sourceUnique, adapter.entityType));
    // Clear #kbState synchronously so the grabbed branch can't re-enter
    // mid-flight; #dragState stays set until the move resolves (#execute
    // reads it) and the #movePending lock blocks a second #execute.
    this.#kbState = null;
    hideIndicator();
    void (async () => {
      const ok = await this.#execute(plan);
      announce(ok ? `Moved ${sourceName}.` : `Could not move ${sourceName}.`);
      // Best-effort refocus of the moved row so keyboard users keep their place.
      // The reload is async, so the row may not be re-rendered yet; then we
      // simply leave focus where it is.
      const found = findTreeItemByUnique(sourceUnique, adapter.entityType);
      const inner = found?.querySelector('[tabindex],a,button') as HTMLElement | null;
      if (inner?.focus) inner.focus();
      else (found as HTMLElement | null)?.focus?.();
      this.#dragState = null;
    })();
  }

  // --- Server sync -------------------------------------------------------------

  #reload(adapter: TreeAdapter, parentUnique: string | null): void {
    if (!this.#actionEvents) {
      console.warn(`${LOG_PREFIX} reload: action-event context not available — tree won't auto-refresh`);
      return;
    }
    // Top-level rows report parent { unique: null, entityType: '<tree>-root' },
    // so a root reload uses the adapter's root entity type.
    this.#actionEvents.dispatchEvent(
      new UmbRequestReloadChildrenOfEntityEvent({
        unique: parentUnique,
        entityType: parentUnique ? adapter.entityType : adapter.rootEntityType,
      }),
    );
  }

  #showError(message: string): void {
    console.error(LOG_PREFIX, message);
    this.#notifications?.peek('danger', { data: { message } });
  }

  render() { return null; }
}
```

- [ ] **Step 5: `register.ts`, Dictionary adapter, entry**

`TD/src/core/register.ts`:
```ts
// registerTree(): the only thing an entry module calls. Idempotent — a second
// call for the same tree (entry re-evaluated) or another tree only adds the
// adapter; the observer patch and host element are installed once.

import { registerAdapter } from './adapters';
import { installObserver, resweep } from './observer';
import { EsattoTreeDnd } from './host';
import { HOST_TAG } from './constants';
import type { TreeAdapter } from './types';

export function registerTree(adapter: TreeAdapter): void {
  registerAdapter(adapter);
  installObserver();
  mountHost();
  // Rows of this tree may already be rendered and swept before its adapter
  // existed (isTreeItem didn't match them then). Sweep again.
  resweep();
}

function mountHost(): void {
  if (!customElements.get(HOST_TAG)) customElements.define(HOST_TAG, EsattoTreeDnd);
  if (document.querySelector(HOST_TAG)) return;

  // Mount the host INSIDE <umb-app> so Lit contexts (UMB_NOTIFICATION_CONTEXT,
  // UMB_ACTION_EVENT_CONTEXT) provided by ancestors of the section roots can
  // propagate down to it. Mounting on document.body as a sibling of umb-app
  // means consumeContext silently fails — the action-event context (needed to
  // trigger tree reloads after a move) never resolves.
  const host = document.createElement(HOST_TAG);
  host.style.display = 'none';
  const umbApp = document.querySelector('umb-app');
  if (umbApp) {
    umbApp.appendChild(host);
    return;
  }

  // umb-app not yet in the DOM — start mounted on body so the sweep/observe
  // machinery is live, and re-parent into umb-app when it shows up. The
  // disconnect/reconnect on re-parent re-runs setHost → re-sweep.
  document.body.appendChild(host);
  const obs = new MutationObserver(() => {
    const ua = document.querySelector('umb-app');
    if (ua) {
      ua.appendChild(host);
      obs.disconnect();
    }
  });
  obs.observe(document.body, { childList: true });
}
```

`TD/src/adapters/dictionary.ts`:
```ts
// Dictionary (Translation section) tree adapter.
//
// Dictionary items have no sort order: the Management API has a move endpoint
// but no sort endpoint, and the tree always lists siblings alphabetically. So
// there is no `sort` here — the core treats a row's top/bottom edge as "move
// next to this item" (under its parent) instead of "insert above/below".

import { DictionaryService } from '@umbraco-cms/backoffice/external/backend-api';
import { tryExecute } from '@umbraco-cms/backoffice/resources';
import type { TreeAdapter } from '../core/types';

export const dictionaryAdapter: TreeAdapter = {
  entityType: 'dictionary',
  // The dictionary menu item sets hideTreeRoot: true, so no row carries this
  // entity type; it is only used for top-level reload events.
  rootEntityType: 'dictionary-root',
  // The dictionary tree registers its tree items with kind: 'default'.
  innerTag: 'umb-default-tree-item',

  async move(host, unique, parentUnique) {
    const { error } = await tryExecute(
      host,
      DictionaryService.putDictionaryByIdMove({
        path: { id: unique },
        body: { target: parentUnique ? { id: parentUnique } : null },
      }),
      { disableNotifications: true },
    );
    if (error) throw error;
  },
};
```

`TD/src/entries/dictionary.ts`:
```ts
// backofficeEntryPoint for the Dictionary tree (Umb.Section.Translation).
// Emitted by the C# manifest reader only when Esatto:TreeDragAndDrop:Dictionary is true.

import { registerTree } from '../core/register';
import { dictionaryAdapter } from '../adapters/dictionary';

registerTree(dictionaryAdapter);
```

- [ ] **Step 6: Type-check, test, build**

Run: `cd Esatto.Umbraco.Backoffice.TreeDragAndDrop/Client && npm test && npm run build`
Expected: vitest all green; `tsc` reports no errors; Vite writes `../wwwroot/App_Plugins/Esatto.Umbraco.Backoffice.TreeDragAndDrop/dictionary-tree-drag-drop.js`, its `.map`, and `umbraco-package.json` (copied from `public/`).

If `tsc` reports that `DictionaryService` or `putDictionaryByIdMove` is missing from `@umbraco-cms/backoffice/external/backend-api`, check `node_modules/@umbraco-cms/backoffice/dist-cms/packages/core/backend-api/sdk.gen.d.ts` for the exported service name and use that. The request type is `PutDictionaryByIdMoveData` (`path.id`, `body.target: { id } | null`), confirmed in 17.4.2.

Then verify that the bundle keeps Umbraco imports external and has no legacy identifiers:
```bash
F=../wwwroot/App_Plugins/Esatto.Umbraco.Backoffice.TreeDragAndDrop/dictionary-tree-drag-drop.js
grep -o "from\"@umbraco-cms/[a-z/-]*\"\|from \"@umbraco-cms/[a-z/-]*\"" $F | sort -u
grep -c "backoffice-content-tree-dnd\|backoffice-media-tree-dnd" $F
```
Expected: a list of `@umbraco-cms/backoffice/...` specifiers, and a count of `0`.

- [ ] **Step 7: Commit**

```bash
cd /c/src/Esatto.Packages
git add Esatto.Umbraco.Backoffice.TreeDragAndDrop/Client Esatto.Umbraco.Backoffice.TreeDragAndDrop/wwwroot
git commit -m "TreeDragAndDrop: host, observer, indicator and Dictionary adapter"
```

---

### Task 3: C# package — options, manifest reader, registration, tests

**Files:**
- Create: `Esatto.Umbraco.Backoffice.TreeDragAndDrop/Esatto.Umbraco.Backoffice.TreeDragAndDrop.csproj`
- Create: `Esatto.Umbraco.Backoffice.TreeDragAndDrop/src/{TreeDragAndDropOptions,EntryPointExtension,TreeDefinitions,TreeDragAndDropManifestReader,TreeDragAndDropServiceCollectionExtensions,TreeDragAndDropComposer}.cs`
- Create: `Esatto.Umbraco.Backoffice.TreeDragAndDrop.Tests/Esatto.Umbraco.Backoffice.TreeDragAndDrop.Tests.csproj`
- Test: `Esatto.Umbraco.Backoffice.TreeDragAndDrop.Tests/{TreeDragAndDropManifestReaderTests,TreeDragAndDropRegistrationTests}.cs`

**Interfaces:**
- Consumes: the built `dictionary-tree-drag-drop.js` path from Task 2 (`/App_Plugins/Esatto.Umbraco.Backoffice.TreeDragAndDrop/dictionary-tree-drag-drop.js`).
- Produces (public): `TreeDragAndDropOptions { const string SectionName = "Esatto:TreeDragAndDrop"; bool Dictionary = true }`, `EntryPointExtension(Type, Alias, Name, Js, Conditions)`, `ExtensionCondition(Alias, Match)`, `TreeDragAndDropManifestReader(IOptions<TreeDragAndDropOptions>, ILogger<TreeDragAndDropManifestReader>) : IPackageManifestReader`, `IServiceCollection AddTreeDragAndDrop(this IServiceCollection)`, `TreeDragAndDropComposer : IComposer`.

- [ ] **Step 1: Package csproj**

`Esatto.Umbraco.Backoffice.TreeDragAndDrop/Esatto.Umbraco.Backoffice.TreeDragAndDrop.csproj`:
```xml
<Project Sdk="Microsoft.NET.Sdk.Razor">

  <PropertyGroup>
    <TargetFramework>net10.0</TargetFramework>
    <Nullable>enable</Nullable>
    <ImplicitUsings>enable</ImplicitUsings>
    <IsPackable>true</IsPackable>
    <GeneratePackageOnBuild>false</GeneratePackageOnBuild>

    <!-- Serve wwwroot/App_Plugins/... at literal /App_Plugins/... in the consumer.
         Default Razor SDK behaviour would prefix everything with /_content/<PackageId>/,
         which Umbraco's package discovery does not look at. -->
    <StaticWebAssetBasePath>/</StaticWebAssetBasePath>

    <RootNamespace>Esatto.Umbraco.Backoffice.TreeDragAndDrop</RootNamespace>
    <AssemblyName>Esatto.Umbraco.Backoffice.TreeDragAndDrop</AssemblyName>

    <NoWarn>$(NoWarn);CS1591</NoWarn>
  </PropertyGroup>

  <PropertyGroup Label="NuGet">
    <PackageId>Esatto.Umbraco.Backoffice.TreeDragAndDrop</PackageId>
    <Authors>Carl Schéle</Authors>
    <Description>Drag-and-drop and keyboard "grab &amp; place" moving for the Umbraco 17/18 backoffice Dictionary tree, built on a shared tree core that the Content and Media trees join in later releases. Each tree can be switched off in appsettings.json (Esatto:TreeDragAndDrop).</Description>
    <PackageTags>umbraco;umbraco-marketplace;umbraco-18;backoffice;tree;dictionary;translation;drag-and-drop</PackageTags>
    <PackageReadmeFile>README.md</PackageReadmeFile>
    <PackageIcon>icon.png</PackageIcon>
    <PackageLicenseExpression>MIT</PackageLicenseExpression>
    <PackageProjectUrl>https://github.com/carl-schele-esatto/Esatto.Packages/tree/main/Esatto.Umbraco.Backoffice.TreeDragAndDrop</PackageProjectUrl>
    <RepositoryUrl>https://github.com/carl-schele-esatto/Esatto.Packages</RepositoryUrl>
    <RepositoryType>git</RepositoryType>
  </PropertyGroup>

  <ItemGroup>
    <None Include="README.md" Pack="true" PackagePath="\" />
    <None Include="icon.png" Pack="true" PackagePath="\" />
  </ItemGroup>

  <ItemGroup>
    <FrameworkReference Include="Microsoft.AspNetCore.App" />
    <!-- Core: PackageManifest, IComposer, IUmbracoBuilder (and Marketplace version
         detection). Infrastructure: IPackageManifestReader. Min-only 17.0.0;
         resolves to whatever the consumer has. -->
    <PackageReference Include="Umbraco.Cms.Core" Version="17.0.0" />
    <PackageReference Include="Umbraco.Cms.Infrastructure" Version="17.0.0" />
  </ItemGroup>

  <ItemGroup>
    <!-- Don't include the client source as part of the nuget/static-web-assets build -->
    <Content Remove="Client\**" />
    <!-- Keep umbraco-package.json visible in the solution (built copy ships under wwwroot) -->
    <None Include="Client\public\umbraco-package.json" Pack="false" />
  </ItemGroup>

  <!-- Build the backoffice client (npm run build) before building this project.
       Skipped during IDE design-time builds to avoid running npm on every keystroke. -->
  <Target Name="BuildBackofficeClient" BeforeTargets="Build" Condition="'$(DesignTimeBuild)' != 'true' And '$(BuildingProject)' == 'true'">
    <Message Importance="high" Text="Installing backoffice client dependencies (npm install)..." Condition="!Exists('$(MSBuildProjectDirectory)\Client\node_modules')" />
    <Exec Command="npm install" WorkingDirectory="$(MSBuildProjectDirectory)\Client" Condition="!Exists('$(MSBuildProjectDirectory)\Client\node_modules')" />
    <Message Importance="high" Text="Building backoffice client (npm run build)..." />
    <Exec Command="npm run build" WorkingDirectory="$(MSBuildProjectDirectory)\Client" />
  </Target>

  <PropertyGroup Label="AutoPush">
    <!-- Off by default so a local `dotnet pack` never publishes. -->
    <AutoPushToFeed Condition="'$(AutoPushToFeed)' == ''">false</AutoPushToFeed>
    <AutoPushFeedName Condition="'$(AutoPushFeedName)' == ''">esatto-packages</AutoPushFeedName>
  </PropertyGroup>

  <Target Name="AutoPushAfterPack" AfterTargets="Pack" Condition="'$(AutoPushToFeed)' == 'true'">
    <Message Importance="high" Text="Auto-pushing $(PackageId) $(PackageVersion) to '$(AutoPushFeedName)'..." />
    <Exec Command="dotnet nuget push &quot;$([MSBuild]::EnsureTrailingSlash('$(PackageOutputPath)'))$(PackageId).$(PackageVersion).nupkg&quot; --source $(AutoPushFeedName) --api-key az --skip-duplicate" />
  </Target>

</Project>
```

Copy the icon now (the csproj packs it): `cp Esatto.Umbraco.Backoffice.ContentTreeDragAndDrop/icon.png Esatto.Umbraco.Backoffice.TreeDragAndDrop/icon.png`. Create a one-line `README.md` placeholder `# Esatto.Umbraco.Backoffice.TreeDragAndDrop` (Task 5 writes the real one).

- [ ] **Step 2: Tests csproj and failing tests**

`Esatto.Umbraco.Backoffice.TreeDragAndDrop.Tests/Esatto.Umbraco.Backoffice.TreeDragAndDrop.Tests.csproj`:
```xml
<Project Sdk="Microsoft.NET.Sdk">

  <PropertyGroup>
    <!-- Test-only project (not shipped). Targets net10; references the net10 package. -->
    <TargetFramework>net10.0</TargetFramework>
    <Nullable>enable</Nullable>
    <ImplicitUsings>enable</ImplicitUsings>
    <IsPackable>false</IsPackable>
    <IsPublishable>false</IsPublishable>
  </PropertyGroup>

  <ItemGroup>
    <FrameworkReference Include="Microsoft.AspNetCore.App" />
  </ItemGroup>

  <ItemGroup>
    <PackageReference Include="Microsoft.NET.Test.Sdk" Version="17.11.1" />
    <PackageReference Include="xunit" Version="2.9.2" />
    <PackageReference Include="xunit.runner.visualstudio" Version="2.8.2" />
    <PackageReference Include="NSubstitute" Version="5.3.0" />
  </ItemGroup>

  <ItemGroup>
    <ProjectReference Include="..\Esatto.Umbraco.Backoffice.TreeDragAndDrop\Esatto.Umbraco.Backoffice.TreeDragAndDrop.csproj" />
  </ItemGroup>

</Project>
```

`Esatto.Umbraco.Backoffice.TreeDragAndDrop.Tests/TreeDragAndDropManifestReaderTests.cs`:
```csharp
using Esatto.Umbraco.Backoffice.TreeDragAndDrop;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using Umbraco.Cms.Core.Manifest;

namespace Esatto.Umbraco.Backoffice.TreeDragAndDrop.Tests;

public class TreeDragAndDropManifestReaderTests
{
    private const string PackageId = "Esatto.Umbraco.Backoffice.TreeDragAndDrop";

    private static async Task<List<PackageManifest>> ReadAsync(TreeDragAndDropOptions options)
    {
        var reader = new TreeDragAndDropManifestReader(
            Options.Create(options), NullLogger<TreeDragAndDropManifestReader>.Instance);
        return (await reader.ReadPackageManifestsAsync()).ToList();
    }

    [Fact]
    public async Task Emits_the_dictionary_entry_point_when_enabled()
    {
        var manifest = Assert.Single(await ReadAsync(new TreeDragAndDropOptions()));

        var entry = Assert.IsType<EntryPointExtension>(Assert.Single(manifest.Extensions));
        Assert.Equal("backofficeEntryPoint", entry.Type);
        Assert.Equal($"{PackageId}.Dictionary", entry.Alias);
        Assert.Equal($"/App_Plugins/{PackageId}/dictionary-tree-drag-drop.js", entry.Js);
        var condition = Assert.Single(entry.Conditions);
        Assert.Equal("Umb.Condition.SectionAlias", condition.Alias);
        Assert.Equal("Umb.Section.Translation", condition.Match);
    }

    [Fact]
    public async Task Emits_no_manifest_when_every_tree_is_disabled()
    {
        Assert.Empty(await ReadAsync(new TreeDragAndDropOptions { Dictionary = false }));
    }

    [Fact]
    public async Task Manifest_is_private_versioned_and_not_counted_twice()
    {
        var manifest = Assert.Single(await ReadAsync(new TreeDragAndDropOptions()));

        // The static umbraco-package.json carries the package's Id and telemetry;
        // this manifest only contributes entry points.
        Assert.Null(manifest.Id);
        Assert.False(manifest.AllowTelemetry);
        Assert.False(manifest.AllowPublicAccess);
        Assert.False(string.IsNullOrWhiteSpace(manifest.Version));
        Assert.DoesNotContain("+", manifest.Version);
    }
}
```

`Esatto.Umbraco.Backoffice.TreeDragAndDrop.Tests/TreeDragAndDropRegistrationTests.cs`:
```csharp
using Esatto.Umbraco.Backoffice.TreeDragAndDrop;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;
using Umbraco.Cms.Core.Manifest;
using Umbraco.Cms.Infrastructure.Manifest;

namespace Esatto.Umbraco.Backoffice.TreeDragAndDrop.Tests;

public class TreeDragAndDropRegistrationTests
{
    // Stand-ins for Umbraco's BackOfficePackageManifestReader / AppPluginsPackageManifestReader,
    // registered in the same order Umbraco registers them (both plain AddSingleton).
    private sealed class FirstUmbracoReader : IPackageManifestReader
    {
        public Task<IEnumerable<PackageManifest>> ReadPackageManifestsAsync() => Task.FromResult(Enumerable.Empty<PackageManifest>());
    }

    private sealed class LastUmbracoReader : IPackageManifestReader
    {
        public Task<IEnumerable<PackageManifest>> ReadPackageManifestsAsync() => Task.FromResult(Enumerable.Empty<PackageManifest>());
    }

    private static ServiceProvider Build(Dictionary<string, string?>? config = null, int registrations = 1)
    {
        var services = new ServiceCollection();
        services.AddLogging();
        services.AddSingleton<IConfiguration>(new ConfigurationBuilder().AddInMemoryCollection(config ?? []).Build());
        services.AddSingleton<IPackageManifestReader, FirstUmbracoReader>();
        services.AddSingleton<IPackageManifestReader, LastUmbracoReader>();
        for (var i = 0; i < registrations; i++) services.AddTreeDragAndDrop();
        return services.BuildServiceProvider();
    }

    private static TreeDragAndDropManifestReader OurReader(IServiceProvider provider) =>
        provider.GetServices<IPackageManifestReader>().OfType<TreeDragAndDropManifestReader>().Single();

    // PackagingService resolves ONE IPackageManifestReader (the last registered) to build
    // Packages → Installed. Ours must not become that one.
    [Fact]
    public void Single_resolution_stays_umbracos_last_reader()
    {
        using var provider = Build();
        Assert.IsType<LastUmbracoReader>(provider.GetRequiredService<IPackageManifestReader>());
    }

    [Fact]
    public void Our_reader_is_part_of_the_enumerable_that_PackageManifestService_reads()
    {
        using var provider = Build();
        Assert.Contains(provider.GetServices<IPackageManifestReader>(), r => r is TreeDragAndDropManifestReader);
    }

    [Fact]
    public void Registering_twice_adds_the_reader_once()
    {
        using var provider = Build(registrations: 2);
        Assert.Single(provider.GetServices<IPackageManifestReader>().OfType<TreeDragAndDropManifestReader>());
    }

    [Fact]
    public void Binds_the_Esatto_TreeDragAndDrop_section()
    {
        using var provider = Build(new() { ["Esatto:TreeDragAndDrop:Dictionary"] = "false" });
        Assert.False(provider.GetRequiredService<IOptions<TreeDragAndDropOptions>>().Value.Dictionary);
    }

    // Review Focus 2.
    [Fact]
    public async Task Missing_section_enables_dictionary()
    {
        using var provider = Build();
        var manifest = Assert.Single(await OurReader(provider).ReadPackageManifestsAsync());
        Assert.Single(manifest.Extensions);
    }

    // Review Focus 1: a typo in appsettings must not take down the manifest endpoint
    // (and with it the whole backoffice).
    [Fact]
    public async Task Invalid_config_value_falls_back_to_defaults()
    {
        using var provider = Build(new() { ["Esatto:TreeDragAndDrop:Dictionary"] = "nope" });
        var manifest = Assert.Single(await OurReader(provider).ReadPackageManifestsAsync());
        Assert.Single(manifest.Extensions);
    }
}
```

- [ ] **Step 3: Run the tests to verify they fail**

Run: `cd /c/src/Esatto.Packages && dotnet test Esatto.Umbraco.Backoffice.TreeDragAndDrop.Tests`
Expected: build FAILS with `CS0246` for `TreeDragAndDropOptions`, `TreeDragAndDropManifestReader`, `EntryPointExtension`, and `CS1061` for `AddTreeDragAndDrop`. (The npm client build runs first and succeeds.)

- [ ] **Step 4: Implement the C# types**

`src/TreeDragAndDropOptions.cs`:
```csharp
namespace Esatto.Umbraco.Backoffice.TreeDragAndDrop;

/// <summary>
/// Per-tree switches, bound from <c>Esatto:TreeDragAndDrop</c>. A missing section or key means
/// enabled. Changes need an app restart (Umbraco caches package manifests).
/// Content and Media switches arrive with their adapters in 1.1.0 / 1.2.0.
/// </summary>
public sealed class TreeDragAndDropOptions
{
    public const string SectionName = "Esatto:TreeDragAndDrop";

    /// <summary>Drag-and-drop in the Translation section's Dictionary tree.</summary>
    public bool Dictionary { get; set; } = true;
}
```

`src/EntryPointExtension.cs`:
```csharp
using System.Text.Json.Serialization;

namespace Esatto.Umbraco.Backoffice.TreeDragAndDrop;

/// <summary>A <c>backofficeEntryPoint</c> extension as it appears in a package manifest.</summary>
public sealed record EntryPointExtension(
    [property: JsonPropertyName("type")] string Type,
    [property: JsonPropertyName("alias")] string Alias,
    [property: JsonPropertyName("name")] string Name,
    [property: JsonPropertyName("js")] string Js,
    [property: JsonPropertyName("conditions")] IReadOnlyList<ExtensionCondition> Conditions);

/// <summary>An extension condition, e.g. <c>Umb.Condition.SectionAlias</c>.</summary>
public sealed record ExtensionCondition(
    [property: JsonPropertyName("alias")] string Alias,
    [property: JsonPropertyName("match")] string Match);
```

`src/TreeDefinitions.cs`:
```csharp
namespace Esatto.Umbraco.Backoffice.TreeDragAndDrop;

/// <summary>A tree this package can enable: its switch, its section, and its client entry file.</summary>
internal sealed record TreeDefinition(string Name, string SectionAlias, Func<TreeDragAndDropOptions, bool> IsEnabled)
{
    public const string PackageId = "Esatto.Umbraco.Backoffice.TreeDragAndDrop";

    // File names match the Vite entry names in Client/vite.config.ts: <tree>-tree-drag-drop.js.
    public EntryPointExtension ToEntryPoint() => new(
        Type: "backofficeEntryPoint",
        Alias: $"{PackageId}.{Name}",
        Name: $"{Name} Tree Drag and Drop",
        Js: $"/App_Plugins/{PackageId}/{Name.ToLowerInvariant()}-tree-drag-drop.js",
        Conditions: [new ExtensionCondition("Umb.Condition.SectionAlias", SectionAlias)]);
}

internal static class TreeDefinitions
{
    // Content (Umb.Section.Content) and Media (Umb.Section.Media) join here in 1.1.0 / 1.2.0.
    public static readonly IReadOnlyList<TreeDefinition> All =
    [
        new("Dictionary", "Umb.Section.Translation", options => options.Dictionary),
    ];
}
```

`src/TreeDragAndDropManifestReader.cs`:
```csharp
using System.Reflection;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Umbraco.Cms.Core.Manifest;
using Umbraco.Cms.Infrastructure.Manifest;

namespace Esatto.Umbraco.Backoffice.TreeDragAndDrop;

/// <summary>
/// Contributes one <c>backofficeEntryPoint</c> per enabled tree, so a disabled tree's
/// JavaScript is never loaded. The static <c>umbraco-package.json</c> (no extensions) is what
/// lists the package under Packages → Installed; this manifest has no Id and no telemetry so
/// the package isn't counted twice.
/// </summary>
public sealed class TreeDragAndDropManifestReader : IPackageManifestReader
{
    private static readonly string PackageVersion =
        typeof(TreeDragAndDropManifestReader).Assembly
            .GetCustomAttribute<AssemblyInformationalVersionAttribute>()?.InformationalVersion
            .Split('+')[0]
        ?? "0.0.0";

    private readonly IOptions<TreeDragAndDropOptions> _options;
    private readonly ILogger<TreeDragAndDropManifestReader> _logger;

    public TreeDragAndDropManifestReader(IOptions<TreeDragAndDropOptions> options, ILogger<TreeDragAndDropManifestReader> logger)
    {
        _options = options;
        _logger = logger;
    }

    public Task<IEnumerable<PackageManifest>> ReadPackageManifestsAsync()
    {
        TreeDragAndDropOptions options = ReadOptions();
        object[] extensions = TreeDefinitions.All
            .Where(tree => tree.IsEnabled(options))
            .Select(object (tree) => tree.ToEntryPoint())
            .ToArray();

        PackageManifest[] manifests = extensions.Length == 0
            ? []
            :
            [
                new PackageManifest
                {
                    Name = $"{TreeDefinition.PackageId}.EntryPoints",
                    Version = PackageVersion,
                    AllowTelemetry = false,
                    Extensions = extensions,
                },
            ];
        return Task.FromResult<IEnumerable<PackageManifest>>(manifests);
    }

    // The manifest endpoint feeds the whole backoffice; a bad value in appsettings must
    // not take it down. The binder throws InvalidOperationException on an unconvertible value.
    private TreeDragAndDropOptions ReadOptions()
    {
        try
        {
            return _options.Value;
        }
        catch (InvalidOperationException ex)
        {
            _logger.LogWarning(ex, "Invalid '{Section}' configuration; using defaults (all trees enabled).", TreeDragAndDropOptions.SectionName);
            return new TreeDragAndDropOptions();
        }
    }
}
```

`src/TreeDragAndDropServiceCollectionExtensions.cs`:
```csharp
using Microsoft.Extensions.DependencyInjection;
using Umbraco.Cms.Infrastructure.Manifest;

namespace Esatto.Umbraco.Backoffice.TreeDragAndDrop;

public static class TreeDragAndDropServiceCollectionExtensions
{
    /// <summary>
    /// Binds <see cref="TreeDragAndDropOptions"/> and registers <see cref="TreeDragAndDropManifestReader"/>.
    /// Idempotent.
    /// </summary>
    /// <remarks>
    /// Umbraco registers its manifest readers with plain AddSingleton. PackageManifestService reads
    /// ALL of them, but PackagingService resolves ONE (the last registered) to build
    /// Packages → Installed. Appending ours (AddSingleton or TryAddEnumerable alike) would make it
    /// that one and hide every App_Plugins package from the Installed list, so it is inserted in
    /// front of the existing readers instead.
    /// </remarks>
    public static IServiceCollection AddTreeDragAndDrop(this IServiceCollection services)
    {
        if (services.Any(d => d.ServiceType == typeof(IPackageManifestReader)
                              && d.ImplementationType == typeof(TreeDragAndDropManifestReader)))
        {
            return services;
        }

        services.AddOptions<TreeDragAndDropOptions>().BindConfiguration(TreeDragAndDropOptions.SectionName);

        var descriptor = ServiceDescriptor.Singleton<IPackageManifestReader, TreeDragAndDropManifestReader>();
        int firstReader = services.ToList().FindIndex(d => d.ServiceType == typeof(IPackageManifestReader));
        if (firstReader < 0)
        {
            services.Add(descriptor);
        }
        else
        {
            services.Insert(firstReader, descriptor);
        }

        return services;
    }
}
```

`src/TreeDragAndDropComposer.cs`:
```csharp
using Umbraco.Cms.Core.Composing;
using Umbraco.Cms.Core.DependencyInjection;

namespace Esatto.Umbraco.Backoffice.TreeDragAndDrop;

/// <summary>
/// Runs after Umbraco's core registrations (composers run in AddComposers()), so the manifest
/// readers are already in the collection when the reader is inserted in front of them.
/// </summary>
public sealed class TreeDragAndDropComposer : IComposer
{
    public void Compose(IUmbracoBuilder builder) => builder.Services.AddTreeDragAndDrop();
}
```

- [ ] **Step 5: Run the tests to verify they pass**

Run: `cd /c/src/Esatto.Packages && dotnet test Esatto.Umbraco.Backoffice.TreeDragAndDrop.Tests`
Expected: PASS, 9 tests.

If the build reports NU1902/NU1903 audit warnings for transitive Umbraco dependencies, add a `Security overrides` ItemGroup to the package csproj. Pin **only** the packages that are flagged. Take versions and comments from `Esatto.Umbraco.Backoffice.DictionaryFilterValues.csproj` (MessagePack 3.1.7, MailKit 4.16.0, MimeKit 4.16.0, System.Security.Cryptography.Pkcs 10.0.13). Re-run until the build is clean.

- [ ] **Step 6: Pack smoke test**

Run: `dotnet pack Esatto.Umbraco.Backoffice.TreeDragAndDrop -c Release -o ./.local-feed`
Expected: `Successfully created package '...\.local-feed\Esatto.Umbraco.Backoffice.TreeDragAndDrop.0.0.0-preview.0.<n>.nupkg'`, with no push (AutoPush is off). Then check the contents:
```bash
unzip -l .local-feed/Esatto.Umbraco.Backoffice.TreeDragAndDrop.*.nupkg | grep -E "App_Plugins|README|icon|\.dll"
```
Expected: `staticwebassets/App_Plugins/Esatto.Umbraco.Backoffice.TreeDragAndDrop/dictionary-tree-drag-drop.js`, `.../umbraco-package.json`, `README.md`, `icon.png`, `lib/net10.0/Esatto.Umbraco.Backoffice.TreeDragAndDrop.dll`. Confirm `.local-feed` is git-ignored (`git check-ignore -q .local-feed && echo ignored`). If it is not ignored, leave it untracked and don't add it.

- [ ] **Step 7: Commit**

```bash
git add Esatto.Umbraco.Backoffice.TreeDragAndDrop Esatto.Umbraco.Backoffice.TreeDragAndDrop.Tests
git commit -m "TreeDragAndDrop: appsettings switch and manifest reader inserted before Umbraco's readers"
```

---

### Task 4: Live verification on umbracokalaset26 (Umbraco 18)

**Who:** Claude wires it up; **Carl** runs the browser checklist (no browser automation is available in this session) and reports what he sees; Claude fixes, re-runs Tasks 1–3 tests, and Carl re-checks.

**Files (test site, NOT committed — revert at the end):**
- Modify: `c:\src\umbracokalaset26\UmbracoKalaset26.csproj` (or whatever the site csproj is named) — add a ProjectReference
- Modify: `c:\src\umbracokalaset26\appsettings.Development.json` — only for checklist item 8

- [ ] **Step 1: Reference the package from the test site**

The test site has its own uncommitted work (Views, uSync). Don't touch any of it. Add only:
```xml
<ItemGroup Label="Local TreeDragAndDrop (temporary, do not commit)">
  <ProjectReference Include="..\Esatto.Packages\Esatto.Umbraco.Backoffice.TreeDragAndDrop\Esatto.Umbraco.Backoffice.TreeDragAndDrop.csproj" />
</ItemGroup>
```
Run: `cd /c/src/umbracokalaset26 && dotnet build`
Expected: build succeeds (the npm client build runs as part of it).

- [ ] **Step 2: Carl runs the site and the checklist**

Run the site (`dotnet run`, https://localhost:44342/umbraco). Hard-refresh the backoffice and keep DevTools open (Console + Network). Test data: Frukt › Grönsaker › Test, plus at least one more top-level item and one more child of Frukt.

| # | Action | Expected |
|---|---|---|
| 1 | Network tab after load | `dictionary-tree-drag-drop.js` loaded once, no 404 |
| 2 | Drag **Test** onto the middle of **Frukt** | Frukt row outlined; drop → Test under Frukt (alphabetical), both branches refresh |
| 3 | Drag **Test** onto the top/bottom edge of a top-level item | Blue line above the first top-level row; drop → Test at root |
| 4 | Drag **Grönsaker** onto the edge of its sibling | No indicator, "no drop" cursor, drop does nothing |
| 5 | Drag **Frukt** over **Test** (its descendant) | No indicator, nothing happens on drop |
| 6 | Hold a drag over a collapsed row for ~1 s | It expands |
| 7 | Focus Test, press Space, use ↑/↓/→/←, Space | `#esatto-tree-dnd-live` text (inspect it in Elements) reads "Move into …" / "Move next to … (under …)" / "Move to root (…)"; Space moves; Space on a no-op position says "Nothing to move here…"; Escape says "Move cancelled." |
| 8 | DevTools → Network → Offline, then drop | Red toast; back online → tree re-syncs |
| 9 | Content and Media trees | Still drag with the legacy packages; no console errors from either prefix |
| 10 | Packages → Installed | Lists TreeDragAndDrop **and** the other Esatto packages (proves Risk 1 fix) |
| 11 | Add `"Esatto": { "TreeDragAndDrop": { "Dictionary": false } }` to appsettings.Development.json, restart | No `dictionary-tree-drag-drop.js` request; dictionary rows not draggable; Installed list unchanged |
| 12 | Set it to `"nope"`, restart | Backoffice loads, DnD on, warning in the log |
| 13 | After a move, check `uSync/v18/Dictionary/<item>.config` | Note whether `<Parent>` updated (informational; relates to the earlier stale-export finding, not a pass/fail gate) |

- [ ] **Step 3: Fix what fails**

For each failing row, Claude finds the cause first (superpowers:systematic-debugging). The most likely suspects are the `props.item` shape and the row-rect selectors on `umb-default-tree-item` (spec Risk 2). Add a vitest/xUnit test where the cause is in pure code, fix it, re-run `npm test`, `npm run build` and `dotnet test`, and have Carl re-check the row. Commit each fix with a message naming the checklist row.

- [ ] **Step 4: Revert the test site**

Remove the ProjectReference ItemGroup and the `Esatto` appsettings block. `git -C /c/src/umbracokalaset26 diff --stat` must show only Carl's pre-existing changes.

---

### Task 5: README, demo GIF, release prep

**Files:**
- Modify: `Esatto.Umbraco.Backoffice.TreeDragAndDrop/README.md`
- Create: `Esatto.Umbraco.Backoffice.TreeDragAndDrop/docs/images/dictionary-tree-dnd-demo.gif` (Carl records)

- [ ] **Step 1: Write the README**

`README.md`:
````markdown
# Esatto.Umbraco.Backoffice.TreeDragAndDrop

Drag-and-drop **and keyboard moving** for the Umbraco 17/18 backoffice **Dictionary** tree (Translation section).

- Drop onto the middle of an item to move it inside that item
- Drop onto the top or bottom edge of an item to move it next to that item (same parent). Use a top-level item's edge to move something to the root
- Dictionary items are always listed alphabetically, so only the parent changes. The tree re-sorts after the move
- Hover-to-expand collapsed branches mid-drag
- Cycle guard against dropping an item into its own descendants
- Keyboard-accessible "grab & place" (WCAG 2.2 AA) with screen-reader announcements
- Each tree can be switched off in `appsettings.json`

Content and Media tree support is coming to this package in 1.1 and 1.2. Until then, keep using [ContentTreeDragAndDrop](https://www.nuget.org/packages/Esatto.Umbraco.Backoffice.ContentTreeDragAndDrop) and [MediaTreeDragAndDrop](https://www.nuget.org/packages/Esatto.Umbraco.Backoffice.MediaTreeDragAndDrop). They work side by side with this package.

## Demo

![Moving dictionary items by drag-and-drop in the Umbraco Translation section](https://raw.githubusercontent.com/carl-schele-esatto/Esatto.Packages/main/Esatto.Umbraco.Backoffice.TreeDragAndDrop/docs/images/dictionary-tree-dnd-demo.gif)

## Install

```bash
dotnet add package Esatto.Umbraco.Backoffice.TreeDragAndDrop
```

Restart the site and hard-refresh the backoffice.

## Settings

Everything is on by default. To switch a tree off:

```json
{
  "Esatto": {
    "TreeDragAndDrop": {
      "Dictionary": false
    }
  }
}
```

Restart the site after changing it. A disabled tree's script isn't loaded at all.

## Keyboard

| Key | Action |
|-----|--------|
| Space | Grab the focused item / drop it at the chosen position |
| ↑ / ↓ | Move between rows ("next to this item") |
| → | Target the highlighted item itself ("into this item") |
| ← | Jump to the parent row |
| Esc | Cancel |

Each step is announced through an `aria-live` region.

## Compatibility

| Umbraco | Status |
|---------|--------|
| 17.x    | Supported |
| 18.x    | Verified |

## License

MIT.
````

(Change "17.x Supported" to "Verified" only if Carl spot-checks on a 17 site.)

- [ ] **Step 2: Carl records the GIF**

Record moving Test into Frukt, then to the root, then one keyboard move. Save it as `docs/images/dictionary-tree-dnd-demo.gif`, kept under ~3 MB like the content GIF.

- [ ] **Step 3: Final verification**

```bash
cd /c/src/Esatto.Packages
(cd Esatto.Umbraco.Backoffice.TreeDragAndDrop/Client && npm test && npm run build)
dotnet test Esatto.Umbraco.Backoffice.TreeDragAndDrop.Tests
git status --short
```
Expected: all green; the only changes are README + GIF (and the rebuilt bundle if nothing changed, it is byte-identical).

- [ ] **Step 4: Commit, then stop for Carl**

```bash
git add Esatto.Umbraco.Backoffice.TreeDragAndDrop/README.md Esatto.Umbraco.Backoffice.TreeDragAndDrop/docs
git commit -m "TreeDragAndDrop 1.0.0: README and demo"
```

Then **ask Carl** before each of these (none is pre-approved):
1. Merge `feature/tree-drag-and-drop` → `main` and push.
2. `git tag Esatto.Umbraco.Backoffice.TreeDragAndDrop-1.0.0` and push the tag.
3. `dotnet pack Esatto.Umbraco.Backoffice.TreeDragAndDrop -c Release -o artifacts`. **Carl** runs `dotnet nuget push artifacts/Esatto.Umbraco.Backoffice.TreeDragAndDrop.1.0.0.nupkg --source https://api.nuget.org/v3/index.json --api-key $env:NUGET_API_KEY`. The key's glob is `Esatto.*`, so a first push of this new ID is allowed.
4. Optionally, the Marketplace re-scan POST (outward-facing; only on Carl's go).
````
