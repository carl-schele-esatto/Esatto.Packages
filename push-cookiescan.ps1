<#
.SYNOPSIS
    Tags, packs and pushes the four Esatto.CookieScan packages as one release.

.DESCRIPTION
    The scanner is one tool split along a dependency boundary, not four products, so the four
    packages are released together at one version. This script does the whole set in dependency
    order and delegates each pack+push to push-nuget.ps1, so the API-key check, the prerelease
    warning and --skip-duplicate all behave exactly as they do for every other package here.

        Esatto.CookieScan.Core                  the rules (no dependencies)
        Esatto.CookieScan.Engine                -> Core
        Esatto.CookieScan.Cli                   -> Engine        (a dotnet tool)
        Esatto.Umbraco.Backoffice.CookieScan    -> Core          (+ CookieBanner)

    Esatto.CookieScan.Desktop is deliberately NOT here: it is IsPackable=false and ships as a
    self-contained exe. See "PUBLISHING THE DESKTOP EXE" at the bottom of this file.

    Versions come from MinVer, which reads a git tag named "<PackageId>-<version>". This script
    creates those four tags for you when they are missing, verifies any that already exist point at
    HEAD, and then confirms the packed filenames really carry -Version before pushing anything.

.PARAMETER Version
    The version to release all four at, e.g. 1.0.0. Required - there is no default, because
    guessing a version is the one mistake a feed will not let you take back.

.PARAMETER Source
    NuGet source. Defaults to nuget.org. For the internal Azure feed pass the feed name
    (e.g. -Source esatto-packages).

.PARAMETER ApiKey
    Defaults to $env:NUGET_API_KEY. Not needed for a credential-provider-backed feed.

.PARAMETER PackOnly
    Tag and pack, push nothing. Use this first - it is the cheap way to see the exact four
    filenames a real run would push.

.PARAMETER PushTags
    Also `git push` the four tags to origin. Off by default: the tags are what MinVer reads, so
    creating them locally is enough to produce the packages, and pushing them is a separate
    decision about the remote.

.PARAMETER AllowDirty
    Permit a dirty working tree. Off by default, because a package stamped 1.0.0 that contains
    uncommitted code is a version number that lies about what is inside it.

.PARAMETER Force
    Skip the single confirmation prompt.

.EXAMPLE
    # Look before you leap: tags, packs, pushes nothing.
    ./push-cookiescan.ps1 -Version 1.0.0 -PackOnly

.EXAMPLE
    $env:NUGET_API_KEY = "oy2..."; ./push-cookiescan.ps1 -Version 1.0.0

.EXAMPLE
    # The private Azure feed instead of nuget.org.
    ./push-cookiescan.ps1 -Version 1.0.0 -Source esatto-packages
#>
[CmdletBinding()]
param(
    [Parameter(Mandatory = $true)]
    [string]$Version,
    [string]$Source = "https://api.nuget.org/v3/index.json",
    [string]$ApiKey = $env:NUGET_API_KEY,
    [string]$Output = "$PSScriptRoot\artifacts",
    [switch]$PackOnly,
    [switch]$PushTags,
    [switch]$AllowDirty,
    [switch]$Force
)

$ErrorActionPreference = "Stop"

# Dependency order, and the order they are pushed in. Core first is not cosmetic: nuget.org takes a
# few minutes to index, and a consumer who restores Engine before Core is visible gets a restore
# failure naming a package that "does not exist".
$packages = @(
    "Esatto.CookieScan.Core",
    "Esatto.CookieScan.Engine",
    "Esatto.CookieScan.Cli",
    "Esatto.Umbraco.Backoffice.CookieScan"
)

# SemVer 2.0, loosely: MAJOR.MINOR.PATCH with an optional prerelease label. Checked because the
# whole run is driven off this string - it becomes four tag names and four filenames.
if ($Version -notmatch '^\d+\.\d+\.\d+(-[0-9A-Za-z.-]+)?$') {
    throw "Version '$Version' does not look like a version. Expected e.g. 1.0.0 or 1.1.0-preview.1."
}

Push-Location $PSScriptRoot
try {
    # ---------------------------------------------------------------- preflight

    # Test-Path rather than `git rev-parse --is-inside-work-tree`: under Windows PowerShell 5.1,
    # redirecting a native command's stderr raises a NativeCommandError, which $ErrorActionPreference
    # = "Stop" turns into a termination with git's message instead of this one. Every git call below
    # is therefore either silent on stderr or left unredirected on purpose.
    if (-not (Test-Path (Join-Path $PSScriptRoot ".git"))) {
        throw "$PSScriptRoot is not a git repository. MinVer needs one to read a version from."
    }

    $dirty = git status --porcelain
    if ($dirty -and -not $AllowDirty) {
        Write-Host "Working tree is not clean:" -ForegroundColor Yellow
        $dirty | ForEach-Object { Write-Host "  $_" }
        throw "Commit or stash first, or pass -AllowDirty. A package stamped $Version must contain committed code."
    }

    $head = (git rev-parse HEAD).Trim()
    $branch = (git rev-parse --abbrev-ref HEAD).Trim()

    Write-Host ""
    Write-Host "Releasing the Esatto.CookieScan set at $Version" -ForegroundColor Cyan
    Write-Host "  commit : $head"
    Write-Host "  branch : $branch"
    Write-Host "  source : $Source"
    Write-Host ""

    # nuget.org will not accept an anonymous push, and finding that out after three successful
    # pushes leaves the set half-released. Checked here, before the first one.
    $isNugetOrg = $Source -like "*nuget.org*"
    if ($isNugetOrg -and -not $PackOnly -and [string]::IsNullOrWhiteSpace($ApiKey)) {
        throw "No API key. Set `$env:NUGET_API_KEY or pass -ApiKey (required for nuget.org)."
    }

    foreach ($id in $packages) {
        $csproj = Join-Path $PSScriptRoot "$id\$id.csproj"
        if (-not (Test-Path $csproj)) { throw "Project not found: $csproj" }
    }

    # ---------------------------------------------------------------- tags

    Write-Host "Tags (MinVer reads these to decide the version):" -ForegroundColor Cyan

    # Two passes, deliberately: every tag is checked before any tag is created. A single pass that
    # created as it went would leave one or two of the four tagged when it hit the third one's
    # conflict, so the failure it reports would also be a mess it made - and re-running after
    # picking a different version would leave those strays behind pointing at nothing released.
    $toCreate = @()

    foreach ($id in $packages) {
        $tag = "$id-$Version"

        # `git tag --list` writes nothing to stderr and exits 0 whether or not the tag is there,
        # so it needs no redirection and no exit-code interpretation - see the note above.
        if (git tag --list $tag) {
            # Already tagged. Fine if it is this commit; a hard stop if not, because packing would
            # then silently produce a version built from code the tag does not point at.
            $at = (git rev-list -n 1 $tag).Trim()

            if ($at -ne $head) {
                throw "Tag $tag already exists at $at, which is not HEAD ($head). Delete it (git tag -d $tag) or pick another version. No tags were created."
            }

            Write-Host "  = $tag (already at HEAD)" -ForegroundColor DarkGray
        }
        else {
            $toCreate += $tag
        }
    }

    foreach ($tag in $toCreate) {
        git tag $tag
        if ($LASTEXITCODE -ne 0) { throw "Could not create tag $tag." }
        Write-Host "  + $tag" -ForegroundColor Green
    }

    Write-Host ""

    # ---------------------------------------------------------------- pack

    New-Item -ItemType Directory -Force -Path $Output | Out-Null

    $built = @()

    foreach ($id in $packages) {
        Write-Host "Packing $id..." -ForegroundColor Cyan

        dotnet pack (Join-Path $PSScriptRoot "$id\$id.csproj") -c Release -o $Output -p:AutoPushToFeed=false
        if ($LASTEXITCODE -ne 0) { throw "dotnet pack failed for $id (exit $LASTEXITCODE)." }

        # The tag should have produced exactly this file. Asserted rather than assumed: if MinVer
        # read a different tag - a stale one, a prefix typo - this is where it shows, before a
        # wrong version reaches a feed that will never let it be replaced.
        $expected = Join-Path $Output "$id.$Version.nupkg"
        if (-not (Test-Path $expected)) {
            $actual = Get-ChildItem $Output -Filter "$id.*.nupkg" |
                Sort-Object LastWriteTime -Descending |
                Select-Object -First 1 -ExpandProperty Name
            throw "Expected $id.$Version.nupkg but pack produced '$actual'. MinVer did not read the tag - check for a stale tag or a MinVerTagPrefix change."
        }

        $built += $expected
        Write-Host "  -> $id.$Version.nupkg" -ForegroundColor Green
    }

    Write-Host ""

    # ---------------------------------------------------------------- push

    if ($PackOnly) {
        Write-Host "-PackOnly: nothing was pushed. Four packages are in $Output :" -ForegroundColor Yellow
        $built | ForEach-Object { Write-Host "  $(Split-Path $_ -Leaf)" }
        Write-Host ""
        Write-Host "Re-run without -PackOnly to push them." -ForegroundColor Yellow
        return
    }

    if ($Version -match '-') {
        Write-Host "WARNING: $Version is a PRERELEASE version." -ForegroundColor Yellow
    }

    Write-Host "About to push FOUR packages to $Source :" -ForegroundColor Yellow
    $built | ForEach-Object { Write-Host "  $(Split-Path $_ -Leaf)" }
    Write-Host ""
    Write-Host "Feed versions are immutable. A wrong version cannot be replaced, only superseded." -ForegroundColor Yellow
    Write-Host ""

    if (-not $Force) {
        $answer = Read-Host "Push all four? Type 'yes' to continue"
        if ($answer -ne "yes") {
            Write-Host "Aborted. Nothing was pushed. The tags are still here - delete them with:" -ForegroundColor Yellow
            $packages | ForEach-Object { Write-Host "  git tag -d $_-$Version" }
            return
        }
    }

    # Delegated to push-nuget.ps1 one at a time, rather than a dotnet nuget push loop here, so
    # there is one push path in this repository instead of two that can drift. -Force because the
    # single confirmation above already covers all four.
    $pushed = @()

    foreach ($id in $packages) {
        Write-Host ""
        Write-Host "=== $id" -ForegroundColor Cyan

        & (Join-Path $PSScriptRoot "push-nuget.ps1") `
            -Project (Join-Path $PSScriptRoot "$id\$id.csproj") `
            -Source $Source `
            -ApiKey $ApiKey `
            -Output $Output `
            -Force

        if ($LASTEXITCODE -ne 0 -or -not $?) {
            Write-Host ""
            Write-Host "FAILED on $id. Already pushed: $(if ($pushed) { $pushed -join ', ' } else { 'none' })" -ForegroundColor Red
            Write-Host "Those are live and immutable. Fix the cause and re-run - --skip-duplicate makes the successful ones no-ops." -ForegroundColor Red
            throw "Push failed for $id."
        }

        $pushed += $id
    }

    # ---------------------------------------------------------------- after

    Write-Host ""
    Write-Host "All four pushed at $Version." -ForegroundColor Green

    if ($PushTags) {
        Write-Host ""
        Write-Host "Pushing tags to origin..." -ForegroundColor Cyan

        foreach ($id in $packages) {
            git push origin "$id-$Version"
            if ($LASTEXITCODE -ne 0) { throw "Could not push tag $id-$Version." }
        }

        Write-Host "Tags pushed." -ForegroundColor Green
    }
    else {
        Write-Host ""
        Write-Host "The four tags are local only. To publish them:" -ForegroundColor Yellow
        Write-Host "  git push origin $($packages[0])-$Version $($packages[1])-$Version $($packages[2])-$Version $($packages[3])-$Version"
    }

    Write-Host ""
    Write-Host "nuget.org takes a few minutes to index. Until it has, a restore can fail naming a" -ForegroundColor DarkGray
    Write-Host "package that was just pushed - that is the index, not the push." -ForegroundColor DarkGray
    Write-Host ""
    Write-Host "Then, in c:\src\NDSTK:" -ForegroundColor Cyan
    Write-Host "  1. Set Esatto.Umbraco.Backoffice.CookieScan to $Version in NDSTK.csproj"
    Write-Host "  2. Delete nuget.config (it only exists to point at .local-feed)"
    Write-Host "  3. dotnet restore"
}
finally {
    Pop-Location
}

<#
    PUBLISHING THE DESKTOP EXE

    Not a package, so not part of this script:

        dotnet publish Esatto.CookieScan.Desktop -c Release -r win-x64 --self-contained `
            -p:PublishSingleFile=true -p:IncludeNativeLibrariesForSelfExtract=true `
            -p:IncludeAllContentForSelfExtract=true -p:EnableCompressionInSingleFile=true -o dist

    Produces dist\esatto-cookiescan-ui.exe, roughly 86MB. dist\ is gitignored.

    The console tool publishes the same way from Esatto.CookieScan.Cli, for anyone who wants a
    copy-anywhere exe instead of `dotnet tool install -g Esatto.CookieScan.Cli`.
#>
