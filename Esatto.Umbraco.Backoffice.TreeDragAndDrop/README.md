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
| 18.x    | Supported |

## License

MIT.
