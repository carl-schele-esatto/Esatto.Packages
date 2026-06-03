# Backoffice.DictionaryFilterValues

Extends the Umbraco 17 backoffice **Dictionary** section filter to also match translation **values**, not just dictionary-item names.

The built-in Bellissima filter (`Type to filter...`) matches against item keys only — editors who remember a translated string like *"Visa fler"* but not its key (`BlocksPageListBlockShowMore`) can't find what they're looking for. This package makes the filter match across every translation value too.

## Install

```bash
dotnet add package Backoffice.DictionaryFilterValues
```

Restart the site / hard-refresh the backoffice. Activates automatically on the Translation section.

No DI configuration needed — the C# controller is auto-discovered via the Razor Class Library's application-part registration. No SQL migrations, no `Startup` changes.

## How it works (one-paragraph version)

A C# controller exposes `GET /umbraco/api/backoffice-dictionary-filter-values/search` which uses `IDictionaryItemService` to enumerate every dictionary item plus its translation values. A backoffice JS shim patches `window.fetch` to redirect non-empty-filter calls hitting `/umbraco/management/api/v1/dictionary?filter=...` to that custom endpoint, reshapes the response to match the OpenAPI client's expected envelope, and lets the rest of Bellissima render normally. First filter keystroke fetches once and caches in-memory; subsequent keystrokes filter the cache so typing stays instant. The cache is pre-warmed when the user navigates into the Translation section (via a `pushState` patch) so even the first keystroke is responsive.

## Endpoint

- `GET /umbraco/api/backoffice-dictionary-filter-values/search` — `[AllowAnonymous]`, read-only walk of dictionary items + their translation values.
- Endpoint is anonymous because dictionary values are already served to anonymous visitors on public pages. If you have non-public dictionary strings (rare), this package is not appropriate without modification.

## Compatibility

| Umbraco | Status |
|---------|--------|
| 17.x    | Verified |

Earlier versions of Umbraco use a different Dictionary collection repository surface — not supported.

## License

MIT.
