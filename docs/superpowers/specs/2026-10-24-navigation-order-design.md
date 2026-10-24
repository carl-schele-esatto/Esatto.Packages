# NavigationOrder — Design

**Date:** 2026-10-24
**Package:** `Esatto.Umbraco.Backoffice.NavigationOrder` (new, public, nuget.org)
**Repo dir:** `Esatto.Umbraco.Backoffice.NavigationOrder/` + `Esatto.Umbraco.Backoffice.NavigationOrder.Tests/`
**Test site:** `c:\src\umbracokalaset26` (Umbraco 18.2.1). First real consumer: Esatto.Maintenance (18.2.1).

## Summary

Admins reorder the backoffice by dragging tabs where they already are: the **sections in the top bar**
(Content, Media … Forms) and the **tabs inside a section** (e.g. Settings → Welcome, Examine Management …
Scheduled tasks). The order is saved in the database the moment a tab is dropped and applies to every
backoffice user. Other users see the order but cannot drag.

### Decisions

| Decision | By |
|---|---|
| Its own package; drag-and-drop directly in the bars, no separate settings screen | Carl, 2026-10-24 |
| One order for everyone; only admins can change it | Claude's call |
| Saved on drop; no Save button, no confirmation | Claude's call |
| Keyboard: focused tab + **Alt+Shift+←/→** moves it one place | Claude's call |
| Sections/tabs that aren't in the saved order (new packages) go **last**, in Umbraco's default order | Claude's call |
| Umbraco 18 only (references 18.0.0, like AICosts and ScheduledTasks) | Claude's call |

### Out of scope (v1)

- A per-user order.
- Hiding sections or tabs (that's user-group permissions).
- A reset/undo UI — a wrong move is fixed by dragging back.
- Other bars: workspace views, sidebar trees, header apps.
- Moving a tab to another section, or a dashboard in among section views (Umbraco renders those as two
  separate tab groups, dashboards first).

## Background (verified in `@umbraco-cms/backoffice` 18.2.1)

- **Order = manifest `weight`, highest first.** `UmbBaseExtensionsInitializer` sorts permitted extensions
  with `b.weight - a.weight`. There is no stored order anywhere; it's recomputed from manifests on every load.
- **Default weights:** Content 1000, Media 900, Library 850, Settings 800, Packages 700, Users 600,
  Members 500, Translation 400, Copilot (`Uai.Section.CopilotWorkspace`) 50, Esatto Automate 10,
  AI (`ai`) unset, Forms (`Umb.Section.Forms`) 0.
- **Top bar:** `umb-backoffice-header-sections` renders
  `<uui-tab-group id="tabs"><uui-tab data-mark="section-link:<alias>" href="section/<pathname>">…`.
- **Section tabs:** `umb-section-main-views` renders dashboards in `<uui-tab-group id="dashboards">`, then
  section views in `<uui-tab-group id="views">`. Each tab is `<uui-tab href="<routerPath>/dashboard/<path>">`
  (or `/view/<path>`), where `path = meta.pathname || pathFolderName(meta.label ?? name ?? alias)` and
  `routerPath` ends in `section/<sectionPathname>`.
- **A tab's section** comes from its `Umb.Condition.SectionAlias` conditions (`match` and/or `oneOf`). A
  dashboard without one appears in every section.
- **Changing a weight needs a re-registration.** Replacing a manifest object in the registry with a new
  weight does *not* re-sort: initializers keep their per-alias controller, and `_extensionChanged` only
  notifies when permission changes. `unregister(alias)` followed by `register({...raw, weight})` does
  re-sort, and the initializer batches both changes in one `requestAnimationFrame`, so the bar re-renders
  once. `register` appends pending `appendConditions` only once (the entry is deleted after use), so
  re-registering the raw manifest is safe.
- **Raw manifests** (without kind merging) come from `umbExtensionsRegistry.extensions` (observable).
- **Admin:** `UMB_CURRENT_USER_CONTEXT.isAdmin` (observable) client-side; `AuthorizationPolicies.RequireAdminAccess` server-side.
- **The Management API may camel-case dictionary keys** (seen in AICosts), so the API never uses an
  alias as a JSON object key.

## Behaviour

### Applying the order (every user)

A `backofficeEntryPoint` applies the order to every manifest of type `section`, `dashboard` and
`sectionView`, including ones registered later (bundles, late packages):

1. Apply the order cached in `localStorage` (instant, no flash on reload).
2. Fetch the saved order from the API, apply it, cache it. A failed fetch keeps the cached order and logs
   a console warning; nothing is shown to the user.

The order is a set of **lists**: one for the sections, and one per section for its tabs.

- **Effective order of a list:** the saved aliases that exist, in saved order, then every other member in
  default order (original weight, highest first; registration order on ties). Saved aliases that don't
  exist (uninstalled package) are skipped and kept, so a reinstalled package returns to its place.
- **Weights:** every member of a saved list gets `(n - index) * 10`, where `n` is the number of members.
  Members of lists with nothing saved keep — or get back — their **original** weight (captured the first
  time the manifest is seen, before any change).
- **Tab lists:** one saved list per section holds both dashboard and section-view aliases; dashboards
  are ordered among dashboards and views among views. A tab in several sections takes its weight from
  the first saved list that contains it, taking sections in effective top-bar order.
- A manifest is only re-registered when its weight differs from the target, so applying converges.

### Dragging (admins only)

- Drag a tab in the top bar or a section's tab bar, and drop it on another tab **in the same group**.
  The pointer's position against the target's horizontal midpoint decides before/after. A vertical
  insertion line shows where it will land, and the dragged tab is dimmed.
- Dropping anywhere else, or pressing Esc, does nothing. Clicking a tab still navigates.
- For non-admins nothing changes (the browser's normal link drag).
- **On drop:** the new list is the list's *full* effective order — including members the admin can't
  see — with the dragged alias moved before/after the target. It's applied at once, then the whole order
  is PUT to the server. A spinner sits over the moved tab while saving. Saves run one at a time; a move
  made while saving is sent next (only the latest). If a save fails, the last saved order is re-applied
  and a danger notification says: "Couldn't save the new order, so it has been put back."
- **Keyboard:** with a tab focused, **Alt+Shift+←/→** moves it one place within its group and saves the
  same way. An `aria-live` region announces "Forms moved to position 3 of 12".

### API

`/umbraco/management/api/v1/esatto/navigation-order`

| Method | Policy | Body / response |
|---|---|---|
| `GET` | `BackOfficeAccess` (any backoffice user) | `{ sections: string[] \| null, tabs: [{ section, aliases }], updatedUtc, updatedBy }` |
| `PUT` | `RequireAdminAccess` | body `{ sections: string[] \| null, tabs: [{ section, aliases }] \| null }`; 200 with the saved order, 400 `ProblemDetails` listing every error |

`sections: null` and a section missing from `tabs` mean "default order". Validation: every alias and
section non-blank and ≤ 255 characters; ≤ 200 aliases per list; ≤ 100 tab lists; no duplicate alias
within a list; no duplicate section in `tabs`.

### Storage

Table `NavigationOrder`, one row per saved list: `id` (identity), `listKey` (≤ 255, unique:
`sections` or `tabs:<sectionAlias>`), `aliasesJson` (NVARCHAR(MAX), a JSON string array), `updatedUtc`,
`updatedBy`. A PUT replaces all rows in one scope. Created by migration plan
`Esatto.Umbraco.Backoffice.NavigationOrder`, run at `UmbracoApplicationStartingNotification` like Redirects.

### Texts (English, Swedish)

| Key (`esattoNavigationOrder_…`) | English | Swedish |
|---|---|---|
| `moved` (name, position, total) | `{0} moved to position {1} of {2}` | `{0} flyttad till plats {1} av {2}` |
| `saving` | `Saving the order…` | `Sparar ordningen…` |
| `saveFailed` | `Couldn't save the new order, so it has been put back.` | `Den nya ordningen kunde inte sparas, så den har återställts.` |

## Risks

- The drag code relies on Umbraco's internal markup (element names, `data-mark`, tab `href`s). If an
  upgrade changes it, dragging stops, but the saved order is still applied and the bars keep working.
  Tests pin the parsing; the README names the Umbraco versions it was verified on.
- When the top bar overflows, Umbraco moves tabs into a "more" menu; dragging there is best effort.

## Release

1.0.0, born public. Carl approves commits, the tag `Esatto.Umbraco.Backoffice.NavigationOrder-1.0.0`
and runs `dotnet nuget push`. README with screenshots, the shared Esatto icon, and the source-code link.
