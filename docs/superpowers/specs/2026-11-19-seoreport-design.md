# SEO Report package — Design

**Date:** 2026-11-19
**Package:** **`Esatto.Umbraco.Backoffice.SeoReport` 1.0.0** (new, public, nuget.org, `umbraco-marketplace` tag)
**Repo dir:** `Esatto.Umbraco.Backoffice.SeoReport/` + `Esatto.Umbraco.Backoffice.SeoReport.Tests/`
**Source being packaged:** the SEO Report dashboard in `c:\src\Esatto.Web.Nova` (Umbraco 18 on Umbraco Cloud):
`src/Features/Backoffice/Seo/*` (server), `src/wwwroot/App_Plugins/esatto/seo-dashboard.js` (client, one
2,154-line file), `libs/Manadsrapport.Core/*` (GA4 + Search Console agents and the report mapper), and its
unit tests `tests/Esatto.Web.Nova.Tests/Unit/Features/Seo*Tests.cs` (~1,700 lines). Originally written by
a colleague, restyled by Carl on 2026-11-09. Nova's own `tasks/session-log.md` and `tasks/lessons.md`
record the reasons behind many of its rules (search them for "SEO" before changing behaviour).
**Closest sibling to mirror:** `Esatto.Umbraco.Backoffice.Seo` (SEO Health; spec `2026-11-09-seo-design.md`):
project layout, TypeScript/Vite client, tables + migration plan, settings service, composer, csproj.
`Esatto.Umbraco.Backoffice.NavigationOrder` is the precedent for the English-keys localization files.
**Test sites:** `c:\src\umbracokalaset26` (Umbraco 18.2.1, clean site) for the install-from-scratch run;
`c:\src\Esatto.Web.Nova` for real GA4/Search Console data (see *Nova adoption*).

## Summary

Carl wants Nova's SEO Report shipped as a NuGet package that **anyone can install from nuget.org** on an
Umbraco 18 site, **totally standalone** ("fristående"): no Esatto-specific setup, no dependency on Nova,
on the `Manadsrapport.*` libraries or on any other Esatto package. Today the report is site code: the GA4
property, the Search Console property, the brand pattern and the Google key are set in `appsettings` and a
Cloud Shared Secret, and every text is Swedish. A stranger can't be asked to do that, so the package needs
a **Settings tab with a step-by-step setup guide and a Test connection button**, English texts, and
**encrypted storage of the Google key**.

What the dashboard is (unchanged in purpose): a month-by-month report of how the site does in search,
from Google Analytics 4 and Google Search Console: visits from search engines against the year before,
KPI tiles, trend charts, search queries and landing pages, an analysis (movers, opportunities, target
queries, a deterministic written summary), daily traffic, a "to fix" list, and an Analytics-vs-Search-Console
comparison. An editor can add a note per month. Reports are fetched in the background, archived, and
browsable by month.

### Relation to what already exists

- **SEO Health** (`Esatto.Umbraco.Backoffice.Seo`) scans the site's own pages and needs no Google data. This
  package reads Google's data and doesn't scan pages. They are complementary; dashboards are labelled
  **SEO Health** and **SEO Report** and live side by side in Content. No dependency either way.
- **Nova's in-site copy** is replaced by this package later (see *Nova adoption*). The SEO Health spec said
  Nova's SEO tab "stays as it is" as site code; this design deliberately reverses that.
- **Accessibility** and other packages: no relation.

### Decisions

| Question | Decision | By |
|---|---|---|
| Who installs it | Anyone from nuget.org. Each site creates **its own** Google service account; nothing Esatto-specific | Carl |
| Standalone | The Google agents, mapper and logic are **ported into the package**; no project/package reference to Nova or `Manadsrapport.*` | Carl |
| How to build it | **Rebuild on the SEO Health skeleton** (TypeScript/Vite client with one element per tab, own database tables + migration plan, settings service), porting Nova's logic into it. Not a copy of Nova's single JS file | Claude recommended, Carl chose |
| Language | **English only.** Every user-facing text (UI *and* server-produced text) is a **translation key** with an English text in one file (`Client/src/lang/en.ts`, registered as a `localization` extension for `en`). **No Swedish file.** Adding a language later = one more file + one manifest entry, no code change (same mechanism as NavigationOrder) | Carl |
| Sensitive data | The Google service-account key is **encrypted at rest** (see *Security*), never returned to the browser, never logged | Carl's requirement; mechanism is Claude's call |
| Where the key can come from | **Either** configuration (`Esatto:SeoReport:ServiceAccountJson` or `...:ServiceAccountKeyFile`; wins, shown read-only) **or** pasted/uploaded in the Settings tab (stored encrypted). Pasting needs no config file and no restart | Claude's call (Carl asked for encryption; this makes encrypted DB storage the default path for third parties and keeps config for Cloud Shared Secrets) |
| Package name | `Esatto.Umbraco.Backoffice.SeoReport` (`...Seo` is SEO Health) | Claude's call |
| Dashboard | Label **"SEO Report"**, pathname `seo-report`, alias `Esatto.Umbraco.Backoffice.SeoReport.Dashboard`, element `esatto-seo-report-dashboard`, weight -105, Content section. Different pathname from Nova's `seo`, so both can exist during the transition | Claude's call |
| Umbraco / .NET | **Umbraco 18 only**, net10.0 (matches the siblings) | Claude's call |
| GA4 client | **`Google.Apis.AnalyticsData.v1beta`** (stable, REST) instead of Nova's `Google.Analytics.Data.V1Beta` 2.0.0-beta10. A stable nuget.org package can't depend on a prerelease (NU5104). Search Console stays on `Google.Apis.SearchConsole.v1` (already stable) | Claude's call |
| Storage | **Own tables** created by an Umbraco migration plan, like SEO Health. Not `umbracoKeyValue` (Nova's choice; it has no delete and a 256-character key limit) | Claude's call |
| Settings storage | Database; `Esatto:SeoReport:*` configuration wins and shows read-only ("set in configuration"), same as SEO Health | Claude's call |
| Who sees what | Reading the report and Refresh: users with **Content** access. Settings tab, saving settings, Test connection: users with **Settings** access. "To fix" list: restricted to chosen user groups (setting; empty = everyone with Content access), as in Nova | Claude's call (mirrors SEO Health and Nova) |
| Multi-site | **One GA4 property and one Search Console property per installation** in 1.0 | Claude's call |
| Report contract | **Trimmed:** drop the `keywords`, `positions`, `deliverables`, `actions` sections, `agency`, and `client` (they belong to Nova's standalone monthly-report CLI; the dashboard doesn't read them). Texts become keys + parameters (see *English keys*) | Claude's call |
| Brand terms | Entered as a **list of terms**; the package escapes them and builds the RE2 pattern itself (Nova asks for a raw RE2 regex) | Claude's call |
| Public (front-end) summary | Nova's member-page summary (`SeoSummary` view component) is Nova-specific and **stays in Nova**. The package exposes a **public report service interface** so a site can build its own | Claude's call |
| Stored reports | Aggregated traffic/query data, **not encrypted** (it is not a secret and lives in the database like other site data). Only credentials are encrypted | Claude's call — flagged for Carl |
| Release scope | **Package first** (1.0.0 verified on a clean site). Nova adoption is a separate step afterwards | Claude's call |
| First fetch after setup | Saving settings whose connection passes (and a **Fetch now** button) starts the backfill **immediately, in the background, on the instance that received the request**. Only the *scheduled job* is limited to the scheduling server role; user-initiated fetches aren't. A lock row in the database stops two instances fetching the same month. The daily job stays as the safety net (see *First fetch*) | Claude's call (reviewer question) |
| Test connection | Runs on **the values in the form, before saving**. The request body may carry a newly entered key; a blank key field means "use the saved or configured key" | Claude's call (reviewer question) |
| Who may edit month notes and target queries | Users with **Content** access, as in Nova | Claude's call (reviewer question) |
| If Data Protection misbehaves on a host | Configuration always works and is the documented path for hosts that don't keep or share Data Protection keys; the tab degrades to "enter the key again". **No second protector implementation in 1.0** | Claude's call (reviewer question) |
| Checking Data Protection on Umbraco Cloud | **Ship 1.0.0 with no public pre-release**; verify on Nova during adoption. Until verified, the README names configuration as the documented path on Cloud and says a key saved in the tab works wherever the host keeps its Data Protection keys; a patch release corrects the docs if the check says otherwise | Carl (option B) |
| Credentials and data for the GA4 proof and the clean-site run | A **new dedicated read-only service account**, added as Viewer on a GA4 property and in Search Console (this is also a dry run of the package's own setup guide). Real API responses stay **on disk, out of git**; only small synthetic fixtures in the verified shape are committed. **The site owner does the Google console steps** (service account, API enablement, Viewer access on GA4 and Search Console) **and the live testing**; getting the key onto the build machine (local file or user secrets, never git or chat) is a gate before the GA4 proof | Carl agreed |

### Out of scope (1.0)

- Any non-English language file, or a language setting. (Keys exist; files don't.)
- Several GA4 / Search Console properties per installation (multi-site).
- "Sign in with Google" (OAuth). Service account only.
- A front-end/public summary component; e-mail or other alerts; BigQuery export; PDF export.
- Writing anything back to Google.
- Umbraco 17.
- A migration tool for Nova's archived reports, notes and target queries (handled in *Nova adoption*).

### Anti-goals (would make this a failure even if it technically works)

- Anything that only works on esatto.se (hard-coded property, Swedish text, Nova service, Esatto account).
- **The key ever visible**: returned by an API, written to a log, shown in an error, in the page source.
- A misconfigured property or site URL that gives **silently empty results**. Nova's own docs call this out:
  the wrong Search Console form returns empty data without an error. The package must detect and say so.
- A raw exception, stack trace or "500" in front of an editor.
- Needing a site restart after entering the key (Nova reads its secret once at startup).

### Success

Someone who has never seen Nova installs the package on a clean Umbraco 18 site, follows the guide in the
Settings tab, pastes the key, enters the two IDs, clicks **Test connection**, then **Save**, and sees their own GA4 and
Search Console data (latest month first, the rest backfilling) within about fifteen minutes, without editing a config file and without a restart.

## What the backoffice user sees

**Content section → dashboard "SEO Report".** All the behaviour of Nova's dashboard today is kept (see
*Behaviour to keep*), with these changes only: English texts through keys, the Settings tab, the
"not connected" state, and the trimmed contract.

### Layout

Same visual system as Nova's current SEO Report, SEO Health and Accessibility: overview card with a ring
(engaged visits %), KPI tiles with round icon badges, underlined section tabs, every block a card with an
icon heading, `--seo-*` tokens that hold in light, dark and high-contrast themes; month picker; fetched-at
chip; **Refresh** for the selected month. One tab strip:

**Analysis · Traffic · Queries · Most clicked · To fix · Analytics vs Search Console · Settings**

(Nova's tab names, translated: *Analys, Trafik, Sökfrågor, Mest klickat, Att åtgärda, Analytics mot Search
Console*.) "To fix" appears only when there are findings and the user may see them (Nova's existing
data-driven rule). **Settings** appears only for users with Settings-section access.

### Not connected yet

When no key, GA4 property or Search Console property is set, or the first fetch hasn't happened, the
dashboard shows a **"Connect Google to get started"** card instead of empty charts: users with Settings
access get a button to the Settings tab; everyone else is told an administrator needs to set it up. When
connected but the first fetch is still running (a one-time backfill of up to 13 months), show progress, not
an empty report.

### Settings tab

1. **Connection**
   - **Google Analytics 4 property ID.** Accepts `123456789` or `properties/123456789`; stored as the
     latter.
   - **Search Console property.** A choice of *Domain property* (user types `example.com`, stored as
     `sc-domain:example.com`) or *URL-prefix property* (user types `https://www.example.com/`, trailing
     slash enforced). This removes Nova's silent wrong-form pitfall.
   - **Google service-account key.** Write-only: a paste box and a "choose .json file" button (the file is
     read in the browser and sent as text). After saving it shows *Key saved · service account
     `name@project.iam.gserviceaccount.com`* with **Replace** and **Remove**; the key itself is never shown.
     If the key comes from configuration, the field is replaced by "Set in configuration" (read-only).
   - **Test connection** button: checks the key, then GA4, then Search Console, and reports each separately
     with an actionable message (see *Errors*). Shows the service-account email so the user knows what to
     add in Google.
2. **How to connect** (collapsible guide, text from `en.ts`): create or pick a Google Cloud project →
   enable *Google Analytics Data API* and *Google Search Console API* → create a service account and a JSON
   key → add the service account's email as a **Viewer** in GA4 (Admin → Property access management) and as
   a user in Search Console (Settings → Users and permissions) → paste the key and enter the two IDs →
   Test connection. Links to Google's own documentation. (Google's console wording changes; the builder
   verifies the steps against the current UI.)
3. **Report**
   - **Brand terms**: one per line (e.g. `acme`, `acme ab`). Used to split brand from non-brand queries.
   - **Display name** (optional): shown in the header subtitle; empty = omitted.
   - **Who sees "To fix"**: pick user groups; empty = everyone with Content access (Nova's rule: a forgotten
     setting must never hide the list).
4. **Schedule**: automatic fetching on/off (default on). The archive depth is fixed at 13 months (the
   smallest depth that gives year-on-year per page for every month, and below Search Console's 16-month
   window); it is not a setting.

Values pinned in `Esatto:SeoReport:*` configuration show read-only with "set in configuration".

### Behaviour to keep (unchanged from Nova)

Read the Nova code and tests for the exact rules. The ones that matter most, so they aren't lost in the
port: reports are archived **one per month** and never overwritten by the next month; a fetch never happens
because someone merely opened the dashboard (explicit actions fetch: Refresh, saving settings, Fetch now); the refresh job asks "which months are missing?" rather than "is the
data old?", only the *scheduled* job is limited to the scheduling server role (user-initiated fetches run on the instance that got the request), and it waits five minutes after startup (see *First fetch*);
one fetch at a time (a semaphore around one month); the period is `YYYY-MM` and validated (it becomes part
of a storage key); `IsUpToDate` is "the expected latest month is archived", not an age in hours; periods are
listed oldest first; the ring shows a real percentage (engaged visits), never an uncapped number; Search
Console data lags a few days and `ExpectedLatestPeriod` accounts for it; the daily series is cached 30
minutes with the date in the cache key; `FindingsUserGroups` matching is by alias, case-insensitive, and
only filters the API response (the archive keeps the whole report); positions of 0 mean "no value", not best;
accessibility decisions in the client (tab keyboard navigation, a visually hidden data table after every
chart, status never conveyed by colour alone).

## English keys (the localization design)

- **The server never sends prose.** Everything Nova's mapper, narrative, opportunities and target analysis
  produce as Swedish text (KPI labels and descriptions, chart titles and subtitles, annotation titles and
  texts, the narrative sentences, the daily-chart labels, error messages) becomes **a stable key plus
  parameters** (numbers, page paths, query terms, percentages), for example an annotation
  `{ key: "noKeyEvents", severity: "warning" }` or a narrative statement
  `{ key: "nonBrandShare", args: { percent: 62 } }`.
- **The client turns keys into text** through Umbraco's localization system, using the same convention as
  NavigationOrder: texts under `esattoSeoReport` in `Client/src/lang/en.ts`, looked up as
  `esattoSeoReport_<key>`, function-valued entries for text that needs numbers or plurals. English is the
  fallback for every backoffice language, so users with another language see English until someone adds a
  file.
- **Dates and numbers are formatted in the client** from raw values with `Intl` and the backoffice culture
  (so no server-built "Oktober 2026" label). The contract carries `YYYY-MM`.
- **API errors** use ProblemDetails with a stable `code` the client maps to a key.
- **A test must fail** if the server can emit a key that has no English text (and, in the client, if a
  referenced key is missing). The exact mechanism is left to the builder.
- Nova's Swedish texts are the **source for the English wording**: translate, don't invent; keep the
  analytical meaning of each sentence (the narrative's rule: no sentence without support in the numbers).

## Security

**What's sensitive:** the Google service-account key (it contains a private key that grants read access to
the customer's analytics). Settings values (property IDs, brand terms) and archived reports are not secrets.

- **Encrypted at rest.** The key is protected with **ASP.NET Core Data Protection**
  (`IDataProtectionProvider`, a purpose string specific to this package and version, e.g.
  `Esatto.Umbraco.Backoffice.SeoReport.ServiceAccountKey.v1`). Only the ciphertext is stored (its own column
  or row, separate from the plain settings JSON). The plain JSON exists only in memory while a Google
  credential is built.
- **Write-only.** No endpoint returns the key. The settings API returns
  `{ hasKey, source: "configuration" | "database", serviceAccountEmail }`. The email is not secret and is
  what the user adds in GA4 / Search Console. Settings endpoints send `Cache-Control: no-store`.
- **Validated on save.** Must be JSON with `type: "service_account"`, a `client_email` and a `private_key`;
  size-capped (suggest 16 KB); rejected with a message that doesn't echo the content.
- **Never logged.** Not in logs, exceptions, ProblemDetails or the dashboard. Exceptions from the Google
  libraries are wrapped so a key fragment can't leak through a message. Logging that the key was changed or
  removed (user name, time) is fine; Umbraco's audit log is an option (builder's call).
- **Precedence.** Configuration (`ServiceAccountJson`, or `ServiceAccountKeyFile` for hosts that mount a
  file) wins over the stored key; the tab says which is in force. Config is for Umbraco Cloud Shared Secrets
  and read-only hosts; the tab is for everyone else.
- **Key can't be decrypted** (key ring changed, database restored into another environment): treat as "no
  key", log once at warning, show *"The saved key can't be read in this environment. Enter it again."* in
  the dashboard and Settings tab. Never throw, never loop the refresh job on it.
- **Takes effect immediately.** Saving or removing the key invalidates the cached credential and clients;
  no restart. (Configuration-supplied values are read through `IOptionsMonitor`.)
- **Data Protection key ring.** The package relies on the host's Data Protection setup, which Umbraco
  already needs. On a load-balanced host the key ring must be shared (same requirement as Umbraco's own
  cookies and tokens); the README says so. **Verify on Umbraco Cloud (Nova) during adoption, after 1.0.0 is released** (no public pre-release) that a key
  saved in one environment survives a restart and a deploy, and what happens after a database copy.
- **Authorization.** Settings, key and Test connection endpoints require **Settings-section** access;
  report endpoints require Content access; the controller carries the attributes itself (Nova had a bug where
  a stray declaration moved `[Authorize]` and the route to the wrong class; its routing test is the
  precedent, port it).
- **Transport.** Calls to Google only over HTTPS via Google's client libraries; no secret in a URL.
- **Test connection runs on the values in the form, before saving.** The request body may carry a newly
  entered key (HTTPS, Settings access only; never echoed or logged); a blank key field means "use the saved
  or configured key". It is rate-limited (single-flight, a few seconds apart) so it can't be used to burn
  Google quota.

## Errors

The dashboard and Test connection never show a raw exception. Map failures to stable codes with an
actionable English message each (keys): `notConfigured`, `keyUnreadable` (decrypt failed), `keyInvalid`,
`apiDisabled` (which API, with a link to enable it), `noAccess` (which source, and the service-account email
to add), `propertyNotFound` (GA4 ID or Search Console property wrong), `noData` (connected but empty: say
so, don't render empty charts as if it were a result), `quota`, `unknown` (detail goes to the log with a
correlation id, not to the screen). The "wrong Search Console form gives empty results" case must land in
`propertyNotFound` or `noData`, never in a silent empty report.

## Architecture

Mirror SEO Health. Indicative layout (the builder may adjust names):

```
Esatto.Umbraco.Backoffice.SeoReport/
  Client/                      Vite + TypeScript + Lit, built into wwwroot/App_Plugins/<PackageId>/
    src/ seo-report-dashboard.element.ts, tabs/*.element.ts (one per tab), api.ts, types.ts, format.ts,
         styles, lang/en.ts
    public/umbraco-package.json   dashboard + localization(en) extensions
  src/
    SeoReportComposer.cs, ServiceCollectionExtensions.cs, migration run at startup
    Google/       GA4 agents (REST), Search Console agent, credential source (decrypts / reads config)
    Reporting/    report model (trimmed), mapper (from TrafficMapper), narrative, opportunities, movement,
                  target analysis/history, visibility, daily chart, findings filter, period value type
    Persistence/  tables, store, migration plan
    Settings/     settings model, settings service (DB + config override), secret protector, connection tester
    Jobs/         refresh job
    SeoReportController.cs (+ settings endpoints)
Esatto.Umbraco.Backoffice.SeoReport.Tests/
```

- **Composer** auto-registers services, the recurring job (server-role gated, as Nova's `SeoRefreshJob`),
  and the migration plan, like `SeoComposer` in SEO Health. No consumer-side wiring.
- **API:** Management API route `esatto/seo-report`, endpoints equivalent to Nova's (`report`, `refresh`,
  `analysis`, `daily`, `daily/sources`, `note`, `targets`) plus `settings` (GET/PUT), `settings/test`.
- **Persistence:** tables for archived reports (period, JSON, fetched-at, contract version), per-month notes,
  target queries, settings, and the protected key. Must work on **SQL Server and SQLite** (Umbraco's
  databases): SEO Health 1.0.1 fixed a SQL-Server-only break, so test both. A report whose contract version
  is older than the code's is treated as missing and refetched. One table per concern or a small document
  table is the builder's call.
- **Period** is one value type with validation, replacing the copies of `IsValidPeriod` in Nova's services.
- **Cloud/multi-instance:** the archive lives in the database so instances share it; only the scheduling
  server role fetches.
- **Not configured:** the refresh job does nothing and logs once at Information, not an error per day.
- **First fetch.** Saving settings that pass the connection test, and the **Fetch now** button, start a
  background backfill straight away (newest month first), on whichever instance handled the request. A
  database lock row keeps two instances from fetching the same month. The dashboard shows progress
  ("month 3 of 13") and any failure, never an empty report. The scheduled job stays as the safety net and
  for new months: it asks "which months are missing?" and, since waking costs nothing, may wake more often
  than daily while the archive is incomplete. Constraints: a failing month isn't retried more often than
  hourly, and a failure is visible in the dashboard. The wake interval and backoff are the builder's call.
  Opening the dashboard never fetches.
- **GA4 port risk:** Nova's `Ga4Agent` builds gRPC request types and parses the response as raw JSON
  (`MapRaw`). Moving to the REST client changes the request types and may change the response JSON shape.
  The mapper's output must be proven identical for the same input (see *Testing*) before anything else is
  built on it.
- **Google quota:** a full fetch of one month is about 46 API calls (Nova's own measure) and takes ten-plus
  seconds; the first connect backfills up to 13 months. Keep the single-flight gate, and keep the
  "one month at a time, newest wanted first" order so the month someone is looking at lands first.
- **Comments and docs in the ported code are English.** Do the targeted DRY / clean-code / best-practice pass
  the repo conventions ask for, on the code that's ported, without changing behaviour.

## Testing

xUnit project mirroring SEO Health's (`net10.0`, `IsPackable=false`, NSubstitute, `InternalsVisibleTo` is
used by SEO Health; follow it). Vitest for pure client helpers.

- **Mapper characterization:** build a whole report from **synthetic GA4/Search Console rows** (Nova's
  lessons describe doing exactly this) and assert the numbers. Prove the REST GA4 client produces the same
  rows/numbers as the gRPC one for the same inputs: a one-time live comparison on the same property with the
  new read-only service account, **before anything else is built on the REST client**. Real responses stay on
  disk, out of git (the repo is public and they contain esatto.se's real queries); only small synthetic
  fixtures in the verified shape are committed.
- Port Nova's ~1,700 lines of SEO tests (movement, narrative, opportunities, target analysis/history,
  visibility, daily chart, findings filter, notes, controller routing/authorization). Assertions on Swedish
  strings become assertions on keys and arguments.
- **Secrets:** protect/unprotect round trip; the decrypt-failure path (wrong key ring) yields "no key" and
  no exception; key-JSON validation (valid, wrong `type`, missing fields, too big, not JSON); the settings
  GET never contains the key, and a test that searches every serialized API response and log message for a
  known marker string from the key and fails if found.
- **Settings:** configuration overrides database and shows as locked; ID/URL normalization (`123` →
  `properties/123`; `example.com` → `sc-domain:example.com`; URL-prefix gets its slash); brand terms →
  escaped RE2 pattern (check how the Search Console agent applies case; Nova's `ValidateBrandRegex` is the
  starting point).
- **Keys:** a test that fails when the server can emit a key without an English text.
- **Manual / browser:** clean site `umbracokalaset26`: install the package from a local feed, follow the
  in-tab guide with a real Google service account, verify not-connected state, wrong-ID errors, Test
  connection per source, paste key (no restart needed), report appears, month switching, refresh, Settings
  hidden from a non-Settings user, "To fix" hidden from a non-listed group, light/dark/high-contrast.
  Verify on SQL Server and SQLite. Check on Umbraco Cloud via Nova (Data Protection survival).

## Packaging

Per `esatto-packages-conventions` and the SEO Health csproj: Razor SDK, `StaticWebAssetBasePath=/`,
`BuildBackofficeClient` target (`npm install` + `npm run build` before build), `icon.png`, README with
screenshots (absolute raw-GitHub URLs, never relative), **source-code link** (`RepositoryUrl` = repo root,
`PackageProjectUrl` = `.../tree/main/Esatto.Umbraco.Backoffice.SeoReport`, `RepositoryType` git), tags
including `umbraco-marketplace;umbraco-18`, a detectable `Umbraco.Cms.*` dependency (Api.Management 18.0.0 and
Web.Common 18.0.0 minimums, as SEO Health), the same "Security overrides" and `NuGetAuditSuppress` blocks as
SEO Health if the same advisories apply, the `AutoPush` target off by default. Versioning by MinVer from the
git tag `Esatto.Umbraco.Backoffice.SeoReport-1.0.0`. **`dotnet pack` must not warn NU5104** (no prerelease
dependency). README covers: what it does, install, the Google setup steps, where the key can live, the Data
Protection note (on Umbraco Cloud, configuration is the documented path until verified there; a key saved in the tab works wherever the host keeps its Data Protection keys), what it doesn't do.

The work is prepared, committed, tagged and packed by Claude **only with Carl's explicit approval at each
step**; **Carl runs `dotnet nuget push`.** The nuget.org API key glob is `Esatto.*`, so a first push of this
ID is covered (the key itself expires around 2026-11: check it before releasing).

## Nova adoption (separate step, after 1.0.0 is released and verified)

Not part of the package deliverable, listed so nothing is forgotten:

- Reference the package; remove Nova's `Features/Backoffice/Seo`, the `Esatto.Dashboard.Seo` manifest entry,
  `seo-dashboard.js`, and `Manadsrapport.Core` / `Manadsrapport.Checks` if nothing else uses them.
- **Nova's `SeoSummary` member-page component** reads Nova's `ISeoReportService` and the Swedish-text report.
  After adoption it must read the package's public service and **supply its own Swedish for the keys**. This
  is real work and the main cost of adopting; decide then whether to keep, rewrite or retire it.
- Move the Google key from Nova's Shared Secret to the package's setting (paste in
  the tab, or `Esatto__SeoReport__ServiceAccountJson`), and the GA4/Search Console IDs and brand terms.
- Copy Nova's per-month notes and target queries (kept in `umbracoKeyValue`) into the package's tables; the
  archived reports need no migration (the 13-month archive can be refetched from Google).
- **Verify Data Protection on Cloud:** a key saved in the tab survives a restart and a deploy; after a
  database copy to another environment the tab says "enter the key again" and nothing crashes. If the result
  differs from the README's wording, correct it in a patch release.
- Until then Nova keeps its own copy; test the package on the clean site, not on Nova, so Nova doesn't show
  two SEO Report tabs.

## Left to the builder

File and class names within the layout above; table design; how the client tab elements share state;
the exact keys and English wording; threshold constants carried over from Nova; which Nova helper code can
be simplified when ported; the mechanism for the key-completeness test; whether to use Umbraco's audit log
for key changes; how the setup guide is laid out; wording of Google's console steps (verify against the
current UI).
