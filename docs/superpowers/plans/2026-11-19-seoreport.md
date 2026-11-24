# SEO Report package Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** Ship `Esatto.Umbraco.Backoffice.SeoReport` 1.0.0: Nova's GA4 + Search Console SEO Report as a standalone Umbraco 18 package with a Settings tab, encrypted Google key and English translation keys.

**Architecture:** Mirror SEO Health (`Esatto.Umbraco.Backoffice.Seo`): composer-registered services, own NPoco tables + migration plan, `IRecurringBackgroundJob`, Management API controller, Vite/TypeScript/Lit client with one element per tab. Nova's logic (`libs/Manadsrapport.Core`, `src/Features/Backoffice/Seo`) is ported, not referenced. The server sends keys + arguments only; the client turns them into English text.

**Tech Stack:** net10.0, Umbraco 18, NPoco, ASP.NET Core Data Protection, `Google.Apis.AnalyticsData.v1beta` (stable), `Google.Apis.SearchConsole.v1`, TypeScript + Vite + Lit + vitest, xUnit + NSubstitute.

**Spec:** `docs/superpowers/specs/2026-11-19-seoreport-design.md` (approved 2026-11-19). Read it first; this plan names the decisions the spec leaves open.

**Nova source** (read-only reference, `c:\src\Esatto.Web.Nova`): `libs/Manadsrapport.Core/{Agents,Processing,Models,Auth}`, `src/Features/Backoffice/Seo/*.cs`, `src/wwwroot/App_Plugins/esatto/seo-dashboard.js`, `tests/Esatto.Web.Nova.Tests/Unit/Features/Seo*Tests.cs`, `tasks/lessons.md` + `tasks/session-log.md` (search "SEO"). **Sibling to copy patterns from** (`c:\src\Esatto.Packages`): `Esatto.Umbraco.Backoffice.Seo` (csproj, composer, `Persistence/`, `Settings/SettingsService.cs`, `Scanning/SeoScanJob.cs`, `Scanning/ScanCoordinator.cs`, `Client/`), its `.Tests`, and `Esatto.Umbraco.Backoffice.NavigationOrder/Client/src/lang` for localization.

All commands run from `c:\src\Esatto.Packages` unless stated. `<P>` = `Esatto.Umbraco.Backoffice.SeoReport`, `<T>` = `Esatto.Umbraco.Backoffice.SeoReport.Tests`.

## Global Constraints

- Package ID/assembly/root namespace/folder `Esatto.Umbraco.Backoffice.SeoReport`; **Umbraco 18 only, net10.0**; `Umbraco.Cms.Api.Management` 18.0.0 and `Umbraco.Cms.Web.Common` 18.0.0 as minimums.
- GA4 client **`Google.Apis.AnalyticsData.v1beta`** (stable); Search Console **`Google.Apis.SearchConsole.v1`**. No prerelease dependency: `dotnet pack` must not warn **NU5104**.
- **English only.** Every user-facing text is a key in `Client/src/lang/en.ts` under section `esattoSeoReport` (looked up `esattoSeoReport_<key>`); **no Swedish file**; **the server never sends prose** (keys + arguments, `YYYY-MM` periods, raw numbers).
- **Key encryption:** ASP.NET Core Data Protection, purpose `Esatto.Umbraco.Backoffice.SeoReport.ServiceAccountKey.v1`; key is write-only (no endpoint returns it), never logged, never in an exception message or ProblemDetails; accepted only as JSON with `type: "service_account"`, `client_email`, `private_key`, ≤ 16 KB.
- Config `Esatto:SeoReport:*` wins over database and shows read-only; `ServiceAccountJson` / `ServiceAccountKeyFile` are configuration-only keys.
- Dashboard: label **"SEO Report"**, pathname `seo-report`, alias `Esatto.Umbraco.Backoffice.SeoReport.Dashboard`, element `esatto-seo-report-dashboard`, weight `-105`, Content section. **Every custom element the client defines is prefixed `esatto-seo-report-`** (element names are global to the backoffice page; Nova defines `esatto-seo-dashboard`).
- Management API route `esatto/seo-report`, group `Esatto SEO Report`. Reading reports, Refresh, Fetch now, notes, targets: Content access. Settings, key, Test connection, user-group list: **Settings access**. `[Authorize]`/route attributes sit on the controller class itself.
- Archive depth **13 months**, never above Search Console's 16-month window; period is `YYYY-MM`, validated before it reaches a key or query; one fetch at a time; opening the dashboard never fetches; saving passing settings and **Fetch now** start the backfill at once.
- Databases: **SQL Server and SQLite**.
- Namespace gotcha: inline `Umbraco.Cms...` inside `Esatto.Umbraco.*` binds to `Esatto.Umbraco`; use `using` directives and short type names.
- Ported code gets **English comments** and a targeted DRY/clean-code pass without behaviour change.
- **Never `git commit`, tag or push without Carl's explicit approval** (CLAUDE.md). Every "Commit" step below means: stage, show the diff summary, and wait for Carl. **Carl runs `dotnet nuget push`.** Real Google responses and keys never enter git. README images use absolute `raw.githubusercontent.com/carl-schele-esatto/Esatto.Packages/main/<P>/docs/...` URLs.

## Review Focus

Inputs the spec implies but no ordinary test would think of; each is pinned by a test in the owning task.

1. **Search Console property typed in any form** (`EXAMPLE.com/`, `https://www.example.com` without slash, `sc-domain:Example.com`, spaces, `ftp://x`) must normalize or be rejected with a code, never stored as a silently-empty property. (Task 5)
2. **A month with no data** (new site; GA4 with no events; Search Console with zero rows) must produce a `noData` state, no division by zero in KPIs/ring/year-on-year, and no empty chart presented as a result. (Tasks 8, 12, 16)
3. **Pasted key variations**: BOM, CRLF, leading/trailing whitespace, `\n` escapes in `private_key`, a key *file path* that doesn't exist or isn't readable. (Tasks 3, 5)
4. **Two fetches at once**: double-clicking Fetch now, Fetch now while the scheduled job runs, the same request on two instances. One runs; the others report "already running". (Task 13)
5. **The key vanishes mid-backfill** (removed in the tab, or decrypt fails after a restart): the run stops cleanly with `keyUnreadable`/`notConfigured`, no unhandled exception, no retry storm. (Task 13)

---

### Task 1: Scaffold the package, test project and client

**Files:**
- Create: `<P>/<P>.csproj`, `<P>/README.md` (stub), `<P>/icon.png` (copy of `Esatto.Umbraco.Backoffice.Seo/icon.png`), `<P>/src/SeoReportComposer.cs`, `<P>/Client/{package.json,tsconfig.json,vite.config.ts}`, `<P>/Client/public/umbraco-package.json`, `<P>/Client/src/seo-report-dashboard.element.ts`, `<P>/Client/src/lang/en.ts`
- Create: `<T>/<T>.csproj` (SEO Health's test csproj + `Umbraco.Cms.Persistence.Sqlite` 18.0.0, project reference to `<P>`), `<T>/ScaffoldTests.cs`, `<T>/ElementNameTests.cs`
- Branch: `git switch -c feature/seo-report-package` first.

**Interfaces:**
- Produces: a building package + test project; `Client/src/lang/en.ts` default export `{ esattoSeoReport: { /* key: string | (...args) => string */ } }`; custom element `esatto-seo-report-dashboard` (stub); manifest with a `dashboard` and a `localization` (`meta.culture: "en"`, js `/App_Plugins/<P>/lang/en.js`) extension; vite entries `seo-report-dashboard` and `lang/en`.

- [ ] **Step 1: Failing tests.** `ElementNameTests` ported from the sibling with prefix `esatto-seo-report-`: every `@customElement('…')` under `Client/src` starts with the prefix, and the manifest `elementName` equals one defined element. `ScaffoldTests`: manifest has alias `Esatto.Umbraco.Backoffice.SeoReport.Dashboard`, `meta.pathname == "seo-report"`, `weight == -105`, condition `Umb.Condition.SectionAlias` = `Umb.Section.Content`; a `localization` extension with culture `en` and no other culture; `Esatto.Umbraco.Backoffice.SeoReport` is a valid `IComposer` type.
- [ ] **Step 2:** `dotnet test <T>` → FAIL (project missing / build error).
- [ ] **Step 3: Scaffold** by copying SEO Health's csproj (`PackageId`, `AssemblyName`, `RootNamespace` = `<P>`; description for SEO Report; tags `umbraco;umbraco-marketplace;umbraco-18;backoffice;seo;google-analytics;search-console;ga4;report`; drop AngleSharp and the `Knowledge` embed; add `Google.Apis.SearchConsole.v1` 1.75.0.4234 and `dotnet add package Google.Apis.AnalyticsData.v1beta` (no `--prerelease`); keep the security-override and `NuGetAuditSuppress` blocks, `StaticWebAssetBasePath=/`, `BuildBackofficeClient`, AutoPush off, `InternalsVisibleTo` for `<T>`; source-code link: `RepositoryUrl` repo root, `PackageProjectUrl` `.../tree/main/<P>`, `RepositoryType` git). Client: copy SEO Health's `package.json`/`tsconfig.json`/`vite.config.ts`, `outDir ../wwwroot/App_Plugins/<P>`, two entries. Composer is an empty `IComposer`.
- [ ] **Step 4:** `dotnet test <T>` → PASS; `dotnet build <P>` → 0 warnings except those SEO Health also has; `npm run build --prefix <P>/Client` → emits `seo-report-dashboard.js` and `lang/en.js`.
- [ ] **Step 5: Commit** (with Carl's approval): `git add <P> <T>`; message `SeoReport: scaffold package, tests and client`.

---

### Task 2: Period, contract and key registry

**Files:**
- Create: `<P>/src/Reporting/{Period.cs,TextRef.cs,Report.cs,ReportKeys.cs}`, `<P>/Client/src/lang/key-vectors.json`
- Test: `<T>/PeriodTests.cs`, `<T>/ReportContractTests.cs`, `<T>/ReportKeysTests.cs`

**Interfaces:**
- Produces: `readonly record struct Period(int Year, int Month)` with `static bool TryParse(string? s, out Period p)` (exactly `YYYY-MM`, month 01–12, no whitespace), `static Period Parse(string)` (throws `ArgumentException`), `static Period FromDate(DateOnly)`, `Period AddMonths(int)`, `Period Previous()`, `IComparable<Period>`, `ToString()` → `"2026-07"`.
- Produces: `sealed record TextRef(string Key, IReadOnlyDictionary<string, object?>? Args = null)`.
- Produces: `Report` and children, a **trimmed port** of `libs/Manadsrapport.Core/Models/Report.cs`, same `JsonPropertyName`s for retained members. Removed: `client`, `client_name`, `agency`, `report_period_label`, sections `keywords`, `positions`, `deliverables`, `actions`, every `label`/`title`/`subtitle`/`description`/`period_label` string. Changed: `Annotation(string? Period, string Severity, TextRef Text)` (texts `<key>Title` + `<key>Text`); `ChartBlock.Insights` is `List<TextRef>`; `DataStatusNote` is `TextRef?`; `Kpi`, `Series`, `ChartBlock` keep `Key`/`Id` as the text base.
- Produces: `static class ReportKeys { static string Lookup(string area, string baseId, string part); static IReadOnlyCollection<string> All { get; } }`. Rule: `<area><PascalCase(baseId)><Part>` with `traffic_organic` → `TrafficOrganic`, areas `kpi|chart|series|annotation|insight|narrative|opportunity|error|ui`, parts `Label|Description|Title|Subtitle|Text`. Tasks 8, 10, 12 register the bases they emit.

- [ ] **Step 1: Failing tests.** `PeriodTests`: `TryParse("2026-07")` true and `ToString()=="2026-07"`; `TryParse` false for `"2026-13"`, `"2026-7"`, `"2026-00"`, `"../2026"`, `" 2026-07"`, `""`, `null`; `Period.Parse("2026-01").Previous()=="2025-12"`; `AddMonths(-13)` from `2026-11` is `2025-10`; ordering `2025-12 < 2026-01`. `ReportContractTests`: serializing a minimal `Report` yields no property named `keywords|positions|deliverables|actions|agency|client|client_name|report_period_label|label|description`. `ReportKeysTests`: `Lookup("kpi","traffic_organic","Label") == "kpiTrafficOrganicLabel"`; every vector in `Client/src/lang/key-vectors.json` (`[{area,baseId,part,expected}]`, at least 5 rows) matches.
- [ ] **Step 2:** `dotnet test <T> --filter "FullyQualifiedName~PeriodTests|FullyQualifiedName~ReportContractTests|FullyQualifiedName~ReportKeysTests"` → FAIL.
- [ ] **Step 3: Implement** the types above; `ReportKeys.All` starts empty and is built from per-area base lists added by later tasks. Create `key-vectors.json`.
- [ ] **Step 4:** same command → PASS.
- [ ] **Step 5: Commit** (with Carl's approval): `SeoReport: Period, report contract and key registry`.

---

### Task 3: Key validation and encryption

**Files:**
- Create: `<P>/src/Google/{ServiceAccountKey.cs,SecretProtector.cs}`
- Test: `<T>/ServiceAccountKeyTests.cs`, `<T>/SecretProtectorTests.cs`

**Interfaces:**
- Produces: `static class ServiceAccountKey { const int MaxBytes = 16 * 1024; static KeyCheck Validate(string? json); }`, `record KeyCheck(bool IsValid, string? ErrorCode, string? ClientEmail)`; error code `keyInvalid` for every failure.
- Produces: `sealed class SecretProtector(IDataProtectionProvider provider)` with `string Protect(string plain)` and `bool TryUnprotect(string cipher, out string plain)` (false, never throws, on any `CryptographicException` or malformed input).

- [ ] **Step 1: Failing tests.** `Validate`: a valid key (synthetic RSA PEM generated in the test) → `IsValid`, `ClientEmail=="svc@proj.iam.gserviceaccount.com"`; `type:"authorized_user"` → `keyInvalid`; missing `client_email`; missing `private_key`; `"not json"`; 16 KB + 1 → `keyInvalid`; leading BOM + CRLF + surrounding whitespace → valid; `private_key` containing literal `\n` escapes → valid. **No-leak:** for each invalid input containing the marker `LEAK-MARKER-9f3a`, `result.ToString()` and `ErrorCode` do not contain it. `SecretProtector`: round trip equals input; ciphertext does not contain the plaintext; a second `EphemeralDataProtectionProvider` cannot decrypt (`TryUnprotect` false, no exception); `"garbage"` → false.
- [ ] **Step 2:** `dotnet test <T> --filter "FullyQualifiedName~ServiceAccountKeyTests|FullyQualifiedName~SecretProtectorTests"` → FAIL.
- [ ] **Step 3: Implement.** `Validate` uses `System.Text.Json` (`JsonDocumentOptions { AllowTrailingCommas = true }`), strips a BOM, never includes input in any returned value. Protector purpose string is a public const `SecretProtector.Purpose`.
- [ ] **Step 4:** same command → PASS.
- [ ] **Step 5: Commit** (with Carl's approval): `SeoReport: service-account key validation and Data Protection wrapper`.

---

### Task 4: Persistence

**Files:**
- Create: `<P>/src/Persistence/{Entities.cs,ISeoReportStore.cs,NPocoSeoReportStore.cs,SeoReportMigrationPlan.cs}`
- Test: `<T>/Support/InMemoryStore.cs` (test double used by Tasks 5, 11, 13), `<T>/StoreSqlTests.cs`

**Interfaces:**
- Produces tables (all prefixed `esattoSeoReport`): `Report` (`period` nvarchar(7) PK, `json` NVARCHARMAX, `fetchedUtc`, `contractVersion` int), `Note` (`period` PK, `text` nvarchar(1000)), `Document` (`key` nvarchar(100) PK, `json` NVARCHARMAX, `updatedUtc`; keys `settings`, `protectedKey`, `targets`, `fetchState`), `Lock` (`name` nvarchar(50) PK, `owner` nvarchar(100), `expiresUtc`).
- Produces `interface ISeoReportStore`: `Task<ReportEntity?> GetReportAsync(Period)`, `Task SaveReportAsync(ReportEntity)`, `Task<IReadOnlyList<Period>> ListPeriodsAsync()`, `Task<string?> GetNoteAsync(Period)`, `Task SaveNoteAsync(Period, string? text)` (null/empty deletes the row), `Task<string?> GetDocumentAsync(string key)`, `Task SaveDocumentAsync(string key, string json)`, `Task DeleteDocumentAsync(string key)`, `Task<bool> TryAcquireLockAsync(string name, string owner, TimeSpan ttl)` (true if free, expired, or already owned by `owner`), `Task ReleaseLockAsync(string name, string owner)` (only the owner releases).
- Produces `SeoReportMigrationPlan : MigrationPlan("Esatto.Umbraco.Backoffice.SeoReport")` with one idempotent step creating missing tables; `RunSeoReportMigration` notification handler registered in the composer (pattern: `RunSeoMigration` in SEO Health).

- [ ] **Step 1: Failing tests.** `StoreSqlTests` (read the sibling's `SqlServerTests.cs` first and follow it): render every hand-written statement of `NPocoSeoReportStore` under `SqlServerSyntaxProvider` and `SqliteSyntaxProvider` and assert both succeed and use `GetQuotedTableName`/`GetQuotedColumnName` (no hard-coded quoting). `InMemoryStore` implements the interface with the lock semantics above; add `InMemoryStoreTests` proving them (second owner refused until expiry, release by non-owner ignored, re-acquire by owner extends).
- [ ] **Step 2:** `dotnet test <T> --filter "FullyQualifiedName~StoreSqlTests|FullyQualifiedName~InMemoryStoreTests"` → FAIL.
- [ ] **Step 3: Implement** entities (NPoco annotations as in the sibling's `Entities.cs`), store, migration plan, handler. Real-database behaviour is proven in Task 19.
- [ ] **Step 4:** same command → PASS.
- [ ] **Step 5: Commit** (with Carl's approval): `SeoReport: tables, store and migration plan`.

---

### Task 5: Settings, key sources and credentials

**Files:**
- Create: `<P>/src/Settings/{SeoReportSettings.cs,SettingsNormalizer.cs,BrandPattern.cs,SettingsService.cs,KeySource.cs}`, `<P>/src/Google/GoogleCredentialSource.cs`
- Test: `<T>/SettingsNormalizerTests.cs`, `<T>/BrandPatternTests.cs`, `<T>/SettingsServiceTests.cs`, `<T>/GoogleCredentialSourceTests.cs`

**Interfaces:**
- Consumes: `ISeoReportStore`, `SecretProtector`, `ServiceAccountKey.Validate`, `IConfiguration`, `TimeProvider`.
- Produces `sealed class SeoReportSettings { string? Ga4PropertyId; string? SearchConsoleSite; string[] BrandTerms; string? DisplayName; string[] FindingsUserGroups; bool AutoFetch = true; const string SectionName = "Esatto:SeoReport"; }` (mutable, binder-friendly).
- Produces `static class SettingsNormalizer { static string? NormalizeGa4(string? input); static string? NormalizeSearchConsole(string? input); }` (null = invalid; callers return code `ga4PropertyInvalid` / `searchConsoleInvalid`).
- Produces `static class BrandPattern { static string? Build(IEnumerable<string> terms); }` → `(?i)(a|b)`, null when no terms.
- Produces `enum KeySourceKind { None, Configuration, Database }`, `record KeyStatus(bool HasKey, KeySourceKind Source, string? ServiceAccountEmail, bool Unreadable)`, `record EffectiveSettings(SeoReportSettings Settings, IReadOnlyList<string> Locked, KeyStatus Key)`.
- Produces `sealed class SettingsService`: `Task<EffectiveSettings> GetAsync()`, `Task<EffectiveSettings> SaveAsync(SeoReportSettings incoming, string? newKeyJson, bool removeKey)` (throws `SettingsValidationException(string code)` for invalid IDs/key), `Task<SetupState> GetSetupStateAsync()` with `enum SetupState { NotConfigured, KeyUnreadable, Ready }`. Same one-minute cache + generation counter as SEO Health's `SettingsService`; configuration locks fields, listed as `ga4PropertyId`, `searchConsoleSite`, `brandTerms`, `displayName`, `findingsUserGroups`, `autoFetch`.
- Produces `interface IGoogleCredentialSource { Task<CredentialResult> GetAsync(CancellationToken ct); }`, `record CredentialResult(GoogleCredential? Credential, string? ClientEmail, KeySourceKind Source, string? ErrorCode)` with codes `notConfigured`, `keyUnreadable`, `keyInvalid`. Precedence: configuration `ServiceAccountJson`, then `ServiceAccountKeyFile`, then the protected database key. Saving or removing the key invalidates the cached credential at once.

- [ ] **Step 1: Failing tests.** `SettingsNormalizerTests`: GA4 `"123456789"`→`"properties/123456789"`; `" Properties/123456789 "`→`"properties/123456789"`; `""`, `"abc"`, `"properties/"`, `"properties/12a"`, `"properties/properties/1"` → null. Search Console: `"example.com"`→`"sc-domain:example.com"`; `"EXAMPLE.com/"`→`"sc-domain:example.com"`; `"sc-domain:Example.com"`→`"sc-domain:example.com"`; `"https://www.example.com"`→`"https://www.example.com/"`; `"HTTP://Example.com/Path"`→`"http://example.com/Path/"`; `"ftp://x"`, `"has spaces.com"`, `""` → null. `BrandPatternTests`: `["acme","acme ab"]`→`"(?i)(acme|acme ab)"`; `["a.b"]`→`"(?i)(a\\.b)"`; blanks and duplicates dropped; none → null. `SettingsServiceTests` (with `InMemoryStore`, `EphemeralDataProtectionProvider`): database values used without config; config value wins and appears in `Locked`; config `ServiceAccountJson` → `Source==Configuration` and a stored database key is ignored; saving a key stores only ciphertext (assert the store never sees marker `LEAK-MARKER-9f3a`); `removeKey` deletes it; undecryptable stored key → `Unreadable==true`, `HasKey==false`, state `KeyUnreadable`; invalid GA4 id → `SettingsValidationException("ga4PropertyInvalid")`; save-then-`GoogleCredentialSource.GetAsync` reflects the new key with no restart. `GoogleCredentialSourceTests`: BOM/CRLF key pasted via `SaveAsync` works; `ServiceAccountKeyFile` pointing at a missing or unreadable path → `ErrorCode=="keyUnreadable"`; pointing at a readable file that isn't a service-account key → `"keyInvalid"`.
- [ ] **Step 2:** `dotnet test <T> --filter "FullyQualifiedName~SettingsNormalizerTests|FullyQualifiedName~BrandPatternTests|FullyQualifiedName~SettingsServiceTests|FullyQualifiedName~GoogleCredentialSourceTests"` → FAIL.
- [ ] **Step 3: Implement.** Model `SettingsService` on SEO Health's (`Apply` binds config one value at a time, tolerates a bad value). Secrets (`ServiceAccountJson`, `ServiceAccountKeyFile`) are read from `IConfiguration`/`IOptionsMonitor` only and never copied into `SeoReportSettings`. Use `CredentialFactory.FromStreamAsync<ServiceAccountCredential>` as Nova's `JsonGoogleCredentialSource` does (see its comment on CS0618). Document id `settings` and `protectedKey` in the `Document` table.
- [ ] **Step 4:** same command → PASS.
- [ ] **Step 5: Commit** (with Carl's approval): `SeoReport: settings, key sources and credentials`.

---

### Task 6: GA4 agents on the REST client, then the live comparison (GATE A)

**Files:**
- Create: `<P>/src/Google/{IGoogleClients.cs,GoogleClients.cs,Ga4Agent.cs,Ga4DailyAgent.cs}`
- Test: `<T>/Ga4AgentTests.cs`, `<T>/Fixtures/ga4-request-golden-*.json` (request shapes only; no real data)

**Interfaces:**
- Consumes: `IGoogleCredentialSource`.
- Produces `interface IGoogleClients { Task<AnalyticsDataService> GetAnalyticsDataAsync(CancellationToken ct); Task<SearchConsoleService> GetSearchConsoleAsync(CancellationToken ct); }`.
- Produces a port of Nova's `Ga4Agent` / `Ga4DailyAgent` with the same public names and records (`Ga4Row`, `Ga4DailySeries`, `Ga4DailyWindow`, `Ga4DailyPoint`, `Ga4DailySource`, segment constants `Ga4Agent.SegmentOrganic|SegmentTotal|SegmentOrganicGoogle`), constructors taking `(string propertyId, IGoogleClients clients)`; `static RunReportRequest BuildRequest(string propertyId, DateOnly reportMonth, string segment)` now returns the REST type; `static IReadOnlyList<Ga4Row> MapResponse(RunReportResponse)` replaces `MapRaw(string)`; `Task<IReadOnlyList<Ga4Row>> FetchAsync(Period period, CancellationToken ct)`.

- [ ] **Step 1: Failing tests.** Golden request shapes: run Nova's own `Ga4Agent.BuildRequest` for each segment (and `Ga4DailyAgent.BuildSeriesRequest`) once at development time, convert to camelCase JSON, save as `ga4-request-golden-<segment>.json`; `BuildRequest_MatchesNovaGolden(segment)` asserts the new request serializes to identical JSON (dimensions, metrics, date ranges, filters, `property`). `MapResponse_FromSyntheticResponse` builds a `RunReportResponse` by hand for two periods × three segments and asserts the exact `Ga4Row` list. `DateRangeFor("2026-07")` equals Nova's.
- [ ] **Step 2:** `dotnet test <T> --filter "FullyQualifiedName~Ga4AgentTests"` → FAIL.
- [ ] **Step 3: Implement** the port. If the REST request types can't express something the gRPC ones did, stop and tell Carl before improvising.
- [ ] **Step 4:** same command → PASS.
- [ ] **Step 5 (GATE A — owner: the site owner for the key, then me): live comparison.** Prerequisite: a new read-only service account (Viewer on a GA4 property and in Search Console) with the key available to this machine as a local file or user secret, never in git or chat. Write a throwaway console tool in the scratchpad (not committed) that, for the same property and the last 13 months, runs Nova's old `Ga4Agent.FetchRawAsync` + `MapRaw` and the new `Ga4Agent.FetchAsync`, and diffs the `Ga4Row` lists and the `Ga4DailySeries`. Real responses go only to a gitignored `.local/` folder. **Pass = zero differences.** Fail → stop, report the differences to Carl; Tasks 7+ wait.
- [ ] **Step 6: Commit** (with Carl's approval): `SeoReport: GA4 agents on the stable REST client`.

---

### Task 7: Search Console agent

**Files:**
- Create: `<P>/src/Google/GscAgent.cs`
- Test: `<T>/GscAgentTests.cs`

**Interfaces:**
- Consumes: `IGoogleClients`.
- Produces a port of Nova's `GscAgent` with the same public names and records (`GscFetch`, `GscRow`, `GscQueryRow`, `GscQueryPageRow`, `GscPageRow`); constructor `(string siteUrl, string brandRegex, IGoogleClients clients, DateOnly? today = null)`; `Task<GscFetch> FetchAsync(Period period, CancellationToken ct)`; statics `BuildQueriesRequest`, `BuildQueryPagesRequest`, `BuildPagesRequest`, `BuildProbeRequest`, `BuildPeriodRequest`, `DataThroughFrom`, `EarliestAvailableMonth(DateOnly)`, `LatestCompleteMonth(DateOnly)`, `ValidateBrandRegex(string?)`, `MapResponse(...)`.

- [ ] **Step 1: Failing tests (characterization).** For `today` ∈ {first of a month, mid-month, month end, 2026-03-01, 2026-12-31} assert `EarliestAvailableMonth` and `LatestCompleteMonth(DataThroughFrom(probe))` equal the values Nova's methods return (compute them by running Nova's code once and paste the results as literals). Request builders: assert the brand split uses `queryRegex` with `includingRegex`/`excludingRegex` of the `(?i)(…)` pattern from `BrandPattern`, and the date range and row limits match Nova's. `ValidateBrandRegex("(?i)(acme|acme ab)")` passes; `"("` throws `ArgumentException` whose message does not include the pattern's user text beyond the offending character class. `MapResponse` over a hand-built `SearchAnalyticsQueryResponse` yields the exact `GscFetch`.
- [ ] **Step 2:** `dotnet test <T> --filter "FullyQualifiedName~GscAgentTests"` → FAIL.
- [ ] **Step 3: Implement** the port; Search Console was already stable-REST in Nova, so request/response types are unchanged.
- [ ] **Step 4:** same command → PASS.
- [ ] **Step 5: Commit** (with Carl's approval): `SeoReport: Search Console agent`.

---

### Task 8: Report mapper (keys instead of Swedish)

**Files:**
- Create: `<P>/src/Reporting/ReportMapper.cs` (port of `libs/Manadsrapport.Core/Processing/TrafficMapper.cs`); extend `ReportKeys.cs` and `Client/src/lang/en.ts`
- Test: `<T>/ReportMapperTests.cs`

**Interfaces:**
- Consumes: `Report` contract (Task 2), `Ga4Row`, `GscFetch`.
- Produces `static Report ReportMapper.Build(Period reportPeriod, IReadOnlyList<Ga4Row> rows, GscFetch? gsc = null, string? gscProperty = null, string? ga4Property = null, string? brandRegex = null)` (Nova's `Build` minus `client`, `clientName`, `agency`).

- [ ] **Step 1: Failing tests.** Build a whole report from synthetic GA4/GSC rows (Nova's `tasks/lessons.md` describes the technique) for 14 months and assert: organic and total series values; year-on-year trend percent; the engaged-visits ring value is within 0–100; KPI keys exactly `[…]` as Nova emits them (copy the list from `TrafficMapper`); annotation with key `noKeyEvents` and severity `warning` when GA4 key events are zero in every month. **No-data month** (Review Focus 2): rows empty → `Build` doesn't throw, `DataStatus == "noData"`, no KPI value is `NaN`/`Infinity`, ring value null. **Key registry:** walk the serialized report; every `Key`, `Id`, `Series.Key`, `Annotation.Text.Key`, `TextRef.Key` resolves via `ReportKeys.Lookup` into `ReportKeys.All`; no property of the JSON contains a string with a Swedish letter (`åäöÅÄÖ`). **`EnglishTextsTests.EveryRegisteredKeyHasEnglishText`:** reads `<P>/Client/src/lang/en.ts` as text and asserts every entry of `ReportKeys.All` appears as a property name (`\bkey\s*:`); it stays green as Tasks 10 and 12 register more keys and add their wording.
- [ ] **Step 2:** `dotnet test <T> --filter "FullyQualifiedName~ReportMapperTests"` → FAIL.
- [ ] **Step 3: Implement** the port: every Swedish literal (≈ 96 lines) becomes a key + args; register the bases in `ReportKeys.All`; add the English wording to `lang/en.ts`, translated from Nova's Swedish (translate, don't invent). Replace `PeriodLabel` use with raw `YYYY-MM`.
- [ ] **Step 4:** same command → PASS.
- [ ] **Step 5: Commit** (with Carl's approval): `SeoReport: report mapper with translation keys`.

---

### Task 9: Analysis logic, part 1 (movement, opportunities, visibility, targets)

**Files:**
- Create: `<P>/src/Reporting/{Movement.cs,Opportunities.cs,Visibility.cs,TargetAnalysis.cs,TargetHistory.cs}` (ports of Nova `SeoMovement`, `SeoOpportunities`, `SeoVisibility`, `SeoTargetAnalysis`, `SeoTargetHistory`)
- Test: `<T>/{MovementTests,OpportunitiesTests,VisibilityTests,TargetAnalysisTests,TargetHistoryTests}.cs` (ports of Nova's `Seo*Tests.cs` of the same names)

**Interfaces:**
- Produces the same public records and static methods as the Nova originals (read each file; names lose the `Seo` prefix: `SeoMovement`→`Movement`, `SeoVisibilityMonth`→`VisibilityMonth`, `SeoHistoryRow`→`HistoryRow`, …). Any Swedish text a method returned becomes a `TextRef` or a key string registered in `ReportKeys.All` (`opportunity*`, `target*` keys).

- [ ] **Step 1: Failing tests.** Port Nova's tests file by file; every assertion on Swedish text becomes an assertion on key and `Args`; all existing numeric assertions stay verbatim.
- [ ] **Step 2:** `dotnet test <T> --filter "FullyQualifiedName~MovementTests|FullyQualifiedName~OpportunitiesTests|FullyQualifiedName~VisibilityTests|FullyQualifiedName~TargetAnalysisTests|FullyQualifiedName~TargetHistoryTests"` → FAIL.
- [ ] **Step 3: Implement** the ports (English comments; keep the "position 0 means no value" and inclusive top-3 rules).
- [ ] **Step 4:** same command → PASS.
- [ ] **Step 5: Commit** (with Carl's approval): `SeoReport: movement, opportunities, visibility and target analysis`.

---

### Task 10: Analysis logic, part 2 (narrative, daily chart, findings filter)

**Files:**
- Create: `<P>/src/Reporting/{Narrative.cs,DailyChart.cs,FindingsFilter.cs}` (ports of `SeoNarrative`, `SeoDailyChart`, `SeoFindingsFilter`)
- Test: `<T>/{NarrativeTests,DailyChartTests,FindingsFilterTests}.cs`

**Interfaces:**
- Produces `record Statement(TextRef Text)` replacing `SeoStatement(Key, Text)`; `Narrative.*` constants keep their string values (`kallor` etc. become English keys `sources`, `nonBrand`, `movement`, `season`); `FindingsFilter.IsAllowed(IEnumerable<string>? userGroupAliases, IEnumerable<string>? allowedGroups)` and `FindingsFilter.StripFindings(Report?)` unchanged in behaviour (alias match case-insensitive, empty list = everyone, strips annotations with severity `warning` only).

- [ ] **Step 1: Failing tests.** Port Nova's three test files with text assertions turned into key + args assertions; keep "no sentence without support in the numbers" cases (missing data → statement absent).
- [ ] **Step 2:** `dotnet test <T> --filter "FullyQualifiedName~NarrativeTests|FullyQualifiedName~DailyChartTests|FullyQualifiedName~FindingsFilterTests"` → FAIL.
- [ ] **Step 3: Implement**; register `narrative*` keys and add their English wording (function-valued entries for numbers/plurals).
- [ ] **Step 4:** same command → PASS.
- [ ] **Step 5: Commit** (with Carl's approval): `SeoReport: narrative, daily chart and findings filter`.

---

### Task 11: Report, note, target and daily-traffic services

**Files:**
- Create: `<P>/src/Reporting/{ISeoReportService.cs,SeoReportService.cs,NoteService.cs,TargetQueryService.cs,DailyTrafficService.cs}`
- Test: `<T>/{SeoReportServiceTests,NoteServiceTests,TargetQueryServiceTests,DailyTrafficServiceTests}.cs`

**Interfaces:**
- Consumes: `ISeoReportStore`, `SettingsService`, `IGoogleClients`, `GscAgent`, `Ga4Agent`, `ReportMapper`, `IMemoryCache`, `TimeProvider`.
- Produces (public, for sites that build their own summary): `interface ISeoReportService { Task<IReadOnlyList<string>> GetAvailablePeriodsAsync(CancellationToken ct = default); Period ExpectedLatestPeriod { get; } Task<bool> IsUpToDateAsync(CancellationToken ct = default); Task<Report?> GetReportAsync(string? period = null, CancellationToken ct = default); Task<DateTimeOffset?> GetFetchedUtcAsync(string? period = null, CancellationToken ct = default); Task<Report> RefreshAsync(Period period, CancellationToken ct = default); }` (periods oldest first; `RefreshAsync` throws `ReportFetchException(string code)` for credential problems only (codes from `CredentialResult.ErrorCode`); Google API exceptions propagate unchanged).
- Produces `interface INoteService { Task<string?> GetAsync(Period); Task<string?> SaveAsync(Period, string?); const int MaxLength = 1000; }`; `interface ITargetQueryService { Task<IReadOnlyList<string>> GetAsync(); Task<IReadOnlyList<string>> SaveAsync(IEnumerable<string>?); const int MaxTerms = 50, MaxTermLength = 100; }` (lower-cased, deduplicated, trimmed); `interface IDailyTrafficService { Task<(Ga4DailySeries, Ga4DailyWindow)> GetSeriesAsync(string? month, CancellationToken); Task<IReadOnlyList<Ga4DailySource>> GetSourcesAsync(DateOnly day, CancellationToken); }`.

- [ ] **Step 1: Failing tests.** Port the behaviours in Nova's `SeoReportService` (read its tests' equivalents in `Seo*Tests` and its code comments): archive keeps one report per period and never overwrites another month; `GetAvailablePeriodsAsync` is oldest first; `IsUpToDateAsync` true exactly when `ExpectedLatestPeriod` is archived; `GetReportAsync(null)` returns the newest; a report with an older `contractVersion` counts as missing; period cache 5 min, report cache 1 h (use `FakeTimeProvider`); `RefreshAsync` serializes (two concurrent calls → one fetch); invalid period never reaches the store (`Period` type). Notes: >1000 chars truncated, whitespace-only deletes. Targets: `Normalize` rules. Daily: cache key includes the date and month so a day change misses the cache (30 min TTL).
- [ ] **Step 2:** `dotnet test <T> --filter "FullyQualifiedName~SeoReportServiceTests|FullyQualifiedName~NoteServiceTests|FullyQualifiedName~TargetQueryServiceTests|FullyQualifiedName~DailyTrafficServiceTests"` → FAIL.
- [ ] **Step 3: Implement** with a `SemaphoreSlim` around one month, as Nova does. Callers (Tasks 13 and 14) map Google exceptions with Task 12's `GoogleErrorMapper`, so nothing here depends on Task 12. Never log the key. Define `const int ContractVersion = 1`.
- [ ] **Step 4:** same command → PASS.
- [ ] **Step 5: Commit** (with Carl's approval): `SeoReport: report, note, target and daily services`.

---

### Task 12: Error codes and Test connection

**Files:**
- Create: `<P>/src/Google/{ErrorCodes.cs,GoogleErrorMapper.cs,ConnectionTester.cs}`
- Test: `<T>/{GoogleErrorMapperTests,ConnectionTesterTests}.cs`

**Interfaces:**
- Produces `static class ErrorCodes` constants: `notConfigured`, `keyUnreadable`, `keyInvalid`, `apiDisabled`, `noAccess`, `propertyNotFound`, `noData`, `quota`, `unknown`; each registered as `error<Code>Text` (and `…Title`) in `ReportKeys.All`.
- Produces `static class GoogleErrorMapper { static ProblemInfo Map(Exception ex); }`, `record ProblemInfo(string Code, IReadOnlyDictionary<string, object?>? Args)` — Args carry `api` (which API is disabled) or `source` (`ga4`/`searchConsole`); never the exception message.
- Produces `sealed class ConnectionTester { Task<ConnectionTestResult> TestAsync(ConnectionTestRequest request, CancellationToken ct); }` with `record ConnectionTestRequest(string? Ga4PropertyId, string? SearchConsoleSite, string? ServiceAccountJson)` (blank JSON = saved/configured key), `record SourceTest(bool Ok, string? ErrorCode, IReadOnlyDictionary<string, object?>? Args)`, `record ConnectionTestResult(string? ServiceAccountEmail, SourceTest Key, SourceTest Ga4, SourceTest SearchConsole)`. Single-flight, minimum 5 s between runs.

- [ ] **Step 1: Failing tests.** Mapper: `GoogleApiException` 403 "SERVICE_DISABLED" → `apiDisabled` with `api`; 403 "PERMISSION_DENIED" → `noAccess` with `source`; 404 → `propertyNotFound`; 429 → `quota`; any other exception → `unknown`; **no-leak:** an exception whose message contains `LEAK-MARKER-9f3a` yields a `ProblemInfo` whose serialization lacks it. Tester: bad key JSON → `Key.ErrorCode=="keyInvalid"` and GA4/GSC not attempted; good key, GA4 403 → `Ga4.ErrorCode=="noAccess"` while Search Console is still tested; a Search Console probe returning zero rows for a valid site → `Ok` with `ErrorCode=="noData"`; the wrong-form Search Console site (valid-looking but unverified) → `propertyNotFound`, never silent success; a second call within 5 s returns the previous result or `quota`-style refusal without hitting Google; the result never contains the key.
- [ ] **Step 2:** `dotnet test <T> --filter "FullyQualifiedName~GoogleErrorMapperTests|FullyQualifiedName~ConnectionTesterTests"` → FAIL.
- [ ] **Step 3: Implement.** GA4 check = tiny `runReport` for yesterday (one metric); Search Console check = `sites.get` then the existing probe request. Add English wording for every code (what happened, what to do, the service-account email to add).
- [ ] **Step 4:** same command → PASS.
- [ ] **Step 5: Commit** (with Carl's approval): `SeoReport: error mapping and Test connection`.

---

### Task 13: Fetch coordinator and refresh job

**Files:**
- Create: `<P>/src/Jobs/{FetchCoordinator.cs,SeoReportRefreshJob.cs}`
- Test: `<T>/{FetchCoordinatorTests,SeoReportRefreshJobTests}.cs`

**Interfaces:**
- Consumes: `ISeoReportService`, `SettingsService`, `ISeoReportStore` (lock `fetch`, document `fetchState`), `GscAgent.EarliestAvailableMonth`, `GoogleErrorMapper` (Task 12), `TimeProvider`, `IHostApplicationLifetime`.
- Produces `sealed class FetchCoordinator { FetchStartResult TryStart(string startedBy); FetchProgress? Progress { get; } }`, `enum FetchStartResult { Started, AlreadyRunning, NotConfigured }`, `record FetchProgress(bool Running, int Done, int Total, string? CurrentPeriod, string? ErrorCode)`. Backfills missing periods newest first, one at a time, holding the database lock `fetch` (TTL 30 min, renewed per month).
- Produces `static IReadOnlyList<Period> WantedPeriods(Period expectedLatest, DateOnly today)`: 13 months ending at `expectedLatest`, clipped to `GscAgent.EarliestAvailableMonth(today)`, newest first.
- Produces `sealed class SeoReportRefreshJob : IRecurringBackgroundJob`: `Period` 15 minutes, `Delay` 5 minutes, `ServerRoles = [Single, SchedulingPublisher]`; does nothing (one Information log per process) when not configured or `AutoFetch` is false; skips when nothing is missing; skips when `fetchState` shows a failure less than 1 hour ago; otherwise `coordinator.TryStart("scheduled")`.

- [ ] **Step 1: Failing tests.** `WantedPeriods` for 2026-10 expected-latest on 2026-11-19 has 13 items, first `2026-10`, last `2025-10`, clipped when the GSC window is shorter. Coordinator: starts and reports progress `Done/Total`; **two simultaneous `TryStart`** → exactly one `Started`, one `AlreadyRunning` (Review Focus 4); a lock held by another owner → `AlreadyRunning`; `NotConfigured` when `SetupState != Ready`; **key removed mid-run** (credential source switches to `notConfigured` after month 2) → run ends, `Progress.ErrorCode=="notConfigured"`, no exception escapes, remaining months not attempted (Review Focus 5); a failing month records `fetchState` and the run continues or stops per month rule below; lock released on completion and on failure. Job: each skip case above; failed-within-an-hour skip; success path calls `TryStart` once.
- [ ] **Step 2:** `dotnet test <T> --filter "FullyQualifiedName~FetchCoordinatorTests|FullyQualifiedName~SeoReportRefreshJobTests"` → FAIL.
- [ ] **Step 3: Implement.** Catch `ReportFetchException` (use its code) and any other exception (map it with `GoogleErrorMapper.Map`). A month failing with `quota`, `noAccess`, `apiDisabled`, `keyUnreadable`, `keyInvalid` or `notConfigured` stops the run (retrying won't help); other failures skip that month and continue. The coordinator is a singleton like SEO Health's `ScanCoordinator`; the background `Task` observes the host's stopping token.
- [ ] **Step 4:** same command → PASS.
- [ ] **Step 5: Commit** (with Carl's approval): `SeoReport: backfill coordinator and refresh job`.

---

### Task 14: Controller, authorization and wiring

**Files:**
- Create: `<P>/src/SeoReportController.cs`, `<P>/src/Settings/SettingsResponse.cs`; modify `<P>/src/SeoReportComposer.cs`, create `<P>/src/ServiceCollectionExtensions.cs`
- Test: `<T>/{ControllerRoutingTests,ControllerTests,ApiLeakTests}.cs`

**Interfaces:**
- Consumes: everything above plus `IBackOfficeSecurityAccessor`, `IUserGroupService`.
- Produces `SeoReportController` (`[ApiController]`, `[VersionedApiBackOfficeRoute("esatto/seo-report")]`, `[ApiExplorerSettings(GroupName = "Esatto SEO Report")]`, `[Authorize(Policy = AuthorizationPolicies.SectionAccessContent)]` on the class): `GET status` → `{ setup, missing[], fetch, upToDate, periods[], expectedLatestPeriod, displayName }`; `GET report?period`; `POST refresh?period`; `POST fetch`; `GET fetch/progress`; `GET analysis?period`; `GET daily?month`; `GET daily/sources?date`; `PUT note?period`; `GET targets`; `PUT targets`. **Settings access** (`[Authorize(Policy = AuthorizationPolicies.SectionAccessSettings)]` per action): `GET settings`, `PUT settings`, `POST settings/test`, `GET user-groups`.
- Produces `record SettingsResponse(SeoReportSettings Settings, IReadOnlyList<string> Locked, KeyStatus Key)` and `record PutSettingsRequest(SeoReportSettings Settings, string? ServiceAccountJson, bool RemoveKey)`. Errors are `ProblemDetails` with extension `code` (an `ErrorCodes` value or `ga4PropertyInvalid`/`searchConsoleInvalid`) and optional `args`. Settings responses send `Cache-Control: no-store`.
- Behaviour kept from Nova: the report response is run through `FindingsFilter.StripFindings` unless the current user's groups pass `IsAllowed(groups, settings.FindingsUserGroups)`; the archive keeps the whole report. A successful `PUT settings` whose connection test passes calls `FetchCoordinator.TryStart(userName)`. `GET analysis` ports the composition in Nova's `SeoDashboardController.Analysis` (lines ~377–495) over `Movement`, `Opportunities`, `TargetAnalysis`, `TargetHistory`, `Visibility` and `Narrative`; `GET daily` and `daily/sources` port its `Daily`/`DailySources` actions over `DailyChart` and `IDailyTrafficService`.

- [ ] **Step 1: Failing tests.** `ControllerRoutingTests` (precedent: Nova's `SeoDashboardControllerRoutingTests`): reflection asserts `[ApiController]`, route template, `[Authorize]` sit on `SeoReportController` itself, each Settings-only action has the Settings policy attribute, the report actions have none beyond the class policy. `ControllerTests`: a user outside `FindingsUserGroups` gets a report with no `warning` annotations; empty list → everyone sees them; `PUT settings` with invalid GA4 id → 400 with `code == "ga4PropertyInvalid"`; saving a valid key returns `Key.HasKey == true` and the email, not the key; `POST fetch` while running → 409-style `AlreadyRunning` body; `GET report?period=2026-13` → 400; settings GET/PUT responses carry `Cache-Control: no-store`. **`ApiLeakTests`:** with marker `LEAK-MARKER-9f3a` inside a saved key, serialize every response of every endpoint and every captured log message; assert the marker never appears.
- [ ] **Step 2:** `dotnet test <T> --filter "FullyQualifiedName~ControllerRoutingTests|FullyQualifiedName~ControllerTests|FullyQualifiedName~ApiLeakTests"` → FAIL.
- [ ] **Step 3: Implement** the controller; register all services in `AddSeoReport()` (idempotent; singletons for settings, credential source, coordinator, caches; scoped for anything reading Umbraco content); composer adds `AddRecurringBackgroundJob<SeoReportRefreshJob>()` and the migration handler.
- [ ] **Step 4:** same command → PASS; then `dotnet test <T>` (whole suite) → PASS.
- [ ] **Step 5: Commit** (with Carl's approval): `SeoReport: API controller and registration`.

---

### Task 15: Client foundation

**Files:**
- Create: `<P>/Client/src/{api.ts,types.ts,format.ts,localize.ts,navigate.ts,dashboard.styles.ts,not-connected.element.ts,overview.element.ts}`, `<P>/Client/src/lang/en.ts` (extend), tests `format.test.ts`, `localize.test.ts`, `keys.test.ts`; modify `seo-report-dashboard.element.ts`
- Source of look and behaviour: Nova `seo-dashboard.js` (overview card with ring, KPI tiles, month picker, fetched-at chip, Refresh button for the selected month) and SEO Health's `dashboard.styles.ts`.

**Interfaces:**
- Produces `api.ts`: `BASE = '/umbraco/management/api/v1/esatto/seo-report'` and one function per endpoint of Task 14 using `umbHttpClient` with the `bearer` security pattern from SEO Health's `api.ts`.
- Produces `format.ts`: `formatPeriod(period: string, culture: string): string` (`Intl.DateTimeFormat`, month long + year), `formatNumber`, `formatPercent`, `formatDuration(seconds)`; `localize.ts`: `lookupKey(area, baseId, part): string` (same rule as `ReportKeys.Lookup`) and `text(host, key, args?)` wrapping `localize.term('esattoSeoReport_' + key, ...args)`.
- Produces elements `esatto-seo-report-dashboard` (shell: loads `status`, picks the tab strip, month, holds `report`), `esatto-seo-report-not-connected`, `esatto-seo-report-overview`; tab ids `analysis | traffic | queries | topPages | toFix | comparison | settings`; `navigate.ts` event as in SEO Health.
- Shell behaviour: the header subtitle shows `status.displayName` when set; `status.setup != "ready"` → not-connected card (Settings button only if the user has Settings access, else "ask an administrator"); `fetch.running` → progress ("month {done} of {total}"); `upToDate == false` with no report → progress, never empty charts; `report.dataStatus == "noData"` → a no-data message instead of charts; tab strip `Analysis · Traffic · Queries · Most clicked · To fix · Analytics vs Search Console · Settings`, "To fix" only when the report carries `warning` annotations, Settings only for Settings-section users.

- [ ] **Step 1: Failing tests (vitest).** `format.test.ts`: `formatPeriod('2026-10','en')`→`October 2026`, `formatPeriod('2026-10','sv')`→`oktober 2026`; `formatPercent`, `formatDuration(174)`→`2:54`. `localize.test.ts`: every row of `src/lang/key-vectors.json` equals `lookupKey(area, baseId, part)`. `keys.test.ts`: every string literal passed to `text(...)` under `src/**/*.ts` exists in `en.ts`.
- [ ] **Step 2:** `npm test --prefix <P>/Client` → FAIL.
- [ ] **Step 3: Implement** the files; add English wording for every UI string in these elements (`ui*` keys); copy Nova's visual tokens (`--seo-*`) via SEO Health's styles so light/dark/high-contrast hold.
- [ ] **Step 4:** `npm test --prefix <P>/Client` → PASS; `npm run build --prefix <P>/Client` → OK; `dotnet test <T> --filter "FullyQualifiedName~ElementNameTests"` → PASS.
- [ ] **Step 5: Commit** (with Carl's approval): `SeoReport: client foundation, not-connected state and overview`.

---

### Task 16: Client report tabs (Traffic, Queries, Most clicked, Comparison)

**Files:**
- Create: `<P>/Client/src/tabs/{traffic,queries,top-pages,comparison}.element.ts`, `<P>/Client/src/components/{kpi-tile,chart-card,data-table}.element.ts` (shared), tests `tabs.test.ts`
- Port from: Nova `seo-dashboard.js` sections `trafik`, `sokfragor`, `topplistor`, `jamforelse` and `seo-charts.js` (Chart.js use; keep the same chart library and canvas approach as Nova — check how it is loaded and bundle or import it the same way).

**Interfaces:**
- Consumes: `Report` shape from `types.ts` (Task 15); `text`, `format*`.
- Produces elements `esatto-seo-report-traffic`, `-queries`, `-top-pages`, `-comparison`, and shared `-kpi-tile`, `-chart-card`, `-data-table`. Each chart card renders the canvas **and a visually hidden data table directly after it** (WCAG 1.1.1, as in Nova), series values from `block.labels` / `series.values`; gaps read "no data", not a dash; status is never conveyed by colour alone; tabs are keyboard-navigable.

- [ ] **Step 1: Failing tests (vitest).** Pure helpers: table-row builders for queries/pages cap at the same row limits as Nova's view (read the constants); `chartTableRows(block)` returns the exact label/value grid including `null` → "no data"; KPI trend pill picks up/down from sign and never from colour. Zero-data block renders the no-data message component, not a chart.
- [ ] **Step 2:** `npm test --prefix <P>/Client` → FAIL.
- [ ] **Step 3: Implement** the tabs and shared components; every string via `text(...)`/`lookupKey` with English wording added to `en.ts` (translated from Nova's Swedish).
- [ ] **Step 4:** `npm test --prefix <P>/Client` → PASS; `npm run build` → OK.
- [ ] **Step 5: Commit** (with Carl's approval): `SeoReport: traffic, queries, top pages and comparison tabs`.

---

### Task 17: Client Analysis and To fix tabs (targets, notes)

**Files:**
- Create: `<P>/Client/src/tabs/{analysis,to-fix}.element.ts`, `<P>/Client/src/components/{target-grid,note-editor}.element.ts`, tests `analysis.test.ts`
- Port from: Nova `seo-dashboard.js` sections `analys` and `atgarder`.

**Interfaces:**
- Consumes: `api.getAnalysis`, `getTargets`/`putTargets`, `getNote`/`putNote` (Task 14 shapes).
- Produces `esatto-seo-report-analysis` (narrative statements, movers, opportunities, target-query grid with the status labels `missing|deep|near|title|performing` as keys, month note editor with the 1000-character limit) and `esatto-seo-report-to-fix` (findings list; the shell hides the tab when empty). Status labels come from `TargetAnalysis` status keys through `text(...)`, replacing Nova's JS label map.

- [ ] **Step 1: Failing tests (vitest).** Target grid builder maps each status key to the expected English label key; note editor refuses >1000 chars client-side and sends trimmed text; empty term list hides the target grid; `to-fix` list is sorted as Nova sorts (read the JS); statements render from `TextRef` with `Args`.
- [ ] **Step 2:** `npm test --prefix <P>/Client` → FAIL.
- [ ] **Step 3: Implement**; add English wording.
- [ ] **Step 4:** `npm test --prefix <P>/Client` → PASS; `npm run build` → OK.
- [ ] **Step 5: Commit** (with Carl's approval): `SeoReport: analysis and to-fix tabs`.

---

### Task 18: Client Settings tab

**Files:**
- Create: `<P>/Client/src/tabs/settings.element.ts`, `<P>/Client/src/components/{key-field,setup-guide}.element.ts`, `<P>/Client/src/search-console.ts`, tests `settings.test.ts`

**Interfaces:**
- Consumes: `getSettings`, `putSettings`, `testConnection`, `startFetch`, `getFetchProgress`, `getUserGroups`.
- Produces `esatto-seo-report-settings` with: GA4 property ID field; Search Console **Domain / URL-prefix** choice + text (`composeSearchConsole(kind, text): string` and `parseSearchConsole(site): {kind, text}` in `search-console.ts`, mirroring the server normalizer); write-only key field (paste box and "choose .json file", read in the browser; after save shows "Key saved · service account `<email>`" with Replace/Remove; when `key.source == "configuration"` shows read-only "Set in configuration"; when `key.unreadable` shows the re-enter message); **Test connection** (on the form values; blank key = saved key) with per-source results mapped to `error*` keys and showing the service-account email; brand terms (one per line); display name; user-group multi-select from `getUserGroups`; auto-fetch toggle; **Fetch now** with progress; collapsible **How to connect** guide (six steps from the spec, links to Google docs); locked fields read-only with "set in configuration".

- [ ] **Step 1: Failing tests (vitest).** `composeSearchConsole('domain','example.com')`→`sc-domain:example.com`; `('prefix','https://www.example.com')`→`https://www.example.com/`; `parseSearchConsole` round-trips both. Settings view-model: key field never receives the key back from the response; after a successful save the textarea is cleared; Test connection payload omits `serviceAccountJson` when the field is blank; `Locked` fields are disabled; file upload handler reads text and never logs it. A rendered snapshot contains none of the string `private_key` after a save.
- [ ] **Step 2:** `npm test --prefix <P>/Client` → FAIL.
- [ ] **Step 3: Implement**; English wording for the form, the guide steps and every error code. Verify the guide's Google console wording against Google's current documentation before finalising the text (it changes), and say what you checked in the commit message.
- [ ] **Step 4:** `npm test --prefix <P>/Client` → PASS; `npm run build` → OK; `dotnet test <T>` → PASS.
- [ ] **Step 5: Commit** (with Carl's approval): `SeoReport: Settings tab with setup guide`.

---

### Task 19: Verification on a clean site (GATE B)

**Files:** none in the repo (a throwaway local NuGet feed; findings go into the PR/commit notes).

- [ ] **Step 1:** `dotnet pack <P> -c Release -p:AutoPushToFeed=false -o c:\src\Esatto.Packages\.local-feed`; confirm **no NU5104** and no other new warning.
- [ ] **Step 2:** On `c:\src\umbracokalaset26` (Umbraco 18.2.1) add the package from the local feed, start the site on **SQLite**, then repeat on **SQL Server**. Confirm the tables are created on first start and the dashboard appears in Content.
- [ ] **Step 3 (GATE B — owner: the site owner for the Google steps, me for the browser):** follow only the in-tab guide with that key. Check, in order: not-connected card; wrong GA4 id and wrong Search Console form each give a specific error, never an empty report; Test connection per source; paste key + Save → no restart needed, latest month appears first, progress shows, backfill completes; month switching; Refresh; Fetch now twice quickly (second is refused); key set in configuration shows read-only and wins; remove key → not-connected; a user without Settings access doesn't see the Settings tab and gets 403 from the settings endpoints; a user outside `FindingsUserGroups` has no "To fix"; light, dark and high-contrast; keyboard-only through the tabs; browser console clean. Use the `umbraco-chrome-navigation` skill for the browser steps.
- [ ] **Step 4:** Record results (pass/fail per item) and screenshots (for the README) outside git until Task 20; fix failures by returning to the owning task.

---

### Task 20: Packaging and release prep

**Files:**
- Modify: `<P>/README.md`, `<P>/<P>.csproj` (description/tags final); Create: `<P>/docs/*.png` (screenshots from Task 19); commit built `<P>/wwwroot/App_Plugins/<P>/` (as the siblings do)

- [ ] **Step 1:** Write the README per the spec's *Packaging* section: what it does and doesn't, install, the Google setup steps, where the key can live (tab vs configuration, configuration wins), the Data Protection note including "on Umbraco Cloud, configuration is the documented path until verified there", configuration keys under `Esatto:SeoReport`, limits (16-month Search Console window), relation to SEO Health. Images with absolute `raw.githubusercontent.com` URLs.
- [ ] **Step 2:** `dotnet test` (whole repo suite for this package and `npm test`) → PASS; `dotnet pack <P> -c Release -p:AutoPushToFeed=false` → no NU5104; inspect the `.nupkg`: README, icon, `wwwroot/App_Plugins/<P>/{seo-report-dashboard.js,lang/en.js,umbraco-package.json}`, no `Client/`, no source maps you don't want, correct dependency list (no prerelease).
- [ ] **Step 3:** Ask Carl to approve the commit, then `git tag Esatto.Umbraco.Backoffice.SeoReport-1.0.0` (MinVer) and re-pack to get `1.0.0`. **Stop.** Tell Carl the exact `dotnet nuget push` command; **Carl runs it** (check the API key's expiry first; the glob `Esatto.*` already covers the new ID).
- [ ] **Step 4:** After the push, with Carl's go: force a Marketplace re-scan if the package doesn't appear (`POST https://functions.marketplace.umbraco.com/api/InitiateSinglePackageSyncFunction`, body `{"PackageId":"Esatto.Umbraco.Backoffice.SeoReport"}`).

---

## Out of this plan (own plan, after 1.0.0 is released and verified)

**Nova adoption**: reference the package; remove Nova's `Features/Backoffice/Seo`, its manifest entry and `seo-dashboard.js`; re-point the member-page `SeoSummary` to `ISeoReportService` with Nova's own Swedish for the keys; move the Google key, IDs and brand terms; copy its per-month notes and target queries into the package tables; verify Data Protection on Umbraco Cloud and correct the README in a patch release if needed.
