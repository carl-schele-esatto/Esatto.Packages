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

Installing the package registers the services and installs the schema on first start via its composer. Wiring the rendering takes six lines, in three files. Do all four steps — each one fails silently on its own.

### 1. Import the namespace in `Program.cs`

```csharp
using Esatto.Umbraco.Backoffice.CookieBanner;
```

The Umbraco template's implicit usings do not cover it. Without this you get `CS1061: 'WebApplication' does not contain a definition for 'UseCookieConsent'`.

### 2. Add one line to `Program.cs`, after `BootUmbracoAsync()` and before `UseUmbraco()`

```csharp
app.UseCookieConsent();
```

This maps the endpoint the dialog posts decisions to. Without it the dialog renders but Accept and Reject do nothing.

### 3. Register the tag helpers in `Views/_ViewImports.cshtml`

```cshtml
@using Esatto.Umbraco.Backoffice.CookieBanner.TagHelpers
@addTagHelper *, Esatto.Umbraco.Backoffice.CookieBanner
```

Without this, Razor emits `<consent-banner />` as literal markup and the browser ignores it — no banner, no error. If nothing appears at all, check View Source for that literal tag first.

### 4. Add two tags to your layout

```cshtml
<consent-head />     @* in <head>, after your own stylesheet *@
<consent-banner />   @* first in <body>, before <header> *@
```

`<consent-head />` goes after your own stylesheet so the package's rules are not outranked by broad
selectors from your design system. `<consent-banner />` goes first in `<body>` so the dialog is
reachable in DOM tab order.

That order has one consequence worth knowing before you re-theme: `consent.css` declares its tokens
on `:root`, so a `:root` block in a stylesheet linked *above* `<consent-head />` ties on specificity
and loses on load order. Token overrides belong in a stylesheet loaded after it — see
[Theming](#theming).

**Put step 4 in the layout that *every* front-end page uses, not just your home page.** `<consent-banner />` renders the dialog element and loads `consent.js`, and together those power every `data-consent-*` control on the site — including the reopen button on the installed cookie policy page and any footer cookie-settings link you add yourself. On a page whose layout omits the tag, those controls render, look correct, and do nothing, with no error anywhere. Once a visitor has decided the dialog renders hidden, so a missing tag looks identical to a working install until someone clicks one of them.

**The installed policy page takes its layout from your site.** The package writes a physical
`Views/CookiePolicy.cshtml` on first start and deliberately sets no `Layout` — it cannot know what
yours is called. That relies on your site having a `Views/_ViewStart.cshtml`, which is the ASP.NET
Core convention. If your views each assign `Layout` inline instead, add one:

```cshtml
@* Views/_ViewStart.cshtml *@
@{
    Layout = "YourLayout.cshtml";
}
```

Without a layout the policy page renders with no `<html>`, no stylesheet and no site chrome — and
because your layout is what carries `<consent-banner />`, no dialog either, which leaves the policy
page's own reopen button a dead control. Check first that adding `_ViewStart` cannot recurse: if a
document type renders your layout file directly as a view, it would then set itself as its own
layout. The alternative is to add the `Layout` line to the installed view after first start — the
installer looks its template up by key and will not overwrite your edit.

That is the whole setup. The schema, the dictionary items and the policy page appear on first start.

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

A `<button>` works just as well, and the class is entirely optional — the behaviour comes from the
`data-consent-open` attribute, not from the styling, so your own classes are fine:

```cshtml
<button type="button" class="btn-link" data-consent-open>@Umbraco.GetDictionaryValue("Cookies.Footer.Link")</button>
```

Reaching for your own class is the usual choice when the link sits in a footer your design system already
styles — the package's `.consent-btn--link` exists so you *can* stay consistent with the dialog, not because
anything depends on it.

**Whichever element you use, it needs `consent.js` on the page**, which means the layout rendering it must
also render `<consent-banner />`. Without that the control is inert and silent — see the note in
[Install](#install).

## Theming

`consent.css` is self-sufficient: it declares its own `--consent-*` tokens on `:root` with neutral defaults and ships its own `.consent-btn` / `.consent-btn--primary` / `.consent-btn--secondary` / `.consent-btn--link` classes. It depends on no class from your design system, and it deliberately styles nothing outside the dialog, the embed placeholder and the policy tables — no global `footer`, `a` or `button` rules.

Re-theme by redeclaring the tokens in a stylesheet loaded **after** `<consent-head />` — not before
it. Both blocks declare on `:root`, so specificity ties and load order decides which wins. An
override stylesheet that loads first is silently overwritten by the defaults below, and nothing
about the result looks broken: the dialog renders correctly in the package's neutral palette, which
is why this presents as "the tokens do not work" rather than as a mistake in the ordering.

If you must theme from a stylesheet that loads earlier, out-specify `:root` instead of reordering —
`html:root { … }` scores one element higher and wins regardless of position.

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
/* in a stylesheet linked after <consent-head /> */
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

## Troubleshooting

Every failure below is silent — nothing throws and nothing is logged — so they are listed by symptom.

**`CS1061: 'WebApplication' does not contain a definition for 'UseCookieConsent'`**
The namespace import is missing. Add `using Esatto.Umbraco.Backoffice.CookieBanner;` to `Program.cs`. The
extension is on `IApplicationBuilder`, which `WebApplication` implements, so nothing else is needed.

**No banner appears at all, on any page.**
View Source. If you can see a literal `<consent-banner />` in the HTML, the tag helpers are not registered —
add the two `@addTagHelper` / `@using` lines to `Views/_ViewImports.cshtml`. An unregistered tag helper is
emitted verbatim, and browsers silently ignore unknown elements.

**The banner appears but Accept/Reject do nothing.**
`app.UseCookieConsent()` is missing or is placed after `UseUmbraco()`. It registers the endpoint the dialog
posts to; without it the request 404s.

**A footer cookie-settings link, or the policy page's reopen button, does nothing.**
The layout it renders on does not render `<consent-banner />` (install step 4). That tag renders the dialog
element *and* loads `consent.js`; a reopen control needs both, since it has to have a dialog to open. Put
`<consent-banner />` in the layout every front-end page uses. This is the most common setup mistake, because
once a visitor has decided the dialog renders hidden — so on most pages a missing tag looks identical to a
working install.

The policy page's **withdraw** button is the exception: it posts a decision rather than opening the dialog, so
it works from any layout, because the policy page loads its own copy of `consent.js`.

**The dialog is styled, but my `--consent-*` overrides are ignored.**
The override stylesheet is linked before `<consent-head />`. Both declare the tokens on `:root`, so the
tie breaks on load order and the package's defaults win. Move the link below `<consent-head />`, or
out-specify with `html:root { … }`. Nothing appears broken in this state — the dialog simply stays in the
package's neutral palette.

**The cookie policy page has no styling or site chrome, and its "change settings" button does nothing.**
The installed `Views/CookiePolicy.cshtml` carries no `Layout` and your site has no
`Views/_ViewStart.cshtml` to supply one — see install step 4. With no layout there is no
`<consent-banner />` on that page, so there is no dialog for the button to open: `consent.js` returns
early on a missing dialog rather than erroring, which is why nothing appears in the console.

**The dialog appears unstyled.**
`consent.css` is not being served. Confirm `/esatto-cookiebanner/consent.css` returns 200. The package ships
it as a static web asset; if you have customised static-file handling, make sure static web assets are still
mapped.

**Nothing was installed into Umbraco — no document type, no dictionary items.**
The installer runs on `UmbracoApplicationStartedNotification`, gated on `RuntimeLevel.Run`, and it
deliberately logs and swallows failures rather than preventing your site from booting. Search your logs for
`CookieBannerInstallHandler`: you will find either a message saying the runtime level was not `Run` (the site
was installing or upgrading — restart once it is running), or the error that was swallowed.

**Consent was granted but a gated `<consent-script>` still does not load.**
Gating is server-side by design: a blocked script is never sent to the browser, so it can only appear on a
subsequent request. `consent.js` reloads the page after a successful decision for exactly this reason. If you
have suppressed that reload, the script will appear on the visitor's next navigation instead.

## Compatibility

One `net10.0` assembly on the `Umbraco.Cms.Core` 17.0.0 floor with no upper bound. Umbraco 17 and 18 both ship only `lib/net10.0`, so there is no TFM to discriminate on and multi-targeting is not possible.

| Umbraco | Status |
|---------|--------|
| 17.x    | Targeted (package floor) |
| 18.x    | Targeted |

**What "Targeted" means today:** the package has now been installed and exercised end to end on a
real Umbraco site — the schema installed cleanly, both dropdown data types (Cookie category,
Storage type) confirmed as working editors in the backoffice, the cookie policy page rendered, and
dictionary text resolved per culture. That install ran on one major, not both, and this README does
not say which. So neither row above means "Verified": whichever major that real install did *not*
run on still has only the static checks behind it — real assemblies for both majors
decompiled/reflected over to confirm the API surface this package calls exists and resolves to the
same declaring type on both, plus a full test suite against mocked Umbraco services — which is not
the same as an end-to-end run. **Verify on your own major before shipping to production**, especially
after upgrading Umbraco — a mismatch here throws at runtime (`MissingMethodException`), not at
compile time.

Nothing removed in Umbraco 18 is used: no `MigrationBase`/`PackageMigrationBase`, no `ILocalizationService` or `IFileService`, no `UmbracoApiController` or convention-based front-end API routing, no `IPublishedContent.Parent`/`.Children` properties. `GetById(Guid)` is never called on `IContentService`: 17.0.0 re-declares it there with `new` and 18 does not, so a 17.0.0-compiled call binds to a declaration site that vanishes at runtime on 18 (`MissingMethodException`, reproduced against real 17.0.0 and 18.1.1 binaries during development). Existence checks use `IEntityService.Exists(Guid, UmbracoObjectTypes)`, which is identical in both.

Each future major needs a re-verification pass rather than a presumption of forward compatibility — Umbraco's announced service-layer refactoring spans majors 18–21.

## License

MIT.
