# Esatto.Umbraco.Backoffice.BlockPreview 1.0.0 Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** Renew `Backoffice.BlockPreview` as `Esatto.Umbraco.Backoffice.BlockPreview` 1.0.0 and add a generic Block List custom view. The view shows each block's real frontend rendering (server-rendered Razor partial inside the site's preview layout) in a sandboxed, auto-sized iframe.

**Architecture:**
- **Server:** an authenticated Management API endpoint builds a one-item Block List value from the block's raw (unsaved) data and converts it with the document property's own value converter. It then renders `Views/Partials/blocklist/Components/<alias>.cshtml` inside a package `Frame.cshtml` whose layout is the site's `Views/BlockPreview.cshtml`, or a built-in fallback. Finally it injects a small height/link script.
- **Client:** a Lit element posts the block data, shows the HTML in `<iframe srcdoc sandbox="allow-scripts">`, and sizes it from validated `postMessage`s.
- **Manifest:** a C# manifest reader emits the custom view only when enabled. The legacy video/code elements stay as static files.

**Tech Stack:** .NET 10 Razor SDK (RCL with compiled views), Umbraco.Cms.Core / Infrastructure / Web.Common / Api.Management 17.0.0 (min-only), xUnit + NSubstitute; TypeScript 5.9, Vite 7, vitest 2, `@umbraco-cms/backoffice` ^17.4.2.

**Spec:** `docs/superpowers/specs/2026-10-15-blockpreview-renew-design.md`

## Global Constraints

- PackageId / folder / RootNamespace / AssemblyName: `Esatto.Umbraco.Backoffice.BlockPreview`. Tests: `Esatto.Umbraco.Backoffice.BlockPreview.Tests`.
- App_Plugins path: `/App_Plugins/Esatto.Umbraco.Backoffice.BlockPreview/`. New element file `block-preview-frame.js`, element name `esatto-block-preview-frame`.
- Keep the legacy element names `backoffice-blockpreview-video` and `backoffice-blockpreview-code`, and their `meta` contracts. Only their path changes.
- Config section `Esatto:BlockPreview`: `BlockList.Enabled` (default true), `BlockList.IgnoredContentTypes` (default empty), `Layout` (default `~/Views/BlockPreview.cshtml`), `Stylesheets`, `Scripts` (default empty).
- Endpoint `POST /umbraco/management/api/v1/esatto/block-preview/render`, policy `AuthorizationPolicies.BackOfficeAccess`. Handled outcomes always return 200 with `status` = `rendered` | `notPreviewable` | `error`.
- Custom view manifest: `type: blockEditorCustomView`, `forBlockEditor: "block-list"`, no `forContentTypeAlias`, `weight: -1000`.
- Iframe: `srcdoc`, `sandbox="allow-scripts"` (never `allow-same-origin`). Height messages: `{ type: "esatto-block-preview:height", id: <nonce>, height }`.
- Umbraco references are min-only `17.0.0`, net10.0. Source-code link (`RepositoryUrl`, `PackageProjectUrl` = `…/tree/main/Esatto.Umbraco.Backoffice.BlockPreview`, `RepositoryType` git), tags include `umbraco-marketplace;umbraco-18`, `AutoPushToFeed` default false.
- C# under `namespace Esatto.Umbraco...`: put `using Umbraco...;` at the top of the file and use short type names. Never write inline `Umbraco.Cms...`. Test files need `using Xunit;`.
- Git: branch `feature/blockpreview-renew` (exists; the spec commit is on it). Local commits per task are approved with this plan. No push, tag, merge or nuget push without Carl's explicit OK.
- **Risk 1 ruling (spec allowed either):** if the owner document isn't in the content cache (never saved), return `notPreviewable` with `reason: "unsavedDocument"`. The client shows "Save the page once to see a preview." No stub owner in 1.0.

## Review Focus

1. **A culture-variant document, previewed in its non-default culture.** The block must convert (the `expose` entry is for that culture) and render with that culture's `CultureInfo`. Pinned in Task 3 (`Expose_contains_culture_and_invariant_entries`) and Task 4 (`Sets_variation_context_and_culture_while_rendering`).
2. **A block with settings vs without.** Without settings: no `settingsKey`, empty `settingsData`. With settings: both are present and linked. Pinned in Task 3.
3. **Rendered HTML without `</body>`** (a site layout that omits it). The frame script is still injected, exactly once. Pinned in Task 3 (`Appends_script_when_body_tag_missing`).
4. **A height message from another iframe, or with a stale nonce.** Ignored, with no resize loop. Pinned in Task 5 (`frame-messages` tests).
5. **Rapid typing in a block field.** At most one request per 400 ms pause, and none when the fingerprint is unchanged. Pinned in Task 5 (`requestFingerprint` ignores the nonce; equal data gives an equal fingerprint).

---

## File map

```
Esatto.Umbraco.Backoffice.BlockPreview/                     (git mv from Backoffice.BlockPreview)
  Esatto.Umbraco.Backoffice.BlockPreview.csproj             Task 1
  README.md, icon.png, docs/images/                         Tasks 1, 8
  Client/package.json, tsconfig.json, vite.config.ts        Task 1
  Client/public/umbraco-package.json                        Task 1  identity, no extensions
  Client/public/{code,video}-preview.element.js, lib/       Task 1  legacy, moved + path fix
  Client/src/render-request.ts                              Task 5  pure
  Client/src/frame-messages.ts                              Task 5  pure
  Client/src/api.ts                                         Task 6
  Client/src/block-preview-frame.element.ts                 Task 6
  Client/test/*.test.ts                                     Task 5
  src/BlockPreviewOptions.cs                                Task 2
  src/Manifest/BlockCustomViewExtension.cs                  Task 2
  src/Manifest/BlockPreviewManifestReader.cs                Task 2
  src/BlockPreviewServiceCollectionExtensions.cs            Tasks 2, 4
  src/BlockPreviewComposer.cs                               Task 2
  src/Rendering/RenderRequest.cs                            Task 3  DTOs
  src/Rendering/RenderOutcome.cs                            Task 3
  src/Rendering/BlockListValueJson.cs                       Task 3  pure
  src/Rendering/FrameScript.cs                              Task 3  pure
  src/Rendering/FrameModel.cs                               Task 4
  src/Rendering/IBlockItemFactory.cs, BlockItemFactory.cs   Task 4
  src/Rendering/IViewRenderer.cs, RazorViewRenderer.cs      Task 4
  src/Rendering/BlockPreviewRenderer.cs                     Task 4
  src/BlockPreviewController.cs                             Task 4
  Views/EsattoBlockPreview/Frame.cshtml                     Task 4
  Views/EsattoBlockPreview/_DefaultLayout.cshtml            Task 4
Esatto.Umbraco.Backoffice.BlockPreview.Tests/               Tasks 2–4
```

`PKG` = `Esatto.Umbraco.Backoffice.BlockPreview`, `CL` = `PKG/Client`, `TST` = `Esatto.Umbraco.Backoffice.BlockPreview.Tests`.

---

### Task 1: Renew rename, client build scaffold, legacy elements moved

**Files:** `git mv Backoffice.BlockPreview PKG`; rewrite the csproj; create the `CL` scaffold; move the legacy JS into `CL/public`.

**Interfaces:** Produces the build pipeline: `npm run build` writes `PKG/wwwroot/App_Plugins/PKG/` including the legacy files and `umbraco-package.json`.

- [ ] **Step 1: Rename**

```bash
cd /c/src/Esatto.Packages
git mv Backoffice.BlockPreview Esatto.Umbraco.Backoffice.BlockPreview
cd Esatto.Umbraco.Backoffice.BlockPreview
git mv Backoffice.BlockPreview.csproj Esatto.Umbraco.Backoffice.BlockPreview.csproj
mkdir -p Client/public Client/src Client/test
git mv wwwroot/App_Plugins/Backoffice.BlockPreview/code-preview.element.js Client/public/code-preview.element.js
git mv wwwroot/App_Plugins/Backoffice.BlockPreview/video-preview.element.js Client/public/video-preview.element.js
git mv wwwroot/App_Plugins/Backoffice.BlockPreview/lib Client/public/lib
git rm -q wwwroot/App_Plugins/Backoffice.BlockPreview/umbraco-package.json
sed -i 's#/App_Plugins/Backoffice.BlockPreview/#/App_Plugins/Esatto.Umbraco.Backoffice.BlockPreview/#g' Client/public/code-preview.element.js Client/public/video-preview.element.js README.md
grep -rn "App_Plugins/Backoffice.BlockPreview" . --include=*.js --include=*.md
```
Expected: the last grep prints nothing.

- [ ] **Step 2: csproj**: replace `PKG/Esatto.Umbraco.Backoffice.BlockPreview.csproj` with:

```xml
<Project Sdk="Microsoft.NET.Sdk.Razor">

  <PropertyGroup>
    <TargetFramework>net10.0</TargetFramework>
    <Nullable>enable</Nullable>
    <ImplicitUsings>enable</ImplicitUsings>
    <IsPackable>true</IsPackable>
    <GeneratePackageOnBuild>false</GeneratePackageOnBuild>

    <!-- Serve wwwroot/App_Plugins/... at literal /App_Plugins/... in the consumer.
         Default Razor SDK behaviour would prefix everything with /_content/<PackageId>/,
         which Umbraco's package discovery does not look at. -->
    <StaticWebAssetBasePath>/</StaticWebAssetBasePath>

    <!-- Views/EsattoBlockPreview/*.cshtml are compiled into this assembly and found by
         the consumer's view engine at /Views/EsattoBlockPreview/... -->
    <AddRazorSupportForMvc>true</AddRazorSupportForMvc>

    <RootNamespace>Esatto.Umbraco.Backoffice.BlockPreview</RootNamespace>
    <AssemblyName>Esatto.Umbraco.Backoffice.BlockPreview</AssemblyName>

    <NoWarn>$(NoWarn);CS1591</NoWarn>
  </PropertyGroup>

  <PropertyGroup Label="NuGet">
    <PackageId>Esatto.Umbraco.Backoffice.BlockPreview</PackageId>
    <Authors>Carl Schéle</Authors>
    <Description>Live frontend previews for Umbraco 17/18 Block List blocks in the backoffice: each block shows its real Razor partial, rendered with the site's own CSS and JavaScript in a sandboxed, auto-sized iframe that updates as the editor types. Also ships configurable video and code preview elements.</Description>
    <PackageTags>umbraco;umbraco-marketplace;umbraco-18;backoffice;block-editor;block-list;preview;custom-view</PackageTags>
    <PackageReadmeFile>README.md</PackageReadmeFile>
    <PackageIcon>icon.png</PackageIcon>
    <PackageLicenseExpression>MIT</PackageLicenseExpression>
    <PackageProjectUrl>https://github.com/carl-schele-esatto/Esatto.Packages/tree/main/Esatto.Umbraco.Backoffice.BlockPreview</PackageProjectUrl>
    <RepositoryUrl>https://github.com/carl-schele-esatto/Esatto.Packages</RepositoryUrl>
    <RepositoryType>git</RepositoryType>
  </PropertyGroup>

  <ItemGroup>
    <None Include="README.md" Pack="true" PackagePath="\" />
    <None Include="icon.png" Pack="true" PackagePath="\" />
  </ItemGroup>

  <ItemGroup>
    <FrameworkReference Include="Microsoft.AspNetCore.App" />
    <!-- Core: options, PackageManifest, block models. Infrastructure: IPackageManifestReader.
         Web.Common: authorization policies, view support. Api.Management: the controller base
         and versioned route. Min-only 17.0.0; resolves to whatever the consumer has. -->
    <PackageReference Include="Umbraco.Cms.Core" Version="17.0.0" />
    <PackageReference Include="Umbraco.Cms.Infrastructure" Version="17.0.0" />
    <PackageReference Include="Umbraco.Cms.Web.Common" Version="17.0.0" />
    <PackageReference Include="Umbraco.Cms.Api.Management" Version="17.0.0" />
  </ItemGroup>

  <ItemGroup Label="Security overrides">
    <!-- Patched floors for advisories that Umbraco 17's transitive graph pulls in. Drop each once
         Umbraco bumps its own dependency past it. Same pins as DictionaryFilterValues.
         - MessagePack 3.1.4 (via HybridCache): LZ4 decompression OOB read (GHSA-hv8m-jj95-wg3x), fixed 3.1.7.
         - Microsoft.OpenApi 2.3.0 (via Api.Management -> Swashbuckle): circular-ref stack overflow (GHSA-v5pm-xwqc-g5wc), fixed 2.7.5.
         - MailKit 4.14.0: STARTTLS response injection (GHSA-9j88-vvj5-vhgr), fixed 4.16.0.
         - MimeKit 4.14.0: CRLF injection (GHSA-g7hc-96xr-gvvx), fixed 4.15.1; 4.16.0 = MailKit's floor.
         - System.Security.Cryptography.Pkcs: DoS, CVE-2026-84008 (GHSA-846f-f764-76mx); 10.0.13. -->
    <PackageReference Include="MessagePack" Version="3.1.7" />
    <PackageReference Include="Microsoft.OpenApi" Version="2.7.5" />
    <PackageReference Include="MailKit" Version="4.16.0" />
    <PackageReference Include="MimeKit" Version="4.16.0" />
    <PackageReference Include="System.Security.Cryptography.Pkcs" Version="10.0.13" />
  </ItemGroup>

  <ItemGroup>
    <!-- Don't include the client source as part of the nuget/static-web-assets build -->
    <Content Remove="Client\**" />
    <None Include="Client\public\umbraco-package.json" Pack="false" />
  </ItemGroup>

  <!-- Build the backoffice client (npm run build) before building this project.
       Skipped during IDE design-time builds to avoid running npm on every keystroke. -->
  <Target Name="BuildBackofficeClient" BeforeTargets="Build" Condition="'$(DesignTimeBuild)' != 'true' And '$(BuildingProject)' == 'true'">
    <Message Importance="high" Text="Installing backoffice client dependencies (npm install)..." Condition="!Exists('$(MSBuildProjectDirectory)\Client\node_modules')" />
    <Exec Command="npm install" WorkingDirectory="$(MSBuildProjectDirectory)\Client" Condition="!Exists('$(MSBuildProjectDirectory)\Client\node_modules')" />
    <Message Importance="high" Text="Building backoffice client (npm run build)..." />
    <Exec Command="npm run build" WorkingDirectory="$(MSBuildProjectDirectory)\Client" />
  </Target>

  <PropertyGroup Label="AutoPush">
    <!-- Off by default so a local `dotnet pack` never publishes. -->
    <AutoPushToFeed Condition="'$(AutoPushToFeed)' == ''">false</AutoPushToFeed>
    <AutoPushFeedName Condition="'$(AutoPushFeedName)' == ''">esatto-packages</AutoPushFeedName>
  </PropertyGroup>

  <Target Name="AutoPushAfterPack" AfterTargets="Pack" Condition="'$(AutoPushToFeed)' == 'true'">
    <Message Importance="high" Text="Auto-pushing $(PackageId) $(PackageVersion) to '$(AutoPushFeedName)'..." />
    <Exec Command="dotnet nuget push &quot;$([MSBuild]::EnsureTrailingSlash('$(PackageOutputPath)'))$(PackageId).$(PackageVersion).nupkg&quot; --source $(AutoPushFeedName) --api-key az --skip-duplicate" />
  </Target>

</Project>
```

Copy the icon: `cp ../Esatto.Umbraco.Backoffice.TreeDragAndDrop/icon.png icon.png`.

- [ ] **Step 3: Client scaffold**

`CL/package.json`:
```json
{
  "name": "esatto-umbraco-backoffice-block-preview",
  "private": true,
  "version": "0.0.0",
  "type": "module",
  "scripts": {
    "watch": "tsc && vite build --watch",
    "build": "tsc && vite build",
    "test": "vitest run"
  },
  "devDependencies": {
    "@umbraco-cms/backoffice": "^17.4.2",
    "typescript": "^5.9.3",
    "vite": "^7.3.1",
    "vitest": "^2"
  }
}
```
`CL/tsconfig.json`: `cp ../Esatto.Umbraco.Backoffice.TreeDragAndDrop/Client/tsconfig.json Client/tsconfig.json`.

`CL/vite.config.ts`:
```ts
import { defineConfig } from "vite";

export default defineConfig({
  build: {
    lib: {
      entry: { "block-preview-frame": "src/block-preview-frame.element.ts" },
      formats: ["es"],
      // Must match the element path the C# manifest reader emits:
      // /App_Plugins/Esatto.Umbraco.Backoffice.BlockPreview/block-preview-frame.js
      fileName: (_format, entryName) => `${entryName}.js`,
    },
    outDir: "../wwwroot/App_Plugins/Esatto.Umbraco.Backoffice.BlockPreview",
    emptyOutDir: true, // public/ (legacy elements, lib/, umbraco-package.json) is copied in afterwards
    sourcemap: true,
    rollupOptions: { external: [/^@umbraco/] },
  },
});
```

`CL/public/umbraco-package.json`:
```json
{
  "$schema": "../umbraco-package-schema.json",
  "id": "Esatto.Umbraco.Backoffice.BlockPreview",
  "name": "Esatto Umbraco Backoffice Block Preview",
  "version": "1.0.0",
  "extensions": []
}
```

Temporary entry so the build has something to compile (replaced in Task 6), `CL/src/block-preview-frame.element.ts`:
```ts
// Generic Block List custom view: the block's real frontend rendering in an iframe.
export {};
```

- [ ] **Step 4: Build and verify**

```bash
cd Client && npm install && npm run build && cd ..
ls wwwroot/App_Plugins/Esatto.Umbraco.Backoffice.BlockPreview wwwroot/App_Plugins/Esatto.Umbraco.Backoffice.BlockPreview/lib
dotnet build -c Release 2>&1 | grep -E "Build succeeded|error|warning NU19"
```
Expected: the output folder holds `block-preview-frame.js`, `code-preview.element.js`, `video-preview.element.js`, `umbraco-package.json` and `lib/{highlight.min.js,vs2015.css}`. The build succeeds with no NU19xx warnings. If audit warnings appear for packages not pinned above, add pins in the same block and comment them.

- [ ] **Step 5: Commit**: `git add -A . ../Esatto.Umbraco.Backoffice.BlockPreview` → `git commit -m "Renew Backoffice.BlockPreview as Esatto.Umbraco.Backoffice.BlockPreview: rename, client build, legacy elements moved"`

---

### Task 2: Options, manifest reader, registration

**Files:**
- Create: `PKG/src/BlockPreviewOptions.cs`, `PKG/src/Manifest/BlockCustomViewExtension.cs`, `PKG/src/Manifest/BlockPreviewManifestReader.cs`, `PKG/src/BlockPreviewServiceCollectionExtensions.cs`, `PKG/src/BlockPreviewComposer.cs`
- Create: `TST/Esatto.Umbraco.Backoffice.BlockPreview.Tests.csproj`, `TST/BlockPreviewManifestReaderTests.cs`, `TST/BlockPreviewRegistrationTests.cs`

**Interfaces:**
- Produces:
  - `BlockPreviewOptions { const SectionName = "Esatto:BlockPreview"; BlockListOptions BlockList; string Layout; string[] Stylesheets; string[] Scripts }`
  - `BlockListOptions { bool Enabled = true; string[] IgnoredContentTypes }`
  - `IServiceCollection AddBlockPreview(this IServiceCollection)`. Task 4 adds renderer registrations inside it.
  - `BlockPreviewManifestReader(IOptions<BlockPreviewOptions>, ILogger<…>)`.

- [ ] **Step 1: Tests project + failing tests**

`TST/Esatto.Umbraco.Backoffice.BlockPreview.Tests.csproj`:
```xml
<Project Sdk="Microsoft.NET.Sdk">

  <PropertyGroup>
    <TargetFramework>net10.0</TargetFramework>
    <Nullable>enable</Nullable>
    <ImplicitUsings>enable</ImplicitUsings>
    <IsPackable>false</IsPackable>
    <IsPublishable>false</IsPublishable>
  </PropertyGroup>

  <ItemGroup>
    <FrameworkReference Include="Microsoft.AspNetCore.App" />
  </ItemGroup>

  <ItemGroup>
    <PackageReference Include="Microsoft.NET.Test.Sdk" Version="17.11.1" />
    <PackageReference Include="xunit" Version="2.9.2" />
    <PackageReference Include="xunit.runner.visualstudio" Version="2.8.2" />
    <PackageReference Include="NSubstitute" Version="5.3.0" />
  </ItemGroup>

  <ItemGroup>
    <ProjectReference Include="..\Esatto.Umbraco.Backoffice.BlockPreview\Esatto.Umbraco.Backoffice.BlockPreview.csproj" />
  </ItemGroup>

</Project>
```

`TST/BlockPreviewManifestReaderTests.cs`:
```csharp
using Esatto.Umbraco.Backoffice.BlockPreview;
using Esatto.Umbraco.Backoffice.BlockPreview.Manifest;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using Umbraco.Cms.Core.Manifest;
using Xunit;

namespace Esatto.Umbraco.Backoffice.BlockPreview.Tests;

public class BlockPreviewManifestReaderTests
{
    private static async Task<List<PackageManifest>> ReadAsync(BlockPreviewOptions options) =>
        (await new BlockPreviewManifestReader(Options.Create(options), NullLogger<BlockPreviewManifestReader>.Instance)
            .ReadPackageManifestsAsync()).ToList();

    [Fact]
    public async Task Emits_the_generic_block_list_view_when_enabled()
    {
        var manifest = Assert.Single(await ReadAsync(new BlockPreviewOptions()));
        var view = Assert.IsType<BlockCustomViewExtension>(Assert.Single(manifest.Extensions));

        Assert.Equal("blockEditorCustomView", view.Type);
        Assert.Equal("Esatto.Umbraco.Backoffice.BlockPreview.Frame", view.Alias);
        Assert.Equal("/App_Plugins/Esatto.Umbraco.Backoffice.BlockPreview/block-preview-frame.js", view.Element);
        Assert.Equal("esatto-block-preview-frame", view.ElementName);
        Assert.Equal("block-list", view.ForBlockEditor);
        Assert.Equal(-1000, view.Weight);
    }

    [Fact]
    public async Task Emits_nothing_when_block_list_previews_are_disabled()
    {
        Assert.Empty(await ReadAsync(new BlockPreviewOptions { BlockList = new BlockListOptions { Enabled = false } }));
    }

    [Fact]
    public async Task Manifest_has_no_name_id_or_telemetry_so_installed_lists_the_package_once()
    {
        var manifest = Assert.Single(await ReadAsync(new BlockPreviewOptions()));
        Assert.Equal(string.Empty, manifest.Name);
        Assert.Null(manifest.Id);
        Assert.False(manifest.AllowTelemetry);
        Assert.False(manifest.AllowPublicAccess);
    }
}
```

`TST/BlockPreviewRegistrationTests.cs`:
```csharp
using Esatto.Umbraco.Backoffice.BlockPreview;
using Esatto.Umbraco.Backoffice.BlockPreview.Manifest;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;
using Umbraco.Cms.Core.Manifest;
using Umbraco.Cms.Infrastructure.Manifest;
using Xunit;

namespace Esatto.Umbraco.Backoffice.BlockPreview.Tests;

public class BlockPreviewRegistrationTests
{
    private sealed class FirstUmbracoReader : IPackageManifestReader
    {
        public Task<IEnumerable<PackageManifest>> ReadPackageManifestsAsync() => Task.FromResult(Enumerable.Empty<PackageManifest>());
    }

    private sealed class LastUmbracoReader : IPackageManifestReader
    {
        public Task<IEnumerable<PackageManifest>> ReadPackageManifestsAsync() => Task.FromResult(Enumerable.Empty<PackageManifest>());
    }

    private static ServiceProvider Build(Dictionary<string, string?>? config = null, int registrations = 1)
    {
        var services = new ServiceCollection();
        services.AddLogging();
        services.AddSingleton<IConfiguration>(new ConfigurationBuilder().AddInMemoryCollection(config ?? []).Build());
        services.AddSingleton<IPackageManifestReader, FirstUmbracoReader>();
        services.AddSingleton<IPackageManifestReader, LastUmbracoReader>();
        for (var i = 0; i < registrations; i++) services.AddBlockPreview();
        return services.BuildServiceProvider();
    }

    // PackagingService resolves ONE IPackageManifestReader (the last registered); ours must not become it.
    [Fact]
    public void Single_resolution_stays_umbracos_last_reader()
    {
        using var provider = Build();
        Assert.IsType<LastUmbracoReader>(provider.GetRequiredService<IPackageManifestReader>());
    }

    [Fact]
    public void Registering_twice_adds_the_reader_once()
    {
        using var provider = Build(registrations: 2);
        Assert.Single(provider.GetServices<IPackageManifestReader>().OfType<BlockPreviewManifestReader>());
    }

    [Fact]
    public void Binds_the_section()
    {
        using var provider = Build(new()
        {
            ["Esatto:BlockPreview:BlockList:Enabled"] = "false",
            ["Esatto:BlockPreview:BlockList:IgnoredContentTypes:0"] = "heroBlock",
            ["Esatto:BlockPreview:Layout"] = "~/Views/Preview.cshtml",
            ["Esatto:BlockPreview:Stylesheets:0"] = "/css/site.css",
        });
        var options = provider.GetRequiredService<IOptions<BlockPreviewOptions>>().Value;
        Assert.False(options.BlockList.Enabled);
        Assert.Equal(["heroBlock"], options.BlockList.IgnoredContentTypes);
        Assert.Equal("~/Views/Preview.cshtml", options.Layout);
        Assert.Equal(["/css/site.css"], options.Stylesheets);
    }

    [Fact]
    public void Defaults_without_a_section()
    {
        using var provider = Build();
        var options = provider.GetRequiredService<IOptions<BlockPreviewOptions>>().Value;
        Assert.True(options.BlockList.Enabled);
        Assert.Empty(options.BlockList.IgnoredContentTypes);
        Assert.Equal("~/Views/BlockPreview.cshtml", options.Layout);
    }

    [Fact]
    public async Task Invalid_config_value_falls_back_to_defaults()
    {
        using var provider = Build(new() { ["Esatto:BlockPreview:BlockList:Enabled"] = "nope" });
        var reader = provider.GetServices<IPackageManifestReader>().OfType<BlockPreviewManifestReader>().Single();
        var manifest = Assert.Single(await reader.ReadPackageManifestsAsync());
        Assert.Single(manifest.Extensions);
    }
}
```

Run: `dotnet test Esatto.Umbraco.Backoffice.BlockPreview.Tests`
Expected: build FAILS with CS0246 for `BlockPreviewOptions`, `BlockPreviewManifestReader`, `BlockCustomViewExtension`, `BlockListOptions`, and CS1061 for `AddBlockPreview`.

- [ ] **Step 2: Implement**

`PKG/src/BlockPreviewOptions.cs`:
```csharp
namespace Esatto.Umbraco.Backoffice.BlockPreview;

/// <summary>Bound from <c>Esatto:BlockPreview</c>. Everything is optional.</summary>
public sealed class BlockPreviewOptions
{
    public const string SectionName = "Esatto:BlockPreview";

    public BlockListOptions BlockList { get; set; } = new();

    /// <summary>
    /// The site's preview layout: plain Razor with the site's &lt;head&gt; and scripts and
    /// <c>@RenderBody()</c>. It must not declare a model. Missing → the built-in layout with
    /// <see cref="Stylesheets"/> and <see cref="Scripts"/>.
    /// </summary>
    public string Layout { get; set; } = "~/Views/BlockPreview.cshtml";

    /// <summary>Stylesheet URLs for the built-in layout (ignored when the site layout exists).</summary>
    public string[] Stylesheets { get; set; } = [];

    /// <summary>Script URLs for the built-in layout (ignored when the site layout exists).</summary>
    public string[] Scripts { get; set; } = [];
}

public sealed class BlockListOptions
{
    /// <summary>When false, the generic Block List preview isn't registered at all.</summary>
    public bool Enabled { get; set; } = true;

    /// <summary>Block content type aliases that keep Umbraco's plain row.</summary>
    public string[] IgnoredContentTypes { get; set; } = [];
}
```

`PKG/src/Manifest/BlockCustomViewExtension.cs`:
```csharp
using System.Text.Json.Serialization;

namespace Esatto.Umbraco.Backoffice.BlockPreview.Manifest;

/// <summary>A <c>blockEditorCustomView</c> extension as it appears in a package manifest.</summary>
public sealed record BlockCustomViewExtension(
    [property: JsonPropertyName("type")] string Type,
    [property: JsonPropertyName("alias")] string Alias,
    [property: JsonPropertyName("name")] string Name,
    [property: JsonPropertyName("element")] string Element,
    [property: JsonPropertyName("elementName")] string ElementName,
    [property: JsonPropertyName("forBlockEditor")] string ForBlockEditor,
    [property: JsonPropertyName("weight")] int Weight);
```

`PKG/src/Manifest/BlockPreviewManifestReader.cs`:
```csharp
using System.Reflection;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Umbraco.Cms.Core.Manifest;
using Umbraco.Cms.Infrastructure.Manifest;

namespace Esatto.Umbraco.Backoffice.BlockPreview.Manifest;

/// <summary>
/// Contributes the generic Block List custom view, but only when
/// <c>Esatto:BlockPreview:BlockList:Enabled</c> is true, so a disabled preview loads no script.
/// The static <c>umbraco-package.json</c> (no extensions) carries the package identity;
/// this manifest has no name, Id or telemetry so Packages → Installed lists it once.
/// </summary>
public sealed class BlockPreviewManifestReader : IPackageManifestReader
{
    public const string PackageId = "Esatto.Umbraco.Backoffice.BlockPreview";

    private static readonly string PackageVersion =
        typeof(BlockPreviewManifestReader).Assembly
            .GetCustomAttribute<AssemblyInformationalVersionAttribute>()?.InformationalVersion
            .Split('+')[0]
        ?? "0.0.0";

    // The block list renders only the highest-weight matching custom view, so any view a site
    // registers for a specific block type (default weight 0) wins over this generic one.
    private const int GenericViewWeight = -1000;

    private readonly IOptions<BlockPreviewOptions> _options;
    private readonly ILogger<BlockPreviewManifestReader> _logger;

    public BlockPreviewManifestReader(IOptions<BlockPreviewOptions> options, ILogger<BlockPreviewManifestReader> logger)
    {
        _options = options;
        _logger = logger;
    }

    public Task<IEnumerable<PackageManifest>> ReadPackageManifestsAsync()
    {
        if (!ReadOptions().BlockList.Enabled)
        {
            return Task.FromResult(Enumerable.Empty<PackageManifest>());
        }

        PackageManifest[] manifests =
        [
            new PackageManifest
            {
                // Empty on purpose: Packages → Installed lists each manifest that has a name.
                Name = string.Empty,
                Version = PackageVersion,
                AllowTelemetry = false,
                Extensions =
                [
                    new BlockCustomViewExtension(
                        Type: "blockEditorCustomView",
                        Alias: $"{PackageId}.Frame",
                        Name: "Block Preview (frontend rendering)",
                        Element: $"/App_Plugins/{PackageId}/block-preview-frame.js",
                        ElementName: "esatto-block-preview-frame",
                        ForBlockEditor: "block-list",
                        Weight: GenericViewWeight),
                ],
            },
        ];
        return Task.FromResult<IEnumerable<PackageManifest>>(manifests);
    }

    // The manifest endpoint feeds the whole backoffice; a bad appsettings value must not take it down.
    private BlockPreviewOptions ReadOptions()
    {
        try
        {
            return _options.Value;
        }
        catch (InvalidOperationException ex)
        {
            _logger.LogWarning(ex, "Invalid '{Section}' configuration; using defaults.", BlockPreviewOptions.SectionName);
            return new BlockPreviewOptions();
        }
    }
}
```

`PKG/src/BlockPreviewServiceCollectionExtensions.cs`:
```csharp
using Esatto.Umbraco.Backoffice.BlockPreview.Manifest;
using Microsoft.Extensions.DependencyInjection;
using Umbraco.Cms.Infrastructure.Manifest;

namespace Esatto.Umbraco.Backoffice.BlockPreview;

public static class BlockPreviewServiceCollectionExtensions
{
    /// <summary>Binds <see cref="BlockPreviewOptions"/> and registers the manifest reader and renderer. Idempotent.</summary>
    /// <remarks>
    /// Umbraco registers its manifest readers with plain AddSingleton; PackagingService resolves the
    /// LAST one for its installed-package queries. Appending ours would make it that one, so it is
    /// inserted in front of the existing readers instead (same approach as TreeDragAndDrop).
    /// </remarks>
    public static IServiceCollection AddBlockPreview(this IServiceCollection services)
    {
        if (services.Any(d => d.ServiceType == typeof(IPackageManifestReader)
                              && d.ImplementationType == typeof(BlockPreviewManifestReader)))
        {
            return services;
        }

        services.AddOptions<BlockPreviewOptions>().BindConfiguration(BlockPreviewOptions.SectionName);

        var descriptor = ServiceDescriptor.Singleton<IPackageManifestReader, BlockPreviewManifestReader>();
        int firstReader = services.ToList().FindIndex(d => d.ServiceType == typeof(IPackageManifestReader));
        if (firstReader < 0)
        {
            services.Add(descriptor);
        }
        else
        {
            services.Insert(firstReader, descriptor);
        }

        return services;
    }
}
```

`PKG/src/BlockPreviewComposer.cs`:
```csharp
using Umbraco.Cms.Core.Composing;
using Umbraco.Cms.Core.DependencyInjection;

namespace Esatto.Umbraco.Backoffice.BlockPreview;

/// <summary>Runs after Umbraco's core registrations, so the manifest readers already exist to insert in front of.</summary>
public sealed class BlockPreviewComposer : IComposer
{
    public void Compose(IUmbracoBuilder builder) => builder.Services.AddBlockPreview();
}
```

- [ ] **Step 3: Tests pass**: `dotnet test Esatto.Umbraco.Backoffice.BlockPreview.Tests` → Expected: PASS (3 + 5 = 8).

- [ ] **Step 4: Commit**: `git commit -m "BlockPreview: options, manifest reader gated by BlockList.Enabled"`

---

### Task 3: Request DTOs, outcome, block value JSON, frame script (pure)

**Files:**
- Create: `PKG/src/Rendering/RenderRequest.cs`, `PKG/src/Rendering/RenderOutcome.cs`, `PKG/src/Rendering/BlockListValueJson.cs`, `PKG/src/Rendering/FrameScript.cs`
- Test: `TST/BlockListValueJsonTests.cs`, `TST/FrameScriptTests.cs`

**Interfaces:**
- Produces:
  - `RenderRequest { Guid DocumentKey; Guid DocumentTypeKey; string PropertyAlias; string? Culture; string? Segment; string Nonce; BlockDataDto Content; BlockDataDto? Settings }`
  - `BlockDataDto { Guid Key; Guid ContentTypeKey; List<BlockValueDto> Values }`
  - `BlockValueDto { string Alias; string? Culture; string? Segment; JsonElement? Value; string? EditorAlias }`
  - `RenderOutcome(string Status, string? Html, string? Reason, string? Message)` with `Rendered(html)`, `NotPreviewable(reason)` and `Error(message)`
  - `BlockListValueJson.Build(RenderRequest) : string`
  - `FrameScript.Inject(string html, string nonce) : string`

- [ ] **Step 1: Failing tests**

`TST/BlockListValueJsonTests.cs`:
```csharp
using System.Text.Json;
using System.Text.Json.Nodes;
using Esatto.Umbraco.Backoffice.BlockPreview.Rendering;
using Xunit;

namespace Esatto.Umbraco.Backoffice.BlockPreview.Tests;

public class BlockListValueJsonTests
{
    private static readonly Guid ContentKey = Guid.Parse("11111111-1111-1111-1111-111111111111");
    private static readonly Guid SettingsKey = Guid.Parse("22222222-2222-2222-2222-222222222222");

    private static RenderRequest Request(string? culture = "sv", bool withSettings = false) => new()
    {
        DocumentKey = Guid.NewGuid(),
        DocumentTypeKey = Guid.NewGuid(),
        PropertyAlias = "carouselList",
        Culture = culture,
        Nonce = "n1",
        Content = new BlockDataDto
        {
            Key = ContentKey,
            ContentTypeKey = Guid.Parse("33333333-3333-3333-3333-333333333333"),
            Values =
            [
                new BlockValueDto
                {
                    Alias = "images",
                    EditorAlias = "Umbraco.MediaPicker3",
                    Value = JsonDocument.Parse("""[{"key":"k1","mediaKey":"m1"}]""").RootElement,
                },
            ],
        },
        Settings = withSettings
            ? new BlockDataDto { Key = SettingsKey, ContentTypeKey = Guid.NewGuid(), Values = [] }
            : null,
    };

    private static JsonNode Parse(RenderRequest request) => JsonNode.Parse(BlockListValueJson.Build(request))!;

    [Fact]
    public void Layout_references_the_content_block_under_the_block_list_alias()
    {
        var layout = Parse(Request())["layout"]!["Umbraco.BlockList"]!.AsArray();
        Assert.Equal(ContentKey.ToString(), Assert.Single(layout)!["contentKey"]!.GetValue<string>());
    }

    [Fact]
    public void Content_data_keeps_the_raw_values()
    {
        var content = Assert.Single(Parse(Request())["contentData"]!.AsArray())!;
        var value = Assert.Single(content["values"]!.AsArray())!;
        Assert.Equal("images", value["alias"]!.GetValue<string>());
        Assert.Equal("Umbraco.MediaPicker3", value["editorAlias"]!.GetValue<string>());
        Assert.Equal("m1", value["value"]![0]!["mediaKey"]!.GetValue<string>());
    }

    // Review Focus 2.
    [Fact]
    public void Without_settings_there_is_no_settings_key_and_no_settings_data()
    {
        var json = Parse(Request(withSettings: false));
        Assert.Null(json["layout"]!["Umbraco.BlockList"]![0]!["settingsKey"]);
        Assert.Empty(json["settingsData"]!.AsArray());
    }

    [Fact]
    public void With_settings_the_layout_links_them()
    {
        var json = Parse(Request(withSettings: true));
        Assert.Equal(SettingsKey.ToString(), json["layout"]!["Umbraco.BlockList"]![0]!["settingsKey"]!.GetValue<string>());
        Assert.Single(json["settingsData"]!.AsArray());
    }

    // Review Focus 1: the converter only keeps exposed blocks.
    [Fact]
    public void Expose_contains_culture_and_invariant_entries()
    {
        var expose = Parse(Request(culture: "sv"))["expose"]!.AsArray();
        Assert.Contains(expose, e => e!["culture"]?.GetValue<string>() == "sv");
        Assert.Contains(expose, e => e!["culture"] is null);
        Assert.All(expose, e => Assert.Equal(ContentKey.ToString(), e!["contentKey"]!.GetValue<string>()));
    }

    [Fact]
    public void Invariant_document_gets_a_single_invariant_expose_entry()
    {
        Assert.Single(Parse(Request(culture: null))["expose"]!.AsArray());
    }
}
```

`TST/FrameScriptTests.cs`:
```csharp
using Esatto.Umbraco.Backoffice.BlockPreview.Rendering;
using Xunit;

namespace Esatto.Umbraco.Backoffice.BlockPreview.Tests;

public class FrameScriptTests
{
    [Fact]
    public void Injects_before_the_closing_body_tag_once()
    {
        string result = FrameScript.Inject("<html><body><p>x</p></body></html>", "abc");
        int script = result.IndexOf("esatto-block-preview:height", StringComparison.Ordinal);
        Assert.True(script > 0 && script < result.LastIndexOf("</body>", StringComparison.OrdinalIgnoreCase));
        Assert.Equal(1, CountOf(result, "esatto-block-preview:height"));
    }

    // Review Focus 3.
    [Fact]
    public void Appends_script_when_body_tag_missing()
    {
        string result = FrameScript.Inject("<p>no body tag</p>", "abc");
        Assert.StartsWith("<p>no body tag</p>", result);
        Assert.Contains("esatto-block-preview:height", result);
    }

    [Fact]
    public void Matches_body_tag_case_insensitively()
    {
        string result = FrameScript.Inject("<HTML><BODY>x</BODY></HTML>", "abc");
        Assert.True(result.IndexOf("<script>", StringComparison.Ordinal) < result.IndexOf("</BODY>", StringComparison.Ordinal));
    }

    [Fact]
    public void Embeds_the_nonce_as_a_json_string()
    {
        Assert.Contains("\"a\\u0022b\"", FrameScript.Inject("<body></body>", "a\"b"));
    }

    private static int CountOf(string text, string value)
    {
        int count = 0;
        for (int i = text.IndexOf(value, StringComparison.Ordinal); i >= 0; i = text.IndexOf(value, i + 1, StringComparison.Ordinal)) count++;
        return count;
    }
}
```

Run: `dotnet test Esatto.Umbraco.Backoffice.BlockPreview.Tests` → Expected: FAIL to build (CS0246 `RenderRequest`, `BlockDataDto`, `BlockValueDto`, `BlockListValueJson`, `FrameScript`).

- [ ] **Step 2: Implement**

`PKG/src/Rendering/RenderRequest.cs`:
```csharp
using System.Text.Json;

namespace Esatto.Umbraco.Backoffice.BlockPreview.Rendering;

/// <summary>What the backoffice sends to render one block. Values are the block's raw, unsaved data.</summary>
public sealed class RenderRequest
{
    public Guid DocumentKey { get; set; }
    public Guid DocumentTypeKey { get; set; }
    public string PropertyAlias { get; set; } = string.Empty;
    public string? Culture { get; set; }
    public string? Segment { get; set; }
    /// <summary>Per-render id the iframe echoes in its height messages.</summary>
    public string Nonce { get; set; } = string.Empty;
    public BlockDataDto Content { get; set; } = new();
    public BlockDataDto? Settings { get; set; }
}

public sealed class BlockDataDto
{
    public Guid Key { get; set; }
    public Guid ContentTypeKey { get; set; }
    public List<BlockValueDto> Values { get; set; } = [];
}

public sealed class BlockValueDto
{
    public string Alias { get; set; } = string.Empty;
    public string? Culture { get; set; }
    public string? Segment { get; set; }
    public JsonElement? Value { get; set; }
    public string? EditorAlias { get; set; }
}
```

`PKG/src/Rendering/RenderOutcome.cs`:
```csharp
namespace Esatto.Umbraco.Backoffice.BlockPreview.Rendering;

/// <summary>Every handled outcome is a 200 with one of three statuses; the backoffice never sees an exception page.</summary>
public sealed record RenderOutcome(string Status, string? Html = null, string? Reason = null, string? Message = null)
{
    public static RenderOutcome Rendered(string html) => new("rendered", Html: html);

    /// <param name="reason"><c>noPartial</c>, <c>ignored</c> or <c>unsavedDocument</c>.</param>
    public static RenderOutcome NotPreviewable(string reason) => new("notPreviewable", Reason: reason);

    public static RenderOutcome Error(string message) => new("error", Message: message);
}
```

`PKG/src/Rendering/BlockListValueJson.cs`:
```csharp
using System.Text.Json;
using System.Text.Json.Nodes;

namespace Esatto.Umbraco.Backoffice.BlockPreview.Rendering;

/// <summary>
/// Builds the stored JSON of a one-item Block List value from a render request, so it can be
/// converted by the document property's own value converter (the same pipeline as the frontend).
/// </summary>
public static class BlockListValueJson
{
    private const string BlockListLayoutAlias = "Umbraco.BlockList";

    public static string Build(RenderRequest request)
    {
        var layoutItem = new JsonObject { ["contentKey"] = request.Content.Key.ToString() };
        if (request.Settings is not null)
        {
            layoutItem["settingsKey"] = request.Settings.Key.ToString();
        }

        var value = new JsonObject
        {
            ["layout"] = new JsonObject { [BlockListLayoutAlias] = new JsonArray(layoutItem) },
            ["contentData"] = new JsonArray(Data(request.Content)),
            ["settingsData"] = request.Settings is null ? new JsonArray() : new JsonArray(Data(request.Settings)),
            ["expose"] = Expose(request),
        };
        return value.ToJsonString();
    }

    private static JsonObject Data(BlockDataDto data)
    {
        var values = new JsonArray();
        foreach (BlockValueDto v in data.Values)
        {
            values.Add(new JsonObject
            {
                ["alias"] = v.Alias,
                ["culture"] = v.Culture,
                ["segment"] = v.Segment,
                ["value"] = v.Value is { } element ? JsonNode.Parse(element.GetRawText()) : null,
                ["editorAlias"] = v.EditorAlias,
            });
        }

        return new JsonObject
        {
            ["contentTypeKey"] = data.ContentTypeKey.ToString(),
            ["key"] = data.Key.ToString(),
            ["values"] = values,
        };
    }

    // The converter only keeps blocks exposed for the variation being rendered. Expose the block
    // invariantly and, for a culture-variant document, for the requested culture/segment too, so it
    // converts whether or not the element type itself varies.
    private static JsonArray Expose(RenderRequest request)
    {
        string contentKey = request.Content.Key.ToString();
        var expose = new JsonArray(new JsonObject { ["contentKey"] = contentKey, ["culture"] = null, ["segment"] = null });
        if (request.Culture is not null)
        {
            expose.Add(new JsonObject { ["contentKey"] = contentKey, ["culture"] = request.Culture, ["segment"] = request.Segment });
        }

        return expose;
    }
}
```

`PKG/src/Rendering/FrameScript.cs`:
```csharp
using System.Text.Json;

namespace Esatto.Umbraco.Backoffice.BlockPreview.Rendering;

/// <summary>
/// The inline script every preview page gets. It reports the page height to the backoffice
/// (which sizes the iframe) and stops links inside the preview from navigating it away.
/// </summary>
public static class FrameScript
{
    private const string Template = """
        <script>
        (function () {
          var id = __NONCE__;
          function post() {
            var height = Math.ceil(document.documentElement.getBoundingClientRect().height);
            parent.postMessage({ type: "esatto-block-preview:height", id: id, height: height }, "*");
          }
          new ResizeObserver(post).observe(document.documentElement);
          window.addEventListener("load", post);
          document.addEventListener("click", function (e) {
            var a = e.target && e.target.closest ? e.target.closest("a[href]") : null;
            if (a && a.getAttribute("href").charAt(0) !== "#") e.preventDefault();
          }, true);
        })();
        </script>
        """;

    public static string Inject(string html, string nonce)
    {
        // JsonSerializer escapes quotes and '<' so the nonce can't break out of the script.
        string script = Template.Replace("__NONCE__", JsonSerializer.Serialize(nonce));
        int bodyEnd = html.LastIndexOf("</body>", StringComparison.OrdinalIgnoreCase);
        return bodyEnd < 0 ? html + script : html.Insert(bodyEnd, script);
    }
}
```

- [ ] **Step 3: Tests pass**: `dotnet test …Tests` → Expected: PASS (8 + 10 = 18).
- [ ] **Step 4: Commit**: `git commit -m "BlockPreview: render request, block value JSON and frame script"`

---

### Task 4: Renderer, block item factory, view renderer, controller, Razor views

**Files:**
- Create: `PKG/src/Rendering/FrameModel.cs`, `IBlockItemFactory.cs`, `BlockItemFactory.cs`, `IViewRenderer.cs`, `RazorViewRenderer.cs`, `BlockPreviewRenderer.cs`, `PKG/src/BlockPreviewController.cs`, `PKG/Views/EsattoBlockPreview/Frame.cshtml`, `PKG/Views/EsattoBlockPreview/_DefaultLayout.cshtml`
- Modify: `PKG/src/BlockPreviewServiceCollectionExtensions.cs` (register the renderer services)
- Test: `TST/BlockPreviewRendererTests.cs`

**Interfaces:**
- Consumes: Task 3's DTOs, `BlockListValueJson`, `FrameScript`, `RenderOutcome`; Task 2's `BlockPreviewOptions`.
- Produces:
  - `IBlockItemFactory.GetContentTypeAlias(Guid elementTypeKey) : string` and `IBlockItemFactory.Create(RenderRequest) : BlockItemResult` (`BlockItemResult(object? Item, string ContentTypeAlias, bool OwnerMissing)`). The renderer checks `IgnoredContentTypes` with the alias **before** converting, so an ignored block never gets converted.
  - `IViewRenderer.ViewExists(string path) : bool` and `RenderAsync(string path, object model, ActionContext) : Task<string>`
  - `BlockPreviewRenderer.RenderAsync(RenderRequest, ActionContext) : Task<RenderOutcome>`
  - `FrameModel(string PartialPath, object Item, string LayoutPath, string[] Stylesheets, string[] Scripts)`
  - Controller route `POST esatto/block-preview/render`.

- [ ] **Step 1: Failing tests** — `TST/BlockPreviewRendererTests.cs`:
```csharp
using System.Globalization;
using Esatto.Umbraco.Backoffice.BlockPreview;
using Esatto.Umbraco.Backoffice.BlockPreview.Rendering;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using NSubstitute;
using NSubstitute.ExceptionExtensions;
using Umbraco.Cms.Core.Models.PublishedContent;
using Xunit;

namespace Esatto.Umbraco.Backoffice.BlockPreview.Tests;

public class BlockPreviewRendererTests
{
    private const string Partial = "~/Views/Partials/blocklist/Components/carouselBlock.cshtml";
    private readonly IBlockItemFactory _items = Substitute.For<IBlockItemFactory>();
    private readonly IViewRenderer _views = Substitute.For<IViewRenderer>();
    private readonly VariationAccessor _variation = new();

    // A real accessor: the test reads what the renderer set while rendering.
    private sealed class VariationAccessor : IVariationContextAccessor
    {
        public VariationContext? VariationContext { get; set; }
    }
    private readonly object _item = new();

    private BlockPreviewRenderer Renderer(BlockPreviewOptions? options = null) =>
        new(_items, _views, _variation, Options.Create(options ?? new BlockPreviewOptions()), NullLogger<BlockPreviewRenderer>.Instance);

    private static RenderRequest Request(string? culture = null) => new() { Nonce = "n1", Culture = culture, Content = new() { ContentTypeKey = Guid.NewGuid() } };

    private void ItemConverts()
    {
        _items.GetContentTypeAlias(Arg.Any<Guid>()).Returns("carouselBlock");
        _items.Create(Arg.Any<RenderRequest>()).Returns(new BlockItemResult(_item, "carouselBlock", OwnerMissing: false));
    }

    [Fact]
    public async Task Ignored_content_type_is_not_previewable_and_not_converted()
    {
        ItemConverts();
        var options = new BlockPreviewOptions { BlockList = new() { IgnoredContentTypes = ["carouselBlock"] } };
        var outcome = await Renderer(options).RenderAsync(Request(), new ActionContext());
        Assert.Equal(RenderOutcome.NotPreviewable("ignored"), outcome);
        _items.DidNotReceive().Create(Arg.Any<RenderRequest>());
    }

    [Fact]
    public async Task Missing_partial_is_not_previewable()
    {
        ItemConverts();
        _views.ViewExists(Partial).Returns(false);
        Assert.Equal(RenderOutcome.NotPreviewable("noPartial"), await Renderer().RenderAsync(Request(), new ActionContext()));
    }

    [Fact]
    public async Task Unsaved_document_is_not_previewable()
    {
        _items.Create(Arg.Any<RenderRequest>()).Returns(new BlockItemResult(null, "carouselBlock", OwnerMissing: true));
        Assert.Equal(RenderOutcome.NotPreviewable("unsavedDocument"), await Renderer().RenderAsync(Request(), new ActionContext()));
    }

    [Fact]
    public async Task Uses_the_site_layout_when_it_exists()
    {
        ItemConverts();
        _views.ViewExists(Arg.Any<string>()).Returns(true);
        _views.RenderAsync(Arg.Any<string>(), Arg.Any<object>(), Arg.Any<ActionContext>()).Returns("<body></body>");

        await Renderer().RenderAsync(Request(), new ActionContext());

        await _views.Received().RenderAsync(BlockPreviewRenderer.FrameView,
            Arg.Is<FrameModel>(m => m.LayoutPath == "~/Views/BlockPreview.cshtml" && m.PartialPath == Partial && m.Item == _item),
            Arg.Any<ActionContext>());
    }

    [Fact]
    public async Task Falls_back_to_the_built_in_layout()
    {
        ItemConverts();
        _views.ViewExists(Partial).Returns(true);
        _views.ViewExists("~/Views/BlockPreview.cshtml").Returns(false);
        _views.RenderAsync(Arg.Any<string>(), Arg.Any<object>(), Arg.Any<ActionContext>()).Returns("<body></body>");

        await Renderer(new BlockPreviewOptions { Stylesheets = ["/a.css"] }).RenderAsync(Request(), new ActionContext());

        await _views.Received().RenderAsync(BlockPreviewRenderer.FrameView,
            Arg.Is<FrameModel>(m => m.LayoutPath == BlockPreviewRenderer.DefaultLayoutView && m.Stylesheets.SequenceEqual(new[] { "/a.css" })),
            Arg.Any<ActionContext>());
    }

    [Fact]
    public async Task Rendered_html_gets_the_frame_script()
    {
        ItemConverts();
        _views.ViewExists(Arg.Any<string>()).Returns(true);
        _views.RenderAsync(Arg.Any<string>(), Arg.Any<object>(), Arg.Any<ActionContext>()).Returns("<body>ok</body>");

        var outcome = await Renderer().RenderAsync(Request(), new ActionContext());

        Assert.Equal("rendered", outcome.Status);
        Assert.Contains("esatto-block-preview:height", outcome.Html);
        Assert.Contains("\"n1\"", outcome.Html);
    }

    [Fact]
    public async Task Exceptions_become_error_outcomes()
    {
        _items.Create(Arg.Any<RenderRequest>()).Throws(new InvalidOperationException("boom"));
        var outcome = await Renderer().RenderAsync(Request(), new ActionContext());
        Assert.Equal("error", outcome.Status);
        Assert.Contains("boom", outcome.Message);
    }

    // Review Focus 1.
    [Fact]
    public async Task Sets_variation_context_and_culture_while_rendering()
    {
        ItemConverts();
        _views.ViewExists(Arg.Any<string>()).Returns(true);
        string? cultureDuringRender = null;
        string? variationDuringRender = null;
        _views.RenderAsync(Arg.Any<string>(), Arg.Any<object>(), Arg.Any<ActionContext>()).Returns(_ =>
        {
            cultureDuringRender = CultureInfo.CurrentUICulture.Name;
            variationDuringRender = _variation.VariationContext?.Culture;
            return "<body></body>";
        });
        var before = CultureInfo.CurrentUICulture;

        await Renderer().RenderAsync(Request(culture: "sv"), new ActionContext());

        Assert.Equal("sv", cultureDuringRender);
        Assert.Equal("sv", variationDuringRender);
        Assert.Equal(before, CultureInfo.CurrentUICulture);
    }
}
```

Run: `dotnet test …Tests` → Expected: FAIL to build (CS0246 `IBlockItemFactory`, `IViewRenderer`, `BlockItemResult`, `BlockPreviewRenderer`, `FrameModel`).

- [ ] **Step 2: Implement the seams and the renderer**

`PKG/src/Rendering/FrameModel.cs`:
```csharp
namespace Esatto.Umbraco.Backoffice.BlockPreview.Rendering;

/// <summary>Model of the package's Frame view: which partial to render, with what, inside which layout.</summary>
public sealed record FrameModel(string PartialPath, object Item, string LayoutPath, string[] Stylesheets, string[] Scripts);
```

`PKG/src/Rendering/IBlockItemFactory.cs`:
```csharp
namespace Esatto.Umbraco.Backoffice.BlockPreview.Rendering;

/// <summary>
/// Converts a render request into the exact block item the frontend partial receives
/// (e.g. <c>BlockListItem&lt;CarouselBlock&gt;</c>).
/// </summary>
public interface IBlockItemFactory
{
    /// <summary>The block element type's alias (the partial's file name), without converting anything.</summary>
    string GetContentTypeAlias(Guid elementTypeKey);

    BlockItemResult Create(RenderRequest request);
}

/// <param name="Item">The block list item, or null when it couldn't be produced.</param>
/// <param name="ContentTypeAlias">The block's content type alias (the partial's file name).</param>
/// <param name="OwnerMissing">True when the document has never been saved (not in the content cache).</param>
public sealed record BlockItemResult(object? Item, string ContentTypeAlias, bool OwnerMissing);
```

`PKG/src/Rendering/BlockItemFactory.cs`:
```csharp
using Umbraco.Cms.Core.Models.Blocks;
using Umbraco.Cms.Core.Models.PublishedContent;
using Umbraco.Cms.Core.PropertyEditors;
using Umbraco.Cms.Core.PublishedCache;

namespace Esatto.Umbraco.Backoffice.BlockPreview.Rendering;

/// <summary>
/// Runs the block through the document property's own Block List value converter, the same
/// pipeline the frontend uses. The result is the frontend's <c>BlockListItem&lt;T&gt;</c>, with
/// typed models from IPublishedModelFactory (works with InMemoryAuto).
/// </summary>
public sealed class BlockItemFactory : IBlockItemFactory
{
    private readonly IPublishedContentCache _contentCache;
    private readonly IPublishedContentTypeCache _contentTypeCache;

    public BlockItemFactory(IPublishedContentCache contentCache, IPublishedContentTypeCache contentTypeCache)
    {
        _contentCache = contentCache;
        _contentTypeCache = contentTypeCache;
    }

    public string GetContentTypeAlias(Guid elementTypeKey) =>
        _contentTypeCache.Get(PublishedItemType.Element, elementTypeKey).Alias;

    public BlockItemResult Create(RenderRequest request)
    {
        string alias = GetContentTypeAlias(request.Content.ContentTypeKey);

        IPublishedContent? owner = _contentCache.GetById(preview: true, request.DocumentKey);
        if (owner is null)
        {
            return new BlockItemResult(null, alias, OwnerMissing: true);
        }

        IPublishedPropertyType propertyType =
            _contentTypeCache.Get(PublishedItemType.Content, request.DocumentTypeKey).GetPropertyType(request.PropertyAlias)
            ?? throw new InvalidOperationException($"The document type has no property '{request.PropertyAlias}'.");

        string source = BlockListValueJson.Build(request);
        object? inter = propertyType.ConvertSourceToInter(owner, source, preview: true);
        var model = propertyType.ConvertInterToObject(owner, PropertyCacheLevel.None, inter, preview: true) as BlockListModel;
        BlockListItem item = model?.FirstOrDefault()
            ?? throw new InvalidOperationException("The block could not be converted (is it allowed in this Block List?).");

        return new BlockItemResult(item, alias, OwnerMissing: false);
    }
}
```

`PKG/src/Rendering/IViewRenderer.cs`:
```csharp
using Microsoft.AspNetCore.Mvc;

namespace Esatto.Umbraco.Backoffice.BlockPreview.Rendering;

public interface IViewRenderer
{
    bool ViewExists(string path);
    Task<string> RenderAsync(string path, object model, ActionContext actionContext);
}
```

`PKG/src/Rendering/RazorViewRenderer.cs`:
```csharp
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.ModelBinding;
using Microsoft.AspNetCore.Mvc.Razor;
using Microsoft.AspNetCore.Mvc.Rendering;
using Microsoft.AspNetCore.Mvc.ViewEngines;
using Microsoft.AspNetCore.Mvc.ViewFeatures;

namespace Esatto.Umbraco.Backoffice.BlockPreview.Rendering;

/// <summary>Renders a Razor view to a string within the current request.</summary>
public sealed class RazorViewRenderer : IViewRenderer
{
    private readonly IRazorViewEngine _viewEngine;
    private readonly ITempDataProvider _tempDataProvider;
    private readonly IModelMetadataProvider _metadataProvider;

    public RazorViewRenderer(IRazorViewEngine viewEngine, ITempDataProvider tempDataProvider, IModelMetadataProvider metadataProvider)
    {
        _viewEngine = viewEngine;
        _tempDataProvider = tempDataProvider;
        _metadataProvider = metadataProvider;
    }

    public bool ViewExists(string path) => _viewEngine.GetView(executingFilePath: null, path, isMainPage: false).Success;

    public async Task<string> RenderAsync(string path, object model, ActionContext actionContext)
    {
        ViewEngineResult result = _viewEngine.GetView(executingFilePath: null, path, isMainPage: true);
        if (!result.Success)
        {
            throw new InvalidOperationException($"View '{path}' not found.");
        }

        await using var writer = new StringWriter();
        var viewData = new ViewDataDictionary(_metadataProvider, new ModelStateDictionary()) { Model = model };
        var tempData = new TempDataDictionary(actionContext.HttpContext, _tempDataProvider);
        var viewContext = new ViewContext(actionContext, result.View, viewData, tempData, writer, new HtmlHelperOptions());
        await result.View.RenderAsync(viewContext);
        return writer.ToString();
    }
}
```

`PKG/src/Rendering/BlockPreviewRenderer.cs`:
```csharp
using System.Globalization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Umbraco.Cms.Core.Models.PublishedContent;

namespace Esatto.Umbraco.Backoffice.BlockPreview.Rendering;

/// <summary>
/// Request → block item → partial inside the preview layout → HTML with the frame script.
/// Every failure becomes a <see cref="RenderOutcome"/>; nothing is thrown to the client.
/// </summary>
public sealed class BlockPreviewRenderer
{
    public const string FrameView = "/Views/EsattoBlockPreview/Frame.cshtml";
    public const string DefaultLayoutView = "/Views/EsattoBlockPreview/_DefaultLayout.cshtml";
    private const string PartialPathFormat = "~/Views/Partials/blocklist/Components/{0}.cshtml";

    private readonly IBlockItemFactory _items;
    private readonly IViewRenderer _views;
    private readonly IVariationContextAccessor _variation;
    private readonly IOptions<BlockPreviewOptions> _options;
    private readonly ILogger<BlockPreviewRenderer> _logger;

    public BlockPreviewRenderer(IBlockItemFactory items, IViewRenderer views, IVariationContextAccessor variation,
        IOptions<BlockPreviewOptions> options, ILogger<BlockPreviewRenderer> logger)
    {
        _items = items;
        _views = views;
        _variation = variation;
        _options = options;
        _logger = logger;
    }

    public async Task<RenderOutcome> RenderAsync(RenderRequest request, ActionContext actionContext)
    {
        string alias = "(unknown)";
        try
        {
            BlockPreviewOptions options = _options.Value;
            alias = _items.GetContentTypeAlias(request.Content.ContentTypeKey);

            // Checked before converting: an ignored block must show the plain row even if it can't convert.
            if (options.BlockList.IgnoredContentTypes.Contains(alias, StringComparer.OrdinalIgnoreCase))
            {
                return RenderOutcome.NotPreviewable("ignored");
            }

            BlockItemResult block = _items.Create(request);
            if (block.OwnerMissing)
            {
                return RenderOutcome.NotPreviewable("unsavedDocument");
            }

            string partial = string.Format(CultureInfo.InvariantCulture, PartialPathFormat, alias);
            if (!_views.ViewExists(partial))
            {
                return RenderOutcome.NotPreviewable("noPartial");
            }

            string layout = _views.ViewExists(options.Layout) ? options.Layout : DefaultLayoutView;
            var model = new FrameModel(partial, block.Item!, layout, options.Stylesheets, options.Scripts);

            string html = await RenderInCultureAsync(request, () => _views.RenderAsync(FrameView, model, actionContext));
            return RenderOutcome.Rendered(FrameScript.Inject(html, request.Nonce));
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Block preview failed for block type {ContentTypeAlias}.", alias);
            return RenderOutcome.Error(ex.Message);
        }
    }

    // Dictionary lookups, number/date formatting and variant property values must follow the
    // culture being edited, not the backoffice user's. Restored afterwards.
    private async Task<string> RenderInCultureAsync(RenderRequest request, Func<Task<string>> render)
    {
        if (request.Culture is null)
        {
            return await render();
        }

        VariationContext? previousVariation = _variation.VariationContext;
        CultureInfo previousCulture = CultureInfo.CurrentCulture;
        CultureInfo previousUiCulture = CultureInfo.CurrentUICulture;
        try
        {
            _variation.VariationContext = new VariationContext(request.Culture, request.Segment);
            CultureInfo.CurrentCulture = CultureInfo.CurrentUICulture = CultureInfo.GetCultureInfo(request.Culture);
            return await render();
        }
        finally
        {
            _variation.VariationContext = previousVariation;
            CultureInfo.CurrentCulture = previousCulture;
            CultureInfo.CurrentUICulture = previousUiCulture;
        }
    }
}
```

In `BlockPreviewServiceCollectionExtensions.AddBlockPreview`, before `return services;`, add:
```csharp
        services.AddScoped<IBlockItemFactory, BlockItemFactory>();
        services.AddScoped<IViewRenderer, RazorViewRenderer>();
        services.AddScoped<BlockPreviewRenderer>();
```
and add `using Esatto.Umbraco.Backoffice.BlockPreview.Rendering;`.

- [ ] **Step 3: Controller and views**

`PKG/src/BlockPreviewController.cs`:
```csharp
using Esatto.Umbraco.Backoffice.BlockPreview.Rendering;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Umbraco.Cms.Api.Management.Controllers;
using Umbraco.Cms.Api.Management.Routing;
using Umbraco.Cms.Web.Common.Authorization;

namespace Esatto.Umbraco.Backoffice.BlockPreview;

/// <summary>Renders one block's real frontend markup for the backoffice preview iframe.</summary>
[ApiController]
[VersionedApiBackOfficeRoute("esatto/block-preview")]
[ApiExplorerSettings(GroupName = "Esatto Block Preview")]
[Authorize(Policy = AuthorizationPolicies.BackOfficeAccess)]
public sealed class BlockPreviewController : ManagementApiControllerBase
{
    private readonly BlockPreviewRenderer _renderer;

    public BlockPreviewController(BlockPreviewRenderer renderer) => _renderer = renderer;

    /// <summary>Always 200 for a handled outcome: <c>rendered</c>, <c>notPreviewable</c> or <c>error</c>.</summary>
    [HttpPost("render")]
    [ProducesResponseType<RenderOutcome>(StatusCodes.Status200OK)]
    public async Task<IActionResult> Render([FromBody] RenderRequest request)
        => Ok(await _renderer.RenderAsync(request, ControllerContext));
}
```

`PKG/Views/EsattoBlockPreview/Frame.cshtml`:
```cshtml
@model Esatto.Umbraco.Backoffice.BlockPreview.Rendering.FrameModel
@{
    // The site's preview layout (or the built-in one) provides <head>, scripts and @RenderBody().
    Layout = Model.LayoutPath;
}
@await Html.PartialAsync(Model.PartialPath, Model.Item)
```

`PKG/Views/EsattoBlockPreview/_DefaultLayout.cshtml`:
```cshtml
@model Esatto.Umbraco.Backoffice.BlockPreview.Rendering.FrameModel
<!doctype html>
<html>
<head>
    <meta charset="utf-8" />
    <meta name="viewport" content="width=device-width, initial-scale=1" />
    @foreach (string href in Model.Stylesheets)
    {
        <link rel="stylesheet" href="@href" />
    }
</head>
<body>
    @RenderBody()
    @foreach (string src in Model.Scripts)
    {
        <script src="@src"></script>
    }
</body>
</html>
```

- [ ] **Step 4: Tests pass + build**: run `dotnet test …Tests` → Expected: PASS (18 + 8 = 26). Then `dotnet build PKG -c Release` → succeeds. Check the views are compiled into the assembly: `ilspycmd -l c PKG/bin/Release/net10.0/Esatto.Umbraco.Backoffice.BlockPreview.dll | grep -i "EsattoBlockPreview_Frame\|_DefaultLayout"` lists two `AspNetCore.Views_EsattoBlockPreview_*` classes.
- [ ] **Step 5: Commit**: `git commit -m "BlockPreview: render endpoint, block item conversion, frame and default layout"`

---

### Task 5: Client pure logic (render request, frame messages)

**Files:** Create `CL/src/render-request.ts`, `CL/src/frame-messages.ts`, `CL/test/render-request.test.ts`, `CL/test/frame-messages.test.ts`

**Interfaces:**
- Produces:
  - `BlockData = { key: string; contentTypeKey: string; values: Array<{ alias: string; culture?: string | null; segment?: string | null; value?: unknown; editorAlias?: string }> }`
  - `RenderRequestBody`
  - `buildRenderRequest(input: RenderInput): RenderRequestBody`
  - `requestFingerprint(body: RenderRequestBody): string`
  - `readHeight(event: { source: unknown; data: unknown }, expectedSource: unknown, nonce: string): number | null`
  - `HEIGHT_MESSAGE_TYPE`

- [ ] **Step 1: Failing tests**

`CL/test/render-request.test.ts`:
```ts
import { describe, it, expect } from 'vitest';
import { buildRenderRequest, requestFingerprint, type RenderInput } from '../src/render-request';

const input = (over: Partial<RenderInput> = {}): RenderInput => ({
  documentKey: 'doc',
  documentTypeKey: 'doctype',
  propertyAlias: 'carouselList',
  culture: 'sv',
  segment: null,
  nonce: 'n1',
  content: { key: 'c1', contentTypeKey: 'ct', values: [{ alias: 'images', value: [{ mediaKey: 'm1' }], editorAlias: 'Umbraco.MediaPicker3' }] },
  settings: undefined,
  ...over,
});

describe('buildRenderRequest', () => {
  it('maps the contexts and block data', () => {
    const body = buildRenderRequest(input());
    expect(body).toEqual({
      documentKey: 'doc',
      documentTypeKey: 'doctype',
      propertyAlias: 'carouselList',
      culture: 'sv',
      segment: null,
      nonce: 'n1',
      content: input().content,
      settings: null,
    });
  });

  it('passes settings through when present', () => {
    const settings = { key: 's1', contentTypeKey: 'st', values: [] };
    expect(buildRenderRequest(input({ settings })).settings).toEqual(settings);
  });

  it('treats an invariant variant as culture null', () => {
    expect(buildRenderRequest(input({ culture: undefined })).culture).toBeNull();
  });
});

// Review Focus 5: unchanged data must not re-render.
describe('requestFingerprint', () => {
  it('ignores the nonce', () => {
    expect(requestFingerprint(buildRenderRequest(input({ nonce: 'a' }))))
      .toBe(requestFingerprint(buildRenderRequest(input({ nonce: 'b' }))));
  });

  it('changes when a value changes', () => {
    const changed = input({ content: { key: 'c1', contentTypeKey: 'ct', values: [{ alias: 'images', value: [] }] } });
    expect(requestFingerprint(buildRenderRequest(changed))).not.toBe(requestFingerprint(buildRenderRequest(input())));
  });

  it('changes when the culture changes', () => {
    expect(requestFingerprint(buildRenderRequest(input({ culture: 'en-GB' }))))
      .not.toBe(requestFingerprint(buildRenderRequest(input())));
  });
});
```

`CL/test/frame-messages.test.ts`:
```ts
import { describe, it, expect } from 'vitest';
import { readHeight, HEIGHT_MESSAGE_TYPE } from '../src/frame-messages';

const frame = {};
const msg = (data: unknown, source: unknown = frame) => ({ source, data });

// Review Focus 4.
describe('readHeight', () => {
  it('accepts a height from this frame with the current nonce', () => {
    expect(readHeight(msg({ type: HEIGHT_MESSAGE_TYPE, id: 'n1', height: 512 }), frame, 'n1')).toBe(512);
  });

  it('rejects a message from another frame', () => {
    expect(readHeight(msg({ type: HEIGHT_MESSAGE_TYPE, id: 'n1', height: 512 }, {}), frame, 'n1')).toBeNull();
  });

  it('rejects a stale nonce', () => {
    expect(readHeight(msg({ type: HEIGHT_MESSAGE_TYPE, id: 'old', height: 512 }), frame, 'n1')).toBeNull();
  });

  it('rejects other message types and malformed data', () => {
    expect(readHeight(msg({ type: 'other', id: 'n1', height: 512 }), frame, 'n1')).toBeNull();
    expect(readHeight(msg('not an object'), frame, 'n1')).toBeNull();
    expect(readHeight(msg(null), frame, 'n1')).toBeNull();
  });

  it('rejects non-finite or non-positive heights', () => {
    for (const height of [0, -5, Number.NaN, Number.POSITIVE_INFINITY, '512']) {
      expect(readHeight(msg({ type: HEIGHT_MESSAGE_TYPE, id: 'n1', height }), frame, 'n1')).toBeNull();
    }
  });

  it('rounds up fractional heights', () => {
    expect(readHeight(msg({ type: HEIGHT_MESSAGE_TYPE, id: 'n1', height: 100.2 }), frame, 'n1')).toBe(101);
  });
});
```

Run: `cd PKG/Client && npx vitest run` → Expected: FAIL (cannot resolve `../src/render-request`, `../src/frame-messages`).

- [ ] **Step 2: Implement**

`CL/src/render-request.ts`:
```ts
// Pure: the body sent to the render endpoint, and a fingerprint deciding whether a
// re-render is needed (the nonce is per render, so it's not part of it).

export interface BlockDataValue {
  alias: string;
  culture?: string | null;
  segment?: string | null;
  value?: unknown;
  editorAlias?: string;
}

export interface BlockData {
  key: string;
  contentTypeKey: string;
  values: BlockDataValue[];
}

export interface RenderInput {
  documentKey: string;
  documentTypeKey: string;
  propertyAlias: string;
  culture: string | null | undefined;
  segment: string | null | undefined;
  nonce: string;
  content: BlockData;
  settings: BlockData | undefined;
}

export interface RenderRequestBody {
  documentKey: string;
  documentTypeKey: string;
  propertyAlias: string;
  culture: string | null;
  segment: string | null;
  nonce: string;
  content: BlockData;
  settings: BlockData | null;
}

export function buildRenderRequest(input: RenderInput): RenderRequestBody {
  return {
    documentKey: input.documentKey,
    documentTypeKey: input.documentTypeKey,
    propertyAlias: input.propertyAlias,
    culture: input.culture ?? null,
    segment: input.segment ?? null,
    nonce: input.nonce,
    content: input.content,
    settings: input.settings ?? null,
  };
}

export function requestFingerprint(body: RenderRequestBody): string {
  const { nonce: _nonce, ...rest } = body;
  return JSON.stringify(rest);
}
```

`CL/src/frame-messages.ts`:
```ts
// Pure: accept a height message only from this block's own iframe and current render.

export const HEIGHT_MESSAGE_TYPE = 'esatto-block-preview:height';

export function readHeight(event: { source: unknown; data: unknown }, expectedSource: unknown, nonce: string): number | null {
  if (event.source !== expectedSource) return null;
  const data = event.data as { type?: unknown; id?: unknown; height?: unknown } | null;
  if (!data || typeof data !== 'object') return null;
  if (data.type !== HEIGHT_MESSAGE_TYPE || data.id !== nonce) return null;
  const { height } = data;
  if (typeof height !== 'number' || !Number.isFinite(height) || height <= 0) return null;
  return Math.ceil(height);
}
```

- [ ] **Step 3: Tests pass**: `npx vitest run` → Expected: PASS (6 + 6 = 12).
- [ ] **Step 4: Commit**: `git commit -m "BlockPreview: client render request and frame message logic"`

---

### Task 6: The custom view element and API call

**Files:** Create `CL/src/api.ts`. Replace `CL/src/block-preview-frame.element.ts`.

**Interfaces:**
- Consumes: Task 5 (`buildRenderRequest`, `requestFingerprint`, `readHeight`, types) and the server contract from Tasks 3–4.
- Produces: the `esatto-block-preview-frame` custom element in `block-preview-frame.js`.

- [ ] **Step 1: `CL/src/api.ts`**
```ts
import { umbHttpClient } from '@umbraco-cms/backoffice/http-client';
import type { RenderRequestBody } from './render-request';

export type RenderOutcome =
  | { status: 'rendered'; html: string }
  | { status: 'notPreviewable'; reason: 'noPartial' | 'ignored' | 'unsavedDocument' }
  | { status: 'error'; message: string };

const ENDPOINT = '/umbraco/management/api/v1/esatto/block-preview/render';

// `security: [bearer]` makes the shared backoffice client attach the user's token.
export function renderBlockPreview(body: RenderRequestBody) {
  return umbHttpClient.post<{ 200: RenderOutcome }>({
    security: [{ scheme: 'bearer', type: 'http' }],
    url: ENDPOINT,
    body,
    headers: { 'Content-Type': 'application/json' },
  });
}
```

- [ ] **Step 2: `CL/src/block-preview-frame.element.ts`**
```ts
// Generic Block List custom view: shows the block's real frontend rendering — the site's
// Razor partial inside the site's preview layout — in a sandboxed, auto-sized iframe.
// A slim label bar above it opens the block editor; the preview itself is interactive.

import { css, customElement, html, nothing, property, state } from '@umbraco-cms/backoffice/external/lit';
import { UmbLitElement } from '@umbraco-cms/backoffice/lit-element';
import { tryExecute } from '@umbraco-cms/backoffice/resources';
import { UMB_BLOCK_ENTRY_CONTEXT } from '@umbraco-cms/backoffice/block';
import type { UmbBlockEditorCustomViewConfiguration, UmbBlockEditorCustomViewElement } from '@umbraco-cms/backoffice/block-custom-view';
import { UMB_CONTENT_WORKSPACE_CONTEXT } from '@umbraco-cms/backoffice/content';
import { UMB_PROPERTY_CONTEXT, UMB_PROPERTY_DATASET_CONTEXT } from '@umbraco-cms/backoffice/property';
import { buildRenderRequest, requestFingerprint, type BlockData } from './render-request';
import { readHeight } from './frame-messages';
import { renderBlockPreview, type RenderOutcome } from './api';

const DEBOUNCE_MS = 400;

type ViewState =
  | { kind: 'loading' }
  | { kind: 'rendered'; html: string; nonce: string }
  | { kind: 'plain' }
  | { kind: 'unsaved' }
  | { kind: 'error'; message: string };

@customElement('esatto-block-preview-frame')
export class EsattoBlockPreviewFrameElement extends UmbLitElement implements UmbBlockEditorCustomViewElement {
  @property({ attribute: false }) content?: Record<string, unknown>;
  @property({ attribute: false }) settings?: Record<string, unknown>;
  @property({ attribute: false }) label?: string;
  @property({ attribute: false }) icon?: string;
  @property({ attribute: false }) config?: UmbBlockEditorCustomViewConfiguration;
  @property({ attribute: false }) contentInvalid?: boolean;
  @property({ attribute: false }) settingsInvalid?: boolean;

  @state() private _view: ViewState = { kind: 'loading' };
  @state() private _height = 0;

  #entry?: typeof UMB_BLOCK_ENTRY_CONTEXT.TYPE;
  #workspace?: typeof UMB_CONTENT_WORKSPACE_CONTEXT.TYPE;
  #property?: typeof UMB_PROPERTY_CONTEXT.TYPE;
  #dataset?: typeof UMB_PROPERTY_DATASET_CONTEXT.TYPE;
  #timer?: ReturnType<typeof setTimeout>;
  #lastFingerprint?: string;
  #currentNonce = '';

  constructor() {
    super();
    this.consumeContext(UMB_BLOCK_ENTRY_CONTEXT, (ctx) => { this.#entry = ctx; this.#schedule(0); });
    this.consumeContext(UMB_CONTENT_WORKSPACE_CONTEXT, (ctx) => { this.#workspace = ctx; this.#schedule(0); });
    this.consumeContext(UMB_PROPERTY_CONTEXT, (ctx) => { this.#property = ctx; this.#schedule(0); });
    this.consumeContext(UMB_PROPERTY_DATASET_CONTEXT, (ctx) => { this.#dataset = ctx; this.#schedule(0); });
  }

  connectedCallback(): void {
    super.connectedCallback();
    window.addEventListener('message', this.#onMessage);
  }

  disconnectedCallback(): void {
    super.disconnectedCallback();
    window.removeEventListener('message', this.#onMessage);
    clearTimeout(this.#timer);
  }

  protected updated(changed: Map<PropertyKey, unknown>): void {
    if (changed.has('content') || changed.has('settings')) this.#schedule(DEBOUNCE_MS);
  }

  #schedule(delay: number): void {
    clearTimeout(this.#timer);
    this.#timer = setTimeout(() => void this.#render(), delay);
  }

  async #render(): Promise<void> {
    const content = this.#entry?.getContent() as BlockData | undefined;
    const documentKey = this.#workspace?.getUnique();
    const documentTypeKey = this.#workspace?.getContentTypeUnique();
    const propertyAlias = this.#property?.getAlias();
    if (!content || !documentKey || !documentTypeKey || !propertyAlias) return; // contexts not ready yet

    const variant = this.#dataset?.getVariantId();
    const nonce = crypto.randomUUID();
    const body = buildRenderRequest({
      documentKey,
      documentTypeKey,
      propertyAlias,
      culture: variant?.culture,
      segment: variant?.segment,
      nonce,
      content,
      settings: this.#entry?.getSettings() as BlockData | undefined,
    });

    const fingerprint = requestFingerprint(body);
    if (fingerprint === this.#lastFingerprint) return;
    this.#lastFingerprint = fingerprint;

    const { data, error } = await tryExecute(this, renderBlockPreview(body), { disableNotifications: true });
    if (fingerprint !== this.#lastFingerprint) return; // a newer render started meanwhile
    if (error || !data) {
      this.#lastFingerprint = undefined; // retry on the next change
      this._view = { kind: 'error', message: 'The preview could not be loaded.' };
      return;
    }
    this._view = this.#toView(data as RenderOutcome, nonce);
  }

  #toView(outcome: RenderOutcome, nonce: string): ViewState {
    switch (outcome.status) {
      case 'rendered':
        this.#currentNonce = nonce;
        return { kind: 'rendered', html: outcome.html, nonce };
      case 'notPreviewable':
        return outcome.reason === 'unsavedDocument' ? { kind: 'unsaved' } : { kind: 'plain' };
      default:
        return { kind: 'error', message: outcome.message };
    }
  }

  #onMessage = (event: MessageEvent): void => {
    const frame = this.shadowRoot?.querySelector('iframe');
    const height = readHeight(event, frame?.contentWindow, this.#currentNonce);
    if (height !== null) this._height = height;
  };

  #renderBar() {
    const invalid = this.contentInvalid || this.settingsInvalid;
    return html`
      <div class="bar ${invalid ? 'invalid' : ''}">
        <a class="label" href=${this.config?.editContentPath ?? nothing}>
          ${this.icon ? html`<umb-icon name=${this.icon}></umb-icon>` : nothing}
          <span>${this.label}</span>
        </a>
        ${this.config?.showSettingsEdit
          ? html`<a class="settings" href=${this.config.editSettingsPath ?? nothing} title="Settings"><umb-icon name="icon-settings"></umb-icon></a>`
          : nothing}
      </div>`;
  }

  #renderBody() {
    switch (this._view.kind) {
      case 'loading':
        return html`<div class="placeholder"></div>`;
      case 'rendered':
        return html`<iframe
          title=${this.label ?? 'Block preview'}
          sandbox="allow-scripts"
          .srcdoc=${this._view.html}
          style="height:${this._height || 80}px"></iframe>`;
      case 'unsaved':
        return html`<div class="note">Save the page once to see a preview.</div>`;
      case 'error':
        return html`<div class="note warning">Preview failed: ${this._view.message}</div>`;
      default:
        return nothing; // 'plain': label bar only, like Umbraco's own row
    }
  }

  render() {
    return html`${this.#renderBar()}${this.#renderBody()}`;
  }

  static styles = css`
    :host { display: block; border: 1px solid var(--uui-color-border); border-radius: var(--uui-border-radius); overflow: hidden; background: var(--uui-color-surface); }
    .bar { display: flex; align-items: center; min-height: 40px; padding: 0 var(--uui-size-space-4); gap: var(--uui-size-space-2); }
    .bar.invalid { color: var(--uui-color-danger); }
    .label { display: flex; align-items: center; gap: var(--uui-size-space-2); flex: 1; color: inherit; text-decoration: none; font-weight: 600; }
    .label:hover { text-decoration: underline; }
    .settings { color: inherit; }
    iframe { display: block; width: 100%; border: 0; border-top: 1px solid var(--uui-color-border); }
    .placeholder { min-height: 80px; border-top: 1px solid var(--uui-color-border); background: var(--uui-color-surface-alt); }
    .note { padding: var(--uui-size-space-3) var(--uui-size-space-4); border-top: 1px solid var(--uui-color-border); color: var(--uui-color-text-alt); font-size: 0.9em; }
    .note.warning { color: var(--uui-color-warning-standalone); }
  `;
}

export default EsattoBlockPreviewFrameElement;

declare global {
  interface HTMLElementTagNameMap {
    'esatto-block-preview-frame': EsattoBlockPreviewFrameElement;
  }
}
```

- [ ] **Step 3: Type-check, test, build**

Run: `npm test && npm run build`
Expected: vitest passes 12/12, `tsc` is clean, and Vite writes `block-preview-frame.js`.

If `tsc` reports a wrong import path or member name, find the correct one in `node_modules/@umbraco-cms/backoffice/dist-cms` and record a ledger Ruling. Members and paths verified on 17.7.1:
- `UMB_BLOCK_ENTRY_CONTEXT`: `getContent()` / `getSettings()`
- `UMB_CONTENT_WORKSPACE_CONTEXT`: `getUnique()` / `getContentTypeUnique()`
- `UMB_PROPERTY_CONTEXT.getAlias()`
- `UMB_PROPERTY_DATASET_CONTEXT.getVariantId()`
- `UmbBlockEditorCustomViewConfiguration`: `editContentPath` / `editSettingsPath` / `showSettingsEdit`

Then check the bundle keeps Umbraco imports external: `grep -oE 'from ?"@umbraco-cms/[a-z/-]*"' ../wwwroot/App_Plugins/Esatto.Umbraco.Backoffice.BlockPreview/block-preview-frame.js | sort -u` lists only `@umbraco-cms/backoffice/*` specifiers.

- [ ] **Step 4: Commit**: `git add Client wwwroot` → `git commit -m "BlockPreview: generic Block List custom view with live iframe preview"`

---

### Task 7: Live verification on umbracokalaset26 (Carl)

The test site is `c:\src\umbracokalaset26` with Carl's carousel (`carouselBlock`, Bootstrap 5.3.8, `/css/carousel.css`). These changes are **temporary and not committed in that repo**: a ProjectReference, plus the new preview layout file (which Carl may keep).

- [ ] **Step 1: Wire up**

In `UmbracoKalaset26.csproj` add:
```xml
<ItemGroup Label="Local BlockPreview (temporary, do not commit)">
  <ProjectReference Include="..\Esatto.Packages\Esatto.Umbraco.Backoffice.BlockPreview\Esatto.Umbraco.Backoffice.BlockPreview.csproj" />
</ItemGroup>
```
Create `Views/BlockPreview.cshtml`:
```cshtml
@{
    Layout = null;
}
<!doctype html>
<html lang="@System.Globalization.CultureInfo.CurrentCulture.TwoLetterISOLanguageName">
<head>
    <meta charset="utf-8" />
    <meta name="viewport" content="width=device-width, initial-scale=1" />
    <link href="https://cdn.jsdelivr.net/npm/bootstrap@5.3.8/dist/css/bootstrap.min.css" rel="stylesheet" crossorigin="anonymous">
    <link href="~/css/carousel.css" rel="stylesheet" asp-append-version="true" />
</head>
<body>
    @RenderBody()
    <script src="https://cdn.jsdelivr.net/npm/bootstrap@5.3.8/dist/js/bootstrap.bundle.min.js" crossorigin="anonymous"></script>
</body>
</html>
```
Build with `dotnet build`. If Carl's site is running and locks files, ask him to stop it; don't kill his process.

- [ ] **Step 2: Carl's checklist** (Ctrl+F5, Content → Start → Carousel List)

| # | Check | Expect |
|---|---|---|
| 1 | Carousel block in the Block List | Label bar "Carousel", then the real carousel below it, sliding |
| 2 | Arrows and dots inside the preview | They work |
| 3 | Compare with the website (side by side) | Same look: same height, crop, indicators, controls |
| 4 | Click the label bar | The block editor opens |
| 5 | Add or remove an image in the editor, close it without saving the page | The preview updates within about a second |
| 6 | DevTools Console | No errors from `esatto-block-preview` |
| 7 | Switch the variant to en-GB | Still renders |
| 8 | Packages → Installed | "Esatto Umbraco Backoffice Block Preview" listed once |
| 9 | `"Esatto": { "BlockPreview": { "BlockList": { "IgnoredContentTypes": ["carouselBlock"] } } }` in appsettings, restart | Plain label row, no iframe |
| 10 | Create a new page with a Block List, add a block, don't save | "Save the page once to see a preview." |

- [ ] **Step 3: Fix failures test-first** (superpowers:systematic-debugging). Re-run `dotnet test …Tests` and `npm test`, have Carl re-check the row, and commit each fix with a message naming the row.

- [ ] **Step 4: Screenshots for the README**: Carl takes a backoffice screenshot of the carousel preview and saves it to `PKG/docs/images/carousel-preview.png`.

---

### Task 8: README, cleanup, release prep

- [ ] **Step 1: README**: rewrite `PKG/README.md`:
  - **Intro and the frontend preview first:**
    - what it does;
    - the `Views/BlockPreview.cshtml` contract (plain Razor, no `@model`/`@inherits`, `@RenderBody()`), with the example from Task 7;
    - settings;
    - which blocks get previews (all with a partial, `IgnoredContentTypes`, a site view for a type wins);
    - partials must depend only on their model;
    - limitations: Block Grid / RTE not yet, a re-render resets interactive state, unsaved new pages.
  - **Then the existing video and code preview sections**, with the new paths.
  - **"Migrating from Backoffice.BlockPreview":** the package ID and App_Plugins path change, and the element names are unchanged.
  - **Screenshot** via `https://raw.githubusercontent.com/carl-schele-esatto/Esatto.Packages/main/Esatto.Umbraco.Backoffice.BlockPreview/docs/images/carousel-preview.png`. Keep this only if Task 7 Step 4 produced the file; otherwise leave the image out.
  - **Compatibility:** 17.x Supported, 18.x Verified (if Task 7 passed).
  - **Remove the old "Developing this package / VersionPrefix" section.** Versioning is MinVer now.
- [ ] **Step 2: Legacy element cleanup:** read both elements. Fix only clear defects (behaviour-preserving), and record each in the ledger. No restyling.
- [ ] **Step 3: Verify**: run `npm test && npm run build`, `dotnet test …Tests`, and `dotnet pack PKG -c Release -o ./.local-feed -p:AutoPushToFeed=false`. Check the nupkg contains:
  - `staticwebassets/App_Plugins/Esatto.Umbraco.Backoffice.BlockPreview/{block-preview-frame.js,code-preview.element.js,video-preview.element.js,umbraco-package.json,lib/…}`;
  - `lib/net10.0/Esatto.Umbraco.Backoffice.BlockPreview.dll`;
  - `README.md` and `icon.png`.
- [ ] **Step 4: Commit, then stop for Carl.** Each of these needs his explicit go:
  - merge to `main` and push;
  - tag `Esatto.Umbraco.Backoffice.BlockPreview-1.0.0`;
  - pack (Carl pushes to nuget.org);
  - revert the temporary ProjectReference in umbracokalaset26, then swap it for the nuget package.
