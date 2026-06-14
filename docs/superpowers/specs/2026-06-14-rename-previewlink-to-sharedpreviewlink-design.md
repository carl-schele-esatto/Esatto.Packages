# Rename `Backoffice.PreviewLink` → `Esatto.Umbraco.Backoffice.SharedPreviewLink`

**Date:** 2026-06-14
**Status:** Approved (design); pending implementation plan
**Author:** Carl Schéle (with Claude)

## Summary

Rename the Umbraco 17 NuGet package `Backoffice.PreviewLink` to
`Esatto.Umbraco.Backoffice.SharedPreviewLink`, mirroring the established
rename pattern already applied to `Esatto.Umbraco.Backoffice.ContentTreeDragAndDrop`.
Alongside the rename, add a quality pass: an xUnit test project (matching the
sibling test projects), a package icon, README screenshots, and a small set of
low-risk cleanups. Rewire the consuming app at `c:\src\AI.Woowoo` so it
references and resolves the renamed package.

The package stays **private** on the Azure DevOps `esatto-packages` feed
(it does not join the public `Esatto.Umbraco.Backoffice.*` family on nuget.org).

## Goals

- Package identity renamed everywhere it matters (assembly, namespace, NuGet id,
  Umbraco `App_Plugins` discovery folder + manifest).
- Consumer (`AI.Woowoo`) updated and ready to restore the renamed package from
  the private feed.
- First-class quality: xUnit tests over the testable C# surface, package icon,
  README screenshots.
- No behaviour changes to the feature itself.

## Non-goals

- No deep refactor (e.g. extracting a shared URL-builder service, reworking the
  badge-suppression buffering). Explicitly out of scope per the chosen "targeted
  cleanup" level.
- No JS/Vitest test suite (the package has no client build toolchain; adding one
  is disproportionate here). Deferred.
- No publishing to any feed and no git commit/push without explicit approval.
- The public `Esatto.Umbraco.Backoffice.*` siblings keep resolving from
  nuget.org; this package does **not** go public.

## Guiding principle

Rename only what package identity and Umbraco discovery require. Leave the
internal API surface alone to minimize churn and risk.

**Renamed (required):**
- Folder `Backoffice.PreviewLink/` → `Esatto.Umbraco.Backoffice.SharedPreviewLink/`
- `Backoffice.PreviewLink.csproj` → `Esatto.Umbraco.Backoffice.SharedPreviewLink.csproj`
- csproj `PackageId`, `RootNamespace`, `AssemblyName`
- C# `namespace Backoffice.PreviewLink;` → `namespace Esatto.Umbraco.Backoffice.SharedPreviewLink;`
  in all 6 `.cs` files, plus the `@using Backoffice.PreviewLink` in the `.cshtml`
- `wwwroot/App_Plugins/Backoffice.PreviewLink/` → `wwwroot/App_Plugins/Esatto.Umbraco.Backoffice.SharedPreviewLink/`
- `umbraco-package.json`: `id`, `name`, extension `alias`, `api` + `element` paths
- README references to the package name / install command
- `PreviewLinkController.ProtectorPurpose` constant
  `"Backoffice.PreviewLink"` → `"Esatto.Umbraco.Backoffice.SharedPreviewLink"`
  (invalidates any already-minted tokens — acceptable: tokens are 7-day
  ephemeral, no production state)

**Kept as-is (deliberate):**
- C# type names (`PreviewLinkController`, `PreviewLinkMiddleware`,
  `PreviewBadgeSuppressionMiddleware`, `PreviewLinkErrorController`,
  `PreviewLinkRequest`, `PreviewLinkResponse`, `PreviewLinkMiddlewareExtension`)
- The public extension method `app.UsePreviewLink()` (renaming churns the
  documented API for no benefit)
- JS-internal identifiers: custom element `backoffice-preview-link-button`,
  console log tags `[backoffice-preview-link]`, the badge-suppression
  `data-source="backoffice-preview-link"` marker (internal; work regardless of
  package name)
- Error route `/preview-link-error` and `MarkerCookieName` value

**Defaults chosen (vetoable):**
- `umbraco-package.json` `id` uses **dot-notation**
  `Esatto.Umbraco.Backoffice.SharedPreviewLink` (mirrors ContentTreeDragAndDrop).
- Package version stays **`1.0.0`** (matches the renamed siblings).

## Affected files

### Package repo (`c:\src\Esatto.Packages`)

Rename + edit within the (renamed) package folder:

| Current | Action |
|---|---|
| `Backoffice.PreviewLink/` (folder) | rename to `Esatto.Umbraco.Backoffice.SharedPreviewLink/` |
| `Backoffice.PreviewLink.csproj` | rename + edit `PackageId`/`RootNamespace`/`AssemblyName`; add `PackageIcon` + icon `<None>`; add screenshot `<None>` pack items |
| `src/PreviewLinkController.cs` | namespace; `ProtectorPurpose` value; add `[ProducesResponseType(500)]` |
| `src/PreviewLinkMiddleware.cs` | namespace |
| `src/PreviewBadgeSuppressionMiddleware.cs` | namespace |
| `src/PreviewLinkErrorController.cs` | namespace |
| `src/PreviewLinkRequest.cs` | namespace |
| `src/PreviewLinkResponse.cs` | namespace |
| `Views/PreviewLinkError/Index.cshtml` | `@using` namespace |
| `wwwroot/App_Plugins/Backoffice.PreviewLink/` (folder) | rename to `.../Esatto.Umbraco.Backoffice.SharedPreviewLink/` |
| `.../umbraco-package.json` | `id`, `name`, `alias`, `api`+`element` paths |
| `.../share-preview-action.js` | `API_BASE` unchanged (server route unchanged); no rename needed |
| `.../share-preview-button.js` | `_formatExpiry` cleanup (derive from `expiresAt`) |
| `README.md` | package name, install cmd, "How it works" screenshots section |
| `icon.png` (new) | copy of `esatto-logo-square.png` |
| `docs/share-preview-button.png` (new) | from `button.png` |
| `docs/save-modal.png` (new) | from `save.png` |
| `docs/preview-link-modal.png` (new) | from `previe-link.png` |

New test project:

| New | Notes |
|---|---|
| `Esatto.Umbraco.Backoffice.SharedPreviewLink.Tests/` | xUnit, net10.0, `IsPackable=false`, `ProjectReference` to the package |

### Consumer repo (`c:\src\AI.Woowoo`)

| File | Change |
|---|---|
| `src/AI.Woowoo/Directory.Packages.props` | `PackageVersion` id rename (keep `1.0.0`) |
| `src/AI.Woowoo/AI.Woowoo.csproj` | `PackageReference` id rename |
| `src/AI.Woowoo.TestSite/Program.cs` | `using` rename; `app.UsePreviewLink()` unchanged |
| `nuget.config` | add exact source mapping for the new id under `esatto-packages`; keep `Backoffice.*` (still serves MediaTreeDnd + Redirects); update comment |

`nuget.config` addition (exact pattern beats `*`, so it resolves privately;
public siblings keep matching only `*` → nuget.org):

```xml
<packageSource key="esatto-packages">
  <package pattern="Backoffice.*" />
  <package pattern="Esatto.Umbraco.Backoffice.SharedPreviewLink" />
</packageSource>
```

## Targeted cleanup (no behaviour change)

The DRY surface is modest. Concrete, low-risk items only:

- **JS `_formatExpiry`** hardcodes `"in 7 days"`. Derive the relative remaining
  days from the returned `expiresAt` rather than assuming a fixed 7.
- **`PreviewLinkController`** returns `StatusCode(500, …)` for missing Umbraco
  context but does not declare it. Add `[ProducesResponseType(StatusCodes.Status500InternalServerError)]`
  for API-doc accuracy.

Explicitly **not** done:
- `PreviewLinkController.BuildDraftUrl` and `PreviewLinkMiddleware.BuildDraftRelativeUrl`
  look similar but are intentionally different strategies for different execution
  contexts (ambient UmbracoContext vs. ad-hoc middleware context where
  `IPublishedContent.Parent` traversal is unreliable — documented in the code).
  They will **not** be merged.
- The badge-suppression full-response buffering stays as-is (already narrowly
  gated by the marker cookie).

## Tests — `Esatto.Umbraco.Backoffice.SharedPreviewLink.Tests` (xUnit)

Mirrors the sibling test projects: `net10.0`, `Nullable`/`ImplicitUsings` enabled,
`IsPackable=false`, `IsPublishable=false`, `FrameworkReference Microsoft.AspNetCore.App`,
`Microsoft.NET.Test.Sdk` 17.11.1, `xunit` 2.9.2, `xunit.runner.visualstudio` 2.8.2,
`NSubstitute` 5.3.0, and a `ProjectReference` to the package. Tests hit the
**public surface** (no `InternalsVisibleTo`).

Test groups:

1. **Token contract** (`EphemeralDataProtectionProvider`, no Umbraco):
   - protect → unprotect round-trips for a `Guid.ToString("N")` with a
     time-limited protector keyed on the shared purpose;
   - a tampered/garbage token surfaces as `CryptographicException`;
   - a well-formed-but-non-GUID payload surfaces as `FormatException`.
   - Locks the controller↔middleware contract the feature rests on.

2. **`PreviewBadgeSuppressionMiddleware`** (`DefaultHttpContext` + fake `next`):
   - marker cookie present + HTML response → `<style …>` injected immediately
     before `</body>`;
   - HTML response with no `</body>` → style appended at end;
   - non-HTML response → body passes through untouched;
   - no marker cookie → straight passthrough, no buffering.

3. **`PreviewLinkMiddleware`** (`DefaultHttpContext` + `NSubstitute` fakes for the
   Umbraco services, which are only reached after token validation):
   - no `?preview-token` → `next` called, no side effects;
   - tampered/garbage token (`CryptographicException`) → **410** + `expired`
     variant in `Items[ErrorItemsKey]` + path rewritten to `/preview-link-error`;
   - token that unprotects to a non-GUID payload (`FormatException`) → **400** +
     `invalid` variant.

4. **Stretch (only if mock setup stays clean):** the security-critical
   path-binding check — a token minted for page A is rejected (404) when used on
   page B's path. Requires faking `IUmbracoContextFactory.EnsureUmbracoContext`,
   the content cache `GetById(preview:true, …)`, and
   `IDocumentNavigationQueryService.TryGetAncestorsKeys`. Attempt it; drop it
   rather than ship brittle tests if the mock surface is disproportionate.

## Icon + README screenshots

**Icon** (mirror sibling pattern):
- Copy `C:\Users\carl_\Downloads\esatto-logo-square.png` → `icon.png` at package root.
- csproj: `<PackageIcon>icon.png</PackageIcon>` + `<None Include="icon.png" Pack="true" PackagePath="\" />`.
- Caveat: the logo renders wide, not square; NuGet displays icons square
  (≤128×128) so it may letterbox/crop. Used as-is unless a square crop is provided.

**Screenshots** — stored in `docs/`, referenced with relative paths in README,
packed under `PackagePath="docs/"` so links resolve in-repo and inside the nupkg:
- `button.png` → `docs/share-preview-button.png`
- `save.png` → `docs/save-modal.png`
- `previe-link.png` → `docs/preview-link-modal.png` (typo fixed)

A short **"How it works"** section near the top of the README walks the flow:
click **Share preview** (button) → save/variant modal → **Preview link** modal
with the tokenized URL.

Caveat: the nuget.org gallery only renders README images from absolute https
URLs, so relative-path screenshots won't render there — acceptable because the
package is private/repo-first.

## Verification

- `dotnet build -c Release` on the renamed package — compiles clean.
- `dotnet test` on the new test project — all tests pass (report real output).
- `dotnet pack -c Release -p:AutoPushToFeed=false` — produces the nupkg locally
  **without** pushing.
- Consumer edits leave `AI.Woowoo` ready, but its restore of the renamed package
  only succeeds **after** the nupkg is pushed to the `esatto-packages` feed.

## Boundaries / approvals required

- **No feed push.** Pushing to the shared `esatto-packages` Azure DevOps feed is
  an outward action requiring explicit approval. `AutoPushToFeed=false` is used
  for local packing.
- **No git commit/push** in either repo without explicit approval (standing rule).
  Note: this deviates from the brainstorming skill's "commit the design doc" step,
  per the user's standing no-commit rule — the spec is written but left uncommitted.

## Open veto points (defaults stand unless changed)

- Keep `app.UsePreviewLink()` method name + JS-internal element name.
- Dot-notation `umbraco-package.json` `id`.
- Defer JS/Vitest tests.
- Use the wide logo as the icon as-is.
