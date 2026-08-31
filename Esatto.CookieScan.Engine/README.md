# Esatto.CookieScan.Engine

The cookie consent audit engine behind
[`esatto-cookiescan`](https://www.nuget.org/packages/Esatto.CookieScan.Cli). Crawls a site with a
real headless browser, replays it once per consent decision a visitor can make, and reports every
cookie and storage entry set outside the consent it needed.

**Install this package to build your own front end.** To just run a scan, install
`Esatto.CookieScan.Cli` and run `esatto-cookiescan`.

## Install

```bash
dotnet add package Esatto.CookieScan.Engine
```

## Using it

```csharp
using Esatto.CookieScan.Engine;

var options = new ScanOptions(
    Url: new Uri("https://example.com"),
    Target: new Uri("https://example.com"),
    MaxPages: 25,
    Locale: Locale.Sv,
    MemberEmail: null,
    MemberPassword: null,
    ClientId: null,
    ClientSecret: null,
    DryRun: true,
    ReportDir: Environment.CurrentDirectory,
    Headed: false);

IScanLog log = new MyLog();

ScanResult? result = await new ScanRunner(options, () => CatalogueSource.Load(log), log)
    .RunAsync(CancellationToken.None);
```

- **`ScanRunner` is one class on purpose.** Every front end runs the same runner, the same passes,
  the same violation rule and the same catalogue. A window that found something different from what
  CI gates on would be worse than no window.
- **`IScanLog` is the only output seam.** Implement it to put progress wherever you like — a console
  writes lines, a desktop dashboard posts messages to a page. The engine knows neither.
- **`RunAsync` returns `null`** when discovery found no pages. Reporting an empty scan as a
  successful one would be a lie about coverage.
- **`ScanResult` is round-trippable JSON** via `ScanJson` — no computed collections, no types
  `System.Text.Json` cannot rebuild — because the same document is the report file, the history
  entry and the message a UI renders.

## Playwright

The engine uses `Microsoft.Playwright`, which needs a browser on the machine that runs it. If you
publish a **self-contained single file**, three properties are not optional:

```xml
<PublishSingleFile>true</PublishSingleFile>
<IncludeNativeLibrariesForSelfExtract>true</IncludeNativeLibrariesForSelfExtract>
<IncludeAllContentForSelfExtract>true</IncludeAllContentForSelfExtract>
```

The first two are the usual advice: Playwright ships native libraries a single-file bundle cannot
load from memory. The third is the one people miss — Playwright's *driver* is a whole Node.js runtime
plus its own JS files under `.playwright\`, which the SDK classifies as content rather than as a
native library. Without it the exe builds, runs, and then fails at the first browser launch with
*"Microsoft.Playwright assembly was found, but is missing required assets."*

## Writing findings back

`ManagementApiClient` posts a scan's findings to an Umbraco site running
[`Esatto.Umbraco.Backoffice.CookieScan`](https://www.nuget.org/packages/Esatto.Umbraco.Backoffice.CookieScan),
authenticating with client credentials. The merge is append-only and never publishes — see that
package for why.

## Where it keeps things

| What | Where |
| --- | --- |
| Scan history (last 50) | `%LOCALAPPDATA%\Esatto.CookieScan\scans` |
| Reports | Wherever `ScanOptions.ReportDir` says |
| Catalogue override | `cookie-catalogue.json` beside the executable |

The override is resolved from `Environment.ProcessPath`, **not** `AppContext.BaseDirectory`: a
single-file build with full extraction reports the extraction directory under `%TEMP%\.net` as its
base directory, so resolving it there means the feature can never work in a published build.

## License

MIT.
