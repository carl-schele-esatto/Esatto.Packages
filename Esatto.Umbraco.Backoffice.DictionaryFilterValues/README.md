# Esatto.Umbraco.Backoffice.DictionaryFilterValues

Extends the Umbraco 17 backoffice **Dictionary** section filter to also match translation **values**, not just dictionary-item names.

The built-in Bellissima filter (`Type to filter...`) matches against item keys only — editors who remember a translated string like *"Visa fler"* but not its key (`BlocksPageListBlockShowMore`) can't find what they're looking for. This package makes the filter match across every translation value too.

## Install

```bash
dotnet add package Esatto.Umbraco.Backoffice.DictionaryFilterValues
```

Restart the site / hard-refresh the backoffice. It activates automatically — no configuration. The composer and Management API controller are auto-discovered; no DI wiring, no SQL migrations, no `Startup` changes.

## How it works

On load, a small backoffice **entry point** swaps the *data source* behind Umbraco's built-in Dictionary collection using the supported extension registry — it re-points the collection's `repositoryAlias` at this package's own repository (registering ours, then re-registering the stock collection manifest pointed at it). The stock collection element, views, paging and workspace are untouched; only where the data comes from changes.

That repository calls an **authenticated** Management API endpoint which matches the editor's filter text against the dictionary key **or any translation value**, server-side. The endpoint reads from an in-memory cache of the whole dictionary — built once with a single bulk `IDictionaryItemService` call and invalidated automatically whenever a dictionary item is saved or deleted, so edits show up without a reload.

No `window.fetch` patching, no anonymous endpoints, no client-side dump cache.

## Endpoint

- `GET /umbraco/management/api/v1/backoffice/dictionary-filter-values?filter=&skip=&take=`
- **Authenticated** as a backoffice user (`AuthorizationPolicies.TreeAccessDictionary`); it is only ever called from inside the backoffice via the collection repository, with the bearer token attached automatically.
- Returns dictionary items whose key or any translation value matches `filter` (case-insensitive; empty returns all), paged, in the same shape as the built-in dictionary collection endpoint.

## Compatibility

| Umbraco | Status |
|---------|--------|
| 17.x    | Verified |

The collection data-source extension point and the Management API surface are specific to the Umbraco 17 (Bellissima) backoffice; earlier majors are not supported.

## License

MIT.

<!-- TODO: add a Screenshots section (GIF of the filter matching a translated value) once assets are provided. -->
