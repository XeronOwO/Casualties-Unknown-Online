<#
.SYNOPSIS
Builds the CUO solution and deploys the plugin into the game's BepInEx/plugins/CasualtiesUnknownOnline/ folder.

.DESCRIPTION
Deploys all build-output DLLs (plugin, Runtime, Abstractions, Microsoft.Extensions.*,
System.*) plus Steamworks.NET.dll and steam_api64.dll from references/ into
<game>/BepInEx/plugins/CasualtiesUnknownOnline/. Refuses to run while the game is running.

.PARAMETER GameDir
Path to the game installation. If omitted, reads the CUO_GAME_DIR environment variable.

.PARAMETER Packed
Deploys the SINGLE-FILE artifact instead of the loose set: the plugin plus every managed dependency packed into
one `CasualtiesUnknownOnline.dll` (tools/package-single-file.ps1), with `steam_api64.dll` the only loose file.
The normal deployment is unchanged when the switch is absent.

.EXAMPLE
./tools/deploy.ps1 -GameDir "<game-dir>"

.EXAMPLE
./tools/deploy.ps1 -GameDir "<game-dir>" -Packed

.EXAMPLE
$env:CUO_GAME_DIR = "<game-dir>"
./tools/deploy.ps1
#>
param(
    [string]$GameDir = $env:CUO_GAME_DIR,
    [switch]$Packed
)

$ErrorActionPreference = "Stop"
# tools/deploy.ps1 — the repo root is one level up.
$RepoRoot = Split-Path -Parent (Split-Path -Parent $MyInvocation.MyCommand.Path)
$PluginOut = Join-Path $RepoRoot "src\CasualtiesUnknownOnline.Plugin\bin\Debug\net48"

if ([string]::IsNullOrWhiteSpace($GameDir)) {
    Write-Error "Game directory not specified. Pass -GameDir or set the CUO_GAME_DIR environment variable."
    exit 1
}

# Sandboxie paths (e.g. a sandbox redirection root such as <sandbox-root>) are a redirection layer —
# the sandbox instance reads through to the host's game directory, so deploying
# there is both redundant and a way to corrupt the sandbox copy. This guard is
# the hard gate: deploy to the real game directory only.
if ($GameDir -match "sandbox") {
    Write-Error "GameDir '$GameDir' looks like a sandbox path. Sandbox instances read through to the host's game directory — deploy to the real game directory only."
    exit 1
}

$gameProc = Get-Process -Name CasualtiesUnknown -ErrorAction SilentlyContinue
if ($gameProc) {
    Write-Error "The game is running (PID $($gameProc.Id -join ', ')). Close it before deploying."
    exit 1
}

# Second line of defence: a loaded plugin DLL is held open by the game process,
# so a locked file in the deploy target is the ground truth that a game
# instance (possibly one the process check could not name) is still running.
# Covers launch-transient states and leftover instances.
function Test-FileLocked([string]$path) {
    if (-not (Test-Path -LiteralPath $path)) { return $false }
    try {
        $fs = [System.IO.File]::Open($path, 'Open', 'ReadWrite', 'None')
        $fs.Close()
        return $false
    } catch {
        return $true
    }
}
$targetDir = Join-Path $GameDir "BepInEx\plugins\CasualtiesUnknownOnline"
$locked = Get-ChildItem -LiteralPath $targetDir -Filter *.dll -ErrorAction SilentlyContinue |
    Where-Object { Test-FileLocked $_.FullName }
if ($locked) {
    Write-Error "Plugin DLLs are locked: $($locked.Name -join ', '). The game (or a leftover instance) is holding them. Close it before deploying."
    exit 1
}

Write-Host "Building solution..."
dotnet build (Join-Path $RepoRoot "CasualtiesUnknownOnline.slnx") --verbosity quiet
if ($LASTEXITCODE -ne 0) {
    Write-Error "Build failed."
    exit $LASTEXITCODE
}

$targetDir = Join-Path $GameDir "BepInEx\plugins\CasualtiesUnknownOnline"
New-Item -ItemType Directory -Force -Path $targetDir | Out-Null

if ($Packed) {
    # The single-file package: pack this build, then leave ONLY the packed plugin and the native Steam runtime
    # in the folder. Every other file there is a leftover of the loose deployment and is removed one level deep.
    $packOut = Join-Path $RepoRoot "bin\packed"
    if (Test-Path -LiteralPath $packOut) {
        Get-ChildItem -LiteralPath $packOut -File | ForEach-Object { Remove-Item -LiteralPath $_.FullName -Force }
    }
    & (Join-Path $RepoRoot "tools\package-single-file.ps1") -GameDir $GameDir -OutputDir $packOut
    if ($LASTEXITCODE -ne 0) {
        Write-Error "Packing failed."
        exit $LASTEXITCODE
    }
    $packedDll = Join-Path $packOut "CasualtiesUnknownOnline.dll"
    if (-not (Test-Path -LiteralPath $packedDll)) {
        Write-Error "The packer produced no CasualtiesUnknownOnline.dll in $packOut."
        exit 1
    }
    Copy-Item -LiteralPath $packedDll -Destination (Join-Path $targetDir "CasualtiesUnknownOnline.dll") -Force
    $steamApi = Join-Path $RepoRoot "references\steam_api64.dll"
    if (-not (Test-Path -LiteralPath $steamApi)) {
        Write-Error "Missing source: $steamApi"
        exit 1
    }
    Copy-Item -LiteralPath $steamApi -Destination (Join-Path $targetDir "steam_api64.dll") -Force
    $expected = @("CasualtiesUnknownOnline.dll", "steam_api64.dll")
    Get-ChildItem -LiteralPath $targetDir -File |
        Where-Object { $expected -notcontains $_.Name } |
        ForEach-Object {
            Remove-Item -LiteralPath $_.FullName -Force
            Write-Host "  removed loose $($_.Name)"
        }
    Write-Host "Deployed CUO (packed) to $targetDir"
    Write-Host "  packed artifact: $((Get-Item -LiteralPath (Join-Path $targetDir 'CasualtiesUnknownOnline.dll')).Length) bytes, plus the native steam_api64.dll"
    exit 0
}

# Assemblies owned by the game's own BepInEx install (BepInEx/core) — never
# deploy our copies (version conflicts). BepInEx.Core.targets already keeps
# these out of build output; the list is a safety net against package layout changes.
$bepinexOwned = @(
    "BepInEx.dll", "BepInEx.Harmony.dll",
    "Mono.Cecil.dll", "0Harmony.dll",
    "MonoMod.RuntimeDetour.dll", "MonoMod.Utils.dll"
)

# Build-output DLLs: plugin + Runtime + Abstractions + Microsoft.Extensions.*
# + System.* (normal NuGet packages, required in the game's Mono runtime).
$dlls = Get-ChildItem -LiteralPath $PluginOut -Filter *.dll |
    Where-Object { $bepinexOwned -notcontains $_.Name }
if (-not $dlls) {
    Write-Error "No DLLs found in $PluginOut — build produced nothing?"
    exit 1
}
foreach ($dll in $dlls) {
    try {
        Copy-Item -LiteralPath $dll.FullName -Destination (Join-Path $targetDir $dll.Name) -Force
    } catch {
        Write-Error "Failed to deploy $($dll.Name) — the target is locked, so the game (or a leftover instance, or a scanner) is holding it. Close the game and retry."
        exit 1
    }
    Write-Host "  deployed $($dll.Name)"
}

# Steamworks.NET + steam_api64 are not NuGet packages — from references/.
foreach ($name in @("Steamworks.NET.dll", "steam_api64.dll")) {
    $source = Join-Path $RepoRoot "references\$name"
    if (-not (Test-Path $source)) {
        Write-Error "Missing source: $source — copy it into references/ per references/README.md."
        exit 1
    }
    Copy-Item -LiteralPath $source -Destination (Join-Path $targetDir $name) -Force
    Write-Host "  deployed $name"
}

# Keep the plugin directory in sync with the current build: remove stale DLLs
# from older dependency versions so the Mono loader can never accidentally bind
# a file that is no longer part of the resolved graph (e.g. 10.x-era
# Microsoft.Extensions leftovers after a 3.1.x rollback).
$expectedNames = @($dlls.Name) + @("Steamworks.NET.dll", "steam_api64.dll")
$stale = Get-ChildItem -LiteralPath $targetDir -Filter *.dll -File |
    Where-Object { $expectedNames -notcontains $_.Name }
foreach ($file in $stale) {
    Remove-Item -LiteralPath $file.FullName -Force
    Write-Host "  removed stale $($file.Name)"
}

# Satellite mods live in their OWN plugin folders and compile against Abstractions, so a stale copy is a mod that refuses to load with a
# MissingMethodException on the contract it was built for. Until this list existed, `deploy` meant "the main plug-in" and the mods drifted a day behind
# the contract (user report 2026-10-25: cuo.music and cuo.recursivecrafting both failed to load while pinyin, built the same day as the contract,
# worked). Each entry is: built assembly -> the folder the game loads it from; the timestamp is printed so a stale copy is visible in the deploy log.
$satelliteMods = @(
    @{ Assemblies = @("CasualtiesUnknownOnline.Music.dll"); Folder = "MusicShare"; Project = "CasualtiesUnknownOnline.Music" },
    @{ Assemblies = @("CasualtiesUnknownOnline.RecursiveCrafting.dll", "CasualtiesUnknownOnline.RecursiveCrafting.Core.dll"); Folder = "RecursiveCrafting"; Project = "CasualtiesUnknownOnline.RecursiveCrafting" }
)

foreach ($mod in $satelliteMods) {
    # Each mod is its own project, so its build output is its own bin directory — not the plug-in's.
    $modBuildDir = Join-Path $RepoRoot "src\$($mod.Project)\bin\Debug\net48"
    $modOut = Join-Path $modBuildDir ($mod.Assemblies[0])
    if (-not (Test-Path -LiteralPath $modOut)) {
        Write-Host "  skipped satellite mod $($mod.Folder): $($mod.Assemblies[0]) was not built in this run"
        continue
    }

    $modDir = Join-Path $GameDir "BepInEx\plugins\$($mod.Folder)"
    if (-not (Test-Path -LiteralPath $modDir)) {
        New-Item -ItemType Directory -Path $modDir | Out-Null
    }

    foreach ($assembly in $mod.Assemblies) {
        $source = Join-Path $modBuildDir $assembly
        if (-not (Test-Path -LiteralPath $source)) {
            Write-Error "Missing satellite mod assembly: $source"
            exit 1
        }

        Copy-Item -LiteralPath $source -Destination (Join-Path $modDir $assembly) -Force
        Write-Host "  deployed $($mod.Folder)/$assembly ($((Get-Item -LiteralPath $source).LastWriteTime.ToString('MM-dd HH:mm')))"
    }
}

Write-Host "Deployed CUO to $targetDir"
