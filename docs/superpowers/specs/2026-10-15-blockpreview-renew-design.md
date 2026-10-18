# BlockPreview renew + live frontend preview — Design

**Date:** 2026-10-15
**Package:** `Backoffice.BlockPreview` (private feed, last 1.1.1) → **`Esatto.Umbraco.Backoffice.BlockPreview` 1.0.0** (public, nuget.org)
**Repo dir:** `Esatto.Umbraco.Backoffice.BlockPreview/` + `Esatto.Umbraco.Backoffice.BlockPreview.Tests/`
**Test site:** `c:\src\umbracokalaset26` (Umbraco 18.2.1, InMemoryAuto models, a Bootstrap carousel block)

## Summary

Renew the legacy `Backoffice.BlockPreview` package and give it a new main feature: every **Block List**
block in the backoffice shows the block's **real frontend rendering**. The block's own Razor partial is
rendered on the server inside the site's own `<head>`, and shown in a sandboxed **iframe**:

- the site's CSS applies exactly (its own `:root`, `rem`, media queries at the block's width);
- the site's JavaScript runs (a Bootstrap carousel slides; arrows and dots work);
- unsaved edits show up, re-rendered as the editor types.

The existing video and code preview elements stay, unchanged apart from their App_Plugins path.

### Why not Umbraco.Community.BlockPreview

Carl tried it (6.2.0, Umbraco 18) on the test site and rejected it: *"images don't slide, it doesn't
reflect the css 100%"*. It injects the rendered HTML into the backoffice DOM (shadow root), where scripts
don't run and the CSS can't match (`rem`, `:root`, media queries follow the backoffice, not the site). It
also required compiled models on disk (its check scans assemblies for `[PublishedModel]`, so InMemoryAuto
fails). An iframe fixes the first two by construction; going through `IPublishedModelFactory` fixes the
third.

### Out of scope (1.0)

- Block Grid and Rich Text blocks (planned 1.1; the render endpoint is designed to serve them).
- Click-to-edit inside the preview (editing is opened from the label bar).
- Keeping interactive state (e.g. the current carousel slide) across re-renders.

## Renew (per the repo's "renew" workflow)

- `git mv` to `Esatto.Umbraco.Backoffice.BlockPreview`: folder, csproj, PackageId / RootNamespace /
  AssemblyName, App_Plugins folder, `umbraco-package.json` id/name, README.
- **Kept:** the custom element names `backoffice-blockpreview-video` and `backoffice-blockpreview-code`,
  and their `meta` contracts. Their served path changes to
  `/App_Plugins/Esatto.Umbraco.Backoffice.BlockPreview/{video,code}-preview.element.js`. That's the only
  breaking change for sites that registered them, and no project under `c:\src` references the legacy
  package.
- Add: `Esatto.Umbraco.Backoffice.BlockPreview.Tests` (xUnit), `icon.png`, README screenshots under
  `docs/`, the source-code link (`RepositoryUrl`, per-package `PackageProjectUrl`, `RepositoryType`), tags
  `umbraco-marketplace;umbraco-18`, and min-only Umbraco references at `17.0.0`.
- A targeted, behaviour-preserving cleanup of the two existing elements, if they need it.
- Client build: a `Client/` folder with TS + Vite + vitest, like ContentTreeDragAndDrop. The two existing
  JS elements and their `lib/` files move to `Client/public/`, so Vite copies them unchanged.
- The legacy `Backoffice.BlockPreview` gets no further releases.

## Behaviour

### What the editor sees (Block List)

```
┌──────────────────────────────────────────────┐
│ ▣ Carousel                          (✎ ⚙)    │  label bar: block icon + label;
├──────────────────────────────────────────────┤  click → opens the block editor
│                                              │
│      iframe: the block's real rendering      │  site CSS + site JS
│                                              │
└──────────────────────────────────────────────┘
```

- **Label bar.** The block's icon and label (`icon`, `label`). Clicking the bar follows
  `config.editContentPath`. A ⚙ link appears when `config.showSettingsEdit` is true and follows
  `config.editSettingsPath`. The bar shows invalid state when `contentInvalid` / `settingsInvalid` is set.
  Umbraco's own hover actions (edit / delete / copy) are untouched.
- **Iframe.** Full width. Its height follows the rendered content. It is fully interactive.
- **Loading.** A light placeholder (min-height about 80px) until the first render arrives. Later renders
  keep the current preview visible and swap when the new one is ready (no flicker).
- **Not previewable** (no partial found, or the type is in `IgnoredContentTypes`): only the label bar
  shows, so it looks like Umbraco's normal block row.
- **Render error:** the label bar plus a small warning line with the server's message. The backoffice
  never shows an exception page.

### When it re-renders

On first display, and whenever `content` or `settings` change, debounced by about 400 ms. A change only
counts if the serialized values differ from the last rendered ones. A re-render resets the preview's
interactive state.

### Which blocks

Every Block List block, through one generic `blockEditorCustomView` registered with
`forBlockEditor: "block-list"`, no `forContentTypeAlias`, and a very low `weight`. The block list renders
only the highest-weight matching view, so a site's own custom view for a type (for example the video
preview registered for `videoRow`) wins automatically.

## Architecture

### Server

**Settings** (`Esatto:BlockPreview`, all optional):

```json
"Esatto": {
  "BlockPreview": {
    "BlockList": { "Enabled": true, "IgnoredContentTypes": [] },
    "Layout": "~/Views/BlockPreview.cshtml",
    "Stylesheets": [],
    "Scripts": []
  }
}
```

- `BlockList.Enabled`: when false, the generic view's manifest is not emitted, so no script loads. The
  manifest comes from a C# `IPackageManifestReader`, inserted before Umbraco's readers and with an empty
  manifest name, exactly as in TreeDragAndDrop (see that spec's Risk 1). The static `umbraco-package.json`
  keeps the package identity.
- `Layout`: the site's preview layout. A plain Razor layout with the site's `<head>` (CSS, fonts,
  scripts) and `@RenderBody()`. If the file doesn't exist, a built-in layout renders `Stylesheets` as
  `<link>` and `Scripts` as `<script>`.
- Invalid values fall back to defaults with a logged warning. The manifest endpoint never throws.

**Render endpoint:** `POST /umbraco/management/api/v1/esatto/block-preview/render`.
`ManagementApiControllerBase` + `VersionedApiBackOfficeRoute`, policy `BackOfficeAccess`.

Request:

```json
{
  "documentKey": "…",               // owner document (may be unsaved; see Risk 1)
  "documentTypeKey": "…",
  "propertyAlias": "carouselList",
  "culture": "sv" | null,
  "segment": null,
  "blockEditor": "block-list",
  "content":  { "key": "…", "contentTypeKey": "…", "values": [ { "alias", "culture", "segment", "value", "editorAlias" } ] },
  "settings": { … } | null          // same shape
}
```

Response (always 200 for a handled outcome):

```json
{ "status": "rendered", "html": "<!doctype html>…" }
{ "status": "notPreviewable", "reason": "noPartial" | "ignored" }
{ "status": "error", "message": "…" }
```

**Pipeline** (`BlockPreviewRenderer`, one class per step behind small interfaces so each is unit-testable):

1. **Resolve the owner and the property type.** The owner is the document's draft from the published
   content cache (`preview: true`). The property type is `documentTypeKey` + `propertyAlias` from the
   published content type cache.
2. **Build the block value.** Synthesize a one-item `BlockListValue` (layout + contentData + settingsData,
   plus an `expose` entry for the requested culture/segment, so the converter doesn't filter the block
   out). Convert it with **the property type's own value converter**, the same pipeline as the frontend.
   The result is the `BlockListItem<TContent, TSettings>` the frontend partial receives. Typed models come
   from `IPublishedModelFactory`, so InMemoryAuto works.
3. **Find the partial.** `Views/Partials/blocklist/Components/{contentTypeAlias}.cshtml`, the location
   Umbraco's `blocklist/default.cshtml` uses. Not found → `notPreviewable/noPartial`. Ignored type →
   `notPreviewable/ignored`.
4. **Render.** A package Razor view (`/Views/EsattoBlockPreview/Frame.cshtml`, shipped in the RCL) uses
   the resolved layout and renders the partial with the item as its model, under the request culture
   (`VariationContext` + `CultureInfo`).
5. **Inject the frame script** before `</body>`. It's a small inline script that:
   - posts `{ type: "esatto-block-preview:height", id, height }` to the parent via a `ResizeObserver`;
   - cancels navigation for links inside the preview.

   The id is a per-render nonce that the client passes and checks.
6. Exceptions in steps 1–5 become `error` (logged with the content type alias). They're never rethrown
   to the client.

### Client (`Client/src`)

- `manifests.ts`: the generic `blockEditorCustomView` (element `esatto-block-preview-frame`, low weight).
  Emitted through the C# manifest reader so `Enabled` gates it.
- `block-preview-frame.element.ts`: an `UmbLitElement` implementing `UmbBlockEditorCustomViewElement`.
  It reads the owner context (document key, document type key, culture, property alias) from the
  workspace, variant and property contexts. It renders the label bar, the placeholder or the iframe
  (`srcdoc` = html, `sandbox="allow-scripts"`, width 100%), and the fallback or error states.
- `render-request.ts` (pure): builds the request from the element's properties and the contexts. It also
  answers "did content/settings change since the last render?" by comparing stable serializations.
- `frame-messages.ts` (pure): validates incoming messages (`event.source` is this iframe's
  `contentWindow`, `type` matches, `id` equals this render's nonce, height is a finite positive number)
  and returns the height to apply.
- API calls use `umbHttpClient` + `tryExecute`, as in DictionaryFilterValues.

`sandbox="allow-scripts"` without `allow-same-origin` gives the preview an opaque origin: it can't touch
the backoffice or its auth. Relative asset URLs resolve against the backoffice origin (`srcdoc` inherits
the parent's base URL), which is the site itself. Cross-origin CDN assets with `crossorigin` need CORS
headers, which the common CDNs (jsdelivr, cdnjs) send.

## Error handling

| Situation | Result |
|---|---|
| No partial for the block type | Label bar only (looks like Umbraco's row) |
| Type in `IgnoredContentTypes` | Label bar only |
| Conversion or rendering throws | Label bar + warning with the message; logged server-side |
| Endpoint unreachable / 401 | Label bar + warning; retried on the next change |
| Height message from another frame, or invalid | Ignored |
| `BlockPreview.cshtml` missing | Built-in layout with `Stylesheets` / `Scripts` |
| Invalid config values | Defaults + logged warning |

## Testing

- **xUnit** (`.Tests`, public surface where possible):
  - the request → `BlockItemData` mapping;
  - the synthesized block value includes `expose` for the culture;
  - layout resolution (site file vs built-in);
  - ignored types → `notPreviewable/ignored`;
  - missing partial → `notPreviewable/noPartial`;
  - exceptions → `error`, not thrown;
  - the frame script contains the nonce and is injected once before `</body>`;
  - options defaults and binding; invalid config → defaults;
  - the manifest reader emits the view only when `Enabled`, is inserted before Umbraco's readers, and has
    an empty name.
- **vitest:** `render-request` (shape, culture, change detection) and `frame-messages` (source, type,
  nonce, height validation).
- **Live** (umbracokalaset26, Umbraco 18). Add `Views/BlockPreview.cshtml` with Bootstrap 5.3.8,
  `carousel.css` and the Bootstrap bundle. Then check:
  - the carousel block shows the real carousel, and it slides; the arrows work;
  - it looks the same as on the website (side-by-side screenshot);
  - the label bar opens the block editor;
  - adding or removing an image updates the preview before saving;
  - a block type with no partial shows the plain row;
  - an unsaved new page (Risk 1);
  - the video and code elements still work when registered.

## Risks

1. **Owner for documents that have never been saved.** `ConvertToElement` and the property value
   converter need an owner `IPublishedElement`. A brand-new document has no draft in the cache. The first
   plan task is a spike to settle this: either a stub owner of the right document type, or a clear
   "Save the page once to see previews" state in that one case.
2. **Value converter vs single-item input.** Building a one-item `BlockListValue` must survive the
   converter's validation (layout/contentData consistency, `expose`). Covered by an xUnit test against the
   real converter where feasible, otherwise by the live check.
3. **Partials that need page context** (`Umbraco.AssignedContentItem`, `ViewData` from a page
   controller). These render as errors, not crashes. The README says partials should depend only on their
   model.
4. **Many blocks.** One request and one iframe per block. Fine for typical pages. If needed, 1.1 can
   batch requests or lazy-render off-screen blocks (IntersectionObserver).
