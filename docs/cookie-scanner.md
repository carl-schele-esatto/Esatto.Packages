# Cookie scanner

An audit tool for the cookie consent banner. It drives a real, headless browser through the site
six times, once per consent decision a visitor can make, and compares what actually gets set
against what the policy page already declares. Anything set outside the consent it needed is a
**violation**; anything set but undeclared becomes a **draft** addition to the policy page for an
editor to review and publish.

Five projects carry it, four of which ship as packages:

| Project | Ships as | Contains |
| --- | --- | --- |
| `Esatto.CookieScan.Core` | `Esatto.CookieScan.Core` | The pure rules — catalogue matching, category inference, the violation rule, the merge plan, duration formatting, scan diffing. No Umbraco, no Playwright, no HTTP, and no `PackageReference` at all, so both halves can be unit tested without a browser or a published content graph. |
| `Esatto.CookieScan.Engine` | `Esatto.CookieScan.Engine` | The engine: crawling, the six passes, the report, the write-back client, scan history. |
| `Esatto.CookieScan.Cli` | `Esatto.CookieScan.Cli`, a **dotnet tool** | The console front end, `esatto-cookiescan`. `dotnet tool install -g Esatto.CookieScan.Cli`, or publish it self-contained for a copy-anywhere exe. |
| `Esatto.CookieScan.Desktop` | *not packed* — a self-contained exe | `esatto-cookiescan-ui`, the dashboard: one WebView2 control filling a WinForms window, with a Scan page and a History page in place of stdout. Nothing about the scan itself is different. |
| `Esatto.Umbraco.Backoffice.CookieScan` | `Esatto.Umbraco.Backoffice.CookieScan` | The site side: the merge endpoint the tool posts findings to, and the API user that authenticates the post. |

The site side is a companion to `Esatto.Umbraco.Backoffice.CookieBanner`, whose `cookieDefinition`
element type and `cookiePolicy` page it writes into, and depends on it as a package.

The dashboard is the one half that is not a NuGet package, and deliberately: a WinForms window is
not a library anyone references, and making it a dotnet tool would trade the copy-anywhere exe — the
whole point of the thing an operator double-clicks — for a machine that has the Windows Desktop
runtime installed.

## What it does, and what it deliberately does not do

- It runs Chromium **on the machine that runs the tool**, never on the production server. The
  published exe is meant to be copied to a laptop and pointed at a URL; nothing about it runs
  in-process with the site.
- It never deletes or rewrites an existing declaration. The merge is append-only by construction —
  see `MergePlanner.Plan` and `CookieScanWriter.Append` — because a declaration's purpose text is
  legal wording an editor may have hand-written, and a tool that silently rewrote it would be worse
  than no tool.
- It never publishes. A successful write-back calls `IContentService.Save`, never `Publish`. A
  placeholder purpose on an unrecognised cookie must not become public legal text without a human
  reading it first.
- It never declares a `Pixel`. The storage-type dropdown the CookieBanner package offers has a
  `Pixel` option, but the scanner has no way to detect a tracking pixel from what a browser exposes
  and does not attempt to guess — see Known limitations, below.
- The only two forms it ever submits are the member login form and the cookie-consent decision
  itself (via the site's own `/api/cookie-consent` endpoint). Nothing here books a class, cancels a
  booking, registers a child, or completes a payment — see the TempData limitation below for why
  that matters.
- `/umbraco` is excluded from the crawl outright (`SiteCrawler.Exclusions.IsExcluded`). Backoffice
  cookies are not a visitor's cookies and have no business on a public policy page.

## The dashboard

`Esatto.CookieScan.Cli` installs as the `esatto-cookiescan` dotnet tool, or publishes as
`esatto-cookiescan.exe` — the console front end, and what CI runs. `Esatto.CookieScan.Desktop`
publishes as `esatto-cookiescan-ui.exe`, the dashboard — and the one to double-click. Neither holds
any scanning logic: both are front ends over `Esatto.CookieScan.Engine`, which is why they cannot
disagree. Both run the same `ScanRunner`, the same six passes,
the same violation rule and the same catalogue; the console tool prints what `ScanRunner` and
`ScanReportWriter` produce, and the dashboard puts the same `ScanResult` on a page instead.
`ScanRunner`'s own remarks say why this is one class rather than two similar ones: a window that
found something different from what CI gates on would be worse than no window.

**It is a web page in a desktop window, not a browser app.** The shell is a `net10.0-windows`
WinExe whose entire client area is one WebView2 control (`DashboardForm`). The page's files —
`index.html`, `app.css`, `app.js`, the Lit components, a vendored Lit and one woff2 — are embedded
resources inside the exe, served to the control over `https://app.localhost/` through
`WebResourceRequested`; nothing is fetched from the network and there is no build step. The page
and the host talk in one JSON envelope both ways (`PostWebMessageAsJson` out of the host,
`chrome.webview.postMessage` in), so `ScanSession` runs the same engine the CLI does and reports
progress as messages rather than as console lines.

The dashboard has three pages. **Scan** takes the CLI's options as fields — site URL, max pages,
locale, member email and password, client id — plus the client secret, which the CLI takes from the
environment instead; all of them picked in one go from the site dropdown above
them (see Site profiles, below), runs the scan with live progress in a log
panel (warnings called out), and fills four stat tiles plus a findings table where violations are
tinted red and `NeedsReview` candidates amber (`cs-findings-table`). A trend chart beside the run
card plots entries and violations across the last twenty scans of whatever site is in the URL
field. **History** lists every past scan newest-first (see Scan history, below), each row carrying
its result as a word on a pill (`clean`, `1 violation`, `write-back failed`) — the numeric exit code
itself lives in the row's `title` tooltip, not on the pill — shows any one of them in the same
findings table, and compares any two —
appeared, disappeared and recategorised cookies, as three groups from `ScanDiff.Between` (in
`Esatto.CookieScan.Core`) — with the pair ordered by completion time rather than by which row was
clicked first, and a warning when the two ran under different options, so a difference caused by
the options rather than by the site is not read as a change to the site. **Email** holds the one
mail account this machine sends reports from — see Emailing a report, below; who each report goes to
is set per site, on Scan.

Three differences from the CLI are deliberate. **Only the dashboard can email a report** — nothing in
the console tool sends mail, and the engine gained no mail dependency to make that possible later
without a decision. **Dry run defaults to on** in the dashboard, so the
obvious button to press cannot write to a live policy page; the console tool still defaults it off,
because a CI invocation names every flag explicitly. And the dashboard remembers its options
between runs — as site profiles, below — while the console tool takes flags fresh every time.

A `cookie-catalogue.json` beside *either* exe replaces the embedded catalogue exactly as before —
see Overriding the catalogue, below.

### Site profiles

The run card's dropdown holds one **profile per site**: the URL, max pages, locale, the dry-run
flag, the member email, the member password, the API client id, the API client secret, the
consent cookie name, and whether to email the report and to whom (see Emailing a report, below).
Picking one fills every field below it, including both masked credentials;
**New site…** clears them to the defaults (25 pages, SV, dry run on, email off and no recipients —
a new site must not inherit the last one's, since mailing a client's cookie report to a different
client is the one mistake here that cannot be taken back).

The consent cookie name is the one profile field stored **in clear** rather than under DPAPI. It is
the name of a cookie every visitor of that site already holds, so there is nothing to protect — and
a wrong value there turns the site's own consent cookie into a reported violation, which is a
mistake someone has to be able to spot by opening the file.

**Dry run is the one field no profile restores.** Every URL starts every session checked — including
a site whose profile was saved right after a real write-back, which is exactly the case where
restoring the stored value would hand back an unticked box and arm a write nobody asked for. From
then on the box is the operator's, remembered **per URL** for the rest of the session: untick it for
one site, glance at another, come back, and it is still unticked, with no save needed.

Every other field is refilled from the profile with unsaved edits discarded. Dry run is the
exception in both directions because it is the one field whose stale value is dangerous rather than
merely wrong — restoring a stored `false` arms a write, and re-arming a deliberate `false` between a
glance and a click makes the box untrustworthy. The value is still sent with every run and still
written to the profile, so the record of what a scan did stays accurate. **Save site** writes what the form currently holds, **Delete**
forgets the selected profile, and running a scan saves the profile for the URL it ran against — so a
site scanned once is a site you can pick next time without having typed anything extra.

Picking a profile from the dropdown is not itself remembered: browsing the saved sites changes
nothing on disk, and the window reopens on whichever site was last **saved or scanned** rather than
on whichever was last selected. Deleting the selected profile leaves nothing selected, so the next
launch opens on **New site…**.

The URL is the profile's identity as well as its label: there is no separate name to keep in step
with it. Two URLs are the same profile if they match trimmed and ignoring case. That makes editing
the URL of a selected profile and pressing Save a **save as** — the new URL matches nothing, so a
second profile appears and the original stays until it is deleted. Copying a set of options from
staging to production is the common case, and a window that silently renamed the original would
have destroyed what it was copied from.

Profiles live in `%LOCALAPPDATA%\Esatto.CookieScan.Engine\settings.json`, and **the member email, the
member password, the API client id and the API client secret are encrypted at rest** — as are the
mail account's username and password, which sit beside the profiles in the same file — with DPAPI
(`System.Security.Cryptography.ProtectedData`, `DataProtectionScope.CurrentUser`, plus a fixed
application entropy that is a namespace rather than a secret — see `ProtectedText`). Each is stored
as `"dpapi:<base64>"`, so a reader can tell ciphertext from a value the pre-profiles build wrote in
the clear.

What that is worth, precisely:

- **It protects** the file against another Windows account on this machine, against another
  machine, and against the file being copied anywhere else. All three produce a blob that will not
  decrypt.
- **It does not protect** against anything running as this Windows user. DPAPI hands that code the
  same plaintext it hands the dashboard, by design. This is at-rest protection for a convenience
  file, not a vault — a laptop left unlocked is exactly as bad as it was before.

A blob that will not open costs its own field and nothing else: it loads as empty, the rest of the
profile is kept, and the log gets one line on startup naming the site and the field to retype.
Neither the whole profile nor the whole file is discarded.

**The client secret is written there too**, and used not to be — see Where the client secret comes
from, below, for why the pair now travels together and what that costs.

An old flat `settings.json` from before profiles **migrates automatically**: its fields become one
profile, selected, with an empty password and an empty client secret (the old file stored neither).
Nothing is rewritten on read — the new shape is written the next time anything saves. A file written
by the profiles build that shipped *before* the secret opens the same way: the missing
`ClientSecret` key loads as an empty field, with no warning, because a key that was never written is
not a credential that was lost.

### Emailing a report

The dashboard can send a finished scan as an email — automatically when a scan completes, or by hand
from a button, for any scan including one out of the history. **The console tool cannot**: nothing in
`Esatto.CookieScan.Cli` sends mail, and the engine package gained no mail dependency to make that
possible later without a decision.

The settings are split in two, and the split is the shape of the whole feature:

| What | Where | Why |
| --- | --- | --- |
| The SMTP server, credentials and from address | **One per machine**, on the new **Email** page | This is who the operator *is*. Typed once. |
| Who the report goes to | **Per site**, in that site's profile on the Scan page | This is a fact about the client being scanned. |

Putting the server in the profile instead would mean typing six fields again for every site added,
to say the same thing every time. The recipients are two fields on the site, behind a checkbox, so a
profile that will never mail anybody does not grow two empty rows.

**What is stored, and how.** The mail account lives on `DashboardSettings.Email` — null until one is
saved, which is *not* the same as a blank one and is what a settings file from before this build
holds. Its **username and password are encrypted at rest with DPAPI**, exactly like the four
credentials on a profile and through the same `ProtectedText`. The host, port, security mode and from
address are stored **in clear**, and so are the per-site recipients: credentials are encrypted,
addressing is readable. A wrong recipient is precisely the mistake somebody has to be able to find by
opening the file — encrypted, its only symptom would be a report quietly arriving at the wrong
mailbox. It is the same argument `ConsentCookie` is stored in clear for.

A password that will not decrypt costs that one field and produces a warning naming it — "SMTP
password" in full, because a machine now holds six encrypted values and "a saved credential could not
be read" would have the operator retyping the wrong one.

**Security modes.** `StartTls` (587), `SslOnConnect` (465) and `None` (25), offered as words rather
than as a port to guess from: picking wrongly fails in a way that is hard to read — implicit TLS on
587 hangs, STARTTLS on 465 reads bytes that are not a greeting. Changing the mode moves the port to
the one that goes with it *unless* the operator has typed their own. **Both credentials may be left
empty**, and that is a supported configuration rather than an unfinished one: an internal relay that
accepts mail from this subnet takes no AUTH at all, and offering it one is how such a relay refuses
the connection.

**What the message contains.** `ScanEmail.Compose` builds it, in `Esatto.CookieScan.Engine`, and it
takes a `ScanResult` and nothing else — which is why a scan mails identically whether it finished a
second ago or last Tuesday. The subject is `Cookie scan - <host> - <verdict>`, where the verdict is
`1 violation` / `3 to review` / `clean`; a violation outranks a review and the two are never summed,
the same order `ScanResult.ExitCode` applies. **The dry-run flag is deliberately not in the subject**
— it answers a different question, and an inbox full of "- dry run" says nothing at a glance. The
body is table-based HTML with every colour inline (a mail client strips `<style>`), with a plain-text
alternative part saying the same things in the same order.

**Both report files are attached, generated in memory from the result rather than read off disk.**
That is not an optimisation. `cookie-scan-report.md` is a single file in the report directory,
overwritten by every run, so attaching it by path would mail last night's report for a scan picked
out of the history — and would have nothing at all to attach when the report write had failed. To get
there, `ScanReportWriter`'s markdown builder was split so `WriteFiles(options, result)` and
`Markdown(result)` produce the same bytes for the same run; there is a test asserting exactly that.
The two write-back sentences moved to `WriteBackCounts` and `WriteBackSentence` for the same reason,
so a mail saying "2 added" beside a log saying "2 would be added" is not reachable.

**When it sends.** The automatic send is the third of the three things a finished scan leaves behind,
after the report files and the history entry, and it is in the same shape as those two: a mail that
will not send is a warning line and nothing else, never a scan reported as failed. A cancelled scan
mails nobody — the cancel path returns before the result block. Every reason *not* to send is a line
in the log rather than silence: a site whose profile says "email this report" and then does not is
the failure mode worth spending three warnings on.

The manual button sits on the result card and on a selected History scan. It **opens a row with the
recipients in it rather than sending straight away** — a send cannot be undone, and a button that
mailed a client the instant it was pressed, to a list the operator could not see at the time, is the
one mistake the row exists to prevent. The row is prefilled from the site's own recipients, so the
common case is still one click and one Enter. A send from the History page answers on its own
envelope and shows beside the button, because that page has no log panel to print into.

The `sendEmail` message carries an **optional** path: a path names a kept scan, and null means the
run this session has most recently completed, which `ScanSession` holds itself. That second case is
not a convenience — it is the one that has to work when the history write itself failed, which is
exactly when getting the findings out of the window matters most.

**MailKit**, and only on the desktop project. `Esatto.CookieScan.Desktop` is the one half of the
scanner that is not packed, so a dependency added there reaches nobody who references
`Esatto.CookieScan.Engine` for its scanner. MailKit rather than `System.Net.Mail.SmtpClient`, which
Microsoft's own documentation says not to use for new work: it cannot do implicit TLS on 465 at all,
and its failures arrive as "Failure sending mail" with the reason on an inner exception nobody sees.

### The WebView2 runtime, and where its user-data folder lives

The dashboard needs the **WebView2 Evergreen runtime** on the machine that runs it. It ships with
Windows 11 and with recent Windows 10, so in practice it is already there; it is not bundled into
the exe. `DashboardForm` checks for it before creating anything, by calling
`CoreWebView2Environment.GetAvailableBrowserVersionString()`, and if that throws
`WebView2RuntimeNotFoundException` the window shows a message box titled **"WebView2 runtime not
found"** reading:

```
Esatto cookie scanner needs the WebView2 Evergreen runtime, which is not installed on this
machine. Install it from https://go.microsoft.com/fwlink/p/?LinkId=2124703 and run this program
again.
```

and then closes. A missing runtime is therefore a named, actionable message rather than a silent
blank window — which is what an unhandled `EnsureCoreWebView2Async` failure would otherwise look
like.

**The user-data folder is redirected to
`%LOCALAPPDATA%\Esatto.CookieScan.Engine\webview2`**, deliberately, and this is not a detail. WebView2's
default is a `<exe-name>.WebView2` folder created **beside the exe**, which is exactly wrong for a
portable exe: dropped in `C:\Program Files`, on a read-only share, or in any folder the operator
cannot write to, creating that folder fails and the control never initialises — the window opens
blank or not at all, with nothing saying why. `DashboardForm` passes an explicit folder under
`%LOCALAPPDATA%` to `CoreWebView2Environment.CreateAsync` instead, which is writable wherever the
exe itself happens to sit. It sits beside `settings.json`, `scans\` and `reports\`, all of which
are there for the same reason.

## Scan history

`cookie-scan-report.json` changed shape in this work: it is now `ScanResult` serialized directly
(`ScanJson.Serialize`), camelCase, with enums written as names rather than integers —
`ScanJson`'s own reasoning is that the file is meant to be readable, and an integer would silently
change meaning if a `ConsentPass` or `CandidateFlag` member were ever reordered. The write-back
section is now keyed `outcome` (was `merge`) and the third-party-hosts section is
`hostsByPass` (was `hosts`); the top-level `needsReview` array is gone — it is
`ScanResult.NeedsReview`, a `[JsonIgnore]`d property derived from `candidates` by
`flag == "NeedsReview"`, not a second copy of the same information to keep in step — and
`canReachApi`, `dryRun` and `completedAt` are new. The same change reaches inside every
`CookieDeclarationCandidate` nested in `candidates` and `violations`, too: those objects went
PascalCase to camelCase along with everything else (`"Name"` → `"name"`), and their `flag` is now
a name rather than a number (`"Flag": 0` → `"flag": "None"`) — the change most likely to break a
script that parsed the old report, since it is buried inside every entry rather than sitting at
the top level. **Anything that parsed the old shape needs updating.** `cookie-scan-report.md` is
unchanged.

**`options` is newer than the rest of that list.** Every report now carries a top-level `options`
object recording what the run was asked to do, alongside `site` and `completedAt`:

```json
"options": {
  "maxPages": 7,
  "locale": "Sv",
  "memberScanEnabled": false,
  "dryRun": true
}
```

It is `ScanOptionsSummary`, a record on `ScanResult`, and it exists so History's compare can say
that two scans ran under different options — a member scan against a public one will differ by
`.AspNetCore.Identity.Application` no matter what the site does, and a comparison that presented
that as a change to the site would be actively misleading. It is purely additive: `options` is the
**only** difference between a report this build writes and one the pre-dashboard build wrote, and
no existing key changed name, type or value. It carries no credential — `memberScanEnabled` is a
boolean, not the member's email, and there is no field for the client id or either secret.

A scan kept from before this change has **no** `options` key at all — not `null`, simply absent,
because the key did not exist yet when that file was written. Comparing a scan like that against a
newer one does not claim the two ran the same way: History's compare shows the amber banner
`cs-diff-view.js` renders when either side is missing its summary, reading exactly "The options
were not recorded for one of these scans, so this comparison cannot say whether the two ran the
same way. Anything below may be a difference in how the scan was run rather than a change to the
site."

The reason for the change is `ScanHistory`: every completed run, from either front end, writes
this same JSON to `%LOCALAPPDATA%\Esatto.CookieScan.Engine\scans\<utc-timestamp>-<suffix>.json`, capped
at the most recent 50 (`ScanHistory.Keep`) and pruned *after* the write completes, specifically so
a prune that fails cannot cost the scan that just finished. A file that will not parse is skipped
when history is listed, not fatal to the rest of the list — the folder holds files this code did
not necessarily write itself, so one bad one must not cost the whole list. Both front ends write
to the same folder, so a scan run from the command line shows up in the dashboard's History page.

Scan history is not the same thing as the report files. The console tool's report still goes to
`--report-dir` as before (`cookie-scan-report.md` and `.json`, current directory by default); the
dashboard's own copy of those same two files goes to `%LOCALAPPDATA%\Esatto.CookieScan.Engine\reports`
instead, because a window's current directory is wherever it happened to be launched from — a
desktop shortcut leaves it at the system directory — and reports written there would scatter or
fail to write.

## The six passes

Each pass opens a fresh browser context (empty cookie jar), posts one real consent decision to the
site's own endpoint, then replays the same fixed URL list — discovered once, before any pass runs,
so "first seen in pass N" always means the browser genuinely had not visited that page under any
other consent state first.

| Pass | Consent granted | An entry first appearing here proves |
| --- | --- | --- |
| `Undecided` | Nothing (no decision posted at all) | The site sets it before a visitor has chosen anything — must be `necessary` |
| `RejectAll` | Nothing (explicit refusal) | The site sets it even after an explicit refusal — must be `necessary` |
| `Preferences` | `preferences` | The site sets it once preferences are granted |
| `Statistics` | `statistics` | The site sets it once statistics are granted |
| `Marketing` | `marketing` | The site sets it once marketing is granted |
| `AcceptAll` | `preferences`, `statistics`, `marketing` | Everything was granted, so this alone proves nothing about *which* category it belongs to — these entries are flagged `NeedsReview` rather than confidently categorised |

A seventh dimension, `MemberArea`, runs outside this list: it signs in and crawls the member portal,
a different URL set entirely, so its findings cannot be compared by pass order against the six. An
entry found only there is implicitly `necessary` — a cookie that only exists once you are signed in
is a session artefact by construction.

### The violation rule

`CategoryInference.Classify` decides a cookie's category from the pass it first appeared in, then
`ViolationScan.Find` separately checks **every** sighting in the raw observation list — not just
the earliest one per name — against what that sighting's pass had granted. A catalogued category
appearing in a pass that did not grant it is a consent violation, full stop, whether the visitor
refused outright or granted something unrelated (`necessary` is exempt: it is implied rather than
granted, so it never needs to appear in a granted set).

This is deliberately generalised across passes 1–5: a marketing cookie set once during `RejectAll`
and then set *again* during `Statistics` is flagged both times, because a tag that respects consent
selectively — obeying it in one pass and ignoring it in another — is exactly the failure mode the
six passes exist to catch. Reducing to "the earliest sighting per name" before checking would lose
the second violation entirely; that reduction is used for the *declarations* list, never for
violations.

### Declaring what the crawl cannot reach

The write-back has two sources, not one: everything the passes observed, and every catalogue entry
flagged `"expected": true` that the run did **not** observe.

That second source exists because the crawl issues only GETs — it submits the login form and nothing
else, deliberately, so a scan can never create a real record on a live site. A cookie the site
writes from a booking, cancellation or registration POST therefore cannot appear in the observations
however many times the scan runs. `.AspNetCore.Mvc.CookieTempDataProvider` is the standing example:
`BookingSurfaceController` sets `TempData` on every booked class, so a member who books one really
does get that cookie, and a policy page built from sightings alone would be permanently missing it.

`expected` is the flag that makes this safe. It means "this site's own stack sets this" — a statement
about the site rather than about one crawl — so for those entries *not observed* is a reason to
declare, not a reason to omit. An unflagged entry is never declared unseen: an absent Google cookie
is normal, and declaring one would put a cookie on the policy page that nothing sets. A site whose
stack genuinely does not set an expected entry should drop it from that site's catalogue — see
Overriding the catalogue.

Both front ends list all of it in one table — **All entries declared** — because "what will the
policy page say" is the question that table answers, and a scan proposing four cookies while showing
three read as a scan proposing three. Provenance is a column rather than a separate section: the
*First seen in* cell names the pass for a sighting and says `not observed (catalogue)` otherwise, and
the console summary prints both numbers (`3 entr(ies) found, 4 declared`).

The data model keeps them apart even though the table does not. `Candidates` stays observations
only, and the catalogue rows travel in `DeclaredFromCatalogue`, because a catalogue row is present in
every run whether the site changed or not — folding it into `Candidates` would make it show up in a
comparison between two scans as something that happened. A history file written before that field
existed simply has no value for it, and both readers treat it as empty.

Since a merge only ever saves a draft, an over-declaration is something a human sees before it is
published.

## Flags

All flags are `--name value`, except `--dry-run` and `--headed`, which take no value. Verified
against `Esatto.CookieScan.Engine/ScanOptions.cs`.

| Flag | Required | Default | Meaning |
| --- | --- | --- | --- |
| `--url` | Yes | — | The site to scan. Must be an absolute URL with a scheme. Also the base the crawl's own-host check and the `/umbraco` exclusion are relative to. |
| `--target` | No | same as `--url` | The address the token request and merge POST are sent to, when it differs from `--url` — e.g. crawling the public hostname while the management API is reached over a different address. Also what the loopback check for the self-signed-certificate bypass looks at. |
| `--max-pages` | No | `25` | The page cap per pass (and for the member-area crawl). Any non-positive or unparsable value is silently replaced by the default rather than rejected. |
| `--locale` | No | `sv` | `sv` or `en`. Anything other than exactly `en` (case-insensitive) is treated as Swedish. Governs the wording written into new declarations and the duration text's language. |
| `--member-email` | No | — | Together with `--member-password`, enables the member-area dimension. Both must be set or neither is used. |
| `--member-password` | No | — | See above. |
| `--client-id` | No | — | The API user's client id. Together with the secret in the environment, enables the write-back comparison against the live policy page. |
| `--consent-cookie` | No | `cookie-consent` | What this site calls the banner's own consent cookie, when `CookieBannerOptions.CookieName` was changed from its default. Renames the catalogue entry marked `consentCookie`. See below — a wrong value here is loud, not subtle. |
| `--dry-run` | No | off | Runs the full comparison against the site but writes nothing. Requires `--client-id` and the secret to have any effect — without those the scan is already report-only. |
| `--report-dir` | No | current directory | Where `cookie-scan-report.md` and `cookie-scan-report.json` are written. |
| `--headed` | No | off | Runs Chromium with a visible window instead of headless. For watching a scan run, not for CI. |

The client secret is **not** a flag — see below.

### `--consent-cookie`, and what goes wrong without it

The consent cookie is the one catalogue entry whose name is per-site configuration rather than a
fact about a product. `CookieBannerOptions.CookieName` defaults to `cookie-consent`, which is what
the shipped catalogue names, and any site may change it.

Leave it wrong and one run produces two loud false findings at once:

1. The site's **real** consent cookie matches nothing in the catalogue, so it takes the catalogue's
   `unknownCategory` — `marketing` — is seen on the reject-all pass, and is reported as a
   **violation**.
2. The catalogue's `cookie-consent` is never observed, so it is simultaneously reported as
   declared-but-never-found.

On the one cookie that exists to record the visitor's refusal. The flag renames whichever catalogue
entry carries `"consentCookie": true`, so a catalogue override may nominate its own entry rather
than having to keep the shipped name. The dashboard has the same setting as a field, saved per site
profile and stored in clear — it is the name of a cookie every visitor's browser already holds,
so there is nothing to protect and everything to gain from being able to read it in the file.

The dashboard's Scan page takes the same options through fields rather than flags —
`ScanSession.BuildOptions` builds exactly the `ScanOptions` a CLI invocation with the same values
would have — with two deliberate omissions: no field for `--target` (the dashboard always compares
against the host it scanned) and none for `--headed` (the dashboard always runs headless; `--headed`
exists to watch the engine while debugging it, which is not what a window is for).

It has one field the CLI has no flag for: **the API client secret**, saved per site with the client
id it belongs to. That is not the same omission read backwards — the reason the CLI has no flag is
that a flag ends up in shell history and in process listings, which a masked field in a window does
not. See Where the client secret comes from, below.

## The API user

The write-back endpoint (`CookieScanController.Merge`, at
`/umbraco/management/api/v1/cookie-scan/merge`) is authorised with
`[Authorize(Policy = AuthorizationPolicies.BackOfficeAccess)]`, unchanged from how Task 13 left it.
An API-user token does satisfy that policy: a non-dry-run scan against the local site returned
`saved: true` and drafted three blocks onto the policy page (2026-08-30). The same has **not** been
exercised against production yet — see Production, below.

`CookieScanApiUserSeeder` creates the user in code, on every boot, and is entirely opt-in:

```json
// appsettings.Development.json
{
  "Esatto": {
    "CookieScan": {
      "ApiUser": {
        "Enabled": true,
        "ClientId": "cookie-scanner"
      }
    }
  }
}
```

```json
// appsettings.Secrets.json (gitignored)
{
  "Esatto": {
    "CookieScan": {
      "ApiUser": {
        "ClientSecret": "…"
      }
    }
  }
}
```

**This creates a real credential with content access.** `Esatto:CookieScan:ApiUser:Enabled` is set
per environment — `appsettings.Development.json` and `appsettings.Production.json` — and must never
be set in `appsettings.json`, which ships to every environment including any future one that has no
secret to pair with it. With `Enabled` false, or with `Enabled` true but no secret configured, the seeder does
nothing at all: it logs a warning and returns, and boot is never blocked by it (every failure path
in `CookieScanApiUserSeeder.SeedAsync` is caught and logged, not thrown).

The user is created with `UserKind.Api`, joined to the `editor` group by default
(`UserGroupAliases`), and approved immediately. Content access is what the merge endpoint's
authorisation needs; nothing about the scanner needs Settings or Users access.

**Rotating the secret**: change the value in `appsettings.Secrets.json` and restart the site. The
seeder's `applicationManager.EnsureBackOfficeClientCredentialsApplicationAsync` call is safe to
repeat — on the next boot it re-registers the client id with the new secret against the OpenIddict
application store, with no manual step in the backoffice. Update the copy the scanner uses
(`ESATTO_COOKIESCAN_CLIENT_SECRET`, below) to match, or the next scan's token request fails with 401.

### Production

`appsettings.Production.json` ships `Enabled` and `ClientId`; the secret does not travel in the
repository. Set it on the server, either way round:

```
Esatto__CookieScan__ApiUser__ClientSecret = …
```

or the same `Esatto:CookieScan:ApiUser:ClientSecret` key in an untracked `appsettings.Secrets.json`
beside the deployed app. Restart, then look for `The cookie scanner's API user is ready with client
id cookie-scanner` in the log. Until the secret is set, every boot logs the warning naming it and
changes nothing, so the config file is safe to deploy first.

**Use a different secret per environment.** The scanner stores one per site profile precisely so
production's credential never has to be the development one, and a leaked development secret must
not open the live site's content.

Environment variables alone are enough if the deployment's environment name is not `Production`, or
if a redeploy is inconvenient: setting both `Esatto__CookieScan__ApiUser__Enabled=true` and the secret
needs no new files. The 401 this fixes reads
`invalid_client` / `The specified 'client_id' is invalid` — OpenIddict has no application
registered under that id, which is what the seeder's
`EnsureBackOfficeClientCredentialsApplicationAsync` call creates.

## Where the client secret comes from

```
ESATTO_COOKIESCAN_CLIENT_SECRET=<secret>
```

The variable is **the console tool's only source** and **the dashboard's fallback**. The dashboard's
own source is the site profile, where the secret is stored beside the client id and encrypted at
rest exactly like the member password.

For the console tool it is read once, in `ScanOptions.Parse`, straight from
`Environment.GetEnvironmentVariable`. This is deliberately not a `--client-secret` flag: an argument
passed on the command line ends up in shell history and in any process listing (`ps`, Task Manager, a
CI log that echoes its invocation) for as long as either persists. An environment variable set for
one shell session, or injected by a CI secret store directly into the process environment, does not
have either problem. Nothing about that changes.

### Why the dashboard stores one per site

The client id and the client secret are **one pair**, and each site registers its own API user — see
The API user, above. The pair is therefore a property of the *site*, not of the machine scanning it.
One machine-wide variable can only be right while one machine scans one site: a dashboard with a
staging profile and a production profile has two secrets and one place to put them, and the second
site simply cannot be written back to.

So the secret lives in the profile now, and the argument that used to keep it out no longer applies.
That argument was that the secret had a working alternative the member password did not — a variable
set once, costing the operator nothing per run — so storing it bought nothing but risk. The pair
argument overrides it: with several sites the variable is not an alternative at all. The two
credentials are now treated alike because they now *are* alike.

**The trade-off, exactly.** The secret is on disk where it was not before. It is protected the same
way the member password is — DPAPI, one Windows account, one machine, plus the application entropy —
which means safe against another account, another machine and a copied file, and **not** safe
against anything running as this Windows user. That is a real reduction, and it is the price of
scanning more than one site from one dashboard. Three things bound it: the console tool and CI still
take the secret from the environment and never from a file; nothing writes a secret to a *log*, a
report or a command line; and a profile never absorbs the machine's secret behind the operator's
back — see below.

### The fallback, and what must not happen to it

`ScanSession.BuildOptions` prefers the profile's own secret and falls back to
`ESATTO_COOKIESCAN_CLIENT_SECRET` only when the field is blank. A machine that scans one site and has
the variable set already keeps working with an empty box.

`ScanSession.Remembered` stores **what was typed**, never the effective secret. The two differ
precisely when the box was empty and the variable filled in, and writing that value into the profile
would have the profile quietly absorb the machine's secret on its first run: the box would refill
with dots at the next launch, the site would look as though it had a secret of its own, and moving
the file — or the operator — to a machine with a different variable would fail with a credential
nobody remembers typing. A blank box stays blank on disk.

The note under the credential pair on the Scan page says which half is missing, and nothing more.
It is computed from the two boxes plus two facts the host sends on `ready` — whether the variable is
set, and what it is called. **The value itself never reaches the page.** Its states, quietest first:

| What the form holds | The note says |
| --- | --- |
| An id, and a secret or the fallback | *(nothing — the pair can sign in)* |
| An id, no secret, no fallback | No client secret - write-back will be skipped |
| Neither, but the fallback is set | `ESATTO_COOKIESCAN_CLIENT_SECRET` is set - a client id completes the pair |
| Neither, and no fallback | No API credentials - the scan runs report-only |
| A secret, no id | A client id is needed with the secret |

It stays muted in every state: a scan with no credentials at all still finds every cookie, it just
does not write the policy page. Report-only is a supported mode, not a fault.

Nothing about any of this changes what reaches the page: `saveSite` carries the credentials the
operator just typed, and the host sends a profile's own fields back so the form can be filled from
it. The client secret is now in both of those directions, alongside the member password, and both
directions stay inside the process — WebView2 hands the envelope to a renderer in the same exe, over
no socket and no origin anything else can reach.

## Getting the tool

### As a dotnet tool

```
dotnet tool install -g Esatto.CookieScan.Cli
esatto-cookiescan --url https://example.com
```

The NuGet-native route, and what CI should use — `dotnet tool update` is the upgrade path. It needs
the .NET runtime on the machine, which is why the self-contained exe below still exists.

**The single-file properties in `Esatto.CookieScan.Cli.csproj` are conditioned on
`'$(RuntimeIdentifier)' != ''` for exactly this reason.** A tool package is framework-dependent and
is launched by a shim that resolves the managed dll beside its own `deps.json`; bundling that dll
into a self-extracting host leaves the shim with nothing to resolve. `dotnet pack` sets no RID and a
self-contained publish always does, so the RID is the honest discriminator between the two shapes.

### As a portable exe

```
dotnet publish Esatto.CookieScan.Cli -c Release -r win-x64 --self-contained -p:PublishSingleFile=true -p:IncludeNativeLibrariesForSelfExtract=true -p:IncludeAllContentForSelfExtract=true -p:EnableCompressionInSingleFile=true -o dist
```

Produces `dist/esatto-cookiescan.exe`, **72.1 MB (75,596,037 bytes)** as a single self-contained
file — verified against Windows x64, `net10.0`. `EnableCompressionInSingleFile` is what brings that
down from the roughly 180 MB the same publish produced without it; see First launch, below, for
what it costs at run time.

**Chromium itself is not inside the exe.** `BrowserBootstrap.EnsureChromium` shells out to
Playwright's own installer on every run, which is a no-op once a build is already cached but
downloads it the first time — roughly 150–300MB, into `%LOCALAPPDATA%\ms-playwright`, and needs
internet access for that first run only. A first run on a new machine appearing to hang for a
minute at "Checking for a Chromium build..." is this download, not a fault.

**Both single-file properties baked into the csproj are required, and one of them is not obvious.**
`IncludeNativeLibrariesForSelfExtract` is the documented requirement — without it, Playwright's
native libraries cannot be loaded from inside a single-file bundle and the exe fails at the first
browser launch. That alone was not sufficient here: Playwright's browser *driver* — a bundled
Node.js runtime plus its own JavaScript files, normally deployed as a `.playwright\` folder of loose
content beside the build output — is content, not a native library, so `dotnet publish` still left
it behind as loose files next to the exe. Copied anywhere without that folder, the exe built and ran
but failed immediately with:

```
Microsoft.Playwright assembly was found, but is missing required assets. Please ensure to build
your project before running Playwright tool.
```

`Esatto.CookieScan.Engine.csproj` also sets `IncludeAllContentForSelfExtract`, which bundles that content
into the single file too and extracts it next to the exe on first run. Confirmed by publishing and
then running the exe from a directory containing nothing else, per the Verified section below.

### The dashboard

```
dotnet publish Esatto.CookieScan.Desktop -c Release -r win-x64 --self-contained -p:PublishSingleFile=true -p:IncludeNativeLibrariesForSelfExtract=true -p:IncludeAllContentForSelfExtract=true -p:EnableCompressionInSingleFile=true -o dist
```

Produces `dist/esatto-cookiescan-ui.exe`, **86.1 MB (90,308,631 bytes)**. Same three single-file
properties, baked into `Esatto.CookieScan.Desktop.csproj` for the same reason as above: the
dashboard references `Esatto.CookieScan.Engine` and therefore Playwright, so it hits exactly the same
driver-is-content problem the console tool does, and the failure only shows up once the exe is
copied somewhere without the loose `.playwright\` folder beside it. The native switch also carries
`WebView2Loader.dll` into the bundle — the bundler classifies by content, not by item type — and
the dashboard's own `wwwroot` is embedded resources, so the page needs nothing on disk either.

The assembly name is deliberately still `esatto-cookiescan-ui`, the retired WinForms window's:
the published exe, the shortcut pointing at it and the commands here stay as they were, and the
two were never going to ship together.

`Esatto.CookieScan.Desktop.csproj` also carries a `DropUnusedWebView2Wpf` target. The WebView2
package ships a WPF control beside the WinForms one, and the WPF assembly references a
`WindowsBase` this project does not have — MSB3277 on every build, over a reference nothing here
loads. Removing it silences that and keeps it out of the single file. **Confirmed under `publish`,
not only `build`**: a clean `dotnet publish` of this project emits 0 warnings and no MSB3277.

### First launch

A compressed single-file exe is not run in place. On first launch the runtime extracts the bundle —
roughly 200 MB — to `%TEMP%\.net\<app>\<hash>\`, which takes a few seconds; every later launch
reuses that folder and starts normally. **A first launch that seems to sit there for a few seconds
is this extraction, not a hang**, and it is the price `EnableCompressionInSingleFile` pays for
halving the download.

Two consequences worth knowing. If `%TEMP%` is small, on a locked-down volume, or wiped between
runs, set **`DOTNET_BUNDLE_EXTRACT_BASE_DIR`** to a writable directory with room and the runtime
extracts there instead. And this is separate from Chromium: `%TEMP%\.net` holds the app's own
assemblies, while Playwright's browser build lives in `%LOCALAPPDATA%\ms-playwright` and is
downloaded on first use by `BrowserBootstrap.EnsureChromium`, as above. A genuinely cold machine
pays both, once.

## Overriding the catalogue

A `cookie-catalogue.json` placed beside either exe replaces the embedded catalogue **wholesale** —
it is not merged with the built-in one. `CookieCatalogue.Parse` reads it with
`PropertyNameCaseInsensitive`, comment and trailing-comma tolerant, from
`AppContext.BaseDirectory` — `CatalogueSource.Load` is the one place both front ends call this
from, so an override means the same thing regardless of which exe is running. Verified against
`Esatto.CookieScan.Core/CatalogueEntry.cs` and the shipped
`Esatto.CookieScan.Core/Resources/cookie-catalogue.json`:

```json
{
  "unknownCategory": "marketing",
  "entries": [
    {
      "pattern": "_ga_*",
      "provider": { "sv": "Google Analytics", "en": "Google Analytics" },
      "category": "statistics",
      "purpose": {
        "sv": "Håller reda på sessionen för en enskild Google Analytics-egenskap.",
        "en": "Keeps session state for one Google Analytics property."
      },
      "durationDays": 730,
      "tracker": true,
      "expected": false
    }
  ]
}
```

| Field | Type | Meaning |
| --- | --- | --- |
| `pattern` | string, required | The cookie/storage-key name to match. May use `*` as a wildcard; `CookieCatalogue.Match` picks the most specific match — fewest wildcard characters absorbed, then the longest literal prefix — when more than one pattern matches. |
| `provider` | `{ "sv": …, "en": … }`, required | Who sets it, in both shipped languages. Never generated at runtime — this becomes public legal wording. |
| `category` | string, required | One of `necessary`, `preferences`, `statistics`, `marketing`. |
| `purpose` | `{ "sv": …, "en": … }`, required | Why it's set, in both languages, written straight onto the policy page. |
| `durationDays` | number, optional | The documented lifetime, in days. **`0` means a session cookie. Absent (not present in the JSON) means "use whatever expiry the browser actually reported"** — the catalogue's own number wins when present, because a browser may cap or truncate what it reports. |
| `tracker` | bool, default `false` | Informational metadata only; not currently read by any scan or report logic. |
| `expected` | bool, default `false` | Marks an entry as belonging to *this site's own stack*, so its absence from a scan is itself worth reporting under "Expected but not observed". Leave `false` for third-party entries — an absent Google cookie is normal and not worth flagging. |
| `unknownCategory` (top level, not per-entry) | string, default `marketing` | The category assigned to a name no pattern matches, when the pass it appeared in implies nothing (`AcceptAll`) — see the `NeedsReview` row in the passes table. |

## Exit codes

| Code | Constant | Meaning |
| --- | --- | --- |
| `0` | `ScanResult.ExitCode` | No consent violations found, and no configured write-back failed. |
| `1` | `ScanResult.ExitCode` | One or more consent violations found. |
| `2` | `ScanResult.ExitCode`, or `ScanReportWriter.ExitError` | Either a write-back was configured and attempted but never produced an outcome, or the scan itself could not complete at all — a bad URL, no reachable pages, a Chromium launch failure, and so on. |

Both front ends return the same number for the same reason: `ScanResult.ExitCode` is a property on
the shared result, not something either one recomputes for itself. The dashboard has no process to
exit, but the same rule is what tints its findings table and drives the exit-code pill on every
History row.

**The exit code reflects findings, never configuration.** A report-only run — no `--client-id`, no
secret — that finds a violation still exits `1`; a missing credential can never mask a violation.
`Violations.Count > 0` wins outright before anything else is even considered, so a violation always
exits `1` regardless of what the write-back did. Below that: a write-back that was configured
(`CanReachApi`) and attempted (there were candidates to send) but left `Outcome` null exits `2`.
A merge-endpoint failure (a 401, a validation rejection) is logged and swallowed inside
`ManagementApiClient` rather than thrown — the scan still completes and its report is still
written — but it is not silent: the null `Outcome` it leaves behind is exactly what turns the run's
exit code into `2`, which is what lets a CI job gate on the exit code alone rather than parsing
output for the word "failed". `ScanReportWriter.ExitError` is the same `2` for the separate case
where the scan never got that far — bad arguments, no pages discovered, or an unhandled exception —
returned directly by `Program.cs` before there is a `ScanResult` to ask.

## Known limitations

- **`.AspNetCore.Mvc.CookieTempDataProvider` is declared as `expected` but the crawl will not find
  it.** ASP.NET Core only sets that cookie on a request that actually writes TempData — a booking,
  a cancellation, a child-management action, or a completed registration — and every one of those
  is a POST. The scanner's six passes and the member dimension only ever issue GETs, deliberately:
  `MemberDimension`'s own remarks are explicit that login is the only form it submits, because the
  scanner must not be able to create real bookings, cancellations or payments on a live site. The
  practical effect is that this cookie will always show up under "Expected but not observed" rather
  than being found — declared deliberately in the catalogue rather than something to chase by
  teaching the scanner to submit more forms.
- **No pixel detection.** Third-party hosts contacted during each pass are captured and reported
  (the report's "Third-party hosts contacted" section), so a tracking pixel's *host* is visible, but
  nothing infers a `Pixel` storage-type declaration from that. `StorageKinds` documents that the
  scanner never emits `Pixel` even though the CookieBanner package's dropdown offers it — that
  inference is out of scope by design, not an oversight.

## What has been verified

Everything below was exercised against a real Umbraco 18.1.1 site (`https://localhost:44351`),
not reasoned about. Where a claim rests on reading rather than running, it says so.

**The refactor**

The engine was pulled out from under the console tool and shared with the window across several
tasks; the claim that matters most is that neither front end's behaviour drifted while that
happened. Proven by running the pre-refactor and post-refactor console tool against the same live
site, 75 seconds apart, across four scenarios — public, member, a refused connection, and bad
credentials — and diffing stdout, stderr and the markdown report for each: byte-identical in all
four, with matching exit codes, bad credentials included (`2` on both sides). Bad credentials
exiting `2` rather than `0` — described under Exit codes, above — was decided during the scanner's
original development, before this refactor; this comparison is what confirms the refactor itself
changed nothing further.

**The same gate was run again for the dashboard work**, which added two fields to `ScanResult` and
retired the WinForms window. The console tool built from this branch's merge base and the console
tool built from its tip were run against the same live site 75 seconds apart, across the same four
scenarios, and stdout, stderr and `cookie-scan-report.md` were diffed for each: **empty diffs in
all four, with matching exit codes** (`0`, `0`, `2`, `2`). `cookie-scan-report.json` is excluded
from that diff for one known reason only — the added `options` object, above. The console tool did
not notice the dashboard.

**The scan itself**

- All six consent passes run, each in a fresh browser context, each posting its decision to the
  site's own `/api/cookie-consent` endpoint. The passes genuinely differ: the antiforgery cookie
  appears in `Undecided`, the consent cookie only from `RejectAll` onwards. That difference is what
  proves the decisions are being recorded rather than the same state being measured six times.
- Discovery, the page cap, and the `/umbraco` and sign-out exclusions.
- Both report files, in Swedish and under `--locale en`.
- The exit codes, including that a configured write-back which fails exits `2` rather than `0`.
- A failed write-back still writes the report. Proven by pointing the tool at a site whose API user
  did not yet exist: the token 401'd, the failure was reported with an actionable message, and the
  report was written anyway.

**The member dimension**

- Login against a real member account, the member-area crawl, and the attribution of what it finds
  to the `MemberArea` pass. This found `.AspNetCore.Identity.Application` — a cookie the site sets
  and the policy page did not declare.

**The write-back**

- An API-user client-credentials token against
  `/umbraco/management/api/v1/security/back-office/token` — form-encoded, HTTP 200.
- The merge endpoint. `POST .../cookie-scan/merge` returns 401 rather than 404 for an
  unauthenticated caller, so the route is mapped and the authorisation policy resolves; an
  authenticated call returns 200 and writes.
- **The block actually lands correctly.** Read from the database rather than inferred: `layout`,
  `contentData` and `expose` all grew from 3 entries to 4, the new block's key is present in all
  three, and `category` and `storageType` are stored as `["necessary"]` and `["Cookie"]` — the
  serialized array form the flexible dropdown requires. A block missing from `expose` would save
  and then never render, with no error anywhere, so this was the single most important thing to
  confirm.
- **Append-only.** The three pre-existing blocks were byte-identical and unmoved afterwards, and
  the page's `heading`, `introduction` and `outro` were untouched — the whole-document-replace trap
  the narrow endpoint exists to avoid.
- **Save, never publish.** The page came out in state `PublishedPendingChanges`, i.e. a draft.
- **Idempotence**, twice over: an identical second call added nothing and reported `saved: false`;
  and a real suffixed antiforgery cookie matched the declared `.AspNetCore.Antiforgery.*` pattern
  rather than duplicating it. Without that, every run would re-add the same cookie forever.

**The violation rule**

Exercised through `ViolationScan.Find` directly, with a `_fbp` cookie observed during the
`Undecided` pass: one violation, category `marketing`. Not exercised through a real browser,
because that would mean adding a tracker to a live site; the browser layer above it is covered by
the six-pass runs.

**The console tool's portable exe**

Published, and run from a directory outside the repository with nothing else in it — Chromium
bootstrap, crawl, report. The `IncludeAllContentForSelfExtract` property is what makes that work;
without it the exe runs from its own build folder and fails with a missing-assets error the moment
it is copied anywhere, which is the only situation a portable exe is for.

**The dashboard's portable exe**

Published compressed — `dist/esatto-cookiescan-ui.exe`, 86.0 MB single-file, from a clean rebuild
that emitted **0 warnings and no MSB3277**, which is also what proves the `DropUnusedWebView2Wpf`
target works under `publish` and not only under `build`. Then copied to a directory outside the
repository holding nothing else and run from there: the window appeared in 2.6 s, the embedded page
rendered, and one full scan completed through the UI — eight pages discovered, all six passes in
the log, `2 entr(ies) found.`, both report paths written under
`%LOCALAPPDATA%\Esatto.CookieScan.Engine\reports`, the stat tiles and findings table filled, and the
trend chart and the sidebar's kept-scan count moving from 24 to 25. No Playwright assets error and
no orphan process. **Nothing was created beside the exe** — the folder still held only the exe
afterwards, which is what the redirected user-data folder is for.

**The dashboard from a read-only folder**

The case the default user-data folder fails, and the only way to know the redirect works. The exe
was copied into a folder, a deny ACE for the current user was then applied to that folder for the
specific write rights (`WD,AD,WEA,WA,DC` — not a blanket `W`, which would also deny `SYNCHRONIZE`
and break reads), and the folder was proven read-only before launching: creating a file in it was
denied, and so was creating the `esatto-cookiescan-ui.exe.WebView2` subdirectory WebView2 would have
made by default, while opening the exe for reading still succeeded. Launched from there, the window
appeared in 1.5 s and the dashboard rendered in full — Scan page, trend chart, history counts — and
the folder still contained nothing but the exe while the app was running.

**The dashboard's UI**

Driven end to end through UI Automation across the tasks that built it, not just opened and looked
at: live progress appearing in the log panel as a scan runs, with warnings called out; the stat
tiles and findings table filling to match the report JSON, including the rule that a row is a
violation if it appears in `violations` even when its own `flag` is not `Violation`; a cancelled run
leaving neither a report file nor a history entry behind; settings persisting across a restart with
no credential ever written to `settings.json` — that pass predates site profiles, and the file now
holds four of them encrypted, checked again when the client secret joined them: all four fields
`dpapi:`-prefixed and none of the four typed plaintexts anywhere in the raw text; the trend chart plotting
entries and violations
across kept scans; History listing, opening and comparing scans; and the compare showing exactly
the one cookie that differs between a member scan and a public scan, placed in the same group
(Appeared, Disappeared or Recategorised) regardless of which of the two rows was selected first,
with the differing-options warning shown for that pair.

**Site profiles, end to end through UI Automation**

An existing pre-profiles `settings.json` migrated on the first launch into one selected profile with
an empty password, and the run card opened on it with its URL, page count, locale, member email and
dry-run flag all in place. Three profiles were then created from the dropdown, switched between
(every field refilled from the profile picked, the two masked fields visibly refilling to the right
lengths), one was edited and saved, and one deleted — after which the dropdown returned to
**New site…**, the form cleared to its defaults, and both Save site and Delete correctly disabled
themselves. Closing the window and relaunching brought the survivors back with the edit intact and
every credential still filled.

The file was read at each step. All three credential fields that build wrote were `dpapi:`-prefixed
base64 with no plaintext anywhere in it — checked by searching the raw text for each of the six
values that had been typed, none of which appeared. All six blobs were then decrypted out-of-process with
`ProtectedData.Unprotect` under the same user and entropy and matched what had been typed exactly;
the same blob handed the same call with a different entropy was refused with a
`CryptographicException`, which is what the application entropy is there to do.

One scan was then run from a profile against the local site: it took its 25 pages and `Sv` locale
from the profile, ran all six passes plus the member dimension, found 2 entries and wrote its report
and history entry. Finally, the two hero cards were measured off the captured pixels rather than off
the layout — both spanned rows 262 to 1037 of the window, a 776px box each, top and bottom deltas of
zero.

**The report JSON's `options` object is the only difference**

The pre-dashboard and post-dashboard console tools were run against the same live site minutes
apart and their `cookie-scan-report.json` files compared key by key: the sole structural difference
is the added top-level `options`. No existing key changed name, type or value.

**Write-back against production, from a profile's own secret (2026-08-31)**

`ndstk.se` end to end: six passes, the member dimension signing in, no violations, no third-party
hosts, three cookies found — and the merge saved them onto the policy page, which was then
published. `https://ndstk.se/cookies` now serves `.AspNetCore.Antiforgery.*`,
`.AspNetCore.Identity.Application` and that site's consent cookie.

(The verification log names the real sites the tool was run against, deliberately. Every *example*
in this document uses `example.com`; a record of what was tested is worth nothing if the thing it
was tested against is anonymised.)

That settles three things that used to sit under "not verified": an API-user token does satisfy
`BackOfficeAccess` on the merge endpoint, a profile-sourced secret survives the round trip from
DPAPI to the wire, and the seeder's client-id prefix handling is right against a real OpenIddict
store. Two false starts on the way, both worth remembering: production had no API user at all
(`invalid_client` / *"The specified 'client_id' is invalid"*, `ID2052`), and then the profile was
still holding the development secret while production had registered its own (`ID2055`, *"The
specified client credentials are invalid"*). The two error codes tell those apart — see
Troubleshooting.

## What has not been verified

- **A site with real third-party tags.** This site loads none, so the categorisation of a genuine
  statistics or marketing cookie, and the violation rule firing on a real tracker in a browser,
  have not been seen end to end. The logic is unit-tested and the mechanism is proven; the input
  has never existed here.
- **`.AspNetCore.Mvc.CookieTempDataProvider` being observed.** It is only set by a request that
  writes `TempData` — a booking, cancellation, child-management or registration POST — and the
  crawl issues only GETs, so no scan will ever witness it. It still gets **declared**: see
  Declaring what the crawl cannot reach, above. What remains unverified is only the sighting, which
  is unreachable by design rather than missing.
- **A machine without the WebView2 Evergreen runtime.** Every machine this has run on already had
  it. The missing-runtime path — `GetAvailableBrowserVersionString` throwing
  `WebView2RuntimeNotFoundException`, the named message box, the window then closing — is read from
  `DashboardForm`, not seen. The message's wording above is quoted from the source.
- **`DOTNET_BUNDLE_EXTRACT_BASE_DIR`.** Named above as the escape hatch when `%TEMP%` will not do,
  on the runtime's documented behaviour; not exercised here, because `%TEMP%` has been adequate on
  every machine this has run on.
- **The dashboard anywhere but Windows 11 x64.** Published `win-x64` self-contained and run only
  there. Nothing in it is version-specific beyond the WebView2 runtime requirement, but that is
  reasoning, not a run.

## Troubleshooting

- **A 401 on the token request.** The id and secret the scan sent are not the pair the site
  registered. Read the `error` in the response body first: `invalid_client` with *"The specified
  'client_id' is invalid"* means the environment has **no API user at all** — the seeder never ran
  there, so check `Esatto:CookieScan:ApiUser:Enabled` and the secret on that server (see Production,
  above) before suspecting the pair. Any other 401 is a genuine mismatch.
  In the dashboard the secret that was sent is the profile's **API client secret**
  field, or `ESATTO_COOKIESCAN_CLIENT_SECRET` only if that field is empty; for the console tool it is
  always the variable. Check the source that applies against the site's `appsettings.Secrets.json`,
  and remember the seeder re-registers the pair on every site boot - a secret rotated there needs
  rotating in the profile too.

**"WebView2 runtime not found" when the dashboard starts.**
The WebView2 Evergreen runtime is missing. Install it from
`https://go.microsoft.com/fwlink/p/?LinkId=2124703` and run the exe again. The dashboard checks for
it before it builds anything, so this is a named message box rather than a blank window — see The
WebView2 runtime, above.

**The dashboard's window opens blank, or a window never appears at all.**
Almost always the WebView2 user-data folder. It is redirected to
`%LOCALAPPDATA%\Esatto.CookieScan.Engine\webview2` precisely so this cannot happen from a read-only or
Program Files location, so if it does happen, check that `%LOCALAPPDATA%` itself is writable and
that nothing is holding a lock on that folder — a second copy of the exe already running against
the same profile will do it. Deleting the `webview2` folder is safe: it is a browser profile, and
nothing in it is scan data. Settings, scan history and reports are separate folders beside it.

**The exe sits for a few seconds on first launch and nothing happens.**
Expected. Both exes are compressed single-file bundles, and the first launch extracts roughly
200 MB to `%TEMP%\.net` before any of your code runs; later launches reuse it. If `%TEMP%` is
small, wiped between runs, or on a volume you cannot write to, set
`DOTNET_BUNDLE_EXTRACT_BASE_DIR` to a writable directory with room. A separate first-run pause at
"Checking for a Chromium build..." is Playwright's browser download — different folder, different
cause, see Publishing the portable exes, above.

**`invalid_client` / "The specified 'client_id' is invalid" from the token endpoint.**
The OpenIddict application was never registered. The seeder did not run, or it failed —
check the boot log for `CookieScanApiUserSeeder`. Remember it only runs when both
`Esatto:CookieScan:ApiUser:Enabled` is true and a `ClientSecret` is configured.

**`Authorization failed` / "The user associated with the supplied 'client_id' could not be
found", with the seeder reporting success.**
This one is worth knowing about, because nothing in the log points at it. Umbraco's
back-office token handler prepends `umbraco-back-office-` to the incoming `client_id`
before resolving it to a user. So the user↔client-id association must be stored with the
prefix, while the OpenIddict application and the value you pass to `--client-id` stay
unprefixed. The seeder handles this; if you ever write your own, the raw id will register
an application fine and then fail at user resolution with exactly this message. Verified
against Umbraco 18.1.1 by decompiling `BackOfficeUserClientCredentialsManager.FindUserAsync`
and its `SafeClientId` helper, and against the `umbracoUser2ClientId` table.

**`UserNameIsNotEmail` when the API user is created.**
Umbraco validates a user's username as an email address, so an API user's username cannot
be its client id. The seeder uses the configured email; the client id is attached
separately.

**The endpoint works but does not appear in `/umbraco/swagger`.**
Known and cosmetic. `POST .../cookie-scan/merge` returns 401 for an unauthenticated caller
rather than 404, so the route is mapped. Why it is absent from the swagger document is
unresolved; it serves a CLI, not people browsing swagger.
