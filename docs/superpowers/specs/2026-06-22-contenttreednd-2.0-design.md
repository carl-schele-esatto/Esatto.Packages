# ContentTreeDragAndDrop 2.0 — Design

**Date:** 2026-06-22
**Package:** `Esatto.Umbraco.Backoffice.ContentTreeDragAndDrop`
**Repo dir:** `Esatto.Umbraco.Backoffice.ContentTreeDragAndDrop/`
**Current shipped version:** 1.0.2 → target **2.0.0**
**Branch:** `feature/contenttreednd-2.0`

## Summary

A single 2.0 release that advances the package on five fronts at once:

1. **Umbraco 18 support** (verify-and-badge, not a rewrite)
2. **Move-in-progress spinner** on the node being moved
3. **Keyboard-accessible reorder** via the ARIA APG "grab & place" pattern
4. **Robustness** fixes (in-flight lock, cross-parent edge cases)
5. **Release polish** (version sync, tests, README, a11y notes)

Driven by: the consuming site (Esatto.Web.Nova) moved to Umbraco 18-rc3, and the current
move UX gives no progress feedback during the slow (~8s) sort API.

### Out of scope (explicitly deferred)

- Media-tree drag-and-drop
- Multi-node drag
- Configurable options (appsettings/manifest knobs)

## Background — current architecture

Pure App_Plugins client package (TS + Vite + Lit), no C# code. The csproj already targets
`net10.0` and declares a min-only `Umbraco.Cms.Core 17.0.0` dependency (for Marketplace
discovery). Key pieces:

- `observer.ts` — patches `Element.prototype.attachShadow` at module load to see every shadow
  root (Bellissima nests the tree in shadow DOM), observes for `umb-tree-item` insertions.
- `dnd-host.ts` — a single hidden `<backoffice-content-tree-dnd>` Lit element mounted inside
  `<umb-app>`; owns drag state, installs document-level capture-phase drag listeners, runs the
  Management API calls via `DocumentService` (`putDocumentSort`, `putDocumentByIdMove`), and
  dispatches `UmbRequestReloadChildrenOfEntityEvent` to refresh branches.
- `indicator.ts` — one reused fixed-position element repositioned per row to draw the
  before/into/after drop marker.
- `cycle-guard.ts` / `siblings.ts` / `drop-zone.ts` / `tree-item.ts` — pure helpers, unit-tested
  with Vitest.

The drop pipeline already does optimistic same-parent reorder with rollback, cross-parent move
+ sort, hover-to-expand, and a cycle guard. During the in-flight API call the source row is only
dimmed to `opacity: 0.4` — no progress affordance.

## Workstream A — Umbraco 18 compatibility

**Approach: verify, don't rewrite.** One codebase serves both 17 and 18.

Verification checklist (manual, on Nova@18-rc3):
- `umb-tree-item[entitytype="document"]` DOM shape unchanged; `toggleChildren()` /
  `show-children` / `is-expanded` / `open` attribute probes still hold.
- `DocumentService.putDocumentSort` and `putDocumentByIdMove` request shapes unchanged.
- Context tokens still resolve: `UMB_NOTIFICATION_CONTEXT`, `UMB_ACTION_EVENT_CONTEXT`,
  `UmbRequestReloadChildrenOfEntityEvent`.

Outcome:
- Keep min-dep `Umbraco.Cms.Core` at `17.0.0` so the package serves 17 **and** 18.
- README compatibility matrix → "17.x ✓ / 18.x ✓".
- Add a thin shim **only** if a probe fails (low probability — Management API + tree DOM are
  stable across 17→18).

## Workstream B — Move spinner

New `spinner.ts`, structurally mirroring `indicator.ts`:

- One reused fixed-position overlay element holding a UUI `<uui-loader-circle>`.
- `showSpinner(el)` — pin over a row by its rect (reuse `getRowRect`), display the loader.
- `hideSpinner()` — hide.

Wiring in `dnd-host.ts`:
- Call `showSpinner(sourceEl)` at the moment a move/sort API call starts (drop handler), keep the
  existing `opacity: 0.4` dim underneath.
- `hideSpinner()` in the `finally` of `#onDrop` (covers success, error, and rollback paths).
- For optimistic same-parent reorder the spinner tracks the moved row's new position; for
  `into` / cross-parent moves it stays on the source until the reload completes.
- The same `showSpinner`/`hideSpinner` calls serve keyboard-initiated moves (Workstream C).

This is also the perceived-latency mitigation for the ~8s `putDocumentSort` — there is no
server-side fix available from a client-only package.

## Workstream C — Keyboard-accessible reorder (Grab & place, ARIA APG)

The native HTML5 drag interaction has no keyboard path — a WCAG 2.2 AA gap. Add an accessible
analogue using the ARIA Authoring Practices "grab and place" model.

A document-level capture-phase `keydown` handler (mirroring the drag listeners), keyed off the
focused `umb-tree-item`:

- **Space** on a focused tree item → enter "grabbed" state. Maintain `#kbState` parallel to
  `#dragState` (source unique, source parent unique, descendant-unique set for the cycle guard).
- **↓ / ↑** → move the insertion point across visible tree items (before/after); reuse
  `renderIndicator()` to draw the marker.
- **→** → set zone to `into` (nest as child of the focused target); **←** → pop out to the parent
  level.
- Collapsed target under the insertion point auto-expands after a short dwell, reusing
  `HOVER_EXPAND_MS`.
- **Space / Enter** → commit. Routes through the **same** move/sort pipeline as drop, so the
  cycle guard, optimistic reorder, rollback, reload, and spinner all apply unchanged.
- **Esc** or blur → cancel; hide the indicator; clear `#kbState`.

Accessibility:
- A visually-hidden `aria-live="polite"` region announces: grab ("Grabbed X. Use arrow keys to
  choose a position."), position changes, result ("Moved X" / "Cancelled").
- After a completed move, return focus to the moved node by its unique (best-effort after any
  reload).

Key choices (easy to revisit): **Space** = grab/drop, **Esc** = cancel.

## Workstream D — Robustness

- **In-flight lock:** a flag on the host that ignores a new drag-start or keyboard-grab while a
  move is pending — prevents double-drops and overlapping sort calls.
- Tighten the two cross-parent "couldn't compute slot" branches already flagged in the `#onDrop`
  comments (currently warn + leave at bottom of new parent).
- (Perceived) slow-sort latency addressed by the spinner (Workstream B).

## Workstream E — Release polish

- Fix stale `umbraco-package.json` `version` (currently `0.1.0`; package is at `1.0.2`) — set to
  the real version or wire from build.
- Vitest coverage for the new keyboard state machine, insertion-point math, and spinner
  show/hide; keep existing cycle-guard / drop-zone / reorder suites green.
- README: feature list, keyboard shortcuts table, compatibility matrix, a11y note.
- Version → **2.0.0** (new keyboard feature + spinner; no breaking consumer API, but a meaningful
  feature jump). Pack / AutoPush-to-feed flow unchanged.

## Testing strategy

- **Unit (Vitest):** pure logic only — keyboard state machine transitions, insertion-point
  computation, spinner show/hide, plus the existing helper suites.
- **Manual backoffice verification (Nova@18-rc3):** DOM/API probes, drag + keyboard flows, focus
  restoration, screen-reader announcements. (No DOM-render harness exists in the repo.)

## Risks

1. **Key collisions** — Space/arrow keys clashing with Umbraco's own tree key handling. Mitigate
   with capture phase + `preventDefault` applied only while in grabbed state.
2. **Focus restoration** across the post-move reload (cross-parent / `into`). Best-effort refocus
   by unique.
3. **U18 API drift** — low probability; mitigated by the verification checklist and a shim only if
   needed.

## Versioning & release

- Single release: **2.0.0**.
- Tag convention (matches repo history): `Esatto.Umbraco.Backoffice.ContentTreeDragAndDrop-2.0.0`.
- After release, consumer (Nova) bumps its `<PackageReference>` from `1.0.2` to `2.0.0`.
