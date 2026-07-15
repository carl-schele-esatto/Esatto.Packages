# Esatto.Umbraco.Backoffice.DictionaryLocalization

Bridges Umbraco 17 **content Dictionary items** into the **backoffice UI localization system**. After install, any `#Key` in a content-type property label (or anywhere `localize.string()` is called) resolves to the current backoffice user's culture value from the Dictionary section — for every existing and future dictionary key, with no per-key manifest wiring.

## Why

Umbraco has two independent translation systems that look interchangeable but aren't:

1. **Content Dictionary** (`Translation` section) — for use on the front end via `@Umbraco.GetDictionaryValue("Key")` in Razor.
2. **Backoffice UI localization** — for backoffice strings, consumed by `localize.string()` when a property label contains `#Key`.

The `#Key` prefix in a property label only reads from system 2. Add `TestTag` to the content Dictionary, put `#TestTag` in a property label, save — the label still shows literally `#TestTag`. Two completely different stores.

This package registers every content Dictionary item into `umbLocalizationManager` on backoffice startup, so `#Key` labels resolve as editors intuitively expect.

## Install

```bash
dotnet add package Esatto.Umbraco.Backoffice.DictionaryLocalization
```

Restart the site / hard-refresh the backoffice. It activates automatically — no configuration. The composer, Management API controller, and backoffice entry point are auto-discovered.

## How it works

On backoffice load, a small entry point calls a single authenticated Management API endpoint that returns the whole content Dictionary grouped by culture. It transforms the payload into `UmbLocalizationSetBase` objects (one per culture) and calls `umbLocalizationManager.registerManyLocalizations(sets)`. From then on, `localize.string("#Key")` and `localize.term("Key")` both find your dictionary value in the current backoffice user's culture — with the manager's built-in region → language → `en` fallback.

The server endpoint reads from an in-memory cache of the whole dictionary, built once via a single bulk `IDictionaryItemService.GetDescendantsAsync(null)` call. The cache invalidates automatically on `DictionaryItemSavedNotification` / `DictionaryItemDeletedNotification`, so edits show up after a browser reload.

**Key naming.** The backoffice's `#`-token regex is `/#\w+/g`, which stops at `.` and `-`. To keep both existing dictionary usage and new backoffice labels working, each dictionary key is registered under **two** aliases:

- The original alias (e.g. `ContactForm.Company`) — so front-end / programmatic lookups round-trip.
- An underscore-normalized alias (e.g. `ContactForm_Company`) — so `#ContactForm_Company` in a property label captures cleanly.

Both point to the same string. Existing `@Umbraco.GetDictionaryValue("ContactForm.Company")` on the front end is untouched.

## Endpoint

- `GET /umbraco/management/api/v1/backoffice/dictionary-localization/all`
- **Authenticated** as a backoffice user (`AuthorizationPolicies.BackOfficeAccess`); called from inside the backoffice with the bearer token attached automatically.
- Returns `{ cultures: { "sv-se": { "Key": "Value", ... }, "en": { ... } } }` — culture ISO codes lowercased to match `umbLocalizationManager`'s normalization.

## Compatibility

| Umbraco | Status |
|---------|--------|
| 17.x    | Verified |

The `umbLocalizationManager` API and Management API auth model are specific to the Umbraco 17 (Bellissima) backoffice; earlier majors are not supported.

## Trade-offs

- One HTTP round-trip on backoffice startup (small payload — a few tens of KB for typical dictionaries).
- Doubles the localization map size by registering underscore variants for dotted / hyphenated keys. Both variants point to the same string, so no per-lookup cost.
- Live updates require a browser reload. The server cache invalidates immediately; the client re-registers on next backoffice load. If you need real-time updates on save, patch the entry point to listen for `UmbRequestReloadStructureForEntityEvent` on `dictionary-item`.

## License

MIT.
