# Esatto.Umbraco.Backoffice.TreeDragAndDrop

Drag-and-drop **and keyboard moving** for the Umbraco 17/18 backoffice **Content**, **Media** and **Dictionary** trees. One package, and each tree can be switched off.

- Move items by dragging them in the section sidebar tree
- Real ordering for content and media: drop above, inside or below an item
- Instant visual feedback when reordering, rolled back if saving fails
- A spinner on the item while it's being saved
- Hover over a collapsed item mid-drag to expand it
- Cycle guard: an item can't be dropped into itself or its own descendants
- Keyboard-accessible "grab & place" (WCAG 2.2 AA) with screen-reader announcements
- Pickers (such as "Move to…") and the recycle bin are left alone

Replaces [ContentTreeDragAndDrop](https://www.nuget.org/packages/Esatto.Umbraco.Backoffice.ContentTreeDragAndDrop) and [MediaTreeDragAndDrop](https://www.nuget.org/packages/Esatto.Umbraco.Backoffice.MediaTreeDragAndDrop). See [Replacing the old packages](#replacing-the-old-packages).

## Demo

![Moving dictionary items by drag-and-drop in the Umbraco Translation section](https://raw.githubusercontent.com/carl-schele-esatto/Esatto.Packages/main/Esatto.Umbraco.Backoffice.TreeDragAndDrop/docs/images/dictionary-tree-dnd-demo.gif)

## Install

```bash
dotnet add package Esatto.Umbraco.Backoffice.TreeDragAndDrop
```

Restart the site and hard-refresh the backoffice.

## How dropping works

**Content and Media**: each row has three drop zones.

| Drop on | Result |
|---------|--------|
| Top third of a row | Placed **above** that item (same parent) |
| Middle of a row | Moved **inside** that item |
| Bottom third of a row | Placed **below** that item (same parent) |

**Dictionary**: Umbraco has no sort order for dictionary items and always lists them alphabetically, so a drop only chooses the parent.

| Drop on | Result |
|---------|--------|
| Middle of a row | Moved **inside** that item |
| Top or bottom edge of a row | Moved **next to** that item (same parent). On a top-level item, that means the root |

## Settings

Everything is on by default. To switch a tree off:

```json
{
  "Esatto": {
    "TreeDragAndDrop": {
      "Content": true,
      "Media": true,
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
| Enter | Drop (while an item is grabbed) |
| ↑ / ↓ | Move between rows |
| → | Target the highlighted item itself ("inside this item") |
| ← | Jump to the parent row |
| Esc | Cancel |

Each step is announced through an `aria-live` region. Clicking or typing outside the tree also cancels the move.

## Replacing the old packages

Remove `Esatto.Umbraco.Backoffice.ContentTreeDragAndDrop` and `Esatto.Umbraco.Backoffice.MediaTreeDragAndDrop` when you install this package. While one of them is still installed, this package leaves that tree to it and logs a warning, so a tree never gets two sets of drag handlers.

## Compatibility

| Umbraco | Status |
|---------|--------|
| 17.x    | Supported |
| 18.x    | Supported |

## License

MIT.
