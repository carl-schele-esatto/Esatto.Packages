# Esatto.Umbraco.Backoffice.SeoReport

SEO Report for Umbraco 18: a dashboard in the Content section that turns Google Analytics 4 and Google Search Console into a monthly SEO report, and explains in plain English what changed.

## What it does

- **Archives one report per month**, for the last 13 months: visits, search visibility, the top pages and the top search queries. A month is fetched once and kept in your database, so the history is there even after Google stops serving it.
- **Compares every month** with the month before and with the same month last year.
- **Explains what changed** in sentences ("organic visits rose 12 % while clicks from Google fell"), with the figures behind them.
- **Shows the daily curve** of the month on the Traffic tab, straight from Google Analytics.
- **Splits brand from non-brand search** when you give it your brand terms.
- **Follows target queries and notes.** List the queries you want to be found for, and see for each one whether it is not visible, ranking deep, close to page one or performing, with advice on what to do. Write a note on any month.
- **Lists what to fix** ("To fix") and the quick wins, for the user groups you choose.
- **Connects with a Google service account.** Everything is set on a **Settings** tab with a step-by-step guide and a **Test connection** button, with no configuration file and no restart. The key is stored encrypted.

## What it does not do

- One Google Analytics 4 property and one Search Console property per site. No sign-in with Google (OAuth); a service-account key is required.
- It never writes anything to Google, and sends no e-mail or alerts. There is no PDF export and no front-end component.
- The texts are English only. Other backoffice languages show the English text.
- It doesn't scan your pages. For that, see [Esatto.Umbraco.Backoffice.Seo](https://www.nuget.org/packages/Esatto.Umbraco.Backoffice.Seo) (SEO Health), which needs no Google account and can be installed beside this one.

## Requirements

Umbraco 18.0.0 or later (.NET 10), and a Google account that can add users to your Google Analytics property and to your Search Console property.

## Install

```bash
dotnet add package Esatto.Umbraco.Backoffice.SeoReport
```

Start the site. The dashboard appears in the **Content** section as **SEO Report**. The package creates its own database tables on first start. Nothing needs to be registered in code.

## Connect to Google

The dashboard shows a **Connect Google to get started** card until this is done. Users with access to the Settings section get a button to the **Settings** tab, which holds the same steps with links to Google's own help pages:

1. In the Google Cloud console, create a project (or use one you have).
2. In that project, enable **Google Analytics Data API** and **Google Search Console API**.
3. Create a **service account** (it needs no roles in Google Cloud) and download a **JSON key** for it. If Google Cloud says an organization policy does not allow keys, ask your Google Cloud administrator.
4. Give the service account read access with its e-mail address (`client_email` in the key file):
   - In Google Analytics: **Admin → Property → Access management → Add users**, role **Viewer**.
   - In Search Console: the property's **Settings → Users and permissions → Add user**, permission **Restricted**. Only an owner of the property can do this.
5. On the Settings tab, paste the key or choose the file, and enter:
   - the **GA4 property ID**, as `123456789` or `properties/123456789` (the number under Admin → Property details; a measurement ID such as `G-XXXXXXX` is not a property ID);
   - the **Search Console property exactly as Search Console lists it**: a domain property (`example.com`, stored as `sc-domain:example.com`) or a URL-prefix property (`https://www.example.com/`, with the closing slash). A property written in the wrong form is reported as "property not found".
6. Click **Test connection**. It checks the key, Google Analytics and Search Console one by one and says what to fix. Then click **Save settings**: the report starts fetching the 13 most recent months in the background, newest first, and shows progress as it goes.

## Where the key can live

| Where | How |
| --- | --- |
| **The Settings tab** | Paste the key. It is encrypted with ASP.NET Core Data Protection before it is stored, is write-only (it is never sent back to the browser and never written to a log), and can be replaced or removed there. A key is at most 16 KB. |
| **Configuration** | `Esatto:SeoReport:ServiceAccountJson`: the whole key as a **string**, not as a JSON object (environment variable `Esatto__SeoReport__ServiceAccountJson`). |
| **A key file** | `Esatto:SeoReport:ServiceAccountKeyFile`: the path of the JSON file. Use an absolute path: a relative one is resolved against the process's working directory. |

Configuration wins over the tab: the tab then shows "Set in configuration" and the key field is read-only. A configured key that cannot be used is reported, not skipped. A key saved in the tab earlier stays stored, unused, while configuration supplies one.

A key pasted in the tab is only as durable as your **Data Protection key ring**. If the ring is lost or differs, the key can no longer be decrypted and the tab says the key cannot be read; enter it again (the archive is not touched). That happens in containers that don't persist the ring, on several instances that don't share one, when the content root or deployment slot changes, and when the database is copied to another environment. **On those hosts, and on Umbraco Cloud until you have checked that a saved key survives a restart and a deploy, supply the key through configuration.**

The package ignores the endpoints a key file names for itself and always talks to Google's own. A key that names any other token address is refused as invalid.

## Configuration

Everything on the Settings tab is stored in the database. Any value under `Esatto:SeoReport` in configuration wins and is shown read-only; changes in configuration are picked up within about a minute.

```json
{
  "Esatto": {
    "SeoReport": {
      "Ga4PropertyId": "properties/123456789",
      "SearchConsoleSite": "sc-domain:example.com",
      "BrandTerms": [ "acme", "acme ab" ],
      "DisplayName": "Acme",
      "FindingsUserGroups": [ "admin", "seoEditors" ],
      "AutoFetch": true,
      "ServiceAccountJson": "{ ...the whole key file as one string... }",
      "ServiceAccountKeyFile": "C:\\secrets\\seo-report-key.json"
    }
  }
}
```

- `BrandTerms`: a search query counts as a brand query when it contains one of the terms, in any letter case. You write terms, not a pattern; the package builds and escapes the expression. With no terms there is no brand split.
- `FindingsUserGroups`: the **aliases** of the user groups that see "To fix" and the quick wins. Empty means everyone who can open the report.
- `AutoFetch` (default `true`): whether the scheduled job fetches missing months by itself. **Fetch now** works either way.
- An invalid configured value is ignored with a warning in the log that names the field and never the value. An empty list does not lock its field.
- Limits: at most 50 brand terms of at most 100 characters (white space inside a term is collapsed), and the expression built from them at most 4,000 characters, so the last terms are left out if they don't fit; a display name of at most 200 characters; at most 50 user groups with aliases of at most 200 characters.

## What it fetches, and when

- **Opening the dashboard never fetches the archive.** It reads your database. The one exception is the daily card on the Traffic tab, which asks Google Analytics live and keeps the answer for 30 minutes.
- **A month is fetched five days after it ends** (UTC), by a background job that runs every 15 minutes (the first run is five minutes after the site starts) and only on the server that does the scheduling. It fetches only the months that are missing. After a failure it waits at least an hour. A month that came back with no data is looked at again after 24 hours.
- **A fetch is about 46 Google API calls per month**: three Google Analytics reports, and for Search Console a check, three calls per month of the 13-month curve and three top lists (250 queries, 200 pages, 1,000 query and page rows). The first backfill of 13 months is therefore roughly 600 calls, run one month at a time. Google Analytics' quota is shared with every other tool that uses the same property.
- **One fetch at a time per site**, across servers. If a server stops in the middle of a fetch, the next one can start after at most 30 minutes.
- **Refresh** fetches the selected month again (about fifteen seconds). Use it after changing brand terms (a change applies to months fetched afterwards) or when Google has revised a month.
- **The archive is never pruned**, but Search Console only holds 16 months: older months can't be refreshed, and year-on-year for Search Console is missing beyond that.
- **Changing the GA4 property or the Search Console site** makes the archived months of the old property invisible (they stay in the database and come back if you change it back) and fetches the 13 months again for the new one. Notes and target queries belong to the month, not to the property.
- Months follow Google's own time zones (the property's time zone in Google Analytics, Pacific time in Search Console). Search Console lags about three days, so the newest month appears a few days into the next one.

## Who sees what

| | Needs |
| --- | --- |
| Read the report, **Refresh**, **Fetch now**, write notes, edit target queries | access to the **Content** section |
| The **Settings** tab, saving settings, **Test connection** | access to the **Settings** section |
| "To fix" and the quick wins in the analysis | being in one of `FindingsUserGroups` (everyone, when none is chosen) |

Everyone who can read the report can see the GA4 property ID, the Search Console property and the brand expression it was built with (so a figure can be checked in Google's own tools). Only users with Settings access ever see the service account's e-mail address; nobody sees the key.

The archived reports (aggregated traffic and search-query figures from Search Console) are stored as plain JSON in your database, like other site data. Only the key is encrypted.

## Good to know

- A wrong Google Analytics ID or a missing grant reads as "no access", and the text also says to check the ID. A deleted or disabled key reads as "key invalid" (a wrong server clock can cause the same message).
- **Test connection** asks Google Analytics about yesterday, so a brand-new property shows "connected, no data yet" until it has had traffic.
- Internationalised domains: enter the Search Console property exactly as Search Console lists it (the `xn--` form).
- Progress of a running backfill is shown on the instance that runs it. Umbraco's guidance for load-balanced sites (one backoffice instance) applies.
- After switching between two high-contrast themes, a reload may be needed for the charts.
- Saving settings checks the connection first, which can take up to a minute: mind proxy timeouts in front of the backoffice.

## Uninstall

Removing the package leaves its data behind, so nothing is lost by accident. To remove it as well, drop the tables `esattoSeoReportReport`, `esattoSeoReportNote`, `esattoSeoReportDocument` (settings, the encrypted key, targets and fetch state) and `esattoSeoReportLock`, and delete the row in `umbracoKeyValue` whose key is `Umbraco.Core.Upgrader.State+Esatto.Umbraco.Backoffice.SeoReport`. Delete the service account's key in Google Cloud too.

## Supported surface

The package is used through its dashboard, the `Esatto:SeoReport` configuration keys above and `ISeoReportService` (with the report types it returns), which a site can inject to read the archive. Everything else is public only because Umbraco's controller model requires it or for testing, and may change in a minor release.

## Source

[github.com/carl-schele-esatto/Esatto.Packages](https://github.com/carl-schele-esatto/Esatto.Packages/tree/main/Esatto.Umbraco.Backoffice.SeoReport). MIT licence. The charts use [Chart.js](https://www.chartjs.org/) (MIT) and [@kurkle/color](https://github.com/kurkle/color) (MIT); their notices are in `THIRD-PARTY-NOTICES.md`, in the package and next to the script.
