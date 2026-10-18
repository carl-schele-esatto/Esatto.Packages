<#
.SYNOPSIS
    Tags, packs and pushes the Esatto.CookieScan packages as one release.

.DESCRIPTION
    The scanner is one tool split along a dependency boundary, not four products, so by default the
    four packages are released together at one version. This script does the set in dependency order
    and delegates each pack+push to push-nuget.ps1, so the API-key check, the prerelease warning and
    --skip-duplicate all behave exactly as they do for every other package here.

        Esatto.CookieScan.Core                  the rules (no dependencies)
        Esatto.CookieScan.Engine                -> Core
        Esatto.CookieScan.Cli                   -> Engine        (a dotnet tool)
        Esatto.Umbraco.Backoffice.CookieScan    -> Core          (+ CookieBanner)

    Esatto.CookieScan.Desktop is deliberately NOT here: it is IsPackable=false and ships as a
    self-contained exe. See "PUBLISHING THE DESKTOP EXE" at the bottom of this file.

    Versions come from MinVer, which reads a git tag named "<PackageId>-<version>". This script
    creates those tags for you when they are missing, verifies any that already exist point at HEAD,
    and then confirms the packed filenames really carry -Version before pushing anything.

    THE RULE THIS SCRIPT EXISTS TO ENFORCE. A package's nuspec dependency on another package in this
    repo is whatever MinVer computes for THAT project at the current commit. So releasing Engine at
    1.1.0 while Core's newest tag is an older commit makes Core compute a prerelease - 1.0.1-preview
    .0.2 - and the Engine package then declares a dependency on a version that will never exist on
    the feed. NuGet warns NU5104 and the package is unpublishable in practice.

    Hence -Packages expands to its dependency CLOSURE: ask for Engine and you get Core as well, at
    the same version, tagged at the same commit. There is no combination of flags that releases a
    package without releasing what it depends on, because there is no correct one. After packing,
    every produced nuspec is read back and its in-repo dependencies are checked to be stable and to
    name exactly -Version; that assertion is the thing that catches a stale tag before a feed does.

.PARAMETER Version
    The version to release at, e.g. 1.1.0. Required - there is no default, because guessing a
    version is the one mistake a feed will not let you take back.

.PARAMETER Packages
    Which packages to release. Defaults to all four (lockstep, the documented convention). Pass a
    subset to release only what changed; the dependency closure is added automatically, so
    "-Packages Esatto.CookieScan.Engine" releases Core and Engine.

.PARAMETER Source
    NuGet source. Defaults to nuget.org. For the internal Azure feed pass the feed name
    (e.g. -Source esatto-packages).

.PARAMETER ApiKey
    Defaults to $env:NUGET_API_KEY. Not needed for a credential-provider-backed feed.

.PARAMETER PackOnly
    Tag and pack, push nothing. Use this first - it is the cheap way to see the exact filenames a
    real run would push, and it still runs the dependency assertion.

.PARAMETER PushTags
    Also `git push` the tags to origin. Off by default: the tags are what MinVer reads, so creating
    them locally is enough to produce the packages, and pushing them is a separate decision about
    the remote.

.PARAMETER AllowDirty
    Permit a dirty working tree. Off by default, because a package stamped 1.1.0 that contains
    uncommitted code is a version number that lies about what is inside it.

.PARAMETER Force
    Skip the single confirmation prompt.

.EXAMPLE
    # Look before you leap: tags, packs, asserts, pushes nothing.
    ./push-cookiescan.ps1 -Version 1.1.0 -PackOnly

.EXAMPLE
    # Release only what the email work changed. Core comes along because Engine depends on it.
    $env:NUGET_API_KEY = "oy2..."
    ./push-cookiescan.ps1 -Version 1.1.0 -Packages Esatto.CookieScan.Engine

.EXAMPLE
    # The whole set, lockstep.
    ./push-cookiescan.ps1 -Version 1.1.0

.EXAMPLE
    # The private Azure feed instead of nuget.org.
    ./push-cookiescan.ps1 -Version 1.1.0 -Source esatto-packages
#>
[CmdletBinding()]
param(
    [Parameter(Mandatory = $true)]
    [string]$Version,
    [string[]]$Packages,
    [string]$Source = "https://api.nuget.org/v3/index.json",
    [string]$ApiKey = $env:NUGET_API_KEY,
    [string]$Output = "$PSScriptRoot\artifacts",
    [switch]$PackOnly,
    [switch]$PushTags,
    [switch]$AllowDirty,
    [switch]$Force
)

$ErrorActionPreference = "Stop"

# The in-repo dependency graph, and the order everything is packed and pushed in. Core first is not
# cosmetic: nuget.org takes a few minutes to index, and a consumer who restores Engine before Core
# is visible gets a restore failure naming a package that "does not exist".
#
# Only dependencies WITHIN this repo are listed. Umbraco.Cms, Playwright and CookieBanner are
# ordinary PackageReferences at pinned versions - nothing here can change what they resolve to.
$dependsOn = [ordered]@{
    "Esatto.CookieScan.Core"               = @()
    "Esatto.CookieScan.Engine"             = @("Esatto.CookieScan.Core")
    "Esatto.CookieScan.Cli"                = @("Esatto.CookieScan.Engine")
    "Esatto.Umbraco.Backoffice.CookieScan" = @("Esatto.CookieScan.Core")
}

$allIds = @($dependsOn.Keys)

# SemVer 2.0, loosely: MAJOR.MINOR.PATCH with an optional prerelease label. Checked because the
# whole run is driven off this string - it becomes tag names and filenames.
if ($Version -notmatch '^\d+\.\d+\.\d+(-[0-9A-Za-z.-]+)?$') {
    throw "Version '$Version' does not look like a version. Expected e.g. 1.1.0 or 1.1.0-preview.1."
}

$versionIsStable = ($Version -notmatch '-')

if (-not $Packages) {
    $Packages = $allIds
}

foreach ($id in $Packages) {
    if ($allIds -notcontains $id) {
        throw "Unknown package '$id'. Expected one of: $($allIds -join ', ')"
    }
}

# Every requested package, plus everything it depends on, in the canonical dependency order. See
# THE RULE THIS SCRIPT EXISTS TO ENFORCE, above: a dependency left at an older tag computes a
# prerelease and poisons the dependent's nuspec.
$wanted = @{}
$queue = New-Object System.Collections.Queue

foreach ($id in $Packages) { $queue.Enqueue($id) }

while ($queue.Count -gt 0) {
    $id = $queue.Dequeue()

    if ($wanted.ContainsKey($id)) { continue }

    $wanted[$id] = $true

    foreach ($dep in $dependsOn[$id]) { $queue.Enqueue($dep) }
}

$release = @($allIds | Where-Object { $wanted.ContainsKey($_) })
$added = @($release | Where-Object { $Packages -notcontains $_ })

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
    Write-Host "Releasing $($release.Count) of $($allIds.Count) CookieScan packages at $Version" -ForegroundColor Cyan
    Write-Host "  commit : $head"
    Write-Host "  branch : $branch"
    Write-Host "  source : $Source"
    Write-Host ""

    foreach ($id in $release) {
        if ($added -contains $id) {
            Write-Host "  $id  (added: something in this release depends on it)" -ForegroundColor DarkYellow
        }
        else {
            Write-Host "  $id" -ForegroundColor Gray
        }
    }

    $skipped = @($allIds | Where-Object { $release -notcontains $_ })

    if ($skipped) {
        Write-Host ""
        Write-Host "  NOT in this release: $($skipped -join ', ')" -ForegroundColor DarkGray
        Write-Host "  Their tags stay where they are. Do not pack them from this commit without" -ForegroundColor DarkGray
        Write-Host "  releasing them too - they would pick up a prerelease dependency." -ForegroundColor DarkGray
    }

    Write-Host ""

    # nuget.org will not accept an anonymous push, and finding that out after a successful push
    # leaves the set half-released. Checked here, before the first one.
    $isNugetOrg = $Source -like "*nuget.org*"
    if ($isNugetOrg -and -not $PackOnly -and [string]::IsNullOrWhiteSpace($ApiKey)) {
        throw "No API key. Set `$env:NUGET_API_KEY or pass -ApiKey (required for nuget.org)."
    }

    foreach ($id in $release) {
        $csproj = Join-Path $PSScriptRoot "$id\$id.csproj"
        if (-not (Test-Path $csproj)) { throw "Project not found: $csproj" }
    }

    # ---------------------------------------------------------------- tags

    Write-Host "Tags (MinVer reads these to decide the version):" -ForegroundColor Cyan

    # Two passes, deliberately: every tag is checked before any tag is created. A single pass that
    # created as it went would leave some of the set tagged when it hit a later one's conflict, so
    # the failure it reports would also be a mess it made - and re-running after picking a different
    # version would leave those strays behind pointing at nothing released.
    $toCreate = @()

    foreach ($id in $release) {
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

    foreach ($id in $release) {
        Write-Host "Packing $id..." -ForegroundColor Cyan

        # AutoPushToFeed=false is not decoration: several projects in this repo push to .local-feed
        # from an AfterTargets="Pack" target, and a release pack must not put a release version in
        # the smoke-test feed.
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

    # ---------------------------------------------------------------- assert the dependencies

    # The check the closure above is designed to make pass, verified rather than trusted. dotnet
    # pack leaves the nuspec it generated in obj\Release, so this reads what was really written into
    # the package instead of re-deriving what it ought to say.
    #
    # This is the guard for NU5104 - a stable package declaring a dependency on a prerelease - which
    # is what a dependency left at an older tag produces. NuGet only warns; a warning scrolls past
    # in a wall of pack output, and the package is then broken on the feed forever.
    Write-Host "Checking in-repo dependencies..." -ForegroundColor Cyan

    foreach ($id in $release) {
        $nuspecPath = Join-Path $PSScriptRoot "$id\obj\Release\$id.$Version.nuspec"

        if (-not (Test-Path $nuspecPath)) {
            throw "Could not find the generated nuspec at $nuspecPath. Cannot verify $id's dependencies."
        }

        [xml]$nuspec = Get-Content $nuspecPath

        # Both shapes: dependencies grouped by target framework, and the ungrouped form. Nulls are
        # filtered out rather than guarded for, because a package with no dependencies at all - Core
        # is exactly that - has neither node.
        $deps = @()
        $deps += @($nuspec.package.metadata.dependencies.group.dependency)
        $deps += @($nuspec.package.metadata.dependencies.dependency)

        foreach ($dep in ($deps | Where-Object { $_ -ne $null })) {
            if ($allIds -notcontains $dep.id) { continue }

            $depVersion = $dep.version

            if ($versionIsStable -and $depVersion -match '-') {
                throw @"
$id $Version would depend on $($dep.id) $depVersion - a PRERELEASE, which will never exist on $Source.

That means $($dep.id)'s newest tag is not at this commit, so MinVer computed a prerelease for it.
Release it too:

    ./push-cookiescan.ps1 -Version $Version -Packages $id,$($dep.id)

Nothing was pushed.
"@
            }

            if ($depVersion -notlike "*$Version*") {
                throw @"
$id $Version would depend on $($dep.id) $depVersion, which is not part of this release.

Every in-repo dependency has to move with its dependent, or the package points at something that
may not be on the feed. Add it:

    ./push-cookiescan.ps1 -Version $Version -Packages $id,$($dep.id)

Nothing was pushed.
"@
            }

            Write-Host "  $id -> $($dep.id) $depVersion" -ForegroundColor DarkGray
        }
    }

    Write-Host "  All in-repo dependencies are stable and at $Version." -ForegroundColor Green
    Write-Host ""

    # ---------------------------------------------------------------- push

    if ($PackOnly) {
        Write-Host "-PackOnly: nothing was pushed. $($built.Count) package(s) in $Output :" -ForegroundColor Yellow
        $built | ForEach-Object { Write-Host "  $(Split-Path $_ -Leaf)" }
        Write-Host ""
        Write-Host "Re-run without -PackOnly to push them." -ForegroundColor Yellow
        return
    }

    if (-not $versionIsStable) {
        Write-Host "WARNING: $Version is a PRERELEASE version." -ForegroundColor Yellow
    }

    Write-Host "About to push $($built.Count) package(s) to $Source :" -ForegroundColor Yellow
    $built | ForEach-Object { Write-Host "  $(Split-Path $_ -Leaf)" }
    Write-Host ""
    Write-Host "Feed versions are immutable. A wrong version cannot be replaced, only superseded." -ForegroundColor Yellow
    Write-Host ""

    if (-not $Force) {
        $answer = Read-Host "Push them? Type 'yes' to continue"
        if ($answer -ne "yes") {
            Write-Host "Aborted. Nothing was pushed. The tags are still here - delete them with:" -ForegroundColor Yellow
            $release | ForEach-Object { Write-Host "  git tag -d $_-$Version" }
            return
        }
    }

    # Delegated to push-nuget.ps1 one at a time, rather than a dotnet nuget push loop here, so
    # there is one push path in this repository instead of two that can drift. -Force because the
    # single confirmation above already covers the set.
    $pushed = @()

    foreach ($id in $release) {
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
    Write-Host "Pushed at $Version : $($pushed -join ', ')" -ForegroundColor Green

    if ($PushTags) {
        Write-Host ""
        Write-Host "Pushing tags to origin..." -ForegroundColor Cyan

        foreach ($id in $release) {
            git push origin "$id-$Version"
            if ($LASTEXITCODE -ne 0) { throw "Could not push tag $id-$Version." }
        }

        Write-Host "Tags pushed." -ForegroundColor Green
    }
    else {
        Write-Host ""
        Write-Host "The tags are local only. To publish them:" -ForegroundColor Yellow
        Write-Host "  git push origin $(($release | ForEach-Object { "$_-$Version" }) -join ' ')"
    }

    Write-Host ""
    Write-Host "nuget.org takes a few minutes to index. Until it has, a restore can fail naming a" -ForegroundColor DarkGray
    Write-Host "package that was just pushed - that is the index, not the push." -ForegroundColor DarkGray

    if ($pushed -contains "Esatto.Umbraco.Backoffice.CookieScan") {
        Write-Host ""
        Write-Host "Then, in c:\src\NDSTK:" -ForegroundColor Cyan
        Write-Host "  1. Set Esatto.Umbraco.Backoffice.CookieScan to $Version in NDSTK.csproj"
        Write-Host "  2. Delete nuget.config (it only exists to point at .local-feed)"
        Write-Host "  3. dotnet restore"
    }
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
