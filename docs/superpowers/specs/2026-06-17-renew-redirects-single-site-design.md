# Renew `Backoffice.Redirects` → `Esatto.Umbraco.Backoffice.Redirects` (single-site)

**Date:** 2026-06-17
**Status:** Approved (design); pending implementation plan
**Author:** Carl Schéle (with Claude)

## Summary

Renew the Umbraco 17 NuGet package `Backoffice.Redirects` to
`Esatto.Umbraco.Backoffice.Redirects`, joining the public
`Esatto.Umbraco.Backoffice.*` family on nuget.org and mirroring the established
renew pattern (PreviewLink→SharedPreviewLink, ContentTreeDnd→ContentTreeDragAndDrop).

Alongside the rename, **remove the multi-site feature entirely** — the package
becomes single-site only. The `IRedirectSiteContext` plugin, per-site scoping,
the dashboard site-switcher, and the `siteKey` column all go away.

Full public-release treatment: xUnit test project, package icon, README
screenshots, the source-code link, a targeted behaviour-preserving cleanup, and
a fresh idempotent migration plan that converges the schema to single-site.

The package goes **public** on nuget.org (born-public for this renamed identity),
version `1.0.0`, tag `Esatto.Umbraco.Backoffice.Redirects-1.0.0`.

## Goals

- Package identity renamed everywhere it matters (folder, csproj, assembly,
  namespace, NuGet id, Umbraco `App_Plugins` discovery folder + manifest).
- Multi-site removed: a single, flat list of redirects with no site concept.
- First-class quality: xUnit tests over the testable C# surface, package icon,
  README screenshots, source-code link.
- A migration path that lands a clean single-site schema from any prior state
  (fresh, legacy `esattoRedirects`, or an old `Backoffice.Redirects` install).
- No behaviour changes to the *single-site* feature itself beyond the cleanup.

## Non-goals

- No multi-site support, in any form. (Removing it is the point.)
- No deep rewrite of the dashboard (stays plain JS — no TS/Vitest toolchain
  introduced here) or the runtime content-finder beyond the single-site simplification.
- No JS test suite (no client build toolchain; disproportionate here).
- No publishing and no git commit/push without explicit approval (standing rule).

## Decisions (locked with Carl, 2026-06-17)

- **Scope:** full default renew, **single-site only** (drop multi-site).
- **Migration plan:** a **fresh clean plan** — new idempotent chain. The old
  `"Backoffice.Redirects"` plan key stays recorded in `umbracoKeyValue` but inert
  (its composer is gone once this package replaces the old one).
- **Existing data:** single-site everywhere (all rows `siteKey=""`). Dropping the
  `siteKey` column is safe with no dedup. The collapse migration is still written
  defensively/idempotently.
- **Screenshots:** Claude captures them by running the test site + backoffice.

## Guiding principle

Rename only what package identity and Umbraco discovery require. Remove only what
the multi-site feature owns. Leave the rest of the internal surface
(type names, table name, route, JS element name) alone to minimize churn and risk.

**Renamed (required):**
- Folder `Backoffice.Redirects/` → `Esatto.Umbraco.Backoffice.Redirects/`
- `Backoffice.Redirects.csproj` → `Esatto.Umbraco.Backoffice.Redirects.csproj`
- csproj `PackageId`, `RootNamespace`, `AssemblyName`
- C# `namespace Backoffice.Redirects;` → `namespace Esatto.Umbraco.Backoffice.Redirects;`
  in every `.cs` file
- `wwwroot/App_Plugins/Backoffice.Redirects/` → `.../Esatto.Umbraco.Backoffice.Redirects/`
- `umbraco-package.json`: `id`, `name`, dashboard `alias`, `element` path
- README references to the package name / install command

**Kept as-is (deliberate):**
- Table name `Redirects` (vendor-neutral already; DB-compatible across the rename)
- Management API route `backoffice/redirects` and all endpoint paths
- JS custom element name `backoffice-redirects-dashboard`
- C# type names (`RedirectService`, `RedirectsController`, `RedirectContentFinder`,
  `RedirectEntity`, `RedirectDto`, migration class names that survive)

**Defaults chosen (vetoable):**
- `umbraco-package.json` `id` uses dot-notation `Esatto.Umbraco.Backoffice.Redirects`.
- Version `1.0.0` (matches the renamed siblings).

## Multi-site removal — detail

### Deleted files

| File | Reason |
|---|---|
| `src/IRedirectSiteContext.cs` | the plugin contract + `RedirectSite` record |
| `src/SingleSiteRedirectContext.cs` | the default no-op site context |

### Deleted members

- `BackofficeRedirectsServiceCollectionExtensions.AddBackofficeRedirectsMultiSite<T>()`
  and the matching `IUmbracoBuilder` overload.
- `IRedirectService.GetSiteKeyAsync(int)` (only existed to gate per-site access).
- `RedirectsController.GetSites()` (`GET .../sites`) and the `IsAllowed(...)` gate.

### Per-file changes

- **`RedirectEntity`** — remove the `siteKey` column and the composite
  `IX_Redirects_siteKey_oldPath` index attribute. Add a single-column unique
  index `IX_Redirects_oldPath` on `oldPath`. Keep `id`, `oldPath`, `newUrl`,
  `createdUtc`, `updatedUtc`. Keep the `LegacyOldPathIndexName` constant only if
  the migration still references it (see Migrations).
- **`RedirectDto`** — `record RedirectDto(int Id, string OldPath, string NewUrl)`.
- **`CreateRedirectRequest` / `UpdateRedirectRequest`** — drop `SiteKey`:
  `record CreateRedirectRequest(string OldPath, string NewUrl)` (and Update).
  (The nullable-`SiteKey` NRT note no longer applies — no site field remains.)
- **`IRedirectService` / `RedirectService`**:
  - `GetAllAsync()` — no `siteKey`; `SELECT … ORDER BY oldPath`.
  - `LookupAsync(string path)` — flat lookup.
  - `TryCreateAsync` / `TryUpdateAsync` — drop the `siteKey` normalize, the
    site-key dup scoping (`WHERE oldPath = @0` only), and the "Cannot move a
    redirect between sites" branch in update. Keep all other validation
    (normalize, must start `/`, valid destination, no self-redirect, dup check).
  - Cache `_cache` becomes a flat `Dictionary<string,string>` (oldPath → newUrl),
    same lock + invalidate pattern, drafts (empty `newUrl`) still excluded.
- **`RedirectContentFinder`** — drop the `IRedirectSiteContext` dependency and the
  `ResolveForCurrentRequest()` call; `await _redirects.LookupAsync(path)`. The
  query-string/fragment merge (`AppendQuery`) and 301 behaviour are unchanged.
  Adjust the log message to drop the `{Site}` token.
- **`RedirectsController`** — keep `[Authorize(SectionAccessSettings)]`. Endpoints:
  `GET` (all), `POST` (create), `PUT {id}` (update; 404 if missing), `DELETE {id}`
  (delete; 404 if missing). No site params, no `Forbid()` paths.
- **`ServiceCollectionExtensions`** — collapse to one optional, idempotent
  `AddBackofficeRedirects()` (services-collection + `IUmbracoBuilder` overloads)
  that `TryAddSingleton<IRedirectService, RedirectService>()`. The composer already
  registers the full graph, so this is optional/explicitness-only.
- **`RedirectsComposer`** — drop the `IRedirectSiteContext` registration; keep the
  content-finder append, the `IRedirectService` registration, and the migration
  notification handler.
- **`redirects-dashboard.js`** — remove `_sites`, `_activeSite`, `_sitesLoaded`,
  the `/sites` bootstrap fetch, `#renderSiteSwitcher`, `#switchSite`,
  `#activeSiteLabel`, the `SESSION_STORAGE_KEY`, and `siteKey`/`?site=` from every
  request. `#bootstrap` just calls `#loadRows()`. Headline is plain `Redirects`.
  Drop the `.site-switcher` / `.site-chip` CSS. Everything else (add/edit/delete/
  search/draft) stays.

## Migrations — fresh clean plan

A new `MigrationPlan("Esatto.Umbraco.Backoffice.Redirects")`. Every step detects
existing state, so the chain converges from any starting point and is safe to
re-run. The old `"Backoffice.Redirects"` plan stays recorded but never executes
again (its composer is removed with the old package).

Steps (in order):

1. **`RenameLegacyTableMigration`** (`rename-legacy-table`) — unchanged in intent:
   `esattoRedirects` → `Redirects` for Esatto.Web migrants; SQLite + already-renamed
   + fresh-install are no-ops. Renames the legacy composite/old-path indexes if present.
2. **`AddRedirectsTableMigration`** (`add-redirects-table`) — `Create.Table<RedirectEntity>()`
   (now the single-site schema: `oldPath` unique index, no `siteKey`) when the table
   is absent; no-op if it already exists.
3. **`CollapseToSingleSiteMigration`** (`collapse-to-single-site`, NEW) — for tables
   carried over from an old `Backoffice.Redirects` install:
   - if `IX_Redirects_siteKey_oldPath` exists, drop it;
   - if the `siteKey` column exists, drop it (SQL Server `ALTER TABLE … DROP COLUMN`;
     SQLite has no `DROP COLUMN` pre-3.35 — log + skip, mirroring the existing
     `EnsureSiteKeyColumnMigration` SQLite guard);
   - if `IX_Redirects_oldPath` (single-column unique) is absent, create it.
   Cross-DB index lookup via `SqlSyntax.GetDefinedIndexes` (as the current code does).
   On fresh / already-collapsed tables this is a full no-op.

Notes:
- Single-site data is assumed (Carl confirmed), so dropping `siteKey` cannot
  produce `oldPath` collisions. No dedup guard is required; the step is still
  written idempotently/defensively.
- `EnsureSiteKeyColumnMigration` is **removed** (it added the column we now drop).

## Tests — `Esatto.Umbraco.Backoffice.Redirects.Tests` (xUnit)

Mirrors the sibling test projects: `net10.0`, `Nullable`/`ImplicitUsings`,
`IsPackable=false`, `IsPublishable=false`, `FrameworkReference Microsoft.AspNetCore.App`,
`Microsoft.NET.Test.Sdk` 17.11.1, `xunit` 2.9.2, `xunit.runner.visualstudio` 2.8.2,
`NSubstitute` 5.3.0, `ProjectReference` to the package. Public surface only
(no `InternalsVisibleTo`).

Test groups (no database required):

1. **`RedirectContentFinder`** (`DefaultHttpContext`/`IPublishedRequestBuilder`
   faked, `IRedirectService` faked):
   - no match → `TryFindContent` returns false, no redirect set;
   - match, no incoming query → `SetRedirect(destination, 301)`;
   - match + incoming query → query merged (`?`/`&` chosen correctly);
   - match + destination with `#fragment` → query merged before the fragment;
   - empty/whitespace path → false without hitting the service.

2. **`RedirectService` validation** (these branches return before any DB access,
   so a stub `IScopeProvider` is never exercised):
   - empty old URL → "Old URL is required.";
   - old URL not starting `/` → "Old URL must start with '/'.";
   - invalid destination (non-relative, non-http(s)) → message;
   - old == new → "Old URL and New URL cannot be the same.";
   - both create and update paths.

3. **Stretch (only if it stays clean):** a SQLite-backed round-trip of
   create/lookup/update/delete to characterize the CRUD + cache invalidation.
   Attempt it; drop rather than ship a brittle/disproportionate harness.

## Icon, README, screenshots

- **Icon:** copy the Esatto square logo → `icon.png` at package root; csproj
  `<PackageIcon>icon.png</PackageIcon>` + `<None Include="icon.png" Pack="true" PackagePath="\" />`.
- **Source-code link (required):** `PackageProjectUrl` =
  `https://github.com/carl-schele-esatto/Esatto.Packages/tree/main/Esatto.Umbraco.Backoffice.Redirects`,
  `RepositoryUrl` = repo root, `RepositoryType` = git.
- **README:** rewrite to drop every multi-site section (`IRedirectSiteContext`,
  multi-site install line, `?site=` param, `GET .../sites` endpoint, per-site 403s).
  Keep single-site install (zero-config), the endpoint list (minus `/sites`),
  the DB/migration section (updated for single-site + the legacy rename), drafts,
  search, compatibility, license.
- **Screenshots (Claude captures):** run the test site, open the Settings →
  Redirects dashboard, capture the list, the add form, an inline edit, and a
  search result. Store under `docs/`, pack under `PackagePath="docs/"`, and
  reference in the README with **absolute raw-GitHub URLs**
  (`https://raw.githubusercontent.com/carl-schele-esatto/Esatto.Packages/main/Esatto.Umbraco.Backoffice.Redirects/docs/<file>.png`)
  so they render on nuget.org / the Marketplace.

## Marketplace

The package has C# with a `Umbraco.Cms.Core` reference, so the Marketplace can
detect a supported Umbraco version (the pure-`App_Plugins` failure mode does not
apply). The `umbraco-marketplace` tag is already present. A re-scan POST may be
used after publish if the listing lags — outward action, requires Carl's go.

## Consumer repo (`c:\src\AI.Woowoo`)

`AI.Woowoo` does **not** reference this package (no entry in
`Directory.Packages.props`, `AI.Woowoo.csproj`, or `Program.cs`). So:

- No `PackageReference` / `PackageVersion` / `Program.cs` changes.
- `nuget.config`: remove `Redirects` from the private `Backoffice.*` comment;
  it now resolves from nuget.org via the `*` rule like the other public siblings.
  (`MediaTreeDnd` remains the only private `Backoffice.*`.)

If Carl later wants AI.Woowoo to consume it, that's an additive follow-up.

## Verification

- `dotnet build -c Release` on the renamed package — compiles clean.
- `dotnet test` on the new test project — all pass (report real output).
- `dotnet pack -c Release -p:AutoPushToFeed=false` — produces the nupkg locally,
  no push. Inspect the nuspec: id, version `1.0.0`, icon, README, repo URLs,
  `Umbraco.Cms.*` dependency group, `umbraco-marketplace` tag.
- Run the test site once to capture dashboard screenshots and smoke-test the
  single-site dashboard (add / edit / delete / search / draft) + a live 301.

## Boundaries / approvals required

- **No nuget.org push.** Publishing is Carl's action (his API key; permanent
  public action; the harness gates it). `AutoPushToFeed=false` for local packing.
- **No git commit/push** in either repo without explicit approval (standing rule).
  This spec is written but left **uncommitted**, deviating from the brainstorming
  skill's "commit the design doc" step per that rule.

## Open veto points (defaults stand unless changed)

- Dot-notation `umbraco-package.json` `id`.
- Collapse `ServiceCollectionExtensions` to a single `AddBackofficeRedirects()`
  (vs. dropping the extension class entirely now the composer self-registers).
- Keep the table name `Redirects`, route `backoffice/redirects`, and JS element
  name `backoffice-redirects-dashboard`.
- Defer JS/Vitest tests; SQLite CRUD tests are stretch-only.
- Version `1.0.0`.
