# Accessibility package — Design

**Date:** 2026-10-21
**Package:** **`Esatto.Umbraco.Backoffice.Accessibility` 1.0.0** (new, public, nuget.org, `umbraco-marketplace` tag)
**Repo dir:** `Esatto.Umbraco.Backoffice.Accessibility/` + `Esatto.Umbraco.Backoffice.Accessibility.Tests/`
**First consumer / test site:** `c:\src\Esatto.Web.Nova` (esatto.se, Umbraco 18 on Umbraco Cloud). `c:\src\umbracokalaset26` (Umbraco 18.2.1) is the clean test site.

## Summary

Carl came back from an accessibility seminar wanting to know, from inside Umbraco, how accessible a
site actually is and what to do about it. The package adds:

1. **A backoffice dashboard** that scans the site's published pages against a chosen WCAG target
   (version × level), shows a conformance score, and lists every issue with *why it matters*, *how to
   fix it* and *caveats*. Audience: **developers and editors**.
2. **A manual checklist** for the criteria a machine cannot judge, with results saved (who, when, note).
3. **Visitor features on the public site**, each switchable on/off from the dashboard: follow the
   visitor's own browser/OS settings, and an optional accessibility-preferences panel (larger text,
   bigger targets + more spacing, high contrast, reduced motion, readable font, highlighted links…).

It is a **standalone package**: no dependency on CookieScan, CookieBanner or any other Esatto package.

### Background that shaped the design (agreed in conversation)

- **Version and level are separate axes.** WCAG 2.0 / 2.1 / 2.2 are versions; A / AA / AAA are levels
  inside each. Each version is a superset of the previous one, except that 2.2 made **4.1.1 Parsing**
  obsolete (treated as always satisfied). The target is picked as two dropdowns, e.g. "2.1 · AA".
  EU law (European Accessibility Act via EN 301 549) currently means **2.1 AA**; the UI says so. W3C
  advises against AAA as a site-wide requirement; the UI warns when AAA is picked.
- **"Dynamic accessibility" is real but is not compliance.** Needs conflict (someone with Parkinson's
  wants big, well-spaced targets; someone using magnification wants a compact layout). The approach is
  three layers: (1) the site meets the baseline for everyone, (2) it respects the settings visitors
  already have, (3) an optional preferences panel. The panel is an extra, never a substitute. So-called
  overlays that promise automatic compliance are widely criticised (the US FTC fined accessiBe $1M in
  2025); the dashboard and README say plainly that the panel does not make a site conformant.
- **Automated testing finds only part of the problems** (roughly a third to a half). The report must
  show "needs a human" alongside "fails".

### Decisions

| Question | Decision | By |
|---|---|---|
| Separate package | Yes, own package, no dependency on other Esatto packages | Carl |
| Target picker | Version (2.0 / 2.1 / 2.2) × Level (A / AA / AAA), default **2.2 · AA** | Carl (agreed); default is Claude's call |
| Feature toggles | Every visitor-facing feature can be switched on/off individually | Carl |
| Manual checklist | Yes, saved results | Carl |
| Where scanning runs | **In the backoffice user's browser**: hidden same-origin iframes + axe-core. No server-side browser, no scheduled scans | Claude's recommendation, Carl said "continue" |
| Dashboard overview | **Score first** (mockup A): big %, breakdown by the four principles | Carl |
| Checklist & Settings tabs | As mocked (see below) | Carl ("go ahead") |
| Umbraco version | **18 only**, net10.0, TypeScript + Vite client (AICosts template) | Claude's call (Nova is on 18; AICosts precedent) |
| Dashboard location | **Content section** (editors don't have Settings access); Settings tab restricted | Claude's call |
| Defaults for visitor features | "Follow browser/OS settings" has two sub-toggles: **motion on**, **contrast off**; the visible panel **off**. Installing changes nothing visible except for visitors whose OS asks for reduced motion (plus a forced-colours baseline that only affects Windows High Contrast users) | Claude's call (builder check) |
| Multi-site | Scans, manual results and false positives are keyed **per site** (root node). A site picker shows only when there's more than one root. Settings (target + visitor features) are per installation | Claude's call (builder check) |

### Out of scope (1.0)

- Scheduled / unattended scans and server-side headless browsers.
- Checking content while editors write it (in the content editor / RTE). Good candidate for 1.1.
- Automatic fixing of anything on the site; rewriting the DOM/ARIA of the site at runtime; AI alt text.
- Scanning PDFs and other documents (only flagged in the manual checklist, see below).
- An accessibility-statement generator (tillgänglighetsredogörelse).
- PDF export of the report (CSV export is in).
- Pages behind member login (skipped and listed as skipped).
- Umbraco 17.

## What the backoffice user sees

**Content section → dashboard "Accessibility"**, visible to every user with Content section access.
Four tabs: **Overview · Issues · Manual checklist · Settings**. Settings is visible/editable only to
users with Settings section access (`SectionAccessSettings`); others see the target read-only on the
Overview. Light and dark backoffice themes. The dashboard itself must meet WCAG 2.2 AA.

### Overview (mockup A, approved)

- Big score ring: **NN %** with the target under it ("WCAG 2.2 · AA").
- Target line ("change in Settings" link for those with access), last scan date, number of pages,
  who ran it, **Scan now** button (+ progress while scanning: pages done / total, current URL, Cancel).
- Four tiles, one per principle (Perceivable, Operable, Understandable, Robust): *N fail · N to check*.
  Clicking a tile opens Issues filtered to that principle.
- Summary line: *Automated: X pass / Y fail · Manual: N of M checked*.
- Small history list of the last 10 scans (date, score, target) so progress is visible.

### Score — how it's computed (Claude's call)

Per success criterion in the target set:

- **Fail** — any open automated violation mapped to it, or a manual result of *Fail*.
- **Pass** — no open violations **and** either a manual *Pass* or the criterion is marked
  *fully automatable* in the knowledge base. Very few are (e.g. 3.1.1 Language of Page); most —
  including 1.1.1 and 4.1.2 — are only partly covered by axe and need a manual Pass.
- **Not applicable** — manual *N/A*, or 4.1.1 under 2.2.
- **Not verified** — everything else.

**Score = Pass / (Total − Not applicable)**. Under the ring: "X pass · Y fail · Z not verified". This is
deliberately honest: a site with no automated failures but no manual review will *not* show 100%.
The ring tooltip explains this in one sentence.

### Issues

- Filter by principle, level, owner (**Editor / Developer / Both**), impact (axe's critical/serious/
  moderate/minor), status (open / false positive).
- One row per **rule × criterion**, sorted by impact then number of affected pages:
  "Images missing alternative text — 1.1.1 · A — 12 pages (34 elements) — Editor".
- Opening one (mockup approved):
  - criterion number, title, level, *since 2.x*, pages/elements affected, who fixes;
  - **Why it matters**, **How to fix** (Umbraco-specific where possible, e.g. "open the media item and
    fill in Alternative text"), **Caveats** (e.g. decorative images need empty alt, don't start with
    "Image of…");
  - affected occurrences grouped by page: page name, element snippet, CSS selector,
    **Edit in Umbraco** (opens the content node; for images whose `src` resolves to a media item, opens
    the media item), **View on page** (opens the page in a new tab);
  - link to W3C's "Understanding SC x.y.z";
  - **Mark as false positive** (per occurrence or for the whole rule on a page, with a required note;
    stored and re-applied on later scans; reversible);
  - **Re-check this page** (re-runs the scan for just that page and updates results).
- axe "incomplete" results (needs review) are listed under the criterion as **Needs review** and feed
  the manual checklist item for that criterion (shown there as "the scanner found N things to look at").
- **Export CSV** of the current filtered issue list.

### Manual checklist (as mocked, approved)

- Lists every criterion in the target set that is not *fully automatable*, filter All / Not checked /
  Failed / Needs re-check. Header: "N of M checked".
- Each item: number, title, level, "new in 2.x" tag, a plain-language **How to test** (concrete steps,
  e.g. "Tab through menu, forms, sliders, cookie banner"), any related automated findings, and buttons
  **Pass · Fail · Not applicable** + an optional note. Saves user name and date.
- A result becomes **Needs re-check** after a configurable number of months (default 6). Changing the
  target keeps existing results; criteria new to the target simply start as Not checked.
- A **Documents** hint: the scan collects links to PDF/Office documents and shows the count on the
  relevant items (e.g. 1.1.1, 1.3.1) — "this site links N documents; they must be checked separately".

### Settings (as mocked, approved)

- **Target:** Version dropdown, Level dropdown. Hint "EU legal requirement today: 2.1 AA"; warning on AAA.
- **Scan:** all published, routable pages (all cultures) · *exclude paths* (prefix list) · *also scan at
  mobile width (320 px)* (off by default; doubles scan time) · *include axe best-practice rules*
  (off; shown separately and never counted in the score) · manual re-check interval (months).
- **Visitor features (public website):**
  - *Follow visitors' browser/OS settings*: ☑ *reduced motion* (on by default) · ☐ *more contrast*
    (off by default — generic contrast CSS is the part most likely to clash with a design)
  - ☐ *Show accessibility preferences button* (off by default), and under it each option on/off:
    Larger text · Bigger buttons + more spacing · High contrast · Reduce motion · Readable font ·
    Highlight links · Reading guide (off by default).
  - Button position (bottom-right / bottom-left).
  - Warning: "The preferences button doesn't make the site compliant. It's an extra on top of a site
    that already passes."
- Saving settings takes effect on the public site without restart.
- Any setting pinned in configuration (`Esatto:Accessibility:*`) is shown read-only with
  "set in configuration" (see *Questions for Carl*, 2).

## How a scan works (in the browser)

1. Dashboard asks the server for the page list: every published content item with a template and a
   URL, per culture, minus excluded paths and protected (member-only) pages (listed as skipped).
   Each entry: content key, name, culture, absolute URL.
2. Pages **on the backoffice's own origin** are loaded into hidden iframes (2–3 in parallel), at
   1280 px wide (and 320 px if enabled). Pages whose URL is on another host can't be read from a
   different origin; they're listed as **skipped — open the backoffice on <host> to scan these**.
3. After `load` + a short settle delay, the dashboard injects the package's bundled **axe-core** into
   the frame as a same-origin `<script src>` (works with CSP `script-src 'self'`) and runs it with the
   tags for the target (`wcag2a`, `wcag2aa`, `wcag21a`, `wcag21aa`, `wcag22aa`, `wcag2aaa`… mapped from
   version × level), plus `best-practice` when enabled.
4. Each page's result (violations, incomplete, passes count, inapplicable) is posted to the server as
   it completes, so closing the tab loses at most the in-flight pages. An interrupted scan is shown as
   **incomplete** and can be **resumed**.
5. Scanned page loads carry `?esatto-a11y-scan=1` and the frame sets `window.esattoA11yScan = true`
   before site scripts where possible; the README tells site owners how to exclude these from
   analytics (consent-gated analytics usually won't fire anyway).

Requirements on the site (README): the site must allow same-origin framing (`X-Frame-Options:
SAMEORIGIN` or `frame-ancestors 'self'`, not `DENY`/`'none'`). Nova already sends `SAMEORIGIN` and
`frame-src 'self'`. The dashboard detects a blocked frame and explains the fix.

## Knowledge base

Shipped as data in the package (JSON, embedded), versioned with it:

- **Criteria** — every WCAG 2.0/2.1/2.2 success criterion: id, title, level, version introduced,
  principle, guideline, obsolete-in (4.1.1 → 2.2), *fully automatable* flag, **owner** (Editor /
  Developer / Both), and our own text: **Why it matters**, **How to fix**, **Caveats**, **How to test**
  (manual). Link to W3C Understanding. Source for ids/titles/levels: W3C's published WCAG JSON.
  Explanatory text is written by us (not copied from W3C) — W3C document licence allows linking and
  short quotes.
- **axe rule overrides** — for rules where a generic text isn't enough, a rule-specific How to fix /
  Caveats and owner (e.g. `image-alt` → Editor, "open the media item…"; `color-contrast` → Developer;
  `link-name` → Both). Rule → criteria mapping comes from axe's own `wcagNNN` tags.
- **Language:** English only in 1.0 (see *Questions for Carl*); entries keyed by culture so Swedish can
  be added later.

## Visitor features (public site)

Wiring copies CookieBanner's pattern:

```cshtml
@addTagHelper *, Esatto.Umbraco.Backoffice.Accessibility
<head> … <a11y-head /> </head>      @* CSS + tiny inline script that applies saved prefs before paint *@
<body> … <a11y-panel /> </body>     @* button + dialog; renders nothing when disabled *@
```

Server reads the current settings per request (cached), so a disabled feature emits **nothing** — no
CSS, no JS, no markup.

**How preferences apply:** the inline script sets attributes on `<html>`:
`data-a11y-text="large"`, `data-a11y-spacing="wide"`, `data-a11y-contrast="high"`,
`data-a11y-motion="reduce"`, `data-a11y-font="readable"`, `data-a11y-links="highlight"`.

- **Follow OS settings** sets the same attributes from `prefers-reduced-motion: reduce` (sub-toggle,
  on by default) and `prefers-contrast: more` (sub-toggle, off by default), and ships a
  forced-colors-safe baseline (focus outlines, borders on controls) that only applies under
  `forced-colors: active`. A panel choice overrides the OS.
- The package's stylesheet gives **safe, generic** defaults per attribute:
  text — root font scaling (works when the site uses `rem`; documented); spacing — WCAG 1.4.12 text
  spacing values + `min-block-size/min-inline-size: 44px` and extra gap for links, buttons, inputs
  (the "Parkinson's" option, AAA 2.5.5 size); contrast — darker text, solid backgrounds, visible
  borders/focus; motion — near-zero animation/transition durations, `scroll-behavior: auto`, pause
  autoplaying `<video>`; readable font — **Atkinson Hyperlegible** (OFL, bundled); links — underline +
  outline; reading guide — a horizontal band following the pointer.
- **Caveat (documented, shown in Settings):** generic CSS can't know a site's design and can break
  layouts — the very problem overlays have. So the attributes are the contract: the README documents
  them and site developers are expected to refine with their own CSS
  (`[data-a11y-contrast="high"] .hero { … }`). The defaults are conservative.
- **Storage:** `localStorage` only (no cookie, no server call) — user-requested functionality, no
  consent needed. A "Reset" button clears it.
- **The panel itself** must meet WCAG 2.2 **AAA**: a real `<button>` (44×44, visible label/accessible
  name) opening a modal `<dialog>`, focus moved in and returned on close, Esc closes, all options are
  native checkboxes/radios with labels, works with keyboard, screen reader, 400% zoom and forced colours.
- **Texts in the panel** follow the page's culture: Swedish and English shipped, English fallback,
  every string overridable from Settings (per culture). No dictionary items are created.

## Architecture

### Server (`src/`)

- `AccessibilityComposer` — services (`TryAdd`), migration plan on `UmbracoApplicationStartingNotification`
  when `RuntimeLevel.Run` (Redirects pattern).
- **Persistence (NPoco + migration plan):** tables for settings (single row, JSON payload), scans
  (id, site root key, started/finished, target, user, status, page counts, score snapshot), scanned pages (scan,
  content key, culture, url, status/skip reason), findings (scan, page, rule, criteria, impact, kind
  violation/incomplete, selector, html snippet ≤ 1 kB, media key if resolved), false positives
  (site, rule, content key, selector or whole-page, note, user, date), manual results (site,
  criterion, status, note, user, date). Keep the last **10** scans' findings; older scans keep only their summary row.
- **Management API** `/umbraco/management/api/v1/accessibility/...` (`ManagementApiControllerBase`,
  `VersionedApiBackOfficeRoute`): pages-to-scan, start/resume/finish scan, post page result, report
  (overview + issues, filterable), CSV, false positives, manual results — policy
  `SectionAccessContent`; settings write — `SectionAccessSettings`.
- Media resolution: image `src` → media item via the published media cache by URL path.
- Knowledge base loader + scoring service (pure, unit-tested).
- **Public side:** tag helpers `<a11y-head />`, `<a11y-panel />`, a view component for the panel,
  static assets under `wwwroot/esatto-accessibility/` (panel JS/CSS, font), settings cache invalidated
  on save.

### Client (`Client/src`, Vite → `wwwroot/App_Plugins/Esatto.Umbraco.Backoffice.Accessibility/`)

- Dashboard element with the four tabs (Lit, UUI), `umbraco-package.json` registering a `dashboard`
  with `Umb.Condition.SectionAlias` = `Umb.Section.Content`.
- Scan runner (iframe pool, axe injection, retry/timeout per page, cancel/resume), target → axe tags
  mapping, result reduction — pure logic separated and vitest-covered.
- axe-core bundled as a separate static file (not into the dashboard bundle) so it can be injected
  into the frames; its MPL-2.0 licence notice shipped alongside.

## Error handling

- Frame blocked (X-Frame-Options/CSP) → scan stops on the first page with an explanation and the fix.
- Page timeout (default 30 s) / error status → page marked failed with reason, scan continues.
- Cross-origin page → skipped with reason (see above).
- Server post fails → retried; on persistent failure the scan pauses and offers Resume.
- Knowledge base missing a criterion/rule text → generic text + W3C link, never a crash.

## Testing

- **xUnit + NSubstitute** (repo standard): scoring rules, target → criteria set (incl. 4.1.1 under 2.2),
  knowledge base completeness (every criterion in 2.0/2.1/2.2 has all texts, every owner set),
  false-positive matching, settings → tag helper output (disabled ⇒ empty), retention.
- **vitest:** target → axe tags, result reduction, scan queue/resume logic, panel preference logic
  (OS prefs + overrides → attributes).
- **Manual on Nova and umbracokalaset26:** full scan, re-check page, false positive, manual checklist,
  each visitor toggle on/off, panel with keyboard + NVDA + 400% zoom + Windows high contrast.

## Docs

README (repo standard: screenshots, source link): what it does and does not do (the overlay caveat),
installing, the two tags, framing requirement, analytics exclusion, the `data-a11y-*` contract with
CSS examples, the scoring explanation, axe-core licence note.

## Risks

- **Scan time on big sites:** ~2–4 s per page; 300 pages ≈ 10–20 min with the tab open. Mitigated by
  parallel frames, resume, and per-page re-check.
- **Site scripts in scanned frames** (analytics, chat widgets, cookie banners) run normally; banners may
  be reported as issues — which is correct, they're part of the page.
- **Generic visitor CSS** can clash with a site's design (see caveat); defaults are conservative and
  everything is off unless enabled, except OS motion/contrast following.
- **Score may feel low** at first because unverified criteria count against it — intentional, explained.

## Questions for Carl (answered 2026-10-21)

1. **Language / review.** "Fine for now" → **English only** for 1.0 (dashboard UI and knowledge
   base), matching the sibling packages; texts drafted by Claude, no external expert review before
   1.0. The JSON schema keeps a culture key so Swedish can be added later without a migration. The
   visitor panel still ships Swedish + English (it's visitor-facing).
2. **Settings across Test → Live.** Carl asked for a suggestion; Claude's call:
   **database first, appsettings can lock.** Settings are edited in the backoffice and stored in the
   database. Any value present under `Esatto:Accessibility` in configuration (e.g.
   `Esatto:Accessibility:VisitorFeatures:Panel:Enabled`) **wins**, and the backoffice shows that
   field read-only with "set in configuration". This keeps quick tweaks in the backoffice while letting
   a site like Nova pin the public-facing switches in `appsettings.json` so Live matches Test through
   the normal deploy. Manual checklist results, false positives and scans stay per environment (they
   describe that environment's content).
