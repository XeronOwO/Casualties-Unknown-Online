#Requires -Version 7.0
<#
.SYNOPSIS
    Provisions the BepInEx 5 layer a fresh Casualties Unknown 1.0 install lacks.

.DESCRIPTION
    The release ships no BepInEx: without `winhttp.dll`, `doorstop_config.ini` and
    `BepInEx/core` the game never loads a plugin, so tools/deploy.ps1 has nowhere to
    write. This script copies a pinned BepInEx 5.4.23.4 x64 layer into <game-dir>
    and verifies the files the preloader needs. It never touches the game's own
    files, and it is idempotent: a complete layer is reported and left alone.

    Default source: -SourceDir, else the demo install's layer when present
    (the demo install is a local copy on this machine), else the pinned release
    URL with -Download. Nothing here downloads unless asked.

.PARAMETER GameDir
    The game install root (the folder holding CasualtiesUnknown.exe).

.PARAMETER SourceDir
    A folder whose layout is `winhttp.dll`, `doorstop_config.ini`, `BepInEx\...`
    (a game install that already has BepInEx, or an extracted BepInEx zip).

.PARAMETER Download
    Fetch the pinned BepInEx zip from GitHub when no local layer is found.

.EXAMPLE
    ./tools/install-bepinex.ps1 -GameDir "<game-dir>"

.EXAMPLE
    ./tools/install-bepinex.ps1 -GameDir "<game-dir>" -Download
#>
[CmdletBinding()]
param(
    [Parameter(Mandatory = $true)]
    [string]$GameDir,

    [string]$SourceDir,

    [switch]$Download
)

Set-StrictMode -Version Latest
$ErrorActionPreference = 'Stop'

$pinnedVersion = '5.4.23.4'
$pinnedUrl = "https://github.com/BepInEx/BepInEx/releases/download/v$pinnedVersion/BepInEx_win_x64_$pinnedVersion.zip"
$requiredFiles = @(
    'winhttp.dll',
    'doorstop_config.ini',
    'BepInEx\core\BepInEx.dll',
    'BepInEx\core\BepInEx.Preloader.dll',
    'BepInEx\core\0Harmony.dll'
)

function Test-Layer([string]$Root) {
    foreach ($relative in @('winhttp.dll', 'doorstop_config.ini', 'BepInEx\core\BepInEx.dll')) {
        if (-not (Test-Path -LiteralPath (Join-Path $Root $relative))) { return $false }
    }

    return $true
}

if (-not (Test-Path -LiteralPath $GameDir)) {
    Write-Error "Game directory not found: $GameDir"
    exit 2
}

$game = (Resolve-Path -LiteralPath $GameDir).Path
if ($game -match 'sandbox') {
    Write-Error "GameDir '$game' looks like a sandbox path. Install the layer into the real game directory only."
    exit 2
}

if (-not (Test-Path -LiteralPath (Join-Path $game 'CasualtiesUnknown.exe'))) {
    Write-Error "Not a Casualties Unknown install (CasualtiesUnknown.exe is missing): $game"
    exit 2
}

if (Test-Layer $game) {
    $core = Join-Path $game 'BepInEx\core\BepInEx.dll'
    $version = (Get-Item -LiteralPath $core).VersionInfo.FileVersion
    Write-Output "BepInEx layer already installed (BepInEx $version): $game"
    exit 0
}

$source = $SourceDir

if ([string]::IsNullOrWhiteSpace($source) -and $Download) {
    $temp = Join-Path ([System.IO.Path]::GetTempPath()) ("bepinex-$pinnedVersion")
    if (Test-Path -LiteralPath $temp) { Remove-Item -LiteralPath $temp -Recurse -Force }
    New-Item -ItemType Directory -Path $temp | Out-Null
    $zip = Join-Path $temp "BepInEx_win_x64_$pinnedVersion.zip"
    Write-Output "Downloading BepInEx $pinnedVersion from $pinnedUrl"
    Invoke-WebRequest -Uri $pinnedUrl -OutFile $zip -UseBasicParsing
    $extract = Join-Path $temp 'extracted'
    Expand-Archive -LiteralPath $zip -DestinationPath $extract -Force
    $source = $extract
}

if ([string]::IsNullOrWhiteSpace($source) -or -not (Test-Layer $source)) {
    Write-Error @"
No BepInEx layer found to copy.

Pass -SourceDir <a game install that has BepInEx, or an extracted BepInEx zip>,
or -Download to fetch BepInEx $pinnedVersion from GitHub.
"@
    exit 2
}

Write-Output "Installing the BepInEx layer from $source"
foreach ($relative in @('winhttp.dll', 'doorstop_config.ini')) {
    Copy-Item -LiteralPath (Join-Path $source $relative) -Destination (Join-Path $game $relative) -Force
}

$targetBepInEx = Join-Path $game 'BepInEx'
New-Item -ItemType Directory -Path $targetBepInEx -Force | Out-Null
foreach ($folder in @('core', 'patchers', 'plugins')) {
    $from = Join-Path $source "BepInEx\$folder"
    if (-not (Test-Path -LiteralPath $from)) { continue }
    $to = Join-Path $targetBepInEx $folder
    New-Item -ItemType Directory -Path $to -Force | Out-Null
    Copy-Item -Path (Join-Path $from '*') -Destination $to -Recurse -Force
}

foreach ($relative in $requiredFiles) {
    if (-not (Test-Path -LiteralPath (Join-Path $game $relative))) {
        Write-Error "The copied layer is incomplete: $relative is missing after the copy."
        exit 2
    }
}

$version = (Get-Item -LiteralPath (Join-Path $game 'BepInEx\core\BepInEx.dll')).VersionInfo.FileVersion
Write-Output "BepInEx layer installed (BepInEx $version): $game"
Write-Output "Next: tools/deploy.ps1 -GameDir `"$game`""
