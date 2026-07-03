# Inline Drag-and-Drop Sorting and Moving in the Content Tree

Request for Contribution (RFC) 0000 : _Inline drag-and-drop sorting and moving in the content tree_

## Code of conduct

Please read and respect the [RFC Code of Conduct](https://github.com/umbraco/rfcs/blob/master/CODE_OF_CONDUCT.md).

## Intended Audience

- Umbraco HQ (backoffice / Bellissima team, Management API owners)
- Package authors who currently ship tree-interaction extensions
- Editors and implementors who structure large content trees day-to-day

## Summary

Editors can already **reorder** a node's children (the **Sort** entity action → Sort modal) and **move** a node (the **Move** action → tree-picker modal). Both are modal-driven: you leave the tree, operate in a dialog, and come back.

This RFC proposes adding the **inline gesture** that has been requested since the early days of Umbraco ([U4-229](https://issues.umbraco.org/issue/U4-229)): dragging a node directly in the content tree to reorder it among its siblings, or to move it under a new parent — **reusing the existing sort/move repositories and Management API endpoints unchanged**, and using the backoffice's own [`UmbSorterController`](https://docs.umbraco.com/umbraco-backoffice/extending/sorting) for the drag mechanics.

A working proof-of-concept already exists as a third-party package (`Esatto.Umbraco.Backoffice.ContentTreeDragAndDrop`, compatible with 17.x and 18.x), implemented as an external DOM shim. It demonstrates the UX is wanted and viable, but the shim approach has structural limits that only a native, in-tree implementation can remove. This RFC is about doing it the right way, inside core.

## Motivation

**Why do this**

- It is one of Umbraco's longest-standing editor requests (U4-229). The modal-based Sort/Move flows work, but inline drag is the gesture editors expect from a tree and reach for first.
- The hard parts are already built: the **Management API** (`PUT .../document/sort` and `PUT .../document/{id}/move`), the **repositories** (`sort-children-of.repository`, the move repository), the **entity actions/modals**, and the granular **Sort/Move permissions** all ship today. The only missing piece is a presentation-layer gesture that feeds those existing flows.
- The drag primitive is also already in core: `UmbSorterController` (used by block editors and other sortable UIs) supports nested containers, cross-container moves, placeholders, and keyboard interaction out of the box.

**Why it belongs in core rather than a package**

External shims (including the existing proof-of-concept) cannot do this cleanly. The content tree is owned and rendered by core: its children come from the tree **store/observables**, not from a model an extension controls. To add drag from the outside you must intercept events at the document **capture phase** (Bellissima's own sorters call `stopPropagation`), force `draggable` onto every rendered row, optimistically mutate the DOM and roll back on failure, and monkey-patch prototypes to reach into shadow roots. None of that is necessary inside core, where the tree store can remain the single source of truth and `UmbSorterController` can be attached to the elements that actually render the children.

**Expected outcome**

Editors can drag a node to reorder it under its current parent, or onto/with another node to re-parent it, with the same permission checks, confirmation, and persistence guarantees as the existing Sort/Move actions — and with full keyboard and screen-reader support. The Sort and Move modals remain available and unchanged.

## Detailed Design

### Building blocks that already exist (verified against 18.0.0-rc3)

- `packages/documents/sort-children-of.repository` → `PUT /umbraco/management/api/v1/document/sort` with body `{ parent: { id } | null, sorting: [{ id, sortOrder }] }`.
- Move repository → `PUT /umbraco/management/api/v1/document/{id}/move` with body `{ target: { id } | null }` (`null` = root).
- `sort-children-of-content.action` + `umb-sort-children-of-content-modal` (a paged list of children with load-more; note the `_hasMorePages()` logic).
- Tree composition: `<umb-tree-item>` hosting a kind element (`<umb-document-tree-item>`) whose children are loaded and paged through the tree data source / item context.
- `UmbSorterController` from `@umbraco-cms/backoffice/sorter`.

### Proposed approach

1. **Attach a sorter per children-container.** When a tree item renders its loaded children, attach a `UmbSorterController` to that children container. Configure it with:
   - `getUniqueOfElement` → the child tree item's `unique`
   - `getUniqueOfModel` → the child entity's `unique`
   - `itemSelector` → the child tree-item element; `containerSelector` → the children wrapper
   - a **shared `identifier`** (e.g. `Umb.Sorter.ContentTree`) used by **every** content-tree children-container, so the controller can move an item from one parent's container into another's. This is exactly the nested-containers pattern from the sorter examples, applied to the tree.

2. **Translate a drop into the existing repositories — no optimistic DOM.**
   - **Same parent (reorder):** call the sort repository with the new ordering of the loaded children.
   - **Different parent (re-parent):** call the move repository to the new target, then apply ordering at the destination.
   - On success, let the tree refresh from the store via the existing `UmbRequestReloadChildrenOfEntityEvent` for the affected parents. The **store stays the source of truth**; the controller's model and the rendered DOM follow from it. No manual node surgery, no rollback path.

3. **Gate on permissions.** Reuse the same Sort/Move permission checks the existing entity actions already enforce (including granular per-node permissions). The drag affordance / `sorter.enable()` is only active where the current user may sort the parent or move the node; otherwise the sorter is disabled for that container. This closes the class of issues seen historically when sorting/moving ignored granular permissions (e.g. [#6682](https://github.com/umbraco/Umbraco-CMS/issues/6682)).

4. **Confirm cross-parent moves.** Re-parenting (as opposed to reordering) shows a confirmation, as the original U4-229 request specified. Same-parent reordering does not require confirmation. This behaviour should be configurable.

5. **Accessibility and keyboard.** Use `UmbSorterController`'s keyboard support to provide an APG-style "grab → move → place" interaction, with `aria-live` announcements for grab/move/drop/cancel. (The proof-of-concept package already implements this pattern and can serve as a reference.)

6. **Opt-in / configuration.** Ship behind a setting (default off, or default on for small trees — see Unresolved Issues), with a clean way for an implementor to disable it per section or globally.

### Reference implementation

The `ContentTreeDragAndDrop` package is offered as a behavioural reference and proof-of-concept (demo GIF in its README) — **not** as code to be ported. Its event-handling and optimistic-DOM scaffolding exist only because it runs outside core; the native version should discard all of it in favour of the sorter + store + repositories.

## Drawbacks

- **Paged/virtualized children** make "drop at position N" ambiguous when only part of a node's children are loaded (see Unresolved Issues). This is the main reason the feature has historically been delivered as a modal.
- More moving parts in a very high-traffic, high-visibility UI surface; regressions here are felt by every editor.
- Drag affordances must not interfere with existing tree interactions (expand/collapse, context menu, selection, search results).
- A new gesture adds a small discoverability/learnability surface and another accessibility surface to maintain.

## Alternatives

1. **Keep the status quo** — Sort and Move modals only. Zero risk, but leaves the most-requested gesture unimplemented.
2. **Enhance the Sort modal instead of the tree** — e.g. richer drag in the existing dialog. Sidesteps the paging problem (the modal bounds the child set) but does not deliver the inline gesture editors are asking for.
3. **Leave it to packages** — the external-shim approach this RFC supersedes. Viable as a stopgap, but inherently fragile (capture-phase listeners, prototype patching, optimistic DOM) and unable to use the store as source of truth or honour virtualization. Not a foundation core should depend on.

## Out of Scope

- **Media tree** drag-and-drop (could follow the same pattern in a later RFC; deliberately excluded here to keep the discussion focused).
- **Multi-node** drag (moving a selection of nodes at once).
- Changes to the **server-side** sort/move semantics beyond what is raised in Unresolved Issues.
- Drag-and-drop **between sections** or into pickers/other surfaces.
- Document **list views** (which have their own sort affordances).

## Unresolved Issues

The answers we are hoping to get from the community & Umbraco HQ are:

1. **Paging / virtualization — the central question.** Children are lazy-loaded and paged. How should an inline drop behave when not all siblings are loaded?
   - **Option A (recommended MVP):** enable inline DnD only when a node's full child set is loaded / below the paging threshold; otherwise fall back to the existing Sort modal. Low risk; matches today's behaviour for large sets.
   - **Option B (follow-up, needs API work):** support **insert-relative-to-sibling** ordering ("place before/after node X") so a drop can be expressed without the full child list. The current sort endpoint takes an explicit `sorting` array, which presumes the whole (loaded) ordering — so Option B would require a Management API addition. **HQ owns the Management API**, so this needs HQ direction.
   - How should cross-page drags behave (auto-scroll/auto-load near edges)? Is "drop into an unloaded region" simply disallowed?

2. **Permissions surface.** Is the right gate the existing Sort permission for the parent plus Move permission for the node? How should the affordance behave when a user may reorder but not re-parent (or vice versa)?

3. **Default state.** Off by default, on by default, or on-only-for-small-trees? Per-section opt-out?

4. **Confirmation.** Confirm on every re-parent, or only across certain boundaries (e.g. different content-type/permission scopes)?

5. **Variants/cultures.** Any interaction with culture-variant trees that affects sort order or move validity?

## Related RFCs

- [0021 – Future-proofing the Umbraco backoffice](https://github.com/umbraco/rfcs/blob/main/cms/0021-future-proofing-the-umbraco-backoffice.md)
- [0023 – Define the backoffice extension API](https://github.com/umbraco/rfcs/blob/main/cms/0023-define-the-backoffice-extension-api.md)
- [0024 – Implement the new backoffice](https://github.com/umbraco/rfcs/blob/main/cms/0024-implement-the-new-backoffice.md)
- Docs: [Sorting — New Umbraco Backoffice](https://docs.umbraco.com/umbraco-backoffice/extending/sorting)
- Historic request: [U4-229](https://issues.umbraco.org/issue/U4-229)

## Contributors

This RFC was compiled by:

* Carl Schéle (Esatto) — author of the `ContentTreeDragAndDrop` proof-of-concept package
