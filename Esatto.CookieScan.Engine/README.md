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

## Composing a report as email

`ScanEmail.Compose` turns a finished scan into the message that reports it — subject, an HTML body,
a plain-text alternative, and the two report files as attachments:

```csharp
ScanEmailContent mail = ScanEmail.Compose(result);

// mail.Subject     "Cookie scan - example.com - 1 violation"
// mail.Html        table-based, every colour inline, safe in Outlook
// mail.Text        the same things in the same order, for clients that will not render HTML
// mail.Attachments cookie-scan-<host>-<stamp>.md and .json, as bytes
```

- **It composes; it does not send.** No SMTP, no network, no transport dependency — the engine gains
  nothing you did not already reference it for. Hand `mail` to whatever sender you like; the
  dashboard uses MailKit.
- **It takes a `ScanResult` and nothing else**, so a scan composes identically whether it finished a
  second ago or was read back out of the history folder.
- **Attachments are built in memory, never read off disk.** `cookie-scan-report.md` is one file in
  the report directory, overwritten by every run, so attaching it by path would send the wrong scan's
  report for anything but the latest — and nothing at all when that write had failed.
- **`ScanReportWriter.Markdown(result)`** renders the same report document from a result alone, which
  is what the markdown attachment is. `WriteFiles` and this produce identical bytes for the same run.
- **`EmailRecipients.Parse`** splits one typed field — commas, semicolons or newlines — into trimmed,
  de-duplicated addresses. It deliberately does not validate: there is one rule for what a mailbox is,
  and it belongs to whatever builds the message.

The subject's verdict is `N violation(s)`, `N to review`, or `clean`, in that order of precedence —
the same order `ScanResult.ExitCode` applies. The dry-run flag is deliberately not in it: whether the
policy page was written to is a different question, and it is answered in the body.

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
