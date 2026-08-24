# Esatto.Umbraco.Backoffice.CookieBanner Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** Extract NDSTK's cookie consent feature into a turnkey, themeable, bilingual Umbraco 17+18 NuGet package that a consuming site wires up with two lines of Razor and one `app.UseCookieConsent()` call.

**Architecture:** A single `net10.0` Razor Class Library on the `Umbraco.Cms.* 17.0.0` floor with no upper bound (multi-targeting is impossible — 17 and 18 both resolve to `net10.0`, and NuGet cannot pack two variants of one canonical TFM with different dependency lists). The consent core is pure, request-scoped C# behind `IConsentState`; the endpoint is a package-registered minimal-API `MapPost` with a package-owned throttle, so the consumer never wires ASP.NET rate limiting or `MapControllers()`. Presentation ships as a view component plus two tag helpers, with self-contained `--consent-*` CSS tokens. Six Umbraco schema artefacts and 32 dictionary items install at startup via an `UmbracoApplicationStartedNotification` handler — there is no SQL table and no migration.

**Tech Stack:** .NET 10, Umbraco CMS 17.0.0 floor (verified on 18.1.1), ASP.NET Core Razor SDK / tag helpers / view components / minimal APIs, NPoco not used, xUnit v2 2.9.2 + NSubstitute 5.3.0, embedded resx localization, MinVer versioning.

**Spec:** `docs/superpowers/specs/2026-08-23-cookiebanner-package-design.md`

**Out of scope:** the cookie **scanner** (separate opt-in package, separate spec — it needs headless Chromium) and the **NDSTK migration** (separate plan in the NDSTK repo, since it is a different repo and commit stream).

## Global Constraints

Every task's requirements implicitly include this section.

- **PackageId == folder == csproj == RootNamespace == AssemblyName** = `Esatto.Umbraco.Backoffice.CookieBanner`. Source under `src/`. Tests in a sibling `Esatto.Umbraco.Backoffice.CookieBanner.Tests` folder, files flat.
- **Namespace collision (hard rule).** The namespace begins `Esatto.Umbraco.*`, so an **inline** `Umbraco.Cms.Something` reference binds to `Esatto.Umbraco` and fails to compile. Always use the short type name with a file-level `using Umbraco.Cms...;`. Never `global::`, never inline fully-qualified.
- **Umbraco refs pinned at `17.0.0`**, no upper bound: `Umbraco.Cms.Core`, `Umbraco.Cms.Infrastructure`, `Umbraco.Cms.Web.Common`. **No `Umbraco.Cms.Api.Management`** — 1.0.0 ships no dashboard. `Umbraco.Cms.Core` must stay referenced regardless: the Umbraco Marketplace excludes a tagged package whose supported version it cannot detect from NuGet dependencies.
- **Build against 17.0.0, lint against 17.4.2.** The 17.4.2 assemblies carry ~60 more `"Scheduled for removal in Umbraco 18"` markers and are a far better oracle for what the next major deletes.
- **Never call `GetById(Guid)` on `IContentService`.** Verified by decompiling the real assemblies — an earlier XML-doc-based account of this was wrong, see the spec. The member exists on both majors, but 17.0.0 re-declares it on `IContentService` with `new` and 18 does not, so a library compiled against 17.0.0 binds to a declaration site that no longer exists at runtime on 18 and throws `MissingMethodException`. Reproduced with a real cross-version binary test; each major compiles fine alone, which is why this is invisible without one. Use `IEntityService.Exists(Guid, UmbracoObjectTypes)` / `GetId(Guid, UmbracoObjectTypes)` — declared directly and identically on both.
- **Do not use anything Umbraco 18 removed:** `MigrationBase`/`PackageMigrationBase` (use the `Async*` variants), `ILocalizationService`, `IFileService`, `UmbracoApiController`, convention-based front-end API routing, the `IPublishedContent.Parent`/`.Children` **properties** (use the `Umbraco.Extensions` methods), `GetAtRoot()`, or any Swashbuckle/OpenApi type. Do not use `RecurringBackgroundJobBase` or `ITriggerableRecurringBackgroundJob` — absent in 17.0.0–17.4.2.
- **Cookie wire format is frozen.** Compact JSON, URL-encoded exactly once: `{"v":<int>,"t":"<ISO-8601 offset>","c":["marketing",…],"id":"<base64url>"}`. `necessary` is never written and is dropped on decode. Decode is total — malformed input returns `null`. A double-encoded value must not decode. Attributes: `Path=/`, `SameSite=Lax`, `HttpOnly=false`, `Secure = Request.IsHttps`. `NewConsentId()` is 22 chars base64url with no `+` `/` `=`.
- **JSON serialisation must use `JsonSerializerDefaults.Web` (camelCase).** Introducing a naming policy breaks `consent.js`'s property reads at runtime with no compile error.
- **No Swedish literals in shipped package code** — C# under `src/`, Razor views, JS, resx-independent strings, and backoffice property descriptions. Swedish ships only as `Resources/ConsentText.sv.resx`. **Carve-out:** a test that asserts Swedish output must contain the Swedish string it expects; that is required, not a violation. Test *fixture* data with no such need should still be English.
- **CSS/JS must be self-contained.** All tokens `--consent-*` on `:root` with neutral defaults — never `--primary`/`--accent`/`--bg`/`--text`/`--muted`. Buttons are `.consent-btn`/`--primary`/`--secondary`/`--link`, never the host's `.btn-primary`. No `footer` selector may ship. IDs prefixed `#esatto-consent-*`.
- **Static assets** live at `wwwroot/esatto-cookiebanner/` and serve from `/esatto-cookiebanner/` (`StaticWebAssetBasePath=/`). No `App_Plugins` folder and no `umbraco-package.json` in 1.0.0.
- **Fresh GUID series** `c00c1e00-…`; never reuse NDSTK's `da7a0001`/`e1e50001`/`c0117e17` ranges.
- **Testing:** xUnit **v2** — every test file needs an explicit `using Xunit;`, and `Assert.Single(collection, predicate)` is a v3 form, so use `Assert.Equal(1, x.Count(…))`. NSubstitute for Umbraco interfaces, hand-written fakes when counting, real framework primitives (`DefaultHttpContext`, `NullLogger<T>.Instance`) where possible. Each test carries a one-line comment naming the regression or guarantee it pins. Run: `dotnet test Esatto.Umbraco.Backoffice.CookieBanner.Tests/Esatto.Umbraco.Backoffice.CookieBanner.Tests.csproj`.
- **Repo reality:** no solution file, no CI, no coverage gate. Verification is a manual local `build → test → pack → unzip -l → inspect nuspec` loop.
- **Never `git commit`, `git push`, or `dotnet nuget push` without Carl's explicit approval.** Commit steps are written out because the plan format requires them; ask first. Carl runs the nuget.org push himself. `AutoPushToFeed` defaults to `false`.
- **README images must be absolute:** `https://raw.githubusercontent.com/carl-schele-esatto/Esatto.Packages/main/Esatto.Umbraco.Backoffice.CookieBanner/docs/<file>.png`. Relative paths render broken on nuget.org and nupkgs are immutable, so a mistake costs a patch release.

## File Structure

The package (`Esatto.Umbraco.Backoffice.CookieBanner/`):

| Path | Responsibility |
|---|---|
| `Esatto.Umbraco.Backoffice.CookieBanner.csproj` | Razor SDK library, NuGet metadata, `InternalsVisibleTo`, AutoPush target |
| `README.md`, `icon.png`, `docs/*.png` | Packed listing assets |
| `src/CookieBannerOptions.cs` | The whole configuration surface, bound from `Esatto:CookieBanner` |
| `src/ConsentCategory.cs`, `ConsentCategories.cs` | The four fixed categories and their frozen wire names |
| `src/ConsentDecision.cs` | A decision plus `HasGranted` / `NeedsRePrompt` |
| `src/ConsentCookieCodec.cs` | The frozen wire format — encode, total decode, consent-id minting |
| `src/IConsentState.cs`, `ConsentState.cs` | Request-scoped read of the consent cookie |
| `src/ConsentCookieWriter.cs`, `ConsentAction.cs` | Writes the cookie and owns its attributes |
| `src/ConsentModeScript.cs` | Google Consent Mode v2 string builder |
| `src/ConsentRequest.cs`, `ConsentStateResponse.cs`, `ConsentEndpointHandler.cs` | The endpoint's DTOs and its tested seam |
| `src/ConsentThrottle.cs` | Package-owned per-IP sliding window, replacing ASP.NET rate limiting |
| `src/VaryByConsentCookieMiddleware.cs` | `Vary: Cookie` + `Cache-Control: private, no-cache` on front-end HTML |
| `src/IConsentTextProvider.cs`, `ConsentTextProvider.cs` | Dictionary → resx → English resolution |
| `src/CookieDeclaration.cs`, `CookieRegistry.cs`, `CookieDeclarationMapper.cs` | One tested grouper, replacing two divergent copies in the views |
| `src/CookiePolicyPageResolver.cs` | Resolves the policy page by doctype alias, with a key override |
| `src/CookieBannerKeys.cs` | The fresh GUID series |
| `src/CookieBannerContentTypeFactory.cs` | Copied from NDSTK; stateful `_dataTypes` cache makes sharing unsafe |
| `src/CookieBannerSchemaInstaller.cs` | The six schema artefacts, in load-bearing order |
| `src/CookieBannerDictionaryInstaller.cs` | Culture-agnostic dictionary seeding |
| `src/CookieBannerContentSeeder.cs` | Seeds the policy page with three universally-present cookies |
| `src/CookieBannerComposer.cs`, `CookieBannerInstallHandler.cs` | DI registration and the startup install |
| `src/ServiceCollectionExtensions.cs`, `ApplicationBuilderExtensions.cs` | `AddCookieConsent()` / `UseCookieConsent()` |
| `src/TagHelpers/*.cs` | `<consent-script>`, `<consent-embed>`, `<consent-head />`, `<consent-banner />` |
| `src/ConsentBannerViewComponent.cs`, `ConsentBannerViewModel.cs` | The dialog's model and entry point |
| `Views/Shared/Components/ConsentBanner/Default.cshtml` | The dialog markup |
| `Views/CookiePolicy.cshtml` | The policy page template (no hardcoded `Layout`) |
| `Resources/ConsentText.resx`, `ConsentText.sv.resx` | 32 keys, English neutral + Swedish |
| `wwwroot/esatto-cookiebanner/consent.css`, `consent.js` | Self-contained styling and the `window.cookieConsent` API |

## Task Order

Tasks run in numeric order with one exception: **Task 8 must run before Task 7** (Task 7's `ConsentEmbedTagHelper` consumes `IConsentTextProvider`, which Task 8 introduces; Task 8 depends on nothing from Task 7). The note is repeated at Task 7.

---

### Task 1: Scaffold the package and test project

**Files:**
- Create: `c:\src\Esatto.Packages\Esatto.Umbraco.Backoffice.CookieBanner\Esatto.Umbraco.Backoffice.CookieBanner.csproj`
- Create: `c:\src\Esatto.Packages\Esatto.Umbraco.Backoffice.CookieBanner\README.md`
- Create: `c:\src\Esatto.Packages\Esatto.Umbraco.Backoffice.CookieBanner\icon.png` (byte copy of the house icon)
- Create: `c:\src\Esatto.Packages\Esatto.Umbraco.Backoffice.CookieBanner\src\CookieBannerOptions.cs`
- Create: `c:\src\Esatto.Packages\Esatto.Umbraco.Backoffice.CookieBanner.Tests\Esatto.Umbraco.Backoffice.CookieBanner.Tests.csproj`
- Test: `c:\src\Esatto.Packages\Esatto.Umbraco.Backoffice.CookieBanner.Tests\CookieBannerOptionsTests.cs`

**Interfaces:**
- Consumes: nothing (first task)
- Produces:
  - `public sealed class CookieBannerOptions`
  - `public const string CookieBannerOptions.SectionName = "Esatto:CookieBanner"`
  - `public int PolicyVersion { get; set; } = 1`
  - `public string CookieName { get; set; } = "cookie-consent"`
  - `public int CookieLifetimeDays { get; set; } = 365`
  - `public string? GoogleMeasurementId { get; set; }`
  - `public Guid? PolicyPageKey { get; set; }`
  - `public string EndpointPath { get; set; } = "/api/cookie-consent"`
  - `public int ThrottleRequestsPerMinute { get; set; } = 10`
  - MSBuild: `<InternalsVisibleTo Include="Esatto.Umbraco.Backoffice.CookieBanner.Tests" />` on the package project

- [ ] **Step 1: Create the package csproj (plus README and icon)**

`c:\src\Esatto.Packages\Esatto.Umbraco.Backoffice.CookieBanner\Esatto.Umbraco.Backoffice.CookieBanner.csproj`:

```xml
<Project Sdk="Microsoft.NET.Sdk.Razor">

  <PropertyGroup>
    <TargetFramework>net10.0</TargetFramework>
    <Nullable>enable</Nullable>
    <ImplicitUsings>enable</ImplicitUsings>
    <IsPackable>true</IsPackable>
    <GeneratePackageOnBuild>false</GeneratePackageOnBuild>

    <StaticWebAssetBasePath>/</StaticWebAssetBasePath>
    <RootNamespace>Esatto.Umbraco.Backoffice.CookieBanner</RootNamespace>
    <AssemblyName>Esatto.Umbraco.Backoffice.CookieBanner</AssemblyName>

    <NoWarn>$(NoWarn);CS1591;NU1902</NoWarn>
  </PropertyGroup>

  <PropertyGroup Label="NuGet">
    <PackageId>Esatto.Umbraco.Backoffice.CookieBanner</PackageId>
    <Authors>Carl Schéle</Authors>
    <Description>Turnkey GDPR/ePrivacy cookie consent for Umbraco 17 &amp; 18. A blocking consent dialog, per-category gating of scripts and embeds through the &lt;consent-script&gt; and &lt;consent-embed&gt; tag helpers, Google Consent Mode v2 signalling, an editor-managed cookie registry (cookieDefinition element type plus a cookieRegistry block list) and a rendered cookie policy page. Wiring is two lines of Razor and one app.UseCookieConsent() call.</Description>
    <PackageTags>umbraco;umbraco-marketplace;cookie-banner;cookie-consent;gdpr;eprivacy;consent-mode</PackageTags>
    <PackageReadmeFile>README.md</PackageReadmeFile>
    <PackageLicenseExpression>MIT</PackageLicenseExpression>
    <PackageIcon>icon.png</PackageIcon>
    <PackageProjectUrl>https://github.com/carl-schele-esatto/Esatto.Packages/tree/main/Esatto.Umbraco.Backoffice.CookieBanner</PackageProjectUrl>
    <RepositoryUrl>https://github.com/carl-schele-esatto/Esatto.Packages</RepositoryUrl>
    <RepositoryType>git</RepositoryType>
  </PropertyGroup>

  <ItemGroup>
    <None Include="README.md" Pack="true" PackagePath="\" />
    <None Include="icon.png" Pack="true" PackagePath="\" />
    <None Include="docs\**\*.png" Pack="true" PackagePath="docs\" />
  </ItemGroup>

  <ItemGroup>
    <InternalsVisibleTo Include="Esatto.Umbraco.Backoffice.CookieBanner.Tests" />
  </ItemGroup>

  <ItemGroup>
    <!-- No Umbraco.Cms.Api.Management: 1.0.0 ships no backoffice dashboard. Umbraco.Cms.Core
         alone is what the Marketplace reads to detect a supported version. -->
    <FrameworkReference Include="Microsoft.AspNetCore.App" />
    <PackageReference Include="Umbraco.Cms.Core" Version="17.0.0" />
    <PackageReference Include="Umbraco.Cms.Infrastructure" Version="17.0.0" />
    <PackageReference Include="Umbraco.Cms.Web.Common" Version="17.0.0" />
  </ItemGroup>

  <ItemGroup Label="Security overrides">
    <!-- Pins a patched floor for a high-severity vulnerability (NU1903) that Umbraco 17 pulls in
         transitively: Umbraco.Cms.Web.Common -> Umbraco.Cms.PublishedCache.HybridCache ->
         MessagePack 3.1.4, LZ4 decompression OOB read (GHSA-hv8m-jj95-wg3x), fixed in 3.1.7.
         Drop it once Umbraco bumps its own dependency past it.
         The sibling packages also pin Microsoft.OpenApi; that one arrives only through
         Umbraco.Cms.Api.Management -> Swashbuckle, which this package does not reference. -->
    <PackageReference Include="MessagePack" Version="3.1.7" />
  </ItemGroup>

  <PropertyGroup Label="AutoPush">
    <!-- Off by default so a local `dotnet pack` never publishes. CI opts in
         explicitly with `-p:AutoPushToFeed=true` after review/merge. -->
    <AutoPushToFeed Condition="'$(AutoPushToFeed)' == ''">false</AutoPushToFeed>
    <AutoPushFeedName Condition="'$(AutoPushFeedName)' == ''">esatto-packages</AutoPushFeedName>
  </PropertyGroup>

  <Target Name="AutoPushAfterPack" AfterTargets="Pack" Condition="'$(AutoPushToFeed)' == 'true'">
    <Message Importance="high" Text="Auto-pushing $(PackageId) $(PackageVersion) to '$(AutoPushFeedName)'..." />
    <Exec Command="dotnet nuget push &quot;$([MSBuild]::EnsureTrailingSlash('$(PackageOutputPath)'))$(PackageId).$(PackageVersion).nupkg&quot; --source $(AutoPushFeedName) --api-key az --skip-duplicate" />
  </Target>

</Project>
```

`c:\src\Esatto.Packages\Esatto.Umbraco.Backoffice.CookieBanner\README.md`:

```markdown
# Esatto.Umbraco.Backoffice.CookieBanner

Turnkey GDPR/ePrivacy cookie consent for Umbraco 17 & 18: a blocking consent dialog,
per-category gating of scripts and embeds, Google Consent Mode v2 signalling, an
editor-managed cookie registry and a rendered cookie policy page.

- Four fixed categories — `necessary`, `preferences`, `statistics`, `marketing` — mapped onto Consent Mode v2 signals
- `<consent-script>` and `<consent-embed>` gate third-party code **server-side**, so a blocked script is never sent to the browser
- Editor-managed registry: a `cookieDefinition` element type inside a `cookieRegistry` block list on a `cookiePolicy` document type
- Consent copy lives in Umbraco dictionary items, so legal wording changes without a deploy; English and Swedish ship as fallbacks
- Self-contained styling through `--consent-*` custom properties — no dependency on the host's design system
- No SQL migration, no backoffice steps, no rate-limiter wiring

## Install

```bash
dotnet add package Esatto.Umbraco.Backoffice.CookieBanner
```

Then one line in `Program.cs`, after `BootUmbracoAsync()`:

```csharp
app.UseCookieConsent();
```

And two lines of Razor in your layout:

```cshtml
@* in <head>, after your own stylesheet *@
<consent-head />

@* first element in <body>, before <header>, so the dialog is first in tab order *@
<consent-banner />
```

`_ViewImports.cshtml` needs the tag helpers registered:

```cshtml
@using Esatto.Umbraco.Backoffice.CookieBanner
@addTagHelper *, Esatto.Umbraco.Backoffice.CookieBanner
```

## Configuration

All settings are optional and bind from the `Esatto:CookieBanner` section:

```json
{
  "Esatto": {
    "CookieBanner": {
      "PolicyVersion": 1,
      "CookieName": "cookie-consent",
      "CookieLifetimeDays": 365,
      "GoogleMeasurementId": null,
      "PolicyPageKey": null,
      "EndpointPath": "/api/cookie-consent",
      "ThrottleRequestsPerMinute": 10
    }
  }
}
```

- `PolicyVersion` — bumping it re-prompts every visitor, so reworded consent text can be re-consented
- `CookieName` — set this to an existing name when migrating from a hand-rolled banner and no visitor is re-prompted
- `GoogleMeasurementId` — when null, no Consent Mode snippet and no gtag script are emitted at all
- `PolicyPageKey` — optional override; by default the first published `cookiePolicy` node is used

## Gating third-party code

```cshtml
<consent-script category="Statistics" async src="https://example.com/analytics.js"></consent-script>

<consent-embed category="Marketing" src="https://www.youtube.com/embed/xyz" title="Product tour" />
```

`<consent-script>` renders nothing until the category is granted. `<consent-embed>`
renders a placeholder with a call to action that opens the consent dialog.

## Browser API

```js
window.cookieConsent.open();
window.cookieConsent.has('statistics');   // => boolean
window.cookieConsent.get();               // => { categories, version }
document.addEventListener('cookieconsent:change', e => console.log(e.detail));
```

## Theming

Override the tokens on `:root` in your own stylesheet — for example:

```css
:root {
  --consent-accent: #ffd200;
  --consent-surface: #ffffff;
  --consent-text: #001f54;
  --consent-backdrop: rgba(0, 31, 84, 0.6);
}
```

## Compatibility

Single `net10.0` assembly built on the Umbraco `17.0.0` floor with no upper bound, verified
against 18. No SQL migration: every schema artefact is installed through Umbraco services at
startup, idempotently.

## Licence

MIT.
```

Then copy the shared house icon (byte-identical across all seven sibling packages):

```bash
cp c:/src/Esatto.Packages/Esatto.Umbraco.Backoffice.Redirects/icon.png \
   c:/src/Esatto.Packages/Esatto.Umbraco.Backoffice.CookieBanner/icon.png
```

- [ ] **Step 2: Create the test project csproj**

`c:\src\Esatto.Packages\Esatto.Umbraco.Backoffice.CookieBanner.Tests\Esatto.Umbraco.Backoffice.CookieBanner.Tests.csproj`:

```xml
<Project Sdk="Microsoft.NET.Sdk">

  <PropertyGroup>
    <TargetFramework>net10.0</TargetFramework>
    <Nullable>enable</Nullable>
    <ImplicitUsings>enable</ImplicitUsings>
    <IsPackable>false</IsPackable>
    <IsPublishable>false</IsPublishable>
  </PropertyGroup>

  <!-- Gives the tests the ASP.NET Core + Umbraco abstractions the package uses. -->
  <ItemGroup>
    <FrameworkReference Include="Microsoft.AspNetCore.App" />
  </ItemGroup>

  <!-- xUnit v2: it does NOT add an implicit `using Xunit;`, so every test file declares it. -->
  <ItemGroup>
    <PackageReference Include="Microsoft.NET.Test.Sdk" Version="17.11.1" />
    <PackageReference Include="xunit" Version="2.9.2" />
    <PackageReference Include="xunit.runner.visualstudio" Version="2.8.2" />
    <PackageReference Include="NSubstitute" Version="5.3.0" />
  </ItemGroup>

  <ItemGroup>
    <ProjectReference Include="..\Esatto.Umbraco.Backoffice.CookieBanner\Esatto.Umbraco.Backoffice.CookieBanner.csproj" />
  </ItemGroup>

</Project>
```

- [ ] **Step 3: Write the failing test**

`c:\src\Esatto.Packages\Esatto.Umbraco.Backoffice.CookieBanner.Tests\CookieBannerOptionsTests.cs`:

```csharp
using Xunit;

namespace Esatto.Umbraco.Backoffice.CookieBanner.Tests;

public class CookieBannerOptionsTests
{
    // Pins the config section name: it is the repo's first IOptions section, so this string is the
    // naming precedent and appears verbatim in every consumer's appsettings.json.
    [Fact]
    public void Section_name_is_the_published_config_path()
        => Assert.Equal("Esatto:CookieBanner", CookieBannerOptions.SectionName);

    // Pins the package-neutral defaults: a package must work with an empty config section, and must
    // not default to any one site's cookie name.
    [Fact]
    public void Defaults_are_package_neutral()
    {
        CookieBannerOptions options = new();

        Assert.Equal(1, options.PolicyVersion);
        Assert.Equal("cookie-consent", options.CookieName);
        Assert.Equal(365, options.CookieLifetimeDays);
        Assert.Null(options.GoogleMeasurementId);
        Assert.Null(options.PolicyPageKey);
        Assert.Equal("/api/cookie-consent", options.EndpointPath);
        Assert.Equal(10, options.ThrottleRequestsPerMinute);
    }
}
```

- [ ] **Step 4: Run the test to verify it fails**

Run: `dotnet test Esatto.Umbraco.Backoffice.CookieBanner.Tests/Esatto.Umbraco.Backoffice.CookieBanner.Tests.csproj --filter FullyQualifiedName~CookieBannerOptionsTests`

Expected: FAIL — the test project does not compile:

```
CookieBannerOptionsTests.cs(9,52): error CS0103: The name 'CookieBannerOptions' does not exist in the current context
CookieBannerOptionsTests.cs(16,9): error CS0246: The type or namespace name 'CookieBannerOptions' could not be found (are you missing a using directive or an assembly reference?)
```

- [ ] **Step 5: Implement**

`c:\src\Esatto.Packages\Esatto.Umbraco.Backoffice.CookieBanner\src\CookieBannerOptions.cs`:

```csharp
namespace Esatto.Umbraco.Backoffice.CookieBanner;

/// <summary>
/// Bound from the <c>Esatto:CookieBanner</c> configuration section. Every value has a
/// package-neutral default, so an empty section is a valid configuration.
/// </summary>
public sealed class CookieBannerOptions
{
    public const string SectionName = "Esatto:CookieBanner";

    /// <summary>
    /// Version of the consent text. Bumping this re-prompts every visitor, so it is configuration
    /// rather than a constant: rewording the policy is a deploy-time decision, not a code change.
    /// </summary>
    public int PolicyVersion { get; set; } = 1;

    /// <summary>
    /// Name of the consent cookie. Point this at an existing name when migrating from a
    /// hand-rolled banner and no visitor is re-prompted.
    /// </summary>
    public string CookieName { get; set; } = "cookie-consent";

    public int CookieLifetimeDays { get; set; } = 365;

    /// <summary>
    /// Google measurement id. When null, no Consent Mode snippet and no gtag script are emitted at
    /// all, rather than shipping dead script to every page.
    /// </summary>
    public string? GoogleMeasurementId { get; set; }

    /// <summary>
    /// Optional override for policy-page resolution. When null, the first published node of
    /// document type <c>cookiePolicy</c> is used.
    /// </summary>
    public Guid? PolicyPageKey { get; set; }

    /// <summary>Path the consent endpoint is mapped on by <c>UseCookieConsent()</c>.</summary>
    public string EndpointPath { get; set; } = "/api/cookie-consent";

    /// <summary>Sliding-window budget per client IP for the consent endpoint.</summary>
    public int ThrottleRequestsPerMinute { get; set; } = 10;
}
```

- [ ] **Step 6: Run the test to verify it passes**

Run: `dotnet test Esatto.Umbraco.Backoffice.CookieBanner.Tests/Esatto.Umbraco.Backoffice.CookieBanner.Tests.csproj --filter FullyQualifiedName~CookieBannerOptionsTests`

Expected: PASS — `Passed!  - Failed: 0, Passed: 2, Skipped: 0, Total: 2`

- [ ] **Step 7: Commit**

```bash
git add Esatto.Umbraco.Backoffice.CookieBanner/Esatto.Umbraco.Backoffice.CookieBanner.csproj \
        Esatto.Umbraco.Backoffice.CookieBanner/README.md \
        Esatto.Umbraco.Backoffice.CookieBanner/icon.png \
        Esatto.Umbraco.Backoffice.CookieBanner/src/CookieBannerOptions.cs \
        Esatto.Umbraco.Backoffice.CookieBanner.Tests/Esatto.Umbraco.Backoffice.CookieBanner.Tests.csproj \
        Esatto.Umbraco.Backoffice.CookieBanner.Tests/CookieBannerOptionsTests.cs
git commit -F - <<'MSG'
Scaffold the CookieBanner package and its test project

- Razor Class Library on the Umbraco 17.0.0 floor, net10.0, StaticWebAssetBasePath=/
- No Umbraco.Cms.Api.Management: 1.0.0 ships no backoffice dashboard
- Pin MessagePack 3.1.7 for NU1903 pulled in via Web.Common -> HybridCache
- InternalsVisibleTo the test project, so internals need no public surface
- Add CookieBannerOptions with the Esatto:CookieBanner section name and
  package-neutral defaults, covered by the first two tests

Co-Authored-By: Claude Opus 5 (1M context) <noreply@anthropic.com>
MSG
```

---

### Task 2: Consent categories, decision and cookie codec (pure logic)

**Files:**
- Create: `c:\src\Esatto.Packages\Esatto.Umbraco.Backoffice.CookieBanner\src\ConsentCategory.cs`
- Create: `c:\src\Esatto.Packages\Esatto.Umbraco.Backoffice.CookieBanner\src\ConsentCategories.cs`
- Create: `c:\src\Esatto.Packages\Esatto.Umbraco.Backoffice.CookieBanner\src\ConsentDecision.cs`
- Create: `c:\src\Esatto.Packages\Esatto.Umbraco.Backoffice.CookieBanner\src\ConsentCookieCodec.cs`
- Test: `c:\src\Esatto.Packages\Esatto.Umbraco.Backoffice.CookieBanner.Tests\ConsentCategoriesTests.cs`
- Test: `c:\src\Esatto.Packages\Esatto.Umbraco.Backoffice.CookieBanner.Tests\ConsentDecisionTests.cs`
- Test: `c:\src\Esatto.Packages\Esatto.Umbraco.Backoffice.CookieBanner.Tests\ConsentCookieCodecTests.cs`

**Interfaces:**
- Consumes:
  - the two csproj files and `<InternalsVisibleTo Include="Esatto.Umbraco.Backoffice.CookieBanner.Tests" />` from Task 1
- Produces:
  - `public enum ConsentCategory { Necessary, Preferences, Statistics, Marketing }`
  - `public static class ConsentCategories`
  - `public static readonly IReadOnlyList<ConsentCategory> ConsentCategories.Consentable`
  - `public static readonly IReadOnlyList<ConsentCategory> ConsentCategories.All`
  - `public static string ConsentCategories.ToWireName(ConsentCategory category)`
  - `public static bool ConsentCategories.TryParse(string? wireName, out ConsentCategory category)`
  - `public sealed record ConsentDecision(int PolicyVersion, DateTimeOffset DecidedAt, string ConsentId, IReadOnlySet<ConsentCategory> Granted)`
  - `public bool ConsentDecision.HasGranted(ConsentCategory category)`
  - `public bool ConsentDecision.NeedsRePrompt(int currentPolicyVersion)`
  - `internal static class ConsentCookieCodec`
  - `public static string ConsentCookieCodec.Encode(ConsentDecision decision)` (internal by containment)
  - `public static ConsentDecision? ConsentCookieCodec.Decode(string? cookieValue)` (internal by containment)
  - `public static string ConsentCookieCodec.NewConsentId()` (internal by containment)

- [ ] **Step 1: Write the failing test for the category wire names**

`c:\src\Esatto.Packages\Esatto.Umbraco.Backoffice.CookieBanner.Tests\ConsentCategoriesTests.cs`:

```csharp
using Xunit;

namespace Esatto.Umbraco.Backoffice.CookieBanner.Tests;

public class ConsentCategoriesTests
{
    // Pins the wire names against a member rename: the names are baked into every consent cookie
    // already in the wild, so Enum.ToString must never become the source of them.
    [Theory]
    [InlineData(ConsentCategory.Necessary, "necessary")]
    [InlineData(ConsentCategory.Preferences, "preferences")]
    [InlineData(ConsentCategory.Statistics, "statistics")]
    [InlineData(ConsentCategory.Marketing, "marketing")]
    public void Round_trips_every_wire_name(ConsentCategory category, string wireName)
    {
        Assert.Equal(wireName, ConsentCategories.ToWireName(category));

        Assert.True(ConsentCategories.TryParse(wireName, out ConsentCategory parsed));
        Assert.Equal(category, parsed);
    }

    // Pins that parsing is exact and case-sensitive: the codec feeds it hand-editable cookie
    // content, and a lenient parse would let "Marketing" grant marketing on a rejected cookie.
    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    [InlineData("Necessary")]
    [InlineData("MARKETING")]
    [InlineData("Statistics")]
    [InlineData("telepathy")]
    [InlineData(" statistics")]
    public void Rejects_anything_that_is_not_an_exact_wire_name(string? wireName)
    {
        Assert.False(ConsentCategories.TryParse(wireName, out ConsentCategory parsed));
        Assert.Equal(default, parsed);
    }

    // Pins that an out-of-range cast is loud rather than silently written to a cookie as an empty
    // or wrong category name.
    [Fact]
    public void Throws_when_asked_for_the_wire_name_of_an_undefined_value()
    {
        ArgumentOutOfRangeException exception = Assert.Throws<ArgumentOutOfRangeException>(
            () => ConsentCategories.ToWireName((ConsentCategory)99));

        Assert.Equal("category", exception.ParamName);
    }

    // Pins policy-page display order (necessary first) and that every declared enum member is
    // listed - a fifth category added to the enum must not silently vanish from the policy page.
    [Fact]
    public void All_lists_every_category_in_policy_page_order()
    {
        Assert.Equal(
            new[]
            {
                ConsentCategory.Necessary,
                ConsentCategory.Preferences,
                ConsentCategory.Statistics,
                ConsentCategory.Marketing,
            },
            ConsentCategories.All);

        Assert.Equal(Enum.GetValues<ConsentCategory>().Length, ConsentCategories.All.Count);
    }

    // Pins banner order and that necessary is never offered as a choice: it is implied, never
    // stored, and a checkbox for it would be a false promise.
    [Fact]
    public void Consentable_lists_the_choosable_categories_in_banner_order()
    {
        Assert.Equal(
            new[]
            {
                ConsentCategory.Preferences,
                ConsentCategory.Statistics,
                ConsentCategory.Marketing,
            },
            ConsentCategories.Consentable);

        Assert.DoesNotContain(ConsentCategory.Necessary, ConsentCategories.Consentable);
    }
}
```

- [ ] **Step 2: Run the test to verify it fails**

Run: `dotnet test Esatto.Umbraco.Backoffice.CookieBanner.Tests/Esatto.Umbraco.Backoffice.CookieBanner.Tests.csproj --filter FullyQualifiedName~ConsentCategoriesTests`

Expected: FAIL — the test project does not compile:

```
ConsentCategoriesTests.cs(12,18): error CS0246: The type or namespace name 'ConsentCategory' could not be found (are you missing a using directive or an assembly reference?)
ConsentCategoriesTests.cs(18,25): error CS0103: The name 'ConsentCategories' does not exist in the current context
```

- [ ] **Step 3: Implement the category model**

`c:\src\Esatto.Packages\Esatto.Umbraco.Backoffice.CookieBanner\src\ConsentCategory.cs`:

```csharp
namespace Esatto.Umbraco.Backoffice.CookieBanner;

/// <summary>
/// The four consent categories. <see cref="Necessary"/> is never declinable and is implied rather
/// than stored, so it must not appear in the cookie's category list.
/// </summary>
public enum ConsentCategory
{
    Necessary,
    Preferences,
    Statistics,
    Marketing,
}
```

`c:\src\Esatto.Packages\Esatto.Umbraco.Backoffice.CookieBanner\src\ConsentCategories.cs`:

```csharp
namespace Esatto.Umbraco.Backoffice.CookieBanner;

/// <summary>
/// Wire names for <see cref="ConsentCategory"/>. Kept as an explicit map rather than
/// <c>Enum.ToString</c> so that renaming a member cannot silently invalidate every cookie already
/// in the wild.
/// </summary>
public static class ConsentCategories
{
    /// <summary>The categories a visitor can actually choose, in banner display order.</summary>
    public static readonly IReadOnlyList<ConsentCategory> Consentable =
    [
        ConsentCategory.Preferences,
        ConsentCategory.Statistics,
        ConsentCategory.Marketing,
    ];

    /// <summary>All categories in policy-page display order, necessary first.</summary>
    public static readonly IReadOnlyList<ConsentCategory> All =
    [
        ConsentCategory.Necessary,
        ConsentCategory.Preferences,
        ConsentCategory.Statistics,
        ConsentCategory.Marketing,
    ];

    /// <summary>The stored, wire-stable name of a category.</summary>
    public static string ToWireName(ConsentCategory category) => category switch
    {
        ConsentCategory.Necessary => "necessary",
        ConsentCategory.Preferences => "preferences",
        ConsentCategory.Statistics => "statistics",
        ConsentCategory.Marketing => "marketing",
        _ => throw new ArgumentOutOfRangeException(nameof(category), category, null),
    };

    /// <summary>
    /// Parses a wire name. Deliberately exact and case-sensitive: the input is hand-editable
    /// cookie content, so a lenient match would let a near-miss grant a category.
    /// </summary>
    public static bool TryParse(string? wireName, out ConsentCategory category)
    {
        switch (wireName)
        {
            case "necessary": category = ConsentCategory.Necessary; return true;
            case "preferences": category = ConsentCategory.Preferences; return true;
            case "statistics": category = ConsentCategory.Statistics; return true;
            case "marketing": category = ConsentCategory.Marketing; return true;
            default: category = default; return false;
        }
    }
}
```

- [ ] **Step 4: Run the test to verify it passes**

Run: `dotnet test Esatto.Umbraco.Backoffice.CookieBanner.Tests/Esatto.Umbraco.Backoffice.CookieBanner.Tests.csproj --filter FullyQualifiedName~ConsentCategoriesTests`

Expected: PASS — `Passed!  - Failed: 0, Passed: 15, Skipped: 0, Total: 15`

- [ ] **Step 5: Write the failing test for the decision record**

`c:\src\Esatto.Packages\Esatto.Umbraco.Backoffice.CookieBanner.Tests\ConsentDecisionTests.cs`:

```csharp
using Xunit;

namespace Esatto.Umbraco.Backoffice.CookieBanner.Tests;

public class ConsentDecisionTests
{
    private static ConsentDecision Decision(int policyVersion, params ConsentCategory[] granted)
        => new(policyVersion, new DateTimeOffset(2026, 8, 21, 9, 12, 33, TimeSpan.Zero), "abc123", granted.ToHashSet());

    // Pins that necessary is implied, not stored: it is absent from the cookie by design, so
    // asking the decision about it must still answer true.
    [Fact]
    public void Necessary_is_granted_even_though_it_is_never_stored()
    {
        ConsentDecision decision = Decision(1);

        Assert.True(decision.HasGranted(ConsentCategory.Necessary));
        Assert.DoesNotContain(ConsentCategory.Necessary, decision.Granted);
    }

    // Pins the gate the tag helpers read: only the categories actually in the set are granted.
    [Fact]
    public void Reports_only_the_granted_categories()
    {
        ConsentDecision decision = Decision(1, ConsentCategory.Statistics);

        Assert.True(decision.HasGranted(ConsentCategory.Statistics));
        Assert.False(decision.HasGranted(ConsentCategory.Marketing));
        Assert.False(decision.HasGranted(ConsentCategory.Preferences));
    }

    // Pins the re-prompt rule that makes PolicyVersion useful: an older stored version re-prompts,
    // an equal or newer one does not (so bumping the option cannot loop the banner forever).
    [Fact]
    public void Needs_reprompt_only_when_stored_version_is_older()
    {
        ConsentDecision decision = Decision(1);

        Assert.False(decision.NeedsRePrompt(1));
        Assert.True(decision.NeedsRePrompt(2));
        Assert.False(Decision(3).NeedsRePrompt(2));
    }
}
```

- [ ] **Step 6: Run the test to verify it fails**

Run: `dotnet test Esatto.Umbraco.Backoffice.CookieBanner.Tests/Esatto.Umbraco.Backoffice.CookieBanner.Tests.csproj --filter FullyQualifiedName~ConsentDecisionTests`

Expected: FAIL — the test project does not compile:

```
ConsentDecisionTests.cs(7,21): error CS0246: The type or namespace name 'ConsentDecision' could not be found (are you missing a using directive or an assembly reference?)
```

- [ ] **Step 7: Implement the decision record**

`c:\src\Esatto.Packages\Esatto.Umbraco.Backoffice.CookieBanner\src\ConsentDecision.cs`:

```csharp
namespace Esatto.Umbraco.Backoffice.CookieBanner;

/// <summary>
/// A visitor's recorded consent choice, as carried by the consent cookie (its name comes from
/// <see cref="CookieBannerOptions.CookieName"/>).
/// </summary>
public sealed record ConsentDecision(
    int PolicyVersion,
    DateTimeOffset DecidedAt,
    string ConsentId,
    IReadOnlySet<ConsentCategory> Granted)
{
    /// <summary>
    /// True when the category may run. <see cref="ConsentCategory.Necessary"/> is implied rather
    /// than stored, so it is always granted.
    /// </summary>
    public bool HasGranted(ConsentCategory category)
        => category == ConsentCategory.Necessary || Granted.Contains(category);

    /// <summary>
    /// True when the visitor last decided against an older version of the consent text, which means
    /// the banner must be shown again with their previous choice pre-selected.
    /// </summary>
    public bool NeedsRePrompt(int currentPolicyVersion) => PolicyVersion < currentPolicyVersion;
}
```

- [ ] **Step 8: Run the test to verify it passes**

Run: `dotnet test Esatto.Umbraco.Backoffice.CookieBanner.Tests/Esatto.Umbraco.Backoffice.CookieBanner.Tests.csproj --filter FullyQualifiedName~ConsentDecisionTests`

Expected: PASS — `Passed!  - Failed: 0, Passed: 3, Skipped: 0, Total: 3`

- [ ] **Step 9: Write the failing test for the cookie codec**

`c:\src\Esatto.Packages\Esatto.Umbraco.Backoffice.CookieBanner.Tests\ConsentCookieCodecTests.cs`:

```csharp
using Xunit;

namespace Esatto.Umbraco.Backoffice.CookieBanner.Tests;

public class ConsentCookieCodecTests
{
    private static ConsentDecision Decision(params ConsentCategory[] granted)
        => new(1, new DateTimeOffset(2026, 8, 21, 9, 12, 33, TimeSpan.Zero), "abc123", granted.ToHashSet());

    // Pins the round trip: everything the banner and the tag helpers read must survive a trip
    // through the cookie unchanged.
    [Fact]
    public void Round_trips_a_decision()
    {
        ConsentDecision original = Decision(ConsentCategory.Preferences, ConsentCategory.Statistics);

        ConsentDecision? decoded = ConsentCookieCodec.Decode(ConsentCookieCodec.Encode(original));

        Assert.NotNull(decoded);
        Assert.Equal(original.PolicyVersion, decoded.PolicyVersion);
        Assert.Equal(original.DecidedAt, decoded.DecidedAt);
        Assert.Equal(original.ConsentId, decoded.ConsentId);
        Assert.Equal(
            new[] { ConsentCategory.Preferences, ConsentCategory.Statistics }.ToHashSet(),
            decoded.Granted.ToHashSet());
    }

    // Pins the exact documented wire shape - property order, ordinal-sorted categories, compact
    // JSON, ISO-8601 offset timestamp - because cookies in the wild are decoded by this format and
    // by the browser-side reader in consent.js.
    [Fact]
    public void Encodes_the_documented_wire_shape()
    {
        var encoded = ConsentCookieCodec.Encode(
            Decision(ConsentCategory.Statistics, ConsentCategory.Marketing, ConsentCategory.Preferences));

        Assert.Equal(
            """{"v":1,"t":"2026-08-21T09:12:33+00:00","c":["marketing","preferences","statistics"],"id":"abc123"}""",
            encoded);
    }

    // Pins that necessary is never written to the cookie, and that Encode emits plain JSON -
    // Response.Cookies.Append does the one and only URL-encoding pass, so asserting against the
    // escaped form here would hide a regression back to double encoding.
    [Fact]
    public void Omits_necessary_from_the_wire_format()
    {
        var encoded = ConsentCookieCodec.Encode(Decision(ConsentCategory.Necessary, ConsentCategory.Marketing));

        Assert.DoesNotContain("necessary", encoded);
        Assert.Contains("marketing", encoded);
    }

    // Pins that a decision with nothing granted still grants necessary after a full round trip.
    [Fact]
    public void Necessary_is_always_granted_even_when_absent()
    {
        ConsentDecision? decoded = ConsentCookieCodec.Decode(ConsentCookieCodec.Encode(Decision()));

        Assert.NotNull(decoded);
        Assert.True(decoded.HasGranted(ConsentCategory.Necessary));
        Assert.False(decoded.HasGranted(ConsentCategory.Statistics));
    }

    // Pins that decoding is total: malformed, truncated or hand-edited values mean "no decision",
    // never an exception on a front-end request.
    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    [InlineData("not-json")]
    [InlineData("%7B%22v%22%3A")]
    [InlineData("%7B%7D")]
    public void Returns_null_for_unusable_input(string? value)
        => Assert.Null(ConsentCookieCodec.Decode(value));

    // Pins forward compatibility: a category name this build does not know is dropped rather than
    // invalidating the whole cookie.
    [Fact]
    public void Ignores_unknown_categories()
    {
        // Plain JSON, exactly the shape Request.Cookies hands to Decode in production - never
        // percent-encoded, since the framework already decoded it once by the time Decode sees it.
        var json = """{"v":1,"t":"2026-08-21T09:12:33+00:00","c":["statistics","telepathy"],"id":"abc123"}""";

        ConsentDecision? decoded = ConsentCookieCodec.Decode(json);

        Assert.NotNull(decoded);
        Assert.Equal([ConsentCategory.Statistics], decoded.Granted.ToArray());
    }

    // Pins the corrected contract: if Decode is ever made to unescape again - reintroducing the
    // double-decode bug - a value that has already been through one round of percent-encoding
    // would start parsing successfully again. It must not.
    [Fact]
    public void A_url_encoded_cookie_value_does_not_decode_to_a_decision()
    {
        var encoded = ConsentCookieCodec.Encode(Decision(ConsentCategory.Statistics));
        var doubleEncoded = Uri.EscapeDataString(encoded);

        Assert.Null(ConsentCookieCodec.Decode(doubleEncoded));
    }

    // Pins the consent id shape: 22 base64url chars, no '+', '/' or '=', so it survives a cookie
    // value and a URL without escaping.
    [Fact]
    public void New_consent_id_is_url_safe_and_unique()
    {
        var first = ConsentCookieCodec.NewConsentId();
        var second = ConsentCookieCodec.NewConsentId();

        Assert.NotEqual(first, second);
        Assert.Equal(22, first.Length);
        Assert.DoesNotContain('+', first);
        Assert.DoesNotContain('/', first);
        Assert.DoesNotContain('=', first);
    }
}
```

- [ ] **Step 10: Run the test to verify it fails**

Run: `dotnet test Esatto.Umbraco.Backoffice.CookieBanner.Tests/Esatto.Umbraco.Backoffice.CookieBanner.Tests.csproj --filter FullyQualifiedName~ConsentCookieCodecTests`

Expected: FAIL — the test project does not compile:

```
ConsentCookieCodecTests.cs(17,45): error CS0103: The name 'ConsentCookieCodec' does not exist in the current context
```

- [ ] **Step 11: Implement the cookie codec**

`c:\src\Esatto.Packages\Esatto.Umbraco.Backoffice.CookieBanner\src\ConsentCookieCodec.cs`:

```csharp
using System.Security.Cryptography;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace Esatto.Umbraco.Backoffice.CookieBanner;

/// <summary>
/// Serialises a <see cref="ConsentDecision"/> to and from the cookie's compact JSON form:
/// <c>{"v":1,"t":"&lt;ISO-8601 offset&gt;","c":["marketing"],"id":"&lt;base64url&gt;"}</c>.
/// </summary>
/// <remarks>
/// Decoding is deliberately total: any malformed, truncated or hand-edited value decodes to
/// <c>null</c>, which the rest of the system treats as "no decision yet". The cookie is not a
/// security boundary — the worst a visitor can do is forge their own consent — so it is not signed.
/// </remarks>
internal static class ConsentCookieCodec
{
    private static readonly JsonSerializerOptions SerializerOptions = new()
    {
        DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull,
    };

    public static string Encode(ConsentDecision decision)
    {
        var dto = new ConsentCookieDto
        {
            Version = decision.PolicyVersion,
            DecidedAt = decision.DecidedAt.ToUniversalTime(),
            Categories = decision.Granted
                .Where(category => category != ConsentCategory.Necessary)
                .Select(ConsentCategories.ToWireName)
                .Order(StringComparer.Ordinal)
                .ToArray(),
            ConsentId = decision.ConsentId,
        };

        // Plain JSON: Response.Cookies.Append already URL-encodes the cookie value once. Encoding
        // here too would double-encode it, which the banner's single decodeURIComponent could not undo.
        return JsonSerializer.Serialize(dto, SerializerOptions);
    }

    public static ConsentDecision? Decode(string? cookieValue)
    {
        if (string.IsNullOrWhiteSpace(cookieValue))
        {
            return null;
        }

        try
        {
            // Plain JSON in, plain JSON out: Request.Cookies already URL-decodes the raw header once,
            // so unescaping here too would be a second decode. A value that has been through an extra
            // round of percent-encoding (i.e. does not start with '{') is exactly the shape a
            // double-encode bug would produce, and it must fail to parse rather than silently succeed.
            ConsentCookieDto? dto = JsonSerializer.Deserialize<ConsentCookieDto>(cookieValue, SerializerOptions);

            if (dto is null || dto.Version <= 0 || string.IsNullOrWhiteSpace(dto.ConsentId))
            {
                return null;
            }

            var granted = new HashSet<ConsentCategory>();
            foreach (var name in dto.Categories ?? [])
            {
                if (ConsentCategories.TryParse(name, out ConsentCategory category)
                    && category != ConsentCategory.Necessary)
                {
                    granted.Add(category);
                }
            }

            return new ConsentDecision(dto.Version, dto.DecidedAt, dto.ConsentId, granted);
        }
        catch (Exception exception) when (exception is JsonException or ArgumentException)
        {
            return null;
        }
    }

    /// <summary>
    /// A random 128-bit, URL-safe id for the decision. It is carried in the cookie so one visitor's
    /// consent can be correlated across requests without any further identifier.
    /// </summary>
    public static string NewConsentId()
        => Convert.ToBase64String(RandomNumberGenerator.GetBytes(16))
            .TrimEnd('=')
            .Replace('+', '-')
            .Replace('/', '_');

    private sealed class ConsentCookieDto
    {
        [JsonPropertyName("v")] public int Version { get; set; }

        [JsonPropertyName("t")] public DateTimeOffset DecidedAt { get; set; }

        [JsonPropertyName("c")] public string[]? Categories { get; set; }

        [JsonPropertyName("id")] public string? ConsentId { get; set; }
    }
}
```

- [ ] **Step 12: Run the whole test project to verify everything passes**

Run: `dotnet test Esatto.Umbraco.Backoffice.CookieBanner.Tests/Esatto.Umbraco.Backoffice.CookieBanner.Tests.csproj`

Expected: PASS — `Passed!  - Failed: 0, Passed: 33, Skipped: 0, Total: 33` (2 from Task 1, 15 category cases, 3 decision, 13 codec cases)

- [ ] **Step 13: Commit**

```bash
git add Esatto.Umbraco.Backoffice.CookieBanner/src/ConsentCategory.cs \
        Esatto.Umbraco.Backoffice.CookieBanner/src/ConsentCategories.cs \
        Esatto.Umbraco.Backoffice.CookieBanner/src/ConsentDecision.cs \
        Esatto.Umbraco.Backoffice.CookieBanner/src/ConsentCookieCodec.cs \
        Esatto.Umbraco.Backoffice.CookieBanner.Tests/ConsentCategoriesTests.cs \
        Esatto.Umbraco.Backoffice.CookieBanner.Tests/ConsentDecisionTests.cs \
        Esatto.Umbraco.Backoffice.CookieBanner.Tests/ConsentCookieCodecTests.cs
git commit -F - <<'MSG'
Port the consent category model, decision and cookie codec

- Four fixed categories with an explicit wire-name map, so a member rename
  cannot invalidate cookies already in the wild
- ConsentDecision keeps necessary implied rather than stored, and re-prompts
  only when the stored PolicyVersion is older than the configured one
- Codec is internal: consumers only ever see IConsentState
- Wire format unchanged from the NDSTK original, now pinned by an exact-shape
  assertion as well as by the round-trip and double-encode regression tests
- New coverage for wire-name round trips, exact-match parsing, the undefined
  cast, and All/Consentable ordering

Co-Authored-By: Claude Opus 5 (1M context) <noreply@anthropic.com>
MSG
```

### Task 3: Options, cookie writer and request-scoped consent state

**Files:**
- Create: `Esatto.Umbraco.Backoffice.CookieBanner/src/ConsentRequest.cs`
- Create: `Esatto.Umbraco.Backoffice.CookieBanner/src/ConsentStateResponse.cs`
- Create: `Esatto.Umbraco.Backoffice.CookieBanner/src/IConsentState.cs`
- Create: `Esatto.Umbraco.Backoffice.CookieBanner/src/ConsentState.cs`
- Create: `Esatto.Umbraco.Backoffice.CookieBanner/src/ConsentAction.cs`
- Create: `Esatto.Umbraco.Backoffice.CookieBanner/src/ConsentCookieWriter.cs`
- Test: `Esatto.Umbraco.Backoffice.CookieBanner.Tests/ConsentStateTests.cs`
- Test: `Esatto.Umbraco.Backoffice.CookieBanner.Tests/ConsentCookieWriterTests.cs`
- Test: `Esatto.Umbraco.Backoffice.CookieBanner.Tests/ConsentRequestTests.cs`
- Test: `Esatto.Umbraco.Backoffice.CookieBanner.Tests/ConsentStateResponseTests.cs`

**Interfaces:**
- Consumes: `public enum ConsentCategory { Necessary, Preferences, Statistics, Marketing }` (Task 2); `public static bool ConsentCategories.TryParse(string? wireName, out ConsentCategory category)` (Task 2); `public static string ConsentCategories.ToWireName(ConsentCategory category)` (Task 2); `public sealed record ConsentDecision(int PolicyVersion, DateTimeOffset DecidedAt, string ConsentId, IReadOnlySet<ConsentCategory> Granted)` (Task 2); `public bool ConsentDecision.HasGranted(ConsentCategory category)` (Task 2); `public bool ConsentDecision.NeedsRePrompt(int currentPolicyVersion)` (Task 2); `public static string ConsentCookieCodec.Encode(ConsentDecision decision)` (Task 2); `public static ConsentDecision? ConsentCookieCodec.Decode(string? cookieValue)` (Task 2); `public static string ConsentCookieCodec.NewConsentId()` (Task 2); `public sealed class CookieBannerOptions` with `public const string SectionName = "Esatto:CookieBanner";`, `public int PolicyVersion { get; set; } = 1;`, `public string CookieName { get; set; } = "cookie-consent";` (Task 1)
- Produces: `public interface IConsentState`; `ConsentDecision? IConsentState.Decision { get; }`; `bool IConsentState.NeedsDecision { get; }`; `bool IConsentState.HasGranted(ConsentCategory category)`; `internal sealed class ConsentState(IHttpContextAccessor httpContextAccessor, IOptions<CookieBannerOptions> options) : IConsentState`; `internal enum ConsentAction { AcceptAll, RejectAll, Custom, Withdrawn }`; `internal sealed class ConsentCookieWriter(IOptions<CookieBannerOptions> options)`; `internal static bool ConsentCookieWriter.TryParseAction(string? action, out ConsentAction parsed)`; `internal ConsentDecision ConsentCookieWriter.Write(HttpResponse response, HttpRequest request, ConsentAction action, IEnumerable<string>? categories)`; `internal sealed record ConsentRequest(string[]? Categories, string Action)`; `internal sealed record ConsentStateResponse(int Version, string[] Categories, string ConsentId, string DecidedAt)`; `public int CookieBannerOptions.CookieLifetimeDays { get; set; } = 365;`; `public string? CookieBannerOptions.GoogleMeasurementId { get; set; }`; `public Guid? CookieBannerOptions.PolicyPageKey { get; set; }`; `public string CookieBannerOptions.EndpointPath { get; set; } = "/api/cookie-consent";`; `public int CookieBannerOptions.ThrottleRequestsPerMinute { get; set; } = 10;`

- [ ] **Step 1: Write the failing test**

Create `Esatto.Umbraco.Backoffice.CookieBanner.Tests/ConsentStateTests.cs` — the full port of NDSTK's `tests/NDSTK.Tests/Consent/ConsentStateTests.cs`, retargeted at `CookieBannerOptions`.

```csharp
using Esatto.Umbraco.Backoffice.CookieBanner;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Options;
using Xunit;

namespace Esatto.Umbraco.Backoffice.CookieBanner.Tests;

public class ConsentStateTests
{
    private static IConsentState StateFor(string? cookieValue, int policyVersion = 1)
    {
        var options = new CookieBannerOptions { PolicyVersion = policyVersion };
        var httpContext = new DefaultHttpContext();

        if (cookieValue is not null)
        {
            // A real browser echoes back exactly the percent-encoded text the Set-Cookie response
            // gave it. ConsentCookieCodec.Encode returns plain JSON (the cookie layer, not the
            // codec, does the one encoding pass), so this helper must apply that encoding itself to
            // build a realistic raw Cookie header — otherwise characters like '"' and ',' break
            // RFC 6265 cookie-value grammar before ConsentState ever sees them.
            httpContext.Request.Headers.Cookie = $"{options.CookieName}={Uri.EscapeDataString(cookieValue)}";
        }

        return new ConsentState(
            new HttpContextAccessor { HttpContext = httpContext },
            Options.Create(options));
    }

    private static string CookieFor(int version, params ConsentCategory[] granted)
        => ConsentCookieCodec.Encode(
            new ConsentDecision(version, DateTimeOffset.UtcNow, "abc123", granted.ToHashSet()));

    [Fact]
    public void Needs_a_decision_when_no_cookie_is_present()
    {
        // Pins the blocking-banner guarantee: a first-time visitor must be prompted.
        IConsentState state = StateFor(null);

        Assert.True(state.NeedsDecision);
        Assert.Null(state.Decision);
    }

    [Fact]
    public void Necessary_is_granted_even_without_a_decision()
        // Pins that necessary cookies are never gated behind consent.
        => Assert.True(StateFor(null).HasGranted(ConsentCategory.Necessary));

    [Fact]
    public void Non_necessary_is_denied_without_a_decision()
    {
        // Pins deny-by-default: no cookie must never mean "granted".
        IConsentState state = StateFor(null);

        Assert.False(state.HasGranted(ConsentCategory.Statistics));
        Assert.False(state.HasGranted(ConsentCategory.Marketing));
        Assert.False(state.HasGranted(ConsentCategory.Preferences));
    }

    [Fact]
    public void Reads_granted_categories_from_the_cookie()
    {
        // Pins the cookie -> state read path that gates <consent-script>.
        IConsentState state = StateFor(CookieFor(1, ConsentCategory.Statistics));

        Assert.False(state.NeedsDecision);
        Assert.True(state.HasGranted(ConsentCategory.Statistics));
        Assert.False(state.HasGranted(ConsentCategory.Marketing));
    }

    [Fact]
    public void An_outdated_policy_version_denies_everything_and_reprompts()
    {
        // Pins why PolicyVersion exists: reworded cookie text must re-prompt and grant nothing
        // in the meantime, while the old decision stays readable for pre-selection.
        IConsentState state = StateFor(CookieFor(1, ConsentCategory.Statistics), policyVersion: 2);

        Assert.True(state.NeedsDecision);
        Assert.False(state.HasGranted(ConsentCategory.Statistics));
        Assert.True(state.HasGranted(ConsentCategory.Necessary));
        Assert.NotNull(state.Decision);
    }

    [Fact]
    public void A_corrupt_cookie_is_treated_as_no_decision()
    {
        // Pins that a hand-edited cookie degrades to "no decision" rather than throwing mid-render.
        IConsentState state = StateFor("garbage");

        Assert.True(state.NeedsDecision);
        Assert.False(state.HasGranted(ConsentCategory.Statistics));
    }

    [Fact]
    public void Survives_having_no_http_context()
    {
        // Pins safety outside a request (background work, view rendered from a null accessor).
        IConsentState state = new ConsentState(
            new HttpContextAccessor { HttpContext = null },
            Options.Create(new CookieBannerOptions()));

        Assert.True(state.NeedsDecision);
        Assert.False(state.HasGranted(ConsentCategory.Statistics));
    }
}
```

- [ ] **Step 2: Run the test to verify it fails**

Run: `dotnet test Esatto.Umbraco.Backoffice.CookieBanner.Tests/Esatto.Umbraco.Backoffice.CookieBanner.Tests.csproj --filter FullyQualifiedName~ConsentStateTests`
Expected: FAIL — build error `error CS0246: The type or namespace name 'IConsentState' could not be found (are you missing a using directive or an assembly reference?)` and the same for `ConsentState`.

- [ ] **Step 3: Implement**

Create `Esatto.Umbraco.Backoffice.CookieBanner/src/IConsentState.cs`:

```csharp
namespace Esatto.Umbraco.Backoffice.CookieBanner;

/// <summary>Request-scoped view of the current visitor's consent.</summary>
public interface IConsentState
{
    /// <summary>The decoded decision, or null when there is no usable cookie.</summary>
    ConsentDecision? Decision { get; }

    /// <summary>True when the banner must be shown: no decision, or one made against older text.</summary>
    bool NeedsDecision { get; }

    /// <summary>
    /// True only when the visitor has actively granted this category under the current policy version.
    /// <see cref="ConsentCategory.Necessary"/> is always true.
    /// </summary>
    bool HasGranted(ConsentCategory category);
}
```

Create `Esatto.Umbraco.Backoffice.CookieBanner/src/ConsentState.cs`:

```csharp
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Options;

namespace Esatto.Umbraco.Backoffice.CookieBanner;

/// <summary>
/// Reads and caches the consent cookie for the lifetime of one request. Registered scoped, so the
/// cookie is parsed at most once per request no matter how many tag helpers ask.
/// </summary>
internal sealed class ConsentState(
    IHttpContextAccessor httpContextAccessor,
    IOptions<CookieBannerOptions> options) : IConsentState
{
    private bool _resolved;
    private ConsentDecision? _decision;

    public ConsentDecision? Decision
    {
        get
        {
            if (_resolved)
            {
                return _decision;
            }

            _resolved = true;
            var raw = httpContextAccessor.HttpContext?.Request.Cookies[options.Value.CookieName];
            _decision = ConsentCookieCodec.Decode(raw);
            return _decision;
        }
    }

    public bool NeedsDecision
        => Decision is null || Decision.NeedsRePrompt(options.Value.PolicyVersion);

    public bool HasGranted(ConsentCategory category)
    {
        if (category == ConsentCategory.Necessary)
        {
            return true;
        }

        // A decision made against older cookie text grants nothing until it is renewed.
        return NeedsDecision is false && Decision?.HasGranted(category) is true;
    }
}
```

- [ ] **Step 4: Run the test to verify it passes**

Run: `dotnet test Esatto.Umbraco.Backoffice.CookieBanner.Tests/Esatto.Umbraco.Backoffice.CookieBanner.Tests.csproj --filter FullyQualifiedName~ConsentStateTests`
Expected: PASS — 7 passed.

- [ ] **Step 5: Commit**

```bash
git add Esatto.Umbraco.Backoffice.CookieBanner/src/IConsentState.cs Esatto.Umbraco.Backoffice.CookieBanner/src/ConsentState.cs Esatto.Umbraco.Backoffice.CookieBanner.Tests/ConsentStateTests.cs
git commit -m "Add request-scoped consent state" -m "- IConsentState is the only consent surface consumers need; ConsentState stays internal
- Parses the cookie once per request and denies everything while a re-prompt is pending
- Ports NDSTK's ConsentStateTests, retargeted at CookieBannerOptions" -m "Co-Authored-By: Claude Opus 5 (1M context) <noreply@anthropic.com>"
```

- [ ] **Step 6: Write the failing test**

Create `Esatto.Umbraco.Backoffice.CookieBanner.Tests/ConsentCookieWriterTests.cs` — the six cookie-attribute tests relocated down from NDSTK's `ConsentControllerTests`, plus `TryParseAction` coverage and the non-default cookie name round-trip the design spec demands.

```csharp
using System.Text.Json;
using Esatto.Umbraco.Backoffice.CookieBanner;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Options;
using Microsoft.Net.Http.Headers;
using Xunit;

namespace Esatto.Umbraco.Backoffice.CookieBanner.Tests;

public class ConsentCookieWriterTests
{
    private static (ConsentCookieWriter Writer, DefaultHttpContext Context) Build(
        int policyVersion = 1,
        int cookieLifetimeDays = 365,
        string cookieName = "cookie-consent")
    {
        IOptions<CookieBannerOptions> options = Options.Create(new CookieBannerOptions
        {
            PolicyVersion = policyVersion,
            CookieLifetimeDays = cookieLifetimeDays,
            CookieName = cookieName,
        });

        return (new ConsentCookieWriter(options), new DefaultHttpContext());
    }

    private static string SetCookieHeader(DefaultHttpContext context)
    {
        IEnumerable<string> headers = context.Response.Headers.SetCookie
            .Where(value => value is not null)
            .Select(value => value!);

        return Assert.Single(headers);
    }

    [Fact]
    public void Writes_the_cookie_with_the_documented_attributes()
    {
        // Pins Path=/, SameSite=Lax and the deliberate absence of HttpOnly: the banner reads this
        // cookie from JavaScript to unblock scripts without a reload.
        (ConsentCookieWriter writer, DefaultHttpContext context) = Build();

        writer.Write(context.Response, context.Request, ConsentAction.AcceptAll, ["statistics", "marketing"]);

        var header = SetCookieHeader(context);
        Assert.Contains("cookie-consent=", header);
        Assert.Contains("path=/", header, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("samesite=lax", header, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("httponly", header, StringComparison.OrdinalIgnoreCase);
    }

    [Theory]
    [InlineData(true)]
    [InlineData(false)]
    public void Secure_attribute_tracks_the_request_scheme(bool isHttps)
    {
        // Pins that Secure follows the actual scheme: hardcoding it breaks local http development,
        // omitting it leaks the cookie over http in production.
        (ConsentCookieWriter writer, DefaultHttpContext context) = Build();
        context.Request.IsHttps = isHttps;

        writer.Write(context.Response, context.Request, ConsentAction.RejectAll, []);

        var header = SetCookieHeader(context);

        if (isHttps)
        {
            Assert.Contains("secure", header, StringComparison.OrdinalIgnoreCase);
        }
        else
        {
            Assert.DoesNotContain("secure", header, StringComparison.OrdinalIgnoreCase);
        }
    }

    [Fact]
    public void Expiry_tracks_the_configured_cookie_lifetime_rather_than_the_365_day_default()
    {
        // Pins that CookieLifetimeDays is actually read rather than a constant being written.
        (ConsentCookieWriter writer, DefaultHttpContext context) = Build(cookieLifetimeDays: 30);

        writer.Write(context.Response, context.Request, ConsentAction.RejectAll, []);

        var header = SetCookieHeader(context);
        DateTimeOffset? expires = SetCookieHeaderValue.Parse(header).Expires;

        Assert.NotNull(expires);

        // Day count, not an exact timestamp, so test-runner latency cannot make this flaky. 30
        // falls nowhere near the 365-day default, so a writer that ignored CookieLifetimeDays
        // still fails this even with a generous window.
        var daysUntilExpiry = (expires!.Value - DateTimeOffset.UtcNow).TotalDays;
        Assert.InRange(daysUntilExpiry, 29, 31);
    }

    [Fact]
    public void The_cookie_value_is_encoded_exactly_once()
    {
        // Pins the wire format. Response.Cookies.Append is what URL-encodes the value on its way
        // into Set-Cookie; if ConsentCookieCodec.Encode escaped it too, this single decode would
        // still leave an escaped string and JsonDocument.Parse would throw instead of finding "v" —
        // exactly the bug consent.js's single decodeURIComponent would hit.
        (ConsentCookieWriter writer, DefaultHttpContext context) = Build();

        writer.Write(context.Response, context.Request, ConsentAction.RejectAll, []);

        var header = SetCookieHeader(context);
        var rawValue = SetCookieHeaderValue.Parse(header).Value.ToString();
        var decodedOnce = Uri.UnescapeDataString(rawValue);

        using JsonDocument json = JsonDocument.Parse(decodedOnce);
        Assert.Equal(1, json.RootElement.GetProperty("v").GetInt32());
    }

    [Fact]
    public void Unknown_and_necessary_categories_are_discarded_rather_than_trusted()
    {
        // Pins server-side filtering of an untrusted body: necessary is implied and never stored,
        // and an invented category name must not reach the cookie.
        (ConsentCookieWriter writer, DefaultHttpContext context) = Build();

        ConsentDecision decision = writer.Write(
            context.Response,
            context.Request,
            ConsentAction.Custom,
            ["statistics", "telepathy", "necessary"]);

        Assert.Equal(new[] { ConsentCategory.Statistics }, decision.Granted.ToArray());
    }

    [Fact]
    public void A_rejection_grants_nothing_even_if_categories_are_attached()
    {
        // Pins that the action, not the client's category list, decides a reject-all/withdrawal:
        // the server must not honour grants smuggled alongside an explicit refusal.
        (ConsentCookieWriter writer, DefaultHttpContext context) = Build();

        ConsentDecision decision = writer.Write(
            context.Response,
            context.Request,
            ConsentAction.RejectAll,
            ["statistics", "marketing"]);

        Assert.Empty(decision.Granted);
    }

    [Fact]
    public void The_cookie_records_the_current_policy_version()
    {
        // Pins that the decision carries the version it was made under, which is what makes
        // NeedsRePrompt work after a wording change.
        (ConsentCookieWriter writer, DefaultHttpContext context) = Build(policyVersion: 7);

        ConsentDecision decision = writer.Write(context.Response, context.Request, ConsentAction.RejectAll, []);

        Assert.Equal(7, decision.PolicyVersion);
    }

    [Theory]
    [InlineData("accept-all", ConsentAction.AcceptAll)]
    [InlineData("reject-all", ConsentAction.RejectAll)]
    [InlineData("custom", ConsentAction.Custom)]
    [InlineData("withdrawn", ConsentAction.Withdrawn)]
    // internal, not public: a public member cannot declare an internal parameter type
    // (CS0051), and ConsentAction is internal. xUnit v2 runs internal theories fine.
    internal void TryParseAction_maps_every_wire_action(string wireName, ConsentAction expected)
    {
        // Pins the four action names consent.js posts; renaming a member must not silently change
        // the wire contract.
        Assert.True(ConsentCookieWriter.TryParseAction(wireName, out ConsentAction parsed));
        Assert.Equal(expected, parsed);
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("Accept-All")]
    [InlineData("definitely-not-an-action")]
    public void TryParseAction_rejects_anything_it_does_not_recognise(string? wireName)
    {
        // Pins that an unrecognised or wrongly cased action is a hard failure (the endpoint turns
        // this into 400) rather than defaulting to AcceptAll.
        Assert.False(ConsentCookieWriter.TryParseAction(wireName, out _));
    }

    [Fact]
    public void A_non_default_cookie_name_is_honoured_end_to_end()
    {
        // Pins that no cookie name is hardcoded: a consumer keeping an existing site's cookie name
        // (NDSTK's "ndstk-consent") must not re-prompt a single visitor.
        IOptions<CookieBannerOptions> options = Options.Create(new CookieBannerOptions
        {
            CookieName = "legacy-consent",
        });
        var writeContext = new DefaultHttpContext();

        new ConsentCookieWriter(options).Write(
            writeContext.Response, writeContext.Request, ConsentAction.Custom, ["statistics"]);

        var header = SetCookieHeader(writeContext);
        Assert.StartsWith("legacy-consent=", header);

        SetCookieHeaderValue setCookie = SetCookieHeaderValue.Parse(header);
        var readContext = new DefaultHttpContext();
        readContext.Request.Headers.Cookie = $"{setCookie.Name}={setCookie.Value}";

        IConsentState state = new ConsentState(
            new HttpContextAccessor { HttpContext = readContext },
            options);

        Assert.False(state.NeedsDecision);
        Assert.True(state.HasGranted(ConsentCategory.Statistics));
    }
}
```

- [ ] **Step 7: Run the test to verify it fails**

Run: `dotnet test Esatto.Umbraco.Backoffice.CookieBanner.Tests/Esatto.Umbraco.Backoffice.CookieBanner.Tests.csproj --filter FullyQualifiedName~ConsentCookieWriterTests`
Expected: FAIL — build errors `error CS0246: The type or namespace name 'ConsentCookieWriter' could not be found`, `error CS0246: The type or namespace name 'ConsentAction' could not be found`, and `error CS0117: 'CookieBannerOptions' does not contain a definition for 'CookieLifetimeDays'`.

- [ ] **Step 8: Implement**

**Task 1 already created `CookieBannerOptions.cs` with ALL SEVEN properties.** Do not append anything. Instead VERIFY the existing `Esatto.Umbraco.Backoffice.CookieBanner/src/CookieBannerOptions.cs` matches the content below exactly, and only edit it if something differs. The complete file must read:

```csharp
namespace Esatto.Umbraco.Backoffice.CookieBanner;

/// <summary>
/// Bound from the <c>Esatto:CookieBanner</c> configuration section.
/// </summary>
public sealed class CookieBannerOptions
{
    public const string SectionName = "Esatto:CookieBanner";

    /// <summary>
    /// Version of the cookie text. Bumping this re-prompts every visitor, so it is configuration
    /// rather than a constant: changing the policy wording is a deploy-time decision, not a code change.
    /// </summary>
    public int PolicyVersion { get; set; } = 1;

    /// <summary>
    /// Name of the consent cookie. Package-neutral by default; a site adopting the package over an
    /// existing banner sets this to its old name so no visitor is re-prompted.
    /// </summary>
    public string CookieName { get; set; } = "cookie-consent";

    public int CookieLifetimeDays { get; set; } = 365;

    /// <summary>
    /// Google measurement id. When null, no Consent Mode snippet is emitted at all, rather than
    /// shipping dead script to every page.
    /// </summary>
    public string? GoogleMeasurementId { get; set; }

    /// <summary>
    /// Optional override for policy-page resolution. When null, the first published node of
    /// document type <c>cookiePolicy</c> is used.
    /// </summary>
    public Guid? PolicyPageKey { get; set; }

    /// <summary>Route the package maps its consent endpoint on.</summary>
    public string EndpointPath { get; set; } = "/api/cookie-consent";

    /// <summary>Per-IP sliding-window budget for the consent endpoint.</summary>
    public int ThrottleRequestsPerMinute { get; set; } = 10;
}
```

Create `Esatto.Umbraco.Backoffice.CookieBanner/src/ConsentAction.cs`:

```csharp
namespace Esatto.Umbraco.Backoffice.CookieBanner;

/// <summary>How a decision was reached. This is the endpoint's input contract, not a log record.</summary>
internal enum ConsentAction
{
    AcceptAll,
    RejectAll,
    Custom,
    Withdrawn,
}
```

Create `Esatto.Umbraco.Backoffice.CookieBanner/src/ConsentCookieWriter.cs`:

```csharp
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Options;

namespace Esatto.Umbraco.Backoffice.CookieBanner;

/// <summary>
/// Turns a validated request into a decision and writes the cookie.
/// </summary>
/// <remarks>
/// The cookie is written here, server-side, rather than by JavaScript. That is what guarantees the
/// attributes are correct — lifetime, SameSite, and Secure tracking the actual scheme.
/// </remarks>
internal sealed class ConsentCookieWriter(IOptions<CookieBannerOptions> options)
{
    /// <summary>Known action names, mapped explicitly so an unrecognised value is a hard failure.</summary>
    public static bool TryParseAction(string? action, out ConsentAction parsed)
    {
        switch (action)
        {
            case "accept-all": parsed = ConsentAction.AcceptAll; return true;
            case "reject-all": parsed = ConsentAction.RejectAll; return true;
            case "custom": parsed = ConsentAction.Custom; return true;
            case "withdrawn": parsed = ConsentAction.Withdrawn; return true;
            default: parsed = default; return false;
        }
    }

    public ConsentDecision Write(
        HttpResponse response,
        HttpRequest request,
        ConsentAction action,
        IEnumerable<string>? categories)
    {
        CookieBannerOptions settings = options.Value;

        var granted = new HashSet<ConsentCategory>();

        // An explicit refusal or withdrawal grants nothing whatever the client attached to it; the
        // server decides what "reject all" means.
        if (action is not (ConsentAction.RejectAll or ConsentAction.Withdrawn))
        {
            foreach (var name in categories ?? [])
            {
                // Necessary is implied, never client-supplied; unknown names are discarded.
                if (ConsentCategories.TryParse(name, out ConsentCategory category)
                    && category != ConsentCategory.Necessary)
                {
                    granted.Add(category);
                }
            }
        }

        var decision = new ConsentDecision(
            settings.PolicyVersion,
            DateTimeOffset.UtcNow,
            ConsentCookieCodec.NewConsentId(),
            granted);

        response.Cookies.Append(settings.CookieName, ConsentCookieCodec.Encode(decision), new CookieOptions
        {
            Path = "/",
            SameSite = SameSiteMode.Lax,
            HttpOnly = false, // the banner must read this to unblock scripts without a reload
            Secure = request.IsHttps,
            Expires = DateTimeOffset.UtcNow.AddDays(settings.CookieLifetimeDays),
            IsEssential = true,
        });

        return decision;
    }
}
```

- [ ] **Step 9: Run the test to verify it passes**

Run: `dotnet test Esatto.Umbraco.Backoffice.CookieBanner.Tests/Esatto.Umbraco.Backoffice.CookieBanner.Tests.csproj --filter FullyQualifiedName~ConsentCookieWriterTests`
Expected: PASS — 15 passed (7 facts plus 2 + 4 theory cases).

- [ ] **Step 10: Commit**

```bash
git add Esatto.Umbraco.Backoffice.CookieBanner/src/CookieBannerOptions.cs Esatto.Umbraco.Backoffice.CookieBanner/src/ConsentAction.cs Esatto.Umbraco.Backoffice.CookieBanner/src/ConsentCookieWriter.cs Esatto.Umbraco.Backoffice.CookieBanner.Tests/ConsentCookieWriterTests.cs
git commit -m "Write the consent cookie server-side" -m "- Complete CookieBannerOptions with lifetime, measurement id, policy page key, endpoint path and throttle budget
- ConsentCookieWriter takes the request explicitly so Secure tracks the real scheme
- Relocate the six cookie-attribute tests off the endpoint and add TryParseAction plus a non-default cookie name round-trip" -m "Co-Authored-By: Claude Opus 5 (1M context) <noreply@anthropic.com>"
```

- [ ] **Step 11: Write the failing test**

Create `Esatto.Umbraco.Backoffice.CookieBanner.Tests/ConsentRequestTests.cs`:

```csharp
using System.Text.Json;
using Esatto.Umbraco.Backoffice.CookieBanner;
using Xunit;

namespace Esatto.Umbraco.Backoffice.CookieBanner.Tests;

public class ConsentRequestTests
{
    private static readonly JsonSerializerOptions WebOptions = new(JsonSerializerDefaults.Web);

    [Fact]
    public void Binds_the_body_consent_js_posts()
    {
        // Pins the request wire contract: camelCase names, bound through the record's positional
        // constructor the same way the minimal-API endpoint binds it.
        ConsentRequest? request = JsonSerializer.Deserialize<ConsentRequest>(
            """{"categories":["statistics","marketing"],"action":"accept-all"}""",
            WebOptions);

        Assert.NotNull(request);
        Assert.Equal(new[] { "statistics", "marketing" }, request!.Categories);
        Assert.Equal("accept-all", request.Action);
    }

    [Fact]
    public void A_body_without_categories_leaves_them_null_rather_than_failing()
    {
        // Pins that the writer's `categories ?? []` guard has a reachable null to guard against.
        ConsentRequest? request = JsonSerializer.Deserialize<ConsentRequest>(
            """{"action":"reject-all"}""",
            WebOptions);

        Assert.NotNull(request);
        Assert.Null(request!.Categories);
    }

    [Fact]
    public void Carries_no_culture_field()
    {
        // Pins the dropped consent-log scaffolding: NDSTK's ConsentRequest.Culture was written by
        // consent.js and never read, and must not ship as an unkept promise.
        Assert.Null(typeof(ConsentRequest).GetProperty("Culture"));
    }
}
```

Create `Esatto.Umbraco.Backoffice.CookieBanner.Tests/ConsentStateResponseTests.cs`:

```csharp
using System.Text.Json;
using Esatto.Umbraco.Backoffice.CookieBanner;
using Xunit;

namespace Esatto.Umbraco.Backoffice.CookieBanner.Tests;

public class ConsentStateResponseTests
{
    [Fact]
    public void Serialises_to_the_camel_cased_shape_consent_js_reads()
    {
        // Pins the response wire contract the banner uses to unblock scripts without a reload:
        // renaming a member here silently breaks consent.js, which has no compiler to catch it.
        var json = JsonSerializer.Serialize(
            new ConsentStateResponse(
                3,
                ["marketing", "statistics"],
                "abc123",
                "2026-08-23T10:00:00.0000000+00:00"),
            new JsonSerializerOptions(JsonSerializerDefaults.Web));

        Assert.Equal(
            """{"version":3,"categories":["marketing","statistics"],"consentId":"abc123","decidedAt":"2026-08-23T10:00:00.0000000+00:00"}""",
            json);
    }
}
```

- [ ] **Step 12: Run the test to verify it fails**

Run: `dotnet test Esatto.Umbraco.Backoffice.CookieBanner.Tests/Esatto.Umbraco.Backoffice.CookieBanner.Tests.csproj --filter "FullyQualifiedName~ConsentRequestTests|FullyQualifiedName~ConsentStateResponseTests"`
Expected: FAIL — build errors `error CS0246: The type or namespace name 'ConsentRequest' could not be found` and `error CS0246: The type or namespace name 'ConsentStateResponse' could not be found`.

- [ ] **Step 13: Implement**

Create `Esatto.Umbraco.Backoffice.CookieBanner/src/ConsentRequest.cs`:

```csharp
namespace Esatto.Umbraco.Backoffice.CookieBanner;

/// <summary>
/// Body of the consent endpoint. Every field is untrusted and validated server-side: the action is
/// parsed by <see cref="ConsentCookieWriter.TryParseAction"/> and unknown categories are dropped.
/// </summary>
internal sealed record ConsentRequest(string[]? Categories, string Action);
```

Create `Esatto.Umbraco.Backoffice.CookieBanner/src/ConsentStateResponse.cs`:

```csharp
namespace Esatto.Umbraco.Backoffice.CookieBanner;

/// <summary>
/// Canonical consent state after a decision. The banner uses this to unblock scripts without a
/// reload, so it must reflect what the server actually stored, not what the client asked for.
/// </summary>
internal sealed record ConsentStateResponse(
    int Version,
    string[] Categories,
    string ConsentId,
    string DecidedAt);
```

- [ ] **Step 14: Run the test to verify it passes**

Run: `dotnet test Esatto.Umbraco.Backoffice.CookieBanner.Tests/Esatto.Umbraco.Backoffice.CookieBanner.Tests.csproj --filter "FullyQualifiedName~ConsentRequestTests|FullyQualifiedName~ConsentStateResponseTests"`
Expected: PASS — 4 passed.

- [ ] **Step 15: Commit**

```bash
git add Esatto.Umbraco.Backoffice.CookieBanner/src/ConsentRequest.cs Esatto.Umbraco.Backoffice.CookieBanner/src/ConsentStateResponse.cs Esatto.Umbraco.Backoffice.CookieBanner.Tests/ConsentRequestTests.cs Esatto.Umbraco.Backoffice.CookieBanner.Tests/ConsentStateResponseTests.cs
git commit -m "Add the consent endpoint wire records" -m "- ConsentRequest drops the never-read Culture field from NDSTK's version
- Pin the camelCase request and response shapes consent.js depends on" -m "Co-Authored-By: Claude Opus 5 (1M context) <noreply@anthropic.com>"
```

### Task 4: Google Consent Mode v2 script builder

**Files:**
- Create: `Esatto.Umbraco.Backoffice.CookieBanner/src/ConsentModeScript.cs`
- Test: `Esatto.Umbraco.Backoffice.CookieBanner.Tests/FakeConsentState.cs`
- Test: `Esatto.Umbraco.Backoffice.CookieBanner.Tests/ConsentModeScriptTests.cs`

**Interfaces:**
- Consumes: `public enum ConsentCategory { Necessary, Preferences, Statistics, Marketing }` (Task 2); `public sealed record ConsentDecision(int PolicyVersion, DateTimeOffset DecidedAt, string ConsentId, IReadOnlySet<ConsentCategory> Granted)` (Task 2); `public interface IConsentState` with `ConsentDecision? Decision { get; }`, `bool NeedsDecision { get; }`, `bool HasGranted(ConsentCategory category)` (Task 3); `public string? CookieBannerOptions.GoogleMeasurementId { get; set; }` (Task 3)
- Produces: `public static class ConsentModeScript`; `public static string ConsentModeScript.Defaults()`; `public static string ConsentModeScript.Update(IConsentState consent)`; `public static string ConsentModeScript.Config(string measurementId)`; test helper `internal sealed class FakeConsentState(params ConsentCategory[] granted) : IConsentState` with `public bool NeedsDecision { get; init; }`

- [ ] **Step 1: Write the shared test fake**

Create `Esatto.Umbraco.Backoffice.CookieBanner.Tests/FakeConsentState.cs` — the hand-written `IConsentState` every later tag-helper and view-component test reuses.

```csharp
using Esatto.Umbraco.Backoffice.CookieBanner;

namespace Esatto.Umbraco.Backoffice.CookieBanner.Tests;

/// <summary>
/// Hand-written rather than substituted: these tests assert on category gating, so the fake must
/// reproduce the real rule that a pending decision grants nothing but Necessary.
/// </summary>
internal sealed class FakeConsentState(params ConsentCategory[] granted) : IConsentState
{
    private readonly HashSet<ConsentCategory> _granted = granted.ToHashSet();

    public ConsentDecision? Decision => new(1, DateTimeOffset.UtcNow, "test", _granted);

    public bool NeedsDecision { get; init; }

    public bool HasGranted(ConsentCategory category)
        => category == ConsentCategory.Necessary || (NeedsDecision is false && _granted.Contains(category));
}
```

- [ ] **Step 2: Write the failing test**

Create `Esatto.Umbraco.Backoffice.CookieBanner.Tests/ConsentModeScriptTests.cs` — the full port of NDSTK's `tests/NDSTK.Tests/Consent/ConsentModeScriptTests.cs`.

```csharp
using Esatto.Umbraco.Backoffice.CookieBanner;
using Xunit;

namespace Esatto.Umbraco.Backoffice.CookieBanner.Tests;

public class ConsentModeScriptTests
{
    [Fact]
    public void Defaults_deny_every_signal()
    {
        // Pins the pre-consent state Consent Mode v2 requires: every signal denied before any
        // Google tag can read it, plus the wait_for_update window the double Update() closes.
        var script = ConsentModeScript.Defaults();

        Assert.Contains("'ad_storage':'denied'", script);
        Assert.Contains("'ad_user_data':'denied'", script);
        Assert.Contains("'ad_personalization':'denied'", script);
        Assert.Contains("'analytics_storage':'denied'", script);
        Assert.Contains("'functionality_storage':'denied'", script);
        Assert.Contains("'personalization_storage':'denied'", script);
        Assert.Contains("'wait_for_update':500", script);
        Assert.DoesNotContain("granted", script);
    }

    [Fact]
    public void Statistics_grants_only_analytics_storage()
    {
        // Pins the category -> signal mapping: statistics must not leak into the ad signals.
        var script = ConsentModeScript.Update(new FakeConsentState(ConsentCategory.Statistics));

        Assert.Contains("'ad_storage':'denied'", script);
        Assert.Contains("'ad_user_data':'denied'", script);
        Assert.Contains("'ad_personalization':'denied'", script);
        Assert.Contains("'analytics_storage':'granted'", script);
        Assert.Contains("'functionality_storage':'denied'", script);
        Assert.Contains("'personalization_storage':'denied'", script);
    }

    [Fact]
    public void Marketing_grants_the_three_ad_signals()
    {
        // Pins that marketing consent drives exactly ad_storage, ad_user_data and ad_personalization.
        var script = ConsentModeScript.Update(new FakeConsentState(ConsentCategory.Marketing));

        Assert.Contains("'ad_storage':'granted'", script);
        Assert.Contains("'ad_user_data':'granted'", script);
        Assert.Contains("'ad_personalization':'granted'", script);
        Assert.Contains("'analytics_storage':'denied'", script);
        Assert.Contains("'functionality_storage':'denied'", script);
        Assert.Contains("'personalization_storage':'denied'", script);
    }

    [Fact]
    public void Preferences_grants_functionality_and_personalization()
    {
        // Pins that preferences maps to the two storage signals and nothing else.
        var script = ConsentModeScript.Update(new FakeConsentState(ConsentCategory.Preferences));

        Assert.Contains("'ad_storage':'denied'", script);
        Assert.Contains("'ad_user_data':'denied'", script);
        Assert.Contains("'ad_personalization':'denied'", script);
        Assert.Contains("'analytics_storage':'denied'", script);
        Assert.Contains("'functionality_storage':'granted'", script);
        Assert.Contains("'personalization_storage':'granted'", script);
    }

    [Fact]
    public void Nothing_granted_denies_everything()
    {
        // Pins that an update for a visitor who granted nothing cannot contain a single grant.
        var script = ConsentModeScript.Update(new FakeConsentState());

        Assert.DoesNotContain("granted", script);
    }

    [Fact]
    public void Config_emits_js_and_config_calls_with_the_measurement_id()
    {
        // Pins that the destination is registered and the initial page view fired.
        var script = ConsentModeScript.Config("G-ABC123");

        Assert.Contains("gtag('js',new Date())", script);
        Assert.Contains("gtag('config',\"G-ABC123\")", script);
    }

    [Fact]
    public void Config_safely_encodes_a_measurement_id_that_could_break_out_of_the_script()
    {
        // Pins the injection guard: a configured id is spliced into an inline <script>, so it must
        // go through a JSON encoder that escapes '<' and '>'.
        var script = ConsentModeScript.Config("</script><script>alert(1)");

        Assert.DoesNotContain("</script><script>", script);
    }
}
```

- [ ] **Step 3: Run the test to verify it fails**

Run: `dotnet test Esatto.Umbraco.Backoffice.CookieBanner.Tests/Esatto.Umbraco.Backoffice.CookieBanner.Tests.csproj --filter FullyQualifiedName~ConsentModeScriptTests`
Expected: FAIL — build error `error CS0103: The name 'ConsentModeScript' does not exist in the current context` (four occurrences, one per call site group).

- [ ] **Step 4: Implement**

Create `Esatto.Umbraco.Backoffice.CookieBanner/src/ConsentModeScript.cs`:

```csharp
using System.Text;
using System.Text.Json;

namespace Esatto.Umbraco.Backoffice.CookieBanner;

/// <summary>
/// Builds the Google Consent Mode v2 <c>default</c>, <c>update</c> and tag <c>config</c> calls.
/// </summary>
/// <remarks>
/// The default call must run before any Google tag loads, which is why <c>&lt;consent-head /&gt;</c>
/// emits it inline in <c>&lt;head&gt;</c> rather than from <c>consent.js</c>. Emitted only when a
/// measurement id is configured — see <see cref="CookieBannerOptions.GoogleMeasurementId"/>.
/// </remarks>
public static class ConsentModeScript
{
    private const string Preamble =
        "window.dataLayer=window.dataLayer||[];function gtag(){dataLayer.push(arguments);}";

    public static string Defaults() =>
        Preamble +
        "gtag('consent','default',{" +
        "'ad_storage':'denied'," +
        "'ad_user_data':'denied'," +
        "'ad_personalization':'denied'," +
        "'analytics_storage':'denied'," +
        "'functionality_storage':'denied'," +
        "'personalization_storage':'denied'," +
        "'wait_for_update':500});";

    public static string Update(IConsentState consent)
    {
        var marketing = Signal(consent.HasGranted(ConsentCategory.Marketing));
        var statistics = Signal(consent.HasGranted(ConsentCategory.Statistics));
        var preferences = Signal(consent.HasGranted(ConsentCategory.Preferences));

        return new StringBuilder()
            .Append("gtag('consent','update',{")
            .Append($"'ad_storage':'{marketing}',")
            .Append($"'ad_user_data':'{marketing}',")
            .Append($"'ad_personalization':'{marketing}',")
            .Append($"'analytics_storage':'{statistics}',")
            .Append($"'functionality_storage':'{preferences}',")
            .Append($"'personalization_storage':'{preferences}'")
            .Append("});")
            .ToString();
    }

    /// <summary>
    /// Registers the destination and fires the initial page view. Safe to emit unconditionally
    /// alongside <see cref="Defaults"/> and <see cref="Update"/>, even before - or if never - the
    /// actual gtag.js library loads: <c>gtag()</c> only pushes onto <c>dataLayer</c>, which the
    /// <see cref="Defaults"/> preamble defines regardless of whether the library is present, so this
    /// call simply waits in the queue until (and unless) the tag loads and replays it.
    /// </summary>
    public static string Config(string measurementId) =>
        // JsonSerializer's default encoder escapes '<', '>', '&' and other HTML-sensitive characters,
        // which is what makes it safe to splice a JSON string literal into an inline <script> block
        // without a separate JavaScript/HTML encoding step.
        $"gtag('js',new Date());gtag('config',{JsonSerializer.Serialize(measurementId)});";

    private static string Signal(bool granted) => granted ? "granted" : "denied";
}
```

- [ ] **Step 5: Run the test to verify it passes**

Run: `dotnet test Esatto.Umbraco.Backoffice.CookieBanner.Tests/Esatto.Umbraco.Backoffice.CookieBanner.Tests.csproj --filter FullyQualifiedName~ConsentModeScriptTests`
Expected: PASS — 7 passed.

- [ ] **Step 6: Commit**

```bash
git add Esatto.Umbraco.Backoffice.CookieBanner/src/ConsentModeScript.cs Esatto.Umbraco.Backoffice.CookieBanner.Tests/FakeConsentState.cs Esatto.Umbraco.Backoffice.CookieBanner.Tests/ConsentModeScriptTests.cs
git commit -m "Build the Google Consent Mode v2 signals" -m "- Port ConsentModeScript unchanged; only the options reference in the docs changes
- Add the FakeConsentState test double the tag-helper tests reuse
- Pin the category -> signal mapping and the measurement-id injection guard" -m "Co-Authored-By: Claude Opus 5 (1M context) <noreply@anthropic.com>"
```

### Task 5: Package-owned request throttle

**Files:**
- Create: `c:\src\Esatto.Packages\Esatto.Umbraco.Backoffice.CookieBanner\src\ConsentThrottle.cs`
- Test: `c:\src\Esatto.Packages\Esatto.Umbraco.Backoffice.CookieBanner.Tests\ConsentThrottleTests.cs`

**Interfaces:**
- Consumes: `public sealed class CookieBannerOptions` — `int ThrottleRequestsPerMinute { get; set; } = 10;` (Task 1)
- Produces: `internal interface IConsentThrottle { bool TryAcquire(string clientKey); }`; `internal sealed class ConsentThrottle : IConsentThrottle` with ctor `ConsentThrottle(IOptions<CookieBannerOptions> options, TimeProvider timeProvider)`

Time seam: a `TimeProvider` constructor parameter. Tests pass a hand-written `MutableTimeProvider` (the contract's dependency list has no `Microsoft.Extensions.TimeProvider.Testing`); production resolves `TimeProvider.System`, registered by `AddCookieConsent()` in Task 6. The window length is fixed at one minute because the option is expressed *per minute*; only the limit is configurable.

- [ ] **Step 1: Write the failing test**

Create `Esatto.Umbraco.Backoffice.CookieBanner.Tests/ConsentThrottleTests.cs`:

```csharp
using Microsoft.Extensions.Options;
using Xunit;

namespace Esatto.Umbraco.Backoffice.CookieBanner.Tests;

public class ConsentThrottleTests
{
    private static readonly DateTimeOffset Start = new(2026, 8, 23, 12, 0, 0, TimeSpan.Zero);

    /// <summary>Injectable clock, so a window-expiry test advances time instead of sleeping.</summary>
    private sealed class MutableTimeProvider(DateTimeOffset now) : TimeProvider
    {
        public DateTimeOffset Now { get; set; } = now;

        public override DateTimeOffset GetUtcNow() => Now;
    }

    private static (ConsentThrottle Throttle, MutableTimeProvider Clock) Build(int? requestsPerMinute = null)
    {
        var settings = new CookieBannerOptions();
        if (requestsPerMinute is not null)
        {
            settings.ThrottleRequestsPerMinute = requestsPerMinute.Value;
        }

        var clock = new MutableTimeProvider(Start);
        return (new ConsentThrottle(Options.Create(settings), clock), clock);
    }

    [Fact]
    public void Allows_the_configured_number_of_requests_within_one_window()
    {
        // Pins the contract inherited from the ASP.NET Core rate limiter this replaces:
        // 10 requests per minute per client, taken from the option default.
        (ConsentThrottle throttle, _) = Build();

        for (var i = 1; i <= 10; i++)
        {
            Assert.True(throttle.TryAcquire("198.51.100.4"), $"request {i} should be allowed");
        }
    }

    [Fact]
    public void The_request_after_the_limit_is_refused()
    {
        // QueueLimit was 0 on the old fixed-window limiter: the overflow request is rejected
        // outright, never queued, so the endpoint can answer 429 immediately.
        (ConsentThrottle throttle, _) = Build(requestsPerMinute: 3);

        Assert.True(throttle.TryAcquire("198.51.100.4"));
        Assert.True(throttle.TryAcquire("198.51.100.4"));
        Assert.True(throttle.TryAcquire("198.51.100.4"));
        Assert.False(throttle.TryAcquire("198.51.100.4"));
    }

    [Fact]
    public void Each_client_key_has_its_own_budget()
    {
        // The old limiter partitioned by remote IP. One noisy visitor must not lock out the site.
        (ConsentThrottle throttle, _) = Build(requestsPerMinute: 1);

        Assert.True(throttle.TryAcquire("198.51.100.4"));
        Assert.False(throttle.TryAcquire("198.51.100.4"));
        Assert.True(throttle.TryAcquire("203.0.113.9"));
    }

    [Fact]
    public void The_budget_refreshes_once_the_window_has_passed()
    {
        // Pins that the window slides rather than being a one-shot budget: a visitor blocked at
        // 12:00 can save their choice a minute later.
        (ConsentThrottle throttle, MutableTimeProvider clock) = Build(requestsPerMinute: 1);

        Assert.True(throttle.TryAcquire("198.51.100.4"));
        Assert.False(throttle.TryAcquire("198.51.100.4"));

        clock.Now = Start.AddSeconds(61);

        Assert.True(throttle.TryAcquire("198.51.100.4"));
    }

    [Fact]
    public void A_non_positive_limit_disables_throttling()
    {
        // ThrottleRequestsPerMinute = 0 is the documented off-switch. Without this guard a
        // misconfigured site would answer 429 to every consent POST and pin the banner open.
        (ConsentThrottle throttle, _) = Build(requestsPerMinute: 0);

        for (var i = 0; i < 50; i++)
        {
            Assert.True(throttle.TryAcquire("198.51.100.4"));
        }
    }
}
```

- [ ] **Step 2: Run the test to verify it fails**

Run: `dotnet test Esatto.Umbraco.Backoffice.CookieBanner.Tests/Esatto.Umbraco.Backoffice.CookieBanner.Tests.csproj --filter FullyQualifiedName~ConsentThrottleTests`
Expected: FAIL — build error `error CS0246: The type or namespace name 'ConsentThrottle' could not be found (are you missing a using directive or an assembly reference?)` (from `ConsentThrottleTests.cs`, on the `Build` helper's `new ConsentThrottle(...)` and its return-tuple type).

- [ ] **Step 3: Implement**

Create `Esatto.Umbraco.Backoffice.CookieBanner/src/ConsentThrottle.cs`:

```csharp
using System.Collections.Concurrent;
using Microsoft.Extensions.Options;

namespace Esatto.Umbraco.Backoffice.CookieBanner;

/// <summary>Per-client request budget for the consent endpoint.</summary>
internal interface IConsentThrottle
{
    /// <summary>
    /// Consumes one request from <paramref name="clientKey"/>'s budget. False means the caller
    /// must answer HTTP 429 — nothing is queued.
    /// </summary>
    bool TryAcquire(string clientKey);
}

/// <summary>
/// In-memory sliding window, one budget per client key.
/// </summary>
/// <remarks>
/// Replaces ASP.NET Core rate limiting deliberately. The framework limiter forces a consumer to
/// place <c>UseRateLimiter()</c> between <c>UseUmbraco().WithMiddleware(...)</c> and
/// <c>.WithEndpoints(...)</c> — anyone copying a conventional Umbraco <c>Program.cs</c> gets that
/// wrong, and a missing named policy throws at request time. Owning the window here keeps the
/// package to a single <c>UseCookieConsent()</c> line while preserving the previous contract:
/// 10 requests per minute per remote IP, overflow rejected rather than queued.
/// Registered as a singleton, so <see cref="TryAcquire"/> must be thread-safe.
/// </remarks>
internal sealed class ConsentThrottle : IConsentThrottle
{
    /// <summary>
    /// The window the option is expressed in (<c>ThrottleRequestsPerMinute</c>), so only the
    /// permit count is configurable.
    /// </summary>
    private static readonly TimeSpan WindowLength = TimeSpan.FromMinutes(1);

    private readonly IOptions<CookieBannerOptions> _options;
    private readonly TimeProvider _timeProvider;
    private readonly ConcurrentDictionary<string, ClientWindow> _windows = new(StringComparer.Ordinal);
    private long _nextSweepTicks;

    public ConsentThrottle(IOptions<CookieBannerOptions> options, TimeProvider timeProvider)
    {
        _options = options;
        _timeProvider = timeProvider;
    }

    public bool TryAcquire(string clientKey)
    {
        var limit = _options.Value.ThrottleRequestsPerMinute;
        if (limit <= 0)
        {
            // Documented off-switch. Answering 429 to every POST would pin the banner open.
            return true;
        }

        DateTimeOffset now = _timeProvider.GetUtcNow();
        Sweep(now);

        return _windows
            .GetOrAdd(clientKey, static _ => new ClientWindow())
            .TryAcquire(now, WindowLength, limit);
    }

    /// <summary>
    /// Drops windows that have been idle for a full window, so a crawler cycling through
    /// addresses cannot grow the dictionary without bound. Runs at most once per window: a burst
    /// of requests must not turn into a burst of full-dictionary scans. A sweep racing an
    /// in-flight <see cref="TryAcquire"/> can at worst forget one just-recorded hit, which
    /// loosens the limit for a single request and never tightens it.
    /// </summary>
    private void Sweep(DateTimeOffset now)
    {
        var nowTicks = now.UtcTicks;
        var due = Interlocked.Read(ref _nextSweepTicks);
        if (nowTicks < due)
        {
            return;
        }

        if (Interlocked.CompareExchange(ref _nextSweepTicks, nowTicks + WindowLength.Ticks, due) != due)
        {
            return;
        }

        foreach (KeyValuePair<string, ClientWindow> pair in _windows)
        {
            if (pair.Value.IsIdle(now, WindowLength))
            {
                _windows.TryRemove(pair.Key, out _);
            }
        }
    }

    private sealed class ClientWindow
    {
        private readonly Queue<DateTimeOffset> _hits = new();
        private DateTimeOffset _lastHit;

        public bool TryAcquire(DateTimeOffset now, TimeSpan window, int limit)
        {
            lock (_hits)
            {
                while (_hits.Count > 0 && now - _hits.Peek() >= window)
                {
                    _hits.Dequeue();
                }

                if (_hits.Count >= limit)
                {
                    return false;
                }

                _hits.Enqueue(now);
                _lastHit = now;
                return true;
            }
        }

        public bool IsIdle(DateTimeOffset now, TimeSpan window)
        {
            lock (_hits)
            {
                return now - _lastHit >= window;
            }
        }
    }
}
```

- [ ] **Step 4: Run the test to verify it passes**

Run: `dotnet test Esatto.Umbraco.Backoffice.CookieBanner.Tests/Esatto.Umbraco.Backoffice.CookieBanner.Tests.csproj --filter FullyQualifiedName~ConsentThrottleTests`
Expected: PASS — 5 tests passed.

- [ ] **Step 5: Commit**

```bash
git add Esatto.Umbraco.Backoffice.CookieBanner/src/ConsentThrottle.cs Esatto.Umbraco.Backoffice.CookieBanner.Tests/ConsentThrottleTests.cs
git commit -m "Add a package-owned consent request throttle" -m "Co-Authored-By: Claude Opus 5 (1M context) <noreply@anthropic.com>"
```

---

### Task 6: Endpoint handler, minimal-API registration, middleware and wiring extensions

**Files:**
- Create: `c:\src\Esatto.Packages\Esatto.Umbraco.Backoffice.CookieBanner\src\IConsentTextProvider.cs` (interface declaration only — implementation lands in Task 8)
- Create: `c:\src\Esatto.Packages\Esatto.Umbraco.Backoffice.CookieBanner\src\ICookiePolicyPageResolver.cs` (interface declaration only — implementation lands in Task 16)
- Create: `c:\src\Esatto.Packages\Esatto.Umbraco.Backoffice.CookieBanner\src\ConsentEndpointHandler.cs`
- Create: `c:\src\Esatto.Packages\Esatto.Umbraco.Backoffice.CookieBanner\src\VaryByConsentCookieMiddleware.cs`
- Create: `c:\src\Esatto.Packages\Esatto.Umbraco.Backoffice.CookieBanner\src\ServiceCollectionExtensions.cs`
- Create: `c:\src\Esatto.Packages\Esatto.Umbraco.Backoffice.CookieBanner\src\ApplicationBuilderExtensions.cs`
- Create: `c:\src\Esatto.Packages\Esatto.Umbraco.Backoffice.CookieBanner\src\CookieBannerComposer.cs`
- Test: `c:\src\Esatto.Packages\Esatto.Umbraco.Backoffice.CookieBanner.Tests\ConsentEndpointHandlerTests.cs`
- Test: `c:\src\Esatto.Packages\Esatto.Umbraco.Backoffice.CookieBanner.Tests\VaryByConsentCookieMiddlewareTests.cs`
- Test: `c:\src\Esatto.Packages\Esatto.Umbraco.Backoffice.CookieBanner.Tests\CookieBannerServiceCollectionExtensionsTests.cs`

**Interfaces:**
- Consumes: `CookieBannerOptions.SectionName`, `.PolicyVersion`, `.EndpointPath`, `.ThrottleRequestsPerMinute` (Task 1); `enum ConsentCategory` and `static class ConsentCategories.ToWireName(ConsentCategory)` (Task 2); `internal sealed class ConsentCookieWriter(IOptions<CookieBannerOptions> options)` with `ConsentDecision Write(HttpResponse response, HttpRequest request, ConsentAction action, IEnumerable<string>? categories)` and `static bool TryParseAction(string? action, out ConsentAction parsed)`, `enum ConsentAction`, `sealed record ConsentDecision` (Task 3); `internal sealed class ConsentState : IConsentState` ctor `(IHttpContextAccessor, IOptions<CookieBannerOptions>)` (Task 3); `public interface IConsentTextProvider` and `internal interface ICookiePolicyPageResolver` are DECLARED BY THIS TASK (implementations: Task 8 and Task 16); `internal interface IConsentThrottle` / `internal sealed class ConsentThrottle(IOptions<CookieBannerOptions>, TimeProvider)` (Task 5)
- Produces:
  - `public interface IConsentTextProvider { string Get(string key); }` — **declared here** (Step 3a); implemented and registered in Task 8
  - `internal interface ICookiePolicyPageResolver { IPublishedContent? Resolve(); }` — **declared here** (Step 3a); implemented in Task 16, registered in Task 17
  - `internal sealed record ConsentRequest(string[]? Categories, string Action)`
  - `internal sealed record ConsentStateResponse(int Version, string[] Categories, string ConsentId, string DecidedAt)`
  - `internal sealed class ConsentEndpointHandler(ConsentCookieWriter cookieWriter, IConsentThrottle throttle, IOptions<CookieBannerOptions> options)` — `IResult Handle(ConsentRequest request, HttpContext context)`
  - `internal sealed class VaryByConsentCookieMiddleware(RequestDelegate next)` — `Task InvokeAsync(HttpContext context)`
  - `public static IServiceCollection AddCookieConsent(this IServiceCollection services)`
  - `public static IUmbracoBuilder AddCookieConsent(this IUmbracoBuilder builder)`
  - `public static IApplicationBuilder UseCookieConsent(this IApplicationBuilder app)`
  - `public sealed class CookieBannerComposer : IComposer`

- [ ] **Step 1: Write the failing endpoint test**

Create `Esatto.Umbraco.Backoffice.CookieBanner.Tests/ConsentEndpointHandlerTests.cs`. Ported from `NDSTK.Tests/Consent/ConsentControllerTests.cs`; the six cookie-attribute cases (`path=/`, `samesite=lax`, `httponly`, single-encoding, `secure`, expiry) live in Task 3's `ConsentCookieWriterTests` and are NOT repeated here.

```csharp
using System.Net;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Http.HttpResults;
using Microsoft.Extensions.Options;
using Xunit;

namespace Esatto.Umbraco.Backoffice.CookieBanner.Tests;

public class ConsentEndpointHandlerTests
{
    private static ConsentEndpointHandler Build(int policyVersion = 1, int throttleRequestsPerMinute = 10)
    {
        IOptions<CookieBannerOptions> options = Options.Create(new CookieBannerOptions
        {
            PolicyVersion = policyVersion,
            ThrottleRequestsPerMinute = throttleRequestsPerMinute,
        });

        return new ConsentEndpointHandler(
            new ConsentCookieWriter(options),
            new ConsentThrottle(options, TimeProvider.System),
            options);
    }

    private static DefaultHttpContext NewContext()
    {
        var context = new DefaultHttpContext();
        context.Connection.RemoteIpAddress = IPAddress.Parse("198.51.100.4");
        return context;
    }

    private static ConsentStateResponse Ok(IResult result)
        => Assert.IsType<Ok<ConsentStateResponse>>(result).Value!;

    [Fact]
    public void Accepting_returns_the_stored_state_and_writes_the_cookie()
    {
        // Pins the response shape consent.js reads back (version + categories + id) and that the
        // endpoint really writes a cookie rather than trusting the browser to.
        ConsentEndpointHandler handler = Build();
        DefaultHttpContext context = NewContext();

        IResult result = handler.Handle(new ConsentRequest(["statistics", "marketing"], "accept-all"), context);

        ConsentStateResponse response = Ok(result);
        Assert.Equal(1, response.Version);
        Assert.Equal(["marketing", "statistics"], response.Categories);
        Assert.NotEmpty(response.ConsentId);
        Assert.NotEmpty(context.Response.Headers.SetCookie.ToString());
    }

    [Fact]
    public void Rejecting_stores_no_categories()
    {
        // Reject-all must produce an empty grant set, never a silent accept-all.
        ConsentEndpointHandler handler = Build();

        IResult result = handler.Handle(new ConsentRequest([], "reject-all"), NewContext());

        Assert.Empty(Ok(result).Categories);
    }

    [Fact]
    public void Unknown_categories_are_discarded_rather_than_trusted()
    {
        // The body is untrusted: an invented category is dropped and "necessary" is never echoed
        // back as a granted choice.
        ConsentEndpointHandler handler = Build();

        IResult result = handler.Handle(
            new ConsentRequest(["statistics", "telepathy", "necessary"], "custom"),
            NewContext());

        Assert.Equal(["statistics"], Ok(result).Categories);
    }

    [Fact]
    public void An_unknown_action_is_rejected()
    {
        // An unrecognised action is a hard 400, so a typo in the client cannot write a cookie
        // whose provenance nobody can explain.
        ConsentEndpointHandler handler = Build();

        IResult result = handler.Handle(
            new ConsentRequest([], "definitely-not-an-action"),
            NewContext());

        Assert.IsType<BadRequest<string>>(result);
    }

    [Fact]
    public void The_response_records_the_current_policy_version()
    {
        // PolicyVersion comes from options, not a constant: bumping it is what re-prompts visitors.
        ConsentEndpointHandler handler = Build(policyVersion: 7);

        IResult result = handler.Handle(new ConsentRequest([], "reject-all"), NewContext());

        Assert.Equal(7, Ok(result).Version);
    }

    [Fact]
    public void Requests_beyond_the_throttle_budget_get_429()
    {
        // Preserves the status code the removed ASP.NET Core rate limiter returned, now without
        // requiring UseRateLimiter() to be threaded through the consumer's Umbraco pipeline.
        ConsentEndpointHandler handler = Build(throttleRequestsPerMinute: 1);

        Assert.IsType<Ok<ConsentStateResponse>>(handler.Handle(new ConsentRequest([], "reject-all"), NewContext()));

        IResult second = handler.Handle(new ConsentRequest([], "reject-all"), NewContext());

        Assert.Equal(
            StatusCodes.Status429TooManyRequests,
            Assert.IsType<StatusCodeHttpResult>(second).StatusCode);
    }

    [Fact]
    public void The_throttle_is_consulted_before_the_action_is_validated()
    {
        // Order matters: a flood of invalid actions must consume budget too, otherwise the cheap
        // rejection path is an unmetered way to hammer the endpoint.
        ConsentEndpointHandler handler = Build(throttleRequestsPerMinute: 1);

        Assert.IsType<BadRequest<string>>(handler.Handle(new ConsentRequest([], "nonsense"), NewContext()));

        IResult second = handler.Handle(new ConsentRequest([], "reject-all"), NewContext());

        Assert.Equal(
            StatusCodes.Status429TooManyRequests,
            Assert.IsType<StatusCodeHttpResult>(second).StatusCode);
    }
}
```

- [ ] **Step 2: Run the endpoint test to verify it fails**

Run: `dotnet test Esatto.Umbraco.Backoffice.CookieBanner.Tests/Esatto.Umbraco.Backoffice.CookieBanner.Tests.csproj --filter FullyQualifiedName~ConsentEndpointHandlerTests`
Expected: FAIL — build errors `error CS0246: The type or namespace name 'ConsentEndpointHandler' could not be found (are you missing a using directive or an assembly reference?)`, plus the same CS0246 for `ConsentRequest` and `ConsentStateResponse`.

- [ ] **Step 3a: Declare the two request-scoped service interfaces**

These two interfaces are declared here because Task 6 wires the DI container and Tasks 7 and 10
consume them, but their implementations arrive later (Task 8 and Task 16). Declaring the contract
early is what lets those tasks compile in numeric order.

Create `src/IConsentTextProvider.cs`:

```csharp
namespace Esatto.Umbraco.Backoffice.CookieBanner;

/// <summary>
/// Resolves a piece of consent copy by dictionary key.
/// </summary>
/// <remarks>
/// PUBLIC, deliberately. <c>ConsentEmbedTagHelper</c> is <c>public sealed</c> with a DI-activated
/// public constructor, and a public constructor cannot declare an internal parameter type
/// (CS0051). The implementation (Task 8) stays internal.
/// </remarks>
public interface IConsentTextProvider
{
    /// <summary>Dictionary item, else the embedded resx for the request culture, else English.</summary>
    string Get(string key);
}
```

Create `src/ICookiePolicyPageResolver.cs`:

```csharp
using Umbraco.Cms.Core.Models.PublishedContent;

namespace Esatto.Umbraco.Backoffice.CookieBanner;

/// <summary>
/// Finds the site's cookie policy page. Internal: consumers configure it through
/// <see cref="CookieBannerOptions.PolicyPageKey"/> rather than by implementing this.
/// </summary>
internal interface ICookiePolicyPageResolver
{
    /// <summary>The policy page, or <c>null</c> when the site has none published.</summary>
    IPublishedContent? Resolve();
}
```

- [ ] **Step 3: Confirm the two endpoint DTOs already exist**

> **Task 3 created `src/ConsentRequest.cs` and `src/ConsentStateResponse.cs`, with their own tests
> (`ConsentRequestTests`, `ConsentStateResponseTests` — the latter pins camelCase JSON, which
> `consent.js` depends on). Do NOT recreate them. Read both files, confirm they match the shapes
> below, and move on. If either differs, report it rather than silently rewriting it.**

Verify the existing `Esatto.Umbraco.Backoffice.CookieBanner/src/ConsentRequest.cs` matches:

```csharp
namespace Esatto.Umbraco.Backoffice.CookieBanner;

/// <summary>
/// Body of the consent POST. Every value is untrusted and validated server-side.
/// </summary>
/// <remarks>
/// <c>Action</c> is declared non-nullable but arrives from JSON, so a body that omits it yields
/// null at runtime — <c>ConsentCookieWriter.TryParseAction</c> then rejects it, which is the
/// intended 400. The old <c>Culture</c> field is gone: it fed a consent log that was never built.
/// </remarks>
internal sealed record ConsentRequest(string[]? Categories, string Action);
```

Verify the existing `Esatto.Umbraco.Backoffice.CookieBanner/src/ConsentStateResponse.cs` matches:

```csharp
namespace Esatto.Umbraco.Backoffice.CookieBanner;

/// <summary>
/// Canonical consent state after a decision. The banner uses this to unblock scripts without a
/// reload, so it must reflect what the server actually stored, not what the client asked for.
/// </summary>
internal sealed record ConsentStateResponse(
    int Version,
    string[] Categories,
    string ConsentId,
    string DecidedAt);
```

- [ ] **Step 4: Implement the endpoint handler**

Create `Esatto.Umbraco.Backoffice.CookieBanner/src/ConsentEndpointHandler.cs`:

```csharp
using System.Linq;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Options;

namespace Esatto.Umbraco.Backoffice.CookieBanner;

/// <summary>
/// The consent endpoint's request handling, extracted from the controller it replaces so it is
/// unit-testable without routing.
/// </summary>
/// <remarks>
/// Registered as a minimal-API <c>MapPost</c> by <c>UseCookieConsent()</c>. Attribute-routed
/// front-end API controllers are not a forward-compatible shape: <c>UmbracoApiController</c> and
/// convention-based front-end API routing were both removed in Umbraco 18.
/// </remarks>
internal sealed class ConsentEndpointHandler(
    ConsentCookieWriter cookieWriter,
    IConsentThrottle throttle,
    IOptions<CookieBannerOptions> options)
{
    public IResult Handle(ConsentRequest request, HttpContext context)
    {
        // Metered before the body is inspected, so cheap rejections cost budget too.
        if (options.Value.ThrottleRequestsPerMinute > 0
            && throttle.TryAcquire(ClientKey(context)) is false)
        {
            return TypedResults.StatusCode(StatusCodes.Status429TooManyRequests);
        }

        if (ConsentCookieWriter.TryParseAction(request.Action, out ConsentAction action) is false)
        {
            return TypedResults.BadRequest("Unknown consent action.");
        }

        ConsentDecision decision = cookieWriter.Write(
            context.Response,
            context.Request,
            action,
            request.Categories);

        return TypedResults.Ok(new ConsentStateResponse(
            decision.PolicyVersion,
            decision.Granted.Select(ConsentCategories.ToWireName).Order(StringComparer.Ordinal).ToArray(),
            decision.ConsentId,
            decision.DecidedAt.ToString("O")));
    }

    /// <summary>
    /// Partition key for the throttle: the remote IP, matching the fixed-window limiter this
    /// replaces. Unknown addresses share one bucket rather than escaping the limit entirely.
    /// </summary>
    private static string ClientKey(HttpContext context)
        => context.Connection.RemoteIpAddress?.ToString() ?? "unknown";
}
```

- [ ] **Step 5: Run the endpoint test to verify it passes**

Run: `dotnet test Esatto.Umbraco.Backoffice.CookieBanner.Tests/Esatto.Umbraco.Backoffice.CookieBanner.Tests.csproj --filter FullyQualifiedName~ConsentEndpointHandlerTests`
Expected: PASS — 7 tests passed.

- [ ] **Step 6: Commit the handler**

```bash
git add Esatto.Umbraco.Backoffice.CookieBanner/src/ConsentRequest.cs Esatto.Umbraco.Backoffice.CookieBanner/src/ConsentStateResponse.cs Esatto.Umbraco.Backoffice.CookieBanner/src/ConsentEndpointHandler.cs Esatto.Umbraco.Backoffice.CookieBanner.Tests/ConsentEndpointHandlerTests.cs
git commit -m "Move consent endpoint handling into a testable handler" -m "Co-Authored-By: Claude Opus 5 (1M context) <noreply@anthropic.com>"
```

- [ ] **Step 7: Write the failing middleware test**

Create `Esatto.Umbraco.Backoffice.CookieBanner.Tests/VaryByConsentCookieMiddlewareTests.cs`. The middleware writes its headers inside `Response.OnStarting`, and `DefaultHttpContext`'s built-in `HttpResponseFeature.OnStarting` is a no-op — so the test swaps in a decorator that captures the callbacks and fires them after the pipeline has set the content type, exactly as a real server would.

```csharp
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Http.Features;
using Xunit;

namespace Esatto.Umbraco.Backoffice.CookieBanner.Tests;

public class VaryByConsentCookieMiddlewareTests
{
    /// <summary>
    /// DefaultHttpContext's built-in response feature silently no-ops OnStarting, so the
    /// middleware's callback would never run and every assertion would pass vacuously. This
    /// decorator records the callbacks and delegates status/headers/body to the real feature, so
    /// header writes still land on context.Response.Headers.
    /// </summary>
    private sealed class CallbackCapturingResponseFeature(IHttpResponseFeature inner) : IHttpResponseFeature
    {
        private readonly List<Func<Task>> _onStarting = [];

        public int StatusCode { get => inner.StatusCode; set => inner.StatusCode = value; }

        public string? ReasonPhrase { get => inner.ReasonPhrase; set => inner.ReasonPhrase = value; }

        public IHeaderDictionary Headers { get => inner.Headers; set => inner.Headers = value; }

        public Stream Body { get => inner.Body; set => inner.Body = value; }

        public bool HasStarted => inner.HasStarted;

        public int RegisteredCallbacks => _onStarting.Count;

        public void OnStarting(Func<object, Task> callback, object state)
            => _onStarting.Add(() => callback(state));

        public void OnCompleted(Func<object, Task> callback, object state)
        {
        }

        public async Task FireOnStartingAsync()
        {
            foreach (Func<Task> callback in _onStarting)
            {
                await callback();
            }
        }
    }

    private sealed record Invocation(
        DefaultHttpContext Context,
        CallbackCapturingResponseFeature Feature,
        int NextInvocations);

    private static async Task<Invocation> InvokeAsync(string path, string? contentType)
    {
        var context = new DefaultHttpContext();
        context.Request.Path = path;

        var feature = new CallbackCapturingResponseFeature(context.Features.Get<IHttpResponseFeature>()!);
        context.Features.Set<IHttpResponseFeature>(feature);

        var nextInvocations = 0;
        var middleware = new VaryByConsentCookieMiddleware(inner =>
        {
            nextInvocations++;
            if (contentType is not null)
            {
                inner.Response.ContentType = contentType;
            }

            return Task.CompletedTask;
        });

        await middleware.InvokeAsync(context);

        // The server fires these when the response is about to start, i.e. after the pipeline
        // above has settled the content type.
        await feature.FireOnStartingAsync();

        return new Invocation(context, feature, nextInvocations);
    }

    [Fact]
    public async Task Front_end_html_is_marked_private_and_varying_by_the_consent_cookie()
    {
        // Consent-gated markup (the banner, the gated Google tag) is baked in server-side. Without
        // these headers a shared cache could serve one visitor's consent state to another.
        Invocation invocation = await InvokeAsync("/about", "text/html; charset=utf-8");

        Assert.Equal("Cookie", invocation.Context.Response.Headers.Vary.ToString());
        Assert.Equal("private, no-cache", invocation.Context.Response.Headers.CacheControl.ToString());
    }

    [Fact]
    public async Task Json_responses_are_left_untouched()
    {
        // Scoped to text/html on purpose: API and static-asset responses must keep whatever
        // caching the host chose for them.
        Invocation invocation = await InvokeAsync("/api/cookie-consent", "application/json");

        Assert.Empty(invocation.Context.Response.Headers.Vary.ToString());
        Assert.Empty(invocation.Context.Response.Headers.CacheControl.ToString());
    }

    [Fact]
    public async Task Backoffice_html_is_left_untouched()
    {
        // /umbraco is excluded by path, and excluded before the callback is even registered, so
        // the backoffice pays nothing for a front-end concern.
        Invocation invocation = await InvokeAsync("/umbraco/section/content", "text/html; charset=utf-8");

        Assert.Equal(0, invocation.Feature.RegisteredCallbacks);
        Assert.Empty(invocation.Context.Response.Headers.Vary.ToString());
        Assert.Empty(invocation.Context.Response.Headers.CacheControl.ToString());
    }

    [Theory]
    [InlineData("/")]
    [InlineData("/umbraco/section/content")]
    public async Task Next_is_always_invoked(string path)
    {
        // The middleware only annotates; it must never terminate a request on any path.
        Invocation invocation = await InvokeAsync(path, "text/html");

        Assert.Equal(1, invocation.NextInvocations);
    }
}
```

- [ ] **Step 8: Run the middleware test to verify it fails**

Run: `dotnet test Esatto.Umbraco.Backoffice.CookieBanner.Tests/Esatto.Umbraco.Backoffice.CookieBanner.Tests.csproj --filter FullyQualifiedName~VaryByConsentCookieMiddlewareTests`
Expected: FAIL — build error `error CS0246: The type or namespace name 'VaryByConsentCookieMiddleware' could not be found (are you missing a using directive or an assembly reference?)` in `InvokeAsync`.

- [ ] **Step 9: Implement the middleware**

Create `Esatto.Umbraco.Backoffice.CookieBanner/src/VaryByConsentCookieMiddleware.cs`:

```csharp
using Microsoft.AspNetCore.Http;

namespace Esatto.Umbraco.Backoffice.CookieBanner;

/// <summary>
/// Marks front-end HTML responses as private and varying by the consent cookie.
/// </summary>
/// <remarks>
/// The consent dialog, and any consent-gated <c>&lt;script&gt;</c> or embed such as the Google
/// tag, are baked into server-rendered markup based on the visitor's consent cookie. The moment
/// any shared cache — a CDN, a reverse proxy, an edge network — handles that markup, one
/// visitor's consent state, including a third-party analytics tag, could be served to another.
/// Scoped to <c>text/html</c> responses outside <c>/umbraco</c>: static assets and API responses
/// never carry <c>text/html</c>, and the backoffice is excluded by path, so neither is affected.
/// Registered by <c>UseCookieConsent()</c>, which the consumer calls before <c>UseUmbraco()</c> so
/// this <c>OnStarting</c> callback is queued before anything downstream starts writing the body.
/// </remarks>
internal sealed class VaryByConsentCookieMiddleware(RequestDelegate next)
{
    public async Task InvokeAsync(HttpContext context)
    {
        if (context.Request.Path.StartsWithSegments("/umbraco") is false)
        {
            context.Response.OnStarting(() =>
            {
                if (context.Response.ContentType?.Contains("text/html", StringComparison.OrdinalIgnoreCase) is true)
                {
                    context.Response.Headers.Vary = "Cookie";
                    context.Response.Headers.CacheControl = "private, no-cache";
                }

                return Task.CompletedTask;
            });
        }

        await next(context);
    }
}
```

- [ ] **Step 10: Run the middleware test to verify it passes**

Run: `dotnet test Esatto.Umbraco.Backoffice.CookieBanner.Tests/Esatto.Umbraco.Backoffice.CookieBanner.Tests.csproj --filter FullyQualifiedName~VaryByConsentCookieMiddlewareTests`
Expected: PASS — 5 tests passed (3 facts + 2 theory cases).

- [ ] **Step 11: Commit the middleware**

```bash
git add Esatto.Umbraco.Backoffice.CookieBanner/src/VaryByConsentCookieMiddleware.cs Esatto.Umbraco.Backoffice.CookieBanner.Tests/VaryByConsentCookieMiddlewareTests.cs
git commit -m "Add the vary-by-consent-cookie middleware with its first tests" -m "Co-Authored-By: Claude Opus 5 (1M context) <noreply@anthropic.com>"
```

- [ ] **Step 12: Write the failing registration test**

Create `Esatto.Umbraco.Backoffice.CookieBanner.Tests/CookieBannerServiceCollectionExtensionsTests.cs`:

```csharp
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;
using Xunit;

namespace Esatto.Umbraco.Backoffice.CookieBanner.Tests;

public class CookieBannerServiceCollectionExtensionsTests
{
    [Fact]
    public void Registering_twice_leaves_one_registration_per_service()
    {
        // CookieBannerComposer calls AddCookieConsent() automatically, and the public
        // AddCookieConsent() is documented as safe to call as well. Only TryAdd* keeps that
        // idempotent — plain Add* would give ConsentThrottle two singletons and two budgets.
        var services = new ServiceCollection();

        services.AddCookieConsent();
        services.AddCookieConsent();

        Assert.Equal(1, services.Count(d => d.ServiceType == typeof(IConsentState)));
        Assert.Equal(1, services.Count(d => d.ServiceType == typeof(ConsentCookieWriter)));
        Assert.Equal(1, services.Count(d => d.ServiceType == typeof(IConsentThrottle)));
        Assert.Equal(1, services.Count(d => d.ServiceType == typeof(ConsentEndpointHandler)));
    }

    [Fact]
    public void The_consent_graph_resolves_from_the_container()
    {
        // Pins the lifetimes: ConsentState is scoped, so validateScopes catches a singleton that
        // captures it, and the throttle's TimeProvider dependency must be registered too.
        var services = new ServiceCollection();
        services.AddSingleton<IConfiguration>(new ConfigurationBuilder().Build());
        services.AddCookieConsent();

        using ServiceProvider provider = services.BuildServiceProvider(validateScopes: true);

        Assert.NotNull(provider.GetRequiredService<ConsentCookieWriter>());
        Assert.NotNull(provider.GetRequiredService<IConsentThrottle>());
        Assert.NotNull(provider.GetRequiredService<ConsentEndpointHandler>());

        using IServiceScope scope = provider.CreateScope();
        Assert.NotNull(scope.ServiceProvider.GetRequiredService<IConsentState>());
    }

    [Fact]
    public void An_absent_configuration_section_leaves_the_defaults_intact()
    {
        // BindConfiguration against a missing "Esatto:CookieBanner" section must not blank the
        // options: a consumer with no appsettings entry still gets a working endpoint path.
        var services = new ServiceCollection();
        services.AddSingleton<IConfiguration>(new ConfigurationBuilder().Build());
        services.AddCookieConsent();

        using ServiceProvider provider = services.BuildServiceProvider(validateScopes: true);
        CookieBannerOptions options = provider.GetRequiredService<IOptions<CookieBannerOptions>>().Value;

        Assert.Equal("/api/cookie-consent", options.EndpointPath);
        Assert.Equal(10, options.ThrottleRequestsPerMinute);
    }
}
```

- [ ] **Step 13: Run the registration test to verify it fails**

Run: `dotnet test Esatto.Umbraco.Backoffice.CookieBanner.Tests/Esatto.Umbraco.Backoffice.CookieBanner.Tests.csproj --filter FullyQualifiedName~CookieBannerServiceCollectionExtensionsTests`
Expected: FAIL — build error `error CS1061: 'IServiceCollection' does not contain a definition for 'AddCookieConsent' and no accessible extension method 'AddCookieConsent' accepting a first argument of type 'IServiceCollection' could be found`.

- [ ] **Step 14: Implement the service-registration extensions**

Create `Esatto.Umbraco.Backoffice.CookieBanner/src/ServiceCollectionExtensions.cs`:

```csharp
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Umbraco.Cms.Core.DependencyInjection;

namespace Esatto.Umbraco.Backoffice.CookieBanner;

public static class CookieBannerServiceCollectionExtensions
{
    /// <summary>
    /// Registers the cookie-consent service graph. Optional — <see cref="CookieBannerComposer"/>
    /// already calls this automatically. Kept for explicitness; idempotent.
    /// </summary>
    public static IServiceCollection AddCookieConsent(this IServiceCollection services)
    {
        // ConsentState reads the cookie off the ambient request.
        services.AddHttpContextAccessor();

        services.AddOptions<CookieBannerOptions>()
            .BindConfiguration(CookieBannerOptions.SectionName);

        // ConsentThrottle's injectable clock. Not registered by the host by default.
        services.TryAddSingleton(TimeProvider.System);

        // Scoped: the cookie is parsed at most once per request however many tag helpers ask.
        services.TryAddScoped<IConsentState, ConsentState>();

        services.TryAddSingleton<ConsentCookieWriter>();

        // Singleton, or every request would get a fresh window and no throttle at all.
        services.TryAddSingleton<IConsentThrottle, ConsentThrottle>();

        services.TryAddSingleton<ConsentEndpointHandler>();

        // IConsentTextProvider and ICookiePolicyPageResolver are declared here but registered by
        // the tasks that implement them (Task 8 and Task 17 respectively) - their concrete types
        // do not exist yet, so registering them here would not compile. Both are scoped: each
        // resolves against the current request's culture or published content.

        return services;
    }
}

public static class CookieBannerUmbracoBuilderExtensions
{
    /// <summary>
    /// Registers the cookie-consent service graph on an <see cref="IUmbracoBuilder"/>. Optional
    /// for the same reason as the <see cref="IServiceCollection"/> overload.
    /// </summary>
    public static IUmbracoBuilder AddCookieConsent(this IUmbracoBuilder builder)
    {
        builder.Services.AddCookieConsent();
        return builder;
    }
}
```

- [ ] **Step 15: Run the registration test to verify it passes**

Run: `dotnet test Esatto.Umbraco.Backoffice.CookieBanner.Tests/Esatto.Umbraco.Backoffice.CookieBanner.Tests.csproj --filter FullyQualifiedName~CookieBannerServiceCollectionExtensionsTests`
Expected: PASS — 3 tests passed.

- [ ] **Step 16: Implement `UseCookieConsent()` with the minimal-API endpoint**

Create `Esatto.Umbraco.Backoffice.CookieBanner/src/ApplicationBuilderExtensions.cs`:

```csharp
using System.Text.Json;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;

namespace Esatto.Umbraco.Backoffice.CookieBanner;

public static class CookieBannerApplicationBuilderExtensions
{
    /// <summary>
    /// Adds the cookie-consent middleware and maps the consent endpoint at
    /// <see cref="CookieBannerOptions.EndpointPath"/>. Call after <c>BootUmbracoAsync()</c> and
    /// before <c>UseUmbraco()</c>.
    /// </summary>
    /// <remarks>
    /// This is the whole integration surface. The endpoint is a minimal-API <c>MapPost</c> rather
    /// than an attribute-routed controller, so no <c>MapControllers()</c> is required and nothing
    /// depends on front-end API routing (removed in Umbraco 18). Throttling is package-owned, so
    /// there is no <c>AddRateLimiter</c> and no <c>UseRateLimiter()</c> to wedge between
    /// <c>WithMiddleware(...)</c> and <c>WithEndpoints(...)</c>.
    /// The body is read explicitly instead of being model-bound, which keeps the internal
    /// <see cref="ConsentRequest"/> and <see cref="ConsentEndpointHandler"/> out of minimal-API
    /// parameter inference altogether.
    /// </remarks>
    public static IApplicationBuilder UseCookieConsent(this IApplicationBuilder app)
    {
        app.UseMiddleware<VaryByConsentCookieMiddleware>();

        if (app is not IEndpointRouteBuilder endpoints)
        {
            throw new InvalidOperationException(
                "UseCookieConsent() must be called on a WebApplication (or another "
                + "IApplicationBuilder that is also an IEndpointRouteBuilder) so the consent "
                + "endpoint can be mapped.");
        }

        var endpointPath = app.ApplicationServices
            .GetRequiredService<IOptions<CookieBannerOptions>>()
            .Value.EndpointPath;

        endpoints.MapPost(endpointPath, async (HttpContext context) =>
        {
            ConsentRequest? request;
            try
            {
                request = await context.Request.ReadFromJsonAsync<ConsentRequest>();
            }
            catch (JsonException)
            {
                return Results.BadRequest("Malformed consent request.");
            }

            if (request is null)
            {
                return Results.BadRequest("Missing consent request.");
            }

            ConsentEndpointHandler handler = context.RequestServices
                .GetRequiredService<ConsentEndpointHandler>();

            return handler.Handle(request, context);
        })
        // A visitor-facing endpoint: it must answer before anyone is authenticated, whatever
        // fallback authorization policy the host has configured.
        .AllowAnonymous();

        return app;
    }
}
```

- [ ] **Step 17: Implement the composer**

Create `Esatto.Umbraco.Backoffice.CookieBanner/src/CookieBannerComposer.cs`:

```csharp
using Microsoft.Extensions.DependencyInjection;
using Umbraco.Cms.Core.Composing;
using Umbraco.Cms.Core.DependencyInjection;

namespace Esatto.Umbraco.Backoffice.CookieBanner;

/// <summary>
/// Wires Esatto.Umbraco.Backoffice.CookieBanner into Umbraco's container.
/// </summary>
/// <remarks>
/// Composers are auto-discovered by Umbraco from any referenced assembly, so the service graph is
/// registered with no consumer-side wiring: a consumer's only code change is
/// <c>app.UseCookieConsent()</c> plus the two tag helpers in their layout. The registrations use
/// <c>TryAdd*</c>, so calling <c>AddCookieConsent()</c> explicitly as well is harmless.
/// </remarks>
public sealed class CookieBannerComposer : IComposer
{
    public void Compose(IUmbracoBuilder builder) => builder.Services.AddCookieConsent();
}
```

- [ ] **Step 18: Run the whole test project to verify nothing regressed**

Run: `dotnet test Esatto.Umbraco.Backoffice.CookieBanner.Tests/Esatto.Umbraco.Backoffice.CookieBanner.Tests.csproj`
Expected: PASS — the full suite builds (which also compiles `ApplicationBuilderExtensions.cs` and `CookieBannerComposer.cs`) and every test passes, including the 15 added by steps 1–15.

- [ ] **Step 19: Commit the wiring**

```bash
git add Esatto.Umbraco.Backoffice.CookieBanner/src/ServiceCollectionExtensions.cs Esatto.Umbraco.Backoffice.CookieBanner/src/ApplicationBuilderExtensions.cs Esatto.Umbraco.Backoffice.CookieBanner/src/CookieBannerComposer.cs Esatto.Umbraco.Backoffice.CookieBanner.Tests/CookieBannerServiceCollectionExtensionsTests.cs
git commit -m "Wire the cookie banner up through a composer and two extension methods" -m "Co-Authored-By: Claude Opus 5 (1M context) <noreply@anthropic.com>"
```

### Task 7: The consent-script and consent-embed tag helpers

> **Ordering is fine as numbered.** `ConsentEmbedTagHelper` here consumes `IConsentTextProvider`, whose interface was declared in Task 6; Task 8 supplies the implementation and registers it. Use NSubstitute for the interface in this task's tests.

**Files:**
- Create: `Esatto.Umbraco.Backoffice.CookieBanner/src/TagHelpers/ConsentScriptTagHelper.cs`
- Create: `Esatto.Umbraco.Backoffice.CookieBanner/src/TagHelpers/ConsentEmbedTagHelper.cs`
- Test: `Esatto.Umbraco.Backoffice.CookieBanner.Tests/ConsentScriptTagHelperTests.cs`
- Test: `Esatto.Umbraco.Backoffice.CookieBanner.Tests/ConsentEmbedTagHelperTests.cs`

**Interfaces:**
- **ORDERING: Task 8 must land before this task.** `IConsentTextProvider` is introduced in Task 8; `ConsentEmbedTagHelper` cannot compile without it. If the runner works strictly in numeric order, swap 7 and 8.
- Consumes: `public enum ConsentCategory { Necessary, Preferences, Statistics, Marketing }`
- Consumes: `public static string ConsentCategories.ToWireName(ConsentCategory category)`
- Consumes: `public interface IConsentState { ConsentDecision? Decision { get; } bool NeedsDecision { get; } bool HasGranted(ConsentCategory category); }`
- Consumes: `public interface IConsentTextProvider { string Get(string key); }` — **defined in Task 8**
- Consumes: `internal sealed class FakeConsentState(params ConsentCategory[] granted) : IConsentState` with `public bool NeedsDecision { get; init; }` (shared test helper)
- Produces: `public sealed class ConsentScriptTagHelper(IConsentState consent) : TagHelper` — `[HtmlTargetElement("consent-script")]`, `public ConsentCategory Category { get; set; }`, `public string? Src { get; set; }`, `public bool Async { get; set; }`
- Produces: `public sealed class ConsentEmbedTagHelper(IConsentState consent, IConsentTextProvider text) : TagHelper` — `[HtmlTargetElement("consent-embed", TagStructure = TagStructure.WithoutEndTag)]`, `public ConsentCategory Category { get; set; }`, `public string? Src { get; set; }`, `public string? Title { get; set; }`
- Produces (for the DI/composer task): tag helpers are activated from the container, so `IConsentState` and `IConsentTextProvider` must both be registered by `AddCookieConsent()`.
- Produces (for the CSS task): the blocked placeholder emits `class="consent-btn consent-btn--primary"` and `data-consent-open`; the wrapper emits `class="consent-embed consent-embed--blocked"` and `data-consent-category="<wire name>"`.

- [ ] **Step 1: Write the failing test for the script tag helper**

Create `Esatto.Umbraco.Backoffice.CookieBanner.Tests/ConsentScriptTagHelperTests.cs`:

```csharp
using Microsoft.AspNetCore.Razor.TagHelpers;
using Esatto.Umbraco.Backoffice.CookieBanner.TagHelpers;
using Xunit;

namespace Esatto.Umbraco.Backoffice.CookieBanner.Tests;

public class ConsentScriptTagHelperTests
{
    private static TagHelperContext Context() => new(
        new TagHelperAttributeList(),
        new Dictionary<object, object>(),
        Guid.NewGuid().ToString());

    private static TagHelperOutput Output() => new(
        "consent-script",
        new TagHelperAttributeList(),
        (_, _) => Task.FromResult<TagHelperContent>(new DefaultTagHelperContent()));

    // Pins the core guarantee: an ungated script is suppressed server-side, so there is no
    // window in which the browser could execute it before a choice is made.
    [Fact]
    public void Emits_nothing_at_all_when_the_category_is_not_granted()
    {
        var helper = new ConsentScriptTagHelper(new FakeConsentState())
        {
            Category = ConsentCategory.Statistics,
            Src = "https://example.test/a.js",
        };
        TagHelperOutput output = Output();

        helper.Process(Context(), output);

        Assert.True(output.IsContentModified);
        Assert.Null(output.TagName);
        Assert.Empty(output.Content.GetContent());
    }

    // Pins that a granted category produces a real <script src> with the minimized async attribute.
    [Fact]
    public void Emits_a_script_tag_when_granted()
    {
        var helper = new ConsentScriptTagHelper(new FakeConsentState(ConsentCategory.Statistics))
        {
            Category = ConsentCategory.Statistics,
            Src = "https://example.test/a.js",
            Async = true,
        };
        TagHelperOutput output = Output();

        helper.Process(Context(), output);

        Assert.Equal("script", output.TagName);
        Assert.Equal(TagMode.StartTagAndEndTag, output.TagMode);
        Assert.Equal("https://example.test/a.js", output.Attributes["src"].Value);
        Assert.True(output.Attributes.ContainsName("async"));
    }

    // Pins that async is opt-in: a synchronous script must not silently become async.
    [Fact]
    public void Omits_async_when_not_requested()
    {
        var helper = new ConsentScriptTagHelper(new FakeConsentState(ConsentCategory.Marketing))
        {
            Category = ConsentCategory.Marketing,
            Src = "https://example.test/a.js",
        };
        TagHelperOutput output = Output();

        helper.Process(Context(), output);

        Assert.False(output.Attributes.ContainsName("async"));
    }

    // Pins that the package's own consent.js still loads for a visitor who has decided nothing.
    [Fact]
    public void Necessary_scripts_are_always_emitted()
    {
        var helper = new ConsentScriptTagHelper(new FakeConsentState())
        {
            Category = ConsentCategory.Necessary,
            Src = "/esatto-cookiebanner/consent.js",
        };
        TagHelperOutput output = Output();

        helper.Process(Context(), output);

        Assert.Equal("script", output.TagName);
    }

    // Pins the PolicyVersion re-prompt regression: a decision against older text grants nothing.
    [Fact]
    public void A_stale_decision_suppresses_the_script()
    {
        var helper = new ConsentScriptTagHelper(
            new FakeConsentState(ConsentCategory.Statistics) { NeedsDecision = true })
        {
            Category = ConsentCategory.Statistics,
            Src = "https://example.test/a.js",
        };
        TagHelperOutput output = Output();

        helper.Process(Context(), output);

        Assert.Null(output.TagName);
    }
}
```

- [ ] **Step 2: Run the test to verify it fails**

Run: `dotnet test Esatto.Umbraco.Backoffice.CookieBanner.Tests/Esatto.Umbraco.Backoffice.CookieBanner.Tests.csproj --filter FullyQualifiedName~ConsentScriptTagHelperTests`
Expected: FAIL with a build error, not a test failure:
```
ConsentScriptTagHelperTests.cs(2,49): error CS0234: The type or namespace name 'TagHelpers' does not exist in the namespace 'Esatto.Umbraco.Backoffice.CookieBanner' (are you missing an assembly reference?)
ConsentScriptTagHelperTests.cs(24,26): error CS0246: The type or namespace name 'ConsentScriptTagHelper' could not be found (are you missing a using directive or an assembly reference?)
The build failed. Fix the build errors and run again.
```

- [ ] **Step 3: Implement the script tag helper**

Create `Esatto.Umbraco.Backoffice.CookieBanner/src/TagHelpers/ConsentScriptTagHelper.cs`:

```csharp
using Microsoft.AspNetCore.Razor.TagHelpers;

namespace Esatto.Umbraco.Backoffice.CookieBanner.TagHelpers;

/// <summary>
/// Emits a <c>&lt;script&gt;</c> only when the visitor has granted the given category.
/// </summary>
/// <remarks>
/// This is the primary gating mechanism and the reason the "no consenting cookies before a choice"
/// guarantee holds without a race: when consent is absent the tag never reaches the browser at all,
/// so there is no window in which it could execute.
/// </remarks>
[HtmlTargetElement("consent-script")]
public sealed class ConsentScriptTagHelper(IConsentState consent) : TagHelper
{
    /// <summary>The consent category this element is gated on.</summary>
    /// <remarks>
    /// In Razor, the attribute value must exactly match the PascalCase enum member name, e.g.
    /// <c>category="Statistics"</c>, not <c>category="statistics"</c>. Tag-helper attribute
    /// codegen binds this case-sensitively, so a lowercase value fails at compile time with CS0117.
    /// </remarks>
    [HtmlAttributeName("category")]
    public ConsentCategory Category { get; set; } = ConsentCategory.Marketing;

    [HtmlAttributeName("src")]
    public string? Src { get; set; }

    [HtmlAttributeName("async")]
    public bool Async { get; set; }

    public override void Process(TagHelperContext context, TagHelperOutput output)
    {
        if (consent.HasGranted(Category) is false)
        {
            output.SuppressOutput();
            return;
        }

        output.TagName = "script";
        output.TagMode = TagMode.StartTagAndEndTag;

        if (string.IsNullOrWhiteSpace(Src) is false)
        {
            output.Attributes.SetAttribute("src", Src);
        }

        if (Async)
        {
            output.Attributes.SetAttribute(
                new TagHelperAttribute("async", null, HtmlAttributeValueStyle.Minimized));
        }
    }
}
```

- [ ] **Step 4: Run the test to verify it passes**

Run: `dotnet test Esatto.Umbraco.Backoffice.CookieBanner.Tests/Esatto.Umbraco.Backoffice.CookieBanner.Tests.csproj --filter FullyQualifiedName~ConsentScriptTagHelperTests`
Expected: PASS — `Passed! - Failed: 0, Passed: 5, Skipped: 0`

- [ ] **Step 5: Write the failing test for the embed tag helper**

Create `Esatto.Umbraco.Backoffice.CookieBanner.Tests/ConsentEmbedTagHelperTests.cs`. The NDSTK `StubDictionary` (an `ICultureDictionary` echoing `[key]`) becomes `StubTextProvider` here, because the helper now takes `IConsentTextProvider`; the echo shape and every assertion carry over unchanged. The real `ICultureDictionary` stub is ported into Task 8, where the dictionary lookup itself is under test.

```csharp
using Microsoft.AspNetCore.Razor.TagHelpers;
using Esatto.Umbraco.Backoffice.CookieBanner.TagHelpers;
using Xunit;

namespace Esatto.Umbraco.Backoffice.CookieBanner.Tests;

public class ConsentEmbedTagHelperTests
{
    /// <summary>Echoes the key back so a test can assert which key was asked for.</summary>
    private sealed class StubTextProvider : IConsentTextProvider
    {
        public string Get(string key) => $"[{key}]";
    }

    private static TagHelperContext Context() => new(
        new TagHelperAttributeList(),
        new Dictionary<object, object>(),
        Guid.NewGuid().ToString());

    private static TagHelperOutput Output() => new(
        "consent-embed",
        new TagHelperAttributeList(),
        (_, _) => Task.FromResult<TagHelperContent>(new DefaultTagHelperContent()));

    private static ConsentEmbedTagHelper Helper(IConsentState consent) =>
        new(consent, new StubTextProvider())
        {
            Category = ConsentCategory.Marketing,
            Src = "https://www.youtube-nocookie.com/embed/abc",
            Title = "Team video",
        };

    // Pins that a granted category renders the real iframe with its src and title intact.
    [Fact]
    public void Renders_an_iframe_when_granted()
    {
        TagHelperOutput output = Output();

        Helper(new FakeConsentState(ConsentCategory.Marketing)).Process(Context(), output);

        var html = output.Content.GetContent();
        Assert.Equal("div", output.TagName);
        Assert.Contains("<iframe", html);
        Assert.Contains("https://www.youtube-nocookie.com/embed/abc", html);
        Assert.Contains("title=\"Team video\"", html);
    }

    // Pins that the ungranted case renders an invite, not a hidden iframe, and reads both text keys.
    [Fact]
    public void Renders_a_placeholder_with_no_iframe_when_not_granted()
    {
        TagHelperOutput output = Output();

        Helper(new FakeConsentState()).Process(Context(), output);

        var html = output.Content.GetContent();
        Assert.DoesNotContain("<iframe", html);
        Assert.Contains("data-consent-open", html);
        Assert.Contains("[Cookies.Embed.Blocked.Body]", html);
        Assert.Contains("[Cookies.Embed.Blocked.Button]", html);
    }

    // SECURITY: pins that a blocked embed leaks the URL nowhere - not in a data attribute, not
    // hidden, not commented out. Leaking it is how "blocked" embeds end up firing requests anyway.
    [Fact]
    public void The_placeholder_never_leaks_the_embed_url()
    {
        TagHelperOutput output = Output();

        Helper(new FakeConsentState()).Process(Context(), output);

        Assert.DoesNotContain("youtube-nocookie.com", output.Content.GetContent());
    }

    // Pins XSS escaping: an editor-supplied title is HTML-encoded before it reaches the iframe.
    [Fact]
    public void Escapes_a_hostile_title()
    {
        TagHelperOutput output = Output();
        ConsentEmbedTagHelper helper = Helper(new FakeConsentState(ConsentCategory.Marketing));
        helper.Title = "\"><script>alert(1)</script>";

        helper.Process(Context(), output);

        Assert.DoesNotContain("<script>alert(1)</script>", output.Content.GetContent());
    }

    // Pins the packaging rule: the placeholder button styles itself and must not depend on a
    // host class such as .btn-primary, which only ever existed in NDSTK's site.css.
    [Fact]
    public void The_placeholder_button_uses_only_package_owned_classes()
    {
        TagHelperOutput output = Output();

        Helper(new FakeConsentState()).Process(Context(), output);

        var html = output.Content.GetContent();
        Assert.Contains("class=\"consent-btn consent-btn--primary\"", html);
        Assert.DoesNotContain("btn-primary", html);
        Assert.Equal("consent-embed consent-embed--blocked", output.Attributes["class"].Value);
        Assert.Equal("marketing", output.Attributes["data-consent-category"].Value);
    }
}
```

- [ ] **Step 6: Run the test to verify it fails**

Run: `dotnet test Esatto.Umbraco.Backoffice.CookieBanner.Tests/Esatto.Umbraco.Backoffice.CookieBanner.Tests.csproj --filter FullyQualifiedName~ConsentEmbedTagHelperTests`
Expected: FAIL with a build error:
```
ConsentEmbedTagHelperTests.cs(30,9): error CS0246: The type or namespace name 'ConsentEmbedTagHelper' could not be found (are you missing a using directive or an assembly reference?)
The build failed. Fix the build errors and run again.
```

- [ ] **Step 7: Implement the embed tag helper**

Create `Esatto.Umbraco.Backoffice.CookieBanner/src/TagHelpers/ConsentEmbedTagHelper.cs`:

```csharp
using System.Text.Encodings.Web;
using Microsoft.AspNetCore.Razor.TagHelpers;

namespace Esatto.Umbraco.Backoffice.CookieBanner.TagHelpers;

/// <summary>
/// Renders a third-party embed, or a placeholder inviting the visitor to grant the category it needs.
/// </summary>
/// <remarks>
/// The placeholder deliberately does not contain the embed URL in any form. Emitting it - even hidden,
/// even in a data attribute - is how "blocked" embeds end up firing requests anyway.
/// <para>
/// Text comes from <see cref="IConsentTextProvider" /> rather than the <c>ICultureDictionary</c>
/// indexer, which has no fallback at all: a site missing the dictionary item rendered an empty
/// paragraph and an unlabelled button.
/// </para>
/// </remarks>
[HtmlTargetElement("consent-embed", TagStructure = TagStructure.WithoutEndTag)]
public sealed class ConsentEmbedTagHelper(IConsentState consent, IConsentTextProvider text) : TagHelper
{
    /// <summary>The consent category this element is gated on.</summary>
    /// <remarks>
    /// In Razor, the attribute value must exactly match the PascalCase enum member name, e.g.
    /// <c>category="Statistics"</c>, not <c>category="statistics"</c>. Tag-helper attribute
    /// codegen binds this case-sensitively, so a lowercase value fails at compile time with CS0117.
    /// </remarks>
    [HtmlAttributeName("category")]
    public ConsentCategory Category { get; set; } = ConsentCategory.Marketing;

    [HtmlAttributeName("src")]
    public string? Src { get; set; }

    [HtmlAttributeName("title")]
    public string? Title { get; set; }

    public override void Process(TagHelperContext context, TagHelperOutput output)
    {
        HtmlEncoder encoder = HtmlEncoder.Default;
        output.TagName = "div";
        output.TagMode = TagMode.StartTagAndEndTag;

        if (consent.HasGranted(Category))
        {
            output.Attributes.SetAttribute("class", "consent-embed");
            output.Content.SetHtmlContent(
                $"""<iframe src="{encoder.Encode(Src ?? string.Empty)}" title="{encoder.Encode(Title ?? string.Empty)}" loading="lazy" allowfullscreen></iframe>""");
            return;
        }

        var body = text.Get("Cookies.Embed.Blocked.Body");
        var button = text.Get("Cookies.Embed.Blocked.Button");

        output.Attributes.SetAttribute("class", "consent-embed consent-embed--blocked");
        output.Attributes.SetAttribute("data-consent-category", ConsentCategories.ToWireName(Category));
        output.Content.SetHtmlContent(
            $"""
            <p>{encoder.Encode(body)}</p>
            <button type="button" class="consent-btn consent-btn--primary" data-consent-open>{encoder.Encode(button)}</button>
            """);
    }
}
```

- [ ] **Step 8: Run both tag-helper test classes to verify they pass**

Run: `dotnet test Esatto.Umbraco.Backoffice.CookieBanner.Tests/Esatto.Umbraco.Backoffice.CookieBanner.Tests.csproj --filter FullyQualifiedName~TagHelperTests`
Expected: PASS — `Passed! - Failed: 0, Passed: 10, Skipped: 0`

- [ ] **Step 9: Commit**

```bash
git add Esatto.Umbraco.Backoffice.CookieBanner/src/TagHelpers/ConsentScriptTagHelper.cs Esatto.Umbraco.Backoffice.CookieBanner/src/TagHelpers/ConsentEmbedTagHelper.cs Esatto.Umbraco.Backoffice.CookieBanner.Tests/ConsentScriptTagHelperTests.cs Esatto.Umbraco.Backoffice.CookieBanner.Tests/ConsentEmbedTagHelperTests.cs
git commit -m "Add the consent-script and consent-embed tag helpers"
```

---

### Task 8: Localization — text provider plus embedded resx fallbacks

**Files:**
- Create: `Esatto.Umbraco.Backoffice.CookieBanner/src/ConsentTextProvider.cs`
- Modify: `Esatto.Umbraco.Backoffice.CookieBanner/src/ServiceCollectionExtensions.cs` (append `services.TryAddScoped<IConsentTextProvider, ConsentTextProvider>();` inside `AddCookieConsent`, and add an assertion for it to `CookieBannerServiceCollectionExtensionsTests`)
- NOTE: `src/IConsentTextProvider.cs` already exists — Task 6 declared the interface. Do not recreate it.
- Create: `Esatto.Umbraco.Backoffice.CookieBanner/Resources/ConsentText.resx`
- Create: `Esatto.Umbraco.Backoffice.CookieBanner/Resources/ConsentText.sv.resx`
- Test: `Esatto.Umbraco.Backoffice.CookieBanner.Tests/ConsentTextProviderTests.cs`

**Interfaces:**
- Consumes: nothing from earlier tasks except the package csproj and the test csproj (Task 1), with `<InternalsVisibleTo Include="Esatto.Umbraco.Backoffice.CookieBanner.Tests" />`.
- Produces: `public interface IConsentTextProvider { string Get(string key); }` — namespace `Esatto.Umbraco.Backoffice.CookieBanner`. **Public, not internal** (see notes): `ConsentEmbedTagHelper` is `public sealed` with a DI-activated public constructor, so an internal parameter type is CS0051.
- Produces: `internal sealed class ConsentTextProvider(ICultureDictionaryFactory cultureDictionaryFactory, ILogger<ConsentTextProvider> logger) : IConsentTextProvider`
- Produces: the resource base name `Esatto.Umbraco.Backoffice.CookieBanner.Resources.ConsentText` and the canonical 32-key set below. `CookieBannerDictionaryInstaller`, `Views/CookiePolicy.cshtml`, `Views/Shared/Components/ConsentBanner/Default.cshtml` and `consent.js` fallbacks must use exactly these keys.
- Produces (for the DI/composer task): `services.AddScoped<IConsentTextProvider, ConsentTextProvider>();` inside `AddCookieConsent()`.

**The canonical 32 keys.** Harvested from `c:\src\NDSTK\ContentModel\NdstkDictionaryInstaller.cs` (the 33-tuple `Items` array), minus the three unread keys, plus the two that fix `CookiePolicy.cshtml:45`.

| Key | Swedish (`ConsentText.sv.resx`) | English (`ConsentText.resx`) |
|---|---|---|
| `Cookies.Banner.Heading` | Vi använder kakor | We use cookies |
| `Cookies.Banner.Body` | Vi använder nödvändiga kakor för att sajten ska fungera. Vi vill också gärna använda kakor för statistik och innehåll från andra tjänster. | We use necessary cookies to make the site work. We would also like to use cookies for statistics and content from other services. |
| `Cookies.Banner.AcceptAll` | Godkänn alla | Accept all |
| `Cookies.Banner.RejectAll` | Neka alla | Reject all |
| `Cookies.Banner.Customise` | Anpassa | Customise |
| `Cookies.Banner.Save` | Spara val | Save choices |
| `Cookies.Banner.Cancel` | Avbryt | Cancel |
| `Cookies.Banner.Error` | Något gick fel. Försök igen. | Something went wrong. Please try again. |
| `Cookies.Banner.RateLimited` | Du har försökt för många gånger. Vänta en stund och försök igen. | You've tried too many times. Please wait a moment and try again. |
| `Cookies.Category.Necessary.Name` | Nödvändiga | Necessary |
| `Cookies.Category.Necessary.Description` | Krävs för att sajten ska fungera, till exempel inloggning. Kan inte stängas av. | Required for the site to work, for example logging in. Cannot be turned off. |
| `Cookies.Category.Preferences.Name` | Funktionella | Preferences |
| `Cookies.Category.Preferences.Description` | Sparar dina val, till exempel språk. | Remembers your choices, such as language. |
| `Cookies.Category.Statistics.Name` | Statistik | Statistics |
| `Cookies.Category.Statistics.Description` | Hjälper oss förstå hur sajten används. Helt anonymt. | Helps us understand how the site is used. Fully anonymous. |
| `Cookies.Category.Marketing.Name` | Marknadsföring | Marketing |
| `Cookies.Category.Marketing.Description` | Används av inbäddat innehåll, till exempel filmer från YouTube. | Used by embedded content, such as YouTube videos. |
| `Cookies.Category.Cookies` | Kakor i den här kategorin | Cookies in this category |
| `Cookies.Embed.Blocked.Body` | Det här innehållet kommer från en annan tjänst och kräver ditt samtycke. | This content comes from another service and needs your consent. |
| `Cookies.Embed.Blocked.Button` | Visa innehåll | Show content |
| `Cookies.Policy.CurrentChoice` | Ditt nuvarande val | Your current choice |
| `Cookies.Policy.NoChoice` | Du har inte gjort något val än. | You have not made a choice yet. |
| `Cookies.Policy.On` **(new)** | på | on |
| `Cookies.Policy.Off` **(new)** | av | off |
| `Cookies.Policy.Reopen` | Ändra inställningar | Change settings |
| `Cookies.Policy.Withdraw` | Återkalla samtycke | Withdraw consent |
| `Cookies.Footer.Link` | Cookieinställningar | Cookie settings |
| `Cookies.Table.Name` | Namn | Name |
| `Cookies.Table.Provider` | Leverantör | Provider |
| `Cookies.Table.Purpose` | Syfte | Purpose |
| `Cookies.Table.Duration` | Lagringstid | Duration |
| `Cookies.Table.Type` | Typ | Type |

Dropped from the NDSTK set, seeded but never read: `Cookies.Banner.PolicyLink`, `Cookies.Banner.Label`, `Cookies.Settings.Heading`. 33 − 3 + 2 = **32**.

- [ ] **Step 1: Write the failing test**

Create `Esatto.Umbraco.Backoffice.CookieBanner.Tests/ConsentTextProviderTests.cs`. `StubDictionary` is the NDSTK helper ported from `ConsentEmbedTagHelperTests`, given a settable backing map and culture — the real `ICultureDictionary` surface now matters in exactly this one place.

```csharp
using System.Globalization;
using Microsoft.Extensions.Logging.Abstractions;
using Umbraco.Cms.Core.Dictionary;
using Xunit;

namespace Esatto.Umbraco.Backoffice.CookieBanner.Tests;

public class ConsentTextProviderTests
{
    /// <summary>
    /// Stands in for Umbraco's dictionary. The indexer returns <see cref="string.Empty" /> for an
    /// absent key, which is exactly what <c>DefaultCultureDictionary</c> does.
    /// </summary>
    private sealed class StubDictionary : ICultureDictionary, ICultureDictionaryFactory
    {
        private readonly Dictionary<string, string> _items;

        public StubDictionary(CultureInfo culture, params (string Key, string Value)[] items)
        {
            Culture = culture;
            _items = items.ToDictionary(i => i.Key, i => i.Value);
        }

        public string this[string key] => _items.TryGetValue(key, out var value) ? value : string.Empty;

        public CultureInfo Culture { get; }

        public IDictionary<string, string> GetChildren(string key) => new Dictionary<string, string>();

        public ICultureDictionary CreateDictionary() => this;

        public ICultureDictionary CreateDictionary(CultureInfo culture) => this;
    }

    private static ConsentTextProvider Provider(StubDictionary dictionary) =>
        new(dictionary, NullLogger<ConsentTextProvider>.Instance);

    // Pins the resolution order's first rung: an editor's dictionary edit beats the shipped resx,
    // which is the whole reason the dictionary stays the editable source of truth for legal copy.
    [Fact]
    public void A_dictionary_item_wins_over_the_shipped_resx()
    {
        var dictionary = new StubDictionary(
            new CultureInfo("sv-SE"),
            ("Cookies.Banner.AcceptAll", "Ja tack till allt"));

        Assert.Equal("Ja tack till allt", Provider(dictionary).Get("Cookies.Banner.AcceptAll"));
    }

    // Pins the second rung: with no dictionary item the request culture's embedded resx is used,
    // including neutral-parent fallback (sv-SE resolves the sv satellite).
    [Fact]
    public void The_request_cultures_resx_is_used_when_the_dictionary_has_no_item()
    {
        var dictionary = new StubDictionary(new CultureInfo("sv-SE"));

        Assert.Equal("Godkänn alla", Provider(dictionary).Get("Cookies.Banner.AcceptAll"));
    }

    // Pins the third rung: a culture the package ships no resx for falls back to English rather
    // than to the Swedish literals that used to be hardcoded in the .cshtml fallbacks.
    [Fact]
    public void English_is_used_when_the_culture_has_no_resx()
    {
        var dictionary = new StubDictionary(new CultureInfo("de-DE"));

        Assert.Equal("Accept all", Provider(dictionary).Get("Cookies.Banner.AcceptAll"));
    }

    // Pins the bug fix: on/off on the policy page is a real key in both languages - it was
    // hardcoded Swedish at CookiePolicy.cshtml:45 and rendered "på"/"av" even in English.
    [Fact]
    public void The_policy_on_and_off_text_is_translated()
    {
        var swedish = new StubDictionary(new CultureInfo("sv-SE"));
        var german = new StubDictionary(new CultureInfo("de-DE"));

        Assert.Equal("på", Provider(swedish).Get("Cookies.Policy.On"));
        Assert.Equal("av", Provider(swedish).Get("Cookies.Policy.Off"));
        Assert.Equal("on", Provider(german).Get("Cookies.Policy.On"));
        Assert.Equal("off", Provider(german).Get("Cookies.Policy.Off"));
    }

    // Pins that a blank dictionary translation is treated as absent, not as an answer. Umbraco
    // returns "" for a missing item, and returning that rendered empty buttons and paragraphs.
    [Fact]
    public void A_blank_dictionary_translation_falls_through_to_the_resx()
    {
        var dictionary = new StubDictionary(
            new CultureInfo("sv-SE"),
            ("Cookies.Banner.AcceptAll", "   "));

        Assert.Equal("Godkänn alla", Provider(dictionary).Get("Cookies.Banner.AcceptAll"));
    }

    // Pins that lookup is total: an unknown, null or blank key degrades instead of throwing, so a
    // typo in a view can never 500 the page.
    [Fact]
    public void An_unknown_key_returns_the_key_instead_of_throwing()
    {
        ConsentTextProvider provider = Provider(new StubDictionary(new CultureInfo("sv-SE")));

        Assert.Equal("Cookies.Does.Not.Exist", provider.Get("Cookies.Does.Not.Exist"));
        Assert.Equal(string.Empty, provider.Get(null!));
        Assert.Equal(string.Empty, provider.Get("  "));
    }
}
```

- [ ] **Step 2: Run the test to verify it fails**

Run: `dotnet test Esatto.Umbraco.Backoffice.CookieBanner.Tests/Esatto.Umbraco.Backoffice.CookieBanner.Tests.csproj --filter FullyQualifiedName~ConsentTextProviderTests`
Expected: FAIL with a build error, not a test failure:
```
ConsentTextProviderTests.cs(37,13): error CS0246: The type or namespace name 'ConsentTextProvider' could not be found (are you missing a using directive or an assembly reference?)
ConsentTextProviderTests.cs(38,20): error CS0246: The type or namespace name 'ConsentTextProvider' could not be found (are you missing a using directive or an assembly reference?)
The build failed. Fix the build errors and run again.
```

- [ ] **Step 3: Create the interface**

Create `Esatto.Umbraco.Backoffice.CookieBanner/src/IConsentTextProvider.cs`:

```csharp
namespace Esatto.Umbraco.Backoffice.CookieBanner;

/// <summary>
/// Resolves a piece of consent copy by dictionary key.
/// </summary>
/// <remarks>
/// Public because <c>ConsentEmbedTagHelper</c> is public and DI-activated: a public constructor
/// cannot take an internal parameter type (CS0051). The implementation stays internal.
/// </remarks>
public interface IConsentTextProvider
{
    /// <summary>
    /// Returns the text for <paramref name="key" />, resolved in order: the site's Umbraco
    /// dictionary item, then the embedded resx for the request culture, then English. Never throws
    /// and never returns null; an unknown key comes back as the key itself.
    /// </summary>
    string Get(string key);
}
```

- [ ] **Step 4: Create the English (neutral) resx**

Create `Esatto.Umbraco.Backoffice.CookieBanner/Resources/ConsentText.resx`. The SDK's default globs include `**/*.resx` as `EmbeddedResource`, so with `RootNamespace` = `Esatto.Umbraco.Backoffice.CookieBanner` this file's manifest name is `Esatto.Umbraco.Backoffice.CookieBanner.Resources.ConsentText.resources` — no csproj change needed. Keep the folder at the package root, not under `src/`, or the manifest name gains a `.src.` segment.

```xml
<?xml version="1.0" encoding="utf-8"?>
<root>
  <xsd:schema id="root" xmlns="" xmlns:xsd="http://www.w3.org/2001/XMLSchema" xmlns:msdata="urn:schemas-microsoft-com:xml-msdata">
    <xsd:import namespace="http://www.w3.org/XML/1998/namespace" />
    <xsd:element name="root" msdata:IsDataSet="true">
      <xsd:complexType>
        <xsd:choice maxOccurs="unbounded">
          <xsd:element name="metadata">
            <xsd:complexType>
              <xsd:sequence>
                <xsd:element name="value" type="xsd:string" minOccurs="0" />
              </xsd:sequence>
              <xsd:attribute name="name" use="required" type="xsd:string" />
              <xsd:attribute name="type" type="xsd:string" />
              <xsd:attribute name="mimetype" type="xsd:string" />
              <xsd:attribute ref="xml:space" />
            </xsd:complexType>
          </xsd:element>
          <xsd:element name="assembly">
            <xsd:complexType>
              <xsd:attribute name="alias" type="xsd:string" />
              <xsd:attribute name="name" type="xsd:string" />
            </xsd:complexType>
          </xsd:element>
          <xsd:element name="data">
            <xsd:complexType>
              <xsd:sequence>
                <xsd:element name="value" type="xsd:string" minOccurs="0" msdata:Ordinal="1" />
                <xsd:element name="comment" type="xsd:string" minOccurs="0" msdata:Ordinal="2" />
              </xsd:sequence>
              <xsd:attribute name="name" type="xsd:string" use="required" msdata:Ordinal="1" />
              <xsd:attribute name="type" type="xsd:string" msdata:Ordinal="3" />
              <xsd:attribute name="mimetype" type="xsd:string" msdata:Ordinal="4" />
              <xsd:attribute ref="xml:space" />
            </xsd:complexType>
          </xsd:element>
          <xsd:element name="resheader">
            <xsd:complexType>
              <xsd:sequence>
                <xsd:element name="value" type="xsd:string" minOccurs="0" msdata:Ordinal="1" />
              </xsd:sequence>
              <xsd:attribute name="name" type="xsd:string" use="required" />
            </xsd:complexType>
          </xsd:element>
        </xsd:choice>
      </xsd:complexType>
    </xsd:element>
  </xsd:schema>
  <resheader name="resmimetype">
    <value>text/microsoft-resx</value>
  </resheader>
  <resheader name="version">
    <value>2.0</value>
  </resheader>
  <resheader name="reader">
    <value>System.Resources.ResXResourceReader, System.Windows.Forms, Version=4.0.0.0, Culture=neutral, PublicKeyToken=b77a5c561934e089</value>
  </resheader>
  <resheader name="writer">
    <value>System.Resources.ResXResourceWriter, System.Windows.Forms, Version=4.0.0.0, Culture=neutral, PublicKeyToken=b77a5c561934e089</value>
  </resheader>
  <data name="Cookies.Banner.Heading" xml:space="preserve">
    <value>We use cookies</value>
  </data>
  <data name="Cookies.Banner.Body" xml:space="preserve">
    <value>We use necessary cookies to make the site work. We would also like to use cookies for statistics and content from other services.</value>
  </data>
  <data name="Cookies.Banner.AcceptAll" xml:space="preserve">
    <value>Accept all</value>
  </data>
  <data name="Cookies.Banner.RejectAll" xml:space="preserve">
    <value>Reject all</value>
  </data>
  <data name="Cookies.Banner.Customise" xml:space="preserve">
    <value>Customise</value>
  </data>
  <data name="Cookies.Banner.Save" xml:space="preserve">
    <value>Save choices</value>
  </data>
  <data name="Cookies.Banner.Cancel" xml:space="preserve">
    <value>Cancel</value>
  </data>
  <data name="Cookies.Banner.Error" xml:space="preserve">
    <value>Something went wrong. Please try again.</value>
  </data>
  <data name="Cookies.Banner.RateLimited" xml:space="preserve">
    <value>You've tried too many times. Please wait a moment and try again.</value>
  </data>
  <data name="Cookies.Category.Necessary.Name" xml:space="preserve">
    <value>Necessary</value>
  </data>
  <data name="Cookies.Category.Necessary.Description" xml:space="preserve">
    <value>Required for the site to work, for example logging in. Cannot be turned off.</value>
  </data>
  <data name="Cookies.Category.Preferences.Name" xml:space="preserve">
    <value>Preferences</value>
  </data>
  <data name="Cookies.Category.Preferences.Description" xml:space="preserve">
    <value>Remembers your choices, such as language.</value>
  </data>
  <data name="Cookies.Category.Statistics.Name" xml:space="preserve">
    <value>Statistics</value>
  </data>
  <data name="Cookies.Category.Statistics.Description" xml:space="preserve">
    <value>Helps us understand how the site is used. Fully anonymous.</value>
  </data>
  <data name="Cookies.Category.Marketing.Name" xml:space="preserve">
    <value>Marketing</value>
  </data>
  <data name="Cookies.Category.Marketing.Description" xml:space="preserve">
    <value>Used by embedded content, such as YouTube videos.</value>
  </data>
  <data name="Cookies.Category.Cookies" xml:space="preserve">
    <value>Cookies in this category</value>
  </data>
  <data name="Cookies.Embed.Blocked.Body" xml:space="preserve">
    <value>This content comes from another service and needs your consent.</value>
  </data>
  <data name="Cookies.Embed.Blocked.Button" xml:space="preserve">
    <value>Show content</value>
  </data>
  <data name="Cookies.Policy.CurrentChoice" xml:space="preserve">
    <value>Your current choice</value>
  </data>
  <data name="Cookies.Policy.NoChoice" xml:space="preserve">
    <value>You have not made a choice yet.</value>
  </data>
  <data name="Cookies.Policy.On" xml:space="preserve">
    <value>on</value>
  </data>
  <data name="Cookies.Policy.Off" xml:space="preserve">
    <value>off</value>
  </data>
  <data name="Cookies.Policy.Reopen" xml:space="preserve">
    <value>Change settings</value>
  </data>
  <data name="Cookies.Policy.Withdraw" xml:space="preserve">
    <value>Withdraw consent</value>
  </data>
  <data name="Cookies.Footer.Link" xml:space="preserve">
    <value>Cookie settings</value>
  </data>
  <data name="Cookies.Table.Name" xml:space="preserve">
    <value>Name</value>
  </data>
  <data name="Cookies.Table.Provider" xml:space="preserve">
    <value>Provider</value>
  </data>
  <data name="Cookies.Table.Purpose" xml:space="preserve">
    <value>Purpose</value>
  </data>
  <data name="Cookies.Table.Duration" xml:space="preserve">
    <value>Duration</value>
  </data>
  <data name="Cookies.Table.Type" xml:space="preserve">
    <value>Type</value>
  </data>
</root>
```

- [ ] **Step 5: Create the Swedish resx**

Create `Esatto.Umbraco.Backoffice.CookieBanner/Resources/ConsentText.sv.resx`, saved as UTF-8. This builds into the `sv/Esatto.Umbraco.Backoffice.CookieBanner.resources.dll` satellite assembly.

```xml
<?xml version="1.0" encoding="utf-8"?>
<root>
  <xsd:schema id="root" xmlns="" xmlns:xsd="http://www.w3.org/2001/XMLSchema" xmlns:msdata="urn:schemas-microsoft-com:xml-msdata">
    <xsd:import namespace="http://www.w3.org/XML/1998/namespace" />
    <xsd:element name="root" msdata:IsDataSet="true">
      <xsd:complexType>
        <xsd:choice maxOccurs="unbounded">
          <xsd:element name="metadata">
            <xsd:complexType>
              <xsd:sequence>
                <xsd:element name="value" type="xsd:string" minOccurs="0" />
              </xsd:sequence>
              <xsd:attribute name="name" use="required" type="xsd:string" />
              <xsd:attribute name="type" type="xsd:string" />
              <xsd:attribute name="mimetype" type="xsd:string" />
              <xsd:attribute ref="xml:space" />
            </xsd:complexType>
          </xsd:element>
          <xsd:element name="assembly">
            <xsd:complexType>
              <xsd:attribute name="alias" type="xsd:string" />
              <xsd:attribute name="name" type="xsd:string" />
            </xsd:complexType>
          </xsd:element>
          <xsd:element name="data">
            <xsd:complexType>
              <xsd:sequence>
                <xsd:element name="value" type="xsd:string" minOccurs="0" msdata:Ordinal="1" />
                <xsd:element name="comment" type="xsd:string" minOccurs="0" msdata:Ordinal="2" />
              </xsd:sequence>
              <xsd:attribute name="name" type="xsd:string" use="required" msdata:Ordinal="1" />
              <xsd:attribute name="type" type="xsd:string" msdata:Ordinal="3" />
              <xsd:attribute name="mimetype" type="xsd:string" msdata:Ordinal="4" />
              <xsd:attribute ref="xml:space" />
            </xsd:complexType>
          </xsd:element>
          <xsd:element name="resheader">
            <xsd:complexType>
              <xsd:sequence>
                <xsd:element name="value" type="xsd:string" minOccurs="0" msdata:Ordinal="1" />
              </xsd:sequence>
              <xsd:attribute name="name" type="xsd:string" use="required" />
            </xsd:complexType>
          </xsd:element>
        </xsd:choice>
      </xsd:complexType>
    </xsd:element>
  </xsd:schema>
  <resheader name="resmimetype">
    <value>text/microsoft-resx</value>
  </resheader>
  <resheader name="version">
    <value>2.0</value>
  </resheader>
  <resheader name="reader">
    <value>System.Resources.ResXResourceReader, System.Windows.Forms, Version=4.0.0.0, Culture=neutral, PublicKeyToken=b77a5c561934e089</value>
  </resheader>
  <resheader name="writer">
    <value>System.Resources.ResXResourceWriter, System.Windows.Forms, Version=4.0.0.0, Culture=neutral, PublicKeyToken=b77a5c561934e089</value>
  </resheader>
  <data name="Cookies.Banner.Heading" xml:space="preserve">
    <value>Vi använder kakor</value>
  </data>
  <data name="Cookies.Banner.Body" xml:space="preserve">
    <value>Vi använder nödvändiga kakor för att sajten ska fungera. Vi vill också gärna använda kakor för statistik och innehåll från andra tjänster.</value>
  </data>
  <data name="Cookies.Banner.AcceptAll" xml:space="preserve">
    <value>Godkänn alla</value>
  </data>
  <data name="Cookies.Banner.RejectAll" xml:space="preserve">
    <value>Neka alla</value>
  </data>
  <data name="Cookies.Banner.Customise" xml:space="preserve">
    <value>Anpassa</value>
  </data>
  <data name="Cookies.Banner.Save" xml:space="preserve">
    <value>Spara val</value>
  </data>
  <data name="Cookies.Banner.Cancel" xml:space="preserve">
    <value>Avbryt</value>
  </data>
  <data name="Cookies.Banner.Error" xml:space="preserve">
    <value>Något gick fel. Försök igen.</value>
  </data>
  <data name="Cookies.Banner.RateLimited" xml:space="preserve">
    <value>Du har försökt för många gånger. Vänta en stund och försök igen.</value>
  </data>
  <data name="Cookies.Category.Necessary.Name" xml:space="preserve">
    <value>Nödvändiga</value>
  </data>
  <data name="Cookies.Category.Necessary.Description" xml:space="preserve">
    <value>Krävs för att sajten ska fungera, till exempel inloggning. Kan inte stängas av.</value>
  </data>
  <data name="Cookies.Category.Preferences.Name" xml:space="preserve">
    <value>Funktionella</value>
  </data>
  <data name="Cookies.Category.Preferences.Description" xml:space="preserve">
    <value>Sparar dina val, till exempel språk.</value>
  </data>
  <data name="Cookies.Category.Statistics.Name" xml:space="preserve">
    <value>Statistik</value>
  </data>
  <data name="Cookies.Category.Statistics.Description" xml:space="preserve">
    <value>Hjälper oss förstå hur sajten används. Helt anonymt.</value>
  </data>
  <data name="Cookies.Category.Marketing.Name" xml:space="preserve">
    <value>Marknadsföring</value>
  </data>
  <data name="Cookies.Category.Marketing.Description" xml:space="preserve">
    <value>Används av inbäddat innehåll, till exempel filmer från YouTube.</value>
  </data>
  <data name="Cookies.Category.Cookies" xml:space="preserve">
    <value>Kakor i den här kategorin</value>
  </data>
  <data name="Cookies.Embed.Blocked.Body" xml:space="preserve">
    <value>Det här innehållet kommer från en annan tjänst och kräver ditt samtycke.</value>
  </data>
  <data name="Cookies.Embed.Blocked.Button" xml:space="preserve">
    <value>Visa innehåll</value>
  </data>
  <data name="Cookies.Policy.CurrentChoice" xml:space="preserve">
    <value>Ditt nuvarande val</value>
  </data>
  <data name="Cookies.Policy.NoChoice" xml:space="preserve">
    <value>Du har inte gjort något val än.</value>
  </data>
  <data name="Cookies.Policy.On" xml:space="preserve">
    <value>på</value>
  </data>
  <data name="Cookies.Policy.Off" xml:space="preserve">
    <value>av</value>
  </data>
  <data name="Cookies.Policy.Reopen" xml:space="preserve">
    <value>Ändra inställningar</value>
  </data>
  <data name="Cookies.Policy.Withdraw" xml:space="preserve">
    <value>Återkalla samtycke</value>
  </data>
  <data name="Cookies.Footer.Link" xml:space="preserve">
    <value>Cookieinställningar</value>
  </data>
  <data name="Cookies.Table.Name" xml:space="preserve">
    <value>Namn</value>
  </data>
  <data name="Cookies.Table.Provider" xml:space="preserve">
    <value>Leverantör</value>
  </data>
  <data name="Cookies.Table.Purpose" xml:space="preserve">
    <value>Syfte</value>
  </data>
  <data name="Cookies.Table.Duration" xml:space="preserve">
    <value>Lagringstid</value>
  </data>
  <data name="Cookies.Table.Type" xml:space="preserve">
    <value>Typ</value>
  </data>
</root>
```

- [ ] **Step 6: Implement the provider**

Create `Esatto.Umbraco.Backoffice.CookieBanner/src/ConsentTextProvider.cs`:

```csharp
using System.Globalization;
using System.Resources;
using Microsoft.Extensions.Logging;
using Umbraco.Cms.Core.Dictionary;

namespace Esatto.Umbraco.Backoffice.CookieBanner;

/// <summary>
/// Resolves consent copy: Umbraco dictionary item, then the embedded resx for the request culture,
/// then English.
/// </summary>
/// <remarks>
/// The dictionary comes first because consent copy is exactly the text that changes for legal
/// reasons; editors must be able to reword it without a deploy. The resx layer exists so the
/// package works on a site that has never seen the seeder - the previous design put Swedish
/// literals in Razor fallbacks instead.
/// <para>
/// The culture comes from <see cref="ICultureDictionary.Culture" /> rather than
/// <see cref="CultureInfo.CurrentUICulture" />, so a consumer who replaces
/// <see cref="ICultureDictionaryFactory" /> gets their culture honoured on both layers.
/// </para>
/// </remarks>
internal sealed class ConsentTextProvider(
    ICultureDictionaryFactory cultureDictionaryFactory,
    ILogger<ConsentTextProvider> logger) : IConsentTextProvider
{
    private static readonly ResourceManager Resources = new(
        "Esatto.Umbraco.Backoffice.CookieBanner.Resources.ConsentText",
        typeof(ConsentTextProvider).Assembly);

    /// <summary>The ultimate fallback. Its strings live in the main assembly, not a satellite.</summary>
    private static readonly CultureInfo English = new("en");

    public string Get(string key)
    {
        if (string.IsNullOrWhiteSpace(key))
        {
            return string.Empty;
        }

        CultureInfo culture = English;

        try
        {
            ICultureDictionary dictionary = cultureDictionaryFactory.CreateDictionary();
            culture = dictionary.Culture ?? English;

            // Umbraco returns an empty string for an absent item, so blank means "not translated"
            // rather than "translated to nothing". Falling through is what makes the fallback work.
            var edited = dictionary[key];
            if (string.IsNullOrWhiteSpace(edited) is false)
            {
                return edited;
            }
        }
        catch (Exception ex)
        {
            // Text lookup must never take a page down: outside an Umbraco request scope, or before
            // the database is reachable, the dictionary can throw. The shipped text still renders.
            logger.LogDebug(ex, "Dictionary lookup for {Key} failed; using the shipped text.", key);
        }

        return FromResources(key, culture)
            ?? FromResources(key, English)
            ?? key;
    }

    private static string? FromResources(string key, CultureInfo culture)
    {
        try
        {
            var value = Resources.GetString(key, culture);
            return string.IsNullOrWhiteSpace(value) ? null : value;
        }
        catch (MissingManifestResourceException)
        {
            return null;
        }
    }
}
```

- [ ] **Step 7: Run the test to verify it passes**

Run: `dotnet test Esatto.Umbraco.Backoffice.CookieBanner.Tests/Esatto.Umbraco.Backoffice.CookieBanner.Tests.csproj --filter FullyQualifiedName~ConsentTextProviderTests`
Expected: PASS — `Passed! - Failed: 0, Passed: 6, Skipped: 0`

- [ ] **Step 8: Commit**

```bash
git add Esatto.Umbraco.Backoffice.CookieBanner/src/IConsentTextProvider.cs Esatto.Umbraco.Backoffice.CookieBanner/src/ConsentTextProvider.cs Esatto.Umbraco.Backoffice.CookieBanner/Resources/ConsentText.resx Esatto.Umbraco.Backoffice.CookieBanner/Resources/ConsentText.sv.resx Esatto.Umbraco.Backoffice.CookieBanner.Tests/ConsentTextProviderTests.cs
git commit -m "Resolve consent text from the dictionary with embedded en and sv fallbacks"
```

### Task 9: `CookieDeclaration` record and the shared registry grouper

**Files:**
- Create: `Esatto.Umbraco.Backoffice.CookieBanner/src/CookieDeclaration.cs`
- Create: `Esatto.Umbraco.Backoffice.CookieBanner/src/CookieRegistry.cs`
- Test: `Esatto.Umbraco.Backoffice.CookieBanner.Tests/CookieRegistryTests.cs`

(All paths relative to the repo root `c:\src\Esatto.Packages`, which is also where every `dotnet`/`git` command below is run.)

**Interfaces:**
- Consumes: `enum ConsentCategory { Necessary, Preferences, Statistics, Marketing }`; `static IReadOnlyList<ConsentCategory> ConsentCategories.All`; the package csproj with `<InternalsVisibleTo Include="Esatto.Umbraco.Backoffice.CookieBanner.Tests" />`; the test csproj.
- Produces: `public sealed record CookieDeclaration(string Name, string Provider, ConsentCategory Category, string Purpose, string Duration, string StorageType)`; `internal static class CookieRegistry` with `public static IReadOnlyDictionary<ConsentCategory, IReadOnlyList<CookieDeclaration>> Group(IEnumerable<CookieDeclaration> declarations)`.

This task removes a live divergence: `c:\src\NDSTK\Views\Partials\_ConsentBanner.cshtml:31-40` skips a block when `cookieName` is blank, while `c:\src\NDSTK\Views\CookiePolicy.cshtml:14-20` adds every block whose category parses and then renders `<td><code>@(cookie.Content.Value<string>("cookieName"))</code></td>` — an empty `<code>` cell. The contract fixes the behaviour as "drop blank names", once, here.

- [ ] **Step 1: Write the failing test**

Create `Esatto.Umbraco.Backoffice.CookieBanner.Tests/CookieRegistryTests.cs`:

```csharp
using System.Linq;
using Xunit;

namespace Esatto.Umbraco.Backoffice.CookieBanner.Tests;

public class CookieRegistryTests
{
    private static CookieDeclaration Declaration(
        string name,
        ConsentCategory category = ConsentCategory.Statistics) =>
        new(name, "Example Inc", category, "Measures use of the site", "1 year", "Cookie");

    [Fact]
    public void Groups_every_declaration_under_its_own_category()
    {
        // Pins the single grouping behaviour that replaces the two hand-written copies in
        // _ConsentBanner.cshtml and CookiePolicy.cshtml, whose comments each claimed to match the other.
        var grouped = CookieRegistry.Group(
        [
            Declaration("_ga", ConsentCategory.Statistics),
            Declaration("_gcl_au", ConsentCategory.Marketing),
            Declaration("cookie-consent", ConsentCategory.Necessary),
        ]);

        Assert.Equal(new[] { "cookie-consent" }, grouped[ConsentCategory.Necessary].Select(d => d.Name).ToArray());
        Assert.Equal(new[] { "_ga" }, grouped[ConsentCategory.Statistics].Select(d => d.Name).ToArray());
        Assert.Equal(new[] { "_gcl_au" }, grouped[ConsentCategory.Marketing].Select(d => d.Name).ToArray());
    }

    [Fact]
    public void Drops_declarations_with_a_blank_name()
    {
        // The regression this whole type exists for: the banner dropped blank cookieName blocks, the
        // policy page rendered them as an empty <code> cell. The contract settles it as "drop".
        var grouped = CookieRegistry.Group(
        [
            Declaration(string.Empty),
            Declaration("   "),
            Declaration("_ga"),
        ]);

        Assert.Equal(new[] { "_ga" }, grouped[ConsentCategory.Statistics].Select(d => d.Name).ToArray());
    }

    [Fact]
    public void Drops_a_declaration_whose_category_is_outside_the_known_set()
    {
        // An unparsable category is dropped upstream by the mapper, but an out-of-range enum value
        // must not reach a bucket lookup and throw KeyNotFoundException mid-render either.
        var grouped = CookieRegistry.Group([Declaration("_mystery", (ConsentCategory)99)]);

        Assert.All(grouped.Values, declarations => Assert.Empty(declarations));
    }

    [Fact]
    public void Returns_an_empty_list_for_a_category_with_no_declarations()
    {
        // Both views index this dictionary by category unconditionally, so every category must exist
        // as a key with an empty list rather than be absent.
        var grouped = CookieRegistry.Group([Declaration("_ga", ConsentCategory.Statistics)]);

        Assert.Empty(grouped[ConsentCategory.Necessary]);
        Assert.Empty(grouped[ConsentCategory.Preferences]);
        Assert.Empty(grouped[ConsentCategory.Marketing]);
    }

    [Fact]
    public void Yields_one_bucket_per_category_for_an_empty_sequence()
    {
        // A site with no published policy page hands in nothing; the banner still renders four
        // fieldsets, so four buckets must come back.
        var grouped = CookieRegistry.Group([]);

        Assert.Equal(4, grouped.Count);
        Assert.All(grouped.Values, declarations => Assert.Empty(declarations));
    }

    [Fact]
    public void Enumerates_categories_in_ConsentCategories_All_order()
    {
        // Display order is necessary-first and is read straight off this dictionary's key order.
        var grouped = CookieRegistry.Group([Declaration("_ga")]);

        Assert.Equal(ConsentCategories.All.ToArray(), grouped.Keys.ToArray());
    }

    [Fact]
    public void Preserves_editor_ordering_within_a_category()
    {
        // Editors sort the Block List to control the table order; grouping must not reshuffle it.
        var grouped = CookieRegistry.Group(
        [
            Declaration("_ga"),
            Declaration("_gid"),
            Declaration("_ga_ABC"),
        ]);

        Assert.Equal(
            new[] { "_ga", "_gid", "_ga_ABC" },
            grouped[ConsentCategory.Statistics].Select(d => d.Name).ToArray());
    }
}
```

- [ ] **Step 2: Run the test to verify it fails**

Run: `dotnet test Esatto.Umbraco.Backoffice.CookieBanner.Tests/Esatto.Umbraco.Backoffice.CookieBanner.Tests.csproj --filter FullyQualifiedName~CookieRegistryTests`
Expected: FAIL to compile with `error CS0246: The type or namespace name 'CookieDeclaration' could not be found` and `error CS0103: The name 'CookieRegistry' does not exist in the current context`.

- [ ] **Step 3: Implement the `CookieDeclaration` record**

Create `Esatto.Umbraco.Backoffice.CookieBanner/src/CookieDeclaration.cs`:

```csharp
namespace Esatto.Umbraco.Backoffice.CookieBanner;

/// <summary>
/// One declared cookie, storage entry or pixel, projected out of an editor-managed
/// <c>cookieDefinition</c> block.
/// </summary>
/// <remarks>
/// Deliberately free of Umbraco types: it is what lets the grouping rules be unit tested without a
/// published content graph, and it is the only shape the two views need.
/// <paramref name="Name"/> is the cookie/storage key; a declaration with a blank one is dropped by
/// <c>CookieRegistry.Group</c> rather than rendered as an empty cell.
/// </remarks>
public sealed record CookieDeclaration(
    string Name,
    string Provider,
    ConsentCategory Category,
    string Purpose,
    string Duration,
    string StorageType);
```

- [ ] **Step 4: Implement the grouper**

Create `Esatto.Umbraco.Backoffice.CookieBanner/src/CookieRegistry.cs`:

```csharp
using System.Collections.Generic;
using System.Linq;

namespace Esatto.Umbraco.Backoffice.CookieBanner;

/// <summary>
/// Groups declared cookies by consent category for the banner and the policy page.
/// </summary>
/// <remarks>
/// This logic used to exist twice - once in the consent dialog partial, once in the policy-page
/// template - with a comment in each claiming the two agreed. They did not: only the banner dropped
/// blocks with a blank cookie name. One function, one tested behaviour.
/// </remarks>
internal static class CookieRegistry
{
    /// <summary>
    /// Returns one bucket per <see cref="ConsentCategories.All"/> entry, in that order, so callers
    /// can index by category unconditionally. Declarations with a blank
    /// <see cref="CookieDeclaration.Name"/>, or a category outside the known set, are dropped.
    /// </summary>
    public static IReadOnlyDictionary<ConsentCategory, IReadOnlyList<CookieDeclaration>> Group(
        IEnumerable<CookieDeclaration> declarations)
    {
        Dictionary<ConsentCategory, List<CookieDeclaration>> buckets =
            ConsentCategories.All.ToDictionary(category => category, _ => new List<CookieDeclaration>());

        foreach (CookieDeclaration declaration in declarations)
        {
            // A cookie with no name tells a visitor nothing and cannot be matched against anything a
            // scanner finds, so it is editor noise rather than a declaration.
            if (string.IsNullOrWhiteSpace(declaration.Name))
            {
                continue;
            }

            // Defensive: the mapper already refuses unparsable category values, but an out-of-range
            // enum cast must not become a KeyNotFoundException halfway through rendering a dialog.
            if (buckets.TryGetValue(declaration.Category, out List<CookieDeclaration>? bucket) is false)
            {
                continue;
            }

            bucket.Add(declaration);
        }

        // Rebuilt in All order so key enumeration is the documented display order rather than
        // whatever insertion order happens to yield.
        return ConsentCategories.All.ToDictionary(
            category => category,
            category => (IReadOnlyList<CookieDeclaration>)buckets[category]);
    }
}
```

- [ ] **Step 5: Run the test to verify it passes**

Run: `dotnet test Esatto.Umbraco.Backoffice.CookieBanner.Tests/Esatto.Umbraco.Backoffice.CookieBanner.Tests.csproj --filter FullyQualifiedName~CookieRegistryTests`
Expected: PASS — 7 passed.

- [ ] **Step 6: Commit**

```bash
git add Esatto.Umbraco.Backoffice.CookieBanner/src/CookieDeclaration.cs Esatto.Umbraco.Backoffice.CookieBanner/src/CookieRegistry.cs Esatto.Umbraco.Backoffice.CookieBanner.Tests/CookieRegistryTests.cs
git commit -m "Add CookieDeclaration and one tested cookie-registry grouper

- Replaces the two divergent copies of the grouping logic in NDSTK's
  _ConsentBanner.cshtml and CookiePolicy.cshtml with a single pure function
- Blank cookie names are dropped, matching the banner's old behaviour and
  fixing the policy page's empty <code> cells
- Buckets come back for every category, in ConsentCategories.All order

Co-Authored-By: Claude Opus 5 (1M context) <noreply@anthropic.com>"
```

---

### Task 10: Banner view component, policy template, and the `consent-head` / `consent-banner` tag helpers

**Files:**
- Create: `Esatto.Umbraco.Backoffice.CookieBanner/src/ConsentBannerViewModel.cs`
- Create: `Esatto.Umbraco.Backoffice.CookieBanner/src/CookieDeclarationMapper.cs`
- Create: `Esatto.Umbraco.Backoffice.CookieBanner/src/ConsentBannerViewComponent.cs`
- Create: `Esatto.Umbraco.Backoffice.CookieBanner/Views/Shared/Components/ConsentBanner/Default.cshtml`
- Create: `Esatto.Umbraco.Backoffice.CookieBanner/Views/CookiePolicy.cshtml`
- Create: `Esatto.Umbraco.Backoffice.CookieBanner/src/TagHelpers/ConsentHeadTagHelper.cs`
- Create: `Esatto.Umbraco.Backoffice.CookieBanner/src/TagHelpers/ConsentBannerTagHelper.cs`
- Test: `Esatto.Umbraco.Backoffice.CookieBanner.Tests/ConsentBannerViewComponentTests.cs`
- Test: `Esatto.Umbraco.Backoffice.CookieBanner.Tests/ConsentHeadTagHelperTests.cs`
- Test: `Esatto.Umbraco.Backoffice.CookieBanner.Tests/ConsentBannerTagHelperTests.cs`

**Interfaces:**
- Consumes: `public interface IConsentState` (`bool NeedsDecision { get; }`, `ConsentDecision? Decision { get; }`, `bool HasGranted(ConsentCategory)`); `public sealed class CookieBannerOptions` (`PolicyVersion`, `CookieName`, `CookieLifetimeDays`, `GoogleMeasurementId`, `PolicyPageKey`, `EndpointPath`, `ThrottleRequestsPerMinute`); `public static class ConsentModeScript` — `string Defaults()`, `string Update(IConsentState)`, `string Config(string measurementId)`; `public static class ConsentCategories` — `ToWireName`, `TryParse`, `All`; `internal interface ICookiePolicyPageResolver` — `IPublishedContent? Resolve()`; `public interface IConsentTextProvider` — `string Get(string key)`; `internal static class CookieRegistry.Group(IEnumerable<CookieDeclaration>)` and `public sealed record CookieDeclaration(...)` (Task 9); `internal sealed class FakeConsentState(params ConsentCategory[] granted) : IConsentState` with `init`-settable `NeedsDecision`; the static assets `wwwroot/esatto-cookiebanner/consent.js` and `consent.css`.
- Produces: `public sealed record ConsentBannerViewModel(bool NeedsDecision, IReadOnlySet<ConsentCategory> Granted, IReadOnlyDictionary<ConsentCategory, IReadOnlyList<CookieDeclaration>> CookiesByCategory, string CookieName, int PolicyVersion, string EndpointPath, bool ConsentModeEnabled, Func<string, string> Text)`; `internal static class CookieDeclarationMapper` — `public static IReadOnlyList<CookieDeclaration> FromBlockList(BlockListModel? blocks, IPublishedValueFallback publishedValueFallback)`; `public sealed class ConsentBannerViewComponent : ViewComponent` — `public ConsentBannerViewComponent(IConsentState consent, IOptions<CookieBannerOptions> options, IPublishedValueFallback publishedValueFallback, IConsentTextProvider text, IServiceProvider services)`, `public IViewComponentResult Invoke()`, `internal ConsentBannerViewModel BuildModel()`; `public sealed class ConsentHeadTagHelper : TagHelper` — ctor `(IConsentState, IOptions<CookieBannerOptions>)`, `internal const string StylesheetPath = "/esatto-cookiebanner/consent.css"`; `public sealed class ConsentBannerTagHelper : TagHelper` — ctor `(IViewComponentHelper)`, `internal const string ViewComponentName = "ConsentBanner"`, `[ViewContext] public ViewContext ViewContext { get; set; }`.

The six NDSTK integration points being collapsed, for reference: `Root.cshtml:45-57` (the `GoogleMeasurementId` guard, the Consent Mode `<script>`, and the gated `<consent-script>`), `Root.cshtml:66` (the `consent.css` link), `Root.cshtml:69` (`Html.PartialAsync("_ConsentBanner")`), plus the `@inject IConsentState` / `@inject IOptions<ConsentOptions>` pair at `Root.cshtml:9-10` that only exist to feed them.

- [ ] **Step 1: Write the failing view-component test**

Create `Esatto.Umbraco.Backoffice.CookieBanner.Tests/ConsentBannerViewComponentTests.cs`:

```csharp
using System;
using System.Linq;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;
using NSubstitute;
using Umbraco.Cms.Core.Models.PublishedContent;
using Xunit;

namespace Esatto.Umbraco.Backoffice.CookieBanner.Tests;

public class ConsentBannerViewComponentTests
{
    private static ConsentBannerViewModel Model(IConsentState consent, CookieBannerOptions options)
    {
        var services = new ServiceCollection();
        services.AddSingleton<ICookiePolicyPageResolver>(new NoPolicyPageResolver());
        var text = new EchoTextProvider();
        services.AddSingleton<IConsentTextProvider>(text);

        var component = new ConsentBannerViewComponent(
            consent,
            Options.Create(options),
            Substitute.For<IPublishedValueFallback>(),
            text,
            services.BuildServiceProvider());

        return component.BuildModel();
    }

    [Fact]
    public void Degrades_to_an_empty_registry_when_no_policy_page_is_published()
    {
        // Policy-page resolution is best-effort. The NDSTK partial degraded through four possibly
        // absent steps; the package must still render four fieldsets rather than throw mid-request.
        ConsentBannerViewModel model = Model(
            new FakeConsentState { NeedsDecision = true },
            new CookieBannerOptions());

        Assert.Equal(ConsentCategories.All.ToArray(), model.CookiesByCategory.Keys.ToArray());
        Assert.All(model.CookiesByCategory.Values, declarations => Assert.Empty(declarations));
    }

    [Fact]
    public void Carries_the_configured_cookie_name_version_and_endpoint_into_the_model()
    {
        // A package must not bake in a site's cookie name or endpoint: NDSTK pins CookieName back to
        // ndstk-consent precisely so no existing visitor is re-prompted.
        ConsentBannerViewModel model = Model(
            new FakeConsentState(),
            new CookieBannerOptions
            {
                CookieName = "ndstk-consent",
                PolicyVersion = 7,
                EndpointPath = "/api/consent",
            });

        Assert.Equal("ndstk-consent", model.CookieName);
        Assert.Equal(7, model.PolicyVersion);
        Assert.Equal("/api/consent", model.EndpointPath);
    }

    [Fact]
    public void Consent_mode_is_off_until_a_measurement_id_is_configured()
    {
        // data-consent-mode drives whether consent.js re-signals gtag; with no id there is nothing
        // to signal and the head block is never emitted either.
        Assert.False(Model(new FakeConsentState(), new CookieBannerOptions()).ConsentModeEnabled);
        Assert.True(Model(
            new FakeConsentState(),
            new CookieBannerOptions { GoogleMeasurementId = "G-ABC123" }).ConsentModeEnabled);
    }

    [Fact]
    public void Granted_follows_HasGranted_so_a_stale_decision_pre_ticks_nothing()
    {
        // _ConsentBanner.cshtml read Decision.Granted directly, which ticks Statistics for a visitor
        // whose decision predates the current PolicyVersion - even though the gating code grants
        // nothing to that visitor. HasGranted is the single source of truth.
        ConsentBannerViewModel model = Model(
            new FakeConsentState(ConsentCategory.Statistics) { NeedsDecision = true },
            new CookieBannerOptions());

        Assert.Equal(new[] { ConsentCategory.Necessary }, model.Granted.ToArray());
    }

    [Fact]
    public void Granted_contains_necessary_plus_every_actually_granted_category()
    {
        // Necessary is implied rather than stored, so it must be added back for the disabled,
        // always-checked box.
        ConsentBannerViewModel model = Model(
            new FakeConsentState(ConsentCategory.Statistics, ConsentCategory.Marketing),
            new CookieBannerOptions());

        Assert.Contains(ConsentCategory.Necessary, model.Granted);
        Assert.Contains(ConsentCategory.Statistics, model.Granted);
        Assert.Contains(ConsentCategory.Marketing, model.Granted);
        Assert.DoesNotContain(ConsentCategory.Preferences, model.Granted);
    }

    [Fact]
    public void Text_is_resolved_through_the_package_text_provider()
    {
        // Every string in the dialog goes through IConsentTextProvider (dictionary -> resx -> English),
        // which is what removes the 26 inline Swedish fallbacks from the view.
        ConsentBannerViewModel model = Model(new FakeConsentState(), new CookieBannerOptions());

        Assert.Equal("[Cookies.Banner.Heading]", model.Text("Cookies.Banner.Heading"));
    }

    // NSubstitute cannot proxy this assembly's internal interfaces - Castle would need an
    // InternalsVisibleTo for DynamicProxyGenAssembly2, which the package deliberately does not grant -
    // so the two internal services get hand-written fakes.
    private sealed class NoPolicyPageResolver : ICookiePolicyPageResolver
    {
        public IPublishedContent? Resolve() => null;
    }

    private sealed class EchoTextProvider : IConsentTextProvider
    {
        public string Get(string key) => $"[{key}]";
    }
}
```

- [ ] **Step 2: Run the test to verify it fails**

Run: `dotnet test Esatto.Umbraco.Backoffice.CookieBanner.Tests/Esatto.Umbraco.Backoffice.CookieBanner.Tests.csproj --filter FullyQualifiedName~ConsentBannerViewComponentTests`
Expected: FAIL to compile with `error CS0246: The type or namespace name 'ConsentBannerViewComponent' could not be found` and `error CS0246: The type or namespace name 'ConsentBannerViewModel' could not be found`.

- [ ] **Step 3: Implement the view model**

Create `Esatto.Umbraco.Backoffice.CookieBanner/src/ConsentBannerViewModel.cs`:

```csharp
using System;
using System.Collections.Generic;

namespace Esatto.Umbraco.Backoffice.CookieBanner;

/// <summary>
/// Everything <c>Views/Shared/Components/ConsentBanner/Default.cshtml</c> renders.
/// </summary>
/// <remarks>
/// Public because a Razor view's model type appears in the generated view class's base type, so it
/// cannot be less accessible than the view. <see cref="Text"/> is a delegate over the internal
/// text provider for the same reason: the view must not name an internal type in a member signature.
/// </remarks>
/// <param name="NeedsDecision">
/// True on first run. Drives both the collapsed first-run layout and <c>data-consent-needs-decision</c>.
/// </param>
/// <param name="Granted">
/// Read from <c>IConsentState.HasGranted</c>, never from the raw decision, so a decision made against
/// an older policy version pre-ticks nothing.
/// </param>
/// <param name="Text">Key lookup: dictionary item, then embedded resx for the request culture, then English.</param>
public sealed record ConsentBannerViewModel(
    bool NeedsDecision,
    IReadOnlySet<ConsentCategory> Granted,
    IReadOnlyDictionary<ConsentCategory, IReadOnlyList<CookieDeclaration>> CookiesByCategory,
    string CookieName,
    int PolicyVersion,
    string EndpointPath,
    bool ConsentModeEnabled,
    Func<string, string> Text);
```

- [ ] **Step 4: Implement the Block List → `CookieDeclaration` mapper**

Create `Esatto.Umbraco.Backoffice.CookieBanner/src/CookieDeclarationMapper.cs`:

```csharp
using System.Collections.Generic;
using Umbraco.Cms.Core.Models.Blocks;
using Umbraco.Cms.Core.Models.PublishedContent;
using Umbraco.Extensions;

namespace Esatto.Umbraco.Backoffice.CookieBanner;

/// <summary>
/// Projects a <c>cookieRegistry</c> Block List into <see cref="CookieDeclaration"/> records.
/// </summary>
/// <remarks>
/// The only place in the package that touches <c>cookieDefinition</c> property aliases, so both the
/// banner and the policy page read the same block the same way.
/// </remarks>
internal static class CookieDeclarationMapper
{
    /// <summary>
    /// Maps every block whose <c>category</c> parses to a known wire name. An unparsable or missing
    /// category is dropped: defaulting it to <c>necessary</c> would show a cookie as needing no
    /// consent while the gating code would never grant it.
    /// </summary>
    public static IReadOnlyList<CookieDeclaration> FromBlockList(
        BlockListModel? blocks,
        IPublishedValueFallback publishedValueFallback)
    {
        if (blocks is null)
        {
            return [];
        }

        var declarations = new List<CookieDeclaration>();

        foreach (BlockListItem block in blocks)
        {
            var wireCategory = block.Content.Value<string>(publishedValueFallback, "category");
            if (ConsentCategories.TryParse(wireCategory, out ConsentCategory category) is false)
            {
                continue;
            }

            declarations.Add(new CookieDeclaration(
                Name: block.Content.Value<string>(publishedValueFallback, "cookieName") ?? string.Empty,
                Provider: block.Content.Value<string>(publishedValueFallback, "provider") ?? string.Empty,
                Category: category,
                Purpose: block.Content.Value<string>(publishedValueFallback, "purpose") ?? string.Empty,
                Duration: block.Content.Value<string>(publishedValueFallback, "duration") ?? string.Empty,
                StorageType: block.Content.Value<string>(publishedValueFallback, "storageType") ?? string.Empty));
        }

        return declarations;
    }
}
```

- [ ] **Step 5: Implement the view component**

Create `Esatto.Umbraco.Backoffice.CookieBanner/src/ConsentBannerViewComponent.cs`:

```csharp
using System;
using System.Linq;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;
using Umbraco.Cms.Core.Models.Blocks;
using Umbraco.Cms.Core.Models.PublishedContent;
using Umbraco.Extensions;

namespace Esatto.Umbraco.Backoffice.CookieBanner;

/// <summary>
/// Renders the consent dialog. Invoked by <c>&lt;consent-banner /&gt;</c>.
/// </summary>
/// <remarks>
/// View components must be public types, and MVC activates them through
/// <see cref="ActivatorUtilities"/>, which only considers public constructors. A public constructor
/// cannot name this assembly's internal service interfaces (CS0051), so the policy-page resolver and
/// the text provider are pulled from <see cref="IServiceProvider"/> instead of the signature.
/// </remarks>
public sealed class ConsentBannerViewComponent : ViewComponent
{
    private readonly IConsentState _consent;
    private readonly IOptions<CookieBannerOptions> _options;
    private readonly IPublishedValueFallback _publishedValueFallback;
    private readonly ICookiePolicyPageResolver _policyPageResolver;
    private readonly IConsentTextProvider _text;

    // IConsentTextProvider is public, so it is injected normally. ICookiePolicyPageResolver is
    // internal, and a public constructor cannot declare an internal parameter type (CS0051), so
    // that one is resolved from the container instead. Make the interface public if this bothers
    // you - it is also a reasonable extension point for a consumer with its own lookup rule.
    public ConsentBannerViewComponent(
        IConsentState consent,
        IOptions<CookieBannerOptions> options,
        IPublishedValueFallback publishedValueFallback,
        IConsentTextProvider text,
        IServiceProvider services)
    {
        _consent = consent;
        _options = options;
        _publishedValueFallback = publishedValueFallback;
        _text = text;
        _policyPageResolver = services.GetRequiredService<ICookiePolicyPageResolver>();
    }

    public IViewComponentResult Invoke() => View(BuildModel());

    /// <summary>
    /// The whole of the component's behaviour, separated from <see cref="Invoke"/> so it can be
    /// tested without a ViewContext, view engine or temp-data provider.
    /// </summary>
    internal ConsentBannerViewModel BuildModel()
    {
        CookieBannerOptions settings = _options.Value;

        // Every step of this chain can be absent - no published cookiePolicy page, no cookies block
        // on it, an unparsable category on a block - so it must degrade to "no cookies declared for
        // this category" rather than throw or log on a visitor's first request.
        BlockListModel? blocks = _policyPageResolver.Resolve()
            ?.Value<BlockListModel>(_publishedValueFallback, "cookies");

        return new ConsentBannerViewModel(
            NeedsDecision: _consent.NeedsDecision,
            // Read through HasGranted, not Decision.Granted: a decision made against an older
            // PolicyVersion grants nothing, and pre-ticking its boxes would misreport the state.
            Granted: ConsentCategories.All.Where(_consent.HasGranted).ToHashSet(),
            CookiesByCategory: CookieRegistry.Group(
                CookieDeclarationMapper.FromBlockList(blocks, _publishedValueFallback)),
            CookieName: settings.CookieName,
            PolicyVersion: settings.PolicyVersion,
            EndpointPath: settings.EndpointPath,
            ConsentModeEnabled: string.IsNullOrWhiteSpace(settings.GoogleMeasurementId) is false,
            Text: _text.Get);
    }
}
```

- [ ] **Step 6: Run the view-component test to verify it passes**

Run: `dotnet test Esatto.Umbraco.Backoffice.CookieBanner.Tests/Esatto.Umbraco.Backoffice.CookieBanner.Tests.csproj --filter FullyQualifiedName~ConsentBannerViewComponentTests`
Expected: PASS — 6 passed.

- [ ] **Step 7: Write the dialog view**

Create `Esatto.Umbraco.Backoffice.CookieBanner/Views/Shared/Components/ConsentBanner/Default.cshtml`:

```cshtml
@using Esatto.Umbraco.Backoffice.CookieBanner
@model ConsentBannerViewModel
@{
    // Attribute values are hoisted into locals: Razor cannot parse a double-quoted C# string nested
    // inside a double-quoted HTML attribute value.
    var heading = Model.Text("Cookies.Banner.Heading");
    var errorMessage = Model.Text("Cookies.Banner.Error");
    var rateLimitedMessage = Model.Text("Cookies.Banner.RateLimited");
}

<dialog id="esatto-consent-dialog" class="consent-dialog" aria-labelledby="esatto-consent-dialog-heading">
    <div class="consent-dialog__body">
        @* showModal() always moves focus, to the first autofocus element or else the first focusable
           one. Without this it landed on the first <summary> and drew a focus ring, which read as a
           control being pre-selected. Focusing the heading instead keeps focus inside the dialog -
           so screen readers announce it and the focus trap holds - while no control looks chosen.
           tabindex="-1" makes a non-interactive element focusable without adding it to the tab order. *@
        <h2 id="esatto-consent-dialog-heading" tabindex="-1" autofocus>@heading</h2>
        <p>@(Model.Text("Cookies.Banner.Body"))</p>

        @* First run shows only the two symmetric all-or-nothing choices, so a modal the visitor
           cannot dismiss stays short. Per-category choice sits behind the settings affordance:
           revealed by the Customise button on first run, and already open whenever the dialog is
           reopened from the footer link. Server-rendered, so there is no flash of the wrong state. *@
        <div class="consent-dialog__categories" data-consent-categories hidden="@Model.NeedsDecision">
            @foreach (var category in ConsentCategories.All)
            {
                var wire = ConsentCategories.ToWireName(category);
                var isNecessary = category == ConsentCategory.Necessary;
                var inputId = $"esatto-consent-cat-{wire}";
                var categoryCookies = Model.CookiesByCategory[category];
                <fieldset class="consent-category">
                    <legend>@(Model.Text($"Cookies.Category.{category}.Name"))</legend>
                    <div class="consent-category__row">
                        <input type="checkbox"
                               id="@inputId"
                               value="@wire"
                               data-consent-category-input
                               checked="@(isNecessary || Model.Granted.Contains(category))"
                               disabled="@isNecessary" />
                        <label for="@inputId">
                            @(Model.Text($"Cookies.Category.{category}.Description"))
                        </label>
                    </div>
                    @if (categoryCookies.Count > 0)
                    {
                        <details class="consent-category__cookies">
                            <summary>@(Model.Text("Cookies.Category.Cookies"))</summary>
                            @foreach (var cookie in categoryCookies)
                            {
                                <dl class="consent-cookie">
                                    <dt>@(Model.Text("Cookies.Table.Name"))</dt>
                                    <dd>@cookie.Name</dd>
                                    @if (string.IsNullOrWhiteSpace(cookie.Provider) is false)
                                    {
                                        <dt>@(Model.Text("Cookies.Table.Provider"))</dt>
                                        <dd>@cookie.Provider</dd>
                                    }
                                    @if (string.IsNullOrWhiteSpace(cookie.Purpose) is false)
                                    {
                                        <dt>@(Model.Text("Cookies.Table.Purpose"))</dt>
                                        <dd>@cookie.Purpose</dd>
                                    }
                                    @if (string.IsNullOrWhiteSpace(cookie.Duration) is false)
                                    {
                                        <dt>@(Model.Text("Cookies.Table.Duration"))</dt>
                                        <dd>@cookie.Duration</dd>
                                    }
                                    @if (string.IsNullOrWhiteSpace(cookie.StorageType) is false)
                                    {
                                        <dt>@(Model.Text("Cookies.Table.Type"))</dt>
                                        <dd>@cookie.StorageType</dd>
                                    }
                                </dl>
                            }
                        </details>
                    }
                </fieldset>
            }
        </div>

        <p class="consent-status" role="status" aria-live="polite" hidden data-consent-status></p>

        <div class="consent-dialog__actions">
            <button type="button" class="consent-btn consent-btn--primary" data-consent-action="accept-all">@(Model.Text("Cookies.Banner.AcceptAll"))</button>
            @* Reject shares accept's class deliberately: identical treatment, distinguished only by
               label, so neither option is nudged. *@
            <button type="button" class="consent-btn consent-btn--primary" data-consent-action="reject-all">@(Model.Text("Cookies.Banner.RejectAll"))</button>
            @* First run: reveals the category section and swaps itself for Save. On a reopen the
               categories are already on screen, so there is nothing to reveal and it is not rendered. *@
            @if (Model.NeedsDecision)
            {
                <button type="button" class="consent-btn consent-btn--primary" data-consent-customise>@(Model.Text("Cookies.Banner.Customise"))</button>
            }
            @* Saving a partial selection only means something once the categories are visible, so on
               first run this stays hidden until Customise is pressed. *@
            <button type="button" class="consent-btn consent-btn--primary" data-consent-action="custom" hidden="@Model.NeedsDecision">@(Model.Text("Cookies.Banner.Save"))</button>
            @if (Model.NeedsDecision is false)
            {
                <button type="button" class="consent-btn consent-btn--primary" data-consent-close>@(Model.Text("Cookies.Banner.Cancel"))</button>
            }
        </div>
    </div>
</dialog>

@* Root-relative rather than "~/": this view ships inside a Razor class library that registers no
   _ViewImports, so the framework's UrlResolutionTagHelper is not in scope to expand a tilde. *@
<script src="/esatto-cookiebanner/consent.js" defer
        data-consent-endpoint="@Model.EndpointPath"
        data-consent-cookie="@Model.CookieName"
        data-consent-version="@Model.PolicyVersion"
        data-consent-mode="@(Model.ConsentModeEnabled ? "on" : "off")"
        data-consent-needs-decision="@(Model.NeedsDecision ? "true" : "false")"
        data-consent-error-message="@errorMessage"
        data-consent-rate-limited-message="@rateLimitedMessage"></script>
```

- [ ] **Step 8: Build to verify the view compiles**

Run: `dotnet build Esatto.Umbraco.Backoffice.CookieBanner/Esatto.Umbraco.Backoffice.CookieBanner.csproj`
Expected: PASS — `Build succeeded`, 0 errors. (If Razor reports `RAZORSDK1004` or silently skips the view, the csproj is missing `<AddRazorSupportForMvc>true</AddRazorSupportForMvc>`; add it there, as `Esatto.Umbraco.Backoffice.SharedPreviewLink.csproj` does.)

- [ ] **Step 9: Commit the view component and its view**

```bash
git add Esatto.Umbraco.Backoffice.CookieBanner/src/ConsentBannerViewModel.cs Esatto.Umbraco.Backoffice.CookieBanner/src/CookieDeclarationMapper.cs Esatto.Umbraco.Backoffice.CookieBanner/src/ConsentBannerViewComponent.cs "Esatto.Umbraco.Backoffice.CookieBanner/Views/Shared/Components/ConsentBanner/Default.cshtml" Esatto.Umbraco.Backoffice.CookieBanner.Tests/ConsentBannerViewComponentTests.cs
git commit -m "Render the consent dialog from a packaged view component

- Replaces NDSTK's _ConsentBanner.cshtml partial and its Settings-node
  cookiePolicyPage lookup with ICookiePolicyPageResolver
- All copy goes through IConsentTextProvider, removing the inline Swedish
  fallbacks; IDs and buttons use the esatto-consent-/consent-btn names
- Checkbox state reads HasGranted, so a decision made against an older
  PolicyVersion no longer pre-ticks its categories

Co-Authored-By: Claude Opus 5 (1M context) <noreply@anthropic.com>"
```

- [ ] **Step 10: Write the failing `consent-head` test**

Create `Esatto.Umbraco.Backoffice.CookieBanner.Tests/ConsentHeadTagHelperTests.cs`:

```csharp
using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using Esatto.Umbraco.Backoffice.CookieBanner.TagHelpers;
using Microsoft.AspNetCore.Razor.TagHelpers;
using Microsoft.Extensions.Options;
using Xunit;

namespace Esatto.Umbraco.Backoffice.CookieBanner.Tests;

public class ConsentHeadTagHelperTests
{
    private const string StylesheetLink = """<link rel="stylesheet" href="/esatto-cookiebanner/consent.css" />""";

    private static TagHelperContext Context() => new(
        new TagHelperAttributeList(),
        new Dictionary<object, object>(),
        Guid.NewGuid().ToString());

    private static TagHelperOutput Output() => new(
        "consent-head",
        new TagHelperAttributeList(),
        (_, _) => Task.FromResult<TagHelperContent>(new DefaultTagHelperContent()));

    private static TagHelperOutput Render(IConsentState consent, CookieBannerOptions options)
    {
        var helper = new ConsentHeadTagHelper(consent, Options.Create(options));
        TagHelperOutput output = Output();
        helper.Process(Context(), output);
        return output;
    }

    [Fact]
    public void Emits_nothing_google_related_without_a_measurement_id()
    {
        // Pins "no dead script on every page": with no measurement id the Consent Mode block and the
        // gtag tag are absent entirely, not merely inert.
        var html = Render(new FakeConsentState(ConsentCategory.Statistics), new CookieBannerOptions())
            .Content.GetContent();

        Assert.DoesNotContain("gtag", html);
        Assert.DoesNotContain("googletagmanager", html);
        Assert.DoesNotContain("<script", html);
    }

    [Fact]
    public void Emits_defaults_then_update_then_config_in_that_order()
    {
        // Load-bearing order: 'default' must precede any Google tag, the immediately following
        // 'update' closes the 500ms wait_for_update window, and only then does config fire the first
        // page view. Reordering these silently sends wrongly-denied signals.
        var html = Render(
                new FakeConsentState(ConsentCategory.Statistics),
                new CookieBannerOptions { GoogleMeasurementId = "G-ABC123" })
            .Content.GetContent();

        var defaults = html.IndexOf("'consent','default'", StringComparison.Ordinal);
        var update = html.IndexOf("'consent','update'", StringComparison.Ordinal);
        var config = html.IndexOf("gtag('config'", StringComparison.Ordinal);

        Assert.True(defaults >= 0, "the consent default call is missing");
        Assert.True(defaults < update, "the update call must follow the defaults call");
        Assert.True(update < config, "the config call must come last");
        Assert.Contains("'wait_for_update':500", html);
    }

    [Fact]
    public void Always_emits_the_package_stylesheet()
    {
        // The dialog must be styled on every site, whether or not Google Consent Mode is in play.
        Assert.Contains(StylesheetLink, Render(new FakeConsentState(), new CookieBannerOptions()).Content.GetContent());
        Assert.Contains(
            StylesheetLink,
            Render(new FakeConsentState(), new CookieBannerOptions { GoogleMeasurementId = "G-ABC123" })
                .Content.GetContent());
    }

    [Fact]
    public void The_gtag_library_is_gated_on_statistics_consent()
    {
        // Same server-side gate as <consent-script category="Statistics">: with statistics declined
        // the library never reaches the browser, so there is no window in which it could execute.
        var options = new CookieBannerOptions { GoogleMeasurementId = "G-ABC123" };

        Assert.DoesNotContain(
            "googletagmanager.com",
            Render(new FakeConsentState(), options).Content.GetContent());
        Assert.Contains(
            "googletagmanager.com/gtag/js?id=G-ABC123",
            Render(new FakeConsentState(ConsentCategory.Statistics), options).Content.GetContent());
    }

    [Fact]
    public void Leaves_no_consent_head_element_in_the_output()
    {
        // <consent-head> is a marker; an unknown element in <head> would be invalid markup.
        Assert.Null(Render(new FakeConsentState(), new CookieBannerOptions()).TagName);
    }
}
```

- [ ] **Step 11: Run the test to verify it fails**

Run: `dotnet test Esatto.Umbraco.Backoffice.CookieBanner.Tests/Esatto.Umbraco.Backoffice.CookieBanner.Tests.csproj --filter FullyQualifiedName~ConsentHeadTagHelperTests`
Expected: FAIL to compile with `error CS0246: The type or namespace name 'ConsentHeadTagHelper' could not be found`.

- [ ] **Step 12: Implement `ConsentHeadTagHelper`**

Create `Esatto.Umbraco.Backoffice.CookieBanner/src/TagHelpers/ConsentHeadTagHelper.cs`:

```csharp
using System;
using System.Text;
using System.Text.Encodings.Web;
using Microsoft.AspNetCore.Razor.TagHelpers;
using Microsoft.Extensions.Options;

namespace Esatto.Umbraco.Backoffice.CookieBanner.TagHelpers;

/// <summary>
/// Emits everything the package needs inside <c>&lt;head&gt;</c>: the stylesheet, plus - only when a
/// Google measurement id is configured - the Consent Mode v2 block and the gated gtag.js tag.
/// </summary>
/// <remarks>
/// This exists so the Consent Mode call sequence is package-internal rather than copy-pasted into
/// every consumer's layout, where the deliberate second <c>update</c> call reads like a duplicate and
/// invites deletion.
/// </remarks>
[HtmlTargetElement("consent-head", TagStructure = TagStructure.WithoutEndTag)]
public sealed class ConsentHeadTagHelper(
    IConsentState consent,
    IOptions<CookieBannerOptions> options) : TagHelper
{
    /// <summary>
    /// Root-relative on purpose: the file is a static web asset served from the package's wwwroot at
    /// this literal path (StaticWebAssetBasePath=/), and a tag helper builds raw markup with no
    /// IUrlHelper to expand a tilde.
    /// </summary>
    internal const string StylesheetPath = "/esatto-cookiebanner/consent.css";

    public override void Process(TagHelperContext context, TagHelperOutput output)
    {
        // The element itself is only a marker; nothing but its replacement content is rendered.
        output.TagName = null;
        output.TagMode = TagMode.StartTagOnly;

        CookieBannerOptions settings = options.Value;
        HtmlEncoder encoder = HtmlEncoder.Default;
        var head = new StringBuilder();

        head.Append($"""<link rel="stylesheet" href="{StylesheetPath}" />""");

        var measurementId = settings.GoogleMeasurementId;
        if (string.IsNullOrWhiteSpace(measurementId))
        {
            // No measurement id: emit no Google-related markup at all rather than dead script.
            output.Content.SetHtmlContent(head.ToString());
            return;
        }

        // Consent default must run before anything else Google-related. Update runs again here,
        // synchronously, straight after Defaults - even though consent.js also calls it once the
        // page has loaded - because Defaults leaves a 500ms wait_for_update window during which
        // gtag.js (once it loads) would otherwise see only the "denied" defaults. Emitting the real
        // per-request state immediately closes that window rather than relying on consent.js's later
        // call, which may run after gtag.js has already sent its first, wrongly-denied signals.
        // Do not delete this as a duplicate of consent.js's call.
        head.Append("<script>")
            .Append(ConsentModeScript.Defaults())
            .Append(ConsentModeScript.Update(consent))
            .Append(ConsentModeScript.Config(measurementId))
            .Append("</script>");

        // The same server-side gate <consent-script category="Statistics"> applies: with statistics
        // declined the library never reaches the browser, so it cannot execute at all.
        if (consent.HasGranted(ConsentCategory.Statistics))
        {
            var src = "https://www.googletagmanager.com/gtag/js?id=" + Uri.EscapeDataString(measurementId);
            head.Append($"""<script async src="{encoder.Encode(src)}"></script>""");
        }

        output.Content.SetHtmlContent(head.ToString());
    }
}
```

- [ ] **Step 13: Run the test to verify it passes**

Run: `dotnet test Esatto.Umbraco.Backoffice.CookieBanner.Tests/Esatto.Umbraco.Backoffice.CookieBanner.Tests.csproj --filter FullyQualifiedName~ConsentHeadTagHelperTests`
Expected: PASS — 5 passed.

- [ ] **Step 14: Commit `consent-head`**

```bash
git add Esatto.Umbraco.Backoffice.CookieBanner/src/TagHelpers/ConsentHeadTagHelper.cs Esatto.Umbraco.Backoffice.CookieBanner.Tests/ConsentHeadTagHelperTests.cs
git commit -m "Add the consent-head tag helper

- Collapses four Root.cshtml integration points into one element: the
  stylesheet link, the GoogleMeasurementId guard, the Consent Mode
  Defaults/Update/Config block and the statistics-gated gtag.js tag
- Carries across the comment explaining why the second update() call is
  deliberate: it closes the 500ms wait_for_update window
- Tests pin the call order and that nothing Google-related is emitted
  without a measurement id

Co-Authored-By: Claude Opus 5 (1M context) <noreply@anthropic.com>"
```

- [ ] **Step 15: Write the failing `consent-banner` test**

Create `Esatto.Umbraco.Backoffice.CookieBanner.Tests/ConsentBannerTagHelperTests.cs`:

```csharp
using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using Esatto.Umbraco.Backoffice.CookieBanner.TagHelpers;
using Microsoft.AspNetCore.Html;
using Microsoft.AspNetCore.Mvc.Rendering;
using Microsoft.AspNetCore.Mvc.ViewFeatures;
using Microsoft.AspNetCore.Razor.TagHelpers;
using Xunit;

namespace Esatto.Umbraco.Backoffice.CookieBanner.Tests;

public class ConsentBannerTagHelperTests
{
    private const string DialogMarkup = """<dialog id="esatto-consent-dialog"></dialog>""";

    private static TagHelperContext Context() => new(
        new TagHelperAttributeList(),
        new Dictionary<object, object>(),
        Guid.NewGuid().ToString());

    private static TagHelperOutput Output() => new(
        "consent-banner",
        new TagHelperAttributeList(),
        (_, _) => Task.FromResult<TagHelperContent>(new DefaultTagHelperContent()));

    [Fact]
    public async Task Renders_the_ConsentBanner_view_component_in_place_of_the_element()
    {
        // <consent-banner /> must collapse to the dialog markup itself: a leftover <consent-banner>
        // element would be an unknown inline element wrapping a modal dialog.
        var helper = new FakeViewComponentHelper(DialogMarkup);
        var tagHelper = new ConsentBannerTagHelper(helper) { ViewContext = new ViewContext() };
        TagHelperOutput output = Output();

        await tagHelper.ProcessAsync(Context(), output);

        Assert.Equal("ConsentBanner", helper.InvokedName);
        Assert.Null(output.TagName);
        Assert.Equal(DialogMarkup, output.Content.GetContent());
    }

    [Fact]
    public async Task Contextualizes_the_view_component_helper_before_invoking_it()
    {
        // IViewComponentHelper is injected without a ViewContext; invoking it uncontextualized throws
        // InvalidOperationException at request time, which is exactly the bug this pins.
        var helper = new FakeViewComponentHelper(DialogMarkup);
        var tagHelper = new ConsentBannerTagHelper(helper) { ViewContext = new ViewContext() };

        await tagHelper.ProcessAsync(Context(), Output());

        Assert.True(helper.ContextualizedBeforeInvoke);
    }

    private sealed class FakeViewComponentHelper(string html) : IViewComponentHelper, IViewContextAware
    {
        private bool _contextualized;

        public string? InvokedName { get; private set; }

        public bool ContextualizedBeforeInvoke { get; private set; }

        public void Contextualize(ViewContext viewContext) => _contextualized = true;

        public Task<IHtmlContent> InvokeAsync(string name, object? arguments)
        {
            InvokedName = name;
            ContextualizedBeforeInvoke = _contextualized;
            return Task.FromResult<IHtmlContent>(new HtmlString(html));
        }

        public Task<IHtmlContent> InvokeAsync(Type componentType, object? arguments)
            => throw new NotSupportedException("The tag helper must invoke by name.");
    }
}
```

- [ ] **Step 16: Run the test to verify it fails**

Run: `dotnet test Esatto.Umbraco.Backoffice.CookieBanner.Tests/Esatto.Umbraco.Backoffice.CookieBanner.Tests.csproj --filter FullyQualifiedName~ConsentBannerTagHelperTests`
Expected: FAIL to compile with `error CS0246: The type or namespace name 'ConsentBannerTagHelper' could not be found`.

- [ ] **Step 17: Implement `ConsentBannerTagHelper`**

Create `Esatto.Umbraco.Backoffice.CookieBanner/src/TagHelpers/ConsentBannerTagHelper.cs`:

```csharp
using System.Threading.Tasks;
using Microsoft.AspNetCore.Html;
using Microsoft.AspNetCore.Mvc.Rendering;
using Microsoft.AspNetCore.Mvc.ViewFeatures;
using Microsoft.AspNetCore.Razor.TagHelpers;

namespace Esatto.Umbraco.Backoffice.CookieBanner.TagHelpers;

/// <summary>
/// Renders the consent dialog by invoking the <c>ConsentBanner</c> view component.
/// </summary>
/// <remarks>
/// Belongs first inside <c>&lt;body&gt;</c>, before the site header, so the dialog is reachable in
/// DOM order by keyboard.
/// </remarks>
[HtmlTargetElement("consent-banner", TagStructure = TagStructure.WithoutEndTag)]
public sealed class ConsentBannerTagHelper(IViewComponentHelper viewComponentHelper) : TagHelper
{
    /// <summary>
    /// The name MVC registers <see cref="ConsentBannerViewComponent"/> under: the class name minus
    /// its "ViewComponent" suffix.
    /// </summary>
    internal const string ViewComponentName = "ConsentBanner";

    [HtmlAttributeNotBound]
    [ViewContext]
    public ViewContext ViewContext { get; set; } = null!;

    public override async Task ProcessAsync(TagHelperContext context, TagHelperOutput output)
    {
        // IViewComponentHelper is resolved from DI without a ViewContext. Contextualizing it is not
        // optional: invoking it uncontextualized throws at request time.
        ((IViewContextAware)viewComponentHelper).Contextualize(ViewContext);

        IHtmlContent dialog = await viewComponentHelper.InvokeAsync(ViewComponentName, null);

        // The element is a marker, so it is replaced by the dialog rather than wrapping it.
        output.TagName = null;
        output.TagMode = TagMode.StartTagOnly;
        output.Content.SetHtmlContent(dialog);
    }
}
```

- [ ] **Step 18: Run the test to verify it passes**

Run: `dotnet test Esatto.Umbraco.Backoffice.CookieBanner.Tests/Esatto.Umbraco.Backoffice.CookieBanner.Tests.csproj --filter FullyQualifiedName~ConsentBannerTagHelperTests`
Expected: PASS — 2 passed.

- [ ] **Step 19: Commit `consent-banner`**

```bash
git add Esatto.Umbraco.Backoffice.CookieBanner/src/TagHelpers/ConsentBannerTagHelper.cs Esatto.Umbraco.Backoffice.CookieBanner.Tests/ConsentBannerTagHelperTests.cs
git commit -m "Add the consent-banner tag helper

- Replaces Html.PartialAsync(\"_ConsentBanner\") with a single element that
  invokes the packaged ConsentBanner view component
- Contextualizes the injected IViewComponentHelper, which otherwise throws
  at request time, and pins that with a test

Co-Authored-By: Claude Opus 5 (1M context) <noreply@anthropic.com>"
```

- [ ] **Step 20: Write the cookie policy template**

Create `Esatto.Umbraco.Backoffice.CookieBanner/Views/CookiePolicy.cshtml`:

```cshtml
@using Esatto.Umbraco.Backoffice.CookieBanner
@using Microsoft.Extensions.DependencyInjection
@using Umbraco.Cms.Core.Models.Blocks
@using Umbraco.Cms.Core.Models.PublishedContent
@using Umbraco.Cms.Core.Strings
@using Umbraco.Cms.Web.Common.Views
@using Umbraco.Extensions
@inherits UmbracoViewPage
@inject IConsentState Consent
@{
    // No Layout assignment on purpose. The NDSTK original hardcoded Layout = "Root.cshtml", which
    // only works on a site whose layout happens to carry that name; the consumer's
    // Views/_ViewStart.cshtml owns this.

    // Resolved as locals rather than with @inject: an @inject property is a member signature, and
    // IConsentTextProvider is internal to this assembly. A local of an internal type is always legal.
    var text = Context.RequestServices.GetRequiredService<IConsentTextProvider>();
    var fallback = Context.RequestServices.GetRequiredService<IPublishedValueFallback>();

    // One shared grouper, so the tables here and the <details> in the dialog cannot drift apart:
    // unparsable categories and blank cookie names are dropped in both.
    var byCategory = CookieRegistry.Group(
        CookieDeclarationMapper.FromBlockList(Model.Value<BlockListModel>(fallback, "cookies"), fallback));

    // NDSTK rendered these two words as Swedish string literals with no dictionary key, so "on"/"off"
    // was Swedish in every language including English. They are real keys now.
    var onLabel = text.Get("Cookies.Policy.On");
    var offLabel = text.Get("Cookies.Policy.Off");

    var introduction = Model.Value<IHtmlEncodedString>(fallback, "introduction");
    var outro = Model.Value<IHtmlEncodedString>(fallback, "outro");
}

<div class="consent-policy">
    <article>
        <h1>@(Model.Value<string>(fallback, "heading").IfNullOrWhiteSpace(Model.Name))</h1>
        @introduction
    </article>

    <article>
        <h2>@(text.Get("Cookies.Policy.CurrentChoice"))</h2>
        @if (Consent.NeedsDecision)
        {
            <p>@(text.Get("Cookies.Policy.NoChoice"))</p>
        }
        else
        {
            <ul>
                @foreach (var category in ConsentCategories.All)
                {
                    @* HasGranted, not Decision.Granted: it already accounts for Necessary being
                       implied and for a decision made against an older policy version. *@
                    <li>@(text.Get($"Cookies.Category.{category}.Name")): <strong>@(Consent.HasGranted(category) ? onLabel : offLabel)</strong></li>
                }
            </ul>
        }
        <p>
            <button type="button" class="consent-btn consent-btn--primary" data-consent-open>@(text.Get("Cookies.Policy.Reopen"))</button>
            @if (Consent.NeedsDecision is false)
            {
                <button type="button" class="consent-btn consent-btn--secondary" data-consent-action="withdrawn">@(text.Get("Cookies.Policy.Withdraw"))</button>
            }
        </p>
    </article>

    @foreach (var category in ConsentCategories.All)
    {
        var cookies = byCategory[category];
        if (cookies.Count == 0)
        {
            continue;
        }

        <article>
            <h2>@(text.Get($"Cookies.Category.{category}.Name"))</h2>
            <p>@(text.Get($"Cookies.Category.{category}.Description"))</p>

            <div class="cookie-table-wrapper">
                <table class="cookie-table">
                    <thead>
                        <tr>
                            <th scope="col">@(text.Get("Cookies.Table.Name"))</th>
                            <th scope="col">@(text.Get("Cookies.Table.Provider"))</th>
                            <th scope="col">@(text.Get("Cookies.Table.Purpose"))</th>
                            <th scope="col">@(text.Get("Cookies.Table.Duration"))</th>
                            <th scope="col">@(text.Get("Cookies.Table.Type"))</th>
                        </tr>
                    </thead>
                    <tbody>
                        @foreach (var cookie in cookies)
                        {
                            <tr>
                                <td><code>@cookie.Name</code></td>
                                <td>@cookie.Provider</td>
                                <td>@cookie.Purpose</td>
                                <td>@cookie.Duration</td>
                                <td>@cookie.StorageType</td>
                            </tr>
                        }
                    </tbody>
                </table>
            </div>
        </article>
    }

    @if (outro is not null)
    {
        <article>
            @outro
        </article>
    }
</div>
```

- [ ] **Step 21: Build to verify the template compiles**

Run: `dotnet build Esatto.Umbraco.Backoffice.CookieBanner/Esatto.Umbraco.Backoffice.CookieBanner.csproj`
Expected: PASS — `Build succeeded`, 0 errors.

- [ ] **Step 22: Run the whole test project**

Run: `dotnet test Esatto.Umbraco.Backoffice.CookieBanner.Tests/Esatto.Umbraco.Backoffice.CookieBanner.Tests.csproj`
Expected: PASS — every test in the project passes, including the 7 from Task 9 and the 13 added here.

- [ ] **Step 23: Commit the policy template**

```bash
git add Esatto.Umbraco.Backoffice.CookieBanner/Views/CookiePolicy.cshtml
git commit -m "Add the packaged cookie policy template

- Drops the hardcoded Layout = \"Root.cshtml\" so the consumer's _ViewStart
  owns the layout
- Reads the registry through CookieRegistry.Group, so blank cookie names no
  longer render as empty table cells
- Replaces the hardcoded Swedish \"pa\"/\"av\" with the Cookies.Policy.On and
  Cookies.Policy.Off dictionary keys

Co-Authored-By: Claude Opus 5 (1M context) <noreply@anthropic.com>"
```

### Task 11: Rework the stylesheet to be self-sufficient and themeable

**Files:**
- Create: `c:\src\Esatto.Packages\Esatto.Umbraco.Backoffice.CookieBanner\wwwroot\esatto-cookiebanner\consent.css`
- Test: no unit test (CSS) — verified by grep plus a static browser harness at `C:\Users\carl_\AppData\Local\Temp\claude\c--src-NDSTK\4466d809-d9b8-40cc-8366-57f3fb1b8bcd\scratchpad\consent-css-harness.html`

**Interfaces:**
- Consumes: nothing at runtime. Source of truth for the rewrite is `c:\src\NDSTK\wwwroot\static\css\consent.css` (242 lines) and the five tokens it borrowed from `c:\src\NDSTK\wwwroot\static\css\site.css:1-7` (`--primary: #001F54`, `--accent: #F7E300`, `--bg: #E5E6E8`, `--text: #222`, `--muted: #666`) plus `site.css:130-139` (`.btn-primary { background: var(--accent); color: var(--primary); padding: 0.6rem 1.2rem; border-radius: 4px; font-weight: bold; }`).
- Produces (the contract every view, tag helper and consumer theme binds to):
  - Static asset URL: `/esatto-cookiebanner/consent.css`
  - Element ids: `#esatto-consent-dialog`, `#esatto-consent-dialog-heading`
  - Button classes: `.consent-btn`, `.consent-btn--primary`, `.consent-btn--secondary`, `.consent-btn--link`
  - Structural classes (unchanged from NDSTK): `.consent-dialog`, `.consent-dialog__body`, `.consent-dialog__categories`, `.consent-dialog__actions`, `.consent-status`, `.consent-category`, `.consent-category__row`, `.consent-category__cookies`, `.consent-cookie`, `.consent-embed`, `.consent-embed--blocked`, `.cookie-table-wrapper`, `.cookie-table`
  - Theming tokens on `:root`: `--consent-surface`, `--consent-surface-subtle`, `--consent-text`, `--consent-heading`, `--consent-muted`, `--consent-border`, `--consent-border-strong`, `--consent-rule`, `--consent-backdrop`, `--consent-focus`, `--consent-radius`, `--consent-radius-md`, `--consent-radius-sm`, `--consent-btn-primary-bg`, `--consent-btn-primary-fg`, `--consent-btn-primary-border`, `--consent-btn-secondary-bg`, `--consent-btn-secondary-fg`, `--consent-btn-secondary-border`, `--consent-btn-link-fg`

- [ ] **Step 1: Write the failing test**

The guarantee to pin: the shipped stylesheet must reference no host-owned custom property, no host button class, and no host `footer`. Write the three checks as a script and point them at the NDSTK source that is being replaced, so the checks are proven to actually detect the defects before the rewrite.

```bash
cat > "$TMP/consent-css-checks.sh" <<'SH'
#!/bin/sh
# Guarantee: the package stylesheet is self-sufficient - it must borrow no host token,
# no host button class, and must never restyle a host's page furniture.
target="$1"
echo "--- host tokens (expect no output) ---"
grep -nE 'var\(--(primary|accent|bg|text|muted)\)' "$target"
echo "--- host button classes (expect no output) ---"
grep -nE '(^|[^-])\.btn-(primary|secondary|link)' "$target"
echo "--- host page furniture (expect no output) ---"
grep -ni 'footer' "$target"
echo "--- baked-in brand navy scrim (expect no output) ---"
grep -nE 'rgba\(0, *31, *84' "$target"
SH
chmod +x "$TMP/consent-css-checks.sh"
```

(`$TMP` = `C:\Users\carl_\AppData\Local\Temp\claude\c--src-NDSTK\4466d809-d9b8-40cc-8366-57f3fb1b8bcd\scratchpad`.)

- [ ] **Step 2: Run the test to verify it fails**

Run: `sh "$TMP/consent-css-checks.sh" /c/src/NDSTK/wwwroot/static/css/consent.css`

Expected: FAIL — every section prints hits, proving the checks bite:
```
--- host tokens (expect no output) ---
7:    color: var(--primary);
21:    background: var(--primary);
--- host button classes (expect no output) ---
19:.btn-secondary {
37:.consent-dialog .btn-primary,
--- host page furniture (expect no output) ---
231:footer .btn-link {
--- baked-in brand navy scrim (expect no output) ---
72:    background: rgba(0, 31, 84, 0.6);
```
(24 `var(--*)` hits, 8 `.btn-*` hits, 4 `footer` hits, 1 scrim hit in total.)

- [ ] **Step 3: Implement**

Write `c:\src\Esatto.Packages\Esatto.Umbraco.Backoffice.CookieBanner\wwwroot\esatto-cookiebanner\consent.css`:

```css
/* Esatto.Umbraco.Backoffice.CookieBanner - consent dialog, blocked embeds and cookie policy tables.
 *
 * Self-sufficient by design. Every colour, radius and metric comes from a --consent-* token
 * declared below, so the package renders correctly with no host stylesheet at all, and a consumer
 * re-themes it by redeclaring the tokens on :root (or on any ancestor) - never by overriding rules.
 *
 * Token names are prefixed because generic names like --primary or --bg are too likely to already
 * mean something else in the consumer's design system. Fonts are deliberately absent: every rule
 * uses font-family: inherit, so the dialog picks up the host's type without a token.
 */

:root {
    /* Surfaces and type */
    --consent-surface: #ffffff;
    --consent-surface-subtle: #f4f5f7;
    --consent-text: #1f2328;
    --consent-heading: #10141a;
    --consent-muted: #5f6570;

    /* Lines. --consent-rule is the 2px emphasis rule under headings and table headers. */
    --consent-border: #d5d7db;
    --consent-border-strong: #a8aeb8;
    --consent-rule: #c9cdd4;

    /* The modal scrim. A token because a hardcoded brand colour here means overriding the
       button tokens still leaves the visitor looking at somebody else's brand behind the dialog. */
    --consent-backdrop: rgba(16, 20, 26, 0.6);

    /* Focus ring colour. See the :focus-visible rule - do not set this to the surface colour. */
    --consent-focus: #10141a;

    --consent-radius: 8px;
    --consent-radius-md: 6px;
    --consent-radius-sm: 4px;

    /* Buttons. Primary is the filled affordance used for every decision in the dialog; secondary
       is the quieter outlined variant used for withdrawal on the policy page. */
    --consent-btn-primary-bg: #10141a;
    --consent-btn-primary-fg: #ffffff;
    --consent-btn-primary-border: #10141a;
    --consent-btn-secondary-bg: #ffffff;
    --consent-btn-secondary-fg: #10141a;
    --consent-btn-secondary-border: #a8aeb8;
    --consent-btn-link-fg: #10141a;
}

/* Buttons are fully self-contained: the package must not depend on a host button class, or it
   renders as unstyled browser default buttons on every site but the one it grew up in.
   The base carries a 1px transparent border so filled and outlined variants share box metrics
   exactly - the filled variant must not be one pixel taller than the outlined one. */
.consent-btn {
    display: inline-block;
    margin: 0;
    padding: 0.6rem 1.2rem;
    border: 1px solid transparent;
    border-radius: var(--consent-radius-sm);
    background: none;
    font-weight: bold;
    font-size: 1rem;
    font-family: inherit;
    line-height: 1.2;
    text-align: center;
    text-decoration: none;
    cursor: pointer;
}

    .consent-btn:hover {
        text-decoration: underline;
    }

/* Accept and reject both use .consent-btn--primary, so they are genuinely identical and
   distinguished only by their labels. An earlier version gave reject a differently coloured filled
   variant: the box metrics matched, but equal size with unequal colour salience is not equal
   weight - and the more salient button read as "already selected". */
.consent-btn--primary {
    background: var(--consent-btn-primary-bg);
    border-color: var(--consent-btn-primary-border);
    color: var(--consent-btn-primary-fg);
}

.consent-btn--secondary {
    background: var(--consent-btn-secondary-bg);
    border-color: var(--consent-btn-secondary-border);
    color: var(--consent-btn-secondary-fg);
}

.consent-btn--link {
    display: inline;
    padding: 0.6rem 0.4rem;
    border: none;
    background: none;
    color: var(--consent-btn-link-fg);
    font: inherit;
    text-decoration: underline;
    cursor: pointer;
}

/* Failure feedback for a save/withdraw request. Not an error banner in its own right - same text
   colour as the rest of the dialog - the message content is what signals "this went wrong". */
.consent-status {
    flex: 1 1 100%;
    margin: 0;
    font-weight: 700;
    color: var(--consent-text);
}

.consent-status[hidden] {
    display: none;
}

/* First-run and settings dialog - a single native <dialog>, so focus trap, Esc and the inert
   backdrop come from the platform. */
.consent-dialog {
    border: none;
    border-radius: var(--consent-radius);
    padding: 0;
    max-width: 34rem;
    max-height: 90vh;
    overflow-y: auto;
    width: calc(100% - 2rem);
    color: var(--consent-text);
    background: var(--consent-surface);
}

.consent-dialog::backdrop {
    background: var(--consent-backdrop);
}

.consent-dialog__body {
    padding: 1.5rem;
}

    .consent-dialog__body h2 {
        color: var(--consent-heading);
        margin: 0 0 1rem;
        border-bottom: 2px solid var(--consent-rule);
        padding-bottom: 0.5rem;
    }

    .consent-dialog__body > p {
        margin: 0 0 1rem;
    }

.consent-category {
    border: 1px solid var(--consent-border);
    border-radius: var(--consent-radius-md);
    margin: 0 0 1rem;
    padding: 0.75rem 1rem 1rem;
}

    .consent-category legend {
        color: var(--consent-heading);
        font-weight: 700;
        padding: 0 0.35rem;
    }

.consent-category__row {
    display: flex;
    gap: 0.75rem;
    align-items: flex-start;
    font-size: 0.95rem;
}

    .consent-category__row input {
        margin-top: 0.2rem;
        width: 1.15rem;
        height: 1.15rem;
        flex: 0 0 auto;
    }

/* Per-category cookie facts - lets a visitor inspect what a category covers without leaving the
   dialog. Collapsed by default (see the <details> in the markup) to keep the modal compact. */
.consent-category__cookies {
    margin-top: 0.6rem;
    font-size: 0.9rem;
}

    .consent-category__cookies summary {
        color: var(--consent-heading);
        cursor: pointer;
        font-weight: 600;
    }

/* One definition list per cookie - stacked label/value pairs rather than a table, so the facts
   stay readable at phone widths inside a modal a visitor cannot skip. */
.consent-cookie {
    margin: 0.6rem 0 0;
    padding: 0.6rem 0.75rem;
    background: var(--consent-surface-subtle);
    border-radius: var(--consent-radius-sm);
}

    .consent-cookie dt {
        margin: 0.5rem 0 0;
        font-size: 0.8rem;
        font-weight: 700;
        color: var(--consent-muted);
        text-transform: uppercase;
        letter-spacing: 0.03em;
    }

    .consent-cookie dt:first-child {
        margin-top: 0;
    }

    .consent-cookie dd {
        margin: 0.1rem 0 0;
    }

/* An author-origin `display` beats the user agent's `[hidden] { display: none }`, and
   .consent-btn sets `display: inline-block` - so `hidden` alone would leave the Save button and the
   category section on screen. Stated once for the whole dialog rather than per element. */
.consent-dialog [hidden] {
    display: none;
}

.consent-dialog__actions {
    display: flex;
    gap: 0.75rem;
    flex-wrap: wrap;
    margin-top: 1.25rem;
}

/* Visible focus everywhere - this is a keyboard-driven component by design. Do not remove: a
   consent dialog that blocks the page and gives no focus indication is unusable by keyboard. */
.consent-dialog :focus-visible,
.consent-embed :focus-visible {
    outline: 3px solid var(--consent-focus);
    outline-offset: 2px;
}

/* The heading takes focus when the dialog opens, purely so focus starts inside the dialog. It is
   not interactive and cannot be tabbed to, so a ring on it would signal a control that is not
   there. Every actual control keeps its ring via the rule above. */
#esatto-consent-dialog-heading:focus,
#esatto-consent-dialog-heading:focus-visible {
    outline: none;
}

/* Blocked embed placeholder */
.consent-embed--blocked {
    background: var(--consent-surface-subtle);
    border: 1px dashed var(--consent-border-strong);
    border-radius: var(--consent-radius-md);
    padding: 1.5rem;
    text-align: center;
}

.consent-embed iframe {
    width: 100%;
    aspect-ratio: 16 / 9;
    border: 0;
    border-radius: var(--consent-radius-md);
}

/* Cookie policy tables. Wide content scrolls inside its own container so the page body never does. */
.cookie-table-wrapper {
    overflow-x: auto;
}

.cookie-table {
    width: 100%;
    border-collapse: collapse;
    font-size: 0.9rem;
}

    .cookie-table th,
    .cookie-table td {
        text-align: left;
        vertical-align: top;
        padding: 0.5rem 0.65rem;
        border-bottom: 1px solid var(--consent-border);
    }

    .cookie-table th {
        color: var(--consent-heading);
        border-bottom: 2px solid var(--consent-rule);
        white-space: nowrap;
    }

    .cookie-table code {
        word-break: break-all;
    }
```

What changed relative to `c:\src\NDSTK\wwwroot\static\css\consent.css`, rule by rule:

| NDSTK source | New |
|---|---|
| — | `:root` token layer (20 tokens, neutral defaults) |
| `.btn-secondary` (19-31), `.btn-secondary:hover` (33-35), `.consent-dialog .btn-primary, .consent-dialog .btn-secondary, .consent-embed .btn-primary` (37-45), `.btn-link` (47-55) | `.consent-btn` + `:hover` + the three modifiers. The 37-45 override rule disappears entirely: `margin`, `border`, `font-size`, `font-family` and `cursor` are now on the base class, so there is nothing left to reset inside the dialog |
| `rgba(0, 31, 84, 0.6)` (72) | `var(--consent-backdrop)` |
| `#d5d7db` (91, 218) | `var(--consent-border)` (the hex survives once, as the token's default value) |
| `background: white` (68) | `var(--consent-surface)` — first of the two literal-white uses |
| `color: white` (22) | `var(--consent-btn-secondary-fg)` — second literal-white use |
| `.consent-dialog__body h2:focus{,-visible}` (181-184) | `#esatto-consent-dialog-heading:focus{,-visible}` — prefixed id, and more precise than a descendant `h2` selector |
| `footer .btn-link`, `footer a`, `footer p` (231-242) | **deleted**, no replacement |
| `.consent-status { color: var(--primary) }` | `var(--consent-text)`, which is what its own comment always claimed it was |
| everything else | identical structure, native `<dialog>`, flex actions row, `font-family: inherit` |

- [ ] **Step 4: Run the test to verify it passes**

Run (from `c:\src\Esatto.Packages`):
```bash
sh "$TMP/consent-css-checks.sh" Esatto.Umbraco.Backoffice.CookieBanner/wwwroot/esatto-cookiebanner/consent.css
```
Expected: PASS — the four section headers print with **no lines between them**:
```
--- host tokens (expect no output) ---
--- host button classes (expect no output) ---
--- host page furniture (expect no output) ---
--- baked-in brand navy scrim (expect no output) ---
```

Then the positive counterparts, all from `c:\src\Esatto.Packages`:
```bash
# every token is declared exactly once, on :root
grep -c -- '--consent-' Esatto.Umbraco.Backoffice.CookieBanner/wwwroot/esatto-cookiebanner/consent.css
# the four contract button classes exist
grep -nE '^\.consent-btn(--(primary|secondary|link))? \{' Esatto.Umbraco.Backoffice.CookieBanner/wwwroot/esatto-cookiebanner/consent.css
# #d5d7db survives only as a token default
grep -n 'd5d7db' Esatto.Umbraco.Backoffice.CookieBanner/wwwroot/esatto-cookiebanner/consent.css
# font-family: inherit is still there
grep -c 'font-family: inherit\|font: inherit' Esatto.Umbraco.Backoffice.CookieBanner/wwwroot/esatto-cookiebanner/consent.css
```
Expected: `59`; four `.consent-btn*` rule lines; exactly one `d5d7db` line (`    --consent-border: #d5d7db;`); `2`.

Manual check — the stylesheet must look right with **no host stylesheet at all**. Write the harness once (it is reused by Task 12):

```bash
cat > "$TMP/consent-css-harness.html" <<'HTML'
<!doctype html>
<meta charset="utf-8">
<title>consent.css harness</title>
<link rel="stylesheet" href="/c/src/Esatto.Packages/Esatto.Umbraco.Backoffice.CookieBanner/wwwroot/esatto-cookiebanner/consent.css">
<p>Host page text. No host stylesheet is loaded.</p>
<dialog id="esatto-consent-dialog" class="consent-dialog" aria-labelledby="esatto-consent-dialog-heading">
  <div class="consent-dialog__body">
    <h2 id="esatto-consent-dialog-heading" tabindex="-1">We use cookies</h2>
    <p>Short explanatory paragraph.</p>
    <div class="consent-dialog__categories" data-consent-categories>
      <fieldset class="consent-category">
        <legend>Statistics</legend>
        <div class="consent-category__row">
          <input type="checkbox" id="c-stat" value="statistics" data-consent-category-input>
          <label for="c-stat">Helps us understand how the site is used.</label>
        </div>
        <details class="consent-category__cookies">
          <summary>Cookies in this category</summary>
          <dl class="consent-cookie"><dt>Name</dt><dd><code>_ga</code></dd><dt>Duration</dt><dd>2 years</dd></dl>
        </details>
      </fieldset>
    </div>
    <p class="consent-status" role="status" aria-live="polite" hidden data-consent-status></p>
    <div class="consent-dialog__actions">
      <button type="button" class="consent-btn consent-btn--primary" data-consent-action="accept-all">Accept all</button>
      <button type="button" class="consent-btn consent-btn--primary" data-consent-action="reject-all">Reject all</button>
      <button type="button" class="consent-btn consent-btn--primary" data-consent-customise>Customise</button>
      <button type="button" class="consent-btn consent-btn--primary" data-consent-action="custom" hidden>Save choices</button>
      <button type="button" class="consent-btn consent-btn--secondary" data-consent-action="withdrawn">Withdraw consent</button>
      <button type="button" class="consent-btn consent-btn--link" data-consent-open>Cookie settings</button>
    </div>
  </div>
</dialog>
<script>document.getElementById('esatto-consent-dialog').showModal();</script>
HTML
```

Open `$TMP/consent-css-harness.html` in a browser and confirm all six:
1. The dialog is a white rounded panel, max 34rem wide, centred over a **dark translucent grey** scrim (not navy).
2. All five box buttons are styled — filled dark for the four primaries, white/outlined for Withdraw — and none is the browser default grey. Accept all and Reject all are pixel-identical apart from their labels; measure with devtools that both are the same height as Withdraw (this is what the 1px transparent border buys).
3. "Cookie settings" renders as an underlined text link, not a box.
4. `Save choices` is invisible (proves `.consent-dialog [hidden]` still beats `display: inline-block`).
5. Tab through the controls: each shows a 3px outline offset 2px. Then run `document.getElementById('esatto-consent-dialog-heading').focus()` in the console — the heading gets **no** ring.
6. Paste `document.documentElement.style.setProperty('--consent-btn-primary-bg','#7b1fa2')` in the console: the four primary buttons turn purple immediately, and nothing else in the dialog changes. That is the whole re-theming contract in one command.

- [ ] **Step 5: Commit**

```bash
git add Esatto.Umbraco.Backoffice.CookieBanner/wwwroot/esatto-cookiebanner/consent.css
git commit -m "Add a self-sufficient themeable consent stylesheet" -m "- Declare a 20-token --consent-* layer on :root with neutral defaults, so the
  package no longer borrows --primary/--accent/--bg/--text/--muted from a host
- Replace the host .btn-primary/.btn-secondary/.btn-link dependency with
  self-contained .consent-btn plus --primary/--secondary/--link modifiers, and
  drop the in-dialog override rule those classes needed
- Tokenise the hardcoded rgba(0, 31, 84, 0.6) backdrop and the #d5d7db borders
- Prefix the heading focus rule onto #esatto-consent-dialog-heading
- Drop the three footer rules: a package must not restyle a host's page furniture
- Point .consent-status at the dialog text colour its own comment describes

Co-Authored-By: Claude Opus 5 (1M context) <noreply@anthropic.com>"
```

---

### Task 12: Move and de-brand the client script

**Files:**
- Create: `c:\src\Esatto.Packages\Esatto.Umbraco.Backoffice.CookieBanner\wwwroot\esatto-cookiebanner\consent.js`
- Test: no unit test (there is no JS test harness in the mono-repo — see the design spec's open item on a `Client/` vitest project). Verified by grep plus the browser harness from Task 11.

**Interfaces:**
- Consumes:
  - `CookieBannerOptions.EndpointPath` default `"/api/cookie-consent"` and `CookieName` default `"cookie-consent"` — the script's own fallbacks must match these, because they are what runs if the view component ever emits the tag without attributes.
  - The seven script data attributes emitted by `ConsentBannerViewComponent`: `data-consent-endpoint`, `data-consent-cookie`, `data-consent-version`, `data-consent-mode`, `data-consent-needs-decision`, `data-consent-error-message`, `data-consent-rate-limited-message`.
  - The wire format written by `ConsentCookieCodec.Encode` — `{"v":<int>,"t":"<ISO-8601 offset>","c":[...],"id":"<base64url>"}`, URL-encoded exactly once by `Response.Cookies.Append`.
  - DOM ids and hooks from `Views/Shared/Components/ConsentBanner/Default.cshtml`: `#esatto-consent-dialog`, `#esatto-consent-dialog-heading`, `[data-consent-categories]`, `[data-consent-category-input]`, `[data-consent-status]`, `[data-consent-action]`, `[data-consent-open]`, `[data-consent-customise]`, `[data-consent-close]`.
  - `ConsentRequest(string[]? Categories, string Action)` — the request body has exactly two fields.
- Produces:
  - Static asset URL: `/esatto-cookiebanner/consent.js`
  - `window.cookieConsent = { open, close, get, has, onChange }` — `open([isReopen])`, `close()`, `get()` returns `{version, decidedAt, categories, consentId}` or `null`, `has(category)` returns boolean, `onChange(fn)` registers a callback receiving `{categories, version}`
  - `document`-level `CustomEvent` `cookieconsent:change`, `detail = { categories, version }`
  - POST body shape: `{"categories":["statistics"],"action":"custom"}`

**Rename safety — verified, not assumed.** `grep -rn "ndstkConsent\|ndstk:consent-change" c:\src\NDSTK` returns hits in exactly three kinds of place: the definition itself (`wwwroot/static/js/consent.js:255,393`), design/plan/report prose under `docs/superpowers/` and `.superpowers/sdd/`, and stored review diffs. **No `.cshtml`, `.cs`, `.js` or `.css` file anywhere in NDSTK reads `window.ndstkConsent` or listens for `ndstk:consent-change`.** The rename to `window.cookieConsent` / `cookieconsent:change` therefore breaks no caller, in this repo or in the site.

- [ ] **Step 1: Write the failing test**

The guarantee to pin: a package published to nuget.org must carry no trace of the site it was extracted from, and no Swedish string literals. Write the checks and aim them at the NDSTK source first.

```bash
cat > "$TMP/consent-js-checks.sh" <<'SH'
#!/bin/sh
# Guarantee: the shipped script is de-branded and English-only. A package that hardcodes
# another site's name or another country's language is not shippable.
target="$1"
echo "--- site branding (expect no output) ---"
grep -ni 'ndstk' "$target"
echo "--- Swedish characters (expect no output) ---"
grep -nP '[\x{00E5}\x{00E4}\x{00F6}\x{00C5}\x{00C4}\x{00D6}]' "$target"
echo "--- stale API names (expect no output) ---"
grep -nE 'ndstkConsent|ndstk:consent-change|#consent-dialog\b' "$target"
SH
chmod +x "$TMP/consent-js-checks.sh"
```

If this `grep` build lacks `-P`, substitute `grep -n '[åäöÅÄÖ]' "$target"` — equivalent on a UTF-8 file, just locale-dependent.

- [ ] **Step 2: Run the test to verify it fails**

Run: `sh "$TMP/consent-js-checks.sh" /c/src/NDSTK/wwwroot/static/js/consent.js`

Expected: FAIL — all three sections print hits:
```
--- site branding (expect no output) ---
2: * NDSTK cookie consent.
15:    var cookieName = script.getAttribute('data-consent-cookie') || 'ndstk-consent';
136:                console.warn('ndstk-consent: dialog.showModal is not supported; ...
157:                console.warn('ndstk-consent: the consent dialog could not be displayed; ...
255:        document.dispatchEvent(new CustomEvent('ndstk:consent-change', { detail: detail }));
393:    window.ndstkConsent = {
--- Swedish characters (expect no output) ---
19:    var errorMessage = script.getAttribute('data-consent-error-message') || 'Något gick fel. Försök igen.';
21:        || 'Du har försökt för många gånger. Vänta en stund och försök igen.';
--- stale API names (expect no output) ---
65:    var dialog = document.getElementById('consent-dialog');
85:        var heading = dialog.querySelector('#consent-dialog-heading');
208:            if (event.target === dialog.querySelector('#consent-dialog-heading')) { return; }
255:        document.dispatchEvent(new CustomEvent('ndstk:consent-change', { detail: detail }));
393:    window.ndstkConsent = {
```

- [ ] **Step 3: Implement**

Write `c:\src\Esatto.Packages\Esatto.Umbraco.Backoffice.CookieBanner\wwwroot\esatto-cookiebanner\consent.js`. This is `c:\src\NDSTK\wwwroot\static\js\consent.js` with the renames applied and **nothing else touched** — the focus-reclaim arming, the two-layer Escape suppression, the single `decodeURIComponent`, the `state.version < policyVersion` downgrade rule, `activateScripts`, and the `gtag('consent','update')` mapping are byte-for-byte the behaviour four NDSTK commits converged on:

```javascript
/**
 * Esatto cookie consent.
 *
 * Deliberately dependency-free and self-hosted: a consent tool that itself calls out to a third
 * party would undercut its own purpose.
 *
 * The server is the source of truth. This script never writes the consent cookie - it posts the
 * choice and lets the endpoint set it, which is what guarantees the cookie's attributes are right.
 */
(function () {
    'use strict';

    var script = document.currentScript;
    var endpoint = script.getAttribute('data-consent-endpoint') || '/api/cookie-consent';
    var cookieName = script.getAttribute('data-consent-cookie') || 'cookie-consent';
    var policyVersion = parseInt(script.getAttribute('data-consent-version') || '1', 10);
    var consentModeEnabled = script.getAttribute('data-consent-mode') === 'on';
    var needsDecision = script.getAttribute('data-consent-needs-decision') === 'true';
    var errorMessage = script.getAttribute('data-consent-error-message') || 'Something went wrong. Please try again.';
    var rateLimitedMessage = script.getAttribute('data-consent-rate-limited-message')
        || 'You have tried too many times. Please wait a moment and try again.';

    // Set only when open() finds the dialog cannot actually be displayed. Stops the 'close'
    // handler from reopening an invisible modal in a loop.
    var blockingAbandoned = false;

    // Armed by open(); consumed by the first focus change after it. See the 'focusin' handler.
    var reclaimFocus = false;

    var listeners = [];

    function readCookie() {
        var prefix = cookieName + '=';
        var parts = document.cookie ? document.cookie.split('; ') : [];

        for (var i = 0; i < parts.length; i++) {
            if (parts[i].indexOf(prefix) !== 0) { continue; }
            try {
                var parsed = JSON.parse(decodeURIComponent(parts[i].substring(prefix.length)));
                if (!parsed || typeof parsed.v !== 'number') { return null; }
                return {
                    version: parsed.v,
                    decidedAt: parsed.t,
                    categories: Array.isArray(parsed.c) ? parsed.c : [],
                    consentId: parsed.id
                };
            } catch (error) {
                return null;
            }
        }

        return null;
    }

    function currentCategories() {
        var state = readCookie();
        if (!state || state.version < policyVersion) { return []; }
        return state.categories;
    }

    function has(category) {
        return category === 'necessary' || currentCategories().indexOf(category) !== -1;
    }

    var dialog = document.getElementById('esatto-consent-dialog');

    /**
     * True once the dialog actually occupies space in the layout - not merely `open`. Guards
     * against a zero-height dialog (a stylesheet conflict, or one stripped by a browser
     * extension) leaving the visitor stuck behind a dimmed, unusable page.
     */
    function isDisplayed(element) {
        var box = element.getBoundingClientRect();
        return box.width > 0 && box.height > 0;
    }

    /**
     * Put focus on the dialog's heading. The heading carries tabindex="-1" and is not interactive,
     * so focus starts inside the dialog - keeping the focus trap and screen-reader announcement -
     * without any control appearing pre-selected.
     */
    function focusHeading() {
        if (!dialog || dialog.open === false) { return; }

        var heading = dialog.querySelector('#esatto-consent-dialog-heading');
        if (!heading || typeof heading.focus !== 'function') { return; }

        // preventScroll matters: the dialog scrolls internally, and focusing an element scrolls it
        // into view by default. Without this, pressing Escape part-way down the cookie list yanked
        // the dialog back to the top. Older browsers ignore the options object and simply scroll,
        // which is the pre-existing behaviour rather than a new failure.
        heading.focus({ preventScroll: true });
    }

    /**
     * First run renders Accept all and Reject all only, plus the control that reveals per-category
     * choice. Revealing swaps that control for Save and moves focus into the section it opened: the
     * control the visitor just activated is about to be hidden, and focus left on a hidden element
     * falls out of the modal entirely.
     */
    function revealCategories(trigger) {
        if (!dialog) { return; }

        var categories = dialog.querySelector('[data-consent-categories]');
        var save = dialog.querySelector('[data-consent-action="custom"]');

        if (categories) { categories.hidden = false; }
        if (save) { save.hidden = false; }
        if (trigger) { trigger.hidden = true; }

        var firstInput = categories
            && categories.querySelector('[data-consent-category-input]:not([disabled])');

        if (firstInput && typeof firstInput.focus === 'function') {
            firstInput.focus({ preventScroll: true });
        } else {
            focusHeading();
        }
    }

    /**
     * @param {boolean} isReopen True only when reopening because the dialog was closed with no
     *   decision recorded. The focus reclaim is armed for that case ONLY: on a first open there is
     *   no restoration to fight, and arming it would steal the visitor's first deliberate click.
     */
    function open(isReopen) {
        if (!dialog) { return; }

        var dialogSupported = typeof HTMLDialogElement === 'function'
            && typeof dialog.showModal === 'function';

        if (!dialogSupported) {
            // No native modal <dialog> support: still offer the choice, just not modally -
            // an unusable site is worse than a non-blocking one.
            if (window.console) {
                console.warn('cookie-consent: dialog.showModal is not supported; showing the cookie choice without blocking the page.');
            }
            dialog.setAttribute('open', 'open');
            return;
        }

        dialog.showModal();

        // Closing a <dialog> restores focus to whatever was focused before it opened, and that can
        // land after this handler has run - stealing focus back to the control the visitor had
        // clicked, which then shows a focus ring on it. Racing it with a timer is unreliable, so on
        // a reopen arm a one-shot reclaim instead: focus the heading, then take focus back from
        // whatever grabs it next. Ordering-independent, and it disarms immediately.
        focusHeading();
        if (isReopen === true) { reclaimFocus = true; }

        if (!isDisplayed(dialog)) {
            // showModal() ran but the dialog is not actually visible (a CSS conflict, a browser
            // extension removed it, etc.). A dimmed, invisible modal traps the visitor worse
            // than no consent UI at all, so fail open instead.
            if (window.console) {
                console.warn('cookie-consent: the consent dialog could not be displayed; leaving the page usable.');
            }
            blockingAbandoned = true;
            dialog.close();
        }
    }

    function close() {
        if (!dialog) { return; }
        if (typeof dialog.close === 'function') {
            dialog.close();
        } else {
            dialog.removeAttribute('open');
        }
    }

    // While no decision has been made yet, there is nothing to cancel back to, so Escape must not
    // dismiss the choice. Two layers are needed, because one is not enough:
    //
    // 1. preventDefault() on 'cancel'. This works once the visitor has interacted with the page,
    //    but browsers deliberately ignore it for a dialog opened WITHOUT user activation - which is
    //    exactly our case, since the dialog opens on load. That is anti-abuse behaviour by design
    //    (a page must not be able to trap you), so it cannot be argued with.
    // 2. Reopen on 'close' whenever no decision has been recorded. That covers the first Escape,
    //    which layer 1 cannot. After it, user activation exists and layer 1 handles the rest.
    if (dialog) {
        dialog.addEventListener('cancel', function (event) {
            if (needsDecision === false) { return; }

            event.preventDefault();

            // Escape was swallowed, so the dialog stays put - but the keypress has switched the
            // browser into keyboard modality, which makes :focus-visible start matching whatever
            // already had focus. A control the visitor merely clicked (focused without a ring)
            // suddenly grows one, reading as though Escape had selected it. Moving focus to the
            // non-interactive heading leaves nothing for a ring to be drawn on.
            focusHeading();
        });

        dialog.addEventListener('close', function () {
            // blockingAbandoned means open() already determined the dialog cannot be displayed and
            // closed it on purpose. Reopening then would loop forever on an invisible modal.
            if (needsDecision && blockingAbandoned === false) { open(true); }
        });

        // The one-shot reclaim armed by open(). Fires for the browser's post-close focus
        // restoration and hands focus back to the heading, then disarms.
        dialog.addEventListener('focusin', function (event) {
            if (reclaimFocus === false) { return; }

            reclaimFocus = false;
            if (event.target === dialog.querySelector('#esatto-consent-dialog-heading')) { return; }

            // Blur first: that clears the ring the browser has already drawn on the control,
            // rather than leaving it painted while focus moves elsewhere.
            if (event.target && typeof event.target.blur === 'function') { event.target.blur(); }
            focusHeading();
        });
    }

    /** Turn inert type="text/plain" placeholders into live scripts for the granted categories. */
    function activateScripts() {
        var blocked = document.querySelectorAll('script[type="text/plain"][data-consent-category]');

        Array.prototype.forEach.call(blocked, function (placeholder) {
            if (!has(placeholder.getAttribute('data-consent-category'))) { return; }

            var live = document.createElement('script');
            var src = placeholder.getAttribute('data-src');

            if (src) {
                live.src = src;
            } else {
                live.text = placeholder.textContent;
            }

            placeholder.parentNode.replaceChild(live, placeholder);
        });
    }

    function updateConsentMode() {
        if (!consentModeEnabled || typeof window.gtag !== 'function') { return; }

        var marketing = has('marketing') ? 'granted' : 'denied';

        window.gtag('consent', 'update', {
            ad_storage: marketing,
            ad_user_data: marketing,
            ad_personalization: marketing,
            analytics_storage: has('statistics') ? 'granted' : 'denied',
            functionality_storage: has('preferences') ? 'granted' : 'denied',
            personalization_storage: has('preferences') ? 'granted' : 'denied'
        });
    }

    function announce() {
        var detail = { categories: currentCategories(), version: policyVersion };

        document.dispatchEvent(new CustomEvent('cookieconsent:change', { detail: detail }));
        listeners.forEach(function (listener) {
            try { listener(detail); } catch (error) { /* a bad subscriber must not break consent */ }
        });
    }

    function selectedCategories() {
        var inputs = document.querySelectorAll('[data-consent-category-input]');

        return Array.prototype.filter.call(inputs, function (input) {
            return input.checked && !input.disabled;
        }).map(function (input) {
            return input.value;
        });
    }

    function statusElements() {
        return document.querySelectorAll('[data-consent-status]');
    }

    function actionButtons() {
        return document.querySelectorAll('[data-consent-action]');
    }

    /** role="status"/aria-live elements, so screen reader users hear a failure too, not just see it. */
    function showStatus(message) {
        Array.prototype.forEach.call(statusElements(), function (element) {
            element.textContent = message;
            element.hidden = false;
        });
    }

    function clearStatus() {
        Array.prototype.forEach.call(statusElements(), function (element) {
            element.textContent = '';
            element.hidden = true;
        });
    }

    /** Prevents a double-click (or a slow request plus an impatient second click) from firing twice. */
    function setActionButtonsDisabled(disabled) {
        Array.prototype.forEach.call(actionButtons(), function (button) {
            button.disabled = disabled;
        });
    }

    function send(action, categories) {
        return fetch(endpoint, {
            method: 'POST',
            headers: { 'Content-Type': 'application/json' },
            credentials: 'same-origin',
            body: JSON.stringify({
                categories: categories,
                action: action
            })
        }).then(function (response) {
            if (!response.ok) {
                var error = new Error('Consent request failed: ' + response.status);
                error.status = response.status;
                throw error;
            }
            return response.json();
        }).then(function () {
            // A decision now demonstrably exists (the server accepted it and set the cookie):
            // Escape and the cancel affordance behave normally on any future reopen this page.
            // The Cancel button itself is server-rendered and stays absent until the next
            // navigation - only Escape-suppression needs to be lifted here.
            //
            // This must be cleared BEFORE close(), because the 'close' handler reopens the dialog
            // whenever it closes with no decision recorded. Closing first would bounce it straight
            // back open on the success path.
            needsDecision = false;

            close();
            clearStatus();
            activateScripts();
            updateConsentMode();
            announce();
            return true;
        }).catch(function (error) {
            // Leave the dialog in place: a failed request must not read as a recorded choice.
            if (window.console) { console.error(error); }
            showStatus(error && error.status === 429 ? rateLimitedMessage : errorMessage);
            return false;
        });
    }

    function decide(action) {
        clearStatus();
        setActionButtonsDisabled(true);

        var result;
        if (action === 'accept-all') { result = send(action, ['preferences', 'statistics', 'marketing']); }
        else if (action === 'reject-all') { result = send(action, []); }
        else if (action === 'withdrawn') {
            // Reload only on success: `send` resolves false (never rejects) on a failed
            // request, and a failed withdrawal must not look like a completed one.
            result = send(action, []).then(function (succeeded) {
                if (succeeded) { window.location.reload(); }
                return succeeded;
            });
        } else {
            result = send('custom', selectedCategories());
        }

        return result.then(function (succeeded) {
            setActionButtonsDisabled(false);
            return succeeded;
        });
    }

    document.addEventListener('click', function (event) {
        var target = event.target;
        // This handler lives at the document level for the life of the page, so guard against
        // any click target that is not an Element (e.g. a Text node reached via composed paths).
        if (!target || typeof target.closest !== 'function') { return; }

        var opener = target.closest('[data-consent-open]');
        if (opener) { event.preventDefault(); open(); return; }

        var customiser = target.closest('[data-consent-customise]');
        if (customiser) { event.preventDefault(); revealCategories(customiser); return; }

        var closer = target.closest('[data-consent-close]');
        if (closer) { event.preventDefault(); close(); return; }

        var actor = target.closest('[data-consent-action]');
        if (actor) { event.preventDefault(); decide(actor.getAttribute('data-consent-action')); }
    });

    // Anything already granted from a previous visit becomes live on this page load too.
    activateScripts();
    updateConsentMode();

    // No decision yet: block the site until one is made.
    if (needsDecision) { open(); }

    window.cookieConsent = {
        open: open,
        close: close,
        get: readCookie,
        has: has,
        onChange: function (fn) { if (typeof fn === 'function') { listeners.push(fn); } }
    };
})();
```

Exhaustive diff against the NDSTK source — nine edits, no tenth:

| NDSTK line | Change |
|---|---|
| 2 | `NDSTK cookie consent.` → `Esatto cookie consent.` |
| 14 | endpoint fallback `'/api/consent'` → `'/api/cookie-consent'` (matches `CookieBannerOptions.EndpointPath`) |
| 15 | cookie fallback `'ndstk-consent'` → `'cookie-consent'` (matches `CookieBannerOptions.CookieName`) |
| 19 | `'Något gick fel. Försök igen.'` → `'Something went wrong. Please try again.'` |
| 20-21 | `'Du har försökt för många gånger…'` → `'You have tried too many times. Please wait a moment and try again.'` |
| 65 | `getElementById('consent-dialog')` → `'esatto-consent-dialog'` |
| 85, 208 | `querySelector('#consent-dialog-heading')` → `'#esatto-consent-dialog-heading'` |
| 136, 157 | `console.warn('ndstk-consent: …')` → `'cookie-consent: …'` |
| 255 | `'ndstk:consent-change'` → `'cookieconsent:change'` |
| 309 | `culture: document.documentElement.lang \|\| null` **removed** from the POST body |
| 393 | `window.ndstkConsent` → `window.cookieConsent` |

Line 309 is the one non-rename removal, and it is mandated rather than opportunistic: the contract's `ConsentRequest(string[]? Categories, string Action)` has no `Culture`, and the design spec drops the consent-log scaffolding `Culture` existed for. Sending a field no model binds is dead weight on every request.

Deliberately **not** touched, because four NDSTK commits (`fa42007`, `33a7ed7`, `417cde9`, `ae78a23`, `fcf3572`) converged on exactly this shape: the `reclaimFocus` one-shot and its `isReopen === true` arming condition; the `blockingAbandoned` guard; the two-layer `cancel`/`close` Escape suppression; `focus({ preventScroll: true })` in both call sites; the single `decodeURIComponent` in `readCookie`; the `state.version < policyVersion` downgrade rule; `activateScripts`; the six-signal `gtag('consent','update')` map; and the reload-only-on-success withdrawal.

- [ ] **Step 4: Run the test to verify it passes**

Run (from `c:\src\Esatto.Packages`):
```bash
sh "$TMP/consent-js-checks.sh" Esatto.Umbraco.Backoffice.CookieBanner/wwwroot/esatto-cookiebanner/consent.js
```
Expected: PASS — the three headers print with **no lines between them**:
```
--- site branding (expect no output) ---
--- Swedish characters (expect no output) ---
--- stale API names (expect no output) ---
```

Then the positive checks, and the proof that the preserved logic is intact:
```bash
f=Esatto.Umbraco.Backoffice.CookieBanner/wwwroot/esatto-cookiebanner/consent.js
grep -nE "window\.cookieConsent|cookieconsent:change|esatto-consent-dialog" "$f"
grep -c 'decodeURIComponent' "$f"
grep -n 'state.version < policyVersion' "$f"
grep -n 'if (isReopen === true) { reclaimFocus = true; }' "$f"
grep -n 'culture' "$f"
```
Expected: five lines (65, 85, 208, 255-equivalent, 393-equivalent); `1`; one hit; one hit; **no output** for `culture`.

Manual check — extend the Task 11 harness so it loads the real script and confirm the behaviour survived the move:
```bash
python -m http.server 8099 --directory "$TMP"   # any static server; file:// blocks fetch
```
Append to `$TMP/consent-css-harness.html` (replacing the bare `showModal()` line):
```html
<script src="/c/src/Esatto.Packages/Esatto.Umbraco.Backoffice.CookieBanner/wwwroot/esatto-cookiebanner/consent.js"
        data-consent-endpoint="/nowhere"
        data-consent-cookie="cookie-consent"
        data-consent-version="1"
        data-consent-mode="off"
        data-consent-needs-decision="true"></script>
```
Open `http://localhost:8099/consent-css-harness.html` and confirm all seven:
1. The dialog opens modally on load with no click (`needsDecision="true"`), and focus is on the heading with no ring.
2. Press Escape twice — the dialog stays open both times, focus returns to the heading, and the internal scroll position does not jump (scroll halfway down first to test this properly).
3. Click Customise — the category section and Save appear, Customise disappears, focus lands on the `statistics` checkbox.
4. Click Accept all — the POST to `/nowhere` 404s, the status paragraph appears reading exactly **"Something went wrong. Please try again."** in English, and the dialog stays open (a failed request must not read as a recorded choice).
5. Console: `typeof window.cookieConsent` → `"object"`, and `window.ndstkConsent` → `undefined`.
6. Console: `window.cookieConsent.has('necessary')` → `true`; `window.cookieConsent.has('statistics')` → `false`; `window.cookieConsent.get()` → `null` (no cookie).
7. Console: `document.addEventListener('cookieconsent:change', e => console.log(e.detail))`, then `document.cookie = 'cookie-consent=' + encodeURIComponent('{"v":1,"t":"2026-08-23T00:00:00+02:00","c":["statistics"],"id":"aaaaaaaaaaaaaaaaaaaaaa"}')` and reload — `window.cookieConsent.get().categories` → `["statistics"]`, proving the single-`decodeURIComponent` reader still matches `ConsentCookieCodec.Encode`'s wire format.

- [ ] **Step 5: Commit**

```bash
git add Esatto.Umbraco.Backoffice.CookieBanner/wwwroot/esatto-cookiebanner/consent.js
git commit -m "Move the consent client script into the package and de-brand it" -m "- Rename window.ndstkConsent to window.cookieConsent and the ndstk:consent-change
  event to cookieconsent:change; verified neither has a consumer anywhere in NDSTK
  outside its own definition and design-doc prose, so no caller breaks
- Replace the two Swedish fallback literals with English
- Point the fallbacks at the package defaults /api/cookie-consent and cookie-consent
- Move to the prefixed #esatto-consent-dialog / #esatto-consent-dialog-heading ids
- Drop the culture field from the POST body; ConsentRequest no longer binds it
- Leave the focus reclaim, the two-layer Escape suppression, the single
  decodeURIComponent, the policy-version downgrade rule, activateScripts and the
  Consent Mode update byte-for-byte as they are

Co-Authored-By: Claude Opus 5 (1M context) <noreply@anthropic.com>"
```

### Task 13: Copy the content-type factory into the package

**Why copied, not shared:** `NdstkContentTypeFactory` carries a *mutable* `Dictionary<Guid, IDataType> _dataTypes` cache that only `PreloadDataTypesAsync` fills, and `Property()` **throws** `InvalidOperationException` for any key that was not preloaded. Two independent installers (NDSTK's site model and this package's schema) each own a different preload set. Sharing one singleton instance would mean either installer's ordering bug becomes the other's runtime failure, and a shared assembly would have to publish that cache as part of its contract. 210 generic lines of duplication is cheaper than a third shared assembly plus that coupling. The design doc states this explicitly (`2026-08-23-cookiebanner-package-design.md:87-91`).

**Umbraco 17/18 rules applied while copying:** the source is already clean — it uses `ITemplateService` (not `IFileService`), `IDataTypeService`, `IContentTypeService.Get(Guid)` (which lives on `IContentTypeBaseService<IContentType>` and is present in both 17.0.0 and 18.1.1), no `MigrationBase`, no `IPublishedContent.Parent`/`.Children`, no `IContentService`. So the only changes are the namespace, the type name, and the `<see cref>` retarget. All `Umbraco.Cms...` references stay as file-level `using` directives with short type names — never inline.

**Files:**
- Create: `Esatto.Umbraco.Backoffice.CookieBanner/src/CookieBannerContentTypeFactory.cs`
- Test: `Esatto.Umbraco.Backoffice.CookieBanner.Tests/CookieBannerContentTypeFactoryTests.cs`

**Interfaces:**
- Consumes: the package csproj (`net10.0`, `Microsoft.NET.Sdk.Razor`, `Umbraco.Cms.Core` 17.0.0, `InternalsVisibleTo Esatto.Umbraco.Backoffice.CookieBanner.Tests`) and the test csproj (xunit 2.9.2, NSubstitute 5.3.0)
- Produces:
  - `internal sealed class CookieBannerContentTypeFactory(IContentTypeService contentTypeService, IDataTypeService dataTypeService, ITemplateService templateService, PropertyEditorCollection propertyEditors, IConfigurationEditorJsonSerializer configurationSerializer, IShortStringHelper shortStringHelper)`
  - `Task<ITemplate> EnsureTemplateAsync(Guid key, string name, string alias, string content)`
  - `Task<IDataType> EnsureDataTypeAsync(Guid key, string name, string editorAlias, string editorUiAlias, IDictionary<string, object>? configuration = null)`
  - `Task PreloadDataTypesAsync(params Guid[] keys)`
  - `IPropertyType Property(Guid dataTypeKey, string alias, string name, string? description = null, int sortOrder = 0)`
  - `Task<IContentType> EnsureContentTypeAsync(Guid key, string alias, string name, string icon, Action<IContentType> configure)`
  - `Task SetAllowedChildrenAsync(Guid key, params (Guid Key, string Alias)[] children)`
  - `static void AddGroup(IContentType contentType, Guid key, string alias, string caption, int sortOrder, params IPropertyType[] properties)`
  - `static void UseTemplate(IContentType contentType, ITemplate template)`

**What these tests do and do not cover — stated plainly.** The mono-repo contains **zero** factories of this kind today (`grep -rl 'PreloadDataTypesAsync\|ContentTypeFactory' --include=*.cs .` over `c:\src\Esatto.Packages` returns nothing), so there is no existing pattern to copy and no existing coverage to port. Four of the nine members — `EnsureDataTypeAsync`, `EnsureContentTypeAsync`, `SetAllowedChildrenAsync`, and the create branch of `EnsureTemplateAsync` — call Umbraco services that write to the database and return `Attempt<,>` values produced by a booted CMS. Faking those would only assert that NSubstitute returns what NSubstitute was told to return. They are deliberately **left to the Task 17 integration check** (install against a real site, then verify the six artefacts exist with the right aliases). What is covered below is the part that is pure, cheap, and where the real regression risk lives: the `_dataTypes` cache contract (`Property()` throwing on a non-preloaded key — the exact reason this file is copied rather than shared), the fail-fast in `PreloadDataTypesAsync`, the create-if-missing short-circuit, and the two static helpers.

- [ ] **Step 1: Write the failing test**

```csharp
using NSubstitute;
using Umbraco.Cms.Core.Models;
using Umbraco.Cms.Core.Serialization;
using Umbraco.Cms.Core.Services;
using Umbraco.Cms.Core.Strings;
using Xunit;

namespace Esatto.Umbraco.Backoffice.CookieBanner.Tests;

public class CookieBannerContentTypeFactoryTests
{
    // Umbraco's built-in Textstring data type key, used purely as a stable stand-in.
    private static readonly Guid TextstringKey = new("0cc0eba1-9960-42c9-bf9b-60e150b429ae");
    private static readonly Guid MissingKey = new("00000000-dead-4000-8000-000000000001");

    private readonly IContentTypeService _contentTypes = Substitute.For<IContentTypeService>();
    private readonly IDataTypeService _dataTypes = Substitute.For<IDataTypeService>();
    private readonly ITemplateService _templates = Substitute.For<ITemplateService>();
    private readonly IConfigurationEditorJsonSerializer _serializer =
        Substitute.For<IConfigurationEditorJsonSerializer>();
    private readonly IShortStringHelper _shortStrings = Substitute.For<IShortStringHelper>();

    private CookieBannerContentTypeFactory CreateFactory()
    {
        _shortStrings.CleanStringForSafeAlias(Arg.Any<string>()).Returns(call => call.Arg<string>());

        // propertyEditors is read only by EnsureDataTypeAsync, which needs a booted Umbraco and is
        // covered by the Task 17 integration check; null! keeps these tests off that object graph.
        return new CookieBannerContentTypeFactory(
            _contentTypes, _dataTypes, _templates, null!, _serializer, _shortStrings);
    }

    private static IDataType FakeDataType(Guid key)
    {
        var dataType = Substitute.For<IDataType>();
        dataType.Key.Returns(key);
        dataType.EditorAlias.Returns("Umbraco.TextBox");
        dataType.EditorUiAlias.Returns("Umb.PropertyEditorUi.TextBox");
        return dataType;
    }

    // Pins the cache contract that is the whole reason this factory is COPIED rather than shared:
    // Property() must fail loudly when the install order forgot to preload the data type.
    [Fact]
    public void Property_throws_when_the_data_type_was_not_preloaded()
    {
        CookieBannerContentTypeFactory factory = CreateFactory();

        InvalidOperationException error = Assert.Throws<InvalidOperationException>(
            () => factory.Property(MissingKey, "category", "Category"));

        Assert.Contains("was not preloaded", error.Message);
    }

    // Pins that PreloadDataTypesAsync fills the cache and that Property() copies every declared
    // field through, including Variations.Nothing (an invariant property on an invariant type).
    [Fact]
    public async Task Property_returns_a_property_type_bound_to_the_preloaded_data_type()
    {
        _dataTypes.GetAsync(TextstringKey).Returns(FakeDataType(TextstringKey));
        CookieBannerContentTypeFactory factory = CreateFactory();

        await factory.PreloadDataTypesAsync(TextstringKey);
        IPropertyType property = factory.Property(
            TextstringKey, "cookieName", "Name", "Literal name or pattern, e.g. _ga_*", 4);

        Assert.Equal("cookieName", property.Alias);
        Assert.Equal("Name", property.Name);
        Assert.Equal("Literal name or pattern, e.g. _ga_*", property.Description);
        Assert.Equal(4, property.SortOrder);
        Assert.Equal(ContentVariation.Nothing, property.Variations);
        Assert.Equal(TextstringKey, property.DataTypeKey);
    }

    // Pins the fail-fast: a missing built-in data type must abort the install with the key in the
    // message, not silently produce an element type with no properties.
    [Fact]
    public async Task PreloadDataTypesAsync_throws_when_the_data_type_does_not_exist()
    {
        _dataTypes.GetAsync(MissingKey).Returns((IDataType?)null);
        CookieBannerContentTypeFactory factory = CreateFactory();

        InvalidOperationException error = await Assert.ThrowsAsync<InvalidOperationException>(
            () => factory.PreloadDataTypesAsync(MissingKey));

        Assert.Contains(MissingKey.ToString(), error.Message);
    }

    // Pins create-if-missing: an existing template is returned untouched so backoffice edits to
    // the cookie policy view survive an app restart.
    [Fact]
    public async Task EnsureTemplateAsync_returns_the_existing_template_without_creating_it()
    {
        Guid key = new("c00c1e00-0004-4000-8000-000000000001");
        var existing = Substitute.For<ITemplate>();
        _templates.GetAsync(key).Returns(existing);
        CookieBannerContentTypeFactory factory = CreateFactory();

        ITemplate result = await factory.EnsureTemplateAsync(key, "Cookie policy", "CookiePolicy", "@* x *@");

        Assert.Same(existing, result);
        await _templates.DidNotReceive().CreateAsync(Arg.Any<ITemplate>(), Arg.Any<Guid>());
    }

    // Pins that a group is added as a Tab with the caption and sort order given, and that the
    // property list is carried into it in declaration order.
    [Fact]
    public async Task AddGroup_adds_one_tab_carrying_the_declared_properties()
    {
        _dataTypes.GetAsync(TextstringKey).Returns(FakeDataType(TextstringKey));
        CookieBannerContentTypeFactory factory = CreateFactory();
        await factory.PreloadDataTypesAsync(TextstringKey);

        var contentType = Substitute.For<IContentType>();
        contentType.PropertyGroups.Returns(new PropertyGroupCollection());
        Guid groupKey = new("c00c1e00-0002-4000-8000-000000000081");

        CookieBannerContentTypeFactory.AddGroup(
            contentType, groupKey, "content", "Content", 0,
            factory.Property(TextstringKey, "cookieName", "Name", sortOrder: 0),
            factory.Property(TextstringKey, "provider", "Provider", sortOrder: 1));

        PropertyGroup group = Assert.Single(contentType.PropertyGroups);
        Assert.Equal(groupKey, group.Key);
        Assert.Equal("content", group.Alias);
        Assert.Equal("Content", group.Name);
        Assert.Equal(PropertyGroupType.Tab, group.Type);
        Assert.Equal(0, group.SortOrder);
        Assert.Equal(
            new[] { "cookieName", "provider" },
            group.PropertyTypes!.Select(property => property.Alias));
    }

    // Pins that UseTemplate does BOTH halves: allowing a template without setting it as the
    // default leaves the cookie policy page rendering the host's fallback view.
    [Fact]
    public void UseTemplate_allows_the_template_and_makes_it_the_default()
    {
        var contentType = Substitute.For<IContentType>();
        var template = Substitute.For<ITemplate>();

        CookieBannerContentTypeFactory.UseTemplate(contentType, template);

        contentType.Received().AllowedTemplates =
            Arg.Is<IEnumerable<ITemplate>>(templates => templates.Single() == template);
        contentType.Received().SetDefaultTemplate(template);
    }
}
```

- [ ] **Step 2: Run the test to verify it fails**

Run: `dotnet test Esatto.Umbraco.Backoffice.CookieBanner.Tests/Esatto.Umbraco.Backoffice.CookieBanner.Tests.csproj --filter FullyQualifiedName~CookieBannerContentTypeFactoryTests`
Expected: FAIL to build with `error CS0246: The type or namespace name 'CookieBannerContentTypeFactory' could not be found (are you missing a using directive or an assembly reference?)` — reported once per use site in `CookieBannerContentTypeFactoryTests.cs`.

- [ ] **Step 3: Implement**

```csharp
using Umbraco.Cms.Core;
using Umbraco.Cms.Core.Models;
using Umbraco.Cms.Core.PropertyEditors;
using Umbraco.Cms.Core.Serialization;
using Umbraco.Cms.Core.Services;
using Umbraco.Cms.Core.Strings;

namespace Esatto.Umbraco.Backoffice.CookieBanner;

/// <summary>
/// Thin wrapper over the Umbraco services that turns the declarative descriptions in
/// <see cref="CookieBannerSchemaInstaller"/> into persisted schema. Every Ensure* method is
/// create-if-missing: an entity that already exists is returned untouched, so changes made in
/// the backoffice survive an app restart.
/// </summary>
/// <remarks>
/// This is a deliberate copy of NDSTK's <c>NdstkContentTypeFactory</c> rather than a shared
/// dependency. The <see cref="_dataTypes"/> cache is mutable per instance and
/// <see cref="Property"/> throws for a key that was never preloaded, so one singleton shared
/// between two independent installers would turn either installer's ordering mistake into the
/// other's runtime failure. Duplicating 200 generic lines is cheaper than that coupling.
/// </remarks>
internal sealed class CookieBannerContentTypeFactory(
    IContentTypeService contentTypeService,
    IDataTypeService dataTypeService,
    ITemplateService templateService,
    PropertyEditorCollection propertyEditors,
    IConfigurationEditorJsonSerializer configurationSerializer,
    IShortStringHelper shortStringHelper)
{
    private const int RootParentId = -1;
    private static readonly Guid UserKey = Constants.Security.SuperUserKey;

    private readonly Dictionary<Guid, IDataType> _dataTypes = [];

    public async Task<ITemplate> EnsureTemplateAsync(Guid key, string name, string alias, string content)
    {
        ITemplate? existing = await templateService.GetAsync(key);
        if (existing is not null)
        {
            return existing;
        }

        var template = new Template(shortStringHelper, name, alias)
        {
            Key = key,
            Content = content,
        };

        var attempt = await templateService.CreateAsync(template, UserKey);
        if (attempt.Success is false)
        {
            throw new InvalidOperationException($"Could not create template '{alias}': {attempt.Status}.");
        }

        return attempt.Result!;
    }

    public async Task<IDataType> EnsureDataTypeAsync(
        Guid key,
        string name,
        string editorAlias,
        string editorUiAlias,
        IDictionary<string, object>? configuration = null)
    {
        IDataType? existing = await dataTypeService.GetAsync(key);
        if (existing is not null)
        {
            return existing;
        }

        if (propertyEditors.TryGet(editorAlias, out IDataEditor? editor) is false)
        {
            throw new InvalidOperationException($"No property editor is registered for alias '{editorAlias}'.");
        }

        var dataType = new DataType(editor, configurationSerializer, RootParentId)
        {
            Key = key,
            Name = name,
            EditorUiAlias = editorUiAlias,
        };

        dataType.SetConfigurationData(configuration ?? new Dictionary<string, object>());

        var attempt = await dataTypeService.CreateAsync(dataType, UserKey);
        if (attempt.Success is false)
        {
            throw new InvalidOperationException($"Could not create data type '{name}': {attempt.Status}.");
        }

        return attempt.Result;
    }

    /// <summary>
    /// Loads the data types that <see cref="Property"/> will bind to. Doing it up front keeps the
    /// schema declarations synchronous and therefore readable.
    /// </summary>
    public async Task PreloadDataTypesAsync(params Guid[] keys)
    {
        foreach (Guid key in keys.Distinct().Where(key => _dataTypes.ContainsKey(key) is false))
        {
            _dataTypes[key] = await dataTypeService.GetAsync(key)
                              ?? throw new InvalidOperationException($"Data type {key} was not found.");
        }
    }

    /// <summary>Builds a property type bound to one of the preloaded data types.</summary>
    public IPropertyType Property(
        Guid dataTypeKey,
        string alias,
        string name,
        string? description = null,
        int sortOrder = 0)
    {
        if (_dataTypes.TryGetValue(dataTypeKey, out IDataType? dataType) is false)
        {
            throw new InvalidOperationException($"Data type {dataTypeKey} was not preloaded.");
        }

        return new PropertyType(shortStringHelper, dataType, alias)
        {
            Name = name,
            Description = description,
            SortOrder = sortOrder,
            Variations = ContentVariation.Nothing,
        };
    }

    /// <summary>
    /// Creates a document type or element type when it is missing. <paramref name="configure"/>
    /// only runs for a brand new type, so existing schema is never rewritten.
    /// </summary>
    public async Task<IContentType> EnsureContentTypeAsync(
        Guid key,
        string alias,
        string name,
        string icon,
        Action<IContentType> configure)
    {
        IContentType? existing = contentTypeService.Get(key);
        if (existing is not null)
        {
            return existing;
        }

        var contentType = new ContentType(shortStringHelper, RootParentId)
        {
            Key = key,
            Alias = alias,
            Name = name,
            Icon = icon,
            Variations = ContentVariation.Nothing,
        };

        configure(contentType);

        var attempt = await contentTypeService.CreateAsync(contentType, UserKey);
        if (attempt.Success is false)
        {
            throw new InvalidOperationException($"Could not create content type '{alias}': {attempt.Result}.");
        }

        return contentTypeService.Get(key)
               ?? throw new InvalidOperationException($"Content type '{alias}' was created but could not be read back.");
    }

    /// <summary>
    /// Applies the allowed-children list in a second pass, once every document type exists.
    /// </summary>
    public async Task SetAllowedChildrenAsync(Guid key, params (Guid Key, string Alias)[] children)
    {
        IContentType contentType = contentTypeService.Get(key)
                                   ?? throw new InvalidOperationException($"Content type {key} was not found.");

        ContentTypeSort[] desired = children
            .Select((child, index) => new ContentTypeSort(child.Key, index, child.Alias))
            .ToArray();

        HashSet<Guid> current = contentType.AllowedContentTypes?.Select(x => x.Key).ToHashSet() ?? [];
        if (current.SetEquals(desired.Select(x => x.Key)))
        {
            return;
        }

        contentType.AllowedContentTypes = desired;

        var attempt = await contentTypeService.UpdateAsync(contentType, UserKey);
        if (attempt.Success is false)
        {
            throw new InvalidOperationException($"Could not set allowed children on '{contentType.Alias}': {attempt.Result}.");
        }
    }

    public static void AddGroup(
        IContentType contentType,
        Guid key,
        string alias,
        string caption,
        int sortOrder,
        params IPropertyType[] properties)
        => contentType.PropertyGroups.Add(new PropertyGroup(true)
        {
            Key = key,
            Alias = alias,
            Name = caption,
            Type = PropertyGroupType.Tab,
            SortOrder = sortOrder,
            PropertyTypes = new PropertyTypeCollection(true, properties),
        });

    public static void UseTemplate(IContentType contentType, ITemplate template)
    {
        contentType.AllowedTemplates = [template];
        contentType.SetDefaultTemplate(template);
    }
}
```

- [ ] **Step 4: Run the test to verify it passes**

Run: `dotnet test Esatto.Umbraco.Backoffice.CookieBanner.Tests/Esatto.Umbraco.Backoffice.CookieBanner.Tests.csproj --filter FullyQualifiedName~CookieBannerContentTypeFactoryTests`
Expected: PASS — `Passed! - Failed: 0, Passed: 6, Skipped: 0`

- [ ] **Step 5: Commit**

```bash
git add Esatto.Umbraco.Backoffice.CookieBanner/src/CookieBannerContentTypeFactory.cs Esatto.Umbraco.Backoffice.CookieBanner.Tests/CookieBannerContentTypeFactoryTests.cs
git commit -m "Copy the NDSTK content type factory into the CookieBanner package"
```

---

### Task 14: Install the six schema artefacts

The cookie-related parts of `c:\src\NDSTK\ContentModel\NdstkContentModelInstaller.cs` extracted here are: the ordering comment plus call and preload at lines 42-46; `InstallCookieDataTypesAsync` at 97-128 (the two dropdowns); the `cookieDefinition` element type declaration at 180-187; the `CookieRegistry` Block List at 264-275; the `cookiePolicy` document type at 373-383; and the `(Templates.CookiePolicy, "Cookie policy", "CookiePolicy")` template row at line 71. Everything else in that file is NDSTK site model and stays behind.

Two deliberate departures from the NDSTK source:

1. **Property descriptions become English.** NDSTK's `duration` description is `"\"12 månader\", \"Session\""` (line 186) and its `provider` description is `"NDSTK, Google, YouTube…"` (line 183). Aliases and property aliases are *unchanged* (`cookieDefinition`, `cookieName`, `provider`, `category`, `purpose`, `duration`, `storageType`, `cookiePolicy`, `heading`, `introduction`, `cookies`, `outro`) so NDSTK content stays portable onto package-owned schema.
2. **The template content is the packaged view, not a stub.** `ITemplateService.CreateAsync` writes a physical `Views/CookiePolicy.cshtml` into the consumer app (this is exactly why NDSTK's `InstallTemplatesAsync` reads the real file first — see its comment at lines 57-61). Because Umbraco enables Razor runtime compilation, that physical file *shadows* the RCL-compiled `Views/CookiePolicy.cshtml`. Handing Umbraco a bare `@inherits` stub would therefore blank the policy page. So the view is additionally embedded as a manifest resource and its own source is what gets written — one file, shipped twice, byte-identical on both sides, and editable in the backoffice afterwards.

**Files:**
- Create: `Esatto.Umbraco.Backoffice.CookieBanner/src/CookieBannerKeys.cs`
- Create: `Esatto.Umbraco.Backoffice.CookieBanner/src/CookieBannerSchemaInstaller.cs`
- Modify: `Esatto.Umbraco.Backoffice.CookieBanner/Esatto.Umbraco.Backoffice.CookieBanner.csproj` (add one `ItemGroup` embedding `Views\CookiePolicy.cshtml`)
- Test: `Esatto.Umbraco.Backoffice.CookieBanner.Tests/CookieBannerKeysTests.cs`
- Test: `Esatto.Umbraco.Backoffice.CookieBanner.Tests/CookieBannerSchemaInstallerTests.cs`

**Interfaces:**
- Consumes:
  - `internal sealed class CookieBannerContentTypeFactory(IContentTypeService, IDataTypeService, ITemplateService, PropertyEditorCollection, IConfigurationEditorJsonSerializer, IShortStringHelper)` (Task 13)
  - `Task<ITemplate> CookieBannerContentTypeFactory.EnsureTemplateAsync(Guid key, string name, string alias, string content)`
  - `Task<IDataType> CookieBannerContentTypeFactory.EnsureDataTypeAsync(Guid key, string name, string editorAlias, string editorUiAlias, IDictionary<string, object>? configuration = null)`
  - `Task CookieBannerContentTypeFactory.PreloadDataTypesAsync(params Guid[] keys)`
  - `IPropertyType CookieBannerContentTypeFactory.Property(Guid dataTypeKey, string alias, string name, string? description = null, int sortOrder = 0)`
  - `Task<IContentType> CookieBannerContentTypeFactory.EnsureContentTypeAsync(Guid key, string alias, string name, string icon, Action<IContentType> configure)`
  - `static void CookieBannerContentTypeFactory.AddGroup(IContentType, Guid, string, string, int, params IPropertyType[])`
  - `static void CookieBannerContentTypeFactory.UseTemplate(IContentType, ITemplate)`
  - `static IReadOnlyList<ConsentCategory> ConsentCategories.All`
  - `static string ConsentCategories.ToWireName(ConsentCategory category)`
  - the RCL view file `Esatto.Umbraco.Backoffice.CookieBanner/Views/CookiePolicy.cshtml`
- Produces:
  - `internal static class CookieBannerKeys` with nested `DataTypes.CookieCategory`, `DataTypes.StorageType`, `DataTypes.CookieRegistry`, `ElementTypes.CookieDefinition`, `DocumentTypes.CookiePolicy`, `Templates.CookiePolicy`, `BuiltInDataTypes.Textstring`, `BuiltInDataTypes.Textarea`, `BuiltInDataTypes.RichtextEditor` — all `internal static readonly Guid`
  - `internal sealed class CookieBannerSchemaInstaller(CookieBannerContentTypeFactory factory, ILogger<CookieBannerSchemaInstaller> logger)`
  - `Task CookieBannerSchemaInstaller.InstallAsync()`
  - `internal static readonly string[] CookieBannerSchemaInstaller.CookieCategoryItems`
  - `internal static readonly string[] CookieBannerSchemaInstaller.StorageTypeItems`
  - `internal static Dictionary<string, object> CookieBannerSchemaInstaller.CookieRegistryConfiguration()`
  - `internal static string CookieBannerSchemaInstaller.ReadPackagedTemplate()`

**What these tests do and do not cover — stated plainly.** `InstallAsync()` is six awaited calls into a booted Umbraco; asserting `factory.Received().EnsureDataTypeAsync(...)` six times against a substitute would just restate the implementation line for line and would still not prove the *ordering* holds, because a substituted `PreloadDataTypesAsync` never populates the real cache that `Property()` reads. **The ordering guarantee — dropdowns preloaded before the element type binds, Block List after element types, template before document type — is therefore verified in Task 17's integration check, not here.** What is unit-tested is the data the installer carries, where a silent typo is both likely and expensive: the two dropdown item arrays (wrong strings mean the policy page silently groups nothing), the Block List's single allowed element type, the six contract GUIDs, and the fact that the template content really is the packaged markup and carries no hardcoded host layout.

- [ ] **Step 1: Write the failing test for the key registry**

```csharp
using Xunit;

namespace Esatto.Umbraco.Backoffice.CookieBanner.Tests;

public class CookieBannerKeysTests
{
    // Pins the six schema GUIDs to the published contract. These are written into a consumer's
    // database on install, so changing one orphans the artefact and the next boot creates a
    // duplicate alongside it.
    [Fact]
    public void Schema_keys_match_the_published_contract_guids()
    {
        Assert.Equal(new Guid("c00c1e00-0001-4000-8000-000000000001"), CookieBannerKeys.DataTypes.CookieCategory);
        Assert.Equal(new Guid("c00c1e00-0001-4000-8000-000000000002"), CookieBannerKeys.DataTypes.StorageType);
        Assert.Equal(new Guid("c00c1e00-0001-4000-8000-000000000003"), CookieBannerKeys.DataTypes.CookieRegistry);
        Assert.Equal(new Guid("c00c1e00-0002-4000-8000-000000000001"), CookieBannerKeys.ElementTypes.CookieDefinition);
        Assert.Equal(new Guid("c00c1e00-0003-4000-8000-000000000001"), CookieBannerKeys.DocumentTypes.CookiePolicy);
        Assert.Equal(new Guid("c00c1e00-0004-4000-8000-000000000001"), CookieBannerKeys.Templates.CookiePolicy);
    }

    // Pins that the package's own keys are distinct from each other and from the NDSTK series they
    // replaced: reusing an NDSTK GUID would make the package adopt (and then rewrite) site schema.
    [Fact]
    public void Schema_keys_are_distinct_and_share_no_ground_with_the_ndstk_series()
    {
        Guid[] keys =
        [
            CookieBannerKeys.DataTypes.CookieCategory,
            CookieBannerKeys.DataTypes.StorageType,
            CookieBannerKeys.DataTypes.CookieRegistry,
            CookieBannerKeys.ElementTypes.CookieDefinition,
            CookieBannerKeys.DocumentTypes.CookiePolicy,
            CookieBannerKeys.Templates.CookiePolicy,
        ];

        Assert.Equal(keys.Length, keys.Distinct().Count());
        Assert.All(keys, key => Assert.StartsWith("c00c1e00-", key.ToString()));
    }

    // Pins the three Umbraco built-in data type keys the cookie schema binds to. A wrong key here
    // fails the install with "Data type ... was not found" rather than producing bad schema.
    [Fact]
    public void Built_in_data_type_keys_match_the_umbraco_defaults()
    {
        Assert.Equal(new Guid("0cc0eba1-9960-42c9-bf9b-60e150b429ae"), CookieBannerKeys.BuiltInDataTypes.Textstring);
        Assert.Equal(new Guid("c6bac0dd-4ab9-45b1-8e30-e4b619ee5da3"), CookieBannerKeys.BuiltInDataTypes.Textarea);
        Assert.Equal(new Guid("ca90c950-0aff-4e72-b976-a30b1ac57dad"), CookieBannerKeys.BuiltInDataTypes.RichtextEditor);
    }
}
```

- [ ] **Step 2: Run the test to verify it fails**

Run: `dotnet test Esatto.Umbraco.Backoffice.CookieBanner.Tests/Esatto.Umbraco.Backoffice.CookieBanner.Tests.csproj --filter FullyQualifiedName~CookieBannerKeysTests`
Expected: FAIL to build with `error CS0103: The name 'CookieBannerKeys' does not exist in the current context` at each reference in `CookieBannerKeysTests.cs`.

- [ ] **Step 3: Implement the key registry**

```csharp
namespace Esatto.Umbraco.Backoffice.CookieBanner;

/// <summary>
/// Stable keys for everything <see cref="CookieBannerSchemaInstaller"/> creates. Keeping them in
/// one place makes the installer idempotent across environments: a re-run finds the existing
/// entity by key instead of creating a duplicate, and a uSync export produces the same GUIDs on
/// every site. These are a fresh namespace, deliberately unrelated to the NDSTK series they were
/// extracted from, so installing the package on the NDSTK site cannot adopt or overwrite site
/// schema.
/// </summary>
internal static class CookieBannerKeys
{
    /// <summary>Data types this package adds on top of the Umbraco defaults.</summary>
    internal static class DataTypes
    {
        internal static readonly Guid CookieCategory = new("c00c1e00-0001-4000-8000-000000000001");
        internal static readonly Guid StorageType = new("c00c1e00-0001-4000-8000-000000000002");
        internal static readonly Guid CookieRegistry = new("c00c1e00-0001-4000-8000-000000000003");
    }

    /// <summary>Element types used as Block List blocks.</summary>
    internal static class ElementTypes
    {
        internal static readonly Guid CookieDefinition = new("c00c1e00-0002-4000-8000-000000000001");
    }

    internal static class DocumentTypes
    {
        internal static readonly Guid CookiePolicy = new("c00c1e00-0003-4000-8000-000000000001");
    }

    internal static class Templates
    {
        internal static readonly Guid CookiePolicy = new("c00c1e00-0004-4000-8000-000000000001");
    }

    /// <summary>Umbraco's built-in data types, reused as-is.</summary>
    internal static class BuiltInDataTypes
    {
        internal static readonly Guid Textstring = new("0cc0eba1-9960-42c9-bf9b-60e150b429ae");
        internal static readonly Guid Textarea = new("c6bac0dd-4ab9-45b1-8e30-e4b619ee5da3");
        internal static readonly Guid RichtextEditor = new("ca90c950-0aff-4e72-b976-a30b1ac57dad");
    }
}
```

- [ ] **Step 4: Run the test to verify it passes**

Run: `dotnet test Esatto.Umbraco.Backoffice.CookieBanner.Tests/Esatto.Umbraco.Backoffice.CookieBanner.Tests.csproj --filter FullyQualifiedName~CookieBannerKeysTests`
Expected: PASS — `Passed! - Failed: 0, Passed: 3, Skipped: 0`

- [ ] **Step 5: Commit**

```bash
git add Esatto.Umbraco.Backoffice.CookieBanner/src/CookieBannerKeys.cs Esatto.Umbraco.Backoffice.CookieBanner.Tests/CookieBannerKeysTests.cs
git commit -m "Add the CookieBanner schema key registry"
```

- [ ] **Step 6: Write the failing test for the schema installer**

```csharp
using System.Linq;
using Xunit;

namespace Esatto.Umbraco.Backoffice.CookieBanner.Tests;

public class CookieBannerSchemaInstallerTests
{
    // Pins that the Cookie category dropdown stores ConsentCategories WIRE names in All order.
    // The policy page and the banner group declared cookies by this stored string, so a display
    // label here (or a reordering) silently renders every category empty.
    [Fact]
    public void Cookie_category_dropdown_items_are_the_consent_wire_names_in_order()
    {
        Assert.Equal(
            ConsentCategories.All.Select(ConsentCategories.ToWireName),
            CookieBannerSchemaInstaller.CookieCategoryItems);

        Assert.Equal(
            new[] { "necessary", "preferences", "statistics", "marketing" },
            CookieBannerSchemaInstaller.CookieCategoryItems);
    }

    // Pins the four storage kinds the policy table renders, and their casing: these are shown to
    // visitors verbatim and the deferred scanner package maps its findings onto exactly these.
    [Fact]
    public void Storage_type_dropdown_items_match_the_four_supported_storage_kinds()
        => Assert.Equal(
            new[] { "Cookie", "localStorage", "sessionStorage", "Pixel" },
            CookieBannerSchemaInstaller.StorageTypeItems);

    // Pins that the Cookie registry Block List allows ONLY cookieDefinition. Any other allowed
    // block would put content into the registry that the policy table cannot render.
    [Fact]
    public void Cookie_registry_block_list_allows_only_the_cookie_definition_element_type()
    {
        Dictionary<string, object> configuration = CookieBannerSchemaInstaller.CookieRegistryConfiguration();

        object[] blocks = Assert.IsType<object[]>(configuration["blocks"]);
        Dictionary<string, object> block = Assert.IsType<Dictionary<string, object>>(Assert.Single(blocks));

        Assert.Equal(CookieBannerKeys.ElementTypes.CookieDefinition, block["contentElementTypeKey"]);
        Assert.Equal("Cookie", block["label"]);
    }

    // Pins that the template row is seeded with the packaged view's real markup. ITemplateService
    // writes a physical Views/CookiePolicy.cshtml that shadows the RCL-compiled view, so seeding a
    // bare @inherits stub would blank the policy page on every consumer site.
    [Fact]
    public void Packaged_cookie_policy_template_is_embedded_and_carries_real_markup()
    {
        string markup = CookieBannerSchemaInstaller.ReadPackagedTemplate();

        Assert.Contains("@inherits", markup);
        Assert.Contains("cookies", markup);
        Assert.True(markup.Length > 200, "the embedded view looks like a stub, not the real template");
    }

    // Pins that the packaged template never hardcodes a host layout. NDSTK's original view set
    // Layout = "Root.cshtml" at line 6; a package doing that breaks every other consumer.
    [Fact]
    public void Packaged_cookie_policy_template_leaves_the_layout_to_the_consumer()
    {
        string markup = CookieBannerSchemaInstaller.ReadPackagedTemplate();

        Assert.DoesNotContain("Root.cshtml", markup);
        Assert.DoesNotContain("Layout =", markup);
    }
}
```

- [ ] **Step 7: Run the test to verify it fails**

Run: `dotnet test Esatto.Umbraco.Backoffice.CookieBanner.Tests/Esatto.Umbraco.Backoffice.CookieBanner.Tests.csproj --filter FullyQualifiedName~CookieBannerSchemaInstallerTests`
Expected: FAIL to build with `error CS0103: The name 'CookieBannerSchemaInstaller' does not exist in the current context` at each reference in `CookieBannerSchemaInstallerTests.cs`.

- [ ] **Step 8: Implement the schema installer**

```csharp
using Microsoft.Extensions.Logging;
using Umbraco.Cms.Core;
using Umbraco.Cms.Core.Models;
using static Esatto.Umbraco.Backoffice.CookieBanner.CookieBannerKeys;

namespace Esatto.Umbraco.Backoffice.CookieBanner;

/// <summary>
/// Declares the six schema artefacts the cookie banner owns - two dropdowns, the
/// <c>cookieDefinition</c> element type, the <c>cookieRegistry</c> Block List, and the
/// <c>cookiePolicy</c> template plus document type - and creates whatever is missing. It runs
/// after boot on every start; because each step is create-if-missing it is cheap on an installed
/// site and self-healing on a fresh database.
/// </summary>
internal sealed class CookieBannerSchemaInstaller(
    CookieBannerContentTypeFactory factory,
    ILogger<CookieBannerSchemaInstaller> logger)
{
    /// <summary>
    /// Logical name of <c>Views/CookiePolicy.cshtml</c> embedded alongside the compiled RCL view.
    /// </summary>
    private const string TemplateResourceName =
        "Esatto.Umbraco.Backoffice.CookieBanner.Views.CookiePolicy.cshtml";

    /// <summary>
    /// The Cookie category dropdown's items. These are the wire names from
    /// <c>ConsentCategories.ToWireName</c>, not display labels: the policy page and the banner
    /// group declared cookies by the stored value, so it must match exactly. Display names come
    /// from the <c>Cookies.Category.*.Name</c> dictionary items. The stored value is a JSON array
    /// holding one string, e.g. <c>["necessary"]</c>.
    /// </summary>
    internal static readonly string[] CookieCategoryItems =
        ["necessary", "preferences", "statistics", "marketing"];

    /// <summary>
    /// The Storage type dropdown's items. Rendered verbatim in the policy table, so the casing is
    /// part of the contract. Stored, like every flexible dropdown, as <c>["Cookie"]</c>.
    /// </summary>
    internal static readonly string[] StorageTypeItems =
        ["Cookie", "localStorage", "sessionStorage", "Pixel"];

    public async Task InstallAsync()
    {
        // The built-in data types the cookie schema binds to.
        await factory.PreloadDataTypesAsync(
            BuiltInDataTypes.Textstring,
            BuiltInDataTypes.Textarea,
            BuiltInDataTypes.RichtextEditor);

        // Step 1. The cookie category / storage type dropdowns must exist - and be preloaded -
        // before the cookie definition element type is declared, because that element type binds
        // to them and factory.Property throws if a data type was not preloaded first.
        await InstallDropdownDataTypesAsync();
        await factory.PreloadDataTypesAsync(DataTypes.CookieCategory, DataTypes.StorageType);

        // Step 2. The element type. Nothing may reference it before this point.
        await InstallCookieDefinitionAsync();

        // Step 3. The Block List references the element type by key, so it can only be created
        // once the element type exists. Preloaded straight away for the document type below.
        await InstallCookieRegistryAsync();
        await factory.PreloadDataTypesAsync(DataTypes.CookieRegistry);

        // Step 4. Template before document type: UseTemplate needs a persisted ITemplate.
        ITemplate template = await factory.EnsureTemplateAsync(
            Templates.CookiePolicy,
            "Cookie policy",
            "CookiePolicy",
            ReadPackagedTemplate());

        await InstallCookiePolicyAsync(template);

        logger.LogInformation("Cookie banner schema is up to date.");
    }

    // ---------------------------------------------------------------- dropdowns

    private async Task InstallDropdownDataTypesAsync()
    {
        await factory.EnsureDataTypeAsync(
            DataTypes.CookieCategory,
            "Cookie category",
            Constants.PropertyEditors.Aliases.DropDownListFlexible,
            "Umb.PropertyEditorUi.Dropdown",
            new Dictionary<string, object>
            {
                ["multiple"] = false,
                ["items"] = CookieCategoryItems,
            });

        await factory.EnsureDataTypeAsync(
            DataTypes.StorageType,
            "Storage type",
            Constants.PropertyEditors.Aliases.DropDownListFlexible,
            "Umb.PropertyEditorUi.Dropdown",
            new Dictionary<string, object>
            {
                ["multiple"] = false,
                ["items"] = StorageTypeItems,
            });
    }

    // ------------------------------------------------------------- element type

    /// <remarks>
    /// Aliases and property aliases are identical to the NDSTK original so existing content is
    /// portable onto package-owned schema. Only the descriptions changed: NDSTK's were partly
    /// Swedish and named its own site as an example provider.
    /// </remarks>
    private Task InstallCookieDefinitionAsync()
        => factory.EnsureContentTypeAsync(
            ElementTypes.CookieDefinition, "cookieDefinition", "Cookie", "icon-lock", type =>
            {
                type.IsElement = true;
                type.Description = "One declared cookie, shown in the cookie policy table.";
                CookieBannerContentTypeFactory.AddGroup(
                    type, DeriveKey(ElementTypes.CookieDefinition, 1), "content", "Content", 0,
                    factory.Property(BuiltInDataTypes.Textstring, "cookieName", "Name",
                        "Literal name or pattern, e.g. _ga_*", 0),
                    factory.Property(BuiltInDataTypes.Textstring, "provider", "Provider",
                        "Who sets the cookie, e.g. this site, Google, YouTube.", 1),
                    factory.Property(DataTypes.CookieCategory, "category", "Category", sortOrder: 2),
                    factory.Property(BuiltInDataTypes.Textarea, "purpose", "Purpose", sortOrder: 3),
                    factory.Property(BuiltInDataTypes.Textstring, "duration", "Duration",
                        "How long it is stored, e.g. \"12 months\" or \"Session\".", 4),
                    factory.Property(DataTypes.StorageType, "storageType", "Storage type", sortOrder: 5));
            });

    // --------------------------------------------------------------- Block List

    private Task InstallCookieRegistryAsync()
        => factory.EnsureDataTypeAsync(
            DataTypes.CookieRegistry,
            "Cookie registry",
            Constants.PropertyEditors.Aliases.BlockList,
            "Umb.PropertyEditorUi.BlockList",
            CookieRegistryConfiguration());

    /// <summary>The Block List configuration: cookie definitions and nothing else.</summary>
    internal static Dictionary<string, object> CookieRegistryConfiguration() => new()
    {
        ["blocks"] = new object[] { Block(ElementTypes.CookieDefinition, "Cookie") },
    };

    private static Dictionary<string, object> Block(Guid elementTypeKey, string label) => new()
    {
        ["contentElementTypeKey"] = elementTypeKey,
        ["label"] = label,
        ["editorSize"] = "medium",
    };

    // ------------------------------------------------------------ document type

    private Task InstallCookiePolicyAsync(ITemplate template)
        => factory.EnsureContentTypeAsync(
            DocumentTypes.CookiePolicy, "cookiePolicy", "Cookie policy", "icon-lock", type =>
            {
                type.Description = "Lists the declared cookies and the visitor's current consent.";

                // A package cannot add itself to a consumer's document type structure, so the page
                // is allowed at root. CookiePolicyPageResolver finds it anywhere in the tree, and
                // an editor is free to allow it under their own page types instead.
                type.AllowedAsRoot = true;

                CookieBannerContentTypeFactory.UseTemplate(type, template);
                CookieBannerContentTypeFactory.AddGroup(
                    type, DeriveKey(DocumentTypes.CookiePolicy, 1), "content", "Content", 0,
                    factory.Property(BuiltInDataTypes.Textstring, "heading", "Heading",
                        "Falls back to the node name.", 0),
                    factory.Property(BuiltInDataTypes.RichtextEditor, "introduction", "Introduction",
                        sortOrder: 1),
                    factory.Property(DataTypes.CookieRegistry, "cookies", "Declared cookies",
                        sortOrder: 2),
                    factory.Property(BuiltInDataTypes.RichtextEditor, "outro", "Closing text",
                        sortOrder: 3));
            });

    // ------------------------------------------------------------------ helpers

    /// <summary>
    /// Reads the packaged cookie policy view out of the assembly manifest. Umbraco's template
    /// service writes the content it is given to a physical <c>Views/CookiePolicy.cshtml</c>, and
    /// with Razor runtime compilation on that physical file shadows the compiled RCL view - so the
    /// content handed to it has to be the real markup, not a stub.
    /// </summary>
    internal static string ReadPackagedTemplate()
    {
        using Stream stream = typeof(CookieBannerSchemaInstaller).Assembly
                                  .GetManifestResourceStream(TemplateResourceName)
                              ?? throw new InvalidOperationException(
                                  $"Embedded resource '{TemplateResourceName}' is missing from the package.");

        using var reader = new StreamReader(stream);
        return reader.ReadToEnd();
    }

    /// <summary>
    /// Property groups need their own stable keys. Deriving them from the owning type's key keeps
    /// the key registry small while staying deterministic across installs.
    /// </summary>
    private static Guid DeriveKey(Guid owner, byte discriminator)
    {
        Span<byte> bytes = stackalloc byte[16];
        owner.TryWriteBytes(bytes);
        bytes[15] = (byte)(bytes[15] ^ 0x80 ^ discriminator);
        return new Guid(bytes);
    }
}
```

- [ ] **Step 9: Embed the packaged view as a manifest resource**

Add this `ItemGroup` to `Esatto.Umbraco.Backoffice.CookieBanner/Esatto.Umbraco.Backoffice.CookieBanner.csproj`, directly after the existing `<ItemGroup>` that packs `README.md` and `icon.png`:

```xml
  <ItemGroup Label="Template source">
    <!-- Views/CookiePolicy.cshtml ships twice on purpose: once compiled as an RCL view, once
         embedded so CookieBannerSchemaInstaller can hand its exact source to ITemplateService.
         The template service writes a physical Views/CookiePolicy.cshtml into the consumer app,
         which shadows the compiled view under Razor runtime compilation - so the two must be the
         same bytes, or a consumer would render the stub instead of the real page. -->
    <EmbeddedResource Include="Views\CookiePolicy.cshtml" LogicalName="Esatto.Umbraco.Backoffice.CookieBanner.Views.CookiePolicy.cshtml" />
  </ItemGroup>
```

- [ ] **Step 10: Run the test to verify it passes**

Run: `dotnet test Esatto.Umbraco.Backoffice.CookieBanner.Tests/Esatto.Umbraco.Backoffice.CookieBanner.Tests.csproj --filter FullyQualifiedName~CookieBannerSchemaInstallerTests`
Expected: PASS — `Passed! - Failed: 0, Passed: 5, Skipped: 0`

- [ ] **Step 11: Commit**

```bash
git add Esatto.Umbraco.Backoffice.CookieBanner/src/CookieBannerSchemaInstaller.cs Esatto.Umbraco.Backoffice.CookieBanner/Esatto.Umbraco.Backoffice.CookieBanner.csproj Esatto.Umbraco.Backoffice.CookieBanner.Tests/CookieBannerSchemaInstallerTests.cs
git commit -m "Install the six cookie banner schema artefacts"
```

### Task 15: Culture-agnostic dictionary installer

**Files:**
- Create: `c:\src\Esatto.Packages\Esatto.Umbraco.Backoffice.CookieBanner\src\CookieBannerDictionaryInstaller.cs`
- Test: `c:\src\Esatto.Packages\Esatto.Umbraco.Backoffice.CookieBanner.Tests\CookieBannerDictionaryInstallerTests.cs`

**Interfaces:**
- Consumes: `Task<IEnumerable<ILanguage>> ILanguageService.GetAllAsync()`, `Task<IDictionaryItem?> IDictionaryItemService.GetAsync(string)`, `Task<Attempt<IDictionaryItem, DictionaryItemOperationStatus>> IDictionaryItemService.CreateAsync(IDictionaryItem, Guid)`, `Task<Attempt<IDictionaryItem, DictionaryItemOperationStatus>> IDictionaryItemService.MoveAsync(IDictionaryItem, Guid?, Guid)` — all verified present in both Umbraco 17.0.0 and 18.1.1.
- Produces: `internal sealed class CookieBannerDictionaryInstaller` — ctor `(IDictionaryItemService, ILanguageService, ILogger<CookieBannerDictionaryInstaller>)`; `Task InstallAsync()`; `internal const string CookieBannerDictionaryInstaller.ParentKey = "Cookie.Banner"`; `internal static IReadOnlyList<string> CookieBannerDictionaryInstaller.Keys` (the 32 dictionary keys, for Task 20's `ConsentTextProvider` resx parity check).

- [ ] **Step 1: Write the failing test**

Create `c:\src\Esatto.Packages\Esatto.Umbraco.Backoffice.CookieBanner.Tests\CookieBannerDictionaryInstallerTests.cs`:

```csharp
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Esatto.Umbraco.Backoffice.CookieBanner;
using Microsoft.Extensions.Logging.Abstractions;
using NSubstitute;
using Umbraco.Cms.Core;
using Umbraco.Cms.Core.Models;
using Umbraco.Cms.Core.Services;
using Umbraco.Cms.Core.Services.OperationStatus;
using Xunit;

namespace Esatto.Umbraco.Backoffice.CookieBanner.Tests;

public class CookieBannerDictionaryInstallerTests
{
    private static (CookieBannerDictionaryInstaller Installer,
                    IDictionaryItemService Items,
                    List<IDictionaryItem> Created)
        CreateSut(params ILanguage[] siteLanguages)
    {
        var languages = Substitute.For<ILanguageService>();
        languages.GetAllAsync().Returns(siteLanguages.AsEnumerable());

        var items = Substitute.For<IDictionaryItemService>();
        var created = new List<IDictionaryItem>();
        items
            .CreateAsync(Arg.Any<IDictionaryItem>(), Arg.Any<Guid>())
            .Returns(call =>
            {
                IDictionaryItem item = call.Arg<IDictionaryItem>();
                created.Add(item);
                return Attempt.SucceedWithStatus<IDictionaryItem, DictionaryItemOperationStatus>(
                    DictionaryItemOperationStatus.Success, item);
            });

        var installer = new CookieBannerDictionaryInstaller(
            items, languages, NullLogger<CookieBannerDictionaryInstaller>.Instance);

        return (installer, items, created);
    }

    private static IEnumerable<string> SeededKeys(IEnumerable<IDictionaryItem> created)
        => created
            .Select(item => item.ItemKey)
            .Where(key => key != CookieBannerDictionaryInstaller.ParentKey);

    [Fact]
    public async Task Seeds_nothing_and_does_not_throw_when_the_site_has_no_shipped_language()
    {
        // Pins the fix for NDSTK's unshippable hard abort: the old installer demanded a 'sv'
        // language (which only existed because NdstkLanguageInstaller forced it in) and bailed
        // out of ALL seeding when it was missing. A package must never require a language, and
        // must never throw on a site whose languages it ships no text for.
        var (installer, items, created) = CreateSut(new Language("de-DE", "German"));

        await installer.InstallAsync();

        Assert.Empty(created);
        _ = items.DidNotReceiveWithAnyArgs().CreateAsync(null!, default);
    }

    [Fact]
    public async Task Seeds_English_only_for_an_English_only_site()
    {
        // Pins culture-agnostic seeding: an en-GB-only site gets all 32 keys with exactly one
        // translation each, and no 'sv' language is created to hang the Swedish text off.
        var (installer, _, created) = CreateSut(new Language("en-GB", "English (United Kingdom)"));

        await installer.InstallAsync();

        Assert.Equal(32, SeededKeys(created).Count());
        Assert.Contains(CookieBannerDictionaryInstaller.ParentKey, created.Select(item => item.ItemKey));

        IDictionaryItem heading = created.Single(item => item.ItemKey == "Cookies.Banner.Heading");
        IDictionaryTranslation translation = Assert.Single(heading.Translations);
        Assert.Equal("en-GB", translation.LanguageIsoCode);
        Assert.Equal("We use cookies", translation.Value);
    }

    [Fact]
    public async Task Seeds_both_languages_for_a_Swedish_and_English_site()
    {
        // Pins that matching is by the two-letter language part, so sv-SE and en-US match the
        // shipped 'sv'/'en' text sets, and pins the new Cookies.Policy.On/Off keys that replace
        // the hardcoded "på"/"av" literals on the policy page.
        var (installer, _, created) = CreateSut(
            new Language("sv-SE", "Swedish (Sweden)"),
            new Language("en-US", "English (United States)"));

        await installer.InstallAsync();

        Assert.Equal(32, SeededKeys(created).Count());
        Assert.Contains("Cookies.Policy.On", SeededKeys(created));
        Assert.Contains("Cookies.Policy.Off", SeededKeys(created));

        IDictionaryItem heading = created.Single(item => item.ItemKey == "Cookies.Banner.Heading");
        Assert.Equal(2, heading.Translations.Count());
        Assert.Equal(
            "Vi använder kakor",
            heading.Translations.Single(t => t.LanguageIsoCode == "sv-SE").Value);
        Assert.Equal(
            "We use cookies",
            heading.Translations.Single(t => t.LanguageIsoCode == "en-US").Value);
    }

    [Fact]
    public async Task Skips_an_existing_key_and_leaves_an_item_an_editor_moved_where_it_is()
    {
        // Pins create-if-missing only (a re-boot must not overwrite an editor's reworded copy)
        // and pins the TryAdopt guard: an item whose ParentId is already set was deliberately
        // filed somewhere, so the seeder must not re-parent it under Cookie.Banner.
        var existing = Substitute.For<IDictionaryItem>();
        existing.ItemKey.Returns("Cookies.Banner.Heading");
        existing.ParentId.Returns((Guid?)Guid.NewGuid());

        var (installer, items, created) = CreateSut(new Language("en-GB", "English (United Kingdom)"));
        items.GetAsync("Cookies.Banner.Heading").Returns(existing);

        await installer.InstallAsync();

        Assert.DoesNotContain("Cookies.Banner.Heading", SeededKeys(created));
        Assert.Equal(31, SeededKeys(created).Count());
        _ = items.DidNotReceiveWithAnyArgs().MoveAsync(null!, null, default);
    }
}
```

- [ ] **Step 2: Run the test to verify it fails**

Run: `dotnet test Esatto.Umbraco.Backoffice.CookieBanner.Tests/Esatto.Umbraco.Backoffice.CookieBanner.Tests.csproj --filter FullyQualifiedName~CookieBannerDictionaryInstallerTests`
Expected: FAIL — build error `error CS0246: The type or namespace name 'CookieBannerDictionaryInstaller' could not be found (are you missing a using directive or an assembly reference?)` (four occurrences, plus `CS0103` for `CookieBannerDictionaryInstaller.ParentKey`).

- [ ] **Step 3: Implement**

Create `c:\src\Esatto.Packages\Esatto.Umbraco.Backoffice.CookieBanner\src\CookieBannerDictionaryInstaller.cs`:

```csharp
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;
using Umbraco.Cms.Core;
using Umbraco.Cms.Core.Models;
using Umbraco.Cms.Core.Services;

namespace Esatto.Umbraco.Backoffice.CookieBanner;

/// <summary>
/// Seeds the consent banner's text as Umbraco Dictionary items.
/// </summary>
/// <remarks>
/// Dictionary items are culture-variant regardless of document type variance, which is what lets
/// the banner be multilingual while the content types stay invariant.
/// <para>
/// Culture-agnostic by design: it enumerates the languages the site actually has and seeds text
/// for any of them the package ships a translation for. It never requires a language, never
/// creates one, never deletes one, and never aborts - a package must not manage a site's language
/// set. A site with no matching language simply gets no dictionary items, and the resx fallback in
/// <c>ConsentTextProvider</c> keeps the banner readable.
/// </para>
/// <para>
/// Every item is filed under a single <see cref="ParentKey" /> node. Umbraco dictionary keys are
/// global rather than path-based, so the nesting is presentation only - it keeps 32 items from
/// sitting loose at the root of the Dictionary tree without changing a single lookup.
/// </para>
/// </remarks>
internal sealed class CookieBannerDictionaryInstaller(
    IDictionaryItemService dictionaryItemService,
    ILanguageService languageService,
    ILogger<CookieBannerDictionaryInstaller> logger)
{
    private static readonly Guid UserKey = Constants.Security.SuperUserKey;

    /// <summary>Parent node for every item below. Holds no translations: it is a folder, not a label.</summary>
    internal const string ParentKey = "Cookie.Banner";

    /// <summary>
    /// Two-letter language codes the package ships text for. A site language matches when its
    /// primary subtag is in here, so en-GB, en-US and a bare en all resolve to the English set.
    /// </summary>
    private static readonly string[] ShippedLanguages = ["en", "sv"];

    /// <summary>
    /// Key, English, Swedish. English first: it is the package's neutral fallback culture, not a
    /// site's default language.
    /// </summary>
    private static readonly (string Key, string En, string Sv)[] Items =
    [
        ("Cookies.Banner.Heading", "We use cookies", "Vi använder kakor"),
        ("Cookies.Banner.Body",
            "We use necessary cookies to make the site work. We would also like to use cookies for statistics and content from other services.",
            "Vi använder nödvändiga kakor för att sajten ska fungera. Vi vill också gärna använda kakor för statistik och innehåll från andra tjänster."),
        ("Cookies.Banner.AcceptAll", "Accept all", "Godkänn alla"),
        ("Cookies.Banner.RejectAll", "Reject all", "Neka alla"),
        ("Cookies.Banner.Customise", "Customise", "Anpassa"),
        ("Cookies.Banner.Save", "Save choices", "Spara val"),
        ("Cookies.Banner.Cancel", "Cancel", "Avbryt"),
        ("Cookies.Banner.Error", "Something went wrong. Please try again.", "Något gick fel. Försök igen."),
        ("Cookies.Banner.RateLimited",
            "You've tried too many times. Please wait a moment and try again.",
            "Du har försökt för många gånger. Vänta en stund och försök igen."),
        ("Cookies.Category.Necessary.Name", "Necessary", "Nödvändiga"),
        ("Cookies.Category.Necessary.Description",
            "Required for the site to work, for example logging in. Cannot be turned off.",
            "Krävs för att sajten ska fungera, till exempel inloggning. Kan inte stängas av."),
        ("Cookies.Category.Preferences.Name", "Preferences", "Funktionella"),
        ("Cookies.Category.Preferences.Description",
            "Remembers your choices, such as language.",
            "Sparar dina val, till exempel språk."),
        ("Cookies.Category.Statistics.Name", "Statistics", "Statistik"),
        ("Cookies.Category.Statistics.Description",
            "Helps us understand how the site is used. Fully anonymous.",
            "Hjälper oss förstå hur sajten används. Helt anonymt."),
        ("Cookies.Category.Marketing.Name", "Marketing", "Marknadsföring"),
        ("Cookies.Category.Marketing.Description",
            "Used by embedded content, such as YouTube videos.",
            "Används av inbäddat innehåll, till exempel filmer från YouTube."),
        ("Cookies.Category.Cookies", "Cookies in this category", "Kakor i den här kategorin"),
        ("Cookies.Embed.Blocked.Body",
            "This content comes from another service and needs your consent.",
            "Det här innehållet kommer från en annan tjänst och kräver ditt samtycke."),
        ("Cookies.Embed.Blocked.Button", "Show content", "Visa innehåll"),
        ("Cookies.Policy.CurrentChoice", "Your current choice", "Ditt nuvarande val"),
        ("Cookies.Policy.NoChoice", "You have not made a choice yet.", "Du har inte gjort något val än."),
        // On/Off exist because CookiePolicy.cshtml used to render a hardcoded "på"/"av", making
        // the policy page Swedish in every language including English.
        ("Cookies.Policy.On", "on", "på"),
        ("Cookies.Policy.Off", "off", "av"),
        ("Cookies.Policy.Reopen", "Change settings", "Ändra inställningar"),
        ("Cookies.Policy.Withdraw", "Withdraw consent", "Återkalla samtycke"),
        ("Cookies.Footer.Link", "Cookie settings", "Cookieinställningar"),
        ("Cookies.Table.Name", "Name", "Namn"),
        ("Cookies.Table.Provider", "Provider", "Leverantör"),
        ("Cookies.Table.Purpose", "Purpose", "Syfte"),
        ("Cookies.Table.Duration", "Duration", "Lagringstid"),
        ("Cookies.Table.Type", "Type", "Typ"),
    ];

    /// <summary>Every key this installer seeds, for the resx parity check in the text provider.</summary>
    internal static IReadOnlyList<string> Keys { get; } = [.. Items.Select(item => item.Key)];

    public async Task InstallAsync()
    {
        IEnumerable<ILanguage> siteLanguages = await languageService.GetAllAsync();

        List<(ILanguage Language, string Code)> targets = siteLanguages
            .Select(language => (Language: language, Code: PrimarySubtag(language.IsoCode)))
            .Where(target => ShippedLanguages.Contains(target.Code))
            .ToList();

        if (targets.Count == 0)
        {
            // Not a failure. The site simply has no language the package ships text for; the
            // resx fallback covers the banner. Never create a language to fix this.
            logger.LogInformation(
                "Skipping cookie dictionary seeding: the site has no language the package ships text for ({Shipped}).",
                string.Join(", ", ShippedLanguages));
            return;
        }

        Guid? parentId = await EnsureParentAsync();

        var created = 0;
        var adopted = 0;
        foreach ((string key, string en, string sv) item in Items)
        {
            IDictionaryItem? existing = await dictionaryItemService.GetAsync(item.key);
            if (existing is not null)
            {
                if (await TryAdoptAsync(existing, parentId))
                {
                    adopted++;
                }

                continue;
            }

            var translations = new List<IDictionaryTranslation>();
            foreach ((ILanguage language, string code) in targets)
            {
                translations.Add(new DictionaryTranslation(language, TextFor(code, item)));
            }

            var dictionaryItem = new DictionaryItem(parentId, item.key) { Translations = translations };

            var attempt = await dictionaryItemService.CreateAsync(dictionaryItem, UserKey);
            if (attempt.Success is false)
            {
                logger.LogWarning("Could not create dictionary item {Key}: {Status}.", item.key, attempt.Status);
                continue;
            }

            created++;
        }

        if (created > 0)
        {
            logger.LogInformation(
                "Seeded {Count} cookie dictionary items for {Languages}.",
                created,
                string.Join(", ", targets.Select(target => target.Language.IsoCode)));
        }

        if (adopted > 0)
        {
            logger.LogInformation(
                "Filed {Count} existing cookie dictionary items under '{Parent}'.", adopted, ParentKey);
        }
    }

    /// <summary>The primary language subtag, lowercased: "en-GB" -> "en", "sv" -> "sv".</summary>
    private static string PrimarySubtag(string isoCode)
    {
        int dash = isoCode.IndexOf('-');
        return (dash < 0 ? isoCode : isoCode[..dash]).ToLowerInvariant();
    }

    private static string TextFor(string code, (string Key, string En, string Sv) item)
        => code == "sv" ? item.Sv : item.En;

    /// <summary>
    /// Returns the id of the parent node, creating it if absent. Returns null when it cannot be
    /// created: seeding the text still matters more than where the items sit in the tree.
    /// </summary>
    private async Task<Guid?> EnsureParentAsync()
    {
        IDictionaryItem? existing = await dictionaryItemService.GetAsync(ParentKey);
        if (existing is not null)
        {
            return existing.Key;
        }

        // No translations. The tree labels a node by its key, so this reads as "Cookie.Banner"
        // while staying invisible to GetDictionaryValue - nothing renders it.
        var parent = new DictionaryItem(ParentKey) { Translations = [] };

        var attempt = await dictionaryItemService.CreateAsync(parent, UserKey);
        if (attempt.Success is false)
        {
            logger.LogWarning(
                "Could not create the '{Parent}' dictionary parent: {Status}. Items stay at the root.",
                ParentKey,
                attempt.Status);
            return null;
        }

        return attempt.Result?.Key;
    }

    /// <summary>
    /// Files an item that is still at the root under the parent - the one-off tidy for items
    /// seeded before this grouping existed. An item an editor has deliberately moved somewhere
    /// else is left where they put it: this seeder creates and tidies, it does not enforce a
    /// shape on every boot.
    /// </summary>
    private async Task<bool> TryAdoptAsync(IDictionaryItem item, Guid? parentId)
    {
        if (parentId is null || item.ParentId is not null)
        {
            return false;
        }

        var attempt = await dictionaryItemService.MoveAsync(item, parentId, UserKey);
        if (attempt.Success)
        {
            return true;
        }

        logger.LogWarning(
            "Could not file dictionary item {Key} under '{Parent}': {Status}.",
            item.ItemKey,
            ParentKey,
            attempt.Status);
        return false;
    }
}
```

- [ ] **Step 4: Run the test to verify it passes**

Run: `dotnet test Esatto.Umbraco.Backoffice.CookieBanner.Tests/Esatto.Umbraco.Backoffice.CookieBanner.Tests.csproj --filter FullyQualifiedName~CookieBannerDictionaryInstallerTests`
Expected: PASS — `Passed! - Failed: 0, Passed: 4`

- [ ] **Step 5: Commit**

```bash
git add Esatto.Umbraco.Backoffice.CookieBanner/src/CookieBannerDictionaryInstaller.cs Esatto.Umbraco.Backoffice.CookieBanner.Tests/CookieBannerDictionaryInstallerTests.cs
git commit -m "Seed cookie dictionary items for whatever languages the site has"
```

---

### Task 16: Policy-page resolution and seeding

**Files:**
- Create: `c:\src\Esatto.Packages\Esatto.Umbraco.Backoffice.CookieBanner\src\CookiePolicyPageResolver.cs`
- Create: `c:\src\Esatto.Packages\Esatto.Umbraco.Backoffice.CookieBanner\src\CookieBannerContentSeeder.cs`
- Modify: `c:\src\Esatto.Packages\Esatto.Umbraco.Backoffice.CookieBanner\src\CookieBannerKeys.cs` (add the `Nodes` class — one GUID)
- Test: `c:\src\Esatto.Packages\Esatto.Umbraco.Backoffice.CookieBanner.Tests\CookiePolicyPageResolverTests.cs`
- Test: `c:\src\Esatto.Packages\Esatto.Umbraco.Backoffice.CookieBanner.Tests\CookieBannerContentSeederTests.cs`

**Interfaces:**
- Consumes: `CookieBannerOptions.PolicyPageKey` / `.CookieName` / `.CookieLifetimeDays` (Task 4); `CookieBannerKeys.ElementTypes.CookieDefinition` = `c00c1e00-0002-4000-8000-000000000001` (Task 7); `IPublishedContentCache.GetById(Guid)`; `IContentTypeBaseService<IContentType>.Get(string)`; `IContentService.GetPagedOfType(int, long, int, out long, IQuery<IContent>?, Ordering?)`; `IEntityService.Exists(Guid, UmbracoObjectTypes)`; `IContentService.GetRootContent()` / `.Create(string, int, string, int)` / `.Save(IContent, int?, ContentScheduleCollection?)` / `.Publish(IContent, string[], int)` — every one verified present and signature-identical on both Umbraco.Cms.Core 17.0.0 and 18.1.1.
- Produces: `internal sealed class CookiePolicyPageResolver : ICookiePolicyPageResolver` (the interface itself was declared in Task 6 — do NOT redeclare it; delete the `internal interface ICookiePolicyPageResolver` block from the code below if you paste it verbatim) — ctor `(IPublishedContentCache, IContentTypeService, IContentService, IOptions<CookieBannerOptions>, ILogger<CookiePolicyPageResolver>)`; `internal const string CookiePolicyPageResolver.ContentTypeAlias = "cookiePolicy"`; `internal sealed class CookieBannerContentSeeder` — ctor `(IContentService, IContentTypeService, IEntityService, IJsonSerializer, IOptions<CookieBannerOptions>, ILogger<CookieBannerContentSeeder>)`; `void EnsurePolicyPage()`; `internal static readonly Guid CookieBannerKeys.Nodes.CookiePolicy = c00c1e00-0005-4000-8000-000000000001`.

- [ ] **Step 1: Write the failing resolver test**

Create `c:\src\Esatto.Packages\Esatto.Umbraco.Backoffice.CookieBanner.Tests\CookiePolicyPageResolverTests.cs`:

```csharp
using System;
using System.Collections.Generic;
using System.Linq;
using Esatto.Umbraco.Backoffice.CookieBanner;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using NSubstitute;
using Umbraco.Cms.Core.Models;
using Umbraco.Cms.Core.Models.PublishedContent;
using Umbraco.Cms.Core.PublishedCache;
using Umbraco.Cms.Core.Services;
using Xunit;

namespace Esatto.Umbraco.Backoffice.CookieBanner.Tests;

public class CookiePolicyPageResolverTests
{
    private static IContent Document(Guid key)
    {
        var document = Substitute.For<IContent>();
        document.Key.Returns(key);
        return document;
    }

    private static CookiePolicyPageResolver CreateSut(
        IPublishedContentCache cache,
        IContentTypeService contentTypeService,
        IContentService contentService,
        Guid? policyPageKey)
        => new(
            cache,
            contentTypeService,
            contentService,
            Options.Create(new CookieBannerOptions { PolicyPageKey = policyPageKey }),
            NullLogger<CookiePolicyPageResolver>.Instance);

    [Fact]
    public void Honours_the_explicit_policy_page_key_without_querying_the_document_type()
    {
        // Pins the PolicyPageKey override: a site with several cookiePolicy nodes must be able to
        // name the one the banner and footer point at, and the override must short-circuit the
        // by-type scan entirely rather than merely re-ordering it.
        var key = Guid.NewGuid();
        var expected = Substitute.For<IPublishedContent>();

        var cache = Substitute.For<IPublishedContentCache>();
        cache.GetById(key).Returns(expected);
        var contentTypeService = Substitute.For<IContentTypeService>();
        var contentService = Substitute.For<IContentService>();

        ICookiePolicyPageResolver resolver = CreateSut(cache, contentTypeService, contentService, key);

        Assert.Same(expected, resolver.Resolve());
        contentTypeService.DidNotReceiveWithAnyArgs().Get(default(string)!);
    }

    [Fact]
    public void Falls_back_to_the_first_published_node_of_the_cookie_policy_type()
    {
        // Pins the replacement for NDSTK's cookiePolicyPage Content Picker on the SITE's settings
        // doctype - a cross-model schema write a package may not make. Resolution is by document
        // type, and an unpublished candidate must be skipped: the published cache returns null for
        // it, so "first of the type" is not the same as "first PUBLISHED of the type".
        var draftKey = Guid.NewGuid();
        var publishedKey = Guid.NewGuid();
        var expected = Substitute.For<IPublishedContent>();

        var cache = Substitute.For<IPublishedContentCache>();
        cache.GetById(draftKey).Returns((IPublishedContent?)null);
        cache.GetById(publishedKey).Returns(expected);

        var contentType = Substitute.For<IContentType>();
        contentType.Id.Returns(1234);
        var contentTypeService = Substitute.For<IContentTypeService>();
        contentTypeService.Get(CookiePolicyPageResolver.ContentTypeAlias).Returns(contentType);

        var contentService = Substitute.For<IContentService>();
        long total;
        contentService
            .GetPagedOfType(default, default, default, out total, default, default)
            .ReturnsForAnyArgs(new[] { Document(draftKey), Document(publishedKey) });

        ICookiePolicyPageResolver resolver = CreateSut(cache, contentTypeService, contentService, null);

        Assert.Same(expected, resolver.Resolve());
    }

    [Fact]
    public void Returns_null_when_no_published_cookie_policy_page_exists()
    {
        // Pins that a site with the document type installed but nothing published (or with the
        // type missing entirely, on a boot before the schema installer ran) resolves to null
        // instead of throwing - the banner renders without a policy link, it does not 500.
        var cache = Substitute.For<IPublishedContentCache>();
        var contentTypeService = Substitute.For<IContentTypeService>();
        contentTypeService.Get(CookiePolicyPageResolver.ContentTypeAlias).Returns((IContentType?)null);
        var contentService = Substitute.For<IContentService>();

        ICookiePolicyPageResolver resolver = CreateSut(cache, contentTypeService, contentService, null);

        Assert.Null(resolver.Resolve());
    }
}
```

- [ ] **Step 2: Run the test to verify it fails**

Run: `dotnet test Esatto.Umbraco.Backoffice.CookieBanner.Tests/Esatto.Umbraco.Backoffice.CookieBanner.Tests.csproj --filter FullyQualifiedName~CookiePolicyPageResolverTests`
Expected: FAIL — build error `error CS0246: The type or namespace name 'CookiePolicyPageResolver' could not be found` and `error CS0246: The type or namespace name 'ICookiePolicyPageResolver' could not be found`.

- [ ] **Step 3: Implement the resolver**

Create `c:\src\Esatto.Packages\Esatto.Umbraco.Backoffice.CookieBanner\src\CookiePolicyPageResolver.cs`:

```csharp
using System;
using System.Collections.Generic;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Umbraco.Cms.Core.Models;
using Umbraco.Cms.Core.Models.PublishedContent;
using Umbraco.Cms.Core.PublishedCache;
using Umbraco.Cms.Core.Services;

namespace Esatto.Umbraco.Backoffice.CookieBanner;

/// <summary>Finds the cookie policy page the banner and the footer link point at.</summary>
internal interface ICookiePolicyPageResolver
{
    /// <summary>The policy page, or null when the site has none published.</summary>
    IPublishedContent? Resolve();
}

/// <summary>
/// Resolves the policy page by document type instead of by a picker property.
/// </summary>
/// <remarks>
/// The feature this package was extracted from added a <c>cookiePolicyPage</c> Content Picker to
/// the CONSUMING site's <c>settings</c> document type and read the page out of it. A package
/// cannot add properties to a document type it does not own, and that single cross-model write is
/// the entire reason the old site needed a hand-written upgrade document plus four manual
/// backoffice steps.
/// <para>
/// Instead: the first published node of type <see cref="ContentTypeAlias" />, with
/// <see cref="CookieBannerOptions.PolicyPageKey" /> as an explicit override for a site that has
/// more than one. No manual backoffice step, no schema write outside the package's own GUIDs.
/// </para>
/// </remarks>
internal sealed class CookiePolicyPageResolver(
    IPublishedContentCache contentCache,
    IContentTypeService contentTypeService,
    IContentService contentService,
    IOptions<CookieBannerOptions> options,
    ILogger<CookiePolicyPageResolver> logger) : ICookiePolicyPageResolver
{
    internal const string ContentTypeAlias = "cookiePolicy";

    /// <summary>
    /// A site with more policy pages than this has bigger problems than the banner. The cap keeps
    /// the fallback scan to one bounded query.
    /// </summary>
    private const int ScanPageSize = 100;

    // Registered scoped, so this memoises for the lifetime of one request: <consent-banner /> and
    // the policy template can both ask without paying for a second database round trip.
    private bool _resolved;
    private IPublishedContent? _page;

    public IPublishedContent? Resolve()
    {
        if (_resolved)
        {
            return _page;
        }

        _page = ResolveCore();
        _resolved = true;
        return _page;
    }

    private IPublishedContent? ResolveCore()
    {
        if (options.Value.PolicyPageKey is Guid key)
        {
            // An explicit override wins outright. Falling back to a by-type scan here would
            // silently point the banner at a different page than the one that was configured.
            IPublishedContent? configured = contentCache.GetById(key);
            if (configured is null)
            {
                logger.LogWarning(
                    "{Option} is set to {Key} but no published content with that key exists.",
                    $"{CookieBannerOptions.SectionName}:PolicyPageKey",
                    key);
            }

            return configured;
        }

        IContentType? contentType = contentTypeService.Get(ContentTypeAlias);
        if (contentType is null)
        {
            // The schema installer has not run yet (first boot, or it failed and logged).
            return null;
        }

        IEnumerable<IContent> candidates =
            contentService.GetPagedOfType(contentType.Id, 0, ScanPageSize, out _, null, null);

        foreach (IContent candidate in candidates)
        {
            // The non-preview published cache returns null for a node that is not published, so
            // this filters to published nodes without a second service.
            IPublishedContent? published = contentCache.GetById(candidate.Key);
            if (published is not null)
            {
                return published;
            }
        }

        return null;
    }
}
```

- [ ] **Step 4: Run the resolver test to verify it passes**

Run: `dotnet test Esatto.Umbraco.Backoffice.CookieBanner.Tests/Esatto.Umbraco.Backoffice.CookieBanner.Tests.csproj --filter FullyQualifiedName~CookiePolicyPageResolverTests`
Expected: PASS — `Passed! - Failed: 0, Passed: 3`

- [ ] **Step 5: Write the failing seeder test**

Create `c:\src\Esatto.Packages\Esatto.Umbraco.Backoffice.CookieBanner.Tests\CookieBannerContentSeederTests.cs`:

```csharp
using System;
using System.Collections.Generic;
using System.Linq;
using Esatto.Umbraco.Backoffice.CookieBanner;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using NSubstitute;
using Umbraco.Cms.Core.Events;
using Umbraco.Cms.Core.Models;
using Umbraco.Cms.Core.Models.Blocks;
using Umbraco.Cms.Core.Serialization;
using Umbraco.Cms.Core.Services;
using Xunit;

namespace Esatto.Umbraco.Backoffice.CookieBanner.Tests;

public class CookieBannerContentSeederTests
{
    private sealed class Harness
    {
        public IContentService ContentService { get; init; } = null!;
        public IContentTypeService ContentTypeService { get; init; } = null!;
        public IEntityService EntityService { get; init; } = null!;
        public IContent Policy { get; init; } = null!;
        public Func<BlockListValue?> Registry { get; init; } = null!;
        public CookieBannerContentSeeder Seeder { get; init; } = null!;
    }

    private static Harness CreateSut(string cookieName, bool alreadySeeded = false, params IContent[] existingOfType)
    {
        var contentType = Substitute.For<IContentType>();
        contentType.Id.Returns(1234);
        var contentTypeService = Substitute.For<IContentTypeService>();
        contentTypeService.Get(CookiePolicyPageResolver.ContentTypeAlias).Returns(contentType);

        var root = Substitute.For<IContent>();
        root.Id.Returns(1000);
        root.Name.Returns("Home");

        var policy = Substitute.For<IContent>();

        var contentService = Substitute.For<IContentService>();
        long total;
        contentService
            .GetPagedOfType(default, default, default, out total, default, default)
            .ReturnsForAnyArgs(existingOfType);
        contentService.GetRootContent().Returns(new[] { root });
        contentService
            .Create(Arg.Any<string>(), Arg.Any<int>(), Arg.Any<string>(), Arg.Any<int>())
            .Returns(policy);
        contentService
            .Publish(Arg.Any<IContent>(), Arg.Any<string[]>(), Arg.Any<int>())
            .Returns(new PublishResult(PublishResultType.SuccessPublish, new EventMessages(), policy));

        BlockListValue? registry = null;
        var jsonSerializer = Substitute.For<IJsonSerializer>();
        jsonSerializer
            .Serialize(Arg.Do<object>(value => registry ??= value as BlockListValue))
            .Returns("[]");

        var entityService = Substitute.For<IEntityService>();
        // Exists() drives the idempotency guard: false = 'not seeded yet, go ahead'.
        entityService
            .Exists(CookieBannerKeys.Nodes.CookiePolicy, UmbracoObjectTypes.Document)
            .Returns(alreadySeeded);

        var seeder = new CookieBannerContentSeeder(
            contentService,
            contentTypeService,
            entityService,
            jsonSerializer,
            Options.Create(new CookieBannerOptions { CookieName = cookieName, CookieLifetimeDays = 365 }),
            NullLogger<CookieBannerContentSeeder>.Instance);

        return new Harness
        {
            ContentService = contentService,
            ContentTypeService = contentTypeService,
            EntityService = entityService,
            Policy = policy,
            Registry = () => registry,
            Seeder = seeder,
        };
    }

    private static string?[] CookieNames(BlockListValue registry)
        => registry.ContentData
            .SelectMany(block => block.Values)
            .Where(value => value.Alias == "cookieName")
            .Select(value => value.Value as string)
            .ToArray();

    [Fact]
    public void Declares_the_consent_cookie_under_its_configured_name()
    {
        // Pins that the seeded registry reads CookieName from options rather than hardcoding a
        // site's cookie: NDSTK's seeder wrote the literal "ndstk-consent", and a package that
        // ships a policy page naming the wrong cookie publishes a false legal declaration.
        Harness harness = CreateSut("site-consent");

        harness.Seeder.EnsurePolicyPage();

        BlockListValue? registry = harness.Registry();
        Assert.NotNull(registry);
        Assert.Equal(
            new[] { "site-consent", ".AspNetCore.Antiforgery.*", "UMB_MEMBER" },
            CookieNames(registry!));
    }

    [Fact]
    public void Declares_every_seeded_cookie_as_a_necessary_browser_cookie()
    {
        // Pins the category/storageType of the three generic declarations. These three are set
        // before any consent exists, so any category other than necessary would make the page
        // contradict what the banner actually does.
        Harness harness = CreateSut("site-consent");

        harness.Seeder.EnsurePolicyPage();

        BlockListValue? registry = harness.Registry();
        Assert.NotNull(registry);
        Assert.Equal(3, registry!.ContentData.Count);
        Assert.All(registry.ContentData, block =>
        {
            Assert.Equal(
                CookieBannerKeys.ElementTypes.CookieDefinition,
                block.ContentTypeKey);
            Assert.Contains(
                block.Values,
                value => value.Alias == "category" && (value.Value as string) == "[\"necessary\"]");
            Assert.Contains(
                block.Values,
                value => value.Alias == "storageType" && (value.Value as string) == "[\"Cookie\"]");
        });
    }

    [Fact]
    public void Does_not_add_a_second_policy_page_when_the_site_already_has_one()
    {
        // Pins idempotency across the by-type guard, which is what protects a consuming site that
        // seeds its OWN localised policy page (the NDSTK migration path) from getting a second,
        // English one bolted on at every boot.
        Harness harness = CreateSut("site-consent", alreadySeeded: false, Substitute.For<IContent>());

        harness.Seeder.EnsurePolicyPage();

        harness.ContentService.DidNotReceiveWithAnyArgs().Create(default!, default(int), default!, default);
        harness.ContentService.DidNotReceiveWithAnyArgs().Publish(default!, default!, default);
    }

    [Fact]
    public void Does_nothing_when_the_seeded_node_already_exists()
    {
        // Pins the key-based idempotency guard, which is the one that makes a SECOND BOOT a no-op
        // (Task 17's manual check asserts exactly this). It also pins the API choice: the guard
        // must go through IEntityService.Exists, because neither GetById(Guid) overload exists on
        // both Umbraco.Cms.Core 17.0.0 and 18.1.1 - see the comment in CookieBannerContentSeeder.
        Harness harness = CreateSut("site-consent", alreadySeeded: true);

        harness.Seeder.EnsurePolicyPage();

        harness.EntityService
            .Received(1)
            .Exists(CookieBannerKeys.Nodes.CookiePolicy, UmbracoObjectTypes.Document);
        harness.ContentService.DidNotReceiveWithAnyArgs().Create(default!, default(int), default!, default);
        harness.ContentService.DidNotReceiveWithAnyArgs().Save(default!, default(int?), default);
        harness.ContentService.DidNotReceiveWithAnyArgs().Publish(default!, default!, default);
    }
}
```

- [ ] **Step 6: Run the seeder test to verify it fails**

Run: `dotnet test Esatto.Umbraco.Backoffice.CookieBanner.Tests/Esatto.Umbraco.Backoffice.CookieBanner.Tests.csproj --filter FullyQualifiedName~CookieBannerContentSeederTests`
Expected: FAIL — build error `error CS0246: The type or namespace name 'CookieBannerContentSeeder' could not be found` and `error CS0117: 'CookieBannerKeys' does not contain a definition for 'Nodes'`.

- [ ] **Step 7: Add the seeded-node GUID to CookieBannerKeys**

Edit `c:\src\Esatto.Packages\Esatto.Umbraco.Backoffice.CookieBanner\src\CookieBannerKeys.cs`, appending inside the existing `CookieBannerKeys` class (Task 7 created the `DataTypes`, `ElementTypes`, `DocumentTypes` and `Templates` nested classes; this adds only `Nodes`):

```csharp
    /// <summary>
    /// Content nodes the seeder creates. Continues the c00c1e00 series with the -0005- segment so
    /// the whole package occupies one readable GUID namespace and a uSync export produces the
    /// same key on every environment.
    /// </summary>
    internal static class Nodes
    {
        internal static readonly Guid CookiePolicy = new("c00c1e00-0005-4000-8000-000000000001");
    }
```

- [ ] **Step 8: Implement the seeder**

Create `c:\src\Esatto.Packages\Esatto.Umbraco.Backoffice.CookieBanner\src\CookieBannerContentSeeder.cs`:

```csharp
using System;
using System.Collections.Generic;
using System.Linq;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Umbraco.Cms.Core;
using Umbraco.Cms.Core.Models;
using Umbraco.Cms.Core.Models.Blocks;
using Umbraco.Cms.Core.Serialization;
using Umbraco.Cms.Core.Services;

namespace Esatto.Umbraco.Backoffice.CookieBanner;

/// <summary>
/// Creates and publishes a cookie policy page so a fresh install has somewhere for the banner and
/// the footer link to point at, pre-declaring the three cookies that are generic to every Umbraco
/// site. Idempotent: once a policy page exists - the package's or the site's own - this does
/// nothing on every later boot.
/// </summary>
internal sealed class CookieBannerContentSeeder(
    IContentService contentService,
    IContentTypeService contentTypeService,
    IEntityService entityService,
    IJsonSerializer jsonSerializer,
    IOptions<CookieBannerOptions> options,
    ILogger<CookieBannerContentSeeder> logger)
{
    // IContentService still only takes an integer user id, so the obsolete constant is the only
    // option here. Swap to SuperUserKey once the content service exposes key-based overloads.
#pragma warning disable CS0618
    private const int UserId = Constants.Security.SuperUserId;
#pragma warning restore CS0618

    private static readonly string[] AllCultures = ["*"];

    public void EnsurePolicyPage()
    {
        // DO NOT replace this with contentService.GetById(Guid). Verified by DECOMPILING the real
        // Umbraco.Cms.Core assemblies (an earlier XML-doc-based reading of this was wrong):
        //   17.0.0: IContentService re-declares GetById(Guid) directly, with `new`, hiding the
        //           inherited IContentServiceBase<T>.GetById(Guid) - which is also present.
        //   18.1.1: that re-declaration is gone; GetById(Guid) is reachable only by inheritance.
        // Each major compiles fine alone, so this is invisible without a cross-version test. But a
        // library compiled against 17.0.0 binds the callvirt to IContentService::GetById(Guid)
        // specifically, and that member no longer exists there on 18 - so it throws
        // MissingMethodException at runtime. Reproduced with a real cross-version binary test, and
        // documented on neither the 18 breaking-changes page nor its release notes.
        // IEntityService.Exists(Guid, UmbracoObjectTypes) is present and identical in both, and an
        // existence check is all this method needs.
        if (entityService.Exists(CookieBannerKeys.Nodes.CookiePolicy, UmbracoObjectTypes.Document))
        {
            return;
        }

        IContentType? contentType = contentTypeService.Get(CookiePolicyPageResolver.ContentTypeAlias);
        if (contentType is null)
        {
            logger.LogWarning(
                "Skipping the cookie policy page: the '{Alias}' document type does not exist yet.",
                CookiePolicyPageResolver.ContentTypeAlias);
            return;
        }

        // An editor - or a consuming site's own seeder, which is the NDSTK migration path - may
        // already have a policy page under a different key. Never add a second one.
        if (contentService.GetPagedOfType(contentType.Id, 0, 1, out _, null, null).Any())
        {
            return;
        }

        IContent? root = contentService.GetRootContent().FirstOrDefault();
        if (root is null)
        {
            // No site root yet, so there is nothing to parent a page to and a root-level policy
            // page would read as a second site. A later boot, once the site has a home page,
            // picks this up.
            logger.LogInformation(
                "Skipping the cookie policy page: the content tree has no root node yet.");
            return;
        }

        IContent policy = contentService.Create(
            "Cookies", root.Id, CookiePolicyPageResolver.ContentTypeAlias, UserId);
        policy.Key = CookieBannerKeys.Nodes.CookiePolicy;
        policy.SetValue("heading", "Cookies on this site");
        policy.SetValue(
            "introduction",
            "<p>We use cookies to make this site work. Below you can see exactly which cookies "
            + "we set, why, and how long they are kept.</p>");
        policy.SetValue(
            "outro",
            "<p>You can also block and delete cookies in your browser settings. Editors: replace "
            + "this text, and add any cookies set by services this site embeds.</p>");

        // Only the cookies every Umbraco site sets regardless of what it embeds. An invented
        // table would be worse than a short one, so the rest is left to an editor and to the
        // scanner package.
        policy.SetValue("cookies", BlockList(
            Block(CookieBannerKeys.ElementTypes.CookieDefinition,
                // Read from options, never hardcoded: a consumer that pins CookieName so its
                // existing visitors are not re-prompted must not end up with a policy page
                // declaring a cookie name the site does not set.
                ("cookieName", options.Value.CookieName),
                ("provider", "This website"),
                ("category", Dropdown("necessary")),
                ("purpose", "Stores your cookie choices so we do not have to ask again."),
                ("duration", $"{options.Value.CookieLifetimeDays} days"),
                ("storageType", Dropdown("Cookie"))),
            Block(CookieBannerKeys.ElementTypes.CookieDefinition,
                ("cookieName", ".AspNetCore.Antiforgery.*"),
                ("provider", "This website"),
                ("category", Dropdown("necessary")),
                ("purpose", "Protects forms against cross-site request forgery."),
                ("duration", "Session"),
                ("storageType", Dropdown("Cookie"))),
            Block(CookieBannerKeys.ElementTypes.CookieDefinition,
                ("cookieName", "UMB_MEMBER"),
                ("provider", "Umbraco"),
                ("category", Dropdown("necessary")),
                ("purpose", "Keeps a signed-in member logged in."),
                ("duration", "Session"),
                ("storageType", Dropdown("Cookie")))));

        contentService.Save(policy, UserId);

        PublishResult result = contentService.Publish(policy, AllCultures, UserId);
        if (result.Success is false)
        {
            logger.LogWarning(
                "Created the cookie policy page but could not publish it: {Status}.", result.Result);
            return;
        }

        logger.LogInformation(
            "Created and published the cookie policy page under '{Root}'.", root.Name);
    }

    /// <summary>The flexible dropdown always stores an array, even in single-value mode.</summary>
    private string Dropdown(string value) => jsonSerializer.Serialize(new[] { value });

    private static BlockItemData Block(Guid elementTypeKey, params (string Alias, object Value)[] values)
        => new()
        {
            Key = Guid.NewGuid(),
            ContentTypeKey = elementTypeKey,
            Values = values
                .Select(value => new BlockPropertyValue { Alias = value.Alias, Value = value.Value })
                .ToList(),
        };

    /// <summary>
    /// Assembles the Block List property value: the layout referencing each block, the block
    /// content itself, and the "expose" list that marks the blocks as visible.
    /// </summary>
    private string BlockList(params BlockItemData[] blocks)
    {
        var value = new BlockListValue
        {
            Layout = new Dictionary<string, IEnumerable<IBlockLayoutItem>>
            {
                [Constants.PropertyEditors.Aliases.BlockList] =
                    blocks.Select(block => new BlockListLayoutItem(block.Key)).ToArray(),
            },
            ContentData = [.. blocks],
            SettingsData = [],
            Expose = blocks.Select(block => new BlockItemVariation(block.Key, null, null)).ToList(),
        };

        return jsonSerializer.Serialize(value);
    }
}
```

- [ ] **Step 9: Run both test classes to verify they pass**

Run: `dotnet test Esatto.Umbraco.Backoffice.CookieBanner.Tests/Esatto.Umbraco.Backoffice.CookieBanner.Tests.csproj --filter "FullyQualifiedName~CookiePolicyPageResolverTests|FullyQualifiedName~CookieBannerContentSeederTests"`
Expected: PASS — `Passed! - Failed: 0, Passed: 6`

- [ ] **Step 10: Commit**

```bash
git add Esatto.Umbraco.Backoffice.CookieBanner/src/CookiePolicyPageResolver.cs Esatto.Umbraco.Backoffice.CookieBanner/src/CookieBannerContentSeeder.cs Esatto.Umbraco.Backoffice.CookieBanner/src/CookieBannerKeys.cs Esatto.Umbraco.Backoffice.CookieBanner.Tests/CookiePolicyPageResolverTests.cs Esatto.Umbraco.Backoffice.CookieBanner.Tests/CookieBannerContentSeederTests.cs
git commit -m "Resolve the cookie policy page by document type and seed one on install"
```

---

### Task 17: Composer, install handler, and the end-to-end install verification

**Files:**
- Create: `c:\src\Esatto.Packages\Esatto.Umbraco.Backoffice.CookieBanner\src\CookieBannerInstallHandler.cs`
- Modify: `c:\src\Esatto.Packages\Esatto.Umbraco.Backoffice.CookieBanner\src\CookieBannerComposer.cs` (extend the class Task 6 created — do NOT add a second composer)
- Test: `c:\src\Esatto.Packages\Esatto.Umbraco.Backoffice.CookieBanner.Tests\CookieBannerInstallHandlerTests.cs`

**Interfaces:**
- Consumes: `CookieBannerSchemaInstaller.InstallAsync()` (Task 12), `CookieBannerDictionaryInstaller.InstallAsync()` (Task 15), `CookieBannerContentSeeder.EnsurePolicyPage()` (Task 16), `ICookiePolicyPageResolver` / `CookiePolicyPageResolver` (Task 16), `CookieBannerContentTypeFactory` (Task 11), `public sealed class CookieBannerComposer : IComposer` (Task 6).
- Produces: `internal sealed class CookieBannerInstallHandler : INotificationAsyncHandler<UmbracoApplicationStartedNotification>` — ctor `(IRuntimeState, CookieBannerSchemaInstaller, CookieBannerDictionaryInstaller, CookieBannerContentSeeder, ILogger<CookieBannerInstallHandler>)`; the composer's install-time DI registrations, which the tag helpers and views (Tasks 18-21) resolve `ICookiePolicyPageResolver` from.

**Registration split — which task added what.** Task 6 created `CookieBannerComposer` and registered the *request-time* consent surface: the `CookieBannerOptions` binding from the `Esatto:CookieBanner` section, `IConsentState`/`ConsentState`, `ConsentCookieWriter`, `IConsentThrottle`/`ConsentThrottle`, `IConsentTextProvider`/`ConsentTextProvider`. **This** task adds the *install-time* surface to the same class: `CookieBannerContentTypeFactory`, `CookieBannerSchemaInstaller`, `CookieBannerDictionaryInstaller`, `CookieBannerContentSeeder`, `ICookiePolicyPageResolver`, and the `AddNotificationAsyncHandler<UmbracoApplicationStartedNotification, CookieBannerInstallHandler>()` call. Nothing Task 6 registered is removed or re-registered.

- [ ] **Step 1: Write the failing test**

Create `c:\src\Esatto.Packages\Esatto.Umbraco.Backoffice.CookieBanner.Tests\CookieBannerInstallHandlerTests.cs`:

```csharp
using System;
using System.Linq;
using System.Reflection;
using Esatto.Umbraco.Backoffice.CookieBanner;
using Umbraco.Cms.Core.Composing;
using Umbraco.Cms.Core.Events;
using Umbraco.Cms.Core.Notifications;
using Umbraco.Cms.Core.Services;
using Xunit;

namespace Esatto.Umbraco.Backoffice.CookieBanner.Tests;

public class CookieBannerInstallHandlerTests
{
    [Fact]
    public void Composer_is_public_so_the_Umbraco_type_loader_discovers_it()
    {
        // Pins the package's entire zero-config promise. Umbraco's TypeLoader only scans PUBLIC
        // IComposer implementations in referenced assemblies; marking the composer internal - an
        // easy tidy-up, since everything it registers is internal - would silently install
        // nothing at all, with no error anywhere.
        Type composer = typeof(CookieBannerComposer);

        Assert.True(composer.IsPublic, "CookieBannerComposer must be public or Umbraco will not find it.");
        Assert.True(composer.IsSealed);
        Assert.Contains(typeof(IComposer), composer.GetInterfaces());
        Assert.NotNull(composer.GetConstructor(Type.EmptyTypes));
    }

    [Fact]
    public void Install_handler_runs_on_application_STARTED_not_starting()
    {
        // Pins the notification choice. UmbracoApplicationStartingNotification fires before the
        // content, content type, dictionary and language services can be used, so wiring the
        // installer there (as the sibling Redirects migration legitimately does, because a SQL
        // migration CAN run that early) would fail on a cold boot.
        Type handler = typeof(CookieBannerInstallHandler);

        Assert.Contains(
            typeof(INotificationAsyncHandler<UmbracoApplicationStartedNotification>),
            handler.GetInterfaces());
        Assert.DoesNotContain(
            typeof(INotificationAsyncHandler<UmbracoApplicationStartingNotification>),
            handler.GetInterfaces());
    }

    [Fact]
    public void Install_handler_takes_the_runtime_state_so_it_can_gate_on_RuntimeLevel_Run()
    {
        // Pins the gate. On an Install/Upgrade/BootFailed runtime the services this handler calls
        // are half-initialised; running the schema installer there is how a site ends up with a
        // partially created content model that the next boot then treats as already installed.
        ConstructorInfo constructor = typeof(CookieBannerInstallHandler)
            .GetConstructors(BindingFlags.Public | BindingFlags.Instance)
            .Single();

        Assert.Contains(
            typeof(IRuntimeState),
            constructor.GetParameters().Select(parameter => parameter.ParameterType));
    }
}
```

- [ ] **Step 2: Run the test to verify it fails**

Run: `dotnet test Esatto.Umbraco.Backoffice.CookieBanner.Tests/Esatto.Umbraco.Backoffice.CookieBanner.Tests.csproj --filter FullyQualifiedName~CookieBannerInstallHandlerTests`
Expected: FAIL — build error `error CS0246: The type or namespace name 'CookieBannerInstallHandler' could not be found` (three occurrences). `CookieBannerComposer` resolves already, from Task 6.

- [ ] **Step 3: Implement the install handler**

Create `c:\src\Esatto.Packages\Esatto.Umbraco.Backoffice.CookieBanner\src\CookieBannerInstallHandler.cs`:

```csharp
using System;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;
using Umbraco.Cms.Core;
using Umbraco.Cms.Core.Events;
using Umbraco.Cms.Core.Notifications;
using Umbraco.Cms.Core.Services;

namespace Esatto.Umbraco.Backoffice.CookieBanner;

/// <summary>
/// Installs the package's content model once Umbraco is up.
/// </summary>
/// <remarks>
/// The order is load-bearing and must not be reshuffled:
/// <list type="number">
///   <item>schema - the two dropdown data types are created and preloaded before the
///     <c>cookieDefinition</c> element type binds to them, and <c>cookieRegistry</c> is created
///     after element types exist;</item>
///   <item>dictionary - the banner's text;</item>
///   <item>content - the policy page, which needs the document type from step 1.</item>
/// </list>
/// A failure here must not take the site down - the backoffice is the place to fix a broken
/// schema - so it is logged and swallowed. Everything downstream degrades gracefully: the
/// resolver returns null when the document type is missing, and the text provider falls back to
/// the embedded resx when the dictionary items are absent.
/// </remarks>
internal sealed class CookieBannerInstallHandler(
    IRuntimeState runtimeState,
    CookieBannerSchemaInstaller schemaInstaller,
    CookieBannerDictionaryInstaller dictionaryInstaller,
    CookieBannerContentSeeder contentSeeder,
    ILogger<CookieBannerInstallHandler> logger)
    : INotificationAsyncHandler<UmbracoApplicationStartedNotification>
{
    public async Task HandleAsync(
        UmbracoApplicationStartedNotification notification, CancellationToken cancellationToken)
    {
        if (runtimeState.Level is not RuntimeLevel.Run)
        {
            logger.LogInformation(
                "Skipping the cookie banner install; runtime level is {Level}.", runtimeState.Level);
            return;
        }

        try
        {
            await schemaInstaller.InstallAsync();
            await dictionaryInstaller.InstallAsync();
            contentSeeder.EnsurePolicyPage();
        }
        catch (Exception exception)
        {
            logger.LogError(exception, "Installing the cookie banner content model failed.");
        }
    }
}
```

- [ ] **Step 4: Extend the composer Task 6 created**

Edit `c:\src\Esatto.Packages\Esatto.Umbraco.Backoffice.CookieBanner\src\CookieBannerComposer.cs` so the whole file reads as below. The first `Compose` block is Task 6's registrations, unchanged; the second block is this task's.

```csharp
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Umbraco.Cms.Core.Composing;
using Umbraco.Cms.Core.DependencyInjection;
using Umbraco.Cms.Core.Notifications;
using Umbraco.Extensions;

namespace Esatto.Umbraco.Backoffice.CookieBanner;

/// <summary>
/// Wires Esatto.Umbraco.Backoffice.CookieBanner into Umbraco.
/// </summary>
/// <remarks>
/// Composers are auto-discovered by Umbraco from any referenced assembly that has
/// <see cref="IComposer" /> implementations, so the request-time consent surface and the content
/// model install both work with NO consumer-side wiring. Only the two Razor tag helpers and
/// <c>app.UseCookieConsent()</c> are the consumer's job.
/// <para>
/// This type MUST stay public: Umbraco's TypeLoader only scans public composers, and an internal
/// one installs nothing while reporting nothing.
/// </para>
/// </remarks>
public sealed class CookieBannerComposer : IComposer
{
    public void Compose(IUmbracoBuilder builder)
    {
        // ---- request-time consent surface (added in the options/services task) ----
        builder.Services.AddCookieConsent();

        // ---- install-time content model (added here) ----
        // Singletons, mirroring the shape this was extracted from. The content type factory
        // carries a mutable data type cache populated by PreloadDataTypesAsync, so the schema
        // installer and the factory must be the same pair of instances for the whole boot.
        builder.Services.AddSingleton<CookieBannerContentTypeFactory>();
        builder.Services.AddSingleton<CookieBannerSchemaInstaller>();
        builder.Services.AddSingleton<CookieBannerDictionaryInstaller>();
        builder.Services.AddSingleton<CookieBannerContentSeeder>();

        // Scoped: the resolver memoises its answer for one request, so <consent-banner /> and the
        // policy template share a single lookup. TryAdd keeps it idempotent alongside
        // AddCookieConsent(), which a consumer may also call explicitly.
        builder.Services.TryAddScoped<ICookiePolicyPageResolver, CookiePolicyPageResolver>();

        // Started, not Starting: the content, content type, dictionary and language services this
        // handler drives are not usable during Starting.
        builder.AddNotificationAsyncHandler<
            UmbracoApplicationStartedNotification, CookieBannerInstallHandler>();
    }
}
```

- [ ] **Step 5: Run the full test suite to verify it passes**

Run: `dotnet test Esatto.Umbraco.Backoffice.CookieBanner.Tests/Esatto.Umbraco.Backoffice.CookieBanner.Tests.csproj`
Expected: PASS — every test in the project, including the three new `CookieBannerInstallHandlerTests`, with `Failed: 0`.

- [ ] **Step 6: Pack the package to the local feed**

The mono-repo has no integration-test pattern and no CI, so from here the verification is a real manual check against a running Umbraco site. Nothing below is optional.

Run: `dotnet pack Esatto.Umbraco.Backoffice.CookieBanner/Esatto.Umbraco.Backoffice.CookieBanner.csproj -c Release -o c:\src\Esatto.Packages\.local-feed`
Expected: `Successfully created package 'c:\src\Esatto.Packages\.local-feed\Esatto.Umbraco.Backoffice.CookieBanner.<MinVer version>.nupkg'`. Note the exact version string printed — the next step needs it.

- [ ] **Step 7: Install it into a scratch site and boot**

Use a throwaway Umbraco 17 site (`dotnet new umbraco -n CookieBannerScratch --friendly-name a --email a@a.com --password 1234567890 --development-database-type SQLite`) or `c:\src\NDSTK` with its cookie feature branch reverted. Then:

```bash
dotnet nuget add source c:\src\Esatto.Packages\.local-feed -n esatto-local
dotnet add package Esatto.Umbraco.Backoffice.CookieBanner --version <version from Step 6> --source c:\src\Esatto.Packages\.local-feed
dotnet run
```

Expected in the console log, in this order: `Seeded 32 cookie dictionary items for <the site's iso codes>.` then `Created and published the cookie policy page under '<root name>'.` No `Installing the cookie banner content model failed.` line, and no exception stack.

- [ ] **Step 8: Verify every installed artefact in the backoffice**

Log in at `/umbraco` and confirm all nine, by name and by GUID where stated (the GUID is on each item's Info tab):

1. **Settings → Data Types → "Cookie category"** — DropDownListFlexible, single-select off (one value), items exactly `necessary`, `preferences`, `statistics`, `marketing` (wire names, not labels). Key `c00c1e00-0001-4000-8000-000000000001`.
2. **Settings → Data Types → "Storage type"** — DropDownListFlexible, items exactly `Cookie`, `localStorage`, `sessionStorage`, `Pixel`. Key `c00c1e00-0001-4000-8000-000000000002`.
3. **Settings → Data Types → "Cookie registry"** — Block List, allowed block list contains `cookieDefinition` and nothing else. Key `c00c1e00-0001-4000-8000-000000000003`.
4. **Settings → Document Types → Element Types → `cookieDefinition`** — icon `icon-lock`, key `c00c1e00-0002-4000-8000-000000000001`, six properties in this order: `cookieName`, `provider`, `category`, `purpose`, `duration`, `storageType`; every description in English (the `duration` description in particular must not be Swedish).
5. **Settings → Document Types → `cookiePolicy`** — icon `icon-lock`, key `c00c1e00-0003-4000-8000-000000000001`, group `content`, properties `heading`, `introduction`, `cookies`, `outro`, and the `cookiePolicy` template allowed and set as default.
6. **Settings → Templates → `cookiePolicy`** — exists, key `c00c1e00-0004-4000-8000-000000000001`.
7. **Dictionary** — a `Cookie.Banner` node with exactly 32 children, every key prefixed `Cookies.`; `Cookies.Policy.On` and `Cookies.Policy.Off` are present; `Cookies.Banner.PolicyLink`, `Cookies.Banner.Label` and `Cookies.Settings.Heading` are absent. Open `Cookies.Banner.Heading`: one filled value per site language the package ships text for, and an empty box (not an error) for any other language.
8. **Content** — a published "Cookies" page under the site root, key `c00c1e00-0005-4000-8000-000000000001`, whose Cookie registry holds exactly three blocks: the value of `Esatto:CookieBanner:CookieName` (set it to something distinctive in `appsettings.Development.json` first and confirm the page shows *that* string, not `cookie-consent`), `.AspNetCore.Antiforgery.*`, and `UMB_MEMBER` — all three category `necessary`, storageType `Cookie`.
9. **Front end** — browse the "Cookies" page's URL and confirm the template renders the three declarations in a table with English column headings.

- [ ] **Step 9: Verify that a second boot changes nothing**

This is the acceptance criterion for the whole install path, not a nicety: the handler runs on **every** application start, so a non-idempotent step corrupts the site on restart.

Before restarting, deliberately perturb three things: rename `Cookies.Banner.AcceptAll`'s English value to `ACCEPT (edited)`, drag `Cookies.Table.Type` out of `Cookie.Banner` to the Dictionary root, and rename the "Cookies" content page to "Cookie policy". Then `Ctrl+C` and `dotnet run` again.

After the second boot, confirm — explicitly, a second boot must change nothing:

- No `Seeded ... cookie dictionary items` line and no `Created and published the cookie policy page` line in the log.
- Data types still 3, element types still 1, document types still 1, templates still 1 — no `cookieDefinition (1)`-style duplicates anywhere.
- `Cookie.Banner` still has 31 children and `Cookies.Table.Type` is still at the Dictionary root where it was dragged (the create-if-missing seeder respects an editor's manual move; it never re-parents an item whose ParentId is already set).
- `Cookies.Banner.AcceptAll` still reads `ACCEPT (edited)` — the seeder never overwrites edited copy.
- Exactly one policy page in the tree, still named "Cookie policy", still published, still with three blocks.

- [ ] **Step 10: Commit**

```bash
git add Esatto.Umbraco.Backoffice.CookieBanner/src/CookieBannerInstallHandler.cs Esatto.Umbraco.Backoffice.CookieBanner/src/CookieBannerComposer.cs Esatto.Umbraco.Backoffice.CookieBanner.Tests/CookieBannerInstallHandlerTests.cs
git commit -m "Run the cookie banner schema, dictionary and content install on application started"
```

### Task 18: README, icon, docs and pack verification

> **Task 1 already created `README.md` (a short placeholder, required because the csproj declares `PackageReadmeFile`) and `icon.png` (a byte copy of the house icon). This task OVERWRITES `README.md` with the full content below and leaves `icon.png` alone — do not recreate either from scratch.**

**Files:**
- Create: `c:\src\Esatto.Packages\Esatto.Umbraco.Backoffice.CookieBanner\README.md`
- Create: `c:\src\Esatto.Packages\Esatto.Umbraco.Backoffice.CookieBanner\icon.png` (copy of the shared house icon)
- Create: `c:\src\Esatto.Packages\Esatto.Umbraco.Backoffice.CookieBanner\docs\consent-dialog.png`
- Create: `c:\src\Esatto.Packages\Esatto.Umbraco.Backoffice.CookieBanner\docs\consent-customise.png`
- Create: `c:\src\Esatto.Packages\Esatto.Umbraco.Backoffice.CookieBanner\docs\cookie-policy-page.png`
- Create: `c:\src\Esatto.Packages\Esatto.Umbraco.Backoffice.CookieBanner\docs\cookie-registry-editor.png`
- Test: `c:\src\Esatto.Packages\Esatto.Umbraco.Backoffice.CookieBanner.Tests\PackagingMetadataTests.cs`

**Interfaces:**
- Consumes: `public const string CookieBannerOptions.SectionName = "Esatto:CookieBanner";`, `public int CookieBannerOptions.PolicyVersion { get; set; }`, `public string CookieBannerOptions.CookieName { get; set; }`, `public int CookieBannerOptions.CookieLifetimeDays { get; set; }`, `public string? CookieBannerOptions.GoogleMeasurementId { get; set; }`, `public Guid? CookieBannerOptions.PolicyPageKey { get; set; }`, `public string CookieBannerOptions.EndpointPath { get; set; }`, `public int CookieBannerOptions.ThrottleRequestsPerMinute { get; set; }`, `IUmbracoBuilder AddCookieConsent(this IUmbracoBuilder)`, `IApplicationBuilder UseCookieConsent(this IApplicationBuilder)`, the four tag helpers in `Esatto.Umbraco.Backoffice.CookieBanner.TagHelpers`, `window.cookieConsent`, the `--consent-*` token layer in `wwwroot/esatto-cookiebanner/consent.css`
- Produces: `public sealed class Esatto.Umbraco.Backoffice.CookieBanner.Tests.PackagingMetadataTests` (nothing downstream depends on it)

- [ ] **Step 1: Write the failing test**

Create `c:\src\Esatto.Packages\Esatto.Umbraco.Backoffice.CookieBanner.Tests\PackagingMetadataTests.cs`:

```csharp
using System.Reflection;
using System.Text.RegularExpressions;
using Xunit;

namespace Esatto.Umbraco.Backoffice.CookieBanner.Tests;

/// <summary>
/// Guards the shipped documentation surface. Every assertion here failed at least once by hand
/// during the 1.0.0 pack: a relative README image (invisible on nuget.org), an option added to
/// <see cref="CookieBannerOptions"/> and never documented, and a missing icon that made
/// <c>dotnet pack</c> emit NU5046.
/// </summary>
public sealed class PackagingMetadataTests
{
    private const string RawImagePrefix =
        "https://raw.githubusercontent.com/carl-schele-esatto/Esatto.Packages/main/"
        + "Esatto.Umbraco.Backoffice.CookieBanner/docs/";

    private static readonly Regex MarkdownImage = new(@"!\[[^\]]*\]\(([^)]+)\)", RegexOptions.Compiled);

    // bin/<Config>/net10.0 -> test project -> repo root.
    private static string RepoRoot =>
        Path.GetFullPath(Path.Combine(AppContext.BaseDirectory, "..", "..", "..", ".."));

    private static string PackageDirectory =>
        Path.Combine(RepoRoot, "Esatto.Umbraco.Backoffice.CookieBanner");

    private static string ReadmePath => Path.Combine(PackageDirectory, "README.md");

    [Fact]
    public void Readme_documents_every_configuration_option()
    {
        // Pins: an option can never be added to CookieBannerOptions without landing in the README table.
        var readme = File.ReadAllText(ReadmePath);

        Assert.Contains(CookieBannerOptions.SectionName, readme, StringComparison.Ordinal);

        PropertyInfo[] properties = typeof(CookieBannerOptions)
            .GetProperties(BindingFlags.Public | BindingFlags.Instance);

        Assert.NotEmpty(properties);

        foreach (PropertyInfo property in properties)
        {
            Assert.Contains($"`{property.Name}`", readme, StringComparison.Ordinal);
        }
    }

    [Fact]
    public void Readme_images_are_absolute_raw_githubusercontent_urls()
    {
        // Pins: nuget.org rewrites nothing, so a relative image path renders as a broken image on the
        // package page. Every image must be an absolute raw.githubusercontent URL on the main branch.
        var readme = File.ReadAllText(ReadmePath);

        MatchCollection matches = MarkdownImage.Matches(readme);
        Assert.NotEmpty(matches);

        foreach (Match match in matches)
        {
            Assert.StartsWith(RawImagePrefix, match.Groups[1].Value, StringComparison.Ordinal);
        }
    }

    [Fact]
    public void Every_readme_image_exists_in_the_docs_folder()
    {
        // Pins: the absolute URL only resolves once the file is actually committed under docs/.
        var readme = File.ReadAllText(ReadmePath);

        foreach (Match match in MarkdownImage.Matches(readme))
        {
            var fileName = match.Groups[1].Value[RawImagePrefix.Length..];
            var path = Path.Combine(PackageDirectory, "docs", fileName);

            Assert.True(File.Exists(path), $"README references docs/{fileName} but {path} does not exist.");
        }
    }

    [Fact]
    public void Package_ships_the_shared_house_icon()
    {
        // Pins: PackageIcon=icon.png is declared in the csproj, so a missing file breaks `dotnet pack` (NU5046).
        var icon = Path.Combine(PackageDirectory, "icon.png");

        Assert.True(File.Exists(icon), $"{icon} does not exist.");
        Assert.Equal(
            new FileInfo(Path.Combine(RepoRoot, "Esatto.Umbraco.Backoffice.Redirects", "icon.png")).Length,
            new FileInfo(icon).Length);
    }

    [Fact]
    public void Csproj_carries_the_nuget_metadata_the_marketplace_needs()
    {
        // Pins: the Umbraco Marketplace only lists a package carrying the umbraco-marketplace tag,
        // and the repo invariant is that every package exposes its source-code link.
        var csproj = File.ReadAllText(
            Path.Combine(PackageDirectory, "Esatto.Umbraco.Backoffice.CookieBanner.csproj"));

        Assert.Contains("<PackageId>Esatto.Umbraco.Backoffice.CookieBanner</PackageId>", csproj, StringComparison.Ordinal);
        Assert.Contains("umbraco-marketplace", csproj, StringComparison.Ordinal);
        Assert.Contains("<PackageReadmeFile>README.md</PackageReadmeFile>", csproj, StringComparison.Ordinal);
        Assert.Contains("<PackageIcon>icon.png</PackageIcon>", csproj, StringComparison.Ordinal);
        Assert.Contains("<PackageLicenseExpression>MIT</PackageLicenseExpression>", csproj, StringComparison.Ordinal);
        Assert.Contains(
            "<PackageProjectUrl>https://github.com/carl-schele-esatto/Esatto.Packages/tree/main/Esatto.Umbraco.Backoffice.CookieBanner</PackageProjectUrl>",
            csproj,
            StringComparison.Ordinal);
        Assert.Contains(
            "<RepositoryUrl>https://github.com/carl-schele-esatto/Esatto.Packages</RepositoryUrl>",
            csproj,
            StringComparison.Ordinal);
        Assert.Contains("<RepositoryType>git</RepositoryType>", csproj, StringComparison.Ordinal);
    }
}
```

- [ ] **Step 2: Run the test to verify it fails**

Run: `dotnet test Esatto.Umbraco.Backoffice.CookieBanner.Tests/Esatto.Umbraco.Backoffice.CookieBanner.Tests.csproj --filter FullyQualifiedName~PackagingMetadataTests`

Expected: FAIL — 4 of 5 tests fail. The three README tests fail with
`System.IO.FileNotFoundException : Could not find file 'c:\src\Esatto.Packages\Esatto.Umbraco.Backoffice.CookieBanner\README.md'`
and `Package_ships_the_shared_house_icon` fails with
`Assert.True() Failure — c:\src\Esatto.Packages\Esatto.Umbraco.Backoffice.CookieBanner\icon.png does not exist.`
`Csproj_carries_the_nuget_metadata_the_marketplace_needs` passes (the csproj metadata landed in Task 1).

- [ ] **Step 3: Copy the shared house icon**

Every sibling package carries the identical icon (`md5 6b99a2152a4c7eab1f26f4846e4e8d5f`, 26 070 bytes). Copy it rather than authoring a new one:

```bash
cp "c:/src/Esatto.Packages/Esatto.Umbraco.Backoffice.Redirects/icon.png" \
   "c:/src/Esatto.Packages/Esatto.Umbraco.Backoffice.CookieBanner/icon.png"
md5sum "c:/src/Esatto.Packages/Esatto.Umbraco.Backoffice.CookieBanner/icon.png"
```

Expected: `6b99a2152a4c7eab1f26f4846e4e8d5f`

- [ ] **Step 4: Capture the four documentation screenshots**

`Esatto.Umbraco.Backoffice.Redirects` carries exactly one screenshot, `docs/redirects-dashboard.png`, and `Esatto.Umbraco.Backoffice.SharedPreviewLink` carries three; this package needs four because it has both a visitor-facing and an editor-facing surface. Run the NDSTK consumer site against the freshly packed CookieBanner (`dotnet run --project c:\src\NDSTK\NDSTK.csproj`), then capture each shot at 1× on a 1280 px-wide viewport and save it to the exact path below — the filenames are asserted by `Every_readme_image_exists_in_the_docs_folder`:

```bash
mkdir -p "c:/src/Esatto.Packages/Esatto.Umbraco.Backoffice.CookieBanner/docs"
```

| File | What it must show |
|---|---|
| `docs/consent-dialog.png` | first run on the front end: the blocking `<dialog>` with **Accept all**, **Reject all**, **Customise** |
| `docs/consent-customise.png` | the same dialog after **Customise**: the four category rows with `necessary` checked and disabled, plus **Save choices** |
| `docs/cookie-policy-page.png` | the rendered `cookiePolicy` page — the per-category cookie tables and the current-choice/withdraw block |
| `docs/cookie-registry-editor.png` | the backoffice `cookiePolicy` content node, the **Cookies** Block List with two `cookieDefinition` blocks expanded |

- [ ] **Step 5: Write the README**

Create `c:\src\Esatto.Packages\Esatto.Umbraco.Backoffice.CookieBanner\README.md`:

```markdown
# Esatto.Umbraco.Backoffice.CookieBanner

Turnkey GDPR/ePrivacy cookie consent for Umbraco 17 & 18. A blocking consent dialog, per-category gating of scripts and embeds, Google Consent Mode v2 signalling, an editor-managed cookie registry, and a rendered cookie policy page — from two lines of Razor and one line of `Program.cs`.

- Nothing that needs consent reaches the browser before a choice is made — gated `<script>` tags are suppressed **server-side**, so there is no window in which they could execute
- Four fixed categories: `necessary`, `preferences`, `statistics`, `marketing` — they map 1:1 onto Consent Mode v2 signals and onto the cookie wire format
- Consent Mode v2 `default` → `update` → `config`, emitted inline in `<head>` before any Google tag loads, and only when a measurement id is configured
- Editors maintain the cookie declarations themselves in a Block List, and the policy page renders them grouped by category
- Consent copy lives in Umbraco dictionary items, so legal wording changes without a deploy; the package ships `en` and `sv` fallbacks as embedded resources
- `PolicyVersion` re-prompts every visitor when the wording or the cookie set changes
- No SQL table, no migration, no `MapControllers()`, no `AddRateLimiter` — the endpoint and its throttle are package-owned

## How it works

On a first visit the package renders a native `<dialog>` as the first element in `<body>`, so focus trap, `Escape` and the inert backdrop all come from the platform.

![Consent dialog](https://raw.githubusercontent.com/carl-schele-esatto/Esatto.Packages/main/Esatto.Umbraco.Backoffice.CookieBanner/docs/consent-dialog.png)

**Customise** reveals the per-category choice. `necessary` is checked and disabled — it is implied, never client-supplied, and never written to the cookie.

![Per-category choice](https://raw.githubusercontent.com/carl-schele-esatto/Esatto.Packages/main/Esatto.Umbraco.Backoffice.CookieBanner/docs/consent-customise.png)

The decision is posted to a package-owned endpoint, which writes the cookie server-side — that is what guarantees the attributes are right (`SameSite=Lax`, `Secure` tracking the actual scheme, lifetime from configuration).

Editors declare the site's cookies in a Block List on the installed cookie policy page.

![Cookie registry editor](https://raw.githubusercontent.com/carl-schele-esatto/Esatto.Packages/main/Esatto.Umbraco.Backoffice.CookieBanner/docs/cookie-registry-editor.png)

The policy page renders those declarations grouped by category, plus the visitor's current choice, a reopen button and a withdraw button.

![Cookie policy page](https://raw.githubusercontent.com/carl-schele-esatto/Esatto.Packages/main/Esatto.Umbraco.Backoffice.CookieBanner/docs/cookie-policy-page.png)

## Install

```bash
dotnet add package Esatto.Umbraco.Backoffice.CookieBanner
```

Installing the package registers the services and installs the schema on first start via its composer. Three lines wire up the rendering.

In `Program.cs`, after `BootUmbracoAsync()`:

```csharp
app.UseCookieConsent();
```

In your layout — `<consent-head />` goes in `<head>` **after** your own stylesheet so your token overrides win, and `<consent-banner />` goes first in `<body>`, before `<header>`, so the dialog is reachable in DOM tab order:

```cshtml
<consent-head />
<consent-banner />
```

`_ViewImports.cshtml` needs the tag helpers registered once:

```cshtml
@using Esatto.Umbraco.Backoffice.CookieBanner.TagHelpers
@addTagHelper *, Esatto.Umbraco.Backoffice.CookieBanner
```

`builder.CreateUmbracoBuilder().AddCookieConsent()` and `builder.Services.AddCookieConsent()` are also available if you prefer registering explicitly; both are idempotent and both are already done for you by the composer.

## Configuration

Bound from the `Esatto:CookieBanner` section. Every value has a package-neutral default, so an empty section is a working configuration.

| Option | Type | Default | Notes |
|---|---|---|---|
| `PolicyVersion` | `int` | `1` | Bumping it re-prompts every visitor whose cookie carries an older version |
| `CookieName` | `string` | `cookie-consent` | Change it only on a fresh site — renaming it re-prompts everyone |
| `CookieLifetimeDays` | `int` | `365` | Cookie expiry |
| `GoogleMeasurementId` | `string?` | `null` | Non-null switches on the entire Consent Mode + gtag block in `<consent-head />` |
| `PolicyPageKey` | `Guid?` | `null` | Optional override; by default the first published `cookiePolicy` node is used |
| `EndpointPath` | `string` | `/api/cookie-consent` | Where `UseCookieConsent()` maps the decision endpoint |
| `ThrottleRequestsPerMinute` | `int` | `10` | Per-IP sliding window on that endpoint; the excess gets HTTP 429 |

```json
{
  "Esatto": {
    "CookieBanner": {
      "PolicyVersion": 1,
      "CookieName": "cookie-consent",
      "CookieLifetimeDays": 365,
      "GoogleMeasurementId": "G-XXXXXXXXXX",
      "EndpointPath": "/api/cookie-consent",
      "ThrottleRequestsPerMinute": 10
    }
  }
}
```

## Tag helpers

| Tag | Attributes | What it does |
|---|---|---|
| `<consent-head />` | — | Links `/esatto-cookiebanner/consent.css`, then — only when `GoogleMeasurementId` is set — emits the inline Consent Mode `default` + `update` + `config` block and the gtag `<script>` |
| `<consent-banner />` | — | Renders the consent dialog and `/esatto-cookiebanner/consent.js` with its configuration data attributes |
| `<consent-script>` | `category`, `src`, `async` | Emits a `<script>` **only** when the category is granted; otherwise the element never reaches the browser at all |
| `<consent-embed />` | `category`, `src`, `title` | Renders the `<iframe>` when granted; otherwise a placeholder inviting the visitor to grant that category. The placeholder never contains the embed URL in any form, not even in a data attribute |

`category` binds a `ConsentCategory` member, so the value must match the PascalCase member name exactly — `category="Statistics"`, not `category="statistics"`. A lowercase value fails at compile time with CS0117.

```cshtml
<consent-script category="Statistics" async src="https://example.com/analytics.js"></consent-script>

<consent-embed category="Marketing"
               src="https://www.youtube-nocookie.com/embed/dQw4w9WgXcQ"
               title="Product tour" />
```

## Endpoint

`UseCookieConsent()` maps one minimal-API route at `EndpointPath` (default `/api/cookie-consent`):

```
POST /api/cookie-consent
```

```json
{ "action": "custom", "categories": ["statistics", "preferences"] }
```

`action` is one of `accept-all`, `reject-all`, `custom`, `withdrawn`; anything else is a `400`. `categories` is the exact set to grant — the server grants every name in it that parses and is not `necessary`, and discards the rest. The action is recorded but never infers a category set, so a client sending `accept-all` sends the full list explicitly.

```json
{ "version": 1, "categories": ["preferences","statistics"], "consentId": "…", "decidedAt": "…" }
```

`429` once a client IP exceeds `ThrottleRequestsPerMinute` inside a rolling minute. No `AddRateLimiter`, no `UseRateLimiter` placement and no `MapControllers()` are required.

The response also sets the consent cookie: `Path=/`, `SameSite=Lax`, `HttpOnly=false` (the banner reads it to unblock scripts without a reload), `Secure` when the request is HTTPS, `IsEssential=true`. Its value is compact JSON, URL-encoded once:

```
{"v":1,"t":"2026-08-23T09:41:02.1234567+00:00","c":["preferences","statistics"],"id":"…"}
```

A middleware adds `Vary: Cookie` and `Cache-Control: private, no-cache` to `text/html` responses, so a shared cache never serves one visitor's gating decision to another.

## What gets installed into Umbraco

On the first start at `RuntimeLevel.Run`, a notification handler installs six schema artefacts under the package's own GUID namespace. Install is create-if-missing throughout; failures are logged and swallowed rather than blocking boot.

| Artefact | Alias | Kind |
|---|---|---|
| Cookie category | — | Dropdown, single select: `necessary`, `preferences`, `statistics`, `marketing` |
| Storage type | — | Dropdown, single select: `Cookie`, `localStorage`, `sessionStorage`, `Pixel` |
| Cookie definition | `cookieDefinition` | Element type: `cookieName`, `provider`, `category`, `purpose`, `duration`, `storageType` |
| Cookie registry | — | Block List allowing only `cookieDefinition` |
| Cookie policy | `cookiePolicy` | Document type: `heading`, `introduction`, `cookies`, `outro` |
| Cookie policy | `cookiePolicy` | Template, compiled into the package assembly |

A cookie policy page is then seeded with three necessary declarations that are generic to every Umbraco site: the consent cookie itself (named from `CookieName`), the antiforgery cookie, and `UMB_MEMBER`. Add the rest of your site's cookies as blocks on that page.

It also seeds **32 dictionary items** under a `Cookie.Banner` parent, all prefixed `Cookies.`:

`Cookies.Banner.Heading`, `.Body`, `.AcceptAll`, `.RejectAll`, `.Customise`, `.Save`, `.Cancel`, `.Error`, `.RateLimited`; `Cookies.Category.{Necessary,Preferences,Statistics,Marketing}.{Name,Description}`, `Cookies.Category.Cookies`; `Cookies.Embed.Blocked.Body`, `.Button`; `Cookies.Policy.CurrentChoice`, `.NoChoice`, `.Reopen`, `.Withdraw`, `.On`, `.Off`; `Cookies.Footer.Link`; `Cookies.Table.{Name,Provider,Purpose,Duration,Type}`.

The seeder is **culture-agnostic**: it seeds for whatever languages your site already has, for any culture the package ships text for. It never creates, requires or deletes a language, and never aborts. Text resolution is dictionary item → embedded resx for the request culture → English, so every string is editable in the backoffice and none of them is missing before you get there.

The package does **not** touch document types it does not own. The policy page is located by document type — the first published `cookiePolicy` node, or `PolicyPageKey` if you set it — so there is no content picker to wire up on your own settings node.

## Theming

`consent.css` is self-sufficient: it declares its own `--consent-*` tokens on `:root` with neutral defaults and ships its own `.consent-btn` / `.consent-btn--primary` / `.consent-btn--secondary` / `.consent-btn--link` classes. It depends on no class from your design system, and it deliberately styles nothing outside the dialog, the embed placeholder and the policy tables — no global `footer`, `a` or `button` rules.

Re-theme by overriding the tokens after `<consent-head />`:

| Token | Default | Used for |
|---|---|---|
| `--consent-bg` | `#ffffff` | dialog and table surface |
| `--consent-text` | `#1a1a1a` | body copy |
| `--consent-muted` | `#5a5f66` | category descriptions and table meta |
| `--consent-primary` | `#1f2937` | headings, `.consent-btn--link`, `.consent-btn--secondary` fill |
| `--consent-accent` | `#0b57d0` | `.consent-btn--primary` fill |
| `--consent-border` | `#d5d7db` | dialog border and table rules |
| `--consent-backdrop` | `rgba(0, 0, 0, 0.6)` | the `::backdrop` scrim |

```css
:root {
    --consent-primary: #001f54;
    --consent-accent: #c8102e;
    --consent-backdrop: rgba(0, 31, 84, 0.6);
}
```

Fonts need no work — the stylesheet uses `font-family: inherit` throughout.

## JavaScript API

```js
window.cookieConsent.open();               // open the dialog (e.g. from a footer link)
window.cookieConsent.close();              // close it, if a decision already exists
window.cookieConsent.get();                // the decoded cookie, or null when undecided
window.cookieConsent.has('statistics');    // boolean, by wire name
window.cookieConsent.onChange(function (detail) { /* { categories, version } */ });
```

The same payload is dispatched on `document` as a DOM event, for code that would rather not depend on load order:

```js
document.addEventListener('cookieconsent:change', function (event) {
    console.log(event.detail.categories, event.detail.version);
});
```

Declarative hooks, no JavaScript required: `data-consent-open` on any element opens the dialog, `data-consent-action="withdrawn"` withdraws consent and reloads.

Category names in the JS API are the lowercase **wire** names (`necessary`, `preferences`, `statistics`, `marketing`) — the same strings that appear in the cookie. They are a stable contract: renaming a C# enum member must never invalidate cookies already in the wild.

## Compatibility

One `net10.0` assembly on the `Umbraco.Cms.Core` 17.0.0 floor with no upper bound. Umbraco 17 and 18 both ship only `lib/net10.0`, so there is no TFM to discriminate on and multi-targeting is not possible; the single-assembly build is verified on both majors instead.

| Umbraco | Status |
|---------|--------|
| 17.x    | Verified |
| 18.x    | Verified |

Nothing removed in Umbraco 18 is used: no `MigrationBase`/`PackageMigrationBase`, no `ILocalizationService` or `IFileService`, no `UmbracoApiController` or convention-based front-end API routing, no `IPublishedContent.Parent`/`.Children` properties. `GetById(Guid)` is never called on `IContentService`: 17.0.0 re-declares it there with `new` and 18 does not, so a 17.0.0-compiled call binds to a declaration site that vanishes at runtime on 18 (`MissingMethodException`, reproduced with a cross-version binary test). Existence checks use `IEntityService.Exists(Guid, UmbracoObjectTypes)`, which is identical in both.

Each future major needs a re-verification pass rather than a presumption of forward compatibility — Umbraco's announced service-layer refactoring spans majors 18–21.

## License

MIT.
```

- [ ] **Step 6: Run the test to verify it passes**

Run: `dotnet test Esatto.Umbraco.Backoffice.CookieBanner.Tests/Esatto.Umbraco.Backoffice.CookieBanner.Tests.csproj --filter FullyQualifiedName~PackagingMetadataTests`

Expected: PASS — `Passed! - Failed: 0, Passed: 5`

- [ ] **Step 7: Build Release**

Run:

```bash
cd "c:/src/Esatto.Packages" && dotnet build Esatto.Umbraco.Backoffice.CookieBanner/Esatto.Umbraco.Backoffice.CookieBanner.csproj -c Release
```

Expected: `Build succeeded.` with `0 Warning(s)` and `0 Error(s)`. Any `SYSLIB`/`CS0618` obsolete warning here is real — the package compiles against the 17.0.0 floor, which surfaces breaks that an 18.x-only consumer build hides. Fix it rather than packing over it.

- [ ] **Step 8: Run the whole test project**

Run:

```bash
cd "c:/src/Esatto.Packages" && dotnet test Esatto.Umbraco.Backoffice.CookieBanner.Tests/Esatto.Umbraco.Backoffice.CookieBanner.Tests.csproj -c Release
```

Expected: PASS — `Failed: 0`, with every test class from Tasks 2–17 plus `PackagingMetadataTests` present in the run. Paste the real counts into the task report; there is no CI, this run is the gate.

- [ ] **Step 9: Pack to the local feed**

`AutoPushToFeed` defaults to `false`, but pass it explicitly so the pack provably cannot publish. MinVer derives the version from git tags prefixed `Esatto.Umbraco.Backoffice.CookieBanner-`; with no such tag yet the output is a `1.0.0-preview.0.N` prerelease, which is exactly what a smoke-test build should be.

```bash
cd "c:/src/Esatto.Packages" && dotnet pack Esatto.Umbraco.Backoffice.CookieBanner/Esatto.Umbraco.Backoffice.CookieBanner.csproj \
  -c Release -o .local-feed -p:AutoPushToFeed=false
```

Expected: `Successfully created package 'c:\src\Esatto.Packages\.local-feed\Esatto.Umbraco.Backoffice.CookieBanner.<version>.nupkg'.` and **no** `Auto-pushing ...` line.

- [ ] **Step 10: Assert the nupkg contents**

```bash
cd "c:/src/Esatto.Packages" && NUPKG=$(ls -t .local-feed/Esatto.Umbraco.Backoffice.CookieBanner.*.nupkg | head -1) \
  && echo "$NUPKG" && unzip -l "$NUPKG"
```

Expected entries — compare against the shipped `Esatto.Umbraco.Backoffice.Redirects.1.0.0.nupkg`, which has the identical shape:

```
lib/net10.0/Esatto.Umbraco.Backoffice.CookieBanner.dll
staticwebassets/esatto-cookiebanner/consent.js
staticwebassets/esatto-cookiebanner/consent.css
build/Microsoft.AspNetCore.StaticWebAssetEndpoints.props
build/Microsoft.AspNetCore.StaticWebAssets.props
build/Esatto.Umbraco.Backoffice.CookieBanner.props
buildMultiTargeting/Esatto.Umbraco.Backoffice.CookieBanner.props
buildTransitive/Esatto.Umbraco.Backoffice.CookieBanner.props
README.md
icon.png
docs/consent-dialog.png
docs/consent-customise.png
docs/cookie-policy-page.png
docs/cookie-registry-editor.png
```

Exactly **one** assembly: `AddRazorSupportForMvc` compiles the Razor views and the view component into `Esatto.Umbraco.Backoffice.CookieBanner.dll`. A second `*.Views.dll` entry, or any loose `.cshtml`, means view compilation is misconfigured — verified against `Esatto.Umbraco.Backoffice.SharedPreviewLink.1.0.5.nupkg`, which ships a Razor view and still has a single `lib/net10.0` dll.

Then assert the two absences — 1.0.0 has no backoffice extension, so shipping either file would register an empty manifest with the backoffice:

```bash
cd "c:/src/Esatto.Packages" && NUPKG=$(ls -t .local-feed/Esatto.Umbraco.Backoffice.CookieBanner.*.nupkg | head -1) \
  && if unzip -l "$NUPKG" | grep -E 'App_Plugins|umbraco-package\.json'; then echo "FAIL: backoffice artefacts present"; else echo "OK: no App_Plugins, no umbraco-package.json"; fi
```

Expected: `OK: no App_Plugins, no umbraco-package.json`

- [ ] **Step 11: Inspect the .nuspec**

```bash
cd "c:/src/Esatto.Packages" && NUPKG=$(ls -t .local-feed/Esatto.Umbraco.Backoffice.CookieBanner.*.nupkg | head -1) \
  && unzip -p "$NUPKG" Esatto.Umbraco.Backoffice.CookieBanner.nuspec
```

Expected — four things to read off it by eye:

1. `<dependency id="Umbraco.Cms.Core" version="17.0.0" exclude="Build,Analyzers" />` is present in the `net10.0` group. This is what the Umbraco Marketplace reads to decide which majors the package supports; without it the listing shows no version.
2. `Umbraco.Cms.Api.Management` is **absent** — 1.0.0 has no dashboard, so it must not be a dependency (it is present in the Redirects nuspec precisely because that package does have one).
3. `<tags>umbraco umbraco-marketplace …</tags>` — space-separated by nuspec convention even though the csproj declares them semicolon-separated. `umbraco-marketplace` gates the listing entirely.
4. `<projectUrl>https://github.com/carl-schele-esatto/Esatto.Packages/tree/main/Esatto.Umbraco.Backoffice.CookieBanner</projectUrl>` and `<repository type="git" url="https://github.com/carl-schele-esatto/Esatto.Packages" commit="…" />` — the repo-wide source-code-link invariant: `RepositoryUrl` is the repo root, `PackageProjectUrl` the package's subfolder.

Also confirm `<readme>README.md</readme>`, `<icon>icon.png</icon>` and `<license type="expression">MIT</license>`.

- [ ] **Step 12: STOP — hand the diff and the verification output to Carl**

Do **not** commit, tag or push. Present:

- `git status --short` and `git diff --stat` for the whole package
- the `dotnet build -c Release` summary line
- the full `dotnet test` summary line with real counts
- the `dotnet pack` output line and the complete `unzip -l` listing
- the `.nuspec` body, with the four points above called out

Then wait. Only on Carl's explicit approval:

```bash
cd "c:/src/Esatto.Packages"
git add Esatto.Umbraco.Backoffice.CookieBanner Esatto.Umbraco.Backoffice.CookieBanner.Tests
git commit -m "Add Esatto.Umbraco.Backoffice.CookieBanner 1.0.0"
git tag Esatto.Umbraco.Backoffice.CookieBanner-1.0.0
```

The tag prefix is not cosmetic: `Directory.Build.targets` sets `MinVerTagPrefix` to `$(PackageId)-`, so only a tag named exactly `Esatto.Umbraco.Backoffice.CookieBanner-1.0.0` produces a `1.0.0` release build. After the tag, re-run Step 9 to pack the real `1.0.0` — and **Carl runs the `dotnet nuget push` himself**. Never run it.

