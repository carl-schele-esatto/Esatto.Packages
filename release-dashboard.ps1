<#
.SYNOPSIS
    Tags, publishes and releases the cookie scanner dashboard exe to GitHub Releases.

.DESCRIPTION
    The dashboard is the one half of the scanner that is not a NuGet package, and cannot be: a
    WinForms app cannot be a dotnet tool at all - the SDK refuses it outright with

        NETSDK1146: PackAsTool does not support TargetPlatformIdentifier being set ... PackAsTool
        also does not support UseWPF or UseWindowsForms when targeting .NET 5 and higher.

    So it ships as a GitHub release asset instead, and `esatto-cookiescan ui` - a command on the
    console tool, which IS a dotnet tool - fetches it, verifies it and opens it. That is what makes
    "install the tool, run one command" possible for a window. This script is the other end of it.

    push-cookiescan.ps1 is deliberately NOT the place for this. Its entire model is "pack a nupkg and
    push it to a feed", and this is neither a nupkg nor a feed.

    THE THREE THINGS THAT MUST AGREE. The version in the git tag, the version MinVer stamps inside
    the exe, and the folder the launcher caches it under are one string. The tag is what MinVer reads
    (MinVerTagPrefix in the desktop project), and the launcher reads the tag off the release. So this
    script tags first, publishes second, and then asserts that the exe it produced really carries
    -Version before anything is uploaded - a stale or mistyped tag shows up there rather than as a
    release nobody can update from.

.PARAMETER Version
    The version to release, e.g. 1.2.0. Required - there is no default, because a release someone
    has already downloaded cannot be taken back.

.PARAMETER BuildOnly
    Tag, publish and hash, but create no GitHub release. Use this first: it is the cheap way to see
    the exact artifacts and prove the version assertion passes.

    THE TAG IS ALWAYS PUSHED, and there is no flag to stop it. `gh release create --target <sha>`
    fails with

        HTTP 422: Validation Failed

    when GitHub has never seen that commit - which is every release cut from a branch that has not
    been pushed yet. An earlier version of this script left the push optional on the theory that
    creating the release publishes the tag as a side effect; it does, but only once GitHub can
    resolve the target, so the optional step was a precondition wearing a switch. Pushing the tag
    sends the commit and its history with it, which is what makes the target resolvable.

.PARAMETER Notes
    Release notes. Defaults to a line pointing at the launcher, which is how anyone should be
    getting this rather than by downloading the asset by hand.

.PARAMETER AllowDirty
    Permit a dirty working tree. Off by default: an exe stamped 1.2.0 that contains uncommitted code
    is a version number that lies about what is inside it, and this one gets copied between machines.

.PARAMETER Force
    Skip the confirmation prompt.

.EXAMPLE
    # Look before you leap: tags, builds, hashes, releases nothing.
    ./release-dashboard.ps1 -Version 1.2.0 -BuildOnly

.EXAMPLE
    ./release-dashboard.ps1 -Version 1.2.0
#>
[CmdletBinding()]
param(
    [Parameter(Mandatory = $true)]
    [string]$Version,
    [string]$Output = "$PSScriptRoot\dist",
    [string]$Notes,
    [switch]$BuildOnly,
    [switch]$AllowDirty,
    [switch]$Force
)

$ErrorActionPreference = "Stop"

# Must match MinVerTagPrefix in Esatto.CookieScan.Desktop.csproj and DashboardRelease.TagPrefix in
# the CLI. Three places, one string; the assertion further down is what catches them drifting.
$tagPrefix = "Esatto.CookieScan.Desktop-"
$assetName = "esatto-cookiescan-ui.exe"
$project = "$PSScriptRoot\Esatto.CookieScan.Desktop"

if ($Version -notmatch '^\d+\.\d+\.\d+(-[0-9A-Za-z.-]+)?$') {
    throw "Version '$Version' does not look like a version. Expected e.g. 1.2.0 or 1.2.0-preview.1."
}

$tag = "$tagPrefix$Version"

Push-Location $PSScriptRoot
try {
    # ---------------------------------------------------------------- preflight

    if (-not (Test-Path (Join-Path $PSScriptRoot ".git"))) {
        throw "$PSScriptRoot is not a git repository. MinVer needs one to read a version from."
    }

    $dirty = git status --porcelain
    if ($dirty -and -not $AllowDirty) {
        Write-Host "Working tree is not clean:" -ForegroundColor Yellow
        $dirty | ForEach-Object { Write-Host "  $_" }
        throw "Commit or stash first, or pass -AllowDirty. An exe stamped $Version must contain committed code."
    }

    $head = (git rev-parse HEAD).Trim()
    $branch = (git rev-parse --abbrev-ref HEAD).Trim()

    Write-Host ""
    Write-Host "Releasing the cookie scanner dashboard $Version" -ForegroundColor Cyan
    Write-Host "  commit : $head"
    Write-Host "  branch : $branch"
    Write-Host "  tag    : $tag"
    Write-Host ""

    # Checked before anything is built, so a missing or unauthenticated gh does not cost a 90MB
    # publish first.
    if (-not $BuildOnly) {
        $gh = Get-Command gh -ErrorAction SilentlyContinue
        if (-not $gh) {
            throw "The GitHub CLI (gh) is not installed, so the release cannot be created. Install it, or pass -BuildOnly."
        }

        gh auth status 2>$null | Out-Null
        if ($LASTEXITCODE -ne 0) {
            throw "gh is not authenticated. Run 'gh auth login', or pass -BuildOnly."
        }

        # Warned here rather than discovered after the publish, which costs a minute and 90MB before
        # it would have said anything. The tag push below makes the RELEASE work regardless - it
        # sends the commit with it - but a release pointing at a commit that is on no branch is
        # almost never what someone means, and it is invisible once the release page renders fine.
        #
        # ls-remote rather than origin/<branch>, which is only as fresh as the last fetch.
        $remoteHead = (git ls-remote origin "refs/heads/$branch" | ForEach-Object { $_.Split("`t")[0] })

        if ($remoteHead -ne $head) {
            Write-Host "WARNING: origin/$branch does not point at this commit." -ForegroundColor Yellow

            if ($remoteHead) {
                Write-Host "         origin/$branch is at $($remoteHead.Substring(0, 8)); you are at $($head.Substring(0, 8))." -ForegroundColor Yellow
            }
            else {
                Write-Host "         The branch $branch does not exist on origin at all." -ForegroundColor Yellow
            }

            Write-Host "         The release will still work - pushing its tag sends the commit too - but" -ForegroundColor Yellow
            Write-Host "         it will point at a commit that is on no branch. Consider: git push origin $branch" -ForegroundColor Yellow
            Write-Host ""
        }
    }

    # ---------------------------------------------------------------- tag

    if (git tag --list $tag) {
        $at = (git rev-list -n 1 $tag).Trim()

        if ($at -ne $head) {
            throw "Tag $tag already exists at $at, which is not HEAD ($head). Delete it (git tag -d $tag) or pick another version."
        }

        Write-Host "Tag $tag is already at HEAD." -ForegroundColor DarkGray
    }
    else {
        git tag $tag
        if ($LASTEXITCODE -ne 0) { throw "Could not create tag $tag." }
        Write-Host "Created tag $tag." -ForegroundColor Green
    }

    Write-Host ""

    # ---------------------------------------------------------------- publish

    Write-Host "Publishing the self-contained exe (this takes a minute)..." -ForegroundColor Cyan

    New-Item -ItemType Directory -Force -Path $Output | Out-Null

    $exe = Join-Path $Output $assetName

    if (Test-Path $exe) { Remove-Item $exe -Force }

    dotnet publish $project -c Release -r win-x64 --self-contained `
        -p:PublishSingleFile=true -p:IncludeNativeLibrariesForSelfExtract=true `
        -p:IncludeAllContentForSelfExtract=true -p:EnableCompressionInSingleFile=true `
        -o $Output --nologo

    if ($LASTEXITCODE -ne 0) { throw "dotnet publish failed (exit $LASTEXITCODE)." }
    if (-not (Test-Path $exe)) { throw "The publish produced no $assetName in $Output." }

    # ---------------------------------------------------------------- assert the stamped version

    # The whole reason the tag comes first. MinVer reads the tag to stamp the exe, so if it read a
    # different one - a stale tag, a MinVerTagPrefix that drifted from $tagPrefix above - the number
    # inside the exe is not $Version, and the launcher would cache it under a folder that does not
    # match the release it came from. Asserted here, before anything is uploaded.
    $stamped = (Get-Item $exe).VersionInfo.ProductVersion

    # MinVer appends +<sha>; the version is everything before it.
    $stampedVersion = $stamped.Split('+')[0]

    if ($stampedVersion -ne $Version) {
        throw @"
The published exe is stamped $stampedVersion, not $Version.

MinVer did not read the tag $tag. Check that MinVerTagPrefix in
Esatto.CookieScan.Desktop.csproj is exactly '$tagPrefix'.

Nothing was released.
"@
    }

    $size = [math]::Round((Get-Item $exe).Length / 1MB, 1)

    Write-Host ""
    Write-Host "  $assetName  $size MB  stamped $stampedVersion" -ForegroundColor Green

    # ---------------------------------------------------------------- hash

    # Uploaded beside the asset because the launcher downloads a binary and then EXECUTES it. HTTPS
    # establishes that the bytes came from GitHub, not that GitHub holds what this release intended.
    $hash = (Get-FileHash $exe -Algorithm SHA256).Hash.ToLowerInvariant()
    $hashFile = "$exe.sha256"

    # Lowercase hex and nothing else. DashboardRelease.HashMatches tolerates a filename after it and
    # any casing, but there is no reason to write something it has to tolerate.
    [System.IO.File]::WriteAllText($hashFile, $hash)

    Write-Host "  sha256: $hash" -ForegroundColor DarkGray
    Write-Host ""

    # ---------------------------------------------------------------- release

    if ($BuildOnly) {
        Write-Host "-BuildOnly: no GitHub release was created. Artifacts:" -ForegroundColor Yellow
        Write-Host "  $exe"
        Write-Host "  $hashFile"
        Write-Host ""
        Write-Host "Re-run without -BuildOnly to publish them." -ForegroundColor Yellow
        return
    }

    if (-not $Notes) {
        $Notes = @"
The Esatto cookie scanner dashboard, $Version.

Install or update it with the console tool rather than downloading this asset by hand:

    dotnet tool install -g Esatto.CookieScan.Cli
    esatto-cookiescan ui

That fetches this exe, verifies it against the published SHA-256, caches it under
%LOCALAPPDATA%\Esatto.CookieScan\ui and opens it. Running it again picks up any newer release.
"@
    }

    Write-Host "About to create GitHub release $tag with:" -ForegroundColor Yellow
    Write-Host "  $assetName ($size MB)"
    Write-Host "  $assetName.sha256"
    Write-Host ""
    Write-Host "A release someone has already downloaded from cannot be taken back." -ForegroundColor Yellow
    Write-Host ""

    if (-not $Force) {
        $answer = Read-Host "Create it? Type 'yes' to continue"
        if ($answer -ne "yes") {
            Write-Host "Aborted. Nothing was released. The tag is still here - delete it with:" -ForegroundColor Yellow
            Write-Host "  git tag -d $tag"
            return
        }
    }

    # Not optional, and not a convenience. `gh release create --target <sha>` answers HTTP 422 for a
    # commit GitHub has never seen, which is every release cut from a branch that has not been pushed
    # - so the tag goes first, and it carries the commit and its history with it. Idempotent: a tag
    # already on origin at this commit is an "Everything up-to-date" and a zero exit.
    Write-Host "Publishing the tag so GitHub can resolve the release target..." -ForegroundColor Cyan

    git push origin $tag

    if ($LASTEXITCODE -ne 0) {
        throw "Could not push the tag $tag to origin, so the release cannot be created. Nothing was released."
    }

    # --target pins the release to this commit explicitly rather than letting gh place the tag at the
    # default branch's head, which is not necessarily what was built.
    gh release create $tag $exe $hashFile `
        --title "Cookie scanner dashboard $Version" `
        --notes $Notes `
        --target $head

    if ($LASTEXITCODE -ne 0) { throw "gh release create failed (exit $LASTEXITCODE)." }

    Write-Host ""
    Write-Host "Released $tag." -ForegroundColor Green
    Write-Host "  https://github.com/carl-schele-esatto/Esatto.Packages/releases/tag/$tag" -ForegroundColor DarkGray
    Write-Host ""
    Write-Host "Anyone with the console tool now gets it by running:" -ForegroundColor Cyan
    Write-Host "  esatto-cookiescan ui"
}
finally {
    Pop-Location
}
