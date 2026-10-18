# Esatto.CookieScan.Core

The rules behind the [Esatto cookie scanner](https://www.nuget.org/packages/Esatto.CookieScan.Cli),
with **no dependencies at all**.

- Known-cookie catalogue with wildcard patterns and most-specific-wins matching
- Consent-category inference from which passes a cookie appeared in
- The violation rule: set outside the consent it needed
- Append-only merge planning against what a policy page already declares
- Localised duration formatting (`sv`, `en`) from a machine-readable day count
- Scan-to-scan diffing: appeared, disappeared, recategorised

## Why it exists as its own package

The scanner is a browser automation tool; the endpoint it writes through is an Umbraco web
application. Both have to agree exactly on what counts as a violation, what category an unrecognised
cookie gets and how a duration reads in Swedish — a site that declared a cookie differently from the
way the tool proposed it would be the one bug this whole design exists to prevent.

So the shared rules live here, and having **no `PackageReference` whatsoever** is what enforces it:
the Umbraco package can depend on the rules without dragging Playwright into a web application, and
the rules can be unit tested without a browser or a published content graph.

## Install

```bash
dotnet add package Esatto.CookieScan.Core
```

You want this package directly only if you are building your own front end or your own write-back.
To *run* a scan, install `Esatto.CookieScan.Cli`.

## The catalogue

`CookieCatalogue` is loaded from JSON rather than declared in code, because its `purpose` text
becomes public legal wording on a policy page and must be changeable without a rebuild. One entry
looks like this:

```json
{
  "pattern": "_ga_*",
  "provider": { "sv": "Google Analytics", "en": "Google Analytics" },
  "category": "statistics",
  "tracker": true,
  "durationDays": 730,
  "purpose": { "sv": "Mäter användningen av webbplatsen.", "en": "Measures use of the site." }
}
```

- `pattern` — `*` is the only wildcard. The most specific match wins: fewest characters absorbed by
  wildcards, then the longest literal prefix. A name nothing matches returns `null` rather than a
  guess, which is what routes it into the needs-review path instead of a confident wrong declaration.
- `expected` — this site's own stack sets it, so its **absence** from a scan is itself a finding.
  Third-party entries leave it off: an absent Google cookie is normal.
- `consentCookie` — marks the banner's own consent cookie, the one entry whose name is per-site
  configuration. `WithConsentCookieNamed` rewrites it for a site that renamed it.
- `durationDays` — a number rather than pre-written text, so `DurationFormatter` can render it in
  either language. `0` is a session cookie; omitted means no documented lifetime, so use what the
  browser reported.

The shipped catalogue deliberately has **no catch-all entry**. With one, nothing could ever reach
needs-review, and every unknown cookie would be declared with the catch-all's wording.

## License

MIT.
