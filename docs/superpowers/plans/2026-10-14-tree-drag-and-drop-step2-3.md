# TreeDragAndDrop 1.0.0 (Steps 2–3: Content + Media) Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** Add the Content and Media trees to `Esatto.Umbraco.Backoffice.TreeDragAndDrop` (which already handles Dictionary) and ship all three as a single **1.0.0**. The two legacy packages get prepared for deprecation.

**Architecture:** This builds on step 1's core (`TreeAdapter`, `planDrop`, host, observer). Trees whose adapter has `sort` get a new pure `planSortedDrop`, which returns `move` (into), `reorder` (same parent) or `move+sort` (cross-parent slot). The host executes these with ContentTreeDragAndDrop 2.0's optimistic-reorder/rollback behaviour. On the server, `Content`/`Media` switches and a legacy-package guard are added to the manifest reader.

**Tech Stack:** As step 1. Adds `DocumentService` / `MediaService` (`@umbraco-cms/backoffice/external/backend-api`) and `IPackageManifestFileProviderFactory` (Umbraco.Cms.Core).

**Spec:** `docs/superpowers/specs/2026-10-14-tree-drag-and-drop-design.md` (steps 2–3 notes). Step 1's plan and ledger, `docs/superpowers/plans/2026-10-14-tree-drag-and-drop-step1.md` and `.superpowers/sdd/2026-10-14-tree-drag-and-drop-step1/progress.md`, hold the conventions and rulings this builds on.

## Changes from the spec (Carl, 2026-10-14)

- **One release.** Carl approved shipping Content + Media + Dictionary as **1.0.0**, instead of 1.0.0 / 1.1.0 / 1.2.0. Nothing has been published yet.
- **Root-level reorder gets fixed, not preserved.** ContentTreeDragAndDrop 2.0's `listSiblingsInOrder(null)` walks `document` (nodeType 9) and returns nothing. Reordering items at the **tree root** therefore throws "Target sibling not found" and shows an error toast, in both legacy packages. Siblings now come from `listTreeItemsInOrder(entityType)` filtered by parent, which works at the root too. The spec said to keep the quirk for parity; that was a parity-testing convenience, not a requirement, and shipping a known-broken root reorder in 1.0.0 would be worse.
- **Recycle-bin rows are excluded.** Rows with `props.item.isTrashed === true` are never ours. Moving a trashed item out of the recycle bin is a restore, which Umbraco handles through its own action.

## Global Constraints

- Everything in step 1's Global Constraints still applies: IDs, paths, min-only `17.0.0`, source links, `AutoPushToFeed` false, short Umbraco type names in `Esatto.*` namespaces, keep the Bellissima comments.
- Branch: keep working on `feature/tree-drag-and-drop`. Local commits per task are approved with this plan. **No push, tag, merge or nuget push without Carl's explicit OK.**
- Entry-point aliases: `Esatto.Umbraco.Backoffice.TreeDragAndDrop.{Content|Media|Dictionary}`. JS files: `{content|media|dictionary}-tree-drag-drop.js`. Section conditions: `Umb.Section.Content` / `Umb.Section.Media` / `Umb.Section.Translation`.
- Config keys: `Esatto:TreeDragAndDrop:Content`, `:Media`, `:Dictionary`, all bool and default `true`.
- Legacy package IDs: `Esatto.Umbraco.Backoffice.ContentTreeDragAndDrop` (Content) and `Esatto.Umbraco.Backoffice.MediaTreeDragAndDrop` (Media).
- Content/Media adapter facts (verified in `@umbraco-cms/backoffice` 17.7.1):
  - Content: entity type `document`, root `document-root`, inner tag `umb-document-tree-item`, `DocumentService.putDocumentByIdMove` / `putDocumentSort`.
  - Media: entity type `media`, root `media-root`, inner tag `umb-media-tree-item`, `MediaService.putMediaByIdMove` / `putMediaSort`.
  - Sort body: `{ parent: { id } | null, sorting: [{ id, sortOrder }] }`.

## Review Focus

1. **Reorder at the tree root** (Content's top-level nodes, Media's root folders). The new order is saved, with no "Target sibling not found" toast. Pinned in Task 1 (`planSortedDrop` root cases).
2. **Dropping a node back where it is** (above its own next sibling, or below its own previous sibling). This is a no-op: no indicator and no API call. Pinned in Task 1 (`planSortedDrop` unchanged-order no-op).
3. **A legacy package still installed** next to 1.0.0. That tree's entry point is left out and a warning is logged; the other trees still work. Pinned in Task 3 (legacy guard tests).
4. **A recycle-bin row** dragged or grabbed is ignored. Pinned in Task 1 (`isTreeItem` trashed case).
5. **The sort call fails after a cross-parent move succeeded.** The item stays moved, a warning toast says the position couldn't be set, and both branches reload. Covered by Task 2's code, which keeps 2.0's tested behaviour, and checked in the Task 4 live list. The host isn't unit-testable in the node test environment.

---

## File map

```
Client/src/core/drop-plan.ts        Task 1  + planSortedDrop, ReorderPlan, MoveSortPlan; canCommit/describePlan widened
Client/src/core/tree-item.ts        Task 1  + isTrashed exclusion; + listSiblings(parentUnique, entityType)
Client/src/core/reorder-dom.ts      Task 2  optimisticReorder (from 2.0 siblings.ts)
Client/src/core/indicator.ts        Task 2  + 'line-below'
Client/src/core/plan-view.ts        Task 2  showPlan(plan, adapter, targetEl, zone)
Client/src/core/host.ts             Task 2  sorted planning + #execute for reorder / move+sort
Client/src/adapters/run.ts          Task 2  tryExecute wrapper shared by adapters
Client/src/adapters/{content,media}.ts, entries/{content,media}.ts   Task 2
Client/vite.config.ts               Task 2  + content, media entries
src/TreeDragAndDropOptions.cs       Task 3  + Content, Media
src/TreeDefinitions.cs              Task 3  + Content, Media, LegacyPackageId
src/TreeDragAndDropManifestReader.cs Task 3 + legacy guard
README.md, csproj                   Task 5
legacy READMEs/csprojs              Task 5  deprecation notice (no tag)
```

`TD` = `Esatto.Umbraco.Backoffice.TreeDragAndDrop/Client`.

---

### Task 1: Pure planning for sorted trees, trashed rows, sibling listing

**Files:**
- Modify: `TD/src/core/drop-plan.ts`, `TD/src/core/tree-item.ts`
- Test: `TD/test/drop-plan.test.ts`, `TD/test/tree-item.test.ts`

**Interfaces:**
- Produces:
  - `SortedDropContext extends DropContext { sourceUnique: string; siblings: string[] }`
  - `ReorderPlan = { kind: 'reorder'; parentUnique: string | null; order: string[] }`
  - `MoveSortPlan = { kind: 'move+sort'; parentUnique: string | null; order: string[] }`
  - `DropPlan = { kind: 'noop' } | MovePlan | ReorderPlan | MoveSortPlan`
  - `ActionPlan = Exclude<DropPlan, { kind: 'noop' }>`
  - `planSortedDrop(ctx): DropPlan`
  - `canCommit(plan): plan is ActionPlan`
  - `describePlan` handles `reorder` / `move+sort` ("Move above X" / "Move below X")
  - `listSiblings(parentUnique, entityType): string[]` (tree-item.ts, DOM)

- [ ] **Step 1: Failing tests**

Append to `TD/test/drop-plan.test.ts`:
```ts
import { planSortedDrop } from '../src/core/drop-plan';

// Content tree used below:
//   Home        (root)
//   ├ About
//   ├ Blog
//   └ Contact
//   Settings    (root)
describe('planSortedDrop (tree with sort)', () => {
  const homeKids = ['about', 'blog', 'contact'];

  it('middle of a row moves into it', () => {
    expect(planSortedDrop({ zone: 'into', sourceUnique: 'contact', sourceParentUnique: 'home', targetUnique: 'settings', targetParentUnique: null, siblings: ['home', 'settings'] }))
      .toEqual({ kind: 'move', parentUnique: 'settings' });
  });

  it('into the current parent is a no-op', () => {
    expect(planSortedDrop({ zone: 'into', sourceUnique: 'about', sourceParentUnique: 'home', targetUnique: 'home', targetParentUnique: null, siblings: ['home', 'settings'] }))
      .toEqual({ kind: 'noop' });
  });

  it('same parent, above a sibling → reorder', () => {
    expect(planSortedDrop({ zone: 'before', sourceUnique: 'contact', sourceParentUnique: 'home', targetUnique: 'about', targetParentUnique: 'home', siblings: homeKids }))
      .toEqual({ kind: 'reorder', parentUnique: 'home', order: ['contact', 'about', 'blog'] });
  });

  it('same parent, below a sibling → reorder', () => {
    expect(planSortedDrop({ zone: 'after', sourceUnique: 'about', sourceParentUnique: 'home', targetUnique: 'contact', targetParentUnique: 'home', siblings: homeKids }))
      .toEqual({ kind: 'reorder', parentUnique: 'home', order: ['blog', 'contact', 'about'] });
  });

  // Review Focus 1: the legacy packages failed here ("Target sibling not found").
  it('reorders at the tree root', () => {
    expect(planSortedDrop({ zone: 'before', sourceUnique: 'settings', sourceParentUnique: null, targetUnique: 'home', targetParentUnique: null, siblings: ['home', 'settings'] }))
      .toEqual({ kind: 'reorder', parentUnique: null, order: ['settings', 'home'] });
  });

  // Review Focus 2: dropping where it already is does nothing.
  it('above its own next sibling is a no-op', () => {
    expect(planSortedDrop({ zone: 'before', sourceUnique: 'about', sourceParentUnique: 'home', targetUnique: 'blog', targetParentUnique: 'home', siblings: homeKids }))
      .toEqual({ kind: 'noop' });
  });

  it('below its own previous sibling is a no-op', () => {
    expect(planSortedDrop({ zone: 'after', sourceUnique: 'blog', sourceParentUnique: 'home', targetUnique: 'about', targetParentUnique: 'home', siblings: homeKids }))
      .toEqual({ kind: 'noop' });
  });

  it('cross-parent slot → move+sort with the source inserted', () => {
    expect(planSortedDrop({ zone: 'after', sourceUnique: 'settings', sourceParentUnique: null, targetUnique: 'about', targetParentUnique: 'home', siblings: homeKids }))
      .toEqual({ kind: 'move+sort', parentUnique: 'home', order: ['about', 'settings', 'blog', 'contact'] });
  });

  it('cross-parent slot to the root → move+sort under null', () => {
    expect(planSortedDrop({ zone: 'before', sourceUnique: 'blog', sourceParentUnique: 'home', targetUnique: 'settings', targetParentUnique: null, siblings: ['home', 'settings'] }))
      .toEqual({ kind: 'move+sort', parentUnique: null, order: ['home', 'blog', 'settings'] });
  });

  it('target missing from the rendered siblings → plain move (cross-parent) or no-op (same parent)', () => {
    expect(planSortedDrop({ zone: 'before', sourceUnique: 'x', sourceParentUnique: null, targetUnique: 'about', targetParentUnique: 'home', siblings: [] }))
      .toEqual({ kind: 'move', parentUnique: 'home' });
    expect(planSortedDrop({ zone: 'before', sourceUnique: 'blog', sourceParentUnique: 'home', targetUnique: 'about', targetParentUnique: 'home', siblings: [] }))
      .toEqual({ kind: 'noop' });
  });
});

describe('canCommit / describePlan for sorted plans', () => {
  it('commits reorder and move+sort', () => {
    expect(canCommit({ kind: 'reorder', parentUnique: null, order: ['a'] })).toBe(true);
    expect(canCommit({ kind: 'move+sort', parentUnique: 'p', order: ['a'] })).toBe(true);
  });

  it('describes above / below', () => {
    expect(describePlan({ kind: 'reorder', parentUnique: 'home', order: [] }, 'before', 'About', 'Home')).toBe('Move above About');
    expect(describePlan({ kind: 'move+sort', parentUnique: 'home', order: [] }, 'after', 'About', 'Home')).toBe('Move below About');
  });
});
```

Append to `TD/test/tree-item.test.ts` (inside the existing `describe('isTreeItem', …)` block):
```ts
  // Review Focus 4: recycle-bin rows render in the same sidebar but must not move.
  it('rejects a trashed row', () => {
    expect(isTreeItem(row('dictionary', { isMenu: true, item: { isTrashed: true } }))).toBe(false);
  });
```

Run: `cd Esatto.Umbraco.Backoffice.TreeDragAndDrop/Client && npx vitest run`
Expected: FAIL. `planSortedDrop` is not a function, the `canCommit`/`describePlan` sorted cases fail, and the trashed row is accepted.

- [ ] **Step 2: Implement `drop-plan.ts` additions**

Replace the type block and `canCommit`/`describePlan` in `TD/src/core/drop-plan.ts`, and add `planSortedDrop`:
```ts
export interface DropContext {
  zone: DropZone;
  sourceParentUnique: string | null;
  targetUnique: string;
  targetParentUnique: string | null;
}

// Trees with sort also need the source and the target parent's current child
// order (as rendered; may include the source).
export interface SortedDropContext extends DropContext {
  sourceUnique: string;
  siblings: string[];
}

export type MovePlan = { kind: 'move'; parentUnique: string | null };
export type ReorderPlan = { kind: 'reorder'; parentUnique: string | null; order: string[] };
export type MoveSortPlan = { kind: 'move+sort'; parentUnique: string | null; order: string[] };
export type DropPlan = { kind: 'noop' } | MovePlan | ReorderPlan | MoveSortPlan;
export type ActionPlan = Exclude<DropPlan, { kind: 'noop' }>;
```

```ts
// Trees WITH sort (Content, Media). Middle of a row → into it (the server puts
// it last). Top/bottom edge → above/below the target, under the target's
// parent: a reorder when that is the current parent, otherwise move + sort.
export function planSortedDrop(ctx: SortedDropContext): DropPlan {
  const sameParent = ctx.targetParentUnique === ctx.sourceParentUnique;
  if (ctx.zone === 'into') {
    return ctx.targetUnique === ctx.sourceParentUnique
      ? { kind: 'noop' }
      : { kind: 'move', parentUnique: ctx.targetUnique };
  }

  const without = ctx.siblings.filter((u) => u !== ctx.sourceUnique);
  const targetIdx = without.indexOf(ctx.targetUnique);
  if (targetIdx === -1) {
    // Can't place it among siblings we can't see: still honour a parent change.
    return sameParent ? { kind: 'noop' } : { kind: 'move', parentUnique: ctx.targetParentUnique };
  }
  const insertAt = ctx.zone === 'before' ? targetIdx : targetIdx + 1;
  const order = [...without.slice(0, insertAt), ctx.sourceUnique, ...without.slice(insertAt)];

  if (!sameParent) return { kind: 'move+sort', parentUnique: ctx.targetParentUnique, order };
  const unchanged = order.length === ctx.siblings.length && order.every((u, i) => u === ctx.siblings[i]);
  return unchanged ? { kind: 'noop' } : { kind: 'reorder', parentUnique: ctx.targetParentUnique, order };
}

// A plan is worth committing only if it actually changes something. `null`
// means the target was illegal (blocked by the cycle guard or another tree).
export function canCommit(plan: DropPlan | null): plan is ActionPlan {
  return plan !== null && plan.kind !== 'noop';
}

// Screen-reader text for a plan. `parentName` is the destination parent's
// name (null when the destination is the root).
export function describePlan(plan: DropPlan, zone: DropZone, targetName: string, parentName: string | null): string {
  if (plan.kind === 'noop') return 'No change: already here';
  if (zone === 'into') return `Move into ${targetName}`;
  if (plan.kind === 'reorder' || plan.kind === 'move+sort') {
    return zone === 'before' ? `Move above ${targetName}` : `Move below ${targetName}`;
  }
  return parentName === null
    ? `Move to root (next to ${targetName})`
    : `Move next to ${targetName} (under ${parentName})`;
}
```

Update the file's header comment: replace the "1.0.0 covers trees WITHOUT sort … Step 2 (Content) extends DropPlan …" paragraph with:
```ts
// Trees WITHOUT sort (Dictionary) use planDrop: a drop only chooses a
// destination parent, because the tree lists children alphabetically.
// Trees WITH sort (Content, Media) use planSortedDrop.
```

- [ ] **Step 3: Implement `tree-item.ts` additions**

In the `AnyTreeItem` `props.item` type add `isTrashed?: boolean;`. In `isTreeItem`, after the `isMenu` check:
```ts
  // Recycle-bin rows share the sidebar; moving one out is a restore, which
  // Umbraco does through its own action.
  if (node.props?.item?.isTrashed === true) return false;
```
Add after `listTreeItemsInOrder`:
```ts
// Current visible child order under `parentUnique` (null = tree root). Unlike
// ContentTreeDragAndDrop 2.0's walk from the parent row, this works at the
// root, where there is no parent row to walk from.
export function listSiblings(parentUnique: string | null, entityType: string): string[] {
  const out: string[] = [];
  for (const el of listTreeItemsInOrder(entityType)) {
    if (readParentUnique(el) !== parentUnique) continue;
    const u = readUnique(el);
    if (u) out.push(u);
  }
  return out;
}
```

- [ ] **Step 4: Tests pass**

Run: `npx vitest run` → Expected: PASS, all files (50 + 13 new = 63).

- [ ] **Step 5: Commit** — `git commit -m "TreeDragAndDrop: sorted-tree drop planning, trashed rows excluded"`

---

### Task 2: Host support for sorted plans; Content and Media adapters and entries

**Files:**
- Create: `TD/src/core/reorder-dom.ts`, `TD/src/adapters/run.ts`, `TD/src/adapters/content.ts`, `TD/src/adapters/media.ts`, `TD/src/entries/content.ts`, `TD/src/entries/media.ts`
- Modify: `TD/src/core/indicator.ts`, `TD/src/core/plan-view.ts`, `TD/src/core/host.ts`, `TD/src/adapters/dictionary.ts`, `TD/vite.config.ts`

**Interfaces:**
- Consumes: Task 1's `planSortedDrop`, `ActionPlan`, `listSiblings`.
- Produces: `content-tree-drag-drop.js`, `media-tree-drag-drop.js` (names must match Task 3's emitted js paths).

- [ ] **Step 1: `reorder-dom.ts`** — copy `optimisticReorder` from ContentTreeDragAndDrop 2.0:
```ts
// Optimistic DOM reorder for same-parent drops: move the source row next to
// the target before the (slow — ~8 s for ~26 children locally) sort API
// returns, and roll back if it fails. Cross-parent moves can't do this:
// Bellissima's chevron and indentation come from the target's hasChildren in
// its data store, so those reload instead.

import type { DropZone } from './drop-zone';

export function optimisticReorder(sourceEl: Element | null, targetEl: Element | null, zone: DropZone): void {
  if (!sourceEl || !targetEl || sourceEl === targetEl) return;
  const parent = targetEl.parentNode;
  if (!parent) return;
  parent.insertBefore(sourceEl, zone === 'before' ? targetEl : targetEl.nextSibling);
}
```

- [ ] **Step 2: `indicator.ts`** — `IndicatorShape = 'outline' | 'line-above' | 'line-below'`, and in `renderIndicator` handle `'line-below'` with `top: ${rect.bottom - 1}px; height: 2px; background: ${ACCENT};`. Update the header comment to list `'line-below'` ("a 2px bar below a row: insert below it").

- [ ] **Step 3: `plan-view.ts`** — new signature `showPlan(plan: DropPlan, adapter: TreeAdapter, targetEl: Element, zone: DropZone)`:
```ts
export function showPlan(plan: DropPlan, adapter: TreeAdapter, targetEl: Element, zone: DropZone): void {
  if (plan.kind === 'noop') {
    hideIndicator();
    return;
  }
  // Sorted trees: a line where the item will land, or the target outlined for "into".
  if (plan.kind === 'reorder' || plan.kind === 'move+sort') {
    renderIndicator(targetEl, zone === 'before' ? 'line-above' : 'line-below');
    return;
  }
  if (zone === 'into' || adapter.sort) {
    renderIndicator(targetEl, 'outline');
    return;
  }
  // Unsorted trees, edge zone: point at the destination parent (or the root).
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
(Add `import type { DropZone } from './drop-zone';`. Update the header comment to describe both tree kinds.)

- [ ] **Step 4: `adapters/run.ts`** and refactor `dictionary.ts` onto it:
```ts
// Run a Management API call for an adapter: no Umbraco toast (the host shows
// its own), throw on error so the host's error path runs.

import type { UmbControllerHost } from '@umbraco-cms/backoffice/controller-api';
import { tryExecute } from '@umbraco-cms/backoffice/resources';

export async function run<T>(host: UmbControllerHost, request: Promise<T>): Promise<void> {
  const { error } = await tryExecute(host, request, { disableNotifications: true });
  if (error) throw error;
}
```
In `dictionary.ts`, replace the `move` body with `return run(host, DictionaryService.putDictionaryByIdMove({ path: { id: unique }, body: { target: parentUnique ? { id: parentUnique } : null } }));`, drop the `tryExecute` import and import `run` from `./run`.

- [ ] **Step 5: `adapters/content.ts`, `adapters/media.ts`, entries**

`TD/src/adapters/content.ts`:
```ts
// Content (Umb.Section.Content) tree adapter — documents can be ordered.

import { DocumentService } from '@umbraco-cms/backoffice/external/backend-api';
import type { TreeAdapter } from '../core/types';
import { run } from './run';

export const contentAdapter: TreeAdapter = {
  entityType: 'document',
  rootEntityType: 'document-root',
  innerTag: 'umb-document-tree-item',

  move(host, unique, parentUnique) {
    return run(host, DocumentService.putDocumentByIdMove({
      path: { id: unique },
      body: { target: parentUnique ? { id: parentUnique } : null },
    }));
  },

  sort(host, parentUnique, orderedUniques) {
    return run(host, DocumentService.putDocumentSort({
      body: {
        parent: parentUnique ? { id: parentUnique } : null,
        sorting: orderedUniques.map((id, sortOrder) => ({ id, sortOrder })),
      },
    }));
  },
};
```
`TD/src/adapters/media.ts`: the same with header `// Media (Umb.Section.Media) tree adapter — media items can be ordered.`, `mediaAdapter`, `'media'`, `'media-root'`, `'umb-media-tree-item'`, `MediaService.putMediaByIdMove` / `MediaService.putMediaSort`.

`TD/src/entries/content.ts`:
```ts
// backofficeEntryPoint for the Content tree (Umb.Section.Content).
// Emitted by the C# manifest reader only when Esatto:TreeDragAndDrop:Content is true
// and the legacy ContentTreeDragAndDrop package is not installed.

import { registerTree } from '../core/register';
import { contentAdapter } from '../adapters/content';

registerTree(contentAdapter);
```
`TD/src/entries/media.ts`: the same for Media (`mediaAdapter`, `Esatto:TreeDragAndDrop:Media`, legacy MediaTreeDragAndDrop).

`TD/vite.config.ts` entry object becomes:
```ts
      entry: {
        content: "src/entries/content.ts",
        media: "src/entries/media.ts",
        dictionary: "src/entries/dictionary.ts",
      },
```

- [ ] **Step 6: `host.ts` — sorted planning and execution**

a) Imports: add `listSiblings` (tree-item), `planSortedDrop`, `type ActionPlan` (drop-plan; drop `MovePlan` from the import), `optimisticReorder` (./reorder-dom). Add `#showWarn` (from 2.0):
```ts
  #showWarn(message: string): void {
    console.warn(LOG_PREFIX, message);
    this.#notifications?.peek('warning', { data: { message } });
  }
```

b) Replace `#planFor`:
```ts
  #planFor(el: AnyTreeItem, zone: DropZone): DropPlan | null {
    const s = this.#dragState;
    if (!s || adapterOf(el) !== s.adapter) return null;
    const targetUnique = readUnique(el);
    if (!targetUnique) return null;
    if (isBlockedTarget(targetUnique, s.sourceUnique, s.descendantUniques)) return null;
    const ctx = {
      zone,
      sourceParentUnique: s.sourceParentUnique,
      targetUnique,
      targetParentUnique: readParentUnique(el),
    };
    if (!s.adapter.sort) return planDrop(ctx);
    // Siblings are only needed for edge zones; skip the DOM walk for "into".
    const siblings = zone === 'into' ? [] : listSiblings(ctx.targetParentUnique, s.adapter.entityType);
    return planSortedDrop({ ...ctx, sourceUnique: s.sourceUnique, siblings });
  }
```

c) Every `showPlan(plan, adapter)` call becomes `showPlan(plan, adapter, targetEl, zone)`:
- In `#onDragOver`, compute `const zone = getDropZone(el, event.clientY);` once and use it for `#planFor` and `showPlan(plan, this.#dragState.adapter, el, zone)`.
- In the keyboard `'none'` branch: `showPlan(plan, this.#dragState!.adapter, targetEl, r.state.zone)`.

d) `#onDrop`: compute `zone` once, then `await this.#execute(plan, el, zone);`. Keyboard commit: `const ok = await this.#execute(plan, targetEl, r.state.zone);`.

e) Replace `#execute`:
```ts
  // Run a plan for the current #dragState. Returns whether it fully succeeded.
  //   move       → move; reload both branches (chevrons come from the data store).
  //   reorder    → optimistic DOM move first (the sort API is slow), then sort;
  //                roll the DOM back if it fails. No reload on success.
  //   move+sort  → move, then sort. If only the sort fails the item stays moved
  //                (a completed move beats a rollback) and a warning says so.
  // Any failure reloads both branches so the tree matches the server again.
  async #execute(plan: ActionPlan, targetEl: AnyTreeItem, zone: DropZone): Promise<boolean> {
    const s = this.#dragState;
    // In-flight lock: a second drop / Space while a move is running is ignored.
    if (!s || this.#movePending) return false;
    this.#movePending = true;

    const { adapter, sourceUnique, sourceParentUnique } = s;
    const sourceEl = findTreeItemByUnique(sourceUnique, adapter.entityType);
    if (sourceEl) {
      (sourceEl as HTMLElement).style.opacity = '0.4';
      showSpinner(sourceEl);
    }

    let ok = false;
    let reload = true;
    try {
      if (plan.kind === 'move') {
        await adapter.move(this, sourceUnique, plan.parentUnique);
        ok = true;
      } else if (plan.kind === 'reorder') {
        const rollbackParent = sourceEl?.parentNode ?? null;
        const rollbackBefore = sourceEl?.nextSibling ?? null;
        optimisticReorder(sourceEl, targetEl, zone);
        // Re-pin the spinner over the row's NEW position.
        if (sourceEl) showSpinner(sourceEl);
        try {
          await adapter.sort!(this, plan.parentUnique, plan.order);
        } catch (err) {
          if (sourceEl && rollbackParent) rollbackParent.insertBefore(sourceEl, rollbackBefore);
          throw err;
        }
        ok = true;
        reload = false;
      } else {
        await adapter.move(this, sourceUnique, plan.parentUnique);
        try {
          await adapter.sort!(this, plan.parentUnique, plan.order);
          ok = true;
        } catch (sortErr) {
          this.#showWarn(`Moved, but couldn't set the position — it's at the bottom of the new parent. (${(sortErr as Error)?.message ?? sortErr})`);
        }
      }
    } catch (err) {
      this.#showError((err as Error)?.message ?? String(err));
    } finally {
      if (reload) {
        this.#reload(adapter, sourceParentUnique);
        this.#reload(adapter, plan.parentUnique);
      }
      hideSpinner();
      if (sourceEl) (sourceEl as HTMLElement).style.opacity = '';
      this.#movePending = false;
    }
    return ok;
  }
```

f) Update the class header comment: "…runs the adapter's API calls (move, and sort for trees that have it)."

- [ ] **Step 7: Build and check**

Run: `npm test && npm run build`
Expected: 63 tests pass; `tsc` is clean; Vite writes `content-tree-drag-drop.js`, `media-tree-drag-drop.js`, `dictionary-tree-drag-drop.js` and one shared `core-*.js` chunk.
Then check that each entry imports the shared chunk and that the patch flag appears only in the chunk:
```bash
cd ../wwwroot/App_Plugins/Esatto.Umbraco.Backoffice.TreeDragAndDrop
for f in content media dictionary; do grep -o 'from"./core-[^"]*"\|from "./core-[^"]*"' $f-tree-drag-drop.js | head -1; done
grep -l "__esattoTreeDndPatched" *.js
```
Expected: three identical `./core-<hash>.js` imports; the patch flag appears only in `core-<hash>.js` (spec Risk 3).

- [ ] **Step 8: Commit** — `git add` the client and wwwroot; `git commit -m "TreeDragAndDrop: Content and Media adapters with ordering"`

---

### Task 3: C# — Content/Media switches and the legacy guard

**Files:**
- Modify: `src/TreeDragAndDropOptions.cs`, `src/TreeDefinitions.cs`, `src/TreeDragAndDropManifestReader.cs`
- Test: `Esatto.Umbraco.Backoffice.TreeDragAndDrop.Tests/TreeDragAndDropManifestReaderTests.cs`, `TreeDragAndDropRegistrationTests.cs`

**Interfaces:**
- Produces: `TreeDragAndDropOptions.Content`, `.Media` (bool, default true). Reader ctor `(IOptions<TreeDragAndDropOptions>, IPackageManifestFileProviderFactory, ILogger<TreeDragAndDropManifestReader>)`.

- [ ] **Step 1: Failing tests** — replace `TreeDragAndDropManifestReaderTests.cs` with:
```csharp
using Esatto.Umbraco.Backoffice.TreeDragAndDrop;
using Microsoft.Extensions.FileProviders;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using NSubstitute;
using Umbraco.Cms.Core.IO;
using Umbraco.Cms.Core.Manifest;
using Xunit;

namespace Esatto.Umbraco.Backoffice.TreeDragAndDrop.Tests;

public sealed class TreeDragAndDropManifestReaderTests : IDisposable
{
    private const string PackageId = "Esatto.Umbraco.Backoffice.TreeDragAndDrop";
    private readonly string _root = Directory.CreateTempSubdirectory("tdd-").FullName;

    public void Dispose() => Directory.Delete(_root, recursive: true);

    // Simulates an installed legacy package the way Umbraco discovers it: /App_Plugins/<id>/umbraco-package.json.
    private void InstallLegacy(string legacyId)
    {
        string dir = Path.Combine(_root, "App_Plugins", legacyId);
        Directory.CreateDirectory(dir);
        File.WriteAllText(Path.Combine(dir, "umbraco-package.json"), "{}");
    }

    private async Task<List<PackageManifest>> ReadAsync(TreeDragAndDropOptions options, bool noFileProvider = false)
    {
        var factory = Substitute.For<IPackageManifestFileProviderFactory>();
        factory.Create().Returns(noFileProvider ? null : new PhysicalFileProvider(_root));
        var reader = new TreeDragAndDropManifestReader(
            Options.Create(options), factory, NullLogger<TreeDragAndDropManifestReader>.Instance);
        return (await reader.ReadPackageManifestsAsync()).ToList();
    }

    private static List<EntryPointExtension> Entries(PackageManifest manifest) =>
        manifest.Extensions.Cast<EntryPointExtension>().ToList();

    [Theory]
    [InlineData("Content", "content", "Umb.Section.Content")]
    [InlineData("Media", "media", "Umb.Section.Media")]
    [InlineData("Dictionary", "dictionary", "Umb.Section.Translation")]
    public async Task Emits_each_tree_entry_point(string tree, string file, string section)
    {
        var manifest = Assert.Single(await ReadAsync(new TreeDragAndDropOptions()));

        var entry = Assert.Single(Entries(manifest), e => e.Alias == $"{PackageId}.{tree}");
        Assert.Equal("backofficeEntryPoint", entry.Type);
        Assert.Equal($"/App_Plugins/{PackageId}/{file}-tree-drag-drop.js", entry.Js);
        var condition = Assert.Single(entry.Conditions);
        Assert.Equal("Umb.Condition.SectionAlias", condition.Alias);
        Assert.Equal(section, condition.Match);
    }

    [Theory]
    [InlineData("Content")]
    [InlineData("Media")]
    [InlineData("Dictionary")]
    public async Task Each_switch_removes_only_its_tree(string tree)
    {
        var options = new TreeDragAndDropOptions
        {
            Content = tree != "Content",
            Media = tree != "Media",
            Dictionary = tree != "Dictionary",
        };
        var manifest = Assert.Single(await ReadAsync(options));

        var aliases = Entries(manifest).Select(e => e.Alias).ToList();
        Assert.Equal(2, aliases.Count);
        Assert.DoesNotContain($"{PackageId}.{tree}", aliases);
    }

    [Fact]
    public async Task Emits_no_manifest_when_every_tree_is_disabled()
    {
        Assert.Empty(await ReadAsync(new TreeDragAndDropOptions { Content = false, Media = false, Dictionary = false }));
    }

    // Review Focus 3: never two packages on one tree.
    [Theory]
    [InlineData("Esatto.Umbraco.Backoffice.ContentTreeDragAndDrop", "Content")]
    [InlineData("Esatto.Umbraco.Backoffice.MediaTreeDragAndDrop", "Media")]
    public async Task Leaves_out_a_tree_whose_legacy_package_is_installed(string legacyId, string tree)
    {
        InstallLegacy(legacyId);
        var manifest = Assert.Single(await ReadAsync(new TreeDragAndDropOptions()));

        var aliases = Entries(manifest).Select(e => e.Alias).ToList();
        Assert.Equal(2, aliases.Count);
        Assert.DoesNotContain($"{PackageId}.{tree}", aliases);
    }

    [Fact]
    public async Task No_file_provider_means_no_legacy_detected()
    {
        var manifest = Assert.Single(await ReadAsync(new TreeDragAndDropOptions(), noFileProvider: true));
        Assert.Equal(3, manifest.Extensions.Length);
    }

    [Fact]
    public async Task Manifest_is_private_versioned_and_not_counted_twice()
    {
        var manifest = Assert.Single(await ReadAsync(new TreeDragAndDropOptions()));

        // The static umbraco-package.json carries the package's Id, name and telemetry;
        // this manifest only contributes entry points. Packages → Installed lists every
        // manifest with a name, so an empty Name keeps it from showing as a second package.
        Assert.Equal(string.Empty, manifest.Name);
        Assert.Null(manifest.Id);
        Assert.False(manifest.AllowTelemetry);
        Assert.False(manifest.AllowPublicAccess);
        Assert.False(string.IsNullOrWhiteSpace(manifest.Version));
        Assert.DoesNotContain("+", manifest.Version);
    }
}
```

In `TreeDragAndDropRegistrationTests.cs`:
- In `Build(...)`, after `services.AddLogging();` add `services.AddSingleton(Substitute.For<IPackageManifestFileProviderFactory>());`, with `using NSubstitute;` and `using Umbraco.Cms.Core.IO;`.
- In `Missing_section_enables_dictionary`, rename it to `Missing_section_enables_every_tree` and assert `Assert.Equal(3, manifest.Extensions.Length);`.
- In `Invalid_config_value_falls_back_to_defaults`, assert `Assert.Equal(3, manifest.Extensions.Length);`.

Run: `dotnet test Esatto.Umbraco.Backoffice.TreeDragAndDrop.Tests`
Expected: build FAILS. `TreeDragAndDropOptions` has no `Content`/`Media` (CS0117), and the reader has no 3-argument constructor (CS1729).

- [ ] **Step 2: Implement**

`TreeDragAndDropOptions.cs`: add, above `Dictionary`,
```csharp
    /// <summary>Drag-and-drop in the Content section's document tree.</summary>
    public bool Content { get; set; } = true;

    /// <summary>Drag-and-drop in the Media section's tree.</summary>
    public bool Media { get; set; } = true;
```
and change the summary's last line to: `/// Ignored for a tree whose legacy package (ContentTreeDragAndDrop / MediaTreeDragAndDrop) is still installed.`

`TreeDefinitions.cs`:
```csharp
/// <summary>
/// A tree this package can enable: its switch, its section, its client entry file, and the
/// legacy package (if any) that already handles it.
/// </summary>
internal sealed record TreeDefinition(
    string Name,
    string SectionAlias,
    Func<TreeDragAndDropOptions, bool> IsEnabled,
    string? LegacyPackageId = null)
{ /* PackageId + ToEntryPoint unchanged */ }

internal static class TreeDefinitions
{
    public static readonly IReadOnlyList<TreeDefinition> All =
    [
        new("Content", "Umb.Section.Content", options => options.Content, "Esatto.Umbraco.Backoffice.ContentTreeDragAndDrop"),
        new("Media", "Umb.Section.Media", options => options.Media, "Esatto.Umbraco.Backoffice.MediaTreeDragAndDrop"),
        new("Dictionary", "Umb.Section.Translation", options => options.Dictionary),
    ];
}
```

`TreeDragAndDropManifestReader.cs`:
- Add `using Microsoft.Extensions.FileProviders;` and `using Umbraco.Cms.Core.IO;`.
- Add the field `private readonly IPackageManifestFileProviderFactory _fileProviderFactory;` and the ctor parameter (second position), assigned in the ctor.
- Change the selection to `.Where(tree => tree.IsEnabled(options) && !IsTakenByLegacyPackage(tree))`.
- Add:
```csharp
    // Two packages on one tree means double drag handlers. While a legacy package is still
    // installed, it keeps its tree and this package stays out of it. Detected the way Umbraco's
    // AppPluginsPackageManifestReader finds manifests (a static-assets-only package's DLL is
    // never loaded, so an assembly check would not work).
    private bool IsTakenByLegacyPackage(TreeDefinition tree)
    {
        if (tree.LegacyPackageId is null) return false;
        IFileProvider? provider = _fileProviderFactory.Create();
        if (provider is null) return false;

        bool installed = provider.GetDirectoryContents($"/App_Plugins/{tree.LegacyPackageId}")
            .Any(file => !file.IsDirectory && file.Name.Equals("umbraco-package.json", StringComparison.OrdinalIgnoreCase));
        if (installed)
        {
            _logger.LogWarning(
                "{Package}: {Tree} drag-and-drop is left to the legacy package {Legacy}, which is still installed. Remove it to use this package for the {Tree} tree.",
                TreeDefinition.PackageId, tree.Name, tree.LegacyPackageId, tree.Name);
        }
        return installed;
    }
```

Run: `dotnet test Esatto.Umbraco.Backoffice.TreeDragAndDrop.Tests` → Expected: PASS (6 registration + 3 + 3 + 1 + 2 + 1 + 1 = 17 tests).

- [ ] **Step 3: Commit** — `git commit -m "TreeDragAndDrop: Content/Media switches and legacy-package guard"`

---

### Task 4: Live verification (Carl), Content + Media on umbracokalaset26

The test site already has the legacy packages removed and the temporary ProjectReference (from step 1, not committed). Claude rebuilds and restarts it; Carl runs the list; Claude fixes failures test-first.

| # | Where | Do | Expect |
|---|---|---|---|
| 1 | Network | Load the backoffice | `content-`, `media-`, `dictionary-tree-drag-drop.js` and one `core-*.js` load, with no 404s |
| 2 | Content | Drag a child onto the top or bottom edge of a sibling | Blue line above or below it; on drop, the order changes at once and survives a refresh (F5) |
| 3 | Content | Reorder two **top-level** nodes | Same as row 2. The old packages failed here with an error toast |
| 4 | Content | Drag a node onto the middle of another node | That node is outlined; on drop, the dragged node moves inside it |
| 5 | Content | Drag a node to the edge of a row under a **different** parent | It moves there at that position |
| 6 | Content | Drag a node onto the edge of its own neighbour, so nothing would change | No line, nothing happens |
| 7 | Content | Keyboard: Space, then ↑/↓/→, then Space | Line or outline follows; the move happens; a spinner shows while it saves |
| 8 | Content | Recycle bin item | Can't be dragged |
| 9 | Media | Rows 2–7 in the Media tree | Same behaviour |
| 10 | Translation | Quick drag | Still works |
| 11 | Packages → Installed | — | Tree Drag and Drop listed once |
| 12 | Legacy guard | Claude temporarily re-adds the old ContentTreeDragAndDrop package and restarts | Content still drags (via the old package); the log warns; no `content-tree-drag-drop.js` request. Claude then removes the old package again |

---

### Task 5: README, package metadata, legacy deprecation prep

- [ ] **Step 1: README** — rewrite `README.md` for all three trees:
  - Intro: "Drag-and-drop and keyboard moving for the Umbraco 17/18 backoffice **Content**, **Media** and **Dictionary** trees, one package, each tree switchable."
  - "How dropping works": Content/Media: top third = above, middle = inside, bottom third = below, with real ordering. Dictionary: alphabetical, so edges = "next to" (same parent); a top-level row's edge = root. Note that Umbraco has no dictionary sort order.
  - Settings: all three keys.
  - "Replacing ContentTreeDragAndDrop / MediaTreeDragAndDrop": remove the old package(s). While one is still installed, this package leaves that tree to it and logs a warning.
  - Keyboard table: add Enter as an alternative to Space for dropping.
  - Compatibility: 18.x **Verified** (if Task 4 passed), 17.x Supported.
  - Demo: keep the dictionary GIF link, and add `content-tree-dnd-demo.gif` if Carl records one; otherwise reuse the dictionary GIF only.
- [ ] **Step 2: csproj** — Description: "Drag-and-drop and keyboard "grab &amp; place" moving for the Umbraco 17/18 backoffice Content, Media and Dictionary trees, with real ordering for content and media. Each tree can be switched off in appsettings.json (Esatto:TreeDragAndDrop). Replaces Esatto.Umbraco.Backoffice.ContentTreeDragAndDrop and MediaTreeDragAndDrop." Tags: add `content;media`.
- [ ] **Step 3: Legacy deprecation prep (no tags)** — at the top of both legacy READMEs add:
  ```markdown
  > **Deprecated.** Use [Esatto.Umbraco.Backoffice.TreeDragAndDrop](https://www.nuget.org/packages/Esatto.Umbraco.Backoffice.TreeDragAndDrop), which covers the Content, Media and Dictionary trees in one package. Remove this package when you install it.
  ```
  Prefix both csproj `<Description>` values with `Deprecated: use Esatto.Umbraco.Backoffice.TreeDragAndDrop. `.
- [ ] **Step 4: Spec** — record the single-1.0.0 release, the root-reorder fix and the recycle-bin exclusion in the spec's Summary/Step notes.
- [ ] **Step 5: Verify + commit** — `npm test && npm run build`, `dotnet test …Tests`, then `git commit`.
- [ ] **Step 6: Stop for Carl.** Each of these needs his go:
  - Merge to main and push.
  - Tag `Esatto.Umbraco.Backoffice.TreeDragAndDrop-1.0.0`, `…ContentTreeDragAndDrop-2.0.1`, `…MediaTreeDragAndDrop-1.0.2`, then `dotnet pack`. Carl pushes all three to nuget.org.
  - Carl marks the two legacy packages deprecated on nuget.org, with TreeDragAndDrop as the alternative.
  - Revert the test site's temporary ProjectReference, then decide whether to add the published 1.0.0 to `umbracokalaset26` and commit that.
