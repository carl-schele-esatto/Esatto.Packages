# SEO Health package — Design

**Date:** 2026-11-09
**Package:** **`Esatto.Umbraco.Backoffice.Seo` 1.0.0** (new, public, nuget.org, `umbraco-marketplace` tag)
**Repo dir:** `Esatto.Umbraco.Backoffice.Seo/` + `Esatto.Umbraco.Backoffice.Seo.Tests/`
**First consumer / test site:** `c:\src\Esatto.Web.Nova` (esatto.se, Umbraco 18 on Umbraco Cloud, sv + en).
`c:\src\umbracokalaset26` (Umbraco 18.2.1) is the clean test site.
**Closest sibling to mirror:** `Esatto.Umbraco.Backoffice.Accessibility` (spec:
`2026-10-21-accessibility-design.md`) — same dashboard look, same "explain every issue" approach,
same persistence/migration/settings patterns.

## Summary

Carl wants a backoffice dashboard that scans every published page on the site, in every language,
and finds SEO problems according to current best practice — with each problem explained and pointing
to where it's fixed. It ships as a NuGet package because it's meant for **esatto.se and client sites**,
so it must work on any Umbraco 18 site without knowing its document types.

What an editor should walk away with (Claude's proposal, Carl agreed "let's start with this"):
**a short, prioritised to-do list** — "these pages need attention, here's what to do, here's where in
Umbraco" — **plus a score that shows whether the site is getting better over time.** Not a wall of
300 equal-weight warnings.

Carl expects to improve and add checks over time, so adding a check must be cheap (see *Architecture*).

### Relation to what already exists

- **Nova's existing "SEO" dashboard** (`Esatto.Web.Nova/src/Features/Backoffice/Seo/`) shows GA4 +
  Search Console data (traffic, rankings, opportunities). It is site code, depends on Google
  credentials and the Manadsrapport libraries, and **stays as it is**. This package is a separate
  dashboard with a different job: *what's wrong on each page and how to fix it*. The two must not be
  confused, hence a different label (see Decisions).
- **No dependency** on the Accessibility package or any other Esatto package. Checks that
  Accessibility already covers (image alt text, heading order, contrast) are **not** duplicated here.

### Decisions

| Question | Decision | By |
|---|---|---|
| New package vs extend Nova's SEO tab | New, separate package and dashboard; Nova's tab stays | Claude's recommendation, Carl agreed |
| Audience | esatto.se **and client sites** → generic, no knowledge of doctypes/property aliases | Carl |
| Check list for 1.0 | The list under *Checks* | Claude's proposal, Carl: "let's start with this" |
| Package name | `Esatto.Umbraco.Backoffice.Seo` (repo convention; Carl's "Esatto.SEO" was an example) | Claude's call |
| Dashboard label | **"SEO Health"** — must differ from Nova's "SEO" | Claude's call |
| Where scanning runs | **On the server**: the site fetches its own published pages over HTTP and parses the HTML. No browser, no JS execution | Claude's call (see *How a scan works*) |
| Scheduled scans | **Yes**, nightly by default, plus a **Scan now** button. Enables history and the trend | Claude's call |
| Language of UI and advice | **English only** in 1.0, culture-keyed texts so Swedish can be added (same as Accessibility) | Claude's call, following Carl's Accessibility ruling |
| Umbraco version | **18 only**, net10.0, TypeScript + Vite client | Claude's call (matches Accessibility) |
| Dashboard location | **Content section**, visible to every user with Content access; Settings tab only for Settings-section users | Claude's call (matches Accessibility) |
| Settings storage | Database first, `Esatto:Seo:*` in configuration wins and shows read-only (same as Accessibility) | Claude's call |
| Multi-site | Scans, ignores and settings-exclusions keyed **per site root**; site picker only when >1 root | Claude's call (matches Accessibility) |
| Outside data (e.g. Search Console impressions) for prioritising | **Not in 1.0.** Data model keeps content key + culture + URL stable so a priority provider can be added in 1.1 | Claude's call |

### Out of scope (1.0)

- Keyword research, rankings, traffic (Nova's SEO tab / Search Console do this).
- Core Web Vitals field data (CrUX) and anything that needs a real browser (rendered DOM, JS-inserted
  tags, LCP/CLS). The scan sees the HTML the server sends.
- **External** link checking (only links to the site's own hosts are checked).
- Checking while editors write (content-app / save-time validation) — good 1.1 candidate.
- AI suggestions (rewriting titles/descriptions).
- Knowing which property a value lives in (e.g. "fill in *SEO description* on the SEO tab"). Advice is
  generic in 1.0. Candidate for 1.1: optional config mapping check → property alias.
- Pages behind member login (skipped and listed as skipped). Unpublished/draft content.
- Checks the Accessibility package already makes (alt text, heading order, contrast).
- Umbraco 17. PDF export (CSV is in).

## What the backoffice user sees

**Content section → dashboard "SEO Health".** Tabs: **Overview · Issues · Settings**. Light and dark
backoffice themes. The dashboard itself meets WCAG 2.2 AA (repo standard).

### Overview

- Big **score** (0–100) with a small trend line/list of the last scans (date, score, pages) so
  progress is visible.
- Last scan: when, how many pages, started by (user name or "scheduled"), duration; **Scan now**
  button with progress (pages done / total, current URL, Cancel) while running.
- **"Fix these first"**: the top 5 issues by priority (see *Priority*), each one line —
  "Meta description missing — 14 pages — Editor" — linking into Issues.
- Three tiles: **Critical · Warning · Notice** counts (open issues). Clicking filters Issues.
- Site-wide findings (robots.txt, sitemap, HTTPS, www, home page visible) shown as a compact
  checklist with ✓/✗.
- **Hidden from search (N)**: collapsible list of excluded/blocked pages with the reason
  (noindex meta / X-Robots-Tag / robots.txt), so an accidental noindex is easy to spot.
- Banner when the whole site is hidden (see *Indexability*).

### Issues

- Filters: severity, **owner** (Editor / Developer), language, check, status (open / ignored),
  free-text on URL/page name.
- One row per **check**, sorted by priority: "Title too long — 23 pages — Warning — Editor".
  Duplicate checks group by the duplicated value: "Same title on 4 pages: 'Esatto – Hem'".
- Opening a check:
  - **Why it matters**, **How to fix** (Umbraco terms where generic advice allows, e.g. "Open the page
    in Umbraco, switch to the English variant, and fill in the page's meta description field —
    usually on an SEO tab"), **Caveats** (e.g. "Long titles aren't penalised; they're just cut off in
    results").
  - Affected pages: page name, language, URL, the **found value** (e.g. the actual title and its
    length), **Edit in Umbraco** (opens the content node in that culture), **View page**.
  - **Ignore** per page × check (or for the whole check site-wide) with a required note; stored and
    re-applied on later scans; reversible. (Same idea as Accessibility's false positives — e.g. a
    contact page legitimately has thin content.)
  - **Re-check this page** — re-runs all checks for that page and updates results.
- **Export CSV** of the current filtered list.

### Settings

- **Schedule:** nightly scan on/off (default **on**), hour of day (default **03:00** server time).
- **Scope:** exclude paths (prefix list).
- **Speed:** parallel requests (default **2**, max 8), delay between requests (default 0 ms).
- **Connection** (normally empty; see *How a scan works*): base URL override.
- Values pinned in `Esatto:Seo:*` configuration show read-only with "set in configuration".
  Secrets (basic-auth bypass) are **configuration only**, never stored in the database.

## Checks

Each check has: id (stable, kebab-case), severity, owner, scope (page / site / cross-page), and
knowledge-base texts. Thresholds are constants in 1.0 (Claude's call), defined once in the check.

Severity (Claude's call): **Critical** = page can't be found or indexed; **Warning** = hurts how the
page shows in or ranks in search; **Notice** = worth doing.

### Page content — owner Editor

| Id | Finding | Severity |
|---|---|---|
| `title-missing` | No `<title>` or empty | Critical |
| `title-length` | Under 30 or over 60 characters | Warning |
| `title-duplicate` | Same title as another page in the same language | Warning |
| `description-missing` | No `<meta name="description">` or empty | Warning |
| `description-length` | Under 70 or over 160 characters | Notice |
| `description-duplicate` | Same description as another page in the same language | Notice |
| `h1-missing` | No `<h1>`, or only empty ones | Warning |
| `h1-multiple` | More than one `<h1>` | Notice |
| `thin-content` | Under 200 words of visible text in `<main>` (fallback `<body>` minus `header/nav/footer`) | Notice |
| `broken-internal-link` | Link to own host returns 4xx/5xx or doesn't resolve | Warning |
| `redirected-internal-link` | Link to own host goes through a redirect (point it at the final URL) | Notice |
| `og-missing` | Missing `og:title` or `og:image` (blank card when shared) | Notice |
| `translation-missing` | Node published in some but not all of the site's languages | Notice |

`translation-missing` comes from Umbraco (content cache), not the HTML. Languages "of the site" = the
cultures with a domain/hostname under that root, or all installed languages if no domains are set.

### Templates and setup — owner Developer

| Id | Finding | Severity |
|---|---|---|
| `status-error` | Published page returns non-200 (after following redirects: report the chain) | Critical |
| `home-hidden` | The site's root page is excluded (noindex) or blocked (robots.txt) | Critical |
| `hidden-but-blocked` | Page is both noindex and robots-blocked (Google can't see the noindex, so the URL may still show) | Notice |
| `canonical-missing` | No `<link rel="canonical">` | Notice |
| `canonical-mismatch` | Canonical points to another URL, another host, or a non-200 URL | Warning |
| `hreflang-missing` | Node has other published culture variants but the page has no `hreflang` alternates (variant sites only) | Warning |
| `hreflang-invalid` | An alternate has an invalid language code, points to a URL that isn't a 200 page in this scan, doesn't link back (not reciprocal), or the page doesn't list itself | Warning |
| `hreflang-no-x-default` | Alternates present but no `x-default` | Notice |
| `lang-mismatch` | `<html lang>` missing or doesn't match the page's culture | Warning |
| `structured-data-invalid` | `application/ld+json` block that doesn't parse as JSON | Warning |
| `slow-response` | Server response time (time to headers) over 1.5 s | Notice |
| `heavy-html` | HTML over 500 kB | Notice |
| `sitemap-missing-page` | Published, indexable page not in the sitemap | Notice |
| `sitemap-bad-entry` | Sitemap lists a URL that is hidden (noindex/blocked), non-200 or not a published page | Warning |

### Indexability (answers builder-check questions 1 and 4 — Claude's call)

Every fetched page gets an **indexability state**: **Indexable**, **Excluded** (`noindex` in
`<meta name="robots">`, `<meta name="googlebot">` or `X-Robots-Tag`) or **Blocked** (robots.txt
disallows it for `*`/Googlebot). Excluding pages is usually deliberate (login, search, thank-you pages;
Nova's "Hide from search engines" checkbox), and the package can't tell intent from mistake, so:

- Excluded/Blocked is **not a finding by itself**. Overview shows a **"Hidden from search (N)"** list
  so a mistake is easy to spot.
- It becomes a finding only when it contradicts something: `home-hidden` (Critical),
  `sitemap-bad-entry` (Warning), `hidden-but-blocked` (Notice), `canonical-mismatch` when a page's
  canonical points at a hidden URL.
- Hidden pages run only `status-error`, `hidden-but-blocked` and the link check of their outgoing links;
  every content/head check is **not applicable** for them, and they're left out of the score and out of
  duplicate comparison.

**Whole site hidden (Test/Dev environments).** If the root page of a site is hidden *and* at least 90 %
of its fetched pages are hidden, the site is **hidden as a whole**:

- One site-wide finding `site-hidden` (Critical) instead of `home-hidden` and per-page noise:
  "Every page is hidden from search engines. Expected on a Test or Dev environment; on the live site
  this is the most serious problem there is."
- Pages are then treated **as if indexable** for the content checks, so Test still shows the content
  issues worth fixing before they reach Live.
- Configuration `Esatto:Seo:IntentionallyHidden = true` (set per environment, e.g. as a Cloud
  environment setting on Test/Dev) turns `site-hidden` into an info banner with no score penalty.
  Not editable in the backoffice — a database value would travel with content and could hide a real
  problem on Live. The environment name is **not** used: Umbraco Cloud Test runs as `Production` too.

### Multilingual sites (answers builder-check question 3 — Claude's call)

Both shapes work in 1.0: **culture variants** (Nova) and **one root per language**.

- hreflang is validated as a **URL graph** (code valid, target is a 200 page in this scan, reciprocal,
  self-reference present), so alternates pointing to another node/tree are fine.
- Variant-only checks (`translation-missing`, `hreflang-missing`) simply don't apply to invariant nodes.
- A "site" is still one root node (picker, scans, ignores). On a one-root-per-language installation
  each language is its own site in the picker; duplicate checks are per culture anyway, so nothing is
  lost. Grouping roots into one site is a 1.1 option. The README says so.

### Whole site — owner Developer (shown on Overview)

| Id | Finding | Severity |
|---|---|---|
| `robots-missing` | `/robots.txt` missing (per host) | Notice |
| `sitemap-missing` | No sitemap at `/sitemap.xml` or listed in robots.txt | Warning |
| `https-redirect` | `http://` doesn't redirect to `https://` | Critical |
| `host-duplicate` | Both `www.` and bare host answer 200 without redirecting to one | Warning |

Site-wide checks run only for real hostnames (skipped for `localhost` and IPs).

### Priority

Sort key for "Fix these first" and the Issues list (Claude's call): severity weight
(Critical 100 / Warning 10 / Notice 1) × number of affected, non-ignored pages. Built as a pure function
so a future priority provider (e.g. Search Console impressions per URL) can plug in.

### Score

Claude's call (revised after builder check question 2), pure and unit-tested. Weights: Critical 10,
Warning 3, Notice 1. Ignored = not counted (neither pass nor fail).

- **Page score** (indexable pages only, 0–100): **0 if the page has any open Critical finding**,
  otherwise 100 × weighted passes / weighted applicable checks for that page.
- **Site factor** (0–1): weighted passes / weighted applicable **site-wide** checks (`robots-missing`,
  `sitemap-missing`, `https-redirect`, `host-duplicate`, `home-hidden`, `site-hidden`); 1 when none
  apply (e.g. localhost). `site-hidden` with `IntentionallyHidden` is not applicable.
- **Score = round(average page score × site factor)**. **0 when there are no indexable pages.**
- Cross-page findings (duplicates, hreflang, sitemap entries) count against each page they name.
- Tooltip says what it means in one sentence. The formula is versioned (`scoreVersion` stored per scan)
  so a later change doesn't silently mix incomparable numbers in the trend.

## How a scan works (on the server)

1. **Page list:** every published content item with a template and a URL, per culture, under the site
   root, minus excluded paths and member-protected pages (listed as skipped). Each entry: content key,
   name, culture, absolute URL. (Reuse the shape of Accessibility's `PageListBuilder`/`SitePageSource`;
   copy, don't reference.)
2. **Base URL:** absolute URLs from Umbraco's URL provider when domains are bound. When a URL is
   relative (no domain bound), use the base URL override, else
   `Umbraco:CMS:WebRouting:UmbracoApplicationUrl`, else — for a manual scan only — the host of the
   backoffice request that started it. If none is available for a scheduled scan, the scan is skipped
   with a clear message on Overview.
3. **Fetch:** `HttpClient` (named, via `IHttpClientFactory`), user agent
   `EsattoSeoHealth/<version> (+package url)`, `Accept-Language` set to the culture, redirects
   followed manually so chains can be reported, 30 s timeout per request, parallelism and delay from
   Settings. **Protected environments:** if the site uses Umbraco's basic auth
   (`Umbraco:CMS:BasicAuth`), the scanner sends the configured shared-secret header
   (`Umbraco:CMS:BasicAuth:SharedSecret:HeaderName/Value`) so Umbraco Cloud Test/Dev environments can
   be scanned. Optional `Esatto:Seo:RequestHeaders` (config only) for anything else.
4. **Parse:** AngleSharp (MIT) for HTML. Each page check is a function from *(page info, response,
   parsed document)* to findings. Cross-page checks (duplicates, hreflang reciprocity, sitemap
   comparison) run once after all pages are fetched, from stored per-page facts.
5. **Internal links:** collected from every page, de-duplicated, each unique URL checked once per scan
   (HEAD, falling back to GET on 405).
6. **Storage as it goes:** each page's facts and findings are saved when done, so a crash or restart
   leaves a resumable/incomplete scan rather than nothing.
7. **One scan at a time** per site root; Scan now while a scan runs shows the running one.
   Scheduled scans run only on the scheduling-publisher server (load-balanced safe), using Umbraco's
   recurring background job pattern.

Server-side scanning (vs Accessibility's browser iframes) is chosen because: SEO checks need the raw
HTML/headers that search engines get first; it allows scheduled scans and therefore history; no
backoffice tab has to stay open; no analytics noise (no JS runs).

## Knowledge base

Same approach as Accessibility: embedded JSON, per check id: title, why it matters, how to fix,
caveats, owner, severity, optional link to Google Search Central docs. Culture-keyed (`en` only in 1.0).
Written by us, not copied. A unit test asserts every registered check has complete texts.

## Architecture

### Server (`src/`)

- `SeoComposer` — services (`TryAdd`), migration plan on `UmbracoApplicationStartingNotification` when
  `RuntimeLevel.Run`, recurring scan job.
- **Checks:** `IPageCheck`, `ISiteCheck`, `ICrossPageCheck` — each check one small class, registered in
  a collection. **Adding a check = one class + one knowledge-base entry + its tests.** Thresholds live
  as constants in the check.
- **Scanning:** page list builder, fetcher, link checker, scan orchestrator, scheduler.
- **Persistence (NPoco + migration plan):** settings (single row, JSON), scans (id, site root key,
  started/finished, trigger manual/scheduled, user, status, page counts, score), scanned pages (scan,
  content key, culture, url, status code, timings, skip reason, extracted facts as JSON — title,
  description, canonical, hreflang set, etc.), findings (scan, page, check id, found value ≤ 1 kB),
  ignores (site, check id, content key + culture or whole check, note, user, date). Keep the last
  **10** scans' pages/findings; older scans keep only their summary row (the trend).
- **Management API** `/umbraco/management/api/v1/seo-health/...` (`ManagementApiControllerBase`,
  `VersionedApiBackOfficeRoute`): overview, issues (filterable), CSV, start/cancel scan, scan
  progress, re-check page, ignores — policy `SectionAccessContent`; settings write —
  `SectionAccessSettings`. Route and manifest aliases must not collide with Nova's `esatto/seo`.

### Client (`Client/src`, Vite → `wwwroot/App_Plugins/Esatto.Umbraco.Backoffice.Seo/`)

- Dashboard element (Lit, UUI) with three tabs; `umbraco-package.json` registers a `dashboard` with
  `Umb.Condition.SectionAlias` = `Umb.Section.Content`. Progress polled while a scan runs.

## Error handling

- Page timeout / connection error → page marked failed with reason, scan continues.
- Every page fails (e.g. base URL wrong, firewall, basic auth) → scan stops after the first N
  consecutive failures (10) with an explanation of the likely cause and the setting that fixes it.
- Restart mid-scan → scan shown as incomplete; next manual or scheduled run starts fresh.
- Missing knowledge-base text → generic text, never a crash.

## Testing

- **xUnit + NSubstitute** (repo standard). Every check tested against small HTML fixtures (pass and
  fail cases). Cross-page checks (duplicates, hreflang reciprocity, sitemap diff), score, priority,
  ignore matching, robots.txt matching, base-URL resolution, retention, knowledge-base completeness.
- Fetcher tested with a fake `HttpMessageHandler` (redirect chains, timeouts, X-Robots-Tag).
- **Manual on Nova** (sv + en, Umbraco Cloud) and umbracokalaset26: full scan, scheduled run, re-check,
  ignore, CSV, Test environment behind basic auth and hidden from search (expect the `site-hidden`
  banner path, not hundreds of per-page findings).

## Builder-check rulings (2026-11-09)

A reviewer read this as a builder; four questions came back and were settled by Claude (Carl: "take
recommendations"): deliberate noindex → *Indexability*; score inflation → *Score*; one-tree-per-language
sites → *Multilingual sites*; Test/Dev hidden on purpose → *Indexability* (whole site hidden). Retention
per root, what a re-check does to stored scores, CSV shape, sitemap index handling and scheduler
details are left to the builder.

## Docs

README (repo standard: screenshots with absolute raw-GitHub URLs, source link): what it checks and what
it doesn't, install, the base URL / basic-auth notes for Umbraco Cloud, scheduling, how the score works.

## Risks

- **Self-fetch blocked:** firewalls, Cloudflare bot rules or IP restrictions in front of the site can
  block the server calling its own public URL. Mitigation: clear error, base URL override, custom
  headers; documented.
- **JS-rendered head tags** (SPA-style sites) would be reported as missing. Acceptable for Umbraco
  Razor sites; documented.
- **Load on big sites:** 5,000 pages × 2 parallel ≈ well under an hour nightly; parallelism and delay
  are configurable.
- **Thresholds are opinions** (title 30–60, description 70–160, 200 words). Ignores cover legitimate
  exceptions; making them configurable is a 1.1 option if needed.
