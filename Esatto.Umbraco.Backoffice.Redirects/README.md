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
