# Backoffice.BlockPreview

Frontend-styled previews for Umbraco block editors, rendered directly in the backoffice.

Instead of the default block "chip", your editors see a meaningful, styled preview of the block. The previews are **generic and configurable** — you register them against your own content types and tell them which property aliases to read, so the package makes no assumptions about your content model or your site's CSS.

Requires Umbraco 17+.

## Included previews

| Element (`elementName`) | Served path | Purpose |
|---|---|---|
| `backoffice-blockpreview-video` | `/App_Plugins/Backoffice.BlockPreview/video-preview.element.js` | YouTube video preview (thumbnail + play overlay) |
| `backoffice-blockpreview-code` | `/App_Plugins/Backoffice.BlockPreview/code-preview.element.js` | Syntax-highlighted code preview with copy button (bundled highlight.js) |

## Install

```
dotnet add package Backoffice.BlockPreview
```

The package ships its elements as static web assets; it does **not** register any block views itself (it can't know your content types). You register them — see below.

## Usage

Register a `blockEditorCustomView` extension pointing at the element you want, set `forContentTypeAlias` to your block's content type, and pass your property aliases via `meta`.

### From a TypeScript bundle

```ts
const videoPreview: ManifestBlockEditorCustomView = {
  type: "blockEditorCustomView",
  alias: "MySite.BlockView.Video",
  name: "Video Preview",
  element: "/App_Plugins/Backoffice.BlockPreview/video-preview.element.js",
  elementName: "backoffice-blockpreview-video",
  forContentTypeAlias: "videoRow",      // your content type
  forBlockEditor: "block-list",         // or "block-grid"
  meta: { urlAlias: "videoUrl", captionAlias: "caption" }, // your property aliases
};
```

### From a plain `umbraco-package.json`

```json
{
  "type": "blockEditorCustomView",
  "alias": "MySite.BlockView.Video",
  "name": "Video Preview",
  "element": "/App_Plugins/Backoffice.BlockPreview/video-preview.element.js",
  "elementName": "backoffice-blockpreview-video",
  "forContentTypeAlias": "videoRow",
  "forBlockEditor": "block-list",
  "meta": { "urlAlias": "videoUrl", "captionAlias": "caption" }
}
```

### Video preview `meta`

| Key | Default | Description |
|---|---|---|
| `urlAlias` | `videoUrl` | Property alias holding the YouTube URL (accepts `watch?v=`, `youtu.be/`, `/embed/`, `/shorts/`, or a bare id) |
| `captionAlias` | `caption` | Property alias holding an optional caption |

### Code preview `meta`

| Key | Default | Description |
|---|---|---|
| `codeAlias` | `code` | Property alias holding the code/source text |
| `captionAlias` | `caption` | Property alias holding an optional caption/title |
| `themeUrl` | bundled VS2015 dark | URL of a highlight.js theme stylesheet to use instead of the bundled default |

The code preview bundles [highlight.js](https://highlightjs.org) (v11.8.0, BSD-3-Clause) and a VS2015 dark theme, so it highlights without depending on the consuming site. Language is auto-detected. A copy-to-clipboard button appears on hover.

## Notes

- The previews render in shadow DOM with **self-contained styling**, so they look consistent regardless of your site's CSS. They approximate a typical frontend look rather than mirroring your exact stylesheet.
- An unmatched `forContentTypeAlias` simply renders nothing — safe to ship broadly.

## Developing this package

When you're actively changing this package's elements, use a **`ProjectReference`** from the consuming project rather than the published `PackageReference`:

```xml
<ProjectReference Include="..\path\to\Backoffice.BlockPreview\Backoffice.BlockPreview.csproj" />
```

Why: a `ProjectReference` compiles this project from its current source on every build and wires the fresh output (DLL + static web assets) straight into the consumer — edit, rebuild, done.

A `PackageReference`, by contrast, consumes a **frozen, published `.nupkg`** from the NuGet cache; it has no link to your working copy, so edits here won't show up downstream. And because NuGet versions are immutable (re-pushing the same version is skipped), every change would otherwise require: `dotnet pack` → push → bump the version → restore.

So: **`ProjectReference` while developing the package, `PackageReference` once it's stable and you're just consuming it.** Remember to bump `VersionPrefix` in the csproj for each published change.
