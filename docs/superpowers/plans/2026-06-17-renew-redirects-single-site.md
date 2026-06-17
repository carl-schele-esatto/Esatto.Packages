# Renew Redirects → single-site `Esatto.Umbraco.Backoffice.Redirects` — Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** Rename the private `Backoffice.Redirects` package to the public `Esatto.Umbraco.Backoffice.Redirects`, remove the multi-site feature entirely (single-site only), and give it the full public-release treatment (tests, icon, README screenshots, source-code link).

**Architecture:** A `Microsoft.NET.Sdk.Razor` Umbraco 17 package: SQL-backed `Redirects` table, an `IContentFinder` that serves 301s, a Management API controller, and a Lit dashboard in `wwwroot/App_Plugins`. Migrations run at app start via a composer-registered notification handler. The renew is a rename + a behaviour-preserving simplification: the `IRedirectSiteContext` plugin, per-site scoping, the dashboard site-switcher, and the `siteKey` column are all removed.

**Tech Stack:** C# / net10.0, NPoco, Umbraco.Cms 17, Lit (plain JS, no build step), xUnit + NSubstitute, MinVer (git-tag versioning).

## Global Constraints

- **No git commit / tag / push and no nuget push until Carl explicitly approves.** (Standing rule — CLAUDE.md + `no-commit-without-approval`.) Tasks below end with a build/test deliverable, NOT a commit. Commit + tag + pack are a single final gated task. This deliberately deviates from the skill's "frequent commits" default.
- Target framework `net10.0`; Umbraco `Umbraco.Cms.* 17.0.0`.
- Package id / folder / `RootNamespace` / `AssemblyName` all = `Esatto.Umbraco.Backoffice.Redirects`.
- Namespace `Esatto.Umbraco.Backoffice.Redirects` in every `.cs` file. (Verified: no inline `Umbraco.Cms.*` references exist, so the `Esatto.Umbraco` namespace-collision gotcha does not bite — use short type names with `using`s.)
- Keep these internal identifiers UNCHANGED: table name `Redirects`, API route `backoffice/redirects`, `ApiExplorerSettings` GroupName `"Backoffice Redirects"`, JS custom-element `backoffice-redirects-dashboard`.
- `umbraco-package.json` `id` uses dot-notation; `name` = `"Esatto Redirects"`; dashboard `alias` = `Esatto.Umbraco.Backoffice.Redirects.Dashboard`; section tab `meta.label` = `"Redirects"`.
- Single-site data is confirmed everywhere → dropping `siteKey` needs no dedup guard.
- Migration plan is a FRESH key `"Esatto.Umbraco.Backoffice.Redirects"`; the old `"Backoffice.Redirects"` plan stays recorded but inert.
- Source-code link (required): `RepositoryUrl` = `https://github.com/carl-schele-esatto/Esatto.Packages`, `PackageProjectUrl` = `.../tree/main/Esatto.Umbraco.Backoffice.Redirects`, `RepositoryType` = git.
- README images use absolute raw-GitHub URLs.
- Spec: `docs/superpowers/specs/2026-06-17-renew-redirects-single-site-design.md`.

Throughout, the package directory after Task 1 is:
`c:\src\Esatto.Packages\Esatto.Umbraco.Backoffice.Redirects\` (referred to below as `<PKG>/`).

---

### Task 1: Rename package identity (folder, csproj, namespaces, App_Plugins, manifest)

Pure rename. Multi-site stays intact; the package compiles at the end.

**Files:**
- Rename (git mv): `Backoffice.Redirects/` → `Esatto.Umbraco.Backoffice.Redirects/`
- Rename (git mv): `<PKG>/Backoffice.Redirects.csproj` → `<PKG>/Esatto.Umbraco.Backoffice.Redirects.csproj`
- Rename (git mv): `<PKG>/wwwroot/App_Plugins/Backoffice.Redirects/` → `<PKG>/wwwroot/App_Plugins/Esatto.Umbraco.Backoffice.Redirects/`
- Modify: the csproj (`PackageId`/`RootNamespace`/`AssemblyName`)
- Modify: every `.cs` under `<PKG>/src/` (namespace line)
- Modify: `<PKG>/wwwroot/App_Plugins/Esatto.Umbraco.Backoffice.Redirects/umbraco-package.json`
- Modify: `<PKG>/wwwroot/App_Plugins/Esatto.Umbraco.Backoffice.Redirects/redirects-dashboard.js` (manifest `element` path only — JS body handled in Task 4)

**Interfaces:**
- Produces: the renamed namespace `Esatto.Umbraco.Backoffice.Redirects` consumed by every later task.

- [ ] **Step 1: Rename the package folder**

```bash
cd /c/src/Esatto.Packages
git mv Backoffice.Redirects Esatto.Umbraco.Backoffice.Redirects
git mv Esatto.Umbraco.Backoffice.Redirects/Backoffice.Redirects.csproj \
       Esatto.Umbraco.Backoffice.Redirects/Esatto.Umbraco.Backoffice.Redirects.csproj
git mv Esatto.Umbraco.Backoffice.Redirects/wwwroot/App_Plugins/Backoffice.Redirects \
       Esatto.Umbraco.Backoffice.Redirects/wwwroot/App_Plugins/Esatto.Umbraco.Backoffice.Redirects
```

- [ ] **Step 2: Update csproj identity**

In `<PKG>/Esatto.Umbraco.Backoffice.Redirects.csproj`, change three properties (leave everything else for Task 5):

```xml
<RootNamespace>Esatto.Umbraco.Backoffice.Redirects</RootNamespace>
<AssemblyName>Esatto.Umbraco.Backoffice.Redirects</AssemblyName>
```
```xml
<PackageId>Esatto.Umbraco.Backoffice.Redirects</PackageId>
```

- [ ] **Step 3: Update the namespace in every C# file**

In each `.cs` file under `<PKG>/src/`, replace the declaration line:

```csharp
namespace Backoffice.Redirects;
```
with:
```csharp
namespace Esatto.Umbraco.Backoffice.Redirects;
```

Files: `AddRedirectsTableMigration.cs`, `EnsureSiteKeyColumnMigration.cs`, `IRedirectService.cs`, `IRedirectSiteContext.cs`, `RedirectContentFinder.cs`, `RedirectDto.cs`, `RedirectEntity.cs`, `RedirectsController.cs`, `RedirectService.cs`, `RedirectsMigrationComposer.cs`, `RedirectsMigrationPlan.cs`, `RenameLegacyTableMigration.cs`, `ServiceCollectionExtensions.cs`, `SingleSiteRedirectContext.cs`. (Several of these are deleted/rewritten later — rename them all now anyway so the project compiles at this task boundary.)

- [ ] **Step 4: Update `umbraco-package.json`**

Rewrite `<PKG>/wwwroot/App_Plugins/Esatto.Umbraco.Backoffice.Redirects/umbraco-package.json`:

```json
{
  "$schema": "../umbraco-package-schema.json",
  "id": "Esatto.Umbraco.Backoffice.Redirects",
  "name": "Esatto Redirects",
  "version": "1.0.0",
  "extensions": [
    {
      "type": "dashboard",
      "alias": "Esatto.Umbraco.Backoffice.Redirects.Dashboard",
      "name": "Redirects",
      "element": "/App_Plugins/Esatto.Umbraco.Backoffice.Redirects/redirects-dashboard.js",
      "elementName": "backoffice-redirects-dashboard",
      "meta": { "label": "Redirects", "pathname": "redirects", "weight": 100 },
      "conditions": [{ "alias": "Umb.Condition.SectionAlias", "match": "Umb.Section.Settings" }]
    }
  ]
}
```

- [ ] **Step 5: Build to verify the rename compiles**

Run:
```bash
cd /c/src/Esatto.Packages
dotnet build Esatto.Umbraco.Backoffice.Redirects/Esatto.Umbraco.Backoffice.Redirects.csproj -c Debug
```
Expected: `Build succeeded`. (Multi-site still present — that's fine; it's removed next.)

---

### Task 2: Remove multi-site from the service / controller / DTO / composer

Deletes the site-context plugin and strips site scoping from the C# request/runtime layer. The `RedirectEntity` keeps its `siteKey` column for now (removed in Task 3) so migrations still compile.

**Files:**
- Delete: `<PKG>/src/IRedirectSiteContext.cs`
- Delete: `<PKG>/src/SingleSiteRedirectContext.cs`
- Rewrite: `<PKG>/src/IRedirectService.cs`
- Rewrite: `<PKG>/src/RedirectService.cs`
- Rewrite: `<PKG>/src/RedirectDto.cs`
- Rewrite: `<PKG>/src/RedirectContentFinder.cs`
- Rewrite: `<PKG>/src/RedirectsController.cs`
- Rewrite: `<PKG>/src/ServiceCollectionExtensions.cs`
- Modify: `<PKG>/src/RedirectsMigrationComposer.cs` (drop the `IRedirectSiteContext` registration)

**Interfaces:**
- Produces (consumed by Task 7 tests + Task 3):
  - `IRedirectService.GetAllAsync() : Task<IReadOnlyList<RedirectDto>>`
  - `IRedirectService.LookupAsync(string path) : Task<string?>`
  - `IRedirectService.TryCreateAsync(CreateRedirectRequest) : Task<string?>`
  - `IRedirectService.TryUpdateAsync(int id, UpdateRedirectRequest) : Task<string?>`
  - `IRedirectService.DeleteAsync(int id) : Task<bool>`
  - `record RedirectDto(int Id, string OldPath, string NewUrl)`
  - `record CreateRedirectRequest(string OldPath, string NewUrl)`
  - `record UpdateRedirectRequest(string OldPath, string NewUrl)`
  - `RedirectService(IScopeProvider)` ctor; `RedirectContentFinder(IRedirectService, ILogger<RedirectContentFinder>)` ctor.

- [ ] **Step 1: Delete the two site-context files**

```bash
cd /c/src/Esatto.Packages
git rm Esatto.Umbraco.Backoffice.Redirects/src/IRedirectSiteContext.cs
git rm Esatto.Umbraco.Backoffice.Redirects/src/SingleSiteRedirectContext.cs
```

- [ ] **Step 2: Rewrite `IRedirectService.cs`**

```csharp
namespace Esatto.Umbraco.Backoffice.Redirects;

public interface IRedirectService
{
    /// <summary>All redirects, ordered by old path.</summary>
    Task<IReadOnlyList<RedirectDto>> GetAllAsync();

    /// <summary>Looks up a destination by inbound path. Returns null if none.</summary>
    Task<string?> LookupAsync(string path);

    /// <summary>Creates a redirect. Returns null on success, or a validation error message.</summary>
    Task<string?> TryCreateAsync(CreateRedirectRequest request);

    /// <summary>Updates an existing redirect. Returns null on success, error message on failure.</summary>
    Task<string?> TryUpdateAsync(int id, UpdateRedirectRequest request);

    /// <summary>Deletes by id. Returns true if a row was removed.</summary>
    Task<bool> DeleteAsync(int id);
}
```

- [ ] **Step 3: Rewrite `RedirectDto.cs`**

```csharp
namespace Esatto.Umbraco.Backoffice.Redirects;

public sealed record RedirectDto(int Id, string OldPath, string NewUrl);

public sealed record CreateRedirectRequest(string OldPath, string NewUrl);

public sealed record UpdateRedirectRequest(string OldPath, string NewUrl);
```

- [ ] **Step 4: Rewrite `RedirectService.cs`**

```csharp
using NPoco;
using Umbraco.Cms.Infrastructure.Scoping;

namespace Esatto.Umbraco.Backoffice.Redirects;

public sealed class RedirectService : IRedirectService
{
    private readonly IScopeProvider _scopeProvider;

    private volatile Dictionary<string, string>? _cache; // oldPath → newUrl
    private readonly object _cacheLock = new();

    public RedirectService(IScopeProvider scopeProvider)
    {
        _scopeProvider = scopeProvider;
    }

    public Task<IReadOnlyList<RedirectDto>> GetAllAsync()
    {
        using var scope = _scopeProvider.CreateScope(autoComplete: true);
        var rows = scope.Database
            .Query<RedirectEntity>()
            .OrderBy(r => r.OldPath)
            .ToList();

        IReadOnlyList<RedirectDto> result = rows
            .Select(r => new RedirectDto(r.Id, r.OldPath, r.NewUrl))
            .ToList();
        return Task.FromResult(result);
    }

    public Task<string?> LookupAsync(string path)
    {
        var key = Normalize(path);
        if (string.IsNullOrEmpty(key)) return Task.FromResult<string?>(null);

        var cache = GetOrLoadCache();
        return Task.FromResult(cache.TryGetValue(key, out var target) ? target : null);
    }

    public Task<string?> TryCreateAsync(CreateRedirectRequest request)
    {
        var oldPath = Normalize(request.OldPath);
        var newUrl = (request.NewUrl ?? string.Empty).Trim();

        var validationError = Validate(oldPath, newUrl);
        if (validationError is not null) return Task.FromResult<string?>(validationError);

        InvalidateCache();
        using (var scope = _scopeProvider.CreateScope())
        {
            var exists = scope.Database.ExecuteScalar<int>(
                $"SELECT COUNT(*) FROM {RedirectEntity.TableName} WHERE oldPath = @0", oldPath) > 0;
            if (exists)
            {
                scope.Complete();
                return Task.FromResult<string?>("A redirect for that Old URL already exists.");
            }

            var now = DateTime.UtcNow;
            scope.Database.Insert(new RedirectEntity
            {
                OldPath = oldPath,
                NewUrl = newUrl,
                CreatedUtc = now,
                UpdatedUtc = now,
            });
            scope.Complete();
        }

        InvalidateCache();
        return Task.FromResult<string?>(null);
    }

    public Task<string?> TryUpdateAsync(int id, UpdateRedirectRequest request)
    {
        var oldPath = Normalize(request.OldPath);
        var newUrl = (request.NewUrl ?? string.Empty).Trim();

        var validationError = Validate(oldPath, newUrl);
        if (validationError is not null) return Task.FromResult<string?>(validationError);

        using (var scope = _scopeProvider.CreateScope())
        {
            var existing = scope.Database.SingleOrDefaultById<RedirectEntity>(id);
            if (existing is null)
            {
                scope.Complete();
                return Task.FromResult<string?>("Redirect not found.");
            }

            if (!string.Equals(existing.OldPath, oldPath, StringComparison.OrdinalIgnoreCase))
            {
                var conflict = scope.Database.ExecuteScalar<int>(
                    $"SELECT COUNT(*) FROM {RedirectEntity.TableName} WHERE oldPath = @0 AND id <> @1",
                    oldPath, id) > 0;
                if (conflict)
                {
                    scope.Complete();
                    return Task.FromResult<string?>("A redirect for that Old URL already exists.");
                }
            }

            existing.OldPath = oldPath;
            existing.NewUrl = newUrl;
            existing.UpdatedUtc = DateTime.UtcNow;
            scope.Database.Update(existing);
            scope.Complete();
        }

        InvalidateCache();
        return Task.FromResult<string?>(null);
    }

    public Task<bool> DeleteAsync(int id)
    {
        InvalidateCache();
        int rows;
        using (var scope = _scopeProvider.CreateScope())
        {
            rows = scope.Database.Execute(
                $"DELETE FROM {RedirectEntity.TableName} WHERE id = @0", id);
            scope.Complete();
        }
        if (rows == 0) return Task.FromResult(false);

        InvalidateCache();
        return Task.FromResult(true);
    }

    private Dictionary<string, string> GetOrLoadCache()
    {
        if (_cache is not null) return _cache;
        lock (_cacheLock)
        {
            if (_cache is not null) return _cache;

            using var scope = _scopeProvider.CreateScope(autoComplete: true);
            var rows = scope.Database.Query<RedirectEntity>().ToList();

            var cache = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
            foreach (var row in rows)
            {
                if (string.IsNullOrWhiteSpace(row.NewUrl)) continue; // drafts excluded
                cache[row.OldPath] = row.NewUrl;
            }

            _cache = cache;
            return _cache;
        }
    }

    private void InvalidateCache()
    {
        lock (_cacheLock) _cache = null;
    }

    // Shared create/update validation (was duplicated inline in both methods — DRY).
    // Returns an error message, or null when valid.
    private static string? Validate(string oldPath, string newUrl)
    {
        if (string.IsNullOrEmpty(oldPath)) return "Old URL is required.";
        if (!oldPath.StartsWith('/')) return "Old URL must start with '/'.";
        if (!string.IsNullOrEmpty(newUrl) && !IsValidDestination(newUrl))
            return "New URL must be a relative path (/...) or absolute URL.";
        if (!string.IsNullOrEmpty(newUrl) && string.Equals(oldPath, newUrl, StringComparison.OrdinalIgnoreCase))
            return "Old URL and New URL cannot be the same.";
        return null;
    }

    internal static string Normalize(string? input)
    {
        if (string.IsNullOrWhiteSpace(input)) return string.Empty;
        var value = input.Trim();
        if (!value.StartsWith('/')) value = '/' + value;
        while (value.StartsWith("//", StringComparison.Ordinal)) value = value[1..];
        if (value.Length > 1) value = value.TrimEnd('/');
        return value.ToLowerInvariant();
    }

    private static bool IsValidDestination(string value)
    {
        if (value.StartsWith('/')) return true;
        return Uri.TryCreate(value, UriKind.Absolute, out var uri)
            && (uri.Scheme == Uri.UriSchemeHttp || uri.Scheme == Uri.UriSchemeHttps);
    }
}
```

- [ ] **Step 5: Rewrite `RedirectContentFinder.cs`**

Only the site-context dependency and the `{Site}` log token change; `AppendQuery` is copied verbatim.

```csharp
using System.Net;
using Microsoft.Extensions.Logging;
using Umbraco.Cms.Core.Routing;

namespace Esatto.Umbraco.Backoffice.Redirects;

/// <summary>
/// Resolves a 301 redirect from the <c>Redirects</c> table during Umbraco's
/// request pipeline. Hooks into Umbraco's <see cref="IContentFinder"/> chain.
/// </summary>
public sealed class RedirectContentFinder : IContentFinder
{
    private readonly IRedirectService _redirects;
    private readonly ILogger<RedirectContentFinder> _logger;

    public RedirectContentFinder(
        IRedirectService redirects,
        ILogger<RedirectContentFinder> logger)
    {
        _redirects = redirects;
        _logger = logger;
    }

    public async Task<bool> TryFindContent(IPublishedRequestBuilder request)
    {
        var path = request.AbsolutePathDecoded;
        if (string.IsNullOrEmpty(path)) return false;

        var destination = await _redirects.LookupAsync(path);
        if (destination is null) return false;

        var query = request.Uri.Query;
        var finalUrl = AppendQuery(destination, query);

        _logger.LogInformation("Redirecting {From} -> {To} (301)", path, finalUrl);
        request.SetRedirect(finalUrl, (int)HttpStatusCode.MovedPermanently);
        return true;
    }

    private static string AppendQuery(string destination, string incomingQuery)
    {
        if (string.IsNullOrEmpty(incomingQuery)) return destination;

        // incomingQuery begins with '?'; after trimming, a bare '?' leaves nothing to merge.
        var stripped = incomingQuery.TrimStart('?');
        if (stripped.Length == 0) return destination;

        // Split off any fragment so the query merges BEFORE the '#'.
        var hashIndex = destination.IndexOf('#');
        var beforeHash = hashIndex >= 0 ? destination[..hashIndex] : destination;
        var afterHash = hashIndex >= 0 ? destination[hashIndex..] : string.Empty;

        var separator = beforeHash.Contains('?') ? '&' : '?';
        return beforeHash + separator + stripped + afterHash;
    }
}
```

- [ ] **Step 6: Rewrite `RedirectsController.cs`**

```csharp
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Umbraco.Cms.Api.Management.Controllers;
using Umbraco.Cms.Api.Management.Routing;
using Umbraco.Cms.Web.Common.Authorization;

namespace Esatto.Umbraco.Backoffice.Redirects;

/// <summary>
/// Management API for the Redirects dashboard. All endpoints require
/// SectionAccessSettings (i.e. the user can see the Settings section).
/// </summary>
[ApiController]
[VersionedApiBackOfficeRoute("backoffice/redirects")]
[ApiExplorerSettings(GroupName = "Backoffice Redirects")]
[Authorize(Policy = AuthorizationPolicies.SectionAccessSettings)]
public sealed class RedirectsController : ManagementApiControllerBase
{
    private readonly IRedirectService _service;

    public RedirectsController(IRedirectService service)
    {
        _service = service;
    }

    [HttpGet]
    [ProducesResponseType(typeof(IReadOnlyList<RedirectDto>), StatusCodes.Status200OK)]
    public async Task<IActionResult> GetAll()
        => Ok(await _service.GetAllAsync());

    [HttpPost]
    [ProducesResponseType(StatusCodes.Status201Created)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status400BadRequest)]
    public async Task<IActionResult> Create([FromBody] CreateRedirectRequest request)
    {
        var error = await _service.TryCreateAsync(request);
        if (error is not null)
            return BadRequest(new ProblemDetails { Title = error, Status = StatusCodes.Status400BadRequest });
        return StatusCode(StatusCodes.Status201Created);
    }

    [HttpPut("{id:int}")]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> Update(int id, [FromBody] UpdateRedirectRequest request)
    {
        var error = await _service.TryUpdateAsync(id, request);
        if (error is null) return NoContent();
        if (string.Equals(error, "Redirect not found.", StringComparison.Ordinal)) return NotFound();
        return BadRequest(new ProblemDetails { Title = error, Status = StatusCodes.Status400BadRequest });
    }

    [HttpDelete("{id:int}")]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> Delete(int id)
        => await _service.DeleteAsync(id) ? NoContent() : NotFound();
}
```

- [ ] **Step 7: Rewrite `ServiceCollectionExtensions.cs`**

```csharp
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Umbraco.Cms.Core.DependencyInjection;

namespace Esatto.Umbraco.Backoffice.Redirects;

public static class BackofficeRedirectsServiceCollectionExtensions
{
    /// <summary>
    /// Registers Backoffice.Redirects services. Optional — the package's
    /// <see cref="RedirectsComposer"/> already registers the full graph
    /// automatically. Kept for explicitness; idempotent.
    /// </summary>
    public static IServiceCollection AddBackofficeRedirects(this IServiceCollection services)
    {
        services.TryAddSingleton<IRedirectService, RedirectService>();
        return services;
    }
}

public static class BackofficeRedirectsUmbracoBuilderExtensions
{
    public static IUmbracoBuilder AddBackofficeRedirects(this IUmbracoBuilder builder)
    {
        builder.Services.AddBackofficeRedirects();
        return builder;
    }
}
```

- [ ] **Step 8: Drop the site-context registration in `RedirectsMigrationComposer.cs`**

In `RedirectsComposer.Compose`, delete this line:
```csharp
        builder.Services.TryAddSingleton<IRedirectSiteContext, SingleSiteRedirectContext>();
```
Leave the content-finder append, the `IRedirectService` registration, and the migration notification handler. Update the XML-doc summary's "Multi-site installs add a single line…" sentence to drop the multi-site mention. (`RunRedirectsMigration` is unchanged.)

- [ ] **Step 9: Build to verify**

Run:
```bash
cd /c/src/Esatto.Packages
dotnet build Esatto.Umbraco.Backoffice.Redirects/Esatto.Umbraco.Backoffice.Redirects.csproj -c Debug
```
Expected: `Build succeeded` with no references to `IRedirectSiteContext` / `RedirectSite` remaining. (The entity still has its `siteKey` column — removed next.)

---

### Task 3: Single-site entity + migration chain

Drops `siteKey` from the entity and replaces the add-siteKey migration with a collapse-to-single-site migration under a fresh plan key.

**Files:**
- Rewrite: `<PKG>/src/RedirectEntity.cs`
- Modify: `<PKG>/src/RenameLegacyTableMigration.cs` (index-constant references + doc only)
- Modify: `<PKG>/src/AddRedirectsTableMigration.cs` (doc comment only)
- Delete: `<PKG>/src/EnsureSiteKeyColumnMigration.cs`
- Create: `<PKG>/src/CollapseToSingleSiteMigration.cs`
- Rewrite: `<PKG>/src/RedirectsMigrationPlan.cs`

**Interfaces:**
- Consumes: `RedirectEntity.TableName` (Task 2 service).
- Produces (consumed by migrations + Task 7): `RedirectEntity` with columns `id, oldPath, newUrl, createdUtc, updatedUtc`; constants `TableName="Redirects"`, `OldPathIndexName="IX_Redirects_oldPath"`, `LegacyCompositeIndexName="IX_Redirects_siteKey_oldPath"`.

- [ ] **Step 1: Rewrite `RedirectEntity.cs`**

```csharp
using NPoco;
using Umbraco.Cms.Infrastructure.Persistence.DatabaseAnnotations;

namespace Esatto.Umbraco.Backoffice.Redirects;

[TableName(TableName)]
[PrimaryKey("id", AutoIncrement = true)]
[ExplicitColumns]
public sealed class RedirectEntity
{
    public const string TableName = "Redirects";

    // Single-site unique index on oldPath (the entity index below + collapse migration).
    public const string OldPathIndexName = "IX_Redirects_oldPath";

    // Legacy multi-site composite index — renamed-in by the legacy rename step,
    // dropped by CollapseToSingleSiteMigration.
    public const string LegacyCompositeIndexName = "IX_Redirects_siteKey_oldPath";

    [Column("id")]
    [PrimaryKeyColumn(AutoIncrement = true, IdentitySeed = 1)]
    public int Id { get; set; }

    [Column("oldPath")]
    [Length(2048)]
    [Index(IndexTypes.UniqueNonClustered, Name = OldPathIndexName, ForColumns = "oldPath")]
    public string OldPath { get; set; } = string.Empty;

    [Column("newUrl")]
    [Length(2048)]
    public string NewUrl { get; set; } = string.Empty;

    [Column("createdUtc")]
    public DateTime CreatedUtc { get; set; }

    [Column("updatedUtc")]
    public DateTime UpdatedUtc { get; set; }
}
```

- [ ] **Step 2: Update index-constant references in `RenameLegacyTableMigration.cs`**

The migration still renames legacy `esattoRedirects` indexes to their canonical names. Update the two references that pointed at the old entity constants:

- Replace `RedirectEntity.OldPathSiteKeyIndexName` → `RedirectEntity.LegacyCompositeIndexName` (2 occurrences, in the composite-index rename block).
- Replace `RedirectEntity.LegacyOldPathIndexName` → `RedirectEntity.OldPathIndexName` (1 occurrence, the rename target in the single-column block).

Update the trailing comment in that block to: `// normalized below by collapse-to-single-site step`. No other logic changes.

- [ ] **Step 3: Update the doc comment in `AddRedirectsTableMigration.cs`**

The `<remarks>` still mentions "the `siteKey` column and the composite unique index". Replace that sentence with: "On fresh installs this creates the single-site `Redirects` table (unique index on `oldPath`)." No code change — `Create.Table<RedirectEntity>()` now emits the single-site schema.

- [ ] **Step 4: Delete `EnsureSiteKeyColumnMigration.cs`**

```bash
cd /c/src/Esatto.Packages
git rm Esatto.Umbraco.Backoffice.Redirects/src/EnsureSiteKeyColumnMigration.cs
```

- [ ] **Step 5: Create `CollapseToSingleSiteMigration.cs`**

```csharp
using Microsoft.Extensions.Logging;
using NPoco;
using Umbraco.Cms.Infrastructure.Migrations;

namespace Esatto.Umbraco.Backoffice.Redirects;

/// <summary>
/// Converts a <c>Redirects</c> table carried over from the old multi-site
/// Backoffice.Redirects package to the single-site schema: drops the composite
/// (siteKey, oldPath) unique index, drops the <c>siteKey</c> column, and ensures
/// a single-column unique index on <c>oldPath</c>. Idempotent — a no-op on fresh
/// single-site installs (the table is created single-site by the add step).
/// </summary>
/// <remarks>
/// Single-site data is assumed (every legacy row had an empty site key), so
/// flattening cannot produce an oldPath collision; no dedup is required.
/// </remarks>
public sealed class CollapseToSingleSiteMigration : AsyncMigrationBase
{
    private const string SiteKeyColumn = "siteKey";

    public CollapseToSingleSiteMigration(IMigrationContext context) : base(context) { }

    protected override Task MigrateAsync()
    {
        if (!TableExists(RedirectEntity.TableName))
        {
            Logger.LogDebug(
                "{Migration}: table {Table} not present, skipping",
                nameof(CollapseToSingleSiteMigration), RedirectEntity.TableName);
            return Task.CompletedTask;
        }

        // 1. Drop the legacy multi-site composite unique index if present.
        if (IndexExists(RedirectEntity.TableName, RedirectEntity.LegacyCompositeIndexName))
        {
            Delete.Index(RedirectEntity.LegacyCompositeIndexName)
                .OnTable(RedirectEntity.TableName).Do();
            Logger.LogInformation(
                "{Migration}: dropped legacy composite index {Index}",
                nameof(CollapseToSingleSiteMigration), RedirectEntity.LegacyCompositeIndexName);
        }

        // 2. Drop the siteKey column if present.
        if (ColumnExists(RedirectEntity.TableName, SiteKeyColumn))
        {
            if (DatabaseType == DatabaseType.SQLite)
            {
                // A fresh SQLite install never has this column; reaching here means a
                // hand-modified DB. Older SQLite providers lack reliable DROP COLUMN —
                // log and skip rather than emit SQL it can't run.
                Logger.LogWarning(
                    "{Migration}: {Column} present on a SQLite {Table}; skipping DROP COLUMN. Recreate the table if this is unexpected.",
                    nameof(CollapseToSingleSiteMigration), SiteKeyColumn, RedirectEntity.TableName);
            }
            else
            {
                Database.Execute(
                    $"ALTER TABLE [{RedirectEntity.TableName}] DROP COLUMN [{SiteKeyColumn}]");
                Logger.LogInformation(
                    "{Migration}: dropped {Column} column",
                    nameof(CollapseToSingleSiteMigration), SiteKeyColumn);
            }
        }

        // 3. Ensure a single-column UNIQUE index on oldPath. If an index with the
        // canonical name exists but is NOT unique (legacy esatto rename path),
        // drop and recreate it unique.
        var defined = Database.SqlContext.SqlSyntax
            .GetDefinedIndexes(Database)
            .Where(x => string.Equals(x.Item1, RedirectEntity.TableName, StringComparison.OrdinalIgnoreCase)
                     && string.Equals(x.Item2, RedirectEntity.OldPathIndexName, StringComparison.OrdinalIgnoreCase))
            .ToList();

        if (defined.Count > 0 && defined.Any(x => !x.Item4))
        {
            Delete.Index(RedirectEntity.OldPathIndexName).OnTable(RedirectEntity.TableName).Do();
            defined.Clear();
        }

        if (defined.Count == 0)
        {
            Create.Index(RedirectEntity.OldPathIndexName)
                .OnTable(RedirectEntity.TableName)
                .OnColumn("oldPath").Ascending()
                .WithOptions().Unique()
                .Do();
            Logger.LogInformation(
                "{Migration}: ensured unique index {Index}",
                nameof(CollapseToSingleSiteMigration), RedirectEntity.OldPathIndexName);
        }

        return Task.CompletedTask;
    }

    // Cross-database index lookup (SQL Server + SQLite). GetDefinedIndexes returns
    // one tuple per (table, index, column, isUnique).
    private bool IndexExists(string table, string indexName)
        => Database.SqlContext.SqlSyntax
            .GetDefinedIndexes(Database)
            .Any(x => string.Equals(x.Item1, table, StringComparison.OrdinalIgnoreCase)
                   && string.Equals(x.Item2, indexName, StringComparison.OrdinalIgnoreCase));
}
```

- [ ] **Step 6: Rewrite `RedirectsMigrationPlan.cs`**

```csharp
using Umbraco.Cms.Infrastructure.Migrations;

namespace Esatto.Umbraco.Backoffice.Redirects;

/// <summary>
/// Migration plan for the Redirects feature. Runs on every app startup; each
/// step is idempotent so re-runs are safe.
/// </summary>
/// <remarks>
/// <para>
/// Fresh plan key <c>"Esatto.Umbraco.Backoffice.Redirects"</c>. The previous
/// <c>"Backoffice.Redirects"</c> plan stays recorded in <c>umbracoKeyValue</c>
/// but never runs again — its composer is gone once this renamed package
/// replaces the old one. Every step detects existing state, so the chain
/// converges from any starting point (fresh, legacy <c>esattoRedirects</c>, or
/// an old multi-site <c>Backoffice.Redirects</c> install).
/// </para>
/// </remarks>
public sealed class RedirectsMigrationPlan : MigrationPlan
{
    public RedirectsMigrationPlan() : base("Esatto.Umbraco.Backoffice.Redirects")
    {
        From(string.Empty)
            .To<RenameLegacyTableMigration>("rename-legacy-table")
            .To<AddRedirectsTableMigration>("add-redirects-table")
            .To<CollapseToSingleSiteMigration>("collapse-to-single-site");
    }
}
```

- [ ] **Step 7: Build to verify the single-site C# compiles**

Run:
```bash
cd /c/src/Esatto.Packages
dotnet build Esatto.Umbraco.Backoffice.Redirects/Esatto.Umbraco.Backoffice.Redirects.csproj -c Debug
```
Expected: `Build succeeded`, no references to `siteKey` / `EnsureSiteKeyColumnMigration` / old index constants.

---

### Task 4: Single-site dashboard JS

Removes the site-switcher and all `siteKey`/`?site=` plumbing from the Lit element. Plain JS — no build step, no compile check here (smoke-tested in Task 9).

**Files:**
- Rewrite: `<PKG>/wwwroot/App_Plugins/Esatto.Umbraco.Backoffice.Redirects/redirects-dashboard.js`

- [ ] **Step 1: Replace the dashboard with the single-site version**

```javascript
import { LitElement, css, html } from '@umbraco-cms/backoffice/external/lit';
import { UMB_NOTIFICATION_CONTEXT } from '@umbraco-cms/backoffice/notification';
import { UmbElementMixin } from '@umbraco-cms/backoffice/element-api';
import { umbConfirmModal } from '@umbraco-cms/backoffice/modal';
import { umbHttpClient } from '@umbraco-cms/backoffice/http-client';
import { tryExecute, UmbApiError } from '@umbraco-cms/backoffice/resources';

// Management API endpoints require bearer auth. umbHttpClient is the
// pre-configured Bellissima OpenAPI client (token + credentials:'include');
// tryExecute wraps the request promise and returns { data, error }.
// security must be declared explicitly — without it, umbHttpClient does not
// attach the bearer token and the request 401s.
const API_BASE = '/umbraco/management/api/v1/backoffice/redirects';
const SECURITY = [{ scheme: 'bearer', type: 'http' }];

class BackofficeRedirectsDashboard extends UmbElementMixin(LitElement) {
    static properties = {
        _rows: { state: true },
        _oldPath: { state: true },
        _newUrl: { state: true },
        _error: { state: true },
        _busy: { state: true },
        _loaded: { state: true },
        _editingId: { state: true },
        _editOldPath: { state: true },
        _editNewUrl: { state: true },
        _editError: { state: true },
        _searchDraft: { state: true },
        _searchTerm: { state: true },
    };

    #notifications;

    constructor() {
        super();
        this._rows = [];
        this._oldPath = '';
        this._newUrl = '';
        this._error = '';
        this._busy = false;
        this._loaded = false;
        this._editingId = null;
        this._editOldPath = '';
        this._editNewUrl = '';
        this._editError = '';
        this._searchDraft = '';
        this._searchTerm = '';

        this.consumeContext(UMB_NOTIFICATION_CONTEXT, (ctx) => { this.#notifications = ctx; });
    }

    connectedCallback() {
        super.connectedCallback();
        this.#loadRows();
    }

    async #loadRows() {
        this._busy = true;
        try {
            const { data, error } = await tryExecute(
                this,
                umbHttpClient.get({ url: API_BASE, security: SECURITY }),
            );
            if (error) throw error;
            this._rows = data ?? [];
        } catch (err) {
            this.#notifications?.peek('danger', { data: { message: err.message ?? 'Load failed.' } });
        } finally {
            this._loaded = true;
            this._busy = false;
        }
    }

    async #add() {
        this._error = '';
        if (!this._oldPath.trim()) {
            this._error = 'Old URL is required.';
            return;
        }
        this._busy = true;
        try {
            const { error } = await tryExecute(
                this,
                umbHttpClient.post({
                    url: API_BASE,
                    body: { oldPath: this._oldPath, newUrl: this._newUrl },
                    security: SECURITY,
                }),
                { disableNotifications: true },
            );
            if (error) {
                if (UmbApiError.isUmbApiError(error) && error.status === 400) {
                    this._error = error.problemDetails?.title ?? 'Invalid input.';
                    return;
                }
                throw error;
            }

            this._oldPath = '';
            this._newUrl = '';
            await this.#loadRows();
        } catch (err) {
            this.#notifications?.peek('danger', { data: { message: err.message ?? 'Create failed.' } });
        } finally {
            this._busy = false;
        }
    }

    #startEdit(row) {
        this._editingId = row.id;
        this._editOldPath = row.oldPath;
        this._editNewUrl = row.newUrl ?? '';
        this._editError = '';
    }

    #cancelEdit() {
        this._editingId = null;
        this._editOldPath = '';
        this._editNewUrl = '';
        this._editError = '';
    }

    async #saveEdit(row) {
        this._editError = '';
        if (!this._editOldPath.trim()) {
            this._editError = 'Old URL is required.';
            return;
        }
        this._busy = true;
        try {
            const { error } = await tryExecute(
                this,
                umbHttpClient.put({
                    url: `${API_BASE}/${row.id}`,
                    body: { oldPath: this._editOldPath, newUrl: this._editNewUrl },
                    security: SECURITY,
                }),
                { disableNotifications: true },
            );
            if (error) {
                if (UmbApiError.isUmbApiError(error) && error.status === 400) {
                    this._editError = error.problemDetails?.title ?? 'Invalid input.';
                    return;
                }
                throw error;
            }
            this.#cancelEdit();
            await this.#loadRows();
        } catch (err) {
            this.#notifications?.peek('danger', { data: { message: err.message ?? 'Save failed.' } });
        } finally {
            this._busy = false;
        }
    }

    async #delete(row) {
        this._busy = true;
        try {
            await umbConfirmModal(this, {
                headline: 'Delete redirect?',
                content: `${row.oldPath} → ${row.newUrl || '(draft)'}`,
                confirmLabel: 'Delete',
                color: 'danger',
            });
        } catch {
            this._busy = false;
            return; // user cancelled
        }

        try {
            const { error } = await tryExecute(
                this,
                umbHttpClient.delete({ url: `${API_BASE}/${row.id}`, security: SECURITY }),
            );
            if (error) throw error;
            if (this._editingId === row.id) this.#cancelEdit();
            await this.#loadRows();
        } catch (err) {
            this.#notifications?.peek('danger', { data: { message: err.message ?? 'Delete failed.' } });
        } finally {
            this._busy = false;
        }
    }

    #applySearch() {
        this._searchTerm = (this._searchDraft ?? '').trim().toLowerCase();
    }

    #clearSearch() {
        this._searchDraft = '';
        this._searchTerm = '';
    }

    #filteredRows() {
        const term = this._searchTerm;
        if (!term) return this._rows;
        return this._rows.filter((r) => {
            const oldHit = (r.oldPath ?? '').toLowerCase().includes(term);
            const newHit = (r.newUrl ?? '').toLowerCase().includes(term);
            return oldHit || newHit;
        });
    }

    #renderRow(row) {
        const isEditing = this._editingId === row.id;
        const isDraft = !row.newUrl || !row.newUrl.trim();

        if (isEditing) {
            return html`
                <uui-table-row class="editing">
                    <uui-table-cell>
                        <input
                            class="edit-input"
                            type="text"
                            aria-label="Old URL"
                            placeholder="/old-path"
                            .value=${this._editOldPath}
                            ?disabled=${this._busy}
                            @input=${(e) => { this._editOldPath = e.target.value; }}>
                    </uui-table-cell>
                    <uui-table-cell>
                        <input
                            class="edit-input"
                            type="text"
                            aria-label="New URL"
                            placeholder="/new-path or https://example.com/x"
                            .value=${this._editNewUrl}
                            ?disabled=${this._busy}
                            @input=${(e) => { this._editNewUrl = e.target.value; }}>
                        ${this._editError ? html`<p role="alert" class="edit-error">${this._editError}</p>` : ''}
                    </uui-table-cell>
                    <uui-table-cell>
                        <div class="row-actions">
                            <uui-button look="secondary" ?disabled=${this._busy} @click=${() => this.#cancelEdit()}>Cancel</uui-button>
                            <uui-button look="primary" ?disabled=${this._busy} @click=${() => this.#saveEdit(row)}>Save</uui-button>
                        </div>
                    </uui-table-cell>
                </uui-table-row>
            `;
        }

        return html`
            <uui-table-row class=${isDraft ? 'draft' : ''}>
                <uui-table-cell>${row.oldPath}</uui-table-cell>
                <uui-table-cell>
                    ${isDraft
                        ? html`<span class="draft-badge">Draft — no target set</span>`
                        : row.newUrl}
                </uui-table-cell>
                <uui-table-cell>
                    <div class="row-actions">
                        <uui-button look="secondary" aria-label=${`Edit redirect ${row.oldPath}`} ?disabled=${this._busy} @click=${() => this.#startEdit(row)}>Edit</uui-button>
                        <uui-button look="secondary" color="danger" aria-label=${`Delete redirect ${row.oldPath}`} ?disabled=${this._busy} @click=${() => this.#delete(row)}>Delete</uui-button>
                    </div>
                </uui-table-cell>
            </uui-table-row>
        `;
    }

    render() {
        if (!this._loaded) {
            return html`<uui-box headline="Redirects"><p>Loading…</p></uui-box>`;
        }

        return html`
            <uui-box headline="Redirects">
                <p>Redirect dead URLs (that no longer resolve to a page) to a new URL. Matches are exact, case-insensitive, and preserve query strings.<br/>Responses are 301 (permanent). Leave <strong>New URL</strong> empty to save a <em>draft</em> — the row is listed here but no redirect fires until a target is set.</p>

                <uui-form>
                    <form class="form search-form" @submit=${(e) => { e.preventDefault(); this.#applySearch(); }}>
                        <div class="form-field">
                            <label for="redirects-search">Search</label>
                            <input
                                id="redirects-search"
                                class="text-input"
                                type="search"
                                placeholder="Filter by any part of old or new URL (e.g. 'pea' matches 'appear')"
                                .value=${this._searchDraft}
                                ?disabled=${this._busy}
                                @input=${(e) => { this._searchDraft = e.target.value; }}>
                        </div>
                        ${this._searchTerm
                            ? html`<uui-button look="secondary" type="button" ?disabled=${this._busy} @click=${() => this.#clearSearch()}>Clear</uui-button>`
                            : ''}
                        <uui-button look="primary" type="submit" ?disabled=${this._busy}>Search</uui-button>
                    </form>
                </uui-form>

                <uui-form>
                    <form class="form" @submit=${(e) => { e.preventDefault(); this.#add(); }}>
                        <div class="form-field">
                            <label for="redirects-old-url">Old URL</label>
                            <input
                                id="redirects-old-url"
                                class="text-input"
                                type="text"
                                placeholder="/old-path"
                                .value=${this._oldPath}
                                ?disabled=${this._busy}
                                aria-describedby=${this._error ? 'redirects-error' : ''}
                                aria-invalid=${this._error ? 'true' : 'false'}
                                @input=${(e) => { this._oldPath = e.target.value; }}>
                        </div>
                        <div class="form-field">
                            <label for="redirects-new-url">New URL <span class="optional">(optional — draft if empty)</span></label>
                            <input
                                id="redirects-new-url"
                                class="text-input"
                                type="text"
                                placeholder="/new-path or https://example.com/x"
                                .value=${this._newUrl}
                                ?disabled=${this._busy}
                                aria-describedby=${this._error ? 'redirects-error' : ''}
                                aria-invalid=${this._error ? 'true' : 'false'}
                                @input=${(e) => { this._newUrl = e.target.value; }}>
                        </div>
                        <uui-button look="primary" type="submit" ?disabled=${this._busy}>Add</uui-button>
                    </form>
                </uui-form>

                ${this._error ? html`<p id="redirects-error" role="alert" class="error">${this._error}</p>` : ''}

                ${(() => {
                    const filtered = this.#filteredRows();
                    if (this._rows.length === 0) {
                        return this._busy ? '' : html`<p class="empty">No redirects configured.</p>`;
                    }
                    if (filtered.length === 0) {
                        return html`<p class="empty">No matches for <strong>${this._searchTerm}</strong>.</p>`;
                    }
                    return html`
                        ${this._searchTerm ? html`<p class="match-count">${filtered.length} of ${this._rows.length} redirects match.</p>` : ''}
                        <uui-table>
                            <uui-table-head>
                                <uui-table-head-cell>Old URL</uui-table-head-cell>
                                <uui-table-head-cell>New URL</uui-table-head-cell>
                                <uui-table-head-cell></uui-table-head-cell>
                            </uui-table-head>
                            ${filtered.map((row) => this.#renderRow(row))}
                        </uui-table>
                    `;
                })()}
            </uui-box>
        `;
    }

    static styles = css`
        :host { display: block; padding: var(--uui-size-space-5); }
        uui-box p { margin-block-end: var(--uui-size-space-6); }
        .form { display: flex; gap: var(--uui-size-space-3); align-items: flex-end; margin-block-end: var(--uui-size-space-5); }
        uui-button { min-width: 5.5rem; }
        .form-field { display: flex; flex-direction: column; gap: var(--uui-size-space-2); flex: 1; }
        .form-field label { font-weight: var(--uui-font-weight-bold, 700); }
        .form-field label .optional { font-weight: normal; color: var(--uui-color-text-alt); font-size: var(--uui-font-size-small, 0.875em); }
        .form-field .text-input { width: 100%; }
        .text-input,
        .edit-input {
            box-sizing: border-box;
            padding: var(--uui-size-space-2, 6px) var(--uui-size-space-3, 9px);
            border: 1px solid var(--uui-color-border, #d8d7d9);
            border-radius: var(--uui-border-radius, 3px);
            font: inherit;
            color: inherit;
            background: var(--uui-color-surface, #fff);
        }
        .text-input:focus,
        .edit-input:focus {
            outline: none;
            border-color: var(--uui-color-focus, #3879ff);
            box-shadow: 0 0 0 1px var(--uui-color-focus, #3879ff);
        }
        .text-input:disabled,
        .edit-input:disabled { opacity: 0.6; }
        .error { color: var(--uui-color-danger); margin-block-end: var(--uui-size-space-3); }
        .edit-error { color: var(--uui-color-danger); margin: var(--uui-size-space-2) 0 0; font-size: var(--uui-font-size-small, 0.875em); }
        .empty { color: var(--uui-color-text-alt); font-style: italic; margin-block-start: var(--uui-size-space-3); }
        .match-count { color: var(--uui-color-text-alt); margin-block-start: var(--uui-size-space-3); margin-block-end: 0; font-size: var(--uui-font-size-small, 0.875em); }
        uui-table { margin-block-start: var(--uui-size-space-3); table-layout: fixed; width: 100%; }
        uui-table-head-cell:nth-child(1),
        uui-table-cell:nth-child(1),
        uui-table-head-cell:nth-child(2),
        uui-table-cell:nth-child(2) { width: 42%; }
        uui-table-head-cell:nth-child(3),
        uui-table-cell:nth-child(3) { width: 16%; text-align: right; }
        uui-table-cell { word-break: break-word; vertical-align: middle; }
        .row-actions { display: flex; gap: var(--uui-size-space-2); justify-content: flex-end; }
        .draft-badge { color: var(--uui-color-text-alt); font-style: italic; }
        uui-table-row.draft uui-table-cell:first-child { color: var(--uui-color-text-alt); }
        uui-table-row.editing .edit-input { width: 100%; }
    `;
}

customElements.define('backoffice-redirects-dashboard', BackofficeRedirectsDashboard);
export default BackofficeRedirectsDashboard;
```

- [ ] **Step 2: Sanity-check no site references remain**

Run:
```bash
cd /c/src/Esatto.Packages/Esatto.Umbraco.Backoffice.Redirects
grep -in "site\|sessionStorage" wwwroot/App_Plugins/Esatto.Umbraco.Backoffice.Redirects/redirects-dashboard.js || echo "clean"
```
Expected: `clean` (no `site`, `_sites`, `?site=`, or `sessionStorage` left).

---

### Task 5: csproj package metadata (icon, source-code link, docs pack) + icon file

**Files:**
- Modify: `<PKG>/Esatto.Umbraco.Backoffice.Redirects.csproj`
- Create: `<PKG>/icon.png`

- [ ] **Step 1: Copy the Esatto icon**

```bash
cd /c/src/Esatto.Packages
cp /c/Users/carl_/Downloads/esatto-logo-square.png Esatto.Umbraco.Backoffice.Redirects/icon.png
```

- [ ] **Step 2: Add icon + source-code link to the NuGet PropertyGroup**

In the `<PropertyGroup Label="NuGet">` block, after `<PackageLicenseExpression>MIT</PackageLicenseExpression>`, add:

```xml
    <PackageIcon>icon.png</PackageIcon>
    <PackageProjectUrl>https://github.com/carl-schele-esatto/Esatto.Packages/tree/main/Esatto.Umbraco.Backoffice.Redirects</PackageProjectUrl>
    <RepositoryUrl>https://github.com/carl-schele-esatto/Esatto.Packages</RepositoryUrl>
    <RepositoryType>git</RepositoryType>
```

- [ ] **Step 3: Add icon + docs to the pack ItemGroup**

Replace the existing single-line README `<ItemGroup>` with:

```xml
  <ItemGroup>
    <None Include="README.md" Pack="true" PackagePath="\" />
    <None Include="icon.png" Pack="true" PackagePath="\" />
    <None Include="docs\**\*.png" Pack="true" PackagePath="docs\" />
  </ItemGroup>
```

- [ ] **Step 4: Build to confirm the metadata is valid**

Run:
```bash
cd /c/src/Esatto.Packages
dotnet build Esatto.Umbraco.Backoffice.Redirects/Esatto.Umbraco.Backoffice.Redirects.csproj -c Debug
```
Expected: `Build succeeded`. (`docs\**\*.png` matches nothing yet — that's fine; screenshots arrive in Task 9.)

---

### Task 6: README rewrite (single-site)

**Files:**
- Rewrite: `<PKG>/README.md`

- [ ] **Step 1: Replace `README.md`**

Drops every multi-site section. Image links use absolute raw-GitHub URLs; the referenced PNGs are produced in Task 9.

````markdown
# Esatto.Umbraco.Backoffice.Redirects

URL redirects admin for Umbraco 17. Editors manage dead-URL → new-URL rules from a Settings-section dashboard; the runtime serves 301 responses via an Umbraco `IContentFinder`.

- SQL-backed `Redirects` table — no Umbraco content nodes, no uSync churn
- Exact, case-insensitive matching with query-string preservation
- "Draft" state — list a row without a target URL until you decide where it should go
- Search across both old and new URLs
- Idempotent migrations — safe on fresh DBs and on installs that previously used the legacy `esattoRedirects` table

## How it works

Open **Settings → Redirects**, add an Old URL and (optionally) a New URL, and the
runtime answers matching front-end requests with a 301.

![Redirects dashboard](https://raw.githubusercontent.com/carl-schele-esatto/Esatto.Packages/main/Esatto.Umbraco.Backoffice.Redirects/docs/redirects-dashboard.png)

Leave **New URL** empty to keep a draft until you decide the target; inline-edit or
delete any row; search filters across both columns.

![Editing a redirect](https://raw.githubusercontent.com/carl-schele-esatto/Esatto.Packages/main/Esatto.Umbraco.Backoffice.Redirects/docs/redirects-edit.png)

## Install

```bash
dotnet add package Esatto.Umbraco.Backoffice.Redirects
```

**Nothing to wire up.** Installing the package is enough — it self-registers via
its composer (appends the content finder and runs the migration at startup). You
may optionally call `builder.Services.AddBackofficeRedirects();` for explicitness;
it's idempotent.

## Endpoint

The Management API is at:

```
GET    /umbraco/management/api/v1/backoffice/redirects
POST   /umbraco/management/api/v1/backoffice/redirects
PUT    /umbraco/management/api/v1/backoffice/redirects/{id}
DELETE /umbraco/management/api/v1/backoffice/redirects/{id}
```

All endpoints require `AuthorizationPolicies.SectionAccessSettings`.

## Database

- Table: `Redirects`
- Unique index: `IX_Redirects_oldPath`
- Migrations run automatically via Umbraco's `IComposer` at app start
- Migrations are **idempotent** — re-runs are safe

### Migrating from the legacy `esattoRedirects` table

If your install previously used the in-tree Esatto.Web implementation, the first
run renames `esattoRedirects` → `Redirects` (and its indexes) via `sp_rename`.
This is metadata-only — no row data is touched. The rename runs only when the new
`Redirects` table does not yet exist and the legacy `esattoRedirects` table does.

## Compatibility

| Umbraco | Status |
|---------|--------|
| 17.x    | Verified |

## License

MIT.
````

- [ ] **Step 2: Confirm no multi-site references remain**

Run:
```bash
cd /c/src/Esatto.Packages/Esatto.Umbraco.Backoffice.Redirects
grep -in "site\|IRedirectSiteContext\|multi-site" README.md || echo "clean"
```
Expected: `clean`.

---

### Task 7: Test project

xUnit project mirroring the siblings, with no-DB characterization tests over the public surface.

**Files:**
- Create: `Esatto.Umbraco.Backoffice.Redirects.Tests/Esatto.Umbraco.Backoffice.Redirects.Tests.csproj`
- Create: `Esatto.Umbraco.Backoffice.Redirects.Tests/RedirectContentFinderTests.cs`
- Create: `Esatto.Umbraco.Backoffice.Redirects.Tests/RedirectServiceValidationTests.cs`

**Interfaces:**
- Consumes: the Task 2 service/finder public surface and Task 2 DTOs.

- [ ] **Step 1: Create the test csproj**

`Esatto.Umbraco.Backoffice.Redirects.Tests/Esatto.Umbraco.Backoffice.Redirects.Tests.csproj`:

```xml
<Project Sdk="Microsoft.NET.Sdk">

  <PropertyGroup>
    <TargetFramework>net10.0</TargetFramework>
    <Nullable>enable</Nullable>
    <ImplicitUsings>enable</ImplicitUsings>
    <IsPackable>false</IsPackable>
    <IsPublishable>false</IsPublishable>
  </PropertyGroup>

  <!-- Gives the tests the ASP.NET Core + Umbraco abstractions the package uses. -->
  <ItemGroup>
    <FrameworkReference Include="Microsoft.AspNetCore.App" />
  </ItemGroup>

  <ItemGroup>
    <PackageReference Include="Microsoft.NET.Test.Sdk" Version="17.11.1" />
    <PackageReference Include="xunit" Version="2.9.2" />
    <PackageReference Include="xunit.runner.visualstudio" Version="2.8.2" />
    <PackageReference Include="NSubstitute" Version="5.3.0" />
  </ItemGroup>

  <ItemGroup>
    <ProjectReference Include="..\Esatto.Umbraco.Backoffice.Redirects\Esatto.Umbraco.Backoffice.Redirects.csproj" />
  </ItemGroup>

</Project>
```

- [ ] **Step 2: Write `RedirectContentFinderTests.cs`**

```csharp
using Microsoft.Extensions.Logging.Abstractions;
using NSubstitute;
using Umbraco.Cms.Core.Routing;
using Xunit;

namespace Esatto.Umbraco.Backoffice.Redirects.Tests;

public class RedirectContentFinderTests
{
    private static IPublishedRequestBuilder FakeRequest(string path, string uri)
    {
        var request = Substitute.For<IPublishedRequestBuilder>();
        request.AbsolutePathDecoded.Returns(path);
        request.Uri.Returns(new Uri(uri));
        return request;
    }

    private static RedirectContentFinder Finder(IRedirectService service)
        => new RedirectContentFinder(service, NullLogger<RedirectContentFinder>.Instance);

    [Fact]
    public async Task NoMatch_ReturnsFalse_AndDoesNotRedirect()
    {
        var service = Substitute.For<IRedirectService>();
        service.LookupAsync(Arg.Any<string>()).Returns((string?)null);
        var request = FakeRequest("/missing", "https://example.com/missing");

        var handled = await Finder(service).TryFindContent(request);

        Assert.False(handled);
        request.DidNotReceive().SetRedirect(Arg.Any<string>(), Arg.Any<int>());
    }

    [Fact]
    public async Task Match_NoQuery_Sets301ToDestination()
    {
        var service = Substitute.For<IRedirectService>();
        service.LookupAsync("/old").Returns("/new");
        var request = FakeRequest("/old", "https://example.com/old");

        var handled = await Finder(service).TryFindContent(request);

        Assert.True(handled);
        request.Received(1).SetRedirect("/new", 301);
    }

    [Fact]
    public async Task Match_WithIncomingQuery_MergesQuery()
    {
        var service = Substitute.For<IRedirectService>();
        service.LookupAsync("/old").Returns("/new");
        var request = FakeRequest("/old", "https://example.com/old?a=1&b=2");

        await Finder(service).TryFindContent(request);

        request.Received(1).SetRedirect("/new?a=1&b=2", 301);
    }

    [Fact]
    public async Task Match_DestinationHasQuery_UsesAmpersand()
    {
        var service = Substitute.For<IRedirectService>();
        service.LookupAsync("/old").Returns("/new?x=1");
        var request = FakeRequest("/old", "https://example.com/old?a=1");

        await Finder(service).TryFindContent(request);

        request.Received(1).SetRedirect("/new?x=1&a=1", 301);
    }

    [Fact]
    public async Task Match_DestinationHasFragment_MergesQueryBeforeFragment()
    {
        var service = Substitute.For<IRedirectService>();
        service.LookupAsync("/old").Returns("/new#section");
        var request = FakeRequest("/old", "https://example.com/old?a=1");

        await Finder(service).TryFindContent(request);

        request.Received(1).SetRedirect("/new?a=1#section", 301);
    }

    [Fact]
    public async Task EmptyPath_ReturnsFalse_WithoutLookup()
    {
        var service = Substitute.For<IRedirectService>();
        var request = FakeRequest("", "https://example.com/");

        var handled = await Finder(service).TryFindContent(request);

        Assert.False(handled);
        await service.DidNotReceive().LookupAsync(Arg.Any<string>());
    }
}
```

- [ ] **Step 3: Write `RedirectServiceValidationTests.cs`**

These exercise only the validation early-returns, which fire before any DB scope is used, so the faked `IScopeProvider` is never invoked.

```csharp
using NSubstitute;
using Umbraco.Cms.Infrastructure.Scoping;
using Xunit;

namespace Esatto.Umbraco.Backoffice.Redirects.Tests;

public class RedirectServiceValidationTests
{
    private static RedirectService NewService()
        => new RedirectService(Substitute.For<IScopeProvider>());

    [Fact]
    public async Task Create_EmptyOldPath_ReturnsRequiredError()
    {
        var error = await NewService().TryCreateAsync(new CreateRedirectRequest("", "/new"));
        Assert.Equal("Old URL is required.", error);
    }

    [Fact]
    public async Task Create_InvalidDestination_ReturnsDestinationError()
    {
        var error = await NewService().TryCreateAsync(new CreateRedirectRequest("/old", "notaurl"));
        Assert.Equal("New URL must be a relative path (/...) or absolute URL.", error);
    }

    [Fact]
    public async Task Create_SameOldAndNew_ReturnsSameError()
    {
        var error = await NewService().TryCreateAsync(new CreateRedirectRequest("/dup", "/dup"));
        Assert.Equal("Old URL and New URL cannot be the same.", error);
    }

    [Fact]
    public async Task Update_EmptyOldPath_ReturnsRequiredError()
    {
        var error = await NewService().TryUpdateAsync(1, new UpdateRedirectRequest("", "/new"));
        Assert.Equal("Old URL is required.", error);
    }
}
```

- [ ] **Step 4: Run the tests**

Run:
```bash
cd /c/src/Esatto.Packages
dotnet test Esatto.Umbraco.Backoffice.Redirects.Tests/Esatto.Umbraco.Backoffice.Redirects.Tests.csproj
```
Expected: all 10 tests pass. (If `service.LookupAsync("/x").Returns("/y")` fails to compile under the installed NSubstitute, wrap the value: `.Returns(Task.FromResult<string?>("/y"))`.)

---

### Task 8: Consumer `nuget.config` comment (AI.Woowoo)

`AI.Woowoo` does not reference this package, so the only consumer change is documentation: the package now resolves publicly.

**Files:**
- Modify: `c:\src\AI.Woowoo\nuget.config`

- [ ] **Step 1: Update the `packageSourceMapping` comment**

In `c:\src\AI.Woowoo\nuget.config`, update the comment above the `esatto-packages` source so it no longer lists `Redirects` as private. Change the parenthetical:

```xml
    <!-- Private Esatto packages come only from the Azure DevOps feed.
         The public Esatto.Umbraco.Backoffice.* packages (ContentTreeDragAndDrop,
         CustomEditors, DictionaryFilterValues, SharedPreviewLink, Redirects)
         resolve from nuget.org via the '*' rule above; only the private
         Backoffice.* packages (MediaTreeDnd) are mapped here. -->
```

The `<package pattern="Backoffice.*" />` mapping stays (still serves `MediaTreeDnd`). No other AI.Woowoo file changes.

- [ ] **Step 2: Confirm no stale Redirects reference**

Run:
```bash
cd /c/src/AI.Woowoo
grep -rin "redirect" Directory.Packages.props AI.Woowoo.csproj AI.Woowoo.TestSite/Program.cs 2>/dev/null || echo "no code references (expected)"
```
Expected: `no code references (expected)`.

---

### Task 9: Verify, capture screenshots, then commit + tag + pack (gated on Carl's approval)

**Files:**
- Create: `<PKG>/docs/redirects-dashboard.png`
- Create: `<PKG>/docs/redirects-edit.png`

- [ ] **Step 1: Release build + full test run**

Run:
```bash
cd /c/src/Esatto.Packages
dotnet build Esatto.Umbraco.Backoffice.Redirects/Esatto.Umbraco.Backoffice.Redirects.csproj -c Release
dotnet test Esatto.Umbraco.Backoffice.Redirects.Tests/Esatto.Umbraco.Backoffice.Redirects.Tests.csproj -c Release
```
Expected: build succeeds; all tests pass. Report the real output.

- [ ] **Step 2: Capture dashboard screenshots**

Render the single-site dashboard and screenshot the list view and an inline-edit
view. Preferred: invoke the `umbraco-mocked-backoffice` skill to mount
`backoffice-redirects-dashboard` with a mocked `GET /backoffice/redirects`
returning a few rows (one with an empty `newUrl` to show the draft badge), then
screenshot at desktop width. Save as `<PKG>/docs/redirects-dashboard.png` (list +
add form + search) and `<PKG>/docs/redirects-edit.png` (a row in edit mode).

Fallback if a render harness is disproportionate: defer screenshots to a later
patch — remove the two `![...]` image lines from the README in that case and note
it. Do not block the release on screenshots.

- [ ] **Step 3: Pack locally (no push)**

Run:
```bash
cd /c/src/Esatto.Packages
dotnet pack Esatto.Umbraco.Backoffice.Redirects/Esatto.Umbraco.Backoffice.Redirects.csproj -c Release -p:AutoPushToFeed=false -o .local-feed
```
Expected: produces `Esatto.Umbraco.Backoffice.Redirects.<version>.nupkg` (untagged → a `-preview` version). No push occurs.

- [ ] **Step 4: Inspect the nupkg contents**

Run:
```bash
cd /c/src/Esatto.Packages/.local-feed
unzip -l Esatto.Umbraco.Backoffice.Redirects.*.nupkg | grep -Ei "icon.png|README.md|docs/|umbraco-package.json|\.dll"
```
Expected: the nuspec carries the icon, README, `docs/*.png` (if captured), the `App_Plugins` manifest, and the assembly. Also confirm (open the `.nuspec`) `repository url`, `projectUrl`, `Umbraco.Cms.Core` dependency, and the `umbraco-marketplace` tag.

- [ ] **Step 5: STOP — request approval before any git/publish action**

Present the diff summary and the verification output to Carl. Do **not** proceed to Step 6 until Carl explicitly approves committing.

- [ ] **Step 6: Commit + tag (only after explicit approval)**

```bash
cd /c/src/Esatto.Packages
git add -A
git commit -m @'
Renew Backoffice.Redirects -> Esatto.Umbraco.Backoffice.Redirects (single-site)

Rename to the public Esatto.Umbraco.Backoffice.* family, remove multi-site
(IRedirectSiteContext, siteKey column, dashboard site-switcher), add xUnit
tests, icon, README screenshots, and the source-code link. Fresh migration
plan collapses any legacy multi-site table to single-site.

Co-Authored-By: Claude Opus 4.8 (1M context) <noreply@anthropic.com>
'@
git tag Esatto.Umbraco.Backoffice.Redirects-1.0.0
```

Then commit the AI.Woowoo `nuget.config` comment in that repo:
```bash
cd /c/src/AI.Woowoo
git add nuget.config
git commit -m @'
Resolve Esatto.Umbraco.Backoffice.Redirects publicly (renewed, public)

Co-Authored-By: Claude Opus 4.8 (1M context) <noreply@anthropic.com>
'@
```

- [ ] **Step 7: Pack the tagged 1.0.0 (no push) and hand off**

```bash
cd /c/src/Esatto.Packages
dotnet pack Esatto.Umbraco.Backoffice.Redirects/Esatto.Umbraco.Backoffice.Redirects.csproj -c Release -p:AutoPushToFeed=false -o .local-feed
```
Expected: `Esatto.Umbraco.Backoffice.Redirects.1.0.0.nupkg`. Carl runs the
`dotnet nuget push` to nuget.org himself. After publish, if the Marketplace
listing lags, the single-package re-scan POST may be used — outward action,
Carl's go.

---

## Self-Review

**Spec coverage:**
- Rename map → Task 1. ✓
- Multi-site removal (interface/context/extensions/controller/finder/service/dto/composer) → Task 2. ✓
- Single-site entity + fresh migration plan + CollapseToSingleSiteMigration → Task 3. ✓
- Dashboard JS single-site → Task 4. ✓
- Icon + source-code link + docs pack → Task 5. ✓
- README rewrite (absolute image URLs, no multi-site) → Task 6. ✓
- xUnit tests (content finder + validation) → Task 7. ✓
- Consumer nuget.config comment → Task 8. ✓
- Verification, screenshots, marketplace dependency check, commit/tag/pack gated → Task 9. ✓

**Placeholder scan:** No TBD/TODO; every code step shows full content. Screenshot step has an explicit fallback rather than a placeholder.

**Type consistency:** `GetAllAsync()` (no args), `LookupAsync(string)`, `TryCreateAsync(CreateRedirectRequest)`, `TryUpdateAsync(int, UpdateRedirectRequest)`, `DeleteAsync(int)` used identically in Task 2 (definition), Task 2 controller (caller), and Task 7 (tests). DTOs `RedirectDto(Id, OldPath, NewUrl)` / `CreateRedirectRequest(OldPath, NewUrl)` / `UpdateRedirectRequest(OldPath, NewUrl)` consistent across tasks. Entity constants `TableName` / `OldPathIndexName` / `LegacyCompositeIndexName` defined in Task 3 and consumed by `RenameLegacyTableMigration`, `CollapseToSingleSiteMigration`, and `RedirectService`. ✓

**Known caveat (carried from spec, not a gap):** the unique index sits on `oldPath nvarchar(2048)`, which exceeds SQL Server's 1700-byte index-key limit — this is pre-existing (the old composite index had the same exposure) and is left behaviour-preserving; it works on the SQLite dev/test DB.
