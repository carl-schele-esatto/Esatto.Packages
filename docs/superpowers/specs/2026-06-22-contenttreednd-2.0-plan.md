# ContentTreeDragAndDrop 2.0 Implementation Plan

> **For Claude:** REQUIRED SUB-SKILL: Use executing-plans to implement this plan task-by-task.

**Goal:** Ship ContentTreeDragAndDrop 2.0 — Umbraco 18 support, a move-in-progress spinner, keyboard-accessible (ARIA APG "grab & place") reordering, robustness fixes, and release polish.

**Architecture:** Pure App_Plugins client package (TS + Vite + Lit), no C# code. New pure logic (keyboard state machine) is split into its own module and unit-tested with Vitest; DOM glue (spinner overlay, keydown wiring, aria-live) mirrors the existing untested DOM modules (`indicator.ts`) and is verified manually in the backoffice. The drag drop pipeline is refactored into one shared `#performMove(...)` that both pointer-drop and keyboard-commit call.

**Tech Stack:** TypeScript 5.9, Vite 7, Lit (via `@umbraco-cms/backoffice/external/lit`), Vitest 2 (node env, pure-logic tests only), MinVer (tag-driven NuGet versioning), .NET 10 Razor SDK packaging.

**Design doc:** [2026-06-22-contenttreednd-2.0-design.md](2026-06-22-contenttreednd-2.0-design.md)

---

## Conventions for this plan

- **Working dir:** `c:\src\Esatto.Packages` (repo), package under `Esatto.Umbraco.Backoffice.ContentTreeDragAndDrop/`. Client code under `…/Client/`.
- **Branch:** `feature/contenttreednd-2.0` (already created). No worktrees (repo/maintainer convention).
- **Run all tests:** `cd Esatto.Umbraco.Backoffice.ContentTreeDragAndDrop/Client && npm test` (→ `vitest run`).
- **Run one test file:** `npx vitest run test/<file>.test.ts` (from `Client/`).
- **Build client:** `cd …/Client && npm run build` (→ `tsc && vite build`, emits to `../wwwroot/App_Plugins/…/content-tree-drag-drop.js`).
- **Manual backoffice verification:** consume the local build from Esatto.Web.Nova (Umbraco 18-rc3). The maintainer runs the Nova app; ask them to hard-refresh the backoffice after a client build.
- **Commits:** included as checkpoints per TDD convention. The maintainer prefers to commit manually — treat each "Commit" step as a checkpoint to run *or* hand to the maintainer. Commit messages end with the repo's `Co-Authored-By` trailer.
- **Test environment is node, not jsdom** — only test pure functions. Anything touching `document`/DOM is verified manually (this is the existing repo pattern: `indicator.ts` is DOM and untested; `drop-zone.ts`/`siblings.ts` split pure logic out for testing).

---

## Phase 0 — Baseline & Umbraco 18 verification

### Task 0.1: Establish a green baseline

**Files:** none (verification only).

**Step 1:** Run the existing test suite.
Run: `cd Esatto.Umbraco.Backoffice.ContentTreeDragAndDrop/Client && npm install && npm test`
Expected: all existing suites pass (`cycle-guard`, `drop-zone`, `reorder`).

**Step 2:** Build the client.
Run: `npm run build`
Expected: build succeeds; `../wwwroot/App_Plugins/Esatto.Umbraco.Backoffice.ContentTreeDragAndDrop/content-tree-drag-drop.js` is regenerated.

**Step 3:** Record the baseline in the task notes (test count, any warnings). Do not commit (no changes).

---

### Task 0.2: Verify Umbraco 18 DOM + API compatibility

**Files:** none yet (findings feed Task 0.3 + any shim).

This is manual verification on Esatto.Web.Nova (Umbraco 18-rc3), which already references this package. Build the client (Task 0.1 Step 2), have the maintainer hard-refresh the Content section, then confirm each probe:

**Step 1 — DOM shape:** In DevTools, confirm content rows are still `<umb-tree-item entitytype="document">` and that `isTreeItem`/`readUnique`/`readParentUnique` ([tree-item.ts](../../../Esatto.Umbraco.Backoffice.ContentTreeDragAndDrop/Client/src/tree-item.ts)) still read a unique + parent. Confirm the expand affordance still responds to `toggleChildren()` or the `show-children`/`is-expanded`/`open` attributes used in [dnd-host.ts:165-173](../../../Esatto.Umbraco.Backoffice.ContentTreeDragAndDrop/Client/src/dnd-host.ts#L165-L173).

**Step 2 — Drag works end-to-end:** Reorder a node within a parent (optimistic), reparent a node `into` another, and across parents. All succeed and persist after refresh.

**Step 3 — API surface:** Confirm no console errors referencing `DocumentService.putDocumentSort` / `putDocumentByIdMove` shape changes, and that tree reloads fire (`UmbRequestReloadChildrenOfEntityEvent`).

**Step 4 — Record outcome.** If all pass → no code change; proceed to Task 0.3. If a probe fails → STOP and add a focused shim task here (out-of-band; not expected).

---

### Task 0.3: Update compatibility metadata

**Files:**
- Modify: `Esatto.Umbraco.Backoffice.ContentTreeDragAndDrop/README.md` (compatibility table)
- Modify: `Esatto.Umbraco.Backoffice.ContentTreeDragAndDrop/Esatto.Umbraco.Backoffice.ContentTreeDragAndDrop.csproj` (`<Description>` if it hard-codes "Umbraco 17")

**Step 1:** In README.md, change the compatibility table to list both:

```markdown
| Umbraco | Status |
|---------|--------|
| 17.x    | Verified |
| 18.x    | Verified |
```

**Step 2:** Keep the min-dep `Umbraco.Cms.Core` at `17.0.0` in the csproj (one package serves 17 and 18). Update the `<Description>` wording from "Umbraco 17 backoffice" → "Umbraco 17/18 backoffice".

**Step 3: Commit**

```bash
git add Esatto.Umbraco.Backoffice.ContentTreeDragAndDrop/README.md Esatto.Umbraco.Backoffice.ContentTreeDragAndDrop/Esatto.Umbraco.Backoffice.ContentTreeDragAndDrop.csproj
git commit -m "docs: mark ContentTreeDragAndDrop verified on Umbraco 18"
```

---

## Phase 1 — Move spinner

### Task 1.1: Create the spinner overlay module

**Files:**
- Create: `Esatto.Umbraco.Backoffice.ContentTreeDragAndDrop/Client/src/spinner.ts`

DOM module mirroring [indicator.ts](../../../Esatto.Umbraco.Backoffice.ContentTreeDragAndDrop/Client/src/indicator.ts) — single reused fixed-position overlay holding a UUI loader, positioned over a row by its rect. (No unit test — DOM glue, verified manually, same as `indicator.ts`.)

**Step 1: Write `spinner.ts`:**

```ts
// A single reused fixed-position overlay holding a UUI spinner, pinned over the
// row whose move is in flight. Mirrors indicator.ts. No managed lifecycle beyond
// show/hide — one element appended to <body>, repositioned per call.

import { getRowRect } from './drop-zone';

const spinner: HTMLDivElement = (() => {
  const el = document.createElement('div');
  el.id = 'backoffice-content-tree-dnd-spinner';
  el.style.cssText = `
    position: fixed;
    pointer-events: none;
    z-index: 100000;
    display: none;
    align-items: center;
    justify-content: center;
    box-sizing: border-box;
  `;
  // uui-loader-circle is registered by the backoffice; safe to use as a tag.
  el.innerHTML = '<uui-loader-circle style="font-size: 1.2em;"></uui-loader-circle>';
  document.body.appendChild(el);
  return el;
})();

export function showSpinner(targetEl: Element): void {
  const rect = getRowRect(targetEl);
  // Pin to the row's left gutter so it sits over the icon area, not the label.
  spinner.style.left = `${rect.left}px`;
  spinner.style.top = `${rect.top}px`;
  spinner.style.width = `${Math.min(rect.height, 28)}px`;
  spinner.style.height = `${rect.height}px`;
  spinner.style.display = 'flex';
}

export function hideSpinner(): void {
  spinner.style.display = 'none';
}
```

**Step 2:** Build to confirm it compiles.
Run: `cd …/Client && npm run build`
Expected: build succeeds (TypeScript + Vite).

**Step 3: Commit**

```bash
git add Esatto.Umbraco.Backoffice.ContentTreeDragAndDrop/Client/src/spinner.ts
git commit -m "feat: add reusable move-spinner overlay module"
```

---

### Task 1.2: Wire the spinner into the drop pipeline

**Files:**
- Modify: `…/Client/src/dnd-host.ts` (import + `#onDrop`)

**Step 1:** Add the import near the other visual imports (after the `indicator` import, [dnd-host.ts:28](../../../Esatto.Umbraco.Backoffice.ContentTreeDragAndDrop/Client/src/dnd-host.ts#L28)):

```ts
import { showSpinner, hideSpinner } from './spinner';
```

**Step 2:** In `#onDrop`, replace the bare dim with dim **+** spinner. Where the source is dimmed today ([dnd-host.ts:206-207](../../../Esatto.Umbraco.Backoffice.ContentTreeDragAndDrop/Client/src/dnd-host.ts#L206-L207)):

```ts
    // Visual feedback: dim the source AND show a spinner while the API is in flight.
    if (sourceEl) {
      (sourceEl as HTMLElement).style.opacity = '0.4';
      showSpinner(sourceEl);
    }
```

**Step 3:** In the `finally` block ([dnd-host.ts:283-285](../../../Esatto.Umbraco.Backoffice.ContentTreeDragAndDrop/Client/src/dnd-host.ts#L283-L285)) hide the spinner alongside clearing opacity:

```ts
    } finally {
      hideSpinner();
      if (sourceEl) (sourceEl as HTMLElement).style.opacity = '';
    }
```

**Step 4:** Build.
Run: `cd …/Client && npm run build`
Expected: succeeds.

**Step 5: Manual verify.** In Nova@18, reorder a node under a parent with many children (the sort API is ~8s) — the moved row shows a spinner for the duration, then settles. Force an error (e.g. offline) → spinner clears and the row un-dims.

**Step 6: Commit**

```bash
git add Esatto.Umbraco.Backoffice.ContentTreeDragAndDrop/Client/src/dnd-host.ts
git commit -m "feat: show spinner on the moving node during sort/move"
```

---

## Phase 2 — Refactor the move pipeline for reuse (DRY)

### Task 2.1: Extract `#performMove` from `#onDrop`

Both pointer-drop and keyboard-commit need the same move/sort/reload/optimistic/rollback/spinner logic. Extract it so `#onDrop` only computes `(targetEl, zone)` from the event, then delegates.

**Files:**
- Modify: `…/Client/src/dnd-host.ts`

**Step 1:** Introduce a private method with the move logic, keyed off explicit inputs (no `DragEvent`):

```ts
  // Shared move pipeline used by BOTH pointer-drop and keyboard-commit.
  // Contains everything that #onDrop's body did after computing (el, zone):
  // cycle-guard recheck, optimistic reorder + rollback, cross-parent move+sort,
  // reloads, spinner, error toasts.
  async #performMove(targetEl: AnyTreeItem, zone: DropZone): Promise<void> {
    if (!this.#dragState) return;
    // ...body moved verbatim from #onDrop (everything after `const zone = …`)...
  }
```

Move the existing body (from `const sourceUnique = this.#dragState.sourceUnique;` through the end of the `finally`) into `#performMove`. `#dragState` already carries `sourceUnique` / `sourceParentUnique` / `descendantUniques`, so keyboard-commit just sets `#dragState` before calling.

**Step 2:** Reduce `#onDrop` to event handling only:

```ts
  async #onDrop(event: DragEvent, el: AnyTreeItem): Promise<void> {
    if (!this.#dragState) return;
    const targetUnique = readUnique(el);
    if (!targetUnique) return;
    if (isBlockedTarget(targetUnique, this.#dragState.sourceUnique, this.#dragState.descendantUniques)) return;
    const zone = getDropZone(el, event.clientY);
    event.preventDefault();
    hideIndicator();
    await this.#performMove(el, zone);
  }
```

**Step 3:** Build.
Run: `cd …/Client && npm run build`
Expected: succeeds, no type errors.

**Step 4: Manual verify (regression).** In Nova@18, re-run all three drag operations from Task 0.2 Step 2 — behavior must be identical (this is a pure refactor).

**Step 5: Commit**

```bash
git add Esatto.Umbraco.Backoffice.ContentTreeDragAndDrop/Client/src/dnd-host.ts
git commit -m "refactor: extract shared #performMove from #onDrop"
```

---

## Phase 3 — Keyboard grab & place (ARIA APG)

### Task 3.1: Write failing tests for the pure keyboard reducer

**Files:**
- Create: `…/Client/test/keyboard.test.ts`

The reducer is pure: given the current insertion state, a pressed key, and the list of visible candidate rows (each with its unique, parent unique, and a `blocked` flag from the cycle guard), it returns the next state plus an optional action (`commit` / `cancel`). DOM glue (building the candidate list, rendering the indicator, focusing, calling `#performMove`) lives in `dnd-host.ts` and is verified manually.

**Step 1: Write the test file:**

```ts
import { describe, it, expect } from 'vitest';
import { reduceKey, type KbCandidate, type KbState } from '../src/keyboard';

// Tree:  A (parent null)
//        ├ B (parent A)
//        └ C (parent A)
//        D (parent null)
// Source being moved = B. Candidates in visual order, B marked blocked
// (can't target itself); descendants of B would also be blocked.
const candidates: KbCandidate[] = [
  { unique: 'A', parentUnique: null, blocked: false },
  { unique: 'B', parentUnique: 'A', blocked: true },
  { unique: 'C', parentUnique: 'A', blocked: false },
  { unique: 'D', parentUnique: null, blocked: false },
];

const start: KbState = { sourceUnique: 'B', targetIndex: 0, zone: 'before' };

describe('reduceKey', () => {
  it('ArrowDown advances to the next non-blocked candidate', () => {
    const r = reduceKey(start, 'ArrowDown', candidates);
    expect(r.type).toBe('none');
    // index 0 (A) → skip 1 (B, blocked) → land on 2 (C)
    expect(r.state?.targetIndex).toBe(2);
    expect(r.state?.zone).toBe('before');
  });

  it('ArrowUp moves to the previous non-blocked candidate, clamped at 0', () => {
    const mid: KbState = { sourceUnique: 'B', targetIndex: 2, zone: 'before' };
    expect(reduceKey(mid, 'ArrowUp', candidates).state?.targetIndex).toBe(0);
    // already at 0 → stays at 0
    expect(reduceKey(start, 'ArrowUp', candidates).state?.targetIndex).toBe(0);
  });

  it('ArrowDown clamps at the last index', () => {
    const last: KbState = { sourceUnique: 'B', targetIndex: 3, zone: 'before' };
    expect(reduceKey(last, 'ArrowDown', candidates).state?.targetIndex).toBe(3);
  });

  it('ArrowRight nests INTO the current target', () => {
    const r = reduceKey(start, 'ArrowRight', candidates);
    expect(r.state?.zone).toBe('into');
    expect(r.state?.targetIndex).toBe(0);
  });

  it('ArrowLeft pops OUT to the parent row (after it)', () => {
    // target C (index 2, parent A) + ArrowLeft → jump to A (index 0), zone after
    const onC: KbState = { sourceUnique: 'B', targetIndex: 2, zone: 'before' };
    const r = reduceKey(onC, 'ArrowLeft', candidates);
    expect(r.state?.targetIndex).toBe(0); // A
    expect(r.state?.zone).toBe('after');
  });

  it('ArrowLeft on a top-level row is a no-op (no parent)', () => {
    const onD: KbState = { sourceUnique: 'B', targetIndex: 3, zone: 'before' };
    const r = reduceKey(onD, 'ArrowLeft', candidates);
    expect(r.type).toBe('none');
    expect(r.state?.targetIndex).toBe(3);
    expect(r.state?.zone).toBe('before');
  });

  it('Space (or Enter) commits the current state', () => {
    expect(reduceKey(start, ' ', candidates).type).toBe('commit');
    expect(reduceKey(start, 'Enter', candidates).type).toBe('commit');
  });

  it('does not commit when the resolved target is blocked', () => {
    const onB: KbState = { sourceUnique: 'B', targetIndex: 1, zone: 'into' };
    expect(reduceKey(onB, ' ', candidates).type).toBe('none');
  });

  it('Escape cancels', () => {
    expect(reduceKey(start, 'Escape', candidates).type).toBe('cancel');
  });

  it('ignores unrelated keys', () => {
    const r = reduceKey(start, 'a', candidates);
    expect(r.type).toBe('none');
    expect(r.state).toEqual(start);
  });
});
```

**Step 2: Run to confirm failure.**
Run: `cd …/Client && npx vitest run test/keyboard.test.ts`
Expected: FAIL — `Cannot find module '../src/keyboard'`.

---

### Task 3.2: Implement the pure keyboard reducer

**Files:**
- Create: `…/Client/src/keyboard.ts`

**Step 1: Write `keyboard.ts`:**

```ts
// Pure state machine for keyboard "grab & place" reordering. No DOM access — the
// caller (dnd-host.ts) supplies the visible candidate rows and applies the
// resulting state to the indicator / move pipeline.

import type { DropZone } from './drop-zone';

export interface KbCandidate {
  unique: string;
  parentUnique: string | null;
  blocked: boolean; // cycle-guard: the source itself or one of its descendants
}

export interface KbState {
  sourceUnique: string;
  targetIndex: number;
  zone: DropZone; // 'before' | 'into' | 'after'
}

export type KbAction =
  | { type: 'none'; state: KbState }
  | { type: 'commit'; state: KbState }
  | { type: 'cancel' };

function nextNonBlocked(from: number, dir: 1 | -1, candidates: KbCandidate[]): number {
  let i = from + dir;
  while (i >= 0 && i < candidates.length) {
    if (!candidates[i].blocked) return i;
    i += dir;
  }
  return from; // clamp: no non-blocked candidate in that direction
}

export function reduceKey(state: KbState, key: string, candidates: KbCandidate[]): KbAction {
  switch (key) {
    case 'ArrowDown':
      return { type: 'none', state: { ...state, targetIndex: nextNonBlocked(state.targetIndex, 1, candidates), zone: 'before' } };
    case 'ArrowUp':
      return { type: 'none', state: { ...state, targetIndex: nextNonBlocked(state.targetIndex, -1, candidates), zone: 'before' } };
    case 'ArrowRight':
      return { type: 'none', state: { ...state, zone: 'into' } };
    case 'ArrowLeft': {
      const parentUnique = candidates[state.targetIndex]?.parentUnique ?? null;
      if (!parentUnique) return { type: 'none', state };
      const parentIndex = candidates.findIndex((c) => c.unique === parentUnique);
      if (parentIndex < 0) return { type: 'none', state };
      return { type: 'none', state: { ...state, targetIndex: parentIndex, zone: 'after' } };
    }
    case ' ':
    case 'Enter': {
      const target = candidates[state.targetIndex];
      if (!target || target.blocked) return { type: 'none', state };
      return { type: 'commit', state };
    }
    case 'Escape':
      return { type: 'cancel' };
    default:
      return { type: 'none', state };
  }
}
```

**Step 2: Run tests.**
Run: `cd …/Client && npx vitest run test/keyboard.test.ts`
Expected: PASS (all cases).

**Step 3:** Run the full suite to confirm no regressions.
Run: `npm test`
Expected: all suites pass.

**Step 4: Commit**

```bash
git add Esatto.Umbraco.Backoffice.ContentTreeDragAndDrop/Client/src/keyboard.ts Esatto.Umbraco.Backoffice.ContentTreeDragAndDrop/Client/test/keyboard.test.ts
git commit -m "feat: pure keyboard grab-and-place reducer with tests"
```

---

### Task 3.3: Add the aria-live announcement helper

**Files:**
- Create: `…/Client/src/announce.ts`

DOM module (no unit test). A single visually-hidden `aria-live="polite"` region appended to `<body>`.

**Step 1: Write `announce.ts`:**

```ts
// Single visually-hidden polite live region for screen-reader announcements
// during keyboard grab & place.

const region: HTMLDivElement = (() => {
  const el = document.createElement('div');
  el.id = 'backoffice-content-tree-dnd-live';
  el.setAttribute('aria-live', 'polite');
  el.setAttribute('aria-atomic', 'true');
  el.style.cssText = `
    position: absolute; width: 1px; height: 1px; margin: -1px; padding: 0;
    overflow: hidden; clip: rect(0 0 0 0); clip-path: inset(50%); border: 0;
  `;
  document.body.appendChild(el);
  return el;
})();

export function announce(message: string): void {
  // Reset then set so identical consecutive messages are still announced.
  region.textContent = '';
  region.textContent = message;
}
```

**Step 2:** Build to confirm it compiles.
Run: `cd …/Client && npm run build`
Expected: succeeds.

**Step 3: Commit**

```bash
git add Esatto.Umbraco.Backoffice.ContentTreeDragAndDrop/Client/src/announce.ts
git commit -m "feat: add aria-live announcement helper"
```

---

### Task 3.4: Wire keyboard grab & place into the host

**Files:**
- Modify: `…/Client/src/dnd-host.ts`

This is the DOM glue: a document-level capture `keydown` listener, `#kbState`, building the candidate list, applying reducer output to the indicator, committing via `#performMove`, announcing, and restoring focus. Verified manually (no unit test).

**Step 1:** Add imports:

```ts
import { reduceKey, type KbCandidate, type KbState } from './keyboard';
import { announce } from './announce';
```

**Step 2:** Add `#kbState` field and a helper that builds the visible-candidate list in DOM order. Use the existing tree-item discovery (`findTreeItemByUnique`, `readUnique`, `readParentUnique`) plus `collectDescendantUniques` for the `blocked` flag. Sketch:

```ts
  #kbState: KbState | null = null;

  #buildCandidates(sourceUnique: string, descendantUniques: Set<string>): { list: KbCandidate[]; els: AnyTreeItem[] } {
    // Query all rendered tree items across shadow roots (reuse observer's known
    // roots or a composed query); map each to {unique, parentUnique, blocked}.
    // blocked = unique === sourceUnique || descendantUniques.has(unique).
    // Return the parallel element array so the caller can render the indicator
    // and resolve the target element for #performMove.
  }
```

(Implementation detail: reuse the same root-walking the observer already does. If a shared "list all tree items" helper doesn't exist, add one to `tree-item.ts` and unit-test only its pure mapping if it can be made pure; otherwise verify manually.)

**Step 3:** In `#installGlobalListeners`, add a capture-phase `keydown`:

```ts
    document.addEventListener('keydown', (e) => this.#onKeyDown(e), true);
```

**Step 4:** Implement `#onKeyDown`:
- If `#kbState` is null: only react to Space/Enter on a focused tree item → start grab. Set `#dragState` (so `#performMove` and the cycle guard work) and `#kbState = { sourceUnique, targetIndex: <index of source>, zone: 'before' }`. `announce('Grabbed <name>. Use arrow keys to choose a position, space to drop, escape to cancel.')`. `e.preventDefault()`.
- If `#kbState` is set: `const r = reduceKey(this.#kbState, e.key, candidates)`. `e.preventDefault()` (only while grabbed — avoids hijacking normal tree keys). Then:
  - `none` → update `#kbState = r.state`, `renderIndicator(els[r.state.targetIndex], r.state.zone)`, announce the new position succinctly.
  - `commit` → resolve `targetEl = els[r.state.targetIndex]`; `hideIndicator()`; `await this.#performMove(targetEl, r.state.zone)`; `announce('Moved <name>.')`; restore focus to the moved node by unique (best-effort, after any reload); clear `#kbState` + `#dragState`.
  - `cancel` → `hideIndicator()`; `announce('Move cancelled.')`; clear `#kbState` + `#dragState`; keep focus on the source.

**Step 5:** Build.
Run: `cd …/Client && npm run build`
Expected: succeeds.

**Step 6: Manual verify (keyboard + AT).** In Nova@18:
- Tab/arrow to focus a content node. Press Space → hear "Grabbed …".
- ArrowDown/Up moves the indicator; ArrowRight nests (into); ArrowLeft pops out.
- Space commits → spinner shows → tree updates → focus lands on the moved node; "Moved …" announced.
- Esc mid-grab cancels cleanly.
- Confirm Space/arrows don't break normal (un-grabbed) tree navigation.

**Step 7: Commit**

```bash
git add Esatto.Umbraco.Backoffice.ContentTreeDragAndDrop/Client/src/dnd-host.ts Esatto.Umbraco.Backoffice.ContentTreeDragAndDrop/Client/src/tree-item.ts
git commit -m "feat: keyboard grab-and-place reordering (ARIA APG)"
```

---

## Phase 4 — Robustness

### Task 4.1: In-flight move lock

**Files:**
- Modify: `…/Client/src/dnd-host.ts`

**Step 1:** Add `#movePending = false`. Set it `true` at the start of `#performMove` and `false` in its `finally`.

**Step 2:** Guard entry points:
- `#onDragStart`: `if (this.#movePending) { event.preventDefault(); return; }`
- `#onKeyDown` grab branch: `if (this.#movePending) return;`

**Step 3:** Build.
Run: `cd …/Client && npm run build`
Expected: succeeds.

**Step 4: Manual verify.** Start a slow reorder; while the spinner is up, attempt a second drag and a second keyboard grab — both are ignored until the first completes.

**Step 5: Commit**

```bash
git add Esatto.Umbraco.Backoffice.ContentTreeDragAndDrop/Client/src/dnd-host.ts
git commit -m "fix: ignore new moves while one is in flight"
```

---

### Task 4.2: Tighten cross-parent "couldn't compute slot" handling

**Files:**
- Modify: `…/Client/src/dnd-host.ts` (the two `#showWarn('Moved, but couldn't set the position …')` branches in `#performMove`)

**Step 1:** Confirm both branches still reload both affected parents and surface the warning (they do today). Add a brief comment documenting the accepted fallback (node lands at the bottom of the new parent) so it isn't "fixed" away later. No behavior change unless Task 0.2/3.4 surfaced a real defect.

**Step 2:** Build + manual spot-check a cross-parent move into a collapsed/unloaded parent.
Run: `cd …/Client && npm run build`

**Step 3: Commit** (only if changed)

```bash
git add Esatto.Umbraco.Backoffice.ContentTreeDragAndDrop/Client/src/dnd-host.ts
git commit -m "docs: document cross-parent slot fallback"
```

---

## Phase 5 — Polish & release

### Task 5.1: Sync the manifest version field

**Files:**
- Modify: `…/Client/public/umbraco-package.json` (`version`)

**Step 1:** The NuGet package version is MinVer/tag-driven; the `umbraco-package.json` `version` is a separate, display-only field currently stale at `0.1.0`. Set it to `2.0.0` to match the upcoming release.

```json
  "version": "2.0.0",
```

**Step 2:** Build (copies the manifest into `wwwroot`).
Run: `cd …/Client && npm run build`
Expected: succeeds; `wwwroot/App_Plugins/…/umbraco-package.json` shows `2.0.0`.

**Step 3: Commit**

```bash
git add Esatto.Umbraco.Backoffice.ContentTreeDragAndDrop/Client/public/umbraco-package.json
git commit -m "chore: bump manifest version to 2.0.0"
```

---

### Task 5.2: README — features, keyboard shortcuts, a11y

**Files:**
- Modify: `Esatto.Umbraco.Backoffice.ContentTreeDragAndDrop/README.md`

**Step 1:** Add to the feature list: move-in-progress spinner; keyboard-accessible reordering. Add a shortcuts table:

```markdown
## Keyboard reordering

| Key | Action |
|-----|--------|
| Space / Enter | Grab the focused node / drop it at the chosen position |
| ↑ / ↓ | Move the insertion point between rows |
| → | Nest into the highlighted node (as a child) |
| ← | Pop out to the parent level |
| Esc | Cancel the move |

Announced via an `aria-live` region for screen-reader users (WCAG 2.2 AA).
```

**Step 2: Commit**

```bash
git add Esatto.Umbraco.Backoffice.ContentTreeDragAndDrop/README.md
git commit -m "docs: document spinner + keyboard reordering"
```

---

### Task 5.3: Full green + final build

**Files:** none.

**Step 1:** Full test run.
Run: `cd …/Client && npm test`
Expected: all suites pass (cycle-guard, drop-zone, reorder, keyboard).

**Step 2:** Clean build.
Run: `npm run build`
Expected: succeeds; `wwwroot/App_Plugins/…/content-tree-drag-drop.js` regenerated.

**Step 3:** Final manual regression in Nova@18 — pointer drag (reorder/into/cross-parent), keyboard grab & place, spinner, in-flight lock, error rollback. All green.

---

### Task 5.4: Release 2.0.0

**Files:** none (tag + pack).

**Step 1:** Ensure all Phase 0–5 commits are in on `feature/contenttreednd-2.0` and merged per the maintainer's preference (they merge/commit manually).

**Step 2:** Tag the release (MinVer reads this prefix):

```bash
git tag Esatto.Umbraco.Backoffice.ContentTreeDragAndDrop-2.0.0
```

**Step 3:** Pack (the csproj's `AutoPushAfterPack` target pushes to the `esatto-packages` feed with `--skip-duplicate`):

```bash
dotnet pack Esatto.Umbraco.Backoffice.ContentTreeDragAndDrop/Esatto.Umbraco.Backoffice.ContentTreeDragAndDrop.csproj -c Release
```

Expected: produces `…2.0.0.nupkg` and auto-pushes. Confirm MinVer stamped `2.0.0` (check the generated nuspec under `obj/Release/…2.0.0.nuspec`).

**Step 4:** Bump the consumer. In `c:\src\Esatto.Web.Nova\src\Esatto.Web.Nova.csproj`, change the `Esatto.Umbraco.Backoffice.ContentTreeDragAndDrop` reference `Version="1.0.2"` → `Version="2.0.0"`, restore, and verify in the running app.

**Step 5: Commit (consumer)** — left to the maintainer per Nova's manual-commit workflow.

---

## Done criteria

- All Vitest suites pass (incl. new `keyboard.test.ts`).
- Pointer drag unchanged; spinner visible during moves; in-flight lock prevents double-moves.
- Keyboard grab & place works end-to-end with aria-live announcements and focus restoration.
- Verified on Umbraco 18-rc3 (and still valid on 17.x via unchanged min-dep).
- README + manifest reflect 2.0; package tagged + pushed as `2.0.0`; Nova consumes `2.0.0`.
