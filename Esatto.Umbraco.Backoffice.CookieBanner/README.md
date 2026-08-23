# Esatto.Umbraco.Backoffice.CookieBanner

Turnkey GDPR/ePrivacy cookie consent for Umbraco 17 & 18: a blocking consent dialog,
per-category gating of scripts and embeds, Google Consent Mode v2 signalling, an
editor-managed cookie registry and a rendered cookie policy page.

- Four fixed categories — `necessary`, `preferences`, `statistics`, `marketing` — mapped onto Consent Mode v2 signals
- `<consent-script>` and `<consent-embed>` gate third-party code **server-side**, so a blocked script is never sent to the browser
- Editor-managed registry: a `cookieDefinition` element type inside a `cookieRegistry` block list on a `cookiePolicy` document type
- Consent copy lives in Umbraco dictionary items, so legal wording changes without a deploy; English and Swedish ship as fallbacks
- Self-contained styling through `--consent-*` custom properties — no dependency on the host's design system
- No SQL migration, no backoffice steps, no rate-limiter wiring

## Install

```bash
dotnet add package Esatto.Umbraco.Backoffice.CookieBanner
```

Then one line in `Program.cs`, after `BootUmbracoAsync()`:

```csharp
app.UseCookieConsent();
```

And two lines of Razor in your layout:

```cshtml
@* in <head>, after your own stylesheet *@
<consent-head />

@* first element in <body>, before <header>, so the dialog is first in tab order *@
<consent-banner />
```

`_ViewImports.cshtml` needs the tag helpers registered:

```cshtml
@using Esatto.Umbraco.Backoffice.CookieBanner
@addTagHelper *, Esatto.Umbraco.Backoffice.CookieBanner
```

## Configuration

All settings are optional and bind from the `Esatto:CookieBanner` section:

```json
{
  "Esatto": {
    "CookieBanner": {
      "PolicyVersion": 1,
      "CookieName": "cookie-consent",
      "CookieLifetimeDays": 365,
      "GoogleMeasurementId": null,
      "PolicyPageKey": null,
      "EndpointPath": "/api/cookie-consent",
      "ThrottleRequestsPerMinute": 10
    }
  }
}
```

- `PolicyVersion` — bumping it re-prompts every visitor, so reworded consent text can be re-consented
- `CookieName` — set this to an existing name when migrating from a hand-rolled banner and no visitor is re-prompted
- `GoogleMeasurementId` — when null, no Consent Mode snippet and no gtag script are emitted at all
- `PolicyPageKey` — optional override; by default the first published `cookiePolicy` node is used

## Gating third-party code

```cshtml
<consent-script category="Statistics" async src="https://example.com/analytics.js"></consent-script>

<consent-embed category="Marketing" src="https://www.youtube.com/embed/xyz" title="Product tour" />
```

`<consent-script>` renders nothing until the category is granted. `<consent-embed>`
renders a placeholder with a call to action that opens the consent dialog.

## Browser API

```js
window.cookieConsent.open();
window.cookieConsent.has('statistics');   // => boolean
window.cookieConsent.get();               // => { categories, version }
document.addEventListener('cookieconsent:change', e => console.log(e.detail));
```

## Theming

Override the tokens on `:root` in your own stylesheet — for example:

```css
:root {
  --consent-accent: #ffd200;
  --consent-surface: #ffffff;
  --consent-text: #001f54;
  --consent-backdrop: rgba(0, 31, 84, 0.6);
}
```

## Compatibility

Single `net10.0` assembly built on the Umbraco `17.0.0` floor with no upper bound, verified
against 18. No SQL migration: every schema artefact is installed through Umbraco services at
startup, idempotently.

## Licence

MIT.
