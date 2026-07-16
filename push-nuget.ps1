<#
.SYNOPSIS
    Packs a package in this mono-repo and pushes it to NuGet.

.DESCRIPTION
    Version comes from MinVer (git tags), so pack/push the exact bits a release tag
    produced - do NOT hand-edit versions. Typical flow:

        git tag Esatto.Umbraco.Backoffice.DictionaryLocalization-1.1.0
        git push origin Esatto.Umbraco.Backoffice.DictionaryLocalization-1.1.0
        ./push-nuget.ps1                       # packs 1.1.0 from the tag, pushes to nuget.org

    The API key is read from $env:NUGET_API_KEY (or pass -ApiKey). It is never stored
    in this script. Uses --skip-duplicate so re-running an already-published version is safe.

.PARAMETER Project
    Path to the .csproj to pack. Defaults to the Dictionary Localization package.

.PARAMETER Source
    NuGet source. Defaults to nuget.org. For the internal Azure feed pass the feed name
    (e.g. -Source esatto-packages) - it must be a configured nuget source with credentials.

.PARAMETER ApiKey
    API key. Defaults to $env:NUGET_API_KEY. For an Azure feed with a credential provider
    an "az" placeholder is used automatically when no key is supplied.

.PARAMETER Output
    Directory for the produced .nupkg. Defaults to the repo's artifacts folder.

.PARAMETER Force
    Skip the interactive confirmation before pushing.

.EXAMPLE
    $env:NUGET_API_KEY = "oy2..."; ./push-nuget.ps1

.EXAMPLE
    ./push-nuget.ps1 -Project .\Some.Other.Package\Some.Other.Package.csproj -Force
#>
[CmdletBinding()]
param(
    [string]$Project = "$PSScriptRoot\Esatto.Umbraco.Backoffice.DictionaryLocalization\Esatto.Umbraco.Backoffice.DictionaryLocalization.csproj",
    [string]$Source = "https://api.nuget.org/v3/index.json",
    [string]$ApiKey = $env:NUGET_API_KEY,
    [string]$Output = "$PSScriptRoot\artifacts",
    [switch]$Force
)

$ErrorActionPreference = "Stop"

# Resolve and validate the project up front.
if (-not (Test-Path $Project)) {
    throw "Project not found: $Project"
}
$Project = (Resolve-Path $Project).Path
$packageId = [System.IO.Path]::GetFileNameWithoutExtension($Project)

# nuget.org needs a real key; a named feed backed by a credential provider does not.
$isNugetOrg = $Source -like "*nuget.org*"
if ($isNugetOrg -and [string]::IsNullOrWhiteSpace($ApiKey)) {
    throw "No API key. Set `$env:NUGET_API_KEY or pass -ApiKey (required for nuget.org)."
}
if ([string]::IsNullOrWhiteSpace($ApiKey)) {
    $ApiKey = "az"  # placeholder for credential-provider-backed feeds (e.g. Azure Artifacts)
}

New-Item -ItemType Directory -Force -Path $Output | Out-Null

Write-Host "Packing $packageId (Release, version from MinVer/git tag)..." -ForegroundColor Cyan
# Note the newest nupkg BEFORE packing so we can identify the one this run produces.
$before = Get-ChildItem $Output -Filter "$packageId.*.nupkg" -ErrorAction SilentlyContinue

dotnet pack $Project -c Release -o $Output
if ($LASTEXITCODE -ne 0) { throw "dotnet pack failed with exit code $LASTEXITCODE." }

# The freshly-produced package = the newest nupkg for this id (by write time).
$nupkg = Get-ChildItem $Output -Filter "$packageId.*.nupkg" |
    Sort-Object LastWriteTime -Descending |
    Select-Object -First 1
if (-not $nupkg) { throw "No .nupkg found for $packageId in $Output after pack." }

# Pull the version out of the file name for the confirmation prompt.
$version = $nupkg.BaseName.Substring($packageId.Length + 1)
$isNew = -not ($before | Where-Object { $_.Name -eq $nupkg.Name })

Write-Host ""
Write-Host "  Package : $($nupkg.Name)"        -ForegroundColor Green
Write-Host "  Version : $version"              -ForegroundColor Green
Write-Host "  Source  : $Source"              -ForegroundColor Green
if ($version -match '-') {
    Write-Host "  WARNING : this is a PRERELEASE version." -ForegroundColor Yellow
}
if (-not $isNew) {
    Write-Host "  NOTE    : pack produced no new file - this version was built earlier (will --skip-duplicate)." -ForegroundColor Yellow
}
Write-Host ""

if (-not $Force) {
    $answer = Read-Host "Push this package to '$Source'? Type 'yes' to continue"
    if ($answer -ne "yes") {
        Write-Host "Aborted. Nothing was pushed." -ForegroundColor Yellow
        return
    }
}

Write-Host "Pushing..." -ForegroundColor Cyan
dotnet nuget push $nupkg.FullName --source $Source --api-key $ApiKey --skip-duplicate
if ($LASTEXITCODE -ne 0) { throw "dotnet nuget push failed with exit code $LASTEXITCODE." }

Write-Host "Done: $($nupkg.Name) -> $Source" -ForegroundColor Green
