# Accessibility Package Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** Ship `Esatto.Umbraco.Backoffice.Accessibility` 1.0.0: a Content-section dashboard that scans a site's published pages with axe-core in the editor's browser, scores them against a chosen WCAG version × level, explains every issue (why / how to fix / caveats), keeps a manual checklist, and switches visitor-facing accessibility features (OS-preference following + an optional preferences panel) on and off.

**Architecture:** A Razor class library (AICosts template). C# side: an embedded knowledge base (WCAG criteria + axe rule guidance), NPoco tables behind one store interface, pure services (settings merge, page list, ingestion, scoring, report) and one Management API controller. Client side: a TypeScript/Lit dashboard (Vite → `wwwroot/App_Plugins/Esatto.Umbraco.Backoffice.Accessibility/`) whose scan runner loads pages into same-origin iframes and injects a bundled `axe.min.js`; a second Vite build produces the public visitor scripts/CSS (→ `wwwroot/esatto-accessibility/`), which tag helpers emit only when enabled.

**Tech Stack:** .NET 10, Umbraco 18 (`Umbraco.Cms.Api.Management` 18.0.0, `Umbraco.Cms.Web.Common` 18.0.0), NPoco, xUnit 2.9.2 + NSubstitute 5.3.0, TypeScript 5.9, Lit via `@umbraco-cms/backoffice` ^18, Vite 7, vitest 2 (+ jsdom), axe-core 4.14.x, `@fontsource/atkinson-hyperlegible`, MinVer.

**Spec:** `docs/superpowers/specs/2026-10-21-accessibility-design.md`

**Where:** in place in `c:\src\Esatto.Packages` on the current branch (`main`). No worktrees, and never switch branches in that checkout (umbracokalaset26 builds BlockPreview from it). Everything is new folders, so nothing else is touched.

## Global Constraints

- Folder == csproj == PackageId == RootNamespace == AssemblyName == App_Plugins folder == `umbraco-package.json` `id` == `Esatto.Umbraco.Backoffice.Accessibility`.
- Namespace starts `Esatto.Umbraco.*`: never write an inline `Umbraco.Cms...` qualified name; use `using` + short type name.
- `net10.0`; `Umbraco.Cms.Api.Management` **18.0.0** and `Umbraco.Cms.Web.Common` **18.0.0** as minimums; copy AICosts' "Security overrides" ItemGroup and the ImageSharp `NuGetAuditSuppress` group verbatim. No dependency on any other Esatto package.
- NuGet metadata: tags `umbraco;umbraco-marketplace;umbraco-18;backoffice;accessibility;wcag;a11y;axe`, MIT, `icon.png`, `README.md`, `RepositoryUrl` = `https://github.com/carl-schele-esatto/Esatto.Packages`, `PackageProjectUrl` = `https://github.com/carl-schele-esatto/Esatto.Packages/tree/main/Esatto.Umbraco.Backoffice.Accessibility`, `RepositoryType` git, `Authors` Carl Schéle.
- API base: `/umbraco/management/api/v1/esatto/accessibility` (`[VersionedApiBackOfficeRoute("esatto/accessibility")]`). Read + scan + manual + false-positive endpoints: `AuthorizationPolicies.SectionAccessContent`. `PUT settings`: `AuthorizationPolicies.SectionAccessSettings`.
- Config section **`Esatto:Accessibility`**; any key present there wins over the database and is reported as locked.
- Defaults: target **2.2 · AA**; follow OS motion **on**, follow OS contrast **off**; panel **off**, panel options all on except Reading guide; panel position `bottom-right`; exclude paths empty; mobile width off; best-practice off; re-check after **6** months.
- Scan: iframe width **1280** (and **320** when mobile is on), height 900, pool of **3** frames, settle **1000 ms** after `load`, page timeout **30 s**, one retry. Scan URLs carry `esatto-a11y-scan=1`.
- Retention: findings for the **last 10** scans per site; older scans keep only their summary row.
- Finding HTML snippet truncated to **1024** characters.
- Text: dashboard + knowledge base **English only** (`*.en.json`, culture-keyed file names); visitor panel ships **en + sv** resx with per-culture overrides from settings.
- Public static paths: `/esatto-accessibility/a11y.css`, `/esatto-accessibility/a11y-boot.js`, `/esatto-accessibility/a11y-panel.js`, `/esatto-accessibility/fonts/atkinson-hyperlegible-*.woff2`. axe at `/App_Plugins/Esatto.Umbraco.Backoffice.Accessibility/axe.min.js` with `axe-core.LICENSE.txt` beside it.
- `<html>` attribute contract (README documents it; tests pin it): `data-a11y-text="large"`, `data-a11y-spacing="wide"`, `data-a11y-contrast="high"`, `data-a11y-motion="reduce"`, `data-a11y-font="readable"`, `data-a11y-links="highlight"`, `data-a11y-guide="on"`. `localStorage` key `esatto-a11y`.
- Tests: xUnit `Assert` + NSubstitute, no `InternalsVisibleTo` (test the public surface); vitest for client logic.
- **No commits during the tasks.** Carl commits once after verifying the whole feature (repo + Nova rules). At the end, prep the commit and tag only when Carl explicitly says so; Carl runs `dotnet nuget push`.
- The dashboard UI itself meets WCAG 2.2 AA (labels, keyboard, focus, contrast in light + dark theme); the visitor panel meets 2.2 AAA.

## Review Focus

1. **A configured `ExcludePaths` (or any array) in appsettings must replace the database list, not append to it.** `ConfigurationBinder` appends to existing collections, so a naive bind duplicates entries. Pinned in Task 4 (`Config_array_replaces_database_array`).
2. **A target tag that axe doesn't know** would silently select zero rules and give a clean report. Every tag the mapping can emit must exist in the bundled axe's `getRules()`. Pinned in Task 10 (`every emitted tag exists in axe`).
3. **A site with no automated failures and no manual review must not show 100 %.** Pinned in Task 7 (`Clean_scan_without_manual_review_is_not_full_score`).
4. **A page whose X-Frame-Options/CSP blocks framing**, or a page on another host. Blocked must stop the scan with the fix shown. Cross-host must be skipped with the host named, not counted as a pass. Pinned in Task 10 (`partitionPages` + `blocked frame stops queue`).
5. **A finding whose `<img src>` has a query string or isn't media** (`/media/abc/x.jpg?width=600`, `data:` URIs, external CDN) must resolve to the media item, or to nothing, without throwing. Pinned in Task 6 (`Media_src_with_query_resolves`, `Non_media_src_resolves_to_null`).

---

## File Structure

```
Esatto.Umbraco.Backoffice.Accessibility/
  Esatto.Umbraco.Backoffice.Accessibility.csproj  metadata, deps, npm build target, embedded json/resx
  README.md, icon.png, docs/*.png
  Knowledge/criteria.en.json        every WCAG 2.0/2.1/2.2 SC + our texts
  Knowledge/axe-rules.en.json       per-axe-rule owner/howToFix/caveats overrides
  Resources/PanelText.resx, PanelText.sv.resx
  Views/Shared/Components/AccessibilityPanel/Default.cshtml
  src/
    Knowledge/  WcagVersion.cs WcagLevel.cs WcagTarget.cs Criterion.cs AxeRuleGuide.cs KnowledgeBase.cs AxeTags.cs
    Settings/   AccessibilitySettings.cs SettingsService.cs EffectiveSettings.cs
    Persistence/ *Entity.cs AccessibilityMigrationPlan.cs CreateTablesMigration.cs IAccessibilityStore.cs NPocoAccessibilityStore.cs
    Pages/      SitePageSource.cs PageListBuilder.cs PageCandidate.cs ScanPage.cs SiteInfo.cs
    Scanning/   PageResult.cs ScanService.cs MediaResolver.cs Retention.cs
    Reporting/  CriterionStatus.cs ScoreCalculator.cs ReportService.cs Overview.cs IssueGroup.cs IssueFilter.cs CsvWriter.cs
    Manual/     ManualService.cs FalsePositiveService.cs
    Public/     AccessibilityHeadTagHelper.cs AccessibilityPanelTagHelper.cs AccessibilityPanelViewComponent.cs PanelTextProvider.cs PanelViewModel.cs
    AccessibilityController.cs  AccessibilityComposer.cs  ServiceCollectionExtensions.cs
  Client/
    package.json vite.config.ts tsconfig.json scripts/build-public.mjs scripts/copy-vendor.mjs
    public/umbraco-package.json
    src/api.ts types.ts axe-tags.ts links.ts
    src/scan/partition.ts queue.ts frame-runner.ts reduce.ts
    src/accessibility-dashboard.element.ts dashboard.styles.ts
    src/tabs/overview.element.ts issues.element.ts issue-detail.element.ts checklist.element.ts settings.element.ts
    public-site/prefs.ts a11y-boot.ts a11y-panel.ts a11y.css
    test/*.test.ts
Esatto.Umbraco.Backoffice.Accessibility.Tests/
  *.csproj (copy of Redirects.Tests' csproj with the project reference swapped), one test file per service
```

---

### Task 1: Scaffold the package, test project and client builds

**Files:**
- Create: `Esatto.Umbraco.Backoffice.Accessibility/Esatto.Umbraco.Backoffice.Accessibility.csproj` (from AICosts' csproj: same RCL SDK, `StaticWebAssetBasePath=/`, `Content Remove="Client\**"`, `BuildBackofficeClient` target, AutoPush group, security overrides, audit suppressions; swap metadata per Global Constraints; swap Umbraco.AI refs for `Umbraco.Cms.Api.Management` + `Umbraco.Cms.Web.Common` 18.0.0; add `<EmbeddedResource Include="Knowledge\*.json" />`)
- Create: `Client/package.json` (copy AICosts', name `esatto-umbraco-backoffice-accessibility`; add deps `axe-core` `~4.14.0`, `@fontsource/atkinson-hyperlegible`; devDep `jsdom`; scripts `build`: `tsc && vite build && node scripts/build-public.mjs && node scripts/copy-vendor.mjs`, `test`: `vitest run`)
- Create: `Client/vite.config.ts` (entry `accessibility-dashboard` → `src/accessibility-dashboard.element.ts`, outDir `../wwwroot/App_Plugins/Esatto.Umbraco.Backoffice.Accessibility`, externals `/^@umbraco/`)
- Create: `Client/scripts/build-public.mjs`: calls Vite's `build()` API once **per entry** (`a11y-boot` → `public-site/a11y-boot.ts`, `a11y-panel` → `public-site/a11y-panel.ts`) with `lib.formats: ['iife']`, so each file is self-contained (a multi-entry build would split the shared `prefs.ts` into an imported chunk, and `a11y-boot.js` must run as a classic synchronous script). It writes to `../wwwroot/esatto-accessibility` (cleaned first) and copies `public-site/a11y.css` there
- Create: `Client/scripts/copy-vendor.mjs`: copies `node_modules/axe-core/axe.min.js` + `LICENSE` (as `axe-core.LICENSE.txt`) into the App_Plugins folder, and the Atkinson Hyperlegible latin 400/700 woff2 files + `LICENSE` into `wwwroot/esatto-accessibility/fonts/`
- Create: `Client/tsconfig.json` (copy AICosts'; include `src`, `public-site`)
- Create: `Client/public/umbraco-package.json`
- Create: `Client/src/accessibility-dashboard.element.ts` (placeholder element rendering an `<h1>` "Accessibility")
- Create: `Esatto.Umbraco.Backoffice.Accessibility.Tests/Esatto.Umbraco.Backoffice.Accessibility.Tests.csproj`, `ScaffoldTests.cs`
- Create: `README.md` stub, `icon.png` (accessibility glyph, 128×128, same style as siblings)

**Interfaces:**
- Produces: dashboard alias `Esatto.Umbraco.Backoffice.Accessibility.Dashboard`, element name `esatto-accessibility-dashboard`.

- [ ] **Step 1: Write the manifest**

```json
{
  "$schema": "../umbraco-package-schema.json",
  "id": "Esatto.Umbraco.Backoffice.Accessibility",
  "name": "Esatto Umbraco Backoffice Accessibility",
  "version": "1.0.0",
  "allowTelemetry": false,
  "extensions": [{
    "type": "dashboard",
    "alias": "Esatto.Umbraco.Backoffice.Accessibility.Dashboard",
    "name": "Accessibility",
    "element": "/App_Plugins/Esatto.Umbraco.Backoffice.Accessibility/accessibility-dashboard.js",
    "elementName": "esatto-accessibility-dashboard",
    "weight": 50,
    "meta": { "label": "Accessibility", "pathname": "accessibility" },
    "conditions": [{ "alias": "Umb.Condition.SectionAlias", "match": "Umb.Section.Content" }]
  }]
}
```

- [ ] **Step 2: Write `ScaffoldTests.Package_assembly_name_matches_package_id`** asserting `typeof(AccessibilityComposer).Assembly.GetName().Name == "Esatto.Umbraco.Backoffice.Accessibility"`, plus an empty `AccessibilityComposer : IComposer`.

- [ ] **Step 3: Build and test**

Run: `dotnet build Esatto.Umbraco.Backoffice.Accessibility.Tests && dotnet test Esatto.Umbraco.Backoffice.Accessibility.Tests`
Expected: build runs npm install, the dashboard Vite build, build-public and copy-vendor; test PASS; `wwwroot/App_Plugins/Esatto.Umbraco.Backoffice.Accessibility/` contains `accessibility-dashboard.js`, `umbraco-package.json`, `axe.min.js`, `axe-core.LICENSE.txt`; `wwwroot/esatto-accessibility/fonts/` contains the woff2 files.

- [ ] **Step 4: Smoke in umbracokalaset26.** Add a `ProjectReference` to the package in `c:\src\umbracokalaset26\UmbracoKalaset26.csproj` (local only; Carl decides whether it stays), run the site, log in. Expected: Content section shows an "Accessibility" dashboard tab rendering the placeholder heading.

---

### Task 2: Knowledge base model, loader and target → criteria

**Files:**
- Create: `src/Knowledge/WcagVersion.cs`, `WcagLevel.cs`, `WcagTarget.cs`, `Criterion.cs`, `AxeRuleGuide.cs`, `KnowledgeBase.cs`, `AxeTags.cs`
- Create: `Knowledge/criteria.en.json` (metadata only in this task; texts are empty strings until Task 3), `Knowledge/axe-rules.en.json` (`{}`)
- Test: `Esatto.Umbraco.Backoffice.Accessibility.Tests/KnowledgeBaseTests.cs`, `AxeTagsTests.cs`

**Interfaces:**
- Produces:
  - `enum WcagVersion { V20, V21, V22 }`, `enum WcagLevel { A, AA, AAA }`, `enum Owner { Editor, Developer, Both }`
  - `sealed record WcagTarget(WcagVersion Version, WcagLevel Level)` with `bool Includes(Criterion c)` (since ≤ Version, level ≤ Level, not obsolete in Version) and `static WcagTarget Default => new(V22, AA)`
  - `sealed record Criterion(string Id, string Title, WcagLevel Level, WcagVersion Since, WcagVersion? ObsoleteIn, int Principle, string Guideline, bool FullyAutomatable, Owner Owner, string Why, string HowToFix, string Caveats, string HowToTest, string UnderstandingUrl)`
  - `sealed record AxeRuleGuide(string RuleId, Owner? Owner, string? HowToFix, string? Caveats)`
  - `sealed class KnowledgeBase` (singleton): `IReadOnlyList<Criterion> All`, `Criterion? Find(string id)`, `IReadOnlyList<Criterion> For(WcagTarget t)`, `AxeRuleGuide? Rule(string ruleId)`; loads the embedded `*.en.json` once (`System.Text.Json`, camelCase, enums as strings).
  - `static class AxeTags { static IReadOnlyList<string> CriteriaFromTags(IEnumerable<string> tags) }`: maps `wcag\d\d\d{1,2}` tags to `"p.g.n"` ids (`wcag1410` → `1.4.10`); ignores level tags (`wcag2aa`, `wcag21a`…), `best-practice`, `ACT`, `EN-*` etc.

- [ ] **Step 1: Write failing tests**

```csharp
[Theory]
[InlineData(WcagVersion.V20, WcagLevel.A, 25)]
[InlineData(WcagVersion.V20, WcagLevel.AA, 38)]
[InlineData(WcagVersion.V20, WcagLevel.AAA, 61)]
[InlineData(WcagVersion.V21, WcagLevel.A, 30)]
[InlineData(WcagVersion.V21, WcagLevel.AA, 50)]
[InlineData(WcagVersion.V21, WcagLevel.AAA, 78)]
[InlineData(WcagVersion.V22, WcagLevel.A, 31)]   // 32 incl. 4.1.1, obsolete in 2.2
[InlineData(WcagVersion.V22, WcagLevel.AA, 55)]
[InlineData(WcagVersion.V22, WcagLevel.AAA, 86)]
public void Target_selects_the_right_number_of_criteria(WcagVersion v, WcagLevel l, int count)
    => Assert.Equal(count, Kb.For(new WcagTarget(v, l)).Count);

[Fact] public void Parsing_4_1_1_is_in_2_1_but_not_2_2() { /* Find("4.1.1") in For(V21,A), not in For(V22,A) */ }
[Fact] public void Target_size_minimum_is_2_2_AA() { /* Find("2.5.8"): Level AA, Since V22 */ }
[Fact] public void Every_criterion_has_an_understanding_url_on_w3_org() { /* starts with https://www.w3.org/WAI/WCAG22/Understanding/ */ }
[Theory]
[InlineData(new[] { "wcag2a", "wcag111", "cat.text-alternatives" }, new[] { "1.1.1" })]
[InlineData(new[] { "wcag21aa", "wcag1410" }, new[] { "1.4.10" })]
[InlineData(new[] { "wcag22aa", "wcag258" }, new[] { "2.5.8" })]
[InlineData(new[] { "best-practice" }, new string[0])]
public void CriteriaFromTags_maps_axe_tags(string[] tags, string[] expected) => Assert.Equal(expected, AxeTags.CriteriaFromTags(tags));
```

- [ ] **Step 2: Run** `dotnet test --filter "KnowledgeBaseTests|AxeTagsTests"`. Expected: FAIL (types missing).

- [ ] **Step 3: Generate `criteria.en.json` metadata** from W3C's machine-readable `https://www.w3.org/WAI/WCAG22/wcag.json` (fetch once; ids, titles, levels, versions, understanding slugs). Set `obsoleteIn: "V22"` on 4.1.1. Set `fullyAutomatable: true` **only** on `3.1.1`; everything else `false`. Set `owner` per criterion (Editor for text alternatives, headings/labels content, link purpose, language of parts, captions/audio description, page titled; Developer for structure/keyboard/focus/contrast/reflow/timing/target size/name-role-value; Both where either can cause it, e.g. 1.3.1, 2.4.4, 1.4.5). Texts are `""` for now. Implement the types and loader.

- [ ] **Step 4: Run tests.** Expected: PASS.

---

### Task 3: Write the guidance (knowledge base texts)

**Files:**
- Modify: `Knowledge/criteria.en.json` (fill `why`, `howToFix`, `caveats`, `howToTest` for all 87 entries)
- Modify: `Knowledge/axe-rules.en.json` (overrides for at least: `image-alt`, `role-img-alt`, `input-image-alt`, `link-name`, `button-name`, `color-contrast`, `color-contrast-enhanced`, `heading-order`, `empty-heading`, `page-has-heading-one`, `html-has-lang`, `html-lang-valid`, `valid-lang`, `document-title`, `label`, `select-name`, `frame-title`, `list`, `listitem`, `region`, `landmark-one-main`, `target-size`, `video-caption`, `meta-viewport`, `aria-*` group via `aria-valid-attr`, `aria-required-attr`, `duplicate-id-aria`, `link-in-text-block`, `scrollable-region-focusable`, `nested-interactive`)
- Test: `KnowledgeBaseTests.cs` (add completeness tests)

**Interfaces:**
- Consumes: `KnowledgeBase`, `Criterion`, `AxeRuleGuide` (Task 2).

Writing rules (from the spec): our own words, not copied from W3C; plain language for editors; **How to fix** is Umbraco-specific where it applies (media item "Alternative text", Rich Text Editor heading dropdown, the page's Name/SEO title, the template/CSS for developer items); **Caveats** names the common wrong fix (e.g. alt on decorative images, "Image of…", `aria-label` duplicating visible text, `outline: none`); **How to test** is concrete steps a non-expert can follow in a browser, with keyboard-only and screen-reader (NVDA/VoiceOver) steps where relevant. 2–5 sentences per field.

- [ ] **Step 1: Write failing tests**

```csharp
[Fact] public void Every_criterion_has_all_four_texts()
    => Assert.Empty(Kb.All.Where(c => new[] { c.Why, c.HowToFix, c.Caveats, c.HowToTest }.Any(string.IsNullOrWhiteSpace)).Select(c => c.Id));
[Fact] public void Image_alt_is_owned_by_editors_and_mentions_the_media_item()
{ var r = Kb.Rule("image-alt")!; Assert.Equal(Owner.Editor, r.Owner); Assert.Contains("media", r.HowToFix, StringComparison.OrdinalIgnoreCase); }
[Fact] public void Color_contrast_is_owned_by_developers() => Assert.Equal(Owner.Developer, Kb.Rule("color-contrast")!.Owner);
```

- [ ] **Step 2: Run.** Expected: FAIL (empty texts).
- [ ] **Step 3: Write the texts** (principle by principle; 1 → 4).
- [ ] **Step 4: Run.** Expected: PASS.

---

### Task 4: Settings (database + configuration lock)

**Files:**
- Create: `src/Settings/AccessibilitySettings.cs`, `EffectiveSettings.cs`, `SettingsService.cs`
- Test: `SettingsServiceTests.cs`

**Interfaces:**
- Consumes: `WcagVersion`, `WcagLevel` (Task 2); `IAccessibilityStore.GetSettingsJsonAsync()` / `SaveSettingsJsonAsync(string json)` (Task 5; tests substitute it).
- Produces:
  - Mutable classes (the config binder needs setters): `AccessibilitySettings { WcagVersion Version; WcagLevel Level; ScanSettings Scan; VisitorSettings Visitor }`, `ScanSettings { string[] ExcludePaths; bool MobileWidth; bool BestPractice; int RecheckMonths }`, `VisitorSettings { bool FollowOsMotion; bool FollowOsContrast; PanelSettings Panel }`, `PanelSettings { bool Enabled; string Position; bool LargerText; bool Spacing; bool HighContrast; bool ReduceMotion; bool ReadableFont; bool HighlightLinks; bool ReadingGuide; Dictionary<string, Dictionary<string,string>> Texts }` with defaults from Global Constraints. `const string SectionName = "Esatto:Accessibility"`.
  - `sealed record EffectiveSettings(AccessibilitySettings Settings, IReadOnlyList<string> Locked, WcagTarget Target)`; `Locked` = camelCase dotted paths, e.g. `visitor.panel.enabled`, `scan.excludePaths`.
  - `sealed class SettingsService(IAccessibilityStore store, IConfiguration config)`: `Task<EffectiveSettings> GetAsync()` (60 s in-memory cache), `Task<EffectiveSettings> SaveAsync(AccessibilitySettings incoming)` (stores incoming, invalidates cache, returns effective; locked values in `incoming` are ignored because config is re-applied).

- [ ] **Step 1: Write failing tests**

```csharp
[Fact] public async Task Defaults_when_nothing_stored_or_configured()  // Version V22, Level AA, FollowOsMotion true, FollowOsContrast false, Panel.Enabled false, ReadingGuide false, RecheckMonths 6, Locked empty
[Fact] public async Task Config_value_wins_and_is_locked()             // config "Esatto:Accessibility:Visitor:Panel:Enabled"="true", db false → Enabled true, Locked contains "visitor.panel.enabled"
[Fact] public async Task Save_cannot_override_locked_value()           // save Enabled=false with config true → effective true
[Fact] public async Task Config_array_replaces_database_array()        // db ExcludePaths ["/a"], config ExcludePaths:0="/b" → ["/b"], Locked contains "scan.excludePaths"
[Fact] public async Task Unknown_or_corrupt_json_falls_back_to_defaults() // store returns "{not json" → defaults, no throw
```

Config in tests: `new ConfigurationBuilder().AddInMemoryCollection(...)`.

- [ ] **Step 2: Run.** Expected: FAIL.
- [ ] **Step 3: Implement.** Merge = deserialize DB JSON (or defaults), then for each config leaf key under the section: if the key is an array element (`…:N`), clear that array once before binding; then `section.Bind(settings)`. Locked = distinct config leaf paths with array indices stripped, camelCased.
- [ ] **Step 4: Run.** Expected: PASS.

---

### Task 5: Persistence (tables, migration, store)

**Files:**
- Create: `src/Persistence/SettingsEntity.cs`, `ScanEntity.cs`, `ScanPageEntity.cs`, `FindingEntity.cs`, `FalsePositiveEntity.cs`, `ManualResultEntity.cs`, `AccessibilityMigrationPlan.cs`, `CreateTablesMigration.cs`, `IAccessibilityStore.cs`, `NPocoAccessibilityStore.cs`
- Modify: `src/AccessibilityComposer.cs` (migration handler, Redirects' `RunRedirectsMigration` pattern; `services.AddAccessibility()`), create `src/ServiceCollectionExtensions.cs`

**Interfaces:**
- Produces tables (prefix `esattoA11y`): `esattoA11ySettings(id, json nvarchar(max), updatedUtc)`; `esattoA11yScan(id, siteKey uniqueidentifier, startedUtc, finishedUtc null, version, level, mobile bit, userName, status nvarchar(16) [running|complete|incomplete], pagesTotal, pagesDone, pagesSkipped, pagesFailed, score decimal null, passCount, failCount, unverifiedCount, naCount, documentLinks nvarchar(max) null /* distinct newline list */)`; `esattoA11yScanPage(id, scanId, contentKey, culture, url, viewport int, status nvarchar(16) [ok|skipped|failed], reason null, passedRules nvarchar(max) /* comma list of axe rule ids that passed */)`; `esattoA11yFinding(id, scanId, scanPageId, ruleId, help nvarchar(256), helpUrl nvarchar(512), criteria nvarchar(256) comma list, impact null, kind nvarchar(16) [violation|incomplete], selector nvarchar(1024), html nvarchar(1024), mediaKey uniqueidentifier null)`; `esattoA11yFalsePositive(id, siteKey, ruleId, contentKey, selector null = whole page, note, userName, createdUtc)`; `esattoA11yManualResult(id, siteKey, criterionId, status nvarchar(16) [pass|fail|na], note null, userName, updatedUtc)` unique (siteKey, criterionId). Indexes on `scanId` and `(siteKey, startedUtc)`.
- `public interface IAccessibilityStore` with async methods: `GetSettingsJsonAsync`, `SaveSettingsJsonAsync`, `CreateScanAsync(ScanEntity)`→id, `GetScanAsync(id)`, `GetScansAsync(siteKey, take)`, `UpdateScanAsync(ScanEntity)`, `AddPageAsync(ScanPageEntity, IReadOnlyList<FindingEntity>)` (transaction), `ReplacePageAsync(scanId, contentKey, culture, viewport, ScanPageEntity, findings)`, `GetPagesAsync(scanId)`, `GetFindingsAsync(scanId)`, `DeleteFindingsForScansAsync(IEnumerable<int> scanIds)`, `GetFalsePositivesAsync(siteKey)`, `AddFalsePositiveAsync`, `DeleteFalsePositiveAsync(id)`, `GetManualResultsAsync(siteKey)`, `UpsertManualResultAsync`. Entities are public (DI/NPoco), `[ExplicitColumns]`, `[TableName]`, Redirects style.

- [ ] **Step 1:** Implement entities, migration plan `"Esatto.Umbraco.Backoffice.Accessibility"` → `CreateTablesMigration` ("create-tables", each table `if (!TableExists) Create.Table<T>().Do()`), store, composer wiring.
- [ ] **Step 2: Verify on umbracokalaset26:** run the site. Expected: the six `esattoA11y*` tables exist (check with the site's SQLite/SQL), `umbracoKeyValue` has `Umbraco.Core.Upgrader.State+Esatto.Umbraco.Backoffice.Accessibility` = `create-tables`; second start logs no migration work.

---

### Task 6: Page list and scan ingestion

**Files:**
- Create: `src/Pages/SiteInfo.cs`, `PageCandidate.cs`, `ScanPage.cs`, `PageListBuilder.cs`, `SitePageSource.cs`
- Create: `src/Scanning/PageResult.cs`, `MediaResolver.cs`, `Retention.cs`, `ScanService.cs`
- Test: `PageListBuilderTests.cs`, `MediaResolverTests.cs`, `RetentionTests.cs`, `ScanServiceTests.cs`

**Interfaces:**
- Consumes: `IAccessibilityStore` (Task 5), `SettingsService` (Task 4), `AxeTags` (Task 2).
- Produces:
  - `sealed record SiteInfo(Guid Key, string Name, IReadOnlyList<string> Hosts)`: one per root content node; `Hosts` from `IDomainService.GetAllAsync(false)` for that root.
  - `sealed record PageCandidate(Guid ContentKey, string Name, string Culture, string? AbsoluteUrl, bool Protected, bool HasTemplate)`
  - `sealed record ScanPage(Guid ContentKey, string Name, string Culture, string Url, string? SkipReason)`; SkipReason values: `"protected"`, `"excluded"`, `"no-url"`.
  - `static class PageListBuilder { IReadOnlyList<ScanPage> Build(IEnumerable<PageCandidate> candidates, IReadOnlyList<string> excludePaths) }`: drops `!HasTemplate`; excluded = URL path starts with any prefix (case-insensitive, normalized leading `/`, no trailing `/`).
  - `sealed class SitePageSource`: `Task<IReadOnlyList<SiteInfo>> GetSitesAsync()`, `Task<IReadOnlyList<PageCandidate>> GetCandidatesAsync(Guid siteKey)` using `IDocumentNavigationQueryService.TryGetRootKeys/TryGetDescendantsKeys`, `IPublishedContentCache.GetById(key)`, `IPublishedUrlProvider.GetUrl(content, UrlMode.Absolute, culture)` for each culture in `content.Cultures` (or `""` when invariant), `IPublicAccessService.IsProtected(content.Path)`. Not unit-tested (thin Umbraco adapter); verified in Task 15.
  - `sealed record PageResult(Guid ContentKey, string Culture, string Url, int Viewport, string Status, string? Reason, IReadOnlyList<string> PassedRules, IReadOnlyList<RuleResult> Rules, IReadOnlyList<string> DocumentLinks)`; `sealed record RuleResult(string RuleId, string Kind, string? Impact, string Help, string HelpUrl, IReadOnlyList<string> Tags, IReadOnlyList<NodeResult> Nodes)`; `sealed record NodeResult(string Selector, string Html)`.
  - `sealed class MediaResolver(IMediaService media)`: `Guid? Resolve(string html)`: finds the first `<img … src="…">` in the snippet, strips query/fragment, only `/media/` paths; `GetMediaByPath(path)?.Key`; per-instance cache.
  - `static class Retention { IReadOnlyList<int> ScansToPrune(IEnumerable<(int Id, DateTime Started)> scansOfSite, int keep = 10) }`
  - `sealed class ScanService`: `Task<int> StartAsync(Guid siteKey, bool mobile, string userName)`; `Task AddPageAsync(int scanId, PageResult r)` (maps rules → `FindingEntity` per node with `criteria = string.Join(",", AxeTags.CriteriaFromTags(tags))`, html truncated to 1024, mediaKey via `MediaResolver` for `image-alt`/`role-img-alt`/`input-image-alt`; increments counters, merges `DocumentLinks` into the scan's distinct list); `Task FinishAsync(int scanId, bool complete)` (status, snapshot score via `ReportService.ComputeScoreAsync` from Task 7, prune via `Retention`); `Task<IReadOnlyList<(Guid ContentKey, string Culture, int Viewport)>> DoneAsync(int scanId)` (for resume); `Task RecheckAsync(Guid siteKey, PageResult r)` (replaces that page in the latest complete scan, recomputes snapshot).

- [ ] **Step 1: Write failing tests**

```csharp
// PageListBuilderTests
[Fact] public void Pages_without_template_are_dropped()
[Fact] public void Excluded_prefix_matches_case_insensitively_and_not_partial_segments() // "/sok" excludes "/Sok/x" but not "/sokresultat"
[Fact] public void Protected_pages_are_kept_with_skip_reason_protected()
// MediaResolverTests
[Fact] public void Media_src_with_query_resolves()          // <img src="/media/abc/x.jpg?width=600"> → GetMediaByPath("/media/abc/x.jpg")
[Fact] public void Non_media_src_resolves_to_null()          // data: URI, https://cdn.example/x.jpg, no img → null, GetMediaByPath never called
// RetentionTests
[Fact] public void Keeps_newest_ten()                       // 12 scans → the two oldest ids
// ScanServiceTests (store substituted)
[Fact] public async Task Finding_html_is_truncated_to_1024()
[Fact] public async Task Finding_criteria_come_from_wcag_tags()     // tags [wcag2a, wcag111] → "1.1.1"
[Fact] public async Task Finish_prunes_findings_of_scans_beyond_ten()
```

- [ ] **Step 2: Run.** Expected: FAIL.
- [ ] **Step 3: Implement.**
- [ ] **Step 4: Run.** Expected: PASS.

---

### Task 7: Scoring, report, issues and CSV

**Files:**
- Create: `src/Reporting/CriterionStatus.cs`, `ScoreCalculator.cs`, `Overview.cs`, `IssueGroup.cs`, `IssueFilter.cs`, `ReportService.cs`, `CsvWriter.cs`
- Test: `ScoreCalculatorTests.cs`, `ReportServiceTests.cs`, `CsvWriterTests.cs`

**Interfaces:**
- Consumes: `KnowledgeBase`, `WcagTarget`, `IAccessibilityStore`, `SettingsService`.
- Produces:
  - `enum CriterionStatus { Pass, Fail, NotApplicable, NotVerified }`
  - `sealed record CriterionInput(Criterion Criterion, int OpenViolations, bool AutomatedPassSeen, string? Manual /* pass|fail|na|null */)`
  - `static class ScoreCalculator { CriterionStatus StatusOf(CriterionInput i); ScoreResult Compute(IEnumerable<CriterionInput> inputs) }`, `sealed record ScoreResult(decimal? Percent, int Pass, int Fail, int NotApplicable, int NotVerified, int AutomatedPass, int AutomatedFail)`. Rules exactly as the spec's *Score* section; `Percent = null` when the denominator is 0; rounded to whole percent.
  - `sealed record PrincipleTile(int Principle, string Name, int Fail, int ToCheck)`
  - `sealed record Overview(WcagTarget Target, ScoreResult Score, IReadOnlyList<PrincipleTile> Principles, ScanSummary? LastScan, IReadOnlyList<ScanSummary> History, int ManualChecked, int ManualTotal)`; `sealed record ScanSummary(int Id, DateTime StartedUtc, DateTime? FinishedUtc, string Status, string Target, decimal? Score, int PagesTotal, int PagesDone, int PagesSkipped, int PagesFailed, string UserName)`
  - `sealed record IssueFilter(int? Principle, WcagLevel? Level, Owner? Owner, string? Impact, bool FalsePositives)`
  - `sealed record IssueGroup(string RuleId, string Title, IReadOnlyList<string> Criteria, WcagLevel Level, Owner Owner, string Impact, string Kind, int PageCount, int ElementCount, string Why, string HowToFix, string Caveats, string UnderstandingUrl, IReadOnlyList<IssuePage> Pages)`; `sealed record IssuePage(Guid ContentKey, string Name, string Culture, string Url, IReadOnlyList<IssueNode> Nodes)`; `sealed record IssueNode(int FindingId, string Selector, string Html, Guid? MediaKey, int? FalsePositiveId)`
  - **One `IssueGroup` per axe rule** (listing all of its criteria; the spec's "rule × criterion" would show the same problem twice). Title = the finding's `help`. Owner = rule override ?? owner of the rule's first criterion ?? `Developer`. Texts = rule override field ?? first criterion's field; for a best-practice rule with neither, Why = `"Not a WCAG requirement; a recommended practice."`, HowToFix = `help`, Caveats = `""`, and the link is the finding's `helpUrl` instead of an Understanding URL. A missing text never throws. Sort: impact (critical > serious > moderate > minor), then PageCount desc.
  - `sealed class ReportService`: `Task<Overview> GetOverviewAsync(Guid siteKey)`, `Task<IReadOnlyList<IssueGroup>> GetIssuesAsync(Guid siteKey, IssueFilter f)`, `Task<ScoreResult> ComputeScoreAsync(int scanId)`. A violation is *open* unless a false positive matches (same site, rule, content key, and selector equal or false-positive selector null). Only findings of the target's criteria count; best-practice findings (no criteria) appear in Issues under owner/principle filters as "Best practice" and never in the score.
  - `static class CsvWriter { string Write(IEnumerable<IssueGroup> groups) }`: one row per node: `Rule,Criteria,Level,Owner,Impact,Page,Url,Selector,Html`; RFC 4180 quoting, UTF-8 BOM (Excel).

- [ ] **Step 1: Write failing tests**

```csharp
[Fact] public void Open_violation_fails()                       // OpenViolations 1, Manual "pass" → Fail
[Fact] public void Manual_fail_fails_without_violations()
[Fact] public void Manual_pass_passes()
[Fact] public void Fully_automatable_passes_on_automated_pass()  // 3.1.1, AutomatedPassSeen true → Pass
[Fact] public void Partly_automatable_with_automated_pass_is_not_verified() // 1.1.1, AutomatedPassSeen true, no manual → NotVerified
[Fact] public void Manual_na_is_excluded_from_denominator()      // 1 pass, 1 na, 1 notVerified → 50
[Fact] public void Clean_scan_without_manual_review_is_not_full_score() // all 55 of 2.2 AA AutomatedPassSeen, no manual → Percent < 100 (= 1/55 → 2)
[Fact] public async Task False_positive_for_whole_page_closes_all_nodes_of_rule_on_that_page()
[Fact] public async Task Issues_group_by_rule_with_all_criteria()  // link-name with [wcag244, wcag412] → one group, Criteria ["2.4.4","4.1.2"]
[Fact] public async Task Findings_outside_target_are_ignored()     // 2.4.13 (AAA) finding with target 2.2 AA → not in score, not in issues
[Fact] public void Csv_quotes_commas_quotes_and_newlines()         // html `<a title="a, b">\n` → one quoted field with doubled quotes
```

- [ ] **Step 2: Run.** Expected: FAIL.
- [ ] **Step 3: Implement.**
- [ ] **Step 4: Run.** Expected: PASS.

---

### Task 8: Manual results and false positives

**Files:**
- Create: `src/Manual/ManualService.cs`, `src/Manual/FalsePositiveService.cs`
- Test: `ManualServiceTests.cs`, `FalsePositiveServiceTests.cs`

**Interfaces:**
- Consumes: `IAccessibilityStore`, `KnowledgeBase`, `SettingsService`, `TimeProvider` (register `TimeProvider.System` with `TryAddSingleton`).
- Produces:
  - `sealed record ChecklistItem(Criterion Criterion, string? Status, string? Note, string? UserName, DateTime? UpdatedUtc, bool NeedsRecheck, int AutomatedFindings, int DocumentLinks)`
  - `ManualService`: `Task<IReadOnlyList<ChecklistItem>> GetChecklistAsync(Guid siteKey)` (criteria of the current target that are **not** fully automatable; `NeedsRecheck` when `UpdatedUtc` older than `RecheckMonths`; `AutomatedFindings` = incomplete+violation count of the latest scan for that criterion; `DocumentLinks` = count from the latest scan's document links, shown on 1.1.1 and 1.3.1 only), `Task SetAsync(Guid siteKey, string criterionId, string status, string? note, string userName)` (status must be `pass|fail|na`, criterion must exist → otherwise `ArgumentException`).
  - Document links come from `esattoA11yScan.documentLinks` of the latest scan (collected by the client: absolute hrefs whose path ends `.pdf|.doc|.docx|.xlsx|.pptx`).
  - `FalsePositiveService`: `Task<int> AddAsync(Guid siteKey, string ruleId, Guid contentKey, string? selector, string note, string userName)` (note required → `ArgumentException`), `Task RemoveAsync(int id)`.

- [ ] **Step 1: Write failing tests**

```csharp
[Fact] public async Task Checklist_excludes_fully_automatable_criteria()      // 3.1.1 absent
[Fact] public async Task Result_older_than_recheck_interval_needs_recheck()   // updated 7 months ago, RecheckMonths 6 → NeedsRecheck
[Fact] public async Task Changing_target_keeps_results_and_new_criteria_start_unchecked() // result for 1.1.1 kept; 2.5.8 Status null
[Fact] public async Task Invalid_status_is_rejected()
[Fact] public async Task False_positive_requires_a_note()
```

- [ ] **Step 2: Run.** Expected: FAIL. **Step 3: Implement.** **Step 4: Run.** Expected: PASS.

---

### Task 9: Management API

**Files:**
- Create: `src/AccessibilityController.cs`
- Modify: `src/ServiceCollectionExtensions.cs` (register everything: `KnowledgeBase` singleton, `SettingsService` singleton, others scoped)
- Test: `AccessibilityControllerTests.cs`

**Interfaces:**
- Consumes: all services above; `IBackOfficeSecurityAccessor` for `CurrentUser.Name`; `IBackOfficeSecurity.UserHasSectionAccess("settings", user)` for `canEdit`.
- Produces endpoints (class-level `[Authorize(Policy = AuthorizationPolicies.SectionAccessContent)]`, `[ApiExplorerSettings(GroupName = "Esatto Accessibility")]`):

| Verb + path | Body / query | Returns |
|---|---|---|
| `GET knowledge` | | `{ criteria: Criterion[], rules: AxeRuleGuide[] }` |
| `GET sites` | | `SiteInfo[]` |
| `GET pages?site=` | | `ScanPage[]` |
| `GET settings` | | `{ settings, locked, target, canEdit }` |
| `PUT settings` *(SectionAccessSettings)* | `AccessibilitySettings` | same as GET |
| `POST scans` | `{ siteKey, mobile }` | `{ scanId }` |
| `GET scans/{id}/done` | | `{ contentKey, culture, viewport }[]` |
| `POST scans/{id}/pages` | `PageResult` | 204 |
| `POST scans/{id}/finish` | `{ complete }` | `ScanSummary` |
| `POST recheck?site=` | `PageResult` | 204 |
| `GET overview?site=` | | `Overview` |
| `GET issues?site=&principle=&level=&owner=&impact=&falsePositives=` | | `IssueGroup[]` |
| `GET issues/csv?…same…` | | `text/csv` file `accessibility-issues-<yyyy-MM-dd>.csv` |
| `GET checklist?site=` | | `ChecklistItem[]` |
| `PUT checklist/{criterionId}?site=` | `{ status, note }` | 204 / 400 |
| `POST false-positives?site=` | `{ ruleId, contentKey, selector, note }` | `{ id }` / 400 |
| `DELETE false-positives/{id}` | | 204 |

- [ ] **Step 1: Write failing tests** (reflection over the controller):

```csharp
[Fact] public void Controller_requires_content_section() // class AuthorizeAttribute.Policy == AuthorizationPolicies.SectionAccessContent
[Fact] public void Put_settings_requires_settings_section() // PutSettings method AuthorizeAttribute.Policy == AuthorizationPolicies.SectionAccessSettings
[Fact] public void Route_is_esatto_accessibility()
```

- [ ] **Step 2: Run.** Expected: FAIL. **Step 3: Implement.** `ArgumentException` → `BadRequest(message)`; unknown scan id → `NotFound()`. **Step 4: Run.** Expected: PASS.
- [ ] **Step 5: Verify in Swagger** on umbracokalaset26 (`/umbraco/swagger`, group "Esatto Accessibility"): every endpoint listed; `GET sites` and `GET pages?site=<root>` return the test site's pages.

---

### Task 10: Client API, axe tags and the scan runner

**Files:**
- Create: `Client/src/types.ts` (camelCase mirrors of every C# record in Task 9's table), `api.ts` (AICosts' `umbHttpClient` + bearer `security()` pattern, one function per endpoint), `axe-tags.ts`, `links.ts`, `scan/partition.ts`, `scan/queue.ts`, `scan/reduce.ts`, `scan/frame-runner.ts`
- Test: `Client/test/axe-tags.test.ts`, `partition.test.ts`, `queue.test.ts`, `reduce.test.ts`, `links.test.ts`

**Interfaces:**
- Produces:
  - `axeTags(version: 'V20'|'V21'|'V22', level: 'A'|'AA'|'AAA', bestPractice: boolean): string[]` from the ordered list `wcag2a, wcag2aa, wcag2aaa, wcag21a, wcag21aa, wcag22aa` (+ `best-practice`); include a tag when its version ≤ target version and its level ≤ target level. If the installed axe has `wcag21aaa`/`wcag22a`/`wcag22aaa`, add them to the list (checked by the test below).
  - `partitionPages(pages: ScanPage[], origin: string): { scannable: ScanPage[]; skipped: (ScanPage & { skipReason: string })[] }`: existing `skipReason` kept; different origin → `other-host:<host>`.
  - `editContentUrl(key: string, culture: string): string` and `editMediaUrl(key: string): string`. Exact paths are verified in the running backoffice in Task 15 (start from `/umbraco/section/content/workspace/document/edit/<key>/<culture>` and `/umbraco/section/media/workspace/media/edit/<key>`).
  - `class ScanQueue` (pure, no DOM): `constructor(items: Job[], opts: { concurrency: number; retries: number }, run: (job: Job) => Promise<JobOutcome>)`, `start(): Promise<void>`, `cancel(): void`, `onProgress(cb: (p: { done: number; total: number; current: string[] }) => void)`. `JobOutcome = { kind: 'ok' | 'failed' | 'blocked'; result?: PageResult; reason?: string }`. `blocked` stops the whole queue (no further jobs start) and `start()` rejects with `BlockedFrameError(url)`.
  - `reduceAxe(results: AxeResults, job: Job, documentLinks: string[]): PageResult`: violations → `kind 'violation'`, incomplete → `'incomplete'`; nodes `{ selector: target.join(' '), html }`; `passedRules` = pass ids; `help`/`helpUrl` from the rule.
  - `runInFrame(job: Job, host: HTMLElement, opts: { axeUrl: string; tags: string[]; settleMs: number; timeoutMs: number }): Promise<JobOutcome>`: creates an `<iframe>` (`width` = job.viewport, height 900, `aria-hidden="true"`, `tabindex="-1"`, off-screen), loads `url + (?|&)esatto-a11y-scan=1`, on `load` waits `settleMs`, accesses `contentDocument` (throws/`null`/`about:blank` → `blocked`), appends `<script src=axeUrl>`, awaits its load, calls `contentWindow.axe.run(contentDocument, { runOnly: { type: 'tag', values: tags }, resultTypes: ['violations','incomplete'] })` (`resultTypes` only trims node lists of the other types; `passes` still lists every passed rule id, which `reduceAxe` needs). Collects document links. Always removes the frame. Timeout → `failed` with reason `timeout`.

- [ ] **Step 1: Write failing tests**

```ts
test('2.2 AA emits A and AA tags of all versions', () =>
  expect(axeTags('V22','AA',false)).toEqual(['wcag2a','wcag2aa','wcag21a','wcag21aa','wcag22aa']));
test('2.0 A emits only wcag2a', () => expect(axeTags('V20','A',false)).toEqual(['wcag2a']));
test('every emitted tag exists in axe', async () => {          // @vitest-environment jsdom
  const axe = (await import('axe-core')).default;
  const known = new Set(axe.getRules().flatMap(r => r.tags));
  for (const v of ['V20','V21','V22'] as const) for (const l of ['A','AA','AAA'] as const)
    for (const t of axeTags(v, l, true)) expect(known.has(t)).toBe(true);
});
test('other-origin pages are skipped with host', () => { /* origin https://a.se, page https://b.se/x → skipReason 'other-host:b.se' */ });
test('queue runs at most `concurrency` jobs at once', async () => { /* 7 jobs, concurrency 3 → max in-flight 3, all done */ });
test('failed job is retried once then reported failed', async () => { /* run fails twice → outcome failed, called 2 times */ });
test('blocked frame stops queue', async () => { /* 2nd job blocked → start() rejects BlockedFrameError, no job after it starts */ });
test('cancel stops starting new jobs', async () => {});
test('reduceAxe maps violations and incomplete with selector and help', () => {});
```

- [ ] **Step 2: Run** `npm test` in `Client/`. Expected: FAIL.
- [ ] **Step 3: Implement.**
- [ ] **Step 4: Run.** Expected: PASS.

---

### Task 11: Dashboard shell + Overview tab (with scanning)

**Files:**
- Modify: `Client/src/accessibility-dashboard.element.ts` (tabs: `uui-tab-group` Overview · Issues · Manual checklist · Settings; site picker `uui-select` shown only when `sites.length > 1`; loads `knowledge`, `sites`, `settings` once; holds `siteKey` and passes it down; Settings tab hidden when `!canEdit`, target shown read-only on Overview instead)
- Create: `Client/src/tabs/overview.element.ts`, `Client/src/dashboard.styles.ts`, `Client/src/scan/scan-controller.ts`
- Test: `Client/test/scan-controller.test.ts`

**Interfaces:**
- Consumes: `api.ts`, `ScanQueue`, `runInFrame`, `partitionPages`, `axeTags` (Task 10).
- Produces: `scanSite(siteKey, settings, host, onProgress, resumeScanId?): Promise<ScanSummary>` in `scan-controller.ts`: get pages → partition → `POST scans` (or `GET scans/{id}/done` when resuming and drop done jobs) → jobs = scannable × viewports (`[1280]` or `[1280, 320]`) → queue → each ok/failed outcome `POST scans/{id}/pages` (skipped pages are posted with status `skipped` first) → `POST finish { complete: !cancelled && !blocked }`. Server post failure: retry 3× with 1 s/2 s/4 s back-off, then pause the queue and surface "Resume".

Overview UI (mockup A, as approved): score ring (SVG, `role="img"`, `aria-label="Score 72 percent, WCAG 2.2 AA"`, colour by band: <50 red, <90 amber, ≥90 green, using UUI colour tokens that pass contrast in both themes); "X pass · Y fail · Z not verified" under it; tooltip/`<details>` "Why isn't this 100 %?" with the one-sentence explanation; target line with "Change in Settings" link when `canEdit`; last scan line; **Scan now** `uui-button`; while scanning a progress bar (`uui-loader-bar` + live region "Scanning page 12 of 148: /om-oss") and **Cancel**; four principle tiles as buttons that switch to Issues filtered by principle; summary "Automated: X pass / Y fail · Manual: N of M checked"; last-10 history table; incomplete last scan → "Resume" button; blocked-frame error → `uui-box` with the fix text from the spec's *How a scan works*; other-host skips → "N pages on other hosts were skipped — open the backoffice on <host> to scan them".

- [ ] **Step 1: Write failing tests** for `scanSite` with `api` and `runInFrame` mocked: `resume skips done jobs`, `mobile doubles jobs with 320 viewport`, `skipped pages are posted as skipped`, `cancel finishes as incomplete`, `post failure pauses after three retries`.
- [ ] **Step 2: Run.** Expected: FAIL. **Step 3: Implement** controller + UI. **Step 4: Run.** Expected: PASS.
- [ ] **Step 5: Verify on umbracokalaset26:** Scan now completes; ring, tiles and history render; reload mid-scan, then Resume continues; keyboard-only operation works; light + dark theme readable.

---

### Task 12: Issues tab + issue detail

**Files:**
- Create: `Client/src/tabs/issues.element.ts`, `Client/src/tabs/issue-detail.element.ts`

**Interfaces:**
- Consumes: `getIssues`, `getIssuesCsvUrl`, `addFalsePositive`, `removeFalsePositive`, `recheck` (api.ts); `runInFrame` + `reduceAxe` (re-check one page); `editContentUrl`, `editMediaUrl` (links.ts).

UI (as approved): filters (principle, level, owner Editor/Developer/Both, impact, show false positives) as labelled `uui-select`s; list rows "Help — 1.1.1 · A — 12 pages (34 elements) — Editor" sorted as the API returns; selecting a row opens the detail in the same tab (focus moves to its heading; Back returns focus to the row). Detail: criteria + level + "since 2.x" + counts + owner; **Why it matters / How to fix / Caveats** sections; occurrences grouped by page with snippet (escaped, monospace), selector, **Edit in Umbraco** (media link when `mediaKey`, else content link), **View on page** (new tab, `rel="noopener"`), **Mark as false positive** (per node or "whole page"; `uui-dialog` with a required note); W3C Understanding link; **Re-check this page** (runs one job via `runInFrame` and posts `recheck`, then reloads the group). Export CSV button downloads with the current filters.

- [ ] **Step 1: Implement.**
- [ ] **Step 2: Verify on umbracokalaset26:** introduce an image without alt text in a page; scan; the image-alt group shows that page; Edit in Umbraco opens the media item; mark false positive with a note → disappears and reappears with "show false positives"; fix alt text, publish, Re-check this page → group gone; CSV opens in Excel with Swedish characters intact.

---

### Task 13: Manual checklist tab

**Files:**
- Create: `Client/src/tabs/checklist.element.ts`

**Interfaces:**
- Consumes: `getChecklist`, `setChecklist` (api.ts).

UI (as approved): header "N of M checked"; filter All / Not checked / Failed / Needs re-check (radio group); one card per item: id, title, level, "new in 2.x" tag when `since` > 2.0, **How to test** text, "The scanner found N things to look at" link to Issues filtered by that criterion when `automatedFindings > 0`, documents hint when `documentLinks > 0`; buttons **Pass · Fail · Not applicable** (a `uui-button-group`, `aria-pressed` on the current one) + optional note (`uui-textarea`, saved on blur); shows "Anna, 2026-10-14" and a "Re-check" tag when `needsRecheck`. Saving updates the counter without reloading the list.

- [ ] **Step 1: Implement.**
- [ ] **Step 2: Verify:** mark 2.1.1 Pass with a note, reload → kept with name/date; Overview's "Manual: N of M" and score change accordingly.

---

### Task 14: Settings tab

**Files:**
- Create: `Client/src/tabs/settings.element.ts`

**Interfaces:**
- Consumes: `getSettings`, `putSettings` (api.ts); `locked: string[]`.

UI (as approved): **Target**: Version (2.0/2.1/2.2) and Level (A/AA/AAA) selects; hint "EU legal requirement today: 2.1 AA"; warning box when AAA ("W3C does not recommend AAA as a requirement for entire sites; some AAA criteria cannot be met for all content."). **Scan**: exclude paths (one per line), mobile width toggle with "doubles scan time", best-practice toggle with "shown separately, never counted in the score", re-check months (number 1–24). **Visitor features**: Follow OS → reduced motion / more contrast toggles (contrast with the clash warning); Show preferences button toggle with its seven option toggles (disabled while the button is off), position select, per-culture text overrides (collapsible; a key/value table per culture from the panel's resx keys); the overlay warning sentence from the spec. Every field whose path is in `locked` is disabled with the text "Set in configuration". Save button; success/failure via `UMB_NOTIFICATION_CONTEXT`.

- [ ] **Step 1: Implement.**
- [ ] **Step 2: Verify:** change target to 2.1 AAA → warning shows, Overview recomputes; set `Esatto:Accessibility:Visitor:Panel:Enabled=true` in umbracokalaset26's appsettings, restart → toggle disabled with "Set in configuration"; a user without Settings access sees no Settings tab.

---

### Task 15: Visitor features (public site)

**Files:**
- Create: `src/Public/AccessibilityHeadTagHelper.cs`, `AccessibilityPanelTagHelper.cs`, `AccessibilityPanelViewComponent.cs`, `PanelViewModel.cs`, `PanelTextProvider.cs`
- Create: `Views/Shared/Components/AccessibilityPanel/Default.cshtml`, `Resources/PanelText.resx`, `Resources/PanelText.sv.resx`
- Create: `Client/public-site/prefs.ts`, `a11y-boot.ts`, `a11y-panel.ts`, `a11y.css`
- Test: `PublicTagHelperTests.cs`, `PanelTextProviderTests.cs`, `Client/test/prefs.test.ts`

**Interfaces:**
- Consumes: `SettingsService.GetAsync()` (Task 4).
- Produces:
  - `<a11y-head />` (`[HtmlTargetElement("a11y-head", TagStructure = TagStructure.WithoutEndTag)]`): when **any** visitor feature is on, emits `<link rel="stylesheet" href="/esatto-accessibility/a11y.css">` and a **synchronous external** `<script src="/esatto-accessibility/a11y-boot.js" data-follow-motion="true|false" data-follow-contrast="true|false" data-panel="true|false"></script>` (external, not inline, so strict-CSP sites work); emits nothing when all are off.
  - `<a11y-panel />` (CookieBanner's `ConsentBannerTagHelper` pattern, view component name `AccessibilityPanel`): when the panel is enabled, renders the button + `<dialog>` with only the enabled options, plus `<script type="module" src="/esatto-accessibility/a11y-panel.js"></script>`; nothing otherwise.
  - Resx keys: `ButtonLabel` ("Accessibility" / "Tillgänglighet"), `DialogTitle`, `LargerText`, `Spacing` ("Bigger buttons and more spacing" / "Större knappar och mer luft"), `HighContrast`, `ReduceMotion`, `ReadableFont`, `HighlightLinks`, `ReadingGuide`, `Reset`, `Close`, `Note` ("These settings are saved in this browser only." / "Inställningarna sparas bara i den här webbläsaren."). `PanelTextProvider.Get(string key)`: settings override for `CultureInfo.CurrentUICulture.TwoLetterISOLanguageName` → resx for that culture → English.
  - `prefs.ts` (pure): `type Prefs = Partial<Record<'text'|'spacing'|'contrast'|'motion'|'font'|'links'|'guide', boolean>>`; `attributesFor(prefs: Prefs, os: { reducedMotion: boolean; moreContrast: boolean }, follow: { motion: boolean; contrast: boolean }): Record<string, string>` returns the `data-a11y-*` map from Global Constraints; an explicit pref (`true` or `false`) overrides OS; `readPrefs(storage)` / `writePrefs(storage, prefs)` on key `esatto-a11y`, corrupt JSON → `{}`.
  - `a11y-boot.ts`: reads its own `document.currentScript.dataset`, applies `attributesFor` to `document.documentElement` before paint, listens to `matchMedia` changes; sets `window.esattoA11yScan = true` when the URL has `esatto-a11y-scan=1`.
  - `a11y-panel.ts`: opens/closes the `<dialog>` with `showModal()`, returns focus to the button, Esc closes (native), checkbox changes write prefs and re-apply immediately, Reset clears; reading guide = a fixed 3-line-high translucent band following `pointermove`/focus, `pointer-events: none`, hidden under `prefers-reduced-motion` only when motion pref is on.
  - `a11y.css`: rules keyed only on the `data-a11y-*` attributes (and `@media (forced-colors: active)` for the baseline): text → `html { font-size: 125% }`; spacing → WCAG 1.4.12 values (`line-height: 1.5`, letter `0.12em`, word `0.16em`, paragraph `2em`) plus `a, button, input, select, textarea, [role=button], summary { min-block-size: 44px; min-inline-size: 44px }` and `gap`/margin between adjacent controls; contrast → `body` text `#111` on `#fff`, `p, li, h1–h6, label, td, th` get solid `#fff` backgrounds with `#111` text, controls get 2px `#111` borders, focus outline 3px; never `filter: contrast()` (it distorts images); motion → `*, *::before, *::after { animation-duration: .01ms !important; animation-iteration-count: 1 !important; transition-duration: .01ms !important; scroll-behavior: auto !important }`; font → `@font-face` Atkinson Hyperlegible + `body { font-family: "Atkinson Hyperlegible", sans-serif }`; links → `a { text-decoration: underline !important; outline: 2px solid currentColor; outline-offset: 2px }`. The panel's own styles: button 48×48 fixed at the chosen corner, visible label on focus/hover plus `aria-label` matching the visible text, dialog AAA contrast, works at 400 % zoom (scrollable dialog).

- [ ] **Step 1: Write failing tests**

```csharp
[Fact] public async Task Head_emits_nothing_when_all_visitor_features_are_off()
[Fact] public async Task Head_emits_css_and_boot_script_with_follow_flags()   // FollowOsMotion on → data-follow-motion="true"
[Fact] public async Task Panel_emits_nothing_when_disabled()
[Fact] public async Task Panel_renders_only_enabled_options()                  // ReadingGuide off → no "ReadingGuide" checkbox in output
[Fact] public void Text_override_wins_then_swedish_resx_then_english()
```

```ts
test('OS reduced motion sets motion=reduce when following', ...)
test('explicit pref false overrides OS', ...)
test('contrast not followed by default', ...)        // follow.contrast false + os.moreContrast → no data-a11y-contrast
test('corrupt storage reads as empty prefs', ...)
test('attribute names match the documented contract', ...) // exact keys list
```

- [ ] **Step 2: Run** `dotnet test` and `npm test`. Expected: FAIL.
- [ ] **Step 3: Implement.**
- [ ] **Step 4: Run.** Expected: PASS.
- [ ] **Step 5: Verify on umbracokalaset26** (add `@addTagHelper *, Esatto.Umbraco.Backoffice.Accessibility`, `<a11y-head />` in `<head>`, `<a11y-panel />` at the end of `<body>`): defaults → only motion following active (emulate `prefers-reduced-motion` in DevTools); enable the panel → button appears, each option works, settings survive reload, Reset clears; keyboard only + NVDA + 400 % zoom + Windows contrast themes; turn everything off → no package markup, CSS or JS in the page source.

---

### Task 16: README, screenshots, end-to-end on Nova, pack

**Files:**
- Modify: `Esatto.Umbraco.Backoffice.Accessibility/README.md`
- Create: `Esatto.Umbraco.Backoffice.Accessibility/docs/overview.png`, `issues.png`, `checklist.png`, `settings.png`, `panel.png`

README sections (repo standard, absolute raw-GitHub image URLs): what it does / **what it does not do** (overlay caveat, automated coverage limit, the panel ≠ compliance); install; the two tags + `@addTagHelper`; requirements (Umbraco 18, same-origin framing: `X-Frame-Options: SAMEORIGIN` or `frame-ancestors 'self'`); multi-site/other-host note; analytics exclusion (`esatto-a11y-scan=1`, `window.esattoA11yScan`); configuration (`Esatto:Accessibility` keys with an example, locking behaviour); the `data-a11y-*` contract with CSS refinement examples; how the score works; permissions; axe-core MPL-2.0 and Atkinson Hyperlegible OFL notices; source link.

- [ ] **Step 1: Write README; take screenshots on umbracokalaset26.**
- [ ] **Step 2: End-to-end on Nova (local, `c:\src\Esatto.Web.Nova`).** Reference the package via a local `dotnet pack` output folder as a package source (do not commit Nova changes; Carl decides). Scan esatto.se locally (Azurite running); verify Nova's `SAMEORIGIN` + `frame-src 'self'` allow the scan; check the Edit in Umbraco link paths and fix `links.ts` if Umbraco 18's paths differ; check that the cookie banner (CookieBanner/CookieTractor) doesn't break the scan.
- [ ] **Step 3: Full test run.** Run: `dotnet test Esatto.Umbraco.Backoffice.Accessibility.Tests` and `npm test` (in `Client/`). Expected: all PASS.
- [ ] **Step 4: Pack.** Run: `dotnet pack Esatto.Umbraco.Backoffice.Accessibility -c Release -o artifacts`. Expected: `.nupkg` containing `staticwebassets` with `App_Plugins/Esatto.Umbraco.Backoffice.Accessibility/{accessibility-dashboard.js,umbraco-package.json,axe.min.js,axe-core.LICENSE.txt}` and `esatto-accessibility/{a11y.css,a11y-boot.js,a11y-panel.js,fonts/*}`, plus `README.md` and `icon.png` (inspect with `unzip -l`). Version is a MinVer prerelease until Carl tags.
- [ ] **Step 5: Hand over to Carl.** Summarise; Carl verifies, then decides on commit, tag `Esatto.Umbraco.Backoffice.Accessibility-1.0.0` and `dotnet nuget push`.
