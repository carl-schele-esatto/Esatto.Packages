# Backoffice.MediaTreeDnd

Native HTML5 drag-and-drop for the Umbraco 17 backoffice **Media** tree.

Media-tree counterpart to [Backoffice.ContentTreeDnd](../Backoffice.ContentTreeDnd) (which handles the Content tree). Same architecture, different entity type and Management API surface.

- Three-zone drop targets per row: above sibling / into as child / below sibling
- Optimistic same-parent reorder — instant visual feedback, rolled back on API error
- Full cross-parent move (reparent) support
- Hover-to-expand collapsed branches mid-drag
- Cycle guard against dropping a node into its own descendants

Pure App_Plugins package — no C# code, no `Startup` configuration. Drops in via NuGet and lights up the moment the assembly is referenced.

## Install

```bash
dotnet add package Backoffice.MediaTreeDnd
```

Restart the site / hard-refresh the backoffice. The drag-and-drop activates automatically on the Media section.

## Pairing with Backoffice.ContentTreeDnd

The two packages are independent — installing one does not require the other. Both can be installed side-by-side; their `attachShadow` patches chain cleanly because each wrapper calls through to the original.

## Compatibility

| Umbraco | Status |
|---------|--------|
| 17.x    | Verified |

Earlier versions of Umbraco use a different tree-item DOM shape and Management API surface — not supported.

## License

MIT.
