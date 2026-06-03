# Backoffice.Redirects

URL redirects admin for Umbraco 17. Editors manage dead-URL → new-URL rules from a Settings-section dashboard; the runtime serves 301 responses via an Umbraco `IContentFinder`.

- SQL-backed `Redirects` table — no Umbraco content nodes, no uSync churn
- Exact, case-insensitive matching with query-string preservation
- "Draft" state — list a row without a target URL until you decide where it should go
- Search across both old and new URLs
- **Multi-site support via plugin** (`IRedirectSiteContext`)
- Idempotent migrations — safe to install on fresh DBs and on installs that previously used the legacy `esattoRedirects` table

## Install

```bash
dotnet add package Backoffice.Redirects
```

**Single-site: nothing to wire up.** Installing the package is enough — it
self-registers in single-site mode via its composer. The dashboard hides its
site-switcher tab bar and all redirects live in one flat list. (You may still
call `builder.Services.AddBackofficeRedirectsSingleSite();` explicitly if you
prefer; it's optional and idempotent.)

**Multi-site:** add ONE line to `Program.cs`:

```csharp
builder.Services.AddBackofficeRedirectsMultiSite<MyRedirectSiteContext>();
```

…where `MyRedirectSiteContext` is your implementation of `IRedirectSiteContext` (see below). The dashboard renders one tab per site you return; the runtime content finder scopes redirect lookups by the site you resolve from the request.

## The `IRedirectSiteContext` plugin

The package treats "site" as an opaque string key — it doesn't care whether you mean a hostname, a culture, a tenant, or anything else. You implement two methods:

```csharp
public interface IRedirectSiteContext
{
    /// <summary>Sites the current backoffice user can manage. Empty list = single-site mode.</summary>
    IReadOnlyList<RedirectSite> GetAllowedSites();

    /// <summary>Site key for the current incoming front-end request.</summary>
    string ResolveForCurrentRequest();
}

public sealed record RedirectSite(string Key, string Label);
```

Example for a multi-site Umbraco install scoped by startPage:

```csharp
public sealed class MyRedirectSiteContext : IRedirectSiteContext
{
    private readonly IMySiteResolver _sites;
    private readonly IHttpContextAccessor _http;

    public MyRedirectSiteContext(IMySiteResolver sites, IHttpContextAccessor http)
    {
        _sites = sites;
        _http = http;
    }

    public IReadOnlyList<RedirectSite> GetAllowedSites()
        => _sites.GetAllowedSitesForCurrentUser()
            .Select(s => new RedirectSite(s.Key, s.DisplayName))
            .ToList();

    public string ResolveForCurrentRequest()
    {
        var host = _http.HttpContext?.Request.Host.Host ?? "";
        return _sites.ResolveForHost(host).Key;
    }
}
```

## Endpoint

The Management API is at:

```
GET    /umbraco/management/api/v1/backoffice/redirects/sites
GET    /umbraco/management/api/v1/backoffice/redirects?site=<key>
POST   /umbraco/management/api/v1/backoffice/redirects
PUT    /umbraco/management/api/v1/backoffice/redirects/{id}
DELETE /umbraco/management/api/v1/backoffice/redirects/{id}
```

All endpoints require `AuthorizationPolicies.SectionAccessSettings`. Per-site access is enforced via your `IRedirectSiteContext.GetAllowedSites()` — operations on a site the user can't manage return 403.

## Database

- Table: `Redirects`
- Composite unique index: `IX_Redirects_siteKey_oldPath`
- Migrations run automatically via Umbraco's `IComposer` at app start
- Migrations are **idempotent** — re-runs are safe

### Migrating from the legacy `esattoRedirects` table

If your install previously used the in-tree Esatto.Web implementation, the first time this package runs it will rename `esattoRedirects` → `Redirects` (and rename its indexes) via `sp_rename`. This is metadata-only — no row data is touched, no downtime beyond a brief schema lock. The rename migration is idempotent and runs only when:

1. The new table `Redirects` does NOT yet exist, AND
2. The legacy `esattoRedirects` table DOES exist

Both fresh installs (no legacy table → skip rename, create from scratch) and re-runs after success (new table exists → skip everything) are no-ops.

## Compatibility

| Umbraco | Status |
|---------|--------|
| 17.x    | Verified |

## License

MIT.
