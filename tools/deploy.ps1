<#
.SYNOPSIS
Builds the CUO solution and deploys the plugin into the game's BepInEx/plugins/CasualtiesUnknownOnline/ folder.

.DESCRIPTION
Deploys all build-output DLLs (plugin, Runtime, Abstractions, Microsoft.Extensions.*,
System.*) plus Steamworks.NET.dll and steam_api64.dll from references/ into
<game>/BepInEx/plugins/CasualtiesUnknownOnline/. Refuses to run while the game is running.

.PARAMETER GameDir
Path to the game installation. If omitted, reads the CUO_GAME_DIR environment variable.

.EXAMPLE
./tools/deploy.ps1 -GameDir "<game-dir>"

.EXAMPLE
$env:CUO_GAME_DIR = "<game-dir>"
./tools/deploy.ps1
#>
param(
    [string]$GameDir = $env:CUO_GAME_DIR
)

$ErrorActionPreference = "Stop"
# tools/deploy.ps1 — the repo root is one level up.
$RepoRoot = Split-Path -Parent (Split-Path -Parent $MyInvocation.MyCommand.Path)
$PluginOut = Join-Path $RepoRoot "src\CasualtiesUnknownOnline.Plugin\bin\Debug\net48"

if ([string]::IsNullOrWhiteSpace($GameDir)) {
    Write-Error "Game directory not specified. Pass -GameDir or set the CUO_GAME_DIR environment variable."
    exit 1
}

# Sandboxie paths (e.g. <sandbox-root>\Steam1\drive\...) are a redirection layer —
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

Write-Host "Deployed CUO to $targetDir"
