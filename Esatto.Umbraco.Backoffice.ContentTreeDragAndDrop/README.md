# Esatto.Umbraco.Backoffice.ContentTreeDragAndDrop

Native HTML5 drag-and-drop for the Umbraco 17 backoffice **Content** tree.

- Three-zone drop targets per row: above sibling / into as child / below sibling
- Optimistic same-parent reorder — instant visual feedback, rolled back on API error
- Full cross-parent move (reparent) support
- Hover-to-expand collapsed branches mid-drag
- Cycle guard against dropping a node into its own descendants

Pure App_Plugins package — no C# code, no `Startup` configuration. Drops in via NuGet and lights up the moment the assembly is referenced.

## Install

```bash
dotnet add package Esatto.Umbraco.Backoffice.ContentTreeDragAndDrop
```

Restart the site / hard-refresh the backoffice. The drag-and-drop activates automatically on the Content section.

## How it works (one-paragraph version)

The script patches `Element.prototype.attachShadow` at module load to observe every shadow root created in the backoffice from then on, plus walks the existing DOM once at startup. When it sees an `<umb-tree-item entitytype="document">`, it marks it draggable and lets a single hidden Lit element (mounted inside `<umb-app>` so it can consume `UMB_NOTIFICATION_CONTEXT` / `UMB_ACTION_EVENT_CONTEXT`) handle the drag events at the document level in the capture phase. Same-parent reorder mutates the DOM optimistically and calls `DocumentService.putDocumentSort`; cross-parent moves call `putDocumentByIdMove` then dispatch `UmbRequestReloadChildrenOfEntityEvent` to refresh both branches.

## Compatibility

| Umbraco | Status |
|---------|--------|
| 17.x    | Verified |

Earlier versions of Umbraco use a different tree-item DOM shape and Management API surface — not supported.

## License

MIT.
