# Esatto.Umbraco.Backoffice.NavigationOrder

Put the Umbraco 18 backoffice in the order you want, by dragging it.

Umbraco decides the order of the sections in the top bar (Content, Media, Settings … Forms) and of the tabs inside each section (Settings → Welcome, Examine Management … Scheduled tasks), and packages add theirs wherever their authors chose. With this package an admin simply drags a section or a tab to where it belongs. The new order is saved straight away and every backoffice user sees it.

## What you can do

- **Reorder the top bar:** drag a section and drop it on another one. A line shows where it will land.
- **Reorder a section's tabs:** drag a dashboard tab, e.g. move *Scheduled tasks* to the front of Settings.
- **Use the keyboard:** focus a tab and press **Alt+Shift+←** or **Alt+Shift+→** to move it one place. Screen readers announce the new position ("Forms moved to position 3 of 12").
- **Saved on drop:** a small spinner shows on the moved tab while it saves. If saving fails, the previous order comes back and a notification tells you.

Sections and tabs that arrive later (a package you install next month) are placed last until you move them. Uninstall a package and install it again, and its section returns to the place you gave it.

## Who can change the order

Only **admins** (members of a user group with administrator rights) can drag. Everyone else sees the order the admins set, and dragging a tab does nothing special for them. The API that saves the order requires admin access too.

## Install

```bash
dotnet add package Esatto.Umbraco.Backoffice.NavigationOrder
```

Restart the site and hard-refresh the backoffice. The package creates its `NavigationOrder` table on startup; there's nothing to configure.

## How it works

Umbraco sorts sections and tabs by their manifest `weight`, highest first, every time the backoffice loads. This package keeps the order you set in the database and, when the backoffice loads, gives each section and tab the weight that puts it in your order. Nothing in Umbraco's own code or files is changed: uninstall the package and Umbraco's default order is back.

The order is also remembered in each browser, so a reload shows it at once instead of Umbraco's default order first.

### Going back to Umbraco's order

There's no reset button: drag things back, or clear the saved order with the API (as an admin, from the browser's developer tools while logged in to the backoffice, or any tool with a backoffice token):

```http
PUT /umbraco/management/api/v1/esatto/navigation-order
Content-Type: application/json

{ "sections": null, "tabs": [] }
```

`GET` on the same URL returns the saved order, with who saved it and when.

## Texts

English and Swedish.

## Compatibility

| Umbraco | Status |
|---------|--------|
| 18.x    | Supported (verified on 18.2.1) |

## Limitations

- A tab moves within its own row only. Umbraco shows a section's dashboards and its section views as two separate rows (dashboards first), so a dashboard can't be placed among the views.
- Only the top bar and the section tabs. Workspace tabs (such as Content / Info on a page), sidebar trees and header apps keep Umbraco's order.
- One order for everyone; there's no personal order per user.
- Dragging relies on how the backoffice builds its tab bars. Should a future Umbraco version change that, dragging stops working, but the saved order is still applied and the backoffice keeps working normally.
- When the top bar is too narrow and Umbraco moves sections into its "more" menu, dragging inside that menu may not work. Widen the window, or use the keyboard.

## Source

[github.com/carl-schele-esatto/Esatto.Packages](https://github.com/carl-schele-esatto/Esatto.Packages/tree/main/Esatto.Umbraco.Backoffice.NavigationOrder)
