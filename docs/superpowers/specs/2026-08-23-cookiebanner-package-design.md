# Esatto.Umbraco.Backoffice.CookieBanner — design

**Date:** 2026-08-23
**Status:** approved design, not yet implemented
**Source:** extraction of the cookie consent feature from `c:\src\NDSTK` (branch `feature/cookie-consent`)

## Purpose

A turnkey GDPR/ePrivacy cookie consent package for Umbraco 17 and 18: a blocking consent
dialog, per-category gating of scripts and embeds, Google Consent Mode v2 signalling, an
editor-managed cookie registry, and a rendered cookie policy page.

Today this lives in the NDSTK site, tightly coupled to NDSTK's stylesheet, its Swedish
language set, and its site content model. This design extracts the reusable feature and
leaves NDSTK a consumer of it.

A companion cookie **scanner** (headless-browser discovery of cookies the site actually
sets) is deliberately out of scope here — it ships as a separate opt-in package because it
requires Chromium (~281 MB) which cannot run on Azure App Service Windows plans. See
"Deferred" below.

## Non-goals

- The cookie scanner (separate package, separate spec).
- A server-side consent log. The current code contains scaffolding for one
  (`ConsentAction`, `ConsentRequest.Culture`, a specced-but-unbuilt `ndstkConsentLog`
  table). That scaffolding is **dropped** rather than shipped as an unkept promise; the log
  can be a later feature.
- Multi-targeting. See "Umbraco 17 + 18" — it is not merely awkward but impossible.
- Configurable consent categories. The four (`necessary`, `preferences`, `statistics`,
  `marketing`) stay fixed; they map onto Consent Mode v2 signals and onto the wire format.

## Package identity

| Property | Value |
|---|---|
| PackageId / folder / RootNamespace / AssemblyName | `Esatto.Umbraco.Backoffice.CookieBanner` |
| TargetFramework | `net10.0` (single) |
| Umbraco floor | `Umbraco.Cms.Core` / `.Infrastructure` / `.Web.Common` `17.0.0`, no upper bound |
| SDK | `Microsoft.NET.Sdk.Razor`, `StaticWebAssetBasePath=/` |
| Distribution | nuget.org, born public, `umbraco-marketplace` tag |
| Versioning | MinVer, tag `Esatto.Umbraco.Backoffice.CookieBanner-1.0.0` |

The `Backoffice.` infix is kept for consistency with every sibling package even though the
feature is primarily visitor-facing; it does install backoffice content types.

`Umbraco.Cms.Api.Management` is referenced **only if** a backoffice dashboard ships. The
1.0.0 scope has no dashboard, so the reference is omitted — but `Umbraco.Cms.Core` is
present regardless, which is what the Marketplace needs to detect a supported version.

## Architecture

### 1. Boundary

**The package owns:**

- The consent core (18 files, from `NDSTK/Consent/`): cookie codec, decision model, request
  scoped state, cookie writer, Consent Mode v2 script builder, category model, options, and
  the `<consent-script>` / `<consent-embed>` tag helpers.
- Presentation: the consent dialog partial, `consent.js`, `consent.css`, and the cookie
  policy Razor template.
- Six schema artefacts under a **fresh GUID namespace**: the cookie-category dropdown, the
  storage-type dropdown, the `cookieDefinition` element type, the `cookieRegistry` block
  list, and the `cookiePolicy` document type plus its template.
- 33 dictionary items under a `Cookie.Banner` parent, seeded culture-agnostically.
- The wiring surface: `AddCookieConsent()`, `UseCookieConsent()`, `<consent-head />`,
  `<consent-banner />`.

**The site keeps:**

- `NdstkLanguageInstaller` — it forces `sv` into existence and **deletes `en-US`**. A
  package must never manage a site's language set.
- Its own content model, its own seeded Swedish policy-page copy (now pointing at
  package-owned document types), and its `site.css` palette.

**No SQL migration.** Unlike `Esatto.Umbraco.Backoffice.Redirects`, everything here is
Umbraco content types installed through services at startup. There is no NPoco table, no
`MigrationPlan`, no `Upgrader`. (Any future migration must use `AsyncMigrationBase` /
`AsyncPackageMigrationBase` — the non-async bases were removed in Umbraco 18.)

### 2. Install and content model

A package-owned `IComposer` registers an `INotificationAsyncHandler<UmbracoApplicationStartedNotification>`
that runs the installer, gated on `RuntimeLevel.Run`, logging and swallowing failures — the
same shape as `NdstkContentModelInstallHandler` and `RunRedirectsMigration`.

`NdstkContentTypeFactory` (210 generic lines) is **copied into the package, not shared**. It
carries a mutable `_dataTypes` cache populated by `PreloadDataTypesAsync` whose `Property()`
throws on a non-preloaded key; sharing one singleton instance across two independent
installers makes that cache a coupling hazard. Duplication is cheaper than a third shared
assembly.

Installation order is load-bearing and must be preserved: the two dropdowns are created and
preloaded **before** the `cookieDefinition` element type binds to them, and `cookieRegistry`
is created **after** element types exist.

The package seeds a cookie policy page containing three pre-declared necessary cookies that
are generic to every Umbraco site: its own consent cookie (name read from options), the
antiforgery cookie, and `UMB_MEMBER`.

### 3. Policy-page resolution — replaces a cross-model schema write

Today the installer adds a `cookiePolicyPage` Content Picker to **the site's** `settings`
document type, and the banner and footer read it. A package cannot add properties to a
document type it does not own; that single write is the entire reason
`NDSTK/docs/UPGRADING-consent.md` exists.

Replaced by: **resolve the first published node of document type `cookiePolicy`**, with an
optional `PolicyPageKey` option to disambiguate or override. This removes the manual
backoffice step altogether.

### 4. Wiring — 13 touch points collapse to 2

Current integration cost is roughly 13 manual edits across `Program.cs`, `Root.cshtml` and
`_ViewImports.cshtml`, plus 4 backoffice steps. Target:

```cshtml
@* in <head>, after the site stylesheet *@
<consent-head />

@* first element in <body>, before <header>, for DOM-order tab reach *@
<consent-banner />
```

```csharp
app.UseCookieConsent();   // after BootUmbracoAsync()
```

`_ViewImports.cshtml` keeps its two lines (`@using` + `@addTagHelper`).

`<consent-head />` encapsulates the Consent Mode `Defaults() + Update() + Config()` block
and the gated gtag script. This matters: the double `Update()` call is deliberate — it
closes the 500 ms `wait_for_update` window — and a comment in `Root.cshtml` currently begs
the reader not to delete it as a duplicate. Moving it inside a tag helper makes it
package-internal instead of copy-paste that invites deletion.

### 5. Endpoint and throttling — package-owned

The consent endpoint currently relies on ASP.NET Core rate limiting, which forces the
consumer to place `UseRateLimiter()` *between* `UseUmbraco().WithMiddleware(...)` and
`.WithEndpoints(...)`. Anyone copying a conventional Umbraco `Program.cs` gets this wrong,
and `[EnableRateLimiting]` throws at request time when the named policy is absent.

Instead:

- The endpoint is registered as a minimal-API `MapPost` inside `UseCookieConsent()`. No
  `MapControllers()`, no attribute-routing discovery. (Note `UmbracoApiController` and
  convention-based front-end API routing were both removed in Umbraco 18, so a
  package-registered endpoint is the forward-compatible shape anyway.)
- Throttling is a package-owned in-memory sliding window keyed by remote IP in a singleton,
  preserving the current contract: 10 requests/minute, HTTP 429 on rejection.
- The request-handling logic moves into a handler class so the existing controller tests
  retarget it directly rather than being discarded.

Net effect: no `AddRateLimiter`, no `UseRateLimiter` placement, no `MapControllers()` — the
package is genuinely zero-config beyond one `UseCookieConsent()` line.

### 6. Theming

`consent.css` today defines **zero** custom properties. It consumes five (`--primary`,
`--accent`, `--bg`, `--text`, `--muted`) that live in NDSTK's `site.css`, and the dialog
buttons use `.btn-primary` — also defined only in `site.css`. Installed anywhere else, the
package would render five unstyled default browser buttons.

Changes:

- A `--consent-*` prefixed token layer with neutral defaults declared on `:root`, so the
  package is self-sufficient and a consumer re-themes by overriding tokens. Generic names
  like `--primary` are not used; they are too likely to already mean something else in a
  consumer's design system.
- Self-contained `.consent-btn` / `.consent-btn--primary` / `.consent-btn--secondary` /
  `.consent-btn--link` classes. No dependency on host classes.
- Tokenise the hardcoded `rgba(0, 31, 84, 0.6)` `::backdrop` (currently bakes in NDSTK navy,
  so overriding `--primary` still yields a navy scrim) and the `#d5d7db` borders.
- Prefix the `#consent-dialog` / `#consent-dialog-heading` IDs to avoid host collisions.
- **Delete** the three rules at `consent.css:231-241` that style `footer a`,
  `footer .btn-link` and `footer p`. They exist only because NDSTK's footer is dark. A
  package that restyles a host's footer globally is hostile.

Fonts need no work — the stylesheet already uses `font-family: inherit` throughout.

### 7. Localization

Umbraco dictionary items remain the **editable source of truth**: consent copy is exactly
the text that changes for legal reasons, and `PolicyVersion` exists so wording changes can
re-prompt. Editors must be able to reword it without a deploy.

Changes:

- Fallbacks move from inline Swedish literals to **embedded resx per culture**. There are
  currently 26 Swedish literals in `.cshtml` fallbacks and 2 in `consent.js`; a package
  shipping Swedish string literals is indefensible.
- The seeder becomes culture-agnostic: it seeds items for whatever languages the site
  actually has, for any culture the package ships text for. It never requires a language,
  never deletes one, and never hard-aborts. Current behaviour bails out entirely when `sv`
  is missing — and `sv` only exists because `NdstkLanguageInstaller` forces it in.
- Package ships `en` and `sv`.
- Resolution order: dictionary item → resx for the request culture → English.
- `ConsentEmbedTagHelper` uses the `ICultureDictionary` **indexer**, which has no fallback
  at all; it moves to the same lookup-with-fallback path as everything else.
- Three keys that are seeded but never read are dropped: `Cookies.Banner.PolicyLink`,
  `Cookies.Banner.Label`, `Cookies.Settings.Heading`.

**Bug fixed in passing:** `CookiePolicy.cshtml:45` renders
`<strong>@(isOn ? "på" : "av")</strong>` with no dictionary key, so "on"/"off" on the policy
page is Swedish in every language including English. It gets a real key.

### 8. Umbraco 17 + 18 compatibility

**Multi-targeting is impossible.** Umbraco 17 and 18 both ship only `lib/net10.0`, so there
is no TFM to discriminate on, and NuGet's accepted TFM-as-aliases spec states that pack
cannot emit two variants of one canonical TFM with different dependency lists. The historic
Umbraco multi-targeting recipe worked only because each major moved .NET versions; that
lever is gone.

Strategy: **single `net10.0` assembly on the `17.0.0` floor with no upper bound** — the
existing house pattern, already proven on 18 across six shipping packages, and stated as
policy in `2026-06-22-contenttreednd-2.0-design.md`: *"verify, don't rewrite. One codebase
serves both 17 and 18."*

Verified compatible in both majors: the data-type, content-type, template, dictionary,
language and key-value services; `IComposer` / `IUmbracoBuilder`; the notification handler;
`ICultureDictionaryFactory`; `IShortStringHelper`; `BlockListModel`; `IPublishedContent`;
`PublishedContentExtensions.Root()` / `.ChildrenOfType()`; and the Razor, tag-helper and
`IHttpContextAccessor` surface.

**One undocumented break must be handled.** Umbraco 18 re-based `IContentService` onto a new
18-only `IPublishableContentService<T>` and removed the `GetById(Guid)` overload from
`IContentService` itself; it survives only via the inherited `IContentServiceBase<T>`. This
appears in neither the 18 breaking-changes page nor the 18.0.0 release notes. NDSTK hits it
once, at `NdstkContentSeeder.cs:142`. It is harmless in NDSTK today (which compiles against
18.1.1) but is a latent runtime `MissingMethodException` for a package compiled against the
17.0.0 floor and run on 18 — precisely the direction this strategy depends on.

**Correction (2026-08-23, during plan drafting):** the obvious fix — casting to
`IContentServiceBase<IContent>` — does **not** work on the 17.0.0 floor. Verified against the
shipped XML docs of `Umbraco.Cms.Core`:

| API | 17.0.0 | 18.1.1 |
|---|---|---|
| `IContentService.GetById(Guid)` | declared **directly** here, via a `new` re-declaration hiding the inherited one | **not declared directly** — reachable only by inheritance |
| `IContentServiceBase<T>.GetById(Guid)` | present (undocumented in the shipped XML, which is what misled the first check) | present |
| `IContentService.GetById(int)` | present | present |
| `IEntityService.Exists(Guid, UmbracoObjectTypes)` | present | present |

**Second correction (2026-08-23, during Task 16 review).** Two claims in the table above were
originally wrong — that `IContentServiceBase<T>.GetById(Guid)` was absent from 17.0.0 until
17.3.0, and that `IContentService.GetById(Guid)` was removed in 18. Both came from reading the
shipped `.xml` documentation files, which list only *documented* members. That is precisely the
method the compatibility research had warned against, noting that absence from those files is not
evidence of absence from the assembly. Decompiling the real 17.0.0, 17.3.0 and 18.1.1 assemblies
settles it as the corrected table shows.

**The hazard is real, and has now been reproduced.** It is a *binary* break, not a compile-time
absence. Because 17.0.0 re-declares `GetById(Guid)` on `IContentService` with `new`, a
compiler targeting 17.0.0 binds the call to `IContentService::GetById(Guid)` specifically — the
most-derived declaring interface. Umbraco 18 drops that re-declaration, so the token no longer
resolves at runtime. A library compiled against 17.0.0 and loaded into a host referencing 18.1.1
throws:

```
System.MissingMethodException: Method not found:
  Umbraco.Cms.Core.Models.IContent Umbraco.Cms.Core.Services.IContentService.GetById(System.Guid)
```

Each major compiles cleanly on its own, which is exactly what makes this invisible without a
cross-version test. A control repro through `IEntityService.Exists(Guid, UmbracoObjectTypes)`,
declared directly and identically on both, throws nothing.

So the operational rule stands, now evidenced rather than asserted: reach a content node by key
through `IEntityService` — `Exists(Guid, UmbracoObjectTypes)` for an existence check, or
`GetId(Guid, UmbracoObjectTypes)` then `IContentService.GetById(int)` to fetch. The seeder
needs only existence, so it uses `Exists`.

Raising the floor to 17.3.0 was considered as an alternative and rejected — but note that on the
corrected facts it would not have helped anyway, since the break is about where the member is
*declared*, not which versions contain it. `IEntityService` costs nothing and keeps 17.0–17.2.

**Practice:** build against `17.0.0`, but lint against `17.4.2`, whose assemblies carry
~60 more "Scheduled for removal in Umbraco 18" markers and are a far better oracle for what
the next major deletes.

**Shelf life:** Umbraco has announced a service-layer refactoring spanning majors 18–21
(GUID identifiers, async everywhere, `Attempt` for writes). The `IContentService` split is
its first, silent instalment. Each new major needs a re-verification pass rather than a
presumption of forward compatibility.

Backoffice manifest format is unchanged 17→18 for dashboards, so a future dashboard is safe.

### 9. Configuration

A new options class bound from a package-owned config section, with package-neutral
defaults:

| Option | Default | Notes |
|---|---|---|
| `PolicyVersion` | `1` | Bumping re-prompts every visitor |
| `CookieName` | package-neutral (not `ndstk-consent`) | |
| `CookieLifetimeDays` | `365` | |
| `GoogleMeasurementId` | `null` | Non-null switches on the whole Consent Mode head block |
| `PolicyPageKey` | `null` | Optional override for policy-page resolution |

This is the **first** package in the mono-repo to use `IOptions` — a repo-wide grep for
`IOptions` / `GetSection` / `Configure<` currently returns zero hits, and one earlier design
doc lists appsettings knobs as a non-goal. The section-name convention is therefore
established here.

`ConsentState` and the vary-by-cookie middleware are currently `internal`, yet NDSTK's
`Program.cs` names the middleware directly (which compiles only because they are the same
assembly). They either go public or hide entirely behind `UseCookieConsent()`. Preference:
keep them internal with `InternalsVisibleTo` for the test project — consumers only need
`IConsentState` — following the precedent already set by `DictionaryFilterValues` and
`DictionaryLocalization`.

The public JS API is renamed off the `ndstk` prefix. Verified safe: `window.ndstkConsent`
and the `ndstk:consent-change` event have no consumers anywhere in NDSTK beyond their own
definition and design-doc mentions.

## Testing

Mono-repo conventions: sibling `Esatto.Umbraco.Backoffice.CookieBanner.Tests`, flat files,
`net10.0`, `IsPackable=false`, `IsPublishable=false`,
`FrameworkReference Microsoft.AspNetCore.App`, `Microsoft.NET.Test.Sdk 17.11.1`,
`xunit 2.9.2`, `xunit.runner.visualstudio 2.8.2`, `NSubstitute 5.3.0`. No coverage tooling,
no CI — `dotnet test <csproj>` run by hand with real output pasted into the task report.

NDSTK has **33 existing consent test methods** (~39 cases, 694 lines) across 6 classes. All
port; translation is mechanical (xunit v3 → v2: drop `OutputType=Exe` and the implicit
`<Using Include="Xunit" />`, add `using Xunit;` per file, and confirm the
`Assert.Single(collection, predicate)` overload resolves on 2.9.2).

The 6 cookie-attribute tests currently in `ConsentControllerTests` relocate down to a new
`ConsentCookieWriterTests`, leaving the endpoint tests to cover routing and response shape.

New coverage to add:

- The vary-by-cookie middleware — currently untested. `Vary: Cookie` and
  `Cache-Control: private, no-cache` on `text/html`, untouched for JSON, untouched under
  `/umbraco`, `next` always invoked. Note it writes headers inside `Response.OnStarting`, so
  the test must trigger that callback.
- `ConsentCategories` wire-name contract — round-trip all four, reject unknown/null/casing,
  throw on an undefined cast, and pin `All`/`Consentable` ordering. This protects the
  documented claim that renaming a member must not invalidate cookies already in the wild.
- `TryParseAction` for all four actions plus null and unknown.
- A **non-default cookie name honoured end-to-end** by writer and state. Nothing tests this
  today, and a package must not hardcode a site's cookie name.
- The extracted registry grouper (below).
- The package throttle: allows 10/minute, rejects the 11th with 429, per-IP isolation.

### Duplication to remove

The cookie-registry grouping logic is written **twice** — `_ConsentBanner.cshtml:19-43` and
`CookiePolicy.cshtml:13-20` — with comments in both insisting the two must agree. They do
not: the banner drops blocks with a blank `cookieName`, the policy page renders them. The
divergence is untested.

Extract a pure grouper over a `CookieDeclaration(Name, Provider, Category, Purpose,
Duration, StorageType)` record; the views keep only `BlockListItem` → record mapping. One
shared function, one tested behaviour.

## NDSTK migration path

1. Reference the package; pin `CookieName` to `ndstk-consent` via config so **no existing
   visitor is re-prompted**. Pin the config section likewise.
2. Delete ~120 cookie-related lines from `NdstkContentModelInstaller`, the cookie GUIDs from
   `NdstkKeys`, and `NdstkDictionaryInstaller` in full.
3. Leave `NdstkLanguageInstaller` untouched.
4. Replace the six `Root.cshtml` integration points with `<consent-head />` and
   `<consent-banner />`; replace the three `Program.cs` edits with `app.UseCookieConsent()`.
5. Map NDSTK's palette onto the new `--consent-*` tokens in `site.css`.
6. Keep NDSTK's Swedish policy-page seed content, now pointing at package-owned document
   types.
7. Drop the site's now-duplicated `cookiePolicyPage` property once resolution-by-alias is in
   place; retire `docs/UPGRADING-consent.md`.
8. Delete the ported tests from `NDSTK.Tests`.

Because the extracted code must now compile against the 17.0.0 floor rather than 18.1.1,
this step will surface obsolete warnings and nullability flips that NDSTK's 18-only build
currently hides. Budget for it.

## Deferred: the scanner

`Esatto.Umbraco.Backoffice.CookieBanner.Scanner`, separate opt-in package, separate spec.
Recorded here so the banner package's seams anticipate it.

Findings that shape it:

- **Playwright for .NET** (`Microsoft.Playwright`) drives headless Chromium; `CookiesAsync()`
  sees HttpOnly and third-party cookies (third-party cookies remain enabled by default after
  Google cancelled the deprecation in April 2025). Covers localStorage, sessionStorage and
  pixel detection via request events — matching all four `storageType` values.
- Chromium is ~281 MB and **cannot run on Azure App Service Windows plans** (the sandbox
  blocks the GDI/Win32k calls Chromium needs). Needs a glibc Linux container with
  `install --with-deps`. This is why it is a separate package.
- The valuable feature is the **two-state scan**, which is how Cookiebot and OneTrust earn
  their fees: crawl with no consent cookie (anything non-necessary found is a pre-consent
  violation), then crawl with consent granted to build the declaration, optionally crawl
  after reject-all to verify rejection works. The consent cookie is unsigned JSON, so the
  scanner can simulate any state by pre-setting it. Because gated `<consent-script>` tags
  are suppressed **server-side**, diffing requires re-fetching pages per consent state, not
  observing one DOM.
- Classification: bundle a pinned snapshot of the **Open Cookie Database** (Apache 2.0 — the
  only permissively licensed option; cookiedatabase.org is CC BY-NC-ND and its API is
  licensed solely for the Complianz plugin; Tracker Radar and Ghostery TrackerDB are NC;
  CookieBlock is GPLv3). ~2,266 entries whose columns map almost 1:1 onto `cookieDefinition`
  fields. Ship a `THIRD-PARTY-NOTICES` file. Fold dynamic names to prefix patterns (`_ga_*`)
  or reports never stabilise across runs.
- Scheduling: `IRecurringBackgroundJob` **implemented directly**, not via
  `RecurringBackgroundJobBase` — that base class and `ITriggerableRecurringBackgroundJob`
  are absent from every 17.x checked (17.0.0 through 17.4.2), so they are unavailable on a
  17.0.0 floor. For the "Scan now" button, `ILongRunningOperationService` (17.0+, DB-backed)
  gives cross-server status, dedupe and result persistence, and avoids the load-balancing
  hazard where a trigger only signals the server handling the request.
- Monthly is the industry-standard cadence (Cookiebot's default; daily is a paid add-on).

## Open items

- Whether to wire up a policy-page link in the banner or correct
  `NDSTK/docs/UPGRADING-consent.md`, which claims such a link exists. The banner currently
  renders none — it reads the policy page only to pull the registry block list.
- The exact config section name and the neutral `CookieName` default — decided together,
  since both establish naming precedent for the repo. NDSTK overrides `CookieName` back to
  `ndstk-consent` regardless, so the default only affects new consumers.
- Whether `consent.js` gets a `Client/` project with vitest, following the mono-repo's
  `*.logic.ts` + `*.logic.test.ts` convention, so the browser-side cookie reader — which
  mirrors `ConsentCookieCodec.Decode` and could drift from it — is covered. It is entirely
  untested today.
