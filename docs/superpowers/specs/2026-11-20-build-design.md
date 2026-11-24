# Build package — Design

**Date:** 2026-11-20
**Package:** **`Esatto.Umbraco.Backoffice.Build` 1.0.0** (new, public, nuget.org, `umbraco-marketplace` tag)
**Repo dir:** `Esatto.Umbraco.Backoffice.Build/` + `Esatto.Umbraco.Backoffice.Build.Tests/`
**Source being packaged:** the **Content ▸ Build** dashboard in `c:\src\Esatto.Web.Nova` (esatto.se, Umbraco 18 on
Umbraco Cloud), about 750 lines in total:
`src/Features/Backoffice/Build/*` (`BuildInfoController`, `BuildInfoService`, `PipelineBuild` with
`PipelineBuildReader`, `LocalGitLog`), `src/wwwroot/App_Plugins/esatto/build-dashboard.js` (one Lit element),
the manifest entry `Esatto.Dashboard.Build` in that folder's `umbraco-package.json`, the DI line in
`src/Infrastructure/Inititalization/SiteInitialization.cs`, the pipeline script `devops/write-build-info.mjs`, and the
tests `tests/Esatto.Web.Nova.Tests/Unit/Features/PipelineBuildReaderTests.cs` (9 tests).
**Closest sibling to mirror:** `Esatto.Umbraco.Backoffice.Seo` (SEO Health): csproj, TypeScript/Vite client,
composer, test project. `Esatto.Umbraco.Backoffice.NavigationOrder` and the SeoReport design
(`2026-11-19-seoreport-design.md`) are the precedent for English keys in `Client/src/lang/en.ts`.
**Test site:** `c:\src\umbracokalaset26` (clean Umbraco 18) for the install-from-scratch run;
`c:\src\Esatto.Web.Nova` for the adoption step.

## Summary

Carl wants Nova's Build dashboard shipped as a NuGet package that **anyone can install from nuget.org / the
Umbraco Marketplace** on an Umbraco 18 site, with **everything in English** (today the code comments, log messages,
test names and the date format are Swedish; the UI texts are already English).

What the dashboard is (unchanged in purpose): a tab under Content that says **which pipeline build is running on
this environment**, so an editor or developer can see that the right build is on Test and Live. It shows the build
number, branch, commit, build time and pipeline target; the commits that build adds since the previous deployment;
this environment (name, instance, running since, Umbraco version); and the versions of the packages that decide how
the site behaves.

The dashboard only knows the build because **CI writes a `build-info.json` stamp into the artifact** before it is
zipped (Umbraco Cloud rebuilds the code from the zip without `.git`, so the running site can't find its own commit
any other way). Today that stamp is written by an Azure-DevOps-only script in Nova. For a Marketplace audience the
package therefore needs two things: a **documented stamp format** and a **reference script that writes it on the
common CI systems**.

### Relation to what already exists

- **Nova's in-site copy** is replaced by this package later (see *Nova adoption*).
- **Other Esatto packages:** no dependency either way. Its dashboard sits next to SEO Health, SEO Report and
  Accessibility in Content, with the lowest weight so it is the last tab, as in Nova.
- Umbraco Cloud already shows the *environment name* in the backoffice header; this package is about the
  **build and commit**, which Cloud doesn't show, so it doesn't overlap.

### Decisions

| Question | Decision | By |
|---|---|---|
| Who installs it | Anyone, from nuget.org and the Marketplace. Nothing Esatto-specific in defaults, texts or docs | Carl |
| How much of the CI side ships | **Documented stamp format + a reference writer script in the repo** (not inside the nupkg). Not "format only" (useless on first install), not an MSBuild target or dotnet tool (Cloud builds from a zip without `.git`, so a build target runs too late; a tool is a second package to version) | Claude recommended, Carl chose |
| Language | **English only.** Every user-facing text is a translation key with an English text in `Client/src/lang/en.ts`, registered as a `localization` extension for `en`; **no Swedish file** (a Swedish or any other backoffice falls back to English). Code comments, log messages, test names, README, script output: English | Carl; the key mechanism follows SeoReport/NavigationOrder (Claude's call) |
| Dates | Formatted in the client with `Intl.DateTimeFormat(undefined, …)`, i.e. **the browser's locale**, not a hard-coded `sv-SE`. The relative part ("10 days ago") stays English text | Carl (browser locale); relative text Claude's call |
| Package name | `Esatto.Umbraco.Backoffice.Build` | Claude's call (convention) |
| Dashboard | Label **"Build"**, pathname `build`, alias `Esatto.Umbraco.Backoffice.Build.Dashboard`, element `esatto-build-dashboard`, weight `-200`, Content section | Claude's call (kept from Nova) |
| Umbraco / .NET | **Umbraco 18 only**, net10.0 (matches Seo and SeoReport) | Claude's call, Carl accepted |
| API | Route `esatto/build-info` (under the Management API), group "Esatto Build", Content-section authorization: all kept from Nova | Claude's call, Carl accepted |
| No stamp on a deployed site | An **empty state** that says no `build-info.json` was found and links to the package README for how to add it | Claude's call, Carl accepted |
| Packages card | **Configurable.** Built-in defaults: the three add-ons Nova shows today (Umbraco Deploy, uSync, BlockPreview, with their display names) plus every loaded assembly named `Esatto.Umbraco.Backoffice.*`. `Esatto:Build:Packages` (array of assembly names; a trailing `*` matches a family) **adds** entries; display name = assembly name. Only assemblies that are loaded are listed, as today | Claude's call, Carl accepted (defaults detail Claude's call so adopting changes nothing on esatto.se) |
| "App version" source | The **host site's entry assembly** (`Assembly.GetEntryAssembly()`), not `typeof(BuildInfoService).Assembly`. Moved into a package, the latter is the *package's* assembly and would show the wrong version and "built from" commit. A null entry assembly (some test hosts) means "unknown", never an exception | Claude's call (a necessary change) |
| Local git history | Read only when the site runs in the **Development** environment. Nova reads it whenever there is no stamp; a public package shouldn't spawn `git` on someone's production server | **Claude's call — new, please check** |
| Where the stamp is read from | `Esatto:Build:FilePath`, default `build-info.json`, relative to the content root. Lets sites whose artifact layout differs from Nova's `src/` use the package | Claude's call |
| Stamp format versioning | Optional `schemaVersion` (writer emits `1`); the reader ignores unknown fields and doesn't require the field | Claude's call |
| Links | The client renders a URL from the stamp as a link **only if it is `http:` or `https:`** | Claude's call |
| Changes summary with only a commit | "since commit `<short sha>`" when the stamp has `sinceCommit` but no `sinceBuildNumber` | Claude's call |
| Persistence | None. No tables, no migration, no settings UI | follows from the scope |

### Out of scope (1.0)

- A Swedish (or any other) language file. One more file and one manifest entry later, no code change.
- A settings tab. Configuration is `appsettings` only.
- Writing the stamp from MSBuild or a dotnet tool; CI systems other than Azure DevOps and GitHub Actions (others use
  the `BUILD_INFO_*` overrides, below).
- Comparing environments with each other (Test vs Live), release notes, rollback, triggering deployments.
- Hiding commit bodies or authors from the stamp (the README warns instead; see *Security*).

### Anti-goals (would make this a failure even if it technically works)

- Any Esatto-specific value in the package or the script's defaults (Azure project names, the task name
  "Request Start Deployment", `TARGETENVIRONMENTALIAS`, Nova paths).
- Swedish anywhere in the repo for this package, including comments, test names and log messages.
- A missing, empty or broken stamp taking the backoffice down or filling the log with errors: the tab is a help,
  not a gate.
- Real esatto.se data (commit messages, author names, instance ids, build URLs, screenshots of the real site) in this
  **public** repo. Screenshots and fixtures use synthetic data.
- The package silently showing the wrong version or commit because it looked at its own assembly.

### Success

- On a clean Umbraco 18 site, `dotnet add package` from a local feed makes a **Build** tab appear in Content, in
  English, with no consumer-side wiring.
- Without a stamp (Production): the empty state. Without a stamp (Development, inside a git repo): the local commits.
  With a synthetic stamp: the full tab.
- Running the reference script on Azure DevOps (proved by Nova's pipeline during adoption) and on GitHub Actions
  (proved by fixtures here; the first real run is Carl's) writes a stamp the reader accepts.
- Nova, after adoption, shows the same tab as today, minus the Swedish date format, with no Nova code left for it.
- `dotnet pack` is clean (no warnings) and the README renders on nuget.org with working images.

## What the backoffice user sees

Same layout as Nova today (keep it; do not redesign). Three states of the first box:

```
Deployed by a pipeline (stamp found)
┌──────────────────────────────────────────────────────────────┐
│ Deployed build  0.0.108   [branch: main]                     │
│ Commit          9c8d56d  Subject of the commit               │
│ Built           9 Nov 2026 23:44 (10 days ago)               │   ← date in the browser's locale
│ Pipeline target live                                         │
└──────────────────────────────────────────────────────────────┘
┌ What's in this build ────────────────────────────────────────┐
│ 1 commit since build 0.0.107.                                │
│ ▸ Subject of the commit                                      │   ← body collapsed, trailers stripped
│   9c8d56d · Author Name · 9 Nov 2026 23:42                   │
└──────────────────────────────────────────────────────────────┘

No stamp, Development, inside a git repo     No stamp, anywhere else
┌─────────────────────────────────────┐      ┌─────────────────────────────────────┐
│ Deployed build   Local build [branch]│      │ Deployed build   Unknown build      │
│ Not deployed by a pipeline: no       │      │ No build-info.json was found on     │
│ build-info.json on this server.      │      │ this server. See the package README │
│ Built from  f1ff7ec                  │      │ for how your pipeline can write it. │
└─────────────────────────────────────┘      └─────────────────────────────────────┘
┌ Latest commits in this working copy ┐
│ The latest 20 commits on main: 2 not pushed, 3 files changed but not committed.
│ … rows tagged "running" / "not pushed"
```

Below that, always, side by side: **This environment** (Environment, Instance, Running since, Umbraco) and
**Packages** (name, version).

### Behaviour to keep (unchanged from Nova)

- The response is built **once per process** (a deploy is always a restart); only the local git read happens per call.
- A stamp that is missing, unreadable or lacks `buildNumber` or `commit` means "no pipeline build"; it is logged at
  warning level (unreadable) or silently ignored (missing), never thrown.
- The `+<sha>` suffix on an assembly's informational version is stripped for display.
- The changes summary wording: "N commits since build X", "No new commits since build X: this deployment rebuilt the
  same code.", "(only the latest N shown)" when truncated, and "The previous deployment could not be found, so these
  are the latest N commits." when `sinceCommit` is null. **When `sinceCommit` is known but `sinceBuildNumber` is not**
  (the `BUILD_INFO_SINCE_COMMIT` case), the text says "since commit <short sha>" instead of "since build X", so the
  sentence is never left dangling (Claude's call; the reviewer pass found the gap).
- Commit rows: subject always visible, body in a collapsed `<details>`, `Co-Authored-By`-style trailers stripped,
  commit SHA is a link only when the stamp gives a URL.
- Local mode tags a commit **running** (the commit the running app was built from) and **not pushed**.
- Dashboard colours/spacing use `--uui-*` tokens only, so light, dark and high-contrast themes work.

## The build stamp (`build-info.json`)

A documented, versioned contract; the **README is its home** and the C# records are its reader. camelCase JSON.
`buildNumber` and `commit` are required; everything else may be absent or null. Unknown fields are ignored.

```json
{
  "schemaVersion": 1,
  "buildNumber": "42",
  "commit": "0123456789abcdef0123456789abcdef01234567",
  "buildUrl": "https://ci.example.com/builds/42",
  "commitMessage": "Add the contact form",
  "commitUrl": "https://git.example.com/acme/site/commit/0123456789abcdef0123456789abcdef01234567",
  "branch": "main",
  "targetEnvironment": "live",
  "builtAtUtc": "2026-11-20T09:12:00Z",
  "changes": {
    "sinceCommit": "89abcdef0123456789abcdef0123456789abcdef",
    "sinceBuildNumber": "41",
    "sinceBuildUrl": "https://ci.example.com/builds/41",
    "truncated": false,
    "commits": [
      { "sha": "0123456789abcdef0123456789abcdef01234567", "subject": "Add the contact form",
        "body": null, "author": "A. Developer", "dateUtc": "2026-11-20T09:01:00Z",
        "url": "https://git.example.com/acme/site/commit/0123456789abcdef0123456789abcdef01234567" }
    ]
  }
}
```

`changes.sinceCommit` null means "no known previous deployment"; `commits` is then simply the latest commits and
`truncated` is true. Where the file lives: in the content root of the running site (the project folder of the
artifact that Umbraco Cloud builds, or the publish folder elsewhere), or wherever `Esatto:Build:FilePath` points.

## The reference pipeline script

`Esatto.Umbraco.Backoffice.Build/pipeline/write-build-info.mjs`, generalized from Nova's
`devops/write-build-info.mjs`. **In the repo, not in the nupkg.** The README tells people to copy it (or download
the raw file) into their own repo and shows a step for Azure DevOps and one for GitHub Actions.

- **Requirements:** Node 20+ (global `fetch`), no npm dependencies, a **full git history** in the checkout
  (`fetchDepth: 0` / `fetch-depth: 0`). Usage: `node write-build-info.mjs [--out <path>]`, default
  `./build-info.json`.
- **CI detection:** `TF_BUILD=True` → Azure DevOps; `GITHUB_ACTIONS=true` → GitHub Actions; otherwise generic.
- **Field sources** (generic column = the override that works on any CI and wins everywhere it is set):

  | Field | Azure DevOps | GitHub Actions | Override / generic |
  |---|---|---|---|
  | `buildNumber` | `BUILD_BUILDNUMBER` | `GITHUB_RUN_NUMBER` | `BUILD_INFO_BUILD_NUMBER` (required in generic mode) |
  | `buildUrl` | collection URI + project + `_build/results?buildId=` | `GITHUB_SERVER_URL/GITHUB_REPOSITORY/actions/runs/GITHUB_RUN_ID` | `BUILD_INFO_BUILD_URL` |
  | `commit` | `git rev-parse HEAD` | same | |
  | `commitUrl` | `BUILD_REPOSITORY_URI` (credentials stripped) + `/commit/<sha>` | `GITHUB_SERVER_URL/GITHUB_REPOSITORY/commit/<sha>` | `BUILD_INFO_COMMIT_URL_TEMPLATE` with `{sha}` |
  | `branch` | `BUILD_SOURCEBRANCHNAME` | `GITHUB_REF_NAME` | `BUILD_INFO_BRANCH`, else `git branch --show-current` |
  | `targetEnvironment` | `BUILD_INFO_TARGET` | `BUILD_INFO_TARGET` | `BUILD_INFO_TARGET` (Nova's `TARGETENVIRONMENTALIAS` is mapped to it in Nova's own YAML, not in the script) |
  | `builtAtUtc` | now | now | |

- **"Since the previous deployment":** an explicit **`BUILD_INFO_SINCE_COMMIT`** wins on every CI (no API call;
  `sinceBuildNumber`/`sinceBuildUrl` stay null). Otherwise:
  - *Azure DevOps:* latest earlier completed run of the same pipeline on the same branch (builds API,
    `SYSTEM_ACCESSTOKEN` mapped explicitly into the step). If **`BUILD_INFO_DEPLOY_TASK`** names a task, only a run
    in which that task *succeeded* counts (Nova sets it to `Request Start Deployment`: a run counts as deployed once
    Cloud accepted it, even if a later wait step timed out); if unset, any run with result `succeeded` counts.
  - *GitHub Actions:* latest earlier run of the same workflow on the same branch with conclusion `success`
    (`GITHUB_TOKEN`, permission `actions: read`).
  - The found commit must be an ancestor of `HEAD` (`git merge-base --is-ancestor`), else it is reported to the log
    and treated as "not found".
  - Not found, or the lookup failed (the failure goes to the log; **it never fails the CI step**): the latest 20
    commits, `sinceCommit` null, `truncated` true.
- **Limits and format:** at most 100 commits (101 requested to set `truncated`); no merges; trailers filtered with
  the same rule and the same `\x1f`/`\x1e` log format as `LocalGitLog`, so a local and a deployed list look alike.
- **Fails (non-zero exit, clear message)** only when required information is missing: not in a git repository, or
  generic mode without `BUILD_INFO_BUILD_NUMBER`.
- **Testable by construction:** the script exports its pure parts (CI detection and field mapping, the git-log
  parser, trailer filter, range choice, URL building) and takes the HTTP function as a parameter, so `node --test`
  covers it with fixtures and a fake `fetch`. The GitHub adapter **cannot be exercised against the real API from
  here**; its first real run is Carl's to check (see *Testing*).

## Server

- **`BuildComposer : IComposer`** registers `BuildInfoService` as a singleton and binds `BuildOptions`
  (`Esatto:Build`). Composers are auto-discovered, so there is no consumer-side wiring (the dashboard itself is in
  `umbraco-package.json`).
- **`BuildInfoController`** unchanged in shape: `GET esatto/build-info`, `[Authorize(Policy =
  AuthorizationPolicies.SectionAccessContent)]`, `[ProducesResponseType<BuildInfoResponse>(200)]`.
- **`BuildInfoService`** builds the response once (`Lazy`): stamp (from `Esatto:Build:FilePath`), host app version
  (entry assembly), Umbraco version, environment name, machine name as "instance", process start time, packages. `Get()`
  adds `Local` per call **only** when there is no stamp **and** `IHostEnvironment.IsDevelopment()`.
- **`BuildOptions`**: `FilePath` (default `build-info.json`), `Packages` (string array, default empty).
- **`LocalGitLog`** as in Nova (finds `.git` upward from the content root, `.git` may be a file in a worktree,
  `ArgumentList` not a shell string, 5-second timeout that kills the process tree, `Win32Exception` means "git not
  installed"), with English messages.
- The Swagger/OpenAPI document and the client's use of the Management API follow SEO Health's pattern.

## English keys

- The server sends **no prose**: its responses are data (the records above). Log messages are English.
- The client's texts live under `esattoBuild` in `Client/src/lang/en.ts`, looked up as `esattoBuild_<key>`,
  function-valued entries for texts that need numbers or plurals, registered in `umbraco-package.json` as a
  `localization` extension for culture `en`. **A test must fail** if a key referenced in the client sources has no
  English text (mechanism left to the builder).
- Nova's current UI wording is the **source for the English texts**: keep it, only generalize the Nova-specific
  sentences (for example "Test and Live show the build number…" must not assume the reader's environments).

## Security

- **Who can read it:** users with **Content** section access, as in Nova.
- **What it discloses to them:** the stamp (commit subjects and bodies, author names, build and commit URLs, branch,
  target), the local git history in Development, the machine name, and the package versions. The README says so and
  tells people **not to put secrets in commit messages** and to stamp only what they are happy for editors to see.
- **Links:** `buildUrl`, `commitUrl`, `sinceBuildUrl` and each commit `url` are rendered as anchors only if the scheme
  is `http:` or `https:` (otherwise as plain text), always with `rel="noopener"`. All text goes through Lit's
  escaping; nothing is set as raw HTML.
- **Process spawning:** `git` is started only in Development, with an argument list (no shell), a fixed working
  directory found from the content root, and a timeout.
- **File access:** the reader opens only the configured file; it doesn't follow URLs or execute anything from it.
- **The pipeline script** takes its tokens (`SYSTEM_ACCESSTOKEN`, `GITHUB_TOKEN`) only from the environment and never
  writes them to the stamp or the log.

## Errors

- Stamp missing → no pipeline build (no log line). Unreadable or incomplete → warning-level log, no pipeline build.
- `git` unavailable, not a repo, or timing out → no local changes (warning-level log for the unavailable/timeout case).
- Client: if the request fails the tab shows an inline error text (key) instead of the content; it never blocks the
  other dashboards.

## Architecture

```
Esatto.Umbraco.Backoffice.Build/
  Esatto.Umbraco.Backoffice.Build.csproj      Razor SDK, mirrors SEO Health
  icon.png  README.md  docs/*.png             screenshots, synthetic data, absolute raw-GitHub URLs
  src/
    BuildComposer.cs  BuildOptions.cs
    BuildInfoController.cs  BuildInfoService.cs
    PipelineBuild.cs   (records + PipelineBuildReader)
    LocalGitLog.cs
  pipeline/
    write-build-info.mjs  write-build-info.test.mjs
  Client/
    public/umbraco-package.json               dashboard + localization extension
    src/ build-dashboard.element.ts  api.ts  types.ts  format.ts  lang/en.ts  *.test.ts
    package.json  vite.config.ts  tsconfig.json
  wwwroot/App_Plugins/Esatto.Umbraco.Backoffice.Build/   built output, as in SEO Health
Esatto.Umbraco.Backoffice.Build.Tests/
```

Client structure: one dashboard element (it is a single read-only page, not a tabbed app like SEO Health) plus pure
helpers in `format.ts` (date and relative-time formatting with an injectable locale, short SHA, `builtFrom`, the safe-link
check). **Targeted clean-up while porting, behaviour-preserving:** Nova's JS builds "link if there is a URL, else
plain text" three separate ways (commit SHA, build number, since-build number); make it one helper that also does the
scheme check. One `DateTimeFormat` and one `RelativeTimeFormat`, created once. `Packages()` in the service keeps its
shape but reads from the options. Don't force other refactors.

## Testing

xUnit project mirroring SEO Health's (`net10.0`, `IsPackable=false`, NSubstitute, `FrameworkReference`
`Microsoft.AspNetCore.App`, `InternalsVisibleTo` as SEO Health does) with plain xUnit assertions (the packages don't use
AwesomeAssertions). Vitest for the client helpers, `node --test` for the script.

- **Port Nova's 9 tests**, English names, same behaviour: reads the stamp; reads the commits since the previous
  deployment; missing file means no build; broken or incomplete file means no build (theory); version shown without
  commit suffix (theory); local git log parsed without trailers; local git log read from this very repository;
  controller route and Content-section authorization.
- **New server tests:** app version comes from the **entry assembly**, not the package (a seam that lets a test
  supply the assembly, and a null entry assembly gives "unknown"); `Esatto:Build:FilePath` is honoured; `Local` is
  filled only in Development and only without a stamp (a Production environment with a `.git` above the content root
  returns none); `Packages` defaults, wildcard match, additive configuration, only-loaded assemblies, stable order.
- **Client tests:** date formatting with an explicit non-English locale uses that locale's format; relative text
  thresholds (minutes / hours / days); the safe-link check accepts `http`/`https` and rejects `javascript:`, `data:`,
  relative and malformed values; every referenced `esattoBuild_*` key exists in `en.ts`.
- **Script tests (`node --test`):** CI detection; field mapping for Azure DevOps, GitHub Actions and generic mode
  (including `BUILD_INFO_*` overrides winning); credentials stripped from the repository URL; git-log parsing and
  trailer filtering (same fixtures as the C# test, so the two stay in step); range choice for "ancestor found", "not an
  ancestor", "lookup failed", "no lookup possible", `BUILD_INFO_SINCE_COMMIT`; `BUILD_INFO_DEPLOY_TASK` set and unset
  (fake timelines); the GitHub previous-run selection (fake runs response); limits and `truncated`; non-zero exit
  when required information is missing. **A round-trip test:** a stamp the script writes is accepted by
  `PipelineBuildReader` (one fixture committed, asserted from both sides).
- **Manual / browser (`umbracokalaset26`):** install from a local feed; verify the tab appears in English with no
  wiring; empty state in Production with no stamp; local commits in Development; a synthetic stamp renders every
  field and the since-build link; a Swedish-language backoffice user sees English texts; the dates follow the
  browser locale (check with a non-English browser locale); light/dark/high-contrast. Then the script against the
  clean site's own git history, and on Nova's real Azure pipeline during adoption. The **first GitHub Actions run is
  Carl's check** and the README says the GitHub path is verified against fixtures only until then.

## Packaging

Per `esatto-packages-conventions` and the SEO Health csproj: Razor SDK, `StaticWebAssetBasePath=/`, the
`BuildBackofficeClient` target (`npm install` + `npm run build` before build), `icon.png`, README, **source-code
link** (`RepositoryUrl` = repo root, `PackageProjectUrl` = `.../tree/main/Esatto.Umbraco.Backoffice.Build`,
`RepositoryType` git), tags `umbraco;umbraco-marketplace;umbraco-18;backoffice;build;deployment;ci`, a detectable
`Umbraco.Cms.*` dependency (`Umbraco.Cms.Api.Management` and `Umbraco.Cms.Web.Common` 18.0.0 minimums, as SEO Health),
the same "Security overrides" and `NuGetAuditSuppress` blocks as SEO Health **if the same advisories apply** (check
at build time; don't copy blindly), the `AutoPush` target off by default. MinVer from the git tag
`Esatto.Umbraco.Backoffice.Build-1.0.0`. **`dotnet pack` must be warning-free.**

README covers: what it does (with synthetic screenshots, **absolute raw-GitHub URLs**, never relative); install; the
stamp format; the pipeline script with an Azure DevOps and a GitHub Actions example, each saying what the checkout
needs (full history, the token); configuration (`Esatto:Build:FilePath`, `Esatto:Build:Packages`) and where the file must
sit on Umbraco Cloud and elsewhere; the disclosure note; what it doesn't do.

The work is prepared, committed, tagged and packed by Claude **only with Carl's explicit approval at each step**;
**Carl runs `dotnet nuget push`.** The nuget.org API key glob is `Esatto.*`, so a first push of this ID is covered,
but the key itself expires around 2026-11: **check it before releasing** (it is 2026-11-20 as this is written). The
Marketplace re-scan POST, if the package doesn't appear, is outward-facing: get Carl's go first.

## Nova adoption (separate step, after 1.0.0 is released and verified)

Not part of the package deliverable, listed so nothing is forgotten:

- **One atomic change.** Nova's `BuildInfoController` and the package's use the **same route** and the same dashboard
  pathname and element name; with both present, requests would be ambiguous and the element would be defined twice.
  So adopting means: add the `PackageReference`, and in the same commit delete `Features/Backoffice/Build/*`, the
  `Esatto.Dashboard.Build` entry in `umbraco-package.json`, `build-dashboard.js`, the DI line in `SiteInitialization.cs`
  and `PipelineBuildReaderTests.cs`. Test the package on the **clean site**, not on Nova, first.
- Replace `devops/write-build-info.mjs` with the package's script (copy it), keeping Nova's wiring in its YAML:
  `BUILD_INFO_TARGET: $(TargetEnvironmentAlias)`, `BUILD_INFO_DEPLOY_TASK: Request Start Deployment`,
  `SYSTEM_ACCESSTOKEN`, output `src/build-info.json`. Confirm the first deployed build still shows the commits since
  the previous deployment and the same target.
- Add `Esatto:Build:Packages` only if Nova needs more than the defaults (it doesn't today: Deploy, uSync,
  BlockPreview and the `Esatto.Umbraco.Backoffice.*` family are all defaults).
- Nova's root namespace begins `Esatto.`, which collides with inline `Umbraco.Cms…` references once an
  `Esatto.Umbraco.*` package is referenced (see `esatto-packages-conventions`); deleting the Build code shouldn't
  introduce any, but build before pushing.

## Left to the builder

File and class names within the layout above; how the entry-assembly seam is shaped; the exact keys and English
wording; the shape of `BuildOptions` binding; how the key-completeness test works; the Vite/TypeScript details; how
screenshots are produced from the synthetic stamp; whether `wwwroot` output is committed (follow SEO Health); the
wording of the README's CI snippets (verify against each CI's current syntax); which Nova helper code can be
simplified when ported (the clean-up above is the only one decided).
