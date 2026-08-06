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
./deploy.ps1 -GameDir "C:\path\to\game"

.EXAMPLE
$env:CUO_GAME_DIR = "C:\path\to\game"
./deploy.ps1
#>
param(
    [string]$GameDir = $env:CUO_GAME_DIR
)

$ErrorActionPreference = "Stop"
$RepoRoot = Split-Path -Parent $MyInvocation.MyCommand.Path
$PluginOut = Join-Path $RepoRoot "src\CasualtiesUnknownOnline.Plugin\bin\Debug\net48"

if ([string]::IsNullOrWhiteSpace($GameDir)) {
    Write-Error "Game directory not specified. Pass -GameDir or set the CUO_GAME_DIR environment variable."
    exit 1
}

$gameProc = Get-Process -Name CasualtiesUnknown -ErrorAction SilentlyContinue
if ($gameProc) {
    Write-Error "The game is running (PID $($gameProc.Id -join ', ')). Close it before deploying."
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
    Copy-Item -LiteralPath $dll.FullName -Destination (Join-Path $targetDir $dll.Name) -Force
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
