# Esatto.Umbraco.Backoffice.Seo

SEO Health for Umbraco 18: a dashboard that scans every published page of your site, in every language, and tells editors and developers what to fix first to be found in search engines.

## What it does

- **Scans every published page in every language** from the server, the way a search engine first sees it: the HTML and the response headers. Nightly, and whenever someone clicks **Scan now**. No browser tab has to stay open.
- **Runs 32 checks** based on current best practice (Google Search Central):
  - **Page content (editors):** title missing, too short or too long, or duplicated; meta description missing, too long or duplicated; main heading (H1) missing or repeated; very little text; broken links and links through redirects; social sharing tags (Open Graph); pages missing in some languages.
  - **Templates and setup (developers):** pages that don't load; the home page hidden from search; canonical links missing or pointing elsewhere; hreflang language links missing, invalid or not reciprocal; `x-default`; the `lang` attribute; JSON-LD that doesn't parse; slow responses; very large pages; pages missing from the sitemap and sitemap entries that shouldn't be there.
  - **Whole site:** robots.txt, sitemap, http → https redirect, www vs bare domain, the whole site hidden from search engines.
- **Puts the most important fixes first** ("Fix these first"), so editors get a short to-do list rather than hundreds of equal warnings.
- **Explains every issue:** why it matters, how to fix it in Umbraco terms, and what to watch out for. Each affected page links straight to the page in Umbraco (in the right language) and to the live page.
- **Scores the site from 0 to 100** and keeps a trend of recent scans, so you can see whether things are getting better.
- **Re-check one page** after fixing it, without waiting for the next scan.
- **Ignore** a finding on a page, or a whole check, with a note saying why (a contact page is allowed to be short). Ignores apply to every later scan and can be undone.
- **Export to CSV** for sharing or planning.

## What it does not do

- **No keyword research, rankings or traffic.** That's Google Search Console's job, and it complements this package well.
- **No JavaScript is run.** Head tags that only appear after JavaScript runs are reported as missing. Normal Umbraco Razor sites aren't affected.
- **No real-user speed data (Core Web Vitals).** "Slow response" measures the server's own answer time.
- **External links aren't checked**, only links to the site's own hosts.
- **It doesn't know your document types.** The advice is generic ("fill in the meta description, usually on an SEO tab") because the package works on any site.
- It doesn't repeat the checks of [Esatto.Umbraco.Backoffice.Accessibility](https://www.nuget.org/packages/Esatto.Umbraco.Backoffice.Accessibility) (alt texts, heading order, contrast).

## Install

```bash
dotnet add package Esatto.Umbraco.Backoffice.Seo
```

Restart the site. The dashboard appears in the **Content** section as **SEO Health**. The package creates its own database tables on first start. The first nightly scan runs at 03:00 server time; click **Scan now** to scan right away.

Everyone with access to the Content section can see results, scan, re-check and ignore. Only users with the Settings section can change settings.

## How scanning works

The server fetches its own published pages over HTTP, at the addresses Umbraco gives them (Culture and Hostnames). It sends the user agent `EsattoSeoHealth/<version>` and follows redirects itself, so redirect chains can be reported. Two pages are fetched at a time by default; this is adjustable in Settings.

Because the site requests its own public address, check the following:

- **Firewalls and bot protection** (for example Cloudflare rules) must let the server reach its own hostname. If every page fails, the scan stops after 10 and explains what to check.
- **Domains bound without a scheme** (`esatto.se` rather than `https://esatto.se`): outside a web request, Umbraco builds their addresses with `http://` unless `Umbraco:CMS:WebRouting:UmbracoApplicationUrl` starts with `https://`. The scan follows the redirect to https and records the https address, at the cost of one extra request per page. Setting the application URL avoids that.
- **Pages without a domain** (nothing bound in Culture and Hostnames) are fetched from the **Base URL** in Settings, or from `Umbraco:CMS:WebRouting:UmbracoApplicationUrl`. Manual scans otherwise use the address the backoffice is open on.
- **Protected environments.** If the site uses Umbraco's basic authentication (`Umbraco:CMS:BasicAuth`) with a shared secret, the scanner sends `Umbraco:CMS:BasicAuth:SharedSecret` automatically. For anything else, add headers under `Esatto:Seo:RequestHeaders`. These headers are sent only to the site's own hosts: the domains bound in Culture and Hostnames and a configured base URL. They are never sent to another host a page redirects or links to, nor to a host taken from the backoffice request.

### Test and Dev environments

Test environments usually hide every page from search engines, for example with `X-Robots-Tag: noindex` or `Disallow: /`. The package recognises this. Instead of flagging every page, it raises one critical whole-site finding, and it checks the pages' content as if they were public. To mark an environment as hidden on purpose, set this in that environment's configuration (on Umbraco Cloud, as an environment setting on Test/Dev only):

```json
{ "Esatto": { "Seo": { "IntentionallyHidden": true } } }
```

The finding then becomes an information banner and the score isn't penalised. Never set this on the live site.

## Configuration

Everything on the Settings tab is stored in the database. Any value under `Esatto:Seo` in configuration wins and is shown read-only:

```json
{
  "Esatto": {
    "Seo": {
      "Schedule": { "Enabled": true, "Hour": 3 },
      "ExcludePaths": [ "/search" ],
      "Parallelism": 2,
      "DelayMs": 0,
      "BaseUrl": null,
      "IntentionallyHidden": false,
      "RequestHeaders": { "X-My-Header": "value" }
    }
  }
}
```

`IntentionallyHidden` and `RequestHeaders` can only be set in configuration.

## The score

Each page that search engines may index gets a score from 0 to 100:

- 0 if it has a **critical** problem;
- otherwise, the share of checks it passes. Critical checks weigh 10, warnings 3 and notices 1.

The site score is the average of the pages, multiplied by the share of whole-site checks that pass. A site with no indexable pages scores 0. Ignored findings count neither way.

## Multilingual sites

Both ways of building a multilingual site work:

- **Language variants** (one content tree, culture-variant document types).
- **One root per language.** hreflang is checked as links between addresses: valid language codes, targets that load, and pages that link back to each other. On a one-root-per-language installation, each language shows up as its own site in the site picker.

## Source

[github.com/carl-schele-esatto/Esatto.Packages](https://github.com/carl-schele-esatto/Esatto.Packages/tree/main/Esatto.Umbraco.Backoffice.Seo). MIT licence. HTML parsing by [AngleSharp](https://anglesharp.github.io/) (MIT).
