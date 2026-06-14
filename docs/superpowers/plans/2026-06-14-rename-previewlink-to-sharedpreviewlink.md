# Rename PreviewLink → SharedPreviewLink Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** Rename the Umbraco package `Backoffice.PreviewLink` to `Esatto.Umbraco.Backoffice.SharedPreviewLink`, add an xUnit test project, a package icon and README screenshots, apply two low-risk cleanups, and rewire the `AI.Woowoo` consumer — with no behaviour change.

**Architecture:** A net10.0 `Microsoft.NET.Sdk.Razor` package living at `c:\src\Esatto.Packages\Backoffice.PreviewLink` (renamed). It exposes a Management API mint endpoint, two pipeline middlewares (`UsePreviewLink()`), an MVC error view, and a hand-written `App_Plugins` workspace-action (no client build). The consumer at `c:\src\AI.Woowoo` references it via central package management and resolves it privately from the Azure DevOps `esatto-packages` feed.

**Tech Stack:** .NET 10, ASP.NET Core, Umbraco.Cms 17, MSBuild Razor SDK, xUnit + NSubstitute + ASP.NET Core Data Protection, NuGet central package management + package source mapping.

---

## ⚠️ Standing rules (read before executing)

- **NO git commit / push in either repo without the user's explicit approval.** Tasks below use `git mv` for renames (this only *stages* moves — it does not commit). The single final task lists the commit commands but they run **only after the user says go**.
- **NO push to the `esatto-packages` feed.** Packing uses `-p:AutoPushToFeed=false`. The `AI.Woowoo` restore of the renamed package will not succeed until the nupkg is pushed (a later, separately-approved step).
- All paths are absolute. Two repos are involved:
  - Package: `c:\src\Esatto.Packages`
  - Consumer: `c:\src\AI.Woowoo`
- Shell is PowerShell. `git mv` / `Remove-Item` examples are PowerShell-friendly.

## Naming reference (use these exact strings everywhere)

| Concept | Old | New |
|---|---|---|
| Folder | `Backoffice.PreviewLink` | `Esatto.Umbraco.Backoffice.SharedPreviewLink` |
| csproj | `Backoffice.PreviewLink.csproj` | `Esatto.Umbraco.Backoffice.SharedPreviewLink.csproj` |
| PackageId / AssemblyName / RootNamespace | `Backoffice.PreviewLink` | `Esatto.Umbraco.Backoffice.SharedPreviewLink` |
| C# namespace | `Backoffice.PreviewLink` | `Esatto.Umbraco.Backoffice.SharedPreviewLink` |
| App_Plugins folder | `App_Plugins/Backoffice.PreviewLink` | `App_Plugins/Esatto.Umbraco.Backoffice.SharedPreviewLink` |
| manifest `id` | `backoffice-preview-link` | `Esatto.Umbraco.Backoffice.SharedPreviewLink` |
| extension `alias` | `Backoffice.PreviewLink.SharePreviewWorkspaceAction` | `Esatto.Umbraco.Backoffice.SharedPreviewLink.SharePreviewWorkspaceAction` |
| `ProtectorPurpose` value | `"Backoffice.PreviewLink"` | `"Esatto.Umbraco.Backoffice.SharedPreviewLink"` |

**Kept unchanged (do NOT rename):** all C# type names (`PreviewLinkController`, `PreviewLinkMiddleware`, etc.), the `app.UsePreviewLink()` method, the JS custom element `backoffice-preview-link-button` + its `customElements.define`, console tags `[backoffice-preview-link]`, the `data-source="backoffice-preview-link"` style marker, the `/preview-link-error` route, the `MarkerCookieName` value `BackofficePreviewLinkShare`, the server route `backoffice/preview-link`, and the JS filenames `share-preview-action.js` / `share-preview-button.js`.

---

## Task 1: Rename folder, csproj, and C# namespaces (package repo)

**Files:**
- Rename: `c:\src\Esatto.Packages\Backoffice.PreviewLink\` → `...\Esatto.Umbraco.Backoffice.SharedPreviewLink\`
- Rename: `Backoffice.PreviewLink.csproj` → `Esatto.Umbraco.Backoffice.SharedPreviewLink.csproj`
- Modify: `Esatto.Umbraco.Backoffice.SharedPreviewLink.csproj` (PackageId/RootNamespace/AssemblyName)
- Modify (namespace): `src\PreviewLinkController.cs`, `src\PreviewLinkMiddleware.cs`, `src\PreviewBadgeSuppressionMiddleware.cs`, `src\PreviewLinkErrorController.cs`, `src\PreviewLinkRequest.cs`, `src\PreviewLinkResponse.cs`
- Modify (`@using`): `Views\PreviewLinkError\Index.cshtml`

- [ ] **Step 1: Delete untracked build artifacts so the move is deterministic**

```powershell
Remove-Item -Recurse -Force "c:\src\Esatto.Packages\Backoffice.PreviewLink\obj","c:\src\Esatto.Packages\Backoffice.PreviewLink\bin" -ErrorAction SilentlyContinue
```

- [ ] **Step 2: Rename the folder and the csproj with `git mv` (preserves history, stages only)**

```powershell
git -C "c:\src\Esatto.Packages" mv "Backoffice.PreviewLink" "Esatto.Umbraco.Backoffice.SharedPreviewLink"
git -C "c:\src\Esatto.Packages" mv "Esatto.Umbraco.Backoffice.SharedPreviewLink/Backoffice.PreviewLink.csproj" "Esatto.Umbraco.Backoffice.SharedPreviewLink/Esatto.Umbraco.Backoffice.SharedPreviewLink.csproj"
```

- [ ] **Step 3: Edit the csproj identity properties**

In `c:\src\Esatto.Packages\Esatto.Umbraco.Backoffice.SharedPreviewLink\Esatto.Umbraco.Backoffice.SharedPreviewLink.csproj`:

Change:
```xml
    <RootNamespace>Backoffice.PreviewLink</RootNamespace>
    <AssemblyName>Backoffice.PreviewLink</AssemblyName>
```
to:
```xml
    <RootNamespace>Esatto.Umbraco.Backoffice.SharedPreviewLink</RootNamespace>
    <AssemblyName>Esatto.Umbraco.Backoffice.SharedPreviewLink</AssemblyName>
```

Change:
```xml
    <PackageId>Backoffice.PreviewLink</PackageId>
```
to:
```xml
    <PackageId>Esatto.Umbraco.Backoffice.SharedPreviewLink</PackageId>
```

- [ ] **Step 4: Update the C# namespace in all six source files**

In each of `src\PreviewLinkController.cs`, `src\PreviewLinkMiddleware.cs`, `src\PreviewBadgeSuppressionMiddleware.cs`, `src\PreviewLinkErrorController.cs`, `src\PreviewLinkRequest.cs`, `src\PreviewLinkResponse.cs`, change the single line:
```csharp
namespace Backoffice.PreviewLink;
```
to:
```csharp
namespace Esatto.Umbraco.Backoffice.SharedPreviewLink;
```

- [ ] **Step 5: Update the `ProtectorPurpose` constant value**

In `src\PreviewLinkController.cs`, change:
```csharp
    public const string ProtectorPurpose = "Backoffice.PreviewLink";
```
to:
```csharp
    public const string ProtectorPurpose = "Esatto.Umbraco.Backoffice.SharedPreviewLink";
```
(`PreviewLinkMiddleware` references this constant by name, so no second edit is needed there.)

- [ ] **Step 6: Update the Razor view `@using`**

In `Views\PreviewLinkError\Index.cshtml`, change the first line:
```cshtml
@using Backoffice.PreviewLink
```
to:
```cshtml
@using Esatto.Umbraco.Backoffice.SharedPreviewLink
```

- [ ] **Step 7: Build to verify the rename compiles**

Run:
```powershell
dotnet build "c:\src\Esatto.Packages\Esatto.Umbraco.Backoffice.SharedPreviewLink\Esatto.Umbraco.Backoffice.SharedPreviewLink.csproj" -c Release
```
Expected: `Build succeeded`, 0 errors. The produced assembly is `Esatto.Umbraco.Backoffice.SharedPreviewLink.dll`.

- [ ] **Step 8: Confirm no stale namespace references remain in C#/cshtml**

Run:
```powershell
Select-String -Path "c:\src\Esatto.Packages\Esatto.Umbraco.Backoffice.SharedPreviewLink\src\*.cs","c:\src\Esatto.Packages\Esatto.Umbraco.Backoffice.SharedPreviewLink\Views\PreviewLinkError\Index.cshtml" -Pattern "Backoffice\.PreviewLink"
```
Expected: **no matches**.

---

## Task 2: Rename the App_Plugins folder and update the Umbraco manifest

**Files:**
- Rename: `wwwroot\App_Plugins\Backoffice.PreviewLink\` → `wwwroot\App_Plugins\Esatto.Umbraco.Backoffice.SharedPreviewLink\`
- Modify: `wwwroot\App_Plugins\Esatto.Umbraco.Backoffice.SharedPreviewLink\umbraco-package.json`

- [ ] **Step 1: Rename the App_Plugins subfolder with `git mv`**

```powershell
git -C "c:\src\Esatto.Packages" mv "Esatto.Umbraco.Backoffice.SharedPreviewLink/wwwroot/App_Plugins/Backoffice.PreviewLink" "Esatto.Umbraco.Backoffice.SharedPreviewLink/wwwroot/App_Plugins/Esatto.Umbraco.Backoffice.SharedPreviewLink"
```

- [ ] **Step 2: Update `umbraco-package.json`**

Replace the whole file at `wwwroot\App_Plugins\Esatto.Umbraco.Backoffice.SharedPreviewLink\umbraco-package.json` with:
```json
{
  "$schema": "../umbraco-package-schema.json",
  "id": "Esatto.Umbraco.Backoffice.SharedPreviewLink",
  "name": "Esatto Shared Preview Link",
  "version": "1.0.0",
  "extensions": [
    {
      "name": "Esatto Shared Preview Workspace Action",
      "alias": "Esatto.Umbraco.Backoffice.SharedPreviewLink.SharePreviewWorkspaceAction",
      "type": "workspaceAction",
      "kind": "default",
      "weight": 200,
      "api": "/App_Plugins/Esatto.Umbraco.Backoffice.SharedPreviewLink/share-preview-action.js",
      "element": "/App_Plugins/Esatto.Umbraco.Backoffice.SharedPreviewLink/share-preview-button.js",
      "elementName": "backoffice-preview-link-button",
      "meta": {
        "label": "Share preview"
      },
      "conditions": [
        { "alias": "Umb.Condition.WorkspaceAlias", "match": "Umb.Workspace.Document" }
      ]
    }
  ]
}
```
(Note: `version` bumped `0.1.0` → `1.0.0` to match the package; `elementName` kept as `backoffice-preview-link-button` to match `customElements.define` in `share-preview-button.js`.)

- [ ] **Step 3: Rebuild to verify static web assets resolve under the new path**

Run:
```powershell
dotnet build "c:\src\Esatto.Packages\Esatto.Umbraco.Backoffice.SharedPreviewLink\Esatto.Umbraco.Backoffice.SharedPreviewLink.csproj" -c Release
```
Expected: `Build succeeded`.

- [ ] **Step 4: Confirm the old App_Plugins path no longer appears anywhere in the package**

Run:
```powershell
Select-String -Path "c:\src\Esatto.Packages\Esatto.Umbraco.Backoffice.SharedPreviewLink\wwwroot\App_Plugins\Esatto.Umbraco.Backoffice.SharedPreviewLink\*.json" -Pattern "Backoffice\.PreviewLink"
```
Expected: **no matches**.

---

## Task 3: Targeted cleanup (behaviour-preserving except the JS expiry text)

**Files:**
- Modify: `src\PreviewLinkController.cs` (add 500 response type)
- Modify: `wwwroot\App_Plugins\Esatto.Umbraco.Backoffice.SharedPreviewLink\share-preview-button.js` (`_formatExpiry`)

- [ ] **Step 1: Declare the 500 response on the mint endpoint**

In `src\PreviewLinkController.cs`, the `Mint` action currently has:
```csharp
    [HttpPost]
    [ProducesResponseType(typeof(PreviewLinkResponse), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public IActionResult Mint([FromBody] PreviewLinkRequest request)
```
Add the 500 attribute (the action returns `StatusCode(500, …)` when the Umbraco context is unavailable):
```csharp
    [HttpPost]
    [ProducesResponseType(typeof(PreviewLinkResponse), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    [ProducesResponseType(StatusCodes.Status500InternalServerError)]
    public IActionResult Mint([FromBody] PreviewLinkRequest request)
```

- [ ] **Step 2: Derive the expiry text from `expiresAt` instead of hardcoding "7 days"**

In `share-preview-button.js`, replace the `_formatExpiry` method:
```javascript
    _formatExpiry(expiresAtIso) {
        try {
            const d = new Date(expiresAtIso);
            const datePart = d.toISOString().split('T')[0];
            return `Expires ${datePart} (in 7 days)`;
        } catch {
            return '';
        }
    }
```
with:
```javascript
    _formatExpiry(expiresAtIso) {
        try {
            const d = new Date(expiresAtIso);
            if (Number.isNaN(d.getTime())) return '';
            const datePart = d.toISOString().split('T')[0];
            const msPerDay = 24 * 60 * 60 * 1000;
            const days = Math.max(0, Math.round((d.getTime() - Date.now()) / msPerDay));
            const suffix = days === 1 ? 'in 1 day' : `in ${days} days`;
            return `Expires ${datePart} (${suffix})`;
        } catch {
            return '';
        }
    }
```

- [ ] **Step 3: Build to verify the C# change compiles**

Run:
```powershell
dotnet build "c:\src\Esatto.Packages\Esatto.Umbraco.Backoffice.SharedPreviewLink\Esatto.Umbraco.Backoffice.SharedPreviewLink.csproj" -c Release
```
Expected: `Build succeeded`. (The JS change is verified manually in the backoffice later; there is no JS test harness.)

---

## Task 4: Add the package icon and README screenshots

**Files:**
- Create: `c:\src\Esatto.Packages\Esatto.Umbraco.Backoffice.SharedPreviewLink\icon.png` (copy of `C:\Users\carl_\Downloads\esatto-logo-square.png`)
- Create: `...\docs\share-preview-button.png` (copy of `C:\Users\carl_\Downloads\button.png`)
- Create: `...\docs\save-modal.png` (copy of `C:\Users\carl_\Downloads\save.png`)
- Create: `...\docs\preview-link-modal.png` (copy of `C:\Users\carl_\Downloads\previe-link.png`)
- Modify: `Esatto.Umbraco.Backoffice.SharedPreviewLink.csproj` (pack icon + screenshots)
- Modify: `README.md`

- [ ] **Step 1: Copy the image assets into the package**

```powershell
$pkg = "c:\src\Esatto.Packages\Esatto.Umbraco.Backoffice.SharedPreviewLink"
New-Item -ItemType Directory -Force "$pkg\docs" | Out-Null
Copy-Item "C:\Users\carl_\Downloads\esatto-logo-square.png" "$pkg\icon.png"
Copy-Item "C:\Users\carl_\Downloads\button.png"             "$pkg\docs\share-preview-button.png"
Copy-Item "C:\Users\carl_\Downloads\save.png"               "$pkg\docs\save-modal.png"
Copy-Item "C:\Users\carl_\Downloads\previe-link.png"        "$pkg\docs\preview-link-modal.png"
```

- [ ] **Step 2: Pack the icon (csproj `PropertyGroup` + `ItemGroup`)**

In the csproj `PropertyGroup Label="NuGet"`, after the `<PackageLicenseExpression>` line add:
```xml
    <PackageIcon>icon.png</PackageIcon>
```
The csproj already has:
```xml
  <ItemGroup>
    <None Include="README.md" Pack="true" PackagePath="\" />
  </ItemGroup>
```
Replace that `ItemGroup` with:
```xml
  <ItemGroup>
    <None Include="README.md" Pack="true" PackagePath="\" />
    <None Include="icon.png" Pack="true" PackagePath="\" />
    <None Include="docs\**\*.png" Pack="true" PackagePath="docs\" />
  </ItemGroup>
```

- [ ] **Step 3: Update the README — package name, install command, and a "How it works" walkthrough**

In `README.md`:

Change the H1 and intro:
```markdown
# Backoffice.PreviewLink

Shareable preview links for Umbraco 17.
```
to:
```markdown
# Esatto.Umbraco.Backoffice.SharedPreviewLink

Shareable preview links for Umbraco 17.
```

Change the install command:
```markdown
dotnet add package Backoffice.PreviewLink
```
to:
```markdown
dotnet add package Esatto.Umbraco.Backoffice.SharedPreviewLink
```

Immediately after the bullet list near the top (the line ending `...not just published-with-edits` block, before `## Install`), insert a new section:
```markdown
## How it works

1. In any document workspace, click **Share preview**:

   ![Share preview button](docs/share-preview-button.png)

2. The document is saved first (the usual variant picker appears for multi-variant docs):

   ![Save modal](docs/save-modal.png)

3. A tokenized preview link is minted — copy it, or open it directly:

   ![Preview link modal](docs/preview-link-modal.png)

```

In the Architecture section, update the two `App_Plugins/Backoffice.PreviewLink/...` paths to `App_Plugins/Esatto.Umbraco.Backoffice.SharedPreviewLink/...`:
```markdown
- **Workspace action** ([`share-preview-action.js`](wwwroot/App_Plugins/Esatto.Umbraco.Backoffice.SharedPreviewLink/share-preview-action.js) + [`share-preview-button.js`](wwwroot/App_Plugins/Esatto.Umbraco.Backoffice.SharedPreviewLink/share-preview-button.js))
```

- [ ] **Step 4: Pack locally (no push) and confirm icon + screenshots ship**

Run:
```powershell
dotnet pack "c:\src\Esatto.Packages\Esatto.Umbraco.Backoffice.SharedPreviewLink\Esatto.Umbraco.Backoffice.SharedPreviewLink.csproj" -c Release -p:AutoPushToFeed=false
```
Expected: `Successfully created package '...\bin\Release\Esatto.Umbraco.Backoffice.SharedPreviewLink.1.0.0.nupkg'` and NO "Auto-pushing" message.

Then verify contents:
```powershell
$nupkg = Get-ChildItem "c:\src\Esatto.Packages\Esatto.Umbraco.Backoffice.SharedPreviewLink\bin\Release\Esatto.Umbraco.Backoffice.SharedPreviewLink.1.0.0.nupkg"
Add-Type -AssemblyName System.IO.Compression.FileSystem
[System.IO.Compression.ZipFile]::OpenRead($nupkg.FullName).Entries.FullName
```
Expected entries include: `icon.png`, `README.md`, `docs/share-preview-button.png`, `docs/save-modal.png`, `docs/preview-link-modal.png`, and `staticwebassets`/lib entries for the new assembly name.

---

## Task 5: Create the test project + token-contract tests

**Files:**
- Create: `c:\src\Esatto.Packages\Esatto.Umbraco.Backoffice.SharedPreviewLink.Tests\Esatto.Umbraco.Backoffice.SharedPreviewLink.Tests.csproj`
- Create: `...\TokenContractTests.cs`

> These are characterization tests over **existing** code: write the test, run it, and expect **PASS** (the behaviour already exists). A failure means a real bug or a wrong assumption — investigate, don't paper over it.

- [ ] **Step 1: Create the test project file**

Create `Esatto.Umbraco.Backoffice.SharedPreviewLink.Tests\Esatto.Umbraco.Backoffice.SharedPreviewLink.Tests.csproj`:
```xml
<Project Sdk="Microsoft.NET.Sdk">

  <PropertyGroup>
    <TargetFramework>net10.0</TargetFramework>
    <Nullable>enable</Nullable>
    <ImplicitUsings>enable</ImplicitUsings>
    <IsPackable>false</IsPackable>
    <IsPublishable>false</IsPublishable>
  </PropertyGroup>

  <!-- Gives the test access to ASP.NET Core Data Protection + HTTP abstractions. -->
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
    <ProjectReference Include="..\Esatto.Umbraco.Backoffice.SharedPreviewLink\Esatto.Umbraco.Backoffice.SharedPreviewLink.csproj" />
  </ItemGroup>

</Project>
```

- [ ] **Step 2: Write the token-contract tests**

Create `TokenContractTests.cs`:
```csharp
using System.Security.Cryptography;
using Microsoft.AspNetCore.DataProtection;
using Esatto.Umbraco.Backoffice.SharedPreviewLink;
using Xunit;

namespace Esatto.Umbraco.Backoffice.SharedPreviewLink.Tests;

// Locks the data-protection contract shared by PreviewLinkController (mint)
// and PreviewLinkMiddleware (verify): same purpose string, GUID "N" payload,
// time-limited protector. Uses an ephemeral provider so no key ring is needed.
public class TokenContractTests
{
    private static ITimeLimitedDataProtector CreateProtector()
        => new EphemeralDataProtectionProvider()
            .CreateProtector(PreviewLinkController.ProtectorPurpose)
            .ToTimeLimitedDataProtector();

    [Fact]
    public void Protect_then_unprotect_round_trips_the_content_key()
    {
        var protector = CreateProtector();
        var key = Guid.NewGuid();

        var token = protector.Protect(key.ToString("N"), TimeSpan.FromDays(7));
        var restored = Guid.ParseExact(protector.Unprotect(token), "N");

        Assert.Equal(key, restored);
    }

    [Fact]
    public void Tampered_token_throws_CryptographicException()
    {
        var protector = CreateProtector();
        var token = protector.Protect(Guid.NewGuid().ToString("N"), TimeSpan.FromDays(7));

        // Flip the final character to simulate tampering.
        var tampered = token[..^1] + (token[^1] == 'A' ? 'B' : 'A');

        Assert.Throws<CryptographicException>(() => protector.Unprotect(tampered));
    }

    [Fact]
    public void Garbage_token_throws_CryptographicException()
    {
        var protector = CreateProtector();
        Assert.Throws<CryptographicException>(() => protector.Unprotect("not-a-real-token"));
    }

    [Fact]
    public void Expired_token_throws_CryptographicException()
    {
        var protector = CreateProtector();
        // Absolute expiration in the past -> already expired.
        var token = protector.Protect(
            Guid.NewGuid().ToString("N"),
            expiration: DateTimeOffset.UtcNow.AddSeconds(-1));

        Assert.Throws<CryptographicException>(() => protector.Unprotect(token));
    }

    [Fact]
    public void Unprotected_non_guid_payload_throws_FormatException_on_parse()
    {
        var protector = CreateProtector();
        // A validly-protected token whose payload is NOT a GUID — mirrors the
        // middleware's FormatException branch (-> "invalid" / 400).
        var token = protector.Protect("not-a-guid", TimeSpan.FromDays(7));
        var payload = protector.Unprotect(token);

        Assert.Throws<FormatException>(() => Guid.ParseExact(payload, "N"));
    }
}
```

- [ ] **Step 3: Run the token-contract tests**

Run:
```powershell
dotnet test "c:\src\Esatto.Packages\Esatto.Umbraco.Backoffice.SharedPreviewLink.Tests\Esatto.Umbraco.Backoffice.SharedPreviewLink.Tests.csproj" --filter "FullyQualifiedName~TokenContractTests"
```
Expected: `Passed!  - Failed: 0, Passed: 5`. If any fail, investigate the contract before continuing.

---

## Task 6: Badge-suppression middleware tests

**Files:**
- Create: `c:\src\Esatto.Packages\Esatto.Umbraco.Backoffice.SharedPreviewLink.Tests\PreviewBadgeSuppressionMiddlewareTests.cs`

- [ ] **Step 1: Write the tests**

Create `PreviewBadgeSuppressionMiddlewareTests.cs`:
```csharp
using System.Text;
using Microsoft.AspNetCore.Http;
using Esatto.Umbraco.Backoffice.SharedPreviewLink;
using Xunit;

namespace Esatto.Umbraco.Backoffice.SharedPreviewLink.Tests;

public class PreviewBadgeSuppressionMiddlewareTests
{
    private const string HideMarker = "umb-website-preview{display:none!important}";

    // Builds a context with a controllable response buffer and an optional
    // marker cookie, runs the middleware with a `next` that writes `body`
    // as `contentType`, and returns the final bytes written to the original body.
    private static async Task<string> RunAsync(string body, string contentType, bool withMarkerCookie)
    {
        var context = new DefaultHttpContext();
        var finalBody = new MemoryStream();
        context.Response.Body = finalBody;
        if (withMarkerCookie)
        {
            context.Request.Headers["Cookie"] = $"{PreviewLinkMiddleware.MarkerCookieName}=1";
        }

        var middleware = new PreviewBadgeSuppressionMiddleware(async ctx =>
        {
            ctx.Response.ContentType = contentType;
            var bytes = Encoding.UTF8.GetBytes(body);
            await ctx.Response.Body.WriteAsync(bytes);
        });

        await middleware.InvokeAsync(context);

        return Encoding.UTF8.GetString(finalBody.ToArray());
    }

    [Fact]
    public async Task Injects_hide_style_before_closing_body_when_marker_present_and_html()
    {
        var html = "<html><head></head><body><p>hi</p></body></html>";
        var result = await RunAsync(html, "text/html; charset=utf-8", withMarkerCookie: true);

        Assert.Contains(HideMarker, result);
        // Injected before </body>, not after.
        Assert.True(result.IndexOf(HideMarker, StringComparison.Ordinal)
            < result.LastIndexOf("</body>", StringComparison.OrdinalIgnoreCase));
    }

    [Fact]
    public async Task Appends_hide_style_when_html_has_no_closing_body()
    {
        var html = "<div>fragment with no body tag</div>";
        var result = await RunAsync(html, "text/html", withMarkerCookie: true);

        Assert.Contains(HideMarker, result);
        Assert.StartsWith(html, result); // original preserved, style appended after
    }

    [Fact]
    public async Task Leaves_non_html_response_untouched_even_with_marker()
    {
        var json = "{\"hello\":\"world\"}";
        var result = await RunAsync(json, "application/json", withMarkerCookie: true);

        Assert.Equal(json, result);
        Assert.DoesNotContain(HideMarker, result);
    }

    [Fact]
    public async Task Passes_through_untouched_when_marker_cookie_absent()
    {
        var html = "<html><body>hi</body></html>";
        var result = await RunAsync(html, "text/html", withMarkerCookie: false);

        Assert.Equal(html, result);
        Assert.DoesNotContain(HideMarker, result);
    }
}
```

- [ ] **Step 2: Run the tests**

Run:
```powershell
dotnet test "c:\src\Esatto.Packages\Esatto.Umbraco.Backoffice.SharedPreviewLink.Tests\Esatto.Umbraco.Backoffice.SharedPreviewLink.Tests.csproj" --filter "FullyQualifiedName~PreviewBadgeSuppressionMiddlewareTests"
```
Expected: `Passed!  - Failed: 0, Passed: 4`.

---

## Task 7: PreviewLinkMiddleware token-handling tests

**Files:**
- Create: `c:\src\Esatto.Packages\Esatto.Umbraco.Backoffice.SharedPreviewLink.Tests\PreviewLinkMiddlewareTests.cs`

- [ ] **Step 1: Write the tests**

Create `PreviewLinkMiddlewareTests.cs`:
```csharp
using Microsoft.AspNetCore.DataProtection;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Logging.Abstractions;
using NSubstitute;
using Umbraco.Cms.Core.Routing;
using Umbraco.Cms.Core.Services.Navigation;
using Umbraco.Cms.Core.Web;
using Umbraco.Cms.Core.Preview;
using Esatto.Umbraco.Backoffice.SharedPreviewLink;
using Xunit;

namespace Esatto.Umbraco.Backoffice.SharedPreviewLink.Tests;

public class PreviewLinkMiddlewareTests
{
    // The Umbraco services are method-injected into InvokeAsync but are only
    // reached AFTER token validation, so on the no-token / bad-token paths they
    // are never called — unconfigured NSubstitute fakes are fine.
    private static (PreviewLinkMiddleware mw, RequestDelegateProbe next, ITimeLimitedDataProtector protector)
        CreateSut()
    {
        var provider = new EphemeralDataProtectionProvider();
        var protector = provider.CreateProtector(PreviewLinkController.ProtectorPurpose).ToTimeLimitedDataProtector();
        var next = new RequestDelegateProbe();
        var mw = new PreviewLinkMiddleware(next.Invoke, provider, NullLogger<PreviewLinkMiddleware>.Instance);
        return (mw, next, protector);
    }

    private static Task InvokeAsync(PreviewLinkMiddleware mw, HttpContext ctx) =>
        mw.InvokeAsync(
            ctx,
            Substitute.For<IUmbracoContextFactory>(),
            Substitute.For<IPublishedUrlProvider>(),
            Substitute.For<IPreviewTokenGenerator>(),
            Substitute.For<IDocumentNavigationQueryService>());

    [Fact]
    public async Task No_preview_token_calls_next_and_does_nothing()
    {
        var (mw, next, _) = CreateSut();
        var ctx = new DefaultHttpContext();
        ctx.Request.Path = "/some/page";

        await InvokeAsync(mw, ctx);

        Assert.True(next.WasCalled);
        Assert.False(ctx.Items.ContainsKey(PreviewLinkMiddleware.ErrorItemsKey));
    }

    [Fact]
    public async Task Garbage_token_renders_expired_410()
    {
        var (mw, next, _) = CreateSut();
        var ctx = new DefaultHttpContext();
        ctx.Request.Path = "/some/page";
        ctx.Request.QueryString = QueryString.Create(PreviewLinkMiddleware.QueryParam, "garbage-token");

        await InvokeAsync(mw, ctx);

        Assert.Equal("expired", ctx.Items[PreviewLinkMiddleware.ErrorItemsKey]);
        Assert.Equal(StatusCodes.Status410Gone, ctx.Response.StatusCode);
        Assert.Equal("/preview-link-error", ctx.Request.Path);
        Assert.True(next.WasCalled); // RenderErrorAsync delegates to next to render the view
    }

    [Fact]
    public async Task Non_guid_payload_renders_invalid_400()
    {
        var (mw, next, protector) = CreateSut();
        var token = protector.Protect("not-a-guid", TimeSpan.FromDays(7));
        var ctx = new DefaultHttpContext();
        ctx.Request.Path = "/some/page";
        ctx.Request.QueryString = QueryString.Create(PreviewLinkMiddleware.QueryParam, token);

        await InvokeAsync(mw, ctx);

        Assert.Equal("invalid", ctx.Items[PreviewLinkMiddleware.ErrorItemsKey]);
        Assert.Equal(StatusCodes.Status400BadRequest, ctx.Response.StatusCode);
        Assert.Equal("/preview-link-error", ctx.Request.Path);
    }
}

// Minimal RequestDelegate that records whether it ran.
internal sealed class RequestDelegateProbe
{
    public bool WasCalled { get; private set; }
    public Task Invoke(HttpContext context)
    {
        WasCalled = true;
        return Task.CompletedTask;
    }
}
```

- [ ] **Step 2: Run the tests**

Run:
```powershell
dotnet test "c:\src\Esatto.Packages\Esatto.Umbraco.Backoffice.SharedPreviewLink.Tests\Esatto.Umbraco.Backoffice.SharedPreviewLink.Tests.csproj" --filter "FullyQualifiedName~PreviewLinkMiddlewareTests"
```
Expected: `Passed!  - Failed: 0, Passed: 3`.

> If the actual Umbraco namespaces for `IUmbracoContextFactory`, `IPreviewTokenGenerator`, `IPublishedUrlProvider`, or `IDocumentNavigationQueryService` differ from the `using`s above, correct the `using` lines to match those used in `src\PreviewLinkMiddleware.cs` (they are the same interfaces).

---

## Task 8 (STRETCH — drop if mock surface is disproportionate): path-binding rejection test

**Files:**
- Create (maybe): `c:\src\Esatto.Packages\Esatto.Umbraco.Backoffice.SharedPreviewLink.Tests\PreviewLinkPathBindingTests.cs`

This proves a token minted for page A is rejected (404) when replayed on page B's path. It requires faking the full content-resolution chain (`IUmbracoContextFactory.EnsureUmbracoContext()` → `UmbracoContextReference` → `UmbracoContext.Content.GetById(preview:true, key)`) plus `IDocumentNavigationQueryService.TryGetAncestorsKeys`.

- [ ] **Step 1: Spike the mock setup**

Attempt to construct an `UmbracoContextReference` whose `.UmbracoContext.Content` is a substitutable `IPublishedContentCache` returning a fake `IPublishedContent` with a known `UrlSegment`, and a navigation substitute returning ancestor keys. Use `NSubstitute`.

- [ ] **Step 2: Decision gate**

If `EnsureUmbracoContext()`/`UmbracoContextReference` cannot be cleanly faked (non-virtual members, sealed types, or a constructor that drags in real Umbraco services), **delete the file** and record in the task notes: *"Path-binding test skipped — `UmbracoContextReference` not unit-mockable without an integration host; covered by manual backoffice verification instead."* Do **not** ship a brittle or half-working test.

- [ ] **Step 3: If it works, run it**

Run:
```powershell
dotnet test "c:\src\Esatto.Packages\Esatto.Umbraco.Backoffice.SharedPreviewLink.Tests\Esatto.Umbraco.Backoffice.SharedPreviewLink.Tests.csproj" --filter "FullyQualifiedName~PreviewLinkPathBindingTests"
```
Expected: `Passed!`.

---

## Task 9: Full package test run

- [ ] **Step 1: Run the entire test project**

Run:
```powershell
dotnet test "c:\src\Esatto.Packages\Esatto.Umbraco.Backoffice.SharedPreviewLink.Tests\Esatto.Umbraco.Backoffice.SharedPreviewLink.Tests.csproj"
```
Expected: `Passed!  - Failed: 0` with 12 tests (or 13 if the stretch test was kept). Report the exact counts.

---

## Task 10: Rewire the `AI.Woowoo` consumer

**Files:**
- Modify: `c:\src\AI.Woowoo\src\AI.Woowoo\Directory.Packages.props:18`
- Modify: `c:\src\AI.Woowoo\src\AI.Woowoo\AI.Woowoo.csproj:34`
- Modify: `c:\src\AI.Woowoo\src\AI.Woowoo.TestSite\Program.cs:1`
- Modify: `c:\src\AI.Woowoo\nuget.config`

- [ ] **Step 1: Central package version**

In `Directory.Packages.props`, change:
```xml
    <PackageVersion Include="Backoffice.PreviewLink" Version="1.0.0" />
```
to:
```xml
    <PackageVersion Include="Esatto.Umbraco.Backoffice.SharedPreviewLink" Version="1.0.0" />
```

- [ ] **Step 2: Package reference**

In `AI.Woowoo.csproj`, change:
```xml
    <PackageReference Include="Backoffice.PreviewLink" />
```
to:
```xml
    <PackageReference Include="Esatto.Umbraco.Backoffice.SharedPreviewLink" />
```

- [ ] **Step 3: `using` in the test site**

In `AI.Woowoo.TestSite\Program.cs`, change the first line:
```csharp
using Backoffice.PreviewLink;
```
to:
```csharp
using Esatto.Umbraco.Backoffice.SharedPreviewLink;
```
(Leave `app.UsePreviewLink();` on line 18 unchanged — the method name is intentionally kept.)

- [ ] **Step 4: NuGet source mapping (keep it private)**

In `nuget.config`, replace the `packageSourceMapping` `esatto-packages` block:
```xml
    <!-- Private Esatto packages come only from the Azure DevOps feed.
         The public Esatto.Umbraco.Backoffice.* packages (ContentTreeDragAndDrop,
         CustomEditors, DictionaryFilterValues) resolve from nuget.org via the
         '*' rule above; only the private Backoffice.* packages are mapped here. -->
    <packageSource key="esatto-packages">
      <package pattern="Backoffice.*" />
    </packageSource>
```
with:
```xml
    <!-- Private Esatto packages come only from the Azure DevOps feed.
         The public Esatto.Umbraco.Backoffice.* packages (ContentTreeDragAndDrop,
         CustomEditors, DictionaryFilterValues) resolve from nuget.org via the
         '*' rule above. The private Backoffice.* packages and the private
         (renamed) Esatto.Umbraco.Backoffice.SharedPreviewLink are mapped here.
         An exact-id pattern beats '*', so SharedPreviewLink resolves from this
         feed while its public siblings keep resolving from nuget.org. -->
    <packageSource key="esatto-packages">
      <package pattern="Backoffice.*" />
      <package pattern="Esatto.Umbraco.Backoffice.SharedPreviewLink" />
    </packageSource>
```

- [ ] **Step 5: Verify the edits parse and no old references remain**

Run:
```powershell
Select-String -Path "c:\src\AI.Woowoo\src\AI.Woowoo\Directory.Packages.props","c:\src\AI.Woowoo\src\AI.Woowoo\AI.Woowoo.csproj","c:\src\AI.Woowoo\src\AI.Woowoo.TestSite\Program.cs" -Pattern "Backoffice\.PreviewLink"
```
Expected: **no matches**.

> NOTE: `dotnet restore`/`build` of `AI.Woowoo` will FAIL until the renamed `Esatto.Umbraco.Backoffice.SharedPreviewLink.1.0.0` nupkg is pushed to the `esatto-packages` feed (a separate, approval-gated step). This is expected — do not treat that restore failure as a defect in this task. The edits are verified by the grep above plus XML well-formedness.

---

## Task 11: Final verification summary + commit (APPROVAL-GATED)

- [ ] **Step 1: Re-run the package build + full test suite and capture output**

```powershell
dotnet build "c:\src\Esatto.Packages\Esatto.Umbraco.Backoffice.SharedPreviewLink\Esatto.Umbraco.Backoffice.SharedPreviewLink.csproj" -c Release
dotnet test  "c:\src\Esatto.Packages\Esatto.Umbraco.Backoffice.SharedPreviewLink.Tests\Esatto.Umbraco.Backoffice.SharedPreviewLink.Tests.csproj"
```
Expected: build succeeded; all tests pass. Report the real output to the user.

- [ ] **Step 2: Show the user the staged/changed file list in both repos**

```powershell
git -C "c:\src\Esatto.Packages" status
git -C "c:\src\AI.Woowoo" status
```

- [ ] **Step 3: Commit — ONLY after the user explicitly approves**

Do not run these until the user says go. Two separate commits (one per repo):
```powershell
git -C "c:\src\Esatto.Packages" add -A
git -C "c:\src\Esatto.Packages" commit -m @'
Rename Backoffice.PreviewLink -> Esatto.Umbraco.Backoffice.SharedPreviewLink

Rename package id/assembly/namespace + App_Plugins folder and manifest,
add xUnit test project (token contract, badge-suppression, middleware),
add package icon and README screenshots, and two small cleanups
(derive expiry text from expiresAt; declare 500 on the mint endpoint).

Co-Authored-By: Claude Opus 4.8 (1M context) <noreply@anthropic.com>
'@

git -C "c:\src\AI.Woowoo" add -A
git -C "c:\src\AI.Woowoo" commit -m @'
Reference renamed package Esatto.Umbraco.Backoffice.SharedPreviewLink

Update central package version, package reference, the TestSite using, and
the nuget.config source mapping (keep the package private on esatto-packages).

Co-Authored-By: Claude Opus 4.8 (1M context) <noreply@anthropic.com>
'@
```

- [ ] **Step 4: (Separate approval) tag → pack 1.0.0 → push to the feed**

Versioning is MinVer-driven (see `Directory.Build.props`): the version comes from a
git tag `<PackageId>-<version>`. An untagged pack produces `0.0.0-preview.0.N`. To
publish the `1.0.0` that `AI.Woowoo` pins, the release flow is (all approval-gated):
```powershell
# 1. Tag (mirrors the sibling convention, e.g. Esatto.Umbraco.Backoffice.ContentTreeDragAndDrop-1.0.0)
git -C "c:\src\Esatto.Packages" tag Esatto.Umbraco.Backoffice.SharedPreviewLink-1.0.0
git -C "c:\src\Esatto.Packages" push origin Esatto.Umbraco.Backoffice.SharedPreviewLink-1.0.0
# 2. Pack (now produces 1.0.0) and push to the private feed
dotnet pack "c:\src\Esatto.Packages\Esatto.Umbraco.Backoffice.SharedPreviewLink\Esatto.Umbraco.Backoffice.SharedPreviewLink.csproj" -c Release
dotnet nuget push "c:\src\Esatto.Packages\Esatto.Umbraco.Backoffice.SharedPreviewLink\bin\Release\Esatto.Umbraco.Backoffice.SharedPreviewLink.1.0.0.nupkg" --source esatto-packages --api-key az --skip-duplicate
```
(The package's `AutoPushAfterPack` target also auto-pushes on `dotnet pack` unless `-p:AutoPushToFeed=false`.)
Then `dotnet restore` / build `AI.Woowoo` to confirm end-to-end resolution.

---

## Self-review notes (author)

- **Spec coverage:** rename (Tasks 1–2) ✓; consumer rewiring incl. nuget.config (Task 10) ✓; targeted cleanup — JS expiry + 500 response type (Task 3) ✓; xUnit tests — token/badge/middleware + stretch path-binding (Tasks 5–8) ✓; icon + screenshots + README (Task 4) ✓; verification + no-push/no-commit boundaries (Tasks 4, 10, 11) ✓.
- **Error-mapping correction:** Tasks 7 reflects the *actual* code — `CryptographicException` → expired/410, `FormatException` → invalid/400 (the spec was corrected to match).
- **Type consistency:** test code references only public members — `PreviewLinkController.ProtectorPurpose`, `PreviewLinkMiddleware.{QueryParam,ErrorItemsKey,MarkerCookieName}`, `PreviewBadgeSuppressionMiddleware` ctor `(RequestDelegate)` and `InvokeAsync(HttpContext)`, `PreviewLinkMiddleware` ctor `(RequestDelegate, IDataProtectionProvider, ILogger<PreviewLinkMiddleware>)` and `InvokeAsync(HttpContext, IUmbracoContextFactory, IPublishedUrlProvider, IPreviewTokenGenerator, IDocumentNavigationQueryService)` — all verified against the source.
- **No `InternalsVisibleTo`** needed — every assertion targets the public surface.
