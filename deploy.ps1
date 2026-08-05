<#
.SYNOPSIS
Builds the CUO solution and deploys the plugin into the game's BepInEx/plugins/CUO/ folder.

.DESCRIPTION
Deploys the 4 runtime files (plugin, Core, Steamworks.NET, steam_api64.dll) into
<game>/BepInEx/plugins/CUO/. Refuses to run while the game is running.

.PARAMETER GameDir
Path to the game installation. If omitted, reads the CUO_GAME_DIR environment variable.

.EXAMPLE
./deploy.ps1 -GameDir "E:\SteamLibrary\steamapps\common\Casualties Unknown Demo"

.EXAMPLE
$env:CUO_GAME_DIR = "E:\SteamLibrary\steamapps\common\Casualties Unknown Demo"
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

$targetDir = Join-Path $GameDir "BepInEx\plugins\CUO"
New-Item -ItemType Directory -Force -Path $targetDir | Out-Null

# (destination, source) pairs. Steamworks.NET and steam_api64 are not build
# outputs — they come from references/ (gitignored, see references/README.md).
$files = @(
    @("CasualtiesUnknownOnline.dll",       (Join-Path $PluginOut "CasualtiesUnknownOnline.dll")),
    @("CasualtiesUnknownOnline.Core.dll",  (Join-Path $PluginOut "CasualtiesUnknownOnline.Core.dll")),
    @("Steamworks.NET.dll",                (Join-Path $RepoRoot "references\Steamworks.NET.dll")),
    @("steam_api64.dll",                   (Join-Path $RepoRoot "references\steam_api64.dll"))
)

foreach ($pair in $files) {
    $name = $pair[0]
    $source = $pair[1]
    if (-not (Test-Path $source)) {
        Write-Error "Missing source: $source — copy it into references/ per references/README.md."
        exit 1
    }
    Copy-Item -LiteralPath $source -Destination (Join-Path $targetDir $name) -Force
    Write-Host "  deployed $name"
}

Write-Host "Deployed CUO to $targetDir"
