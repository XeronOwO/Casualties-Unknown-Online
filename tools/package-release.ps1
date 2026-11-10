#Requires -Version 5.1
<#
.SYNOPSIS
    Builds the player-installable CUO release zip.

.DESCRIPTION
    Builds the solution in Release, stages the plugin folder the deploy script writes into a game, writes
    an install note, and zips the two into one artifact under artifacts/release/. The archive is verified
    by listing it: the plugin assembly and the note must be inside, and the BepInEx-owned assemblies must
    not (the install's own BepInEx owns those).

    This is the shipping surface, not the deploy path: deploy.ps1 still writes into a game directory so a
    release can be tested before it is packaged.

.PARAMETER Configuration
    Build configuration. Defaults to Release.

.PARAMETER OutputDir
    Where the artifact goes. Defaults to <repo>\artifacts\release.

.PARAMETER Version
    The version label used in the file name. Defaults to the current commit's short sha.
#>
param(
    [string] $Configuration = 'Release',
    [string] $OutputDir,
    [string] $Version,
    [switch] $WithMods = $true
)

$ErrorActionPreference = 'Stop'
$repo = Split-Path -Parent $PSScriptRoot
if (-not $OutputDir) { $OutputDir = Join-Path $repo 'artifacts\release' }
if (-not $Version) {
    $Version = (git -C $repo rev-parse --short HEAD 2>$null)
    if (-not $Version) { $Version = 'dev' }
}

Write-Host "Building the solution ($Configuration)..."
dotnet build (Join-Path $repo 'CasualtiesUnknownOnline.slnx') --configuration $Configuration --verbosity quiet
if ($LASTEXITCODE -ne 0) {
    Write-Error 'Build failed.'
    exit $LASTEXITCODE
}

$pluginOut = Join-Path $repo "src\CasualtiesUnknownOnline.Plugin\bin\$Configuration\net48"
if (-not (Test-Path -LiteralPath $pluginOut)) {
    Write-Error "No plugin build output at $pluginOut."
    exit 1
}

# Assemblies owned by the game's own BepInEx install; the install supplies them and a copy here would be a
# second version of the same assembly.
$bepinexOwned = @(
    'BepInEx.dll', 'BepInEx.Harmony.dll',
    'Mono.Cecil.dll', '0Harmony.dll',
    'MonoMod.RuntimeDetour.dll', 'MonoMod.Utils.dll'
)

$stageRoot = Join-Path $OutputDir 'stage'
$stagePlugin = Join-Path $stageRoot 'CasualtiesUnknownOnline'
if (Test-Path -LiteralPath $stageRoot) {
    Get-ChildItem -LiteralPath $stageRoot -File | ForEach-Object { Remove-Item -LiteralPath $_.FullName -Force }
    if (Test-Path -LiteralPath $stagePlugin) {
        Get-ChildItem -LiteralPath $stagePlugin -File | ForEach-Object { Remove-Item -LiteralPath $_.FullName -Force }
    }
}
New-Item -ItemType Directory -Force -Path $stagePlugin | Out-Null

$copied = 0
foreach ($dll in Get-ChildItem -LiteralPath $pluginOut -Filter '*.dll' | Where-Object { $bepinexOwned -notcontains $_.Name }) {
    Copy-Item -LiteralPath $dll.FullName -Destination (Join-Path $stagePlugin $dll.Name) -Force
    $copied++
}
if ($copied -eq 0) {
    Write-Error 'No plugin assemblies to package.'
    exit 1
}

# The bundled mod plugins: their own assembly and any ".Core" assembly they load from disk. Their shared
# dependencies (Abstractions, Microsoft.Extensions.*, System.*) are already in the core set above, so a mod
# DLL that duplicates one is skipped by name - one copy of an assembly per folder.
$mods = @('RecursiveCrafting', 'PinyinSearch', 'Music')
$modDlls = 0
foreach ($mod in $mods) {
    if (-not $WithMods) { break }
    $modOut = Join-Path $repo "src\CasualtiesUnknownOnline.$mod\bin\$Configuration\net48"
    if (-not (Test-Path -LiteralPath $modOut)) {
        Write-Host "  $mod has no $Configuration output; left out of the package"
        continue
    }
    foreach ($name in @("CasualtiesUnknownOnline.$mod.dll", "CasualtiesUnknownOnline.$mod.Core.dll")) {
        $source = Join-Path $modOut $name
        if (-not (Test-Path -LiteralPath $source)) { continue }
        $target = Join-Path $stagePlugin $name
        if (Test-Path -LiteralPath $target) { continue }
        Copy-Item -LiteralPath $source -Destination $target -Force
        $copied++
        $modDlls++
    }
}

$steamApi = Join-Path $repo 'references\steam_api64.dll'
if (-not (Test-Path -LiteralPath $steamApi)) {
    Write-Error "Missing the native Steam runtime: $steamApi"
    exit 1
}
Copy-Item -LiteralPath $steamApi -Destination (Join-Path $stagePlugin 'steam_api64.dll') -Force
$copied++

$note = @(
    'Casualties Unknown: Online'
    ''
    'Install:'
    '  1. Install BepInEx 5.4.23.4 (x64) into the game folder - winhttp.dll, doorstop_config.ini and the BepInEx folder must sit next to CasualtiesUnknown.exe.'
    '  2. Copy the CasualtiesUnknownOnline folder into <game>\BepInEx\plugins\'
    '  3. Create a text file named steam_appid.txt next to CasualtiesUnknown.exe containing 4576490 (the game needs it for Steam to initialise).'
    '  4. Start the game; the CUO button appears at the top right of the main menu.'
    ''
    'The native steam_api64.dll ships beside the plugin assemblies on purpose: Steamworks loads it from disk.'
    ''
    'The folder also carries the bundled mod plugins (RecursiveCrafting, PinyinSearch and Music). Each one is optional:'
    'delete its assembly (and its .Core assembly when present) to run without it.'
)
Set-Content -LiteralPath (Join-Path $stageRoot 'install.txt') -Value $note -Encoding UTF8

$zip = Join-Path $OutputDir ("casualties-unknown-online-$Version.zip")
if (Test-Path -LiteralPath $zip) { Remove-Item -LiteralPath $zip -Force }
Compress-Archive -Path (Join-Path $stageRoot '*') -DestinationPath $zip -CompressionLevel Optimal
if (-not (Test-Path -LiteralPath $zip)) {
    Write-Error "Compress-Archive produced no artifact at $zip."
    exit 1
}

Add-Type -AssemblyName System.IO.Compression.FileSystem
$entries = [System.IO.Compression.ZipFile]::OpenRead($zip).Entries | ForEach-Object { $_.FullName }
$required = @('CasualtiesUnknownOnline/CasualtiesUnknownOnline.dll', 'CasualtiesUnknownOnline/steam_api64.dll', 'install.txt')
$missing = $required | Where-Object { $entries -notcontains $_ }
if ($missing) {
    Write-Error "The archive is missing: $($missing -join ', ')"
    exit 1
}
$leaked = $entries | Where-Object { $bepinexOwned -contains (Split-Path -Leaf $_) }
if ($leaked) {
    Write-Error "The archive carries BepInEx-owned assemblies: $($leaked -join ', ')"
    exit 1
}

$size = (Get-Item -LiteralPath $zip).Length
Write-Host "Packaged $copied file(s) + the install note"
Write-Host "  artifact: $zip"
Write-Host "  size: $size bytes, $($entries.Count) archive entries"
