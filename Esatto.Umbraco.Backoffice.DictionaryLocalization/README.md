# Esatto.Umbraco.Backoffice.DictionaryLocalization

Bridges Umbraco 17 & 18 **content Dictionary items** into the **backoffice UI localization system**. After install, any `#Key` in a content-type property label (or anywhere `localize.string()` is called) resolves to the current backoffice user's culture value from the Dictionary section — for every existing and future dictionary key, with no per-key manifest wiring.

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

**Dotted / hyphenated keys.** Umbraco's built-in `localize.string()` tokenizes with `/#\w+/g`, which stops at the first `.` or `-` — so out of the box `#SEO.MetaKeywords.Description` would only ever capture `#SEO`. On backoffice load this package replaces `UmbLocalizationController.prototype.string` with a resolver that captures the whole dotted/hyphenated run and resolves the **longest** matching key, keeping any unconsumed remainder as literal text. Unknown tokens are left untouched, exactly like Umbraco's own fallback. The result: **you write labels with your dictionary key verbatim** — `#SEO.MetaKeywords.Description` — no underscores, no renaming.

Each key is still also registered under an underscore-normalized alias (e.g. `SEO_MetaKeywords_Description`) for backward compatibility, and the original dotted alias round-trips for front-end use, so existing `@Umbraco.GetDictionaryValue("SEO.MetaKeywords.Description")` is untouched.

> The `string()` replacement is a strict superset of Umbraco's behaviour (flat keys and unknown tokens behave identically) and was verified against the byte-identical `string()`/`term()`/regex in both 17.3.0 and 18.0.0. A future major that changes that contract would need a revisit.

## Endpoint

- `GET /umbraco/management/api/v1/backoffice/dictionary-localization/all`
- **Authenticated** as a backoffice user (`AuthorizationPolicies.BackOfficeAccess`); called from inside the backoffice with the bearer token attached automatically.
- Returns `{ cultures: { "sv-se": { "Key": "Value", ... }, "en": { ... } } }` — culture ISO codes lowercased to match `umbLocalizationManager`'s normalization.

## Compatibility

| Umbraco | Status |
|---------|--------|
| 17.x    | Verified |
| 18.x    | Verified |

The `umbLocalizationManager` API and Management API auth model are specific to the Umbraco (Bellissima) backoffice; majors before 17 are not supported. A single build serves both 17 and 18: they both target `net10.0`, the backoffice localization contract (`localize.string()`, the `#\w+` token regex, `umbLocalizationManager.registerManyLocalizations`) is identical across them, and the shared `umbHttpClient` supplies the `credentials: 'include'` that 18's Management API requires.

## Trade-offs

- One HTTP round-trip on backoffice startup (small payload — a few tens of KB for typical dictionaries).
- Doubles the localization map size by registering underscore variants for dotted / hyphenated keys. Both variants point to the same string, so no per-lookup cost.
- Live updates require a browser reload. The server cache invalidates immediately; the client re-registers on next backoffice load. If you need real-time updates on save, patch the entry point to listen for `UmbRequestReloadStructureForEntityEvent` on `dictionary-item`.

## License

MIT.
