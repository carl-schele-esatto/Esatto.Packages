# Esatto.Umbraco.Backoffice.CookieBanner

Turnkey GDPR/ePrivacy cookie consent for Umbraco 17 & 18. A blocking consent dialog, per-category gating of scripts and embeds, Google Consent Mode v2 signalling, an editor-managed cookie registry, and a rendered cookie policy page — from two lines of Razor and one line of `Program.cs`.

- Nothing that needs consent reaches the browser before a choice is made — gated `<script>` tags are suppressed **server-side**, so there is no window in which they could execute
- Four fixed categories: `necessary`, `preferences`, `statistics`, `marketing` — they map 1:1 onto Consent Mode v2 signals and onto the cookie wire format
- Consent Mode v2 `default` → `update` → `config`, emitted inline in `<head>` before any Google tag loads, and only when a measurement id is configured
- Editors maintain the cookie declarations themselves in a Block List, and the policy page renders them grouped by category
- Consent copy lives in Umbraco dictionary items, so legal wording changes without a deploy; the package ships `en` and `sv` fallbacks as embedded resources
- `PolicyVersion` re-prompts every visitor when the wording or the cookie set changes
- No SQL table, no migration, no `MapControllers()`, no `AddRateLimiter` — the endpoint and its throttle are package-owned

## How it works

On a first visit the package renders a native `<dialog>` — placed first in `<body>` per the install instructions below — offering **Accept all**, **Reject all** and **Customise**. Because it is a single native `<dialog>`, focus trap and the inert backdrop come from the platform. Escape-suppression until a decision exists does not: browsers deliberately ignore `event.preventDefault()` on a `<dialog>` opened without prior user interaction — exactly the case here, since it opens on page load — so `consent.js` layers a second mechanism (reopening on `close`) on top of the platform's own `cancel` event to make it hold anyway.

**Customise** reveals the per-category choice: the four categories, each with its own declared cookies collapsed under a details disclosure, `necessary` checked and disabled — it is implied, never client-supplied, and never written to the cookie.

The decision is posted to a package-owned endpoint, which writes the cookie server-side — that is what guarantees the attributes are right (`SameSite=Lax`, `Secure` tracking the actual scheme, lifetime from configuration).

Editors declare the site's cookies in a Block List on the installed cookie policy page — one `cookieDefinition` block per cookie, each with a name, provider, category, purpose, duration and storage type. The policy page renders those declarations grouped by category, plus the visitor's current per-category choice, a button to reopen the dialog, and (once a decision exists) a button to withdraw consent.

## Install

```bash
dotnet add package Esatto.Umbraco.Backoffice.CookieBanner
```

Installing the package registers the services and installs the schema on first start via its composer — nothing else to configure. Wiring the rendering is the five lines below: one in `Program.cs`, two in your layout, and two registering the tag helpers.

In `Program.cs`, after `BootUmbracoAsync()` and **before** `UseUmbraco()`:

```csharp
app.UseCookieConsent();
```

In your layout — `<consent-head />` goes in `<head>` **after** your own stylesheet so your token overrides win, and `<consent-banner />` goes first in `<body>`, before `<header>`, so the dialog is reachable in DOM tab order:

```cshtml
<consent-head />
<consent-banner />
```

`_ViewImports.cshtml` needs the tag helpers registered once:

```cshtml
@using Esatto.Umbraco.Backoffice.CookieBanner.TagHelpers
@addTagHelper *, Esatto.Umbraco.Backoffice.CookieBanner
```

`builder.CreateUmbracoBuilder().AddCookieConsent()` and `builder.Services.AddCookieConsent()` are also available if you prefer registering explicitly; both are idempotent and both are already done for you by the composer.

## Configuration

Bound from the `Esatto:CookieBanner` section. Every value has a package-neutral default, so an empty section is a working configuration.

| Option | Type | Default | Notes |
|---|---|---|---|
| `PolicyVersion` | `int` | `1` | Bumping it re-prompts every visitor whose cookie carries an older version |
| `CookieName` | `string` | `cookie-consent` | Change it only on a fresh site — renaming it re-prompts everyone |
| `CookieLifetimeDays` | `int` | `365` | Cookie expiry |
| `GoogleMeasurementId` | `string?` | `null` | Non-null switches on the Consent Mode block in `<consent-head />`; the gtag.js library itself still only loads once `statistics` is granted |
| `PolicyPageKey` | `Guid?` | `null` | Optional override; by default the first published `cookiePolicy` node is used |
| `EndpointPath` | `string` | `/api/cookie-consent` | Where `UseCookieConsent()` maps the decision endpoint |
| `ThrottleRequestsPerMinute` | `int` | `10` | Per-IP sliding window on that endpoint; the excess gets HTTP 429 |

```json
{
  "Esatto": {
    "CookieBanner": {
      "PolicyVersion": 1,
      "CookieName": "cookie-consent",
      "CookieLifetimeDays": 365,
      "GoogleMeasurementId": "G-XXXXXXXXXX",
      "EndpointPath": "/api/cookie-consent",
      "ThrottleRequestsPerMinute": 10
    }
  }
}
```

## Tag helpers

| Tag | Attributes | What it does |
|---|---|---|
| `<consent-head />` | — | Links `/esatto-cookiebanner/consent.css`, then — only when `GoogleMeasurementId` is set — emits the inline Consent Mode `default` + `update` + `config` block |
| `<consent-banner />` | — | Renders the consent dialog and `/esatto-cookiebanner/consent.js` with its configuration data attributes |
| `<consent-script>` | `category`, `src`, `async` | Emits a `<script>` **only** when the category is granted; otherwise the element never reaches the browser at all |
| `<consent-embed />` | `category`, `src`, `title` | Renders the `<iframe>` when granted; otherwise a placeholder inviting the visitor to grant that category. The placeholder never contains the embed URL in any form, not even in a data attribute |

The same server-side gate applies to Google's own tag: the `gtag.js` `<script src="https://www.googletagmanager.com/gtag/js?...">` that `<consent-head />` can emit is itself conditioned on `statistics` being granted, exactly like `<consent-script category="Statistics">` — with statistics declined, the library never reaches the browser and cannot execute.

`category` binds a `ConsentCategory` member, so the value must match the PascalCase member name exactly — `category="Statistics"`, not `category="statistics"`. A lowercase value fails at compile time with CS0117.

```cshtml
<consent-script category="Statistics" async src="https://example.com/analytics.js"></consent-script>

<consent-embed category="Marketing"
               src="https://www.youtube-nocookie.com/embed/dQw4w9WgXcQ"
               title="Product tour" />
```

## Endpoint

`UseCookieConsent()` maps one minimal-API route at `EndpointPath` (default `/api/cookie-consent`):

```
POST /api/cookie-consent
```

```json
{ "action": "custom", "categories": ["statistics", "preferences"] }
```

`action` is one of `accept-all`, `reject-all`, `custom`, `withdrawn`; anything else is a `400`. For `accept-all` and `custom`, `categories` is the exact set to grant — the server grants every name in it that parses and is not `necessary`, and discards the rest, so a client sending `accept-all` must send the full list explicitly. For `reject-all` and `withdrawn`, `categories` is ignored outright and nothing is granted, whatever the client sent — the server does not trust the client to mean "grant nothing" by omission. Nothing about the request itself is logged; only the resulting decision — the granted category set, policy version, timestamp and a consent id — is written to the cookie below.

```json
{ "version": 1, "categories": ["preferences","statistics"], "consentId": "…", "decidedAt": "…" }
```

`429` once a client IP exceeds `ThrottleRequestsPerMinute` inside a rolling minute. No `AddRateLimiter`, no `UseRateLimiter` placement and no `MapControllers()` are required.

The response also sets the consent cookie: `Path=/`, `SameSite=Lax`, `HttpOnly=false` (`consent.js` reads it via `document.cookie` on every page load, not only right after a decision), `Secure` when the request is HTTPS, `IsEssential=true`. Its value is compact JSON, URL-encoded once:

```
{"v":1,"t":"2026-08-23T09:41:02.1234567+00:00","c":["preferences","statistics"],"id":"…"}
```

Outside `/umbraco`, a middleware adds `Vary: Cookie` and `Cache-Control: private, no-cache` to `text/html` responses, so a shared cache never serves one visitor's gating decision — including a third-party tag baked into the markup — to another.

## What gets installed into Umbraco

On the first start at `RuntimeLevel.Run`, a notification handler installs six schema artefacts under the package's own GUID namespace. Install is create-if-missing throughout; failures are logged and swallowed rather than blocking boot.

| Artefact | Alias | Kind |
|---|---|---|
| Cookie category | — | Dropdown, single select: `necessary`, `preferences`, `statistics`, `marketing` |
| Storage type | — | Dropdown, single select: `Cookie`, `localStorage`, `sessionStorage`, `Pixel` |
| Cookie definition | `cookieDefinition` | Element type: `cookieName`, `provider`, `category`, `purpose`, `duration`, `storageType` |
| Cookie registry | — | Block List allowing only `cookieDefinition` |
| Cookie policy | `cookiePolicy` | Document type: `heading`, `introduction`, `cookies`, `outro` |
| Cookie policy | `cookiePolicy` | Template, compiled into the package assembly |

A cookie policy page is then seeded with three necessary declarations that are generic to every Umbraco site: the consent cookie itself (named from `CookieName`), the antiforgery cookie, and `UMB_MEMBER`. Add the rest of your site's cookies as blocks on that page.

It also seeds **33 dictionary items** under a `Cookie.Banner` parent, all prefixed `Cookies.`:

`Cookies.Banner.Heading`, `.Body`, `.AcceptAll`, `.RejectAll`, `.Customise`, `.Save`, `.Cancel`, `.Error`, `.RateLimited`; `Cookies.Category.{Necessary,Preferences,Statistics,Marketing}.{Name,Description}`, `Cookies.Category.Cookies`; `Cookies.Embed.Blocked.Body`, `.Button`; `Cookies.Policy.Heading`, `.CurrentChoice`, `.NoChoice`, `.Reopen`, `.Withdraw`, `.On`, `.Off`; `Cookies.Footer.Link`; `Cookies.Table.{Name,Provider,Purpose,Duration,Type}`.

The seeder is **culture-agnostic**: it seeds for whatever languages your site already has, for any culture the package ships text for (`en` and `sv`). It never creates, requires or deletes a language, and never aborts. Text resolution is dictionary item → embedded resx for the request culture → English, so every string is editable in the backoffice and none of them is missing before you get there.

The package does **not** touch document types it does not own. The policy page is located by document type — the first published `cookiePolicy` node, or `PolicyPageKey` if you set it — so there is no content picker to wire up on your own settings node, and 1.0.0 ships no backoffice dashboard.

`Cookies.Footer.Link` and the `.consent-btn--link` CSS class exist for a footer link you add yourself, since the package cannot know where your footer lives:

```cshtml
<a href="#" class="consent-btn consent-btn--link" data-consent-open>@Umbraco.GetDictionaryValue("Cookies.Footer.Link")</a>
```

## Theming

`consent.css` is self-sufficient: it declares its own `--consent-*` tokens on `:root` with neutral defaults and ships its own `.consent-btn` / `.consent-btn--primary` / `.consent-btn--secondary` / `.consent-btn--link` classes. It depends on no class from your design system, and it deliberately styles nothing outside the dialog, the embed placeholder and the policy tables — no global `footer`, `a` or `button` rules.

Re-theme by overriding the tokens after `<consent-head />`:

| Token | Default | Used for |
|---|---|---|
| `--consent-surface` | `#ffffff` | dialog and table surface |
| `--consent-surface-subtle` | `#f4f5f7` | blocked-embed background, per-category cookie fact cards |
| `--consent-text` | `#1f2328` | body copy |
| `--consent-heading` | `#10141a` | headings, legends, table headers |
| `--consent-muted` | `#5f6570` | cookie-fact labels |
| `--consent-border` | `#d5d7db` | dialog/category borders, table row rules |
| `--consent-border-strong` | `#a8aeb8` | the blocked-embed placeholder's dashed border |
| `--consent-rule` | `#c9cdd4` | the 2px emphasis rule under headings and table headers |
| `--consent-backdrop` | `rgba(16, 20, 26, 0.6)` | the dialog's `::backdrop` scrim |
| `--consent-focus` | `#10141a` | the visible focus ring on every interactive control |
| `--consent-radius` | `8px` | the dialog's corner radius |
| `--consent-radius-md` | `6px` | category fieldset, blocked-embed placeholder and iframe corners |
| `--consent-radius-sm` | `4px` | button and cookie-fact card corners |
| `--consent-btn-primary-bg` | `#10141a` | `.consent-btn--primary` fill |
| `--consent-btn-primary-fg` | `#ffffff` | `.consent-btn--primary` text |
| `--consent-btn-primary-border` | `#10141a` | `.consent-btn--primary` border |
| `--consent-btn-secondary-bg` | `#ffffff` | `.consent-btn--secondary` fill |
| `--consent-btn-secondary-fg` | `#10141a` | `.consent-btn--secondary` text |
| `--consent-btn-secondary-border` | `#a8aeb8` | `.consent-btn--secondary` border |
| `--consent-btn-link-fg` | `#10141a` | `.consent-btn--link` text |

```css
:root {
    --consent-heading: #001f54;
    --consent-btn-primary-bg: #001f54;
    --consent-btn-primary-border: #001f54;
    --consent-backdrop: rgba(0, 31, 84, 0.6);
}
```

Fonts need no work — the stylesheet uses `font-family: inherit` throughout.

## JavaScript API

```js
window.cookieConsent.open();               // open the dialog (e.g. from a footer link)
window.cookieConsent.close();              // close it, if a decision already exists
window.cookieConsent.get();                // the decoded cookie, or null when undecided
window.cookieConsent.has('statistics');    // boolean, by wire name
window.cookieConsent.onChange(function (detail) { /* { categories, version } */ });
```

The same payload is dispatched on `document` as a DOM event, for code that would rather not depend on load order:

```js
document.addEventListener('cookieconsent:change', function (event) {
    console.log(event.detail.categories, event.detail.version);
});
```

Declarative hooks, no JavaScript required: `data-consent-open` on any element opens the dialog; `data-consent-close` closes it; `data-consent-customise` reveals the per-category section; `data-consent-action="accept-all"`, `"reject-all"`, `"custom"` or `"withdrawn"` records that decision and, on success, reloads the page — `<consent-script>` and `<consent-embed>` gate their output server-side, so a reload is what actually activates whatever the visitor just granted on the page they granted it on, rather than leaving it inert until the next navigation.

Category names in the JS API are the lowercase **wire** names (`necessary`, `preferences`, `statistics`, `marketing`) — the same strings that appear in the cookie. They are a stable contract: renaming a C# enum member must never invalidate cookies already in the wild.

## Compatibility

One `net10.0` assembly on the `Umbraco.Cms.Core` 17.0.0 floor with no upper bound. Umbraco 17 and 18 both ship only `lib/net10.0`, so there is no TFM to discriminate on and multi-targeting is not possible.

| Umbraco | Status |
|---------|--------|
| 17.x    | Targeted (package floor) |
| 18.x    | Targeted, not booted |

No Umbraco site has actually been booted with this package installed on either major during development — the checks below are static: real assemblies for both majors decompiled/reflected over to confirm the API surface this package calls exists and resolves to the same declaring type on both, plus a full test suite against mocked Umbraco services. That is not the same as an end-to-end run. **Verify on your own major before shipping to production**, especially after upgrading Umbraco — a mismatch here throws at runtime (`MissingMethodException`), not at compile time.

Nothing removed in Umbraco 18 is used: no `MigrationBase`/`PackageMigrationBase`, no `ILocalizationService` or `IFileService`, no `UmbracoApiController` or convention-based front-end API routing, no `IPublishedContent.Parent`/`.Children` properties. `GetById(Guid)` is never called on `IContentService`: 17.0.0 re-declares it there with `new` and 18 does not, so a 17.0.0-compiled call binds to a declaration site that vanishes at runtime on 18 (`MissingMethodException`, reproduced against real 17.0.0 and 18.1.1 binaries during development). Existence checks use `IEntityService.Exists(Guid, UmbracoObjectTypes)`, which is identical in both.

Each future major needs a re-verification pass rather than a presumption of forward compatibility — Umbraco's announced service-layer refactoring spans majors 18–21.

## License

MIT.
