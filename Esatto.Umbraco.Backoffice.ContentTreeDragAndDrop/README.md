# Esatto.Umbraco.Backoffice.ContentTreeDragAndDrop

Native HTML5 drag-and-drop **and keyboard reordering** for the Umbraco 17/18 backoffice **Content** tree.

- Three-zone drop targets per row: above sibling / into as child / below sibling
- Optimistic same-parent reorder — instant visual feedback, rolled back on API error
- Full cross-parent move (reparent) support
- **Move-in-progress spinner** on the node being moved
- **Keyboard-accessible reordering** (ARIA "grab & place") — WCAG 2.2 AA, with screen-reader announcements
- Hover-to-expand collapsed branches mid-drag
- Cycle guard against dropping a node into its own descendants

Pure App_Plugins package — no C# code, no `Startup` configuration. Drops in via NuGet and lights up the moment the assembly is referenced.

## Demo

![Drag-and-drop reordering in the Umbraco content tree, with a move-in-progress spinner and keyboard grab & place](https://raw.githubusercontent.com/carl-schele-esatto/Esatto.Packages/main/Esatto.Umbraco.Backoffice.ContentTreeDragAndDrop/docs/images/content-tree-dnd-demo.gif)

## Install

```bash
dotnet add package Esatto.Umbraco.Backoffice.ContentTreeDragAndDrop
```

Restart the site / hard-refresh the backoffice. The drag-and-drop activates automatically on the Content section.

## Keyboard reordering

Fully keyboard-operable, as an accessible alternative to dragging (WCAG 2.2 AA):

| Key | Action |
|-----|--------|
| Space | Grab the focused node / drop it at the chosen position |
| ↑ / ↓ | Move the insertion point between rows |
| → | Nest into the highlighted node (as a child) |
| ← | Pop out to the parent level |
| Esc | Cancel the move |

Each step is announced via an `aria-live` region for screen-reader users. (Enter keeps its normal "open node" behaviour when no node is grabbed.)

## How it works (one-paragraph version)

The script patches `Element.prototype.attachShadow` at module load to observe every shadow root created in the backoffice from then on, plus walks the existing DOM once at startup. When it sees an `<umb-tree-item entitytype="document">`, it marks it draggable and lets a single hidden Lit element (mounted inside `<umb-app>` so it can consume `UMB_NOTIFICATION_CONTEXT` / `UMB_ACTION_EVENT_CONTEXT`) handle the drag events at the document level in the capture phase. Same-parent reorder mutates the DOM optimistically and calls `DocumentService.putDocumentSort`; cross-parent moves call `putDocumentByIdMove` then dispatch `UmbRequestReloadChildrenOfEntityEvent` to refresh both branches.

## Compatibility

| Umbraco | Status |
|---------|--------|
| 17.x    | Verified |
| 18.x    | Verified |

Earlier versions of Umbraco use a different tree-item DOM shape and Management API surface — not supported.

## License

MIT.
