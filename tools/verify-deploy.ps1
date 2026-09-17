<#
.SYNOPSIS
Verifies that the deployed CUO plugin folder matches the build output of the current tree.

.DESCRIPTION
Every file in <game>/BepInEx/plugins/CasualtiesUnknownOnline/ is compared against the hash SET of
all bin\<Configuration>\net48 copies in the repo — never against "the first same-named DLL".
Package-restore artifacts land in several projects' bin folders and some of them are years stale
(measured 2026-09-14: comparing against the first recursive hit reported four false mismatches out
of 34 files). The repo build is not byte-reproducible across runs either, so this compares the
deployment against THIS tree's build output: run tools/deploy.ps1 first.

Native payloads that are not build output (steam_api64.dll, copied from references/ by deploy.ps1)
are allowed misses; any other miss fails the check (exit 1), because the game would then be running
a DLL this tree did not produce. The deployed plugin DLL's embedded ProductVersion (+<sha>) is
printed so the delivered artifact can be matched to the delivery commit.

.PARAMETER GameDir
Path to the game installation whose deployed plugin folder is verified.

.PARAMETER Configuration
Build configuration to compare against. Default: Debug.

.EXAMPLE
./tools/verify-deploy.ps1 -GameDir "<game-dir>"
#>
param(
    [Parameter(Mandatory = $true)]
    [string]$GameDir,
    [string]$Configuration = "Debug"
)

$ErrorActionPreference = "Stop"
# tools/verify-deploy.ps1 — the repo root is one level up.
$RepoRoot = Split-Path -Parent (Split-Path -Parent $MyInvocation.MyCommand.Path)

if (-not (Test-Path -LiteralPath $GameDir -PathType Container)) {
    Write-Error "Game directory '$GameDir' does not exist."
    exit 1
}

# Mirrors the guard in tools/deploy.ps1: a sandbox path is a redirection layer over the host game
# directory, so verifying it would report on the wrong copy.
if ($GameDir -match "sandbox") {
    Write-Error "GameDir '$GameDir' looks like a sandbox path. Verify the real game directory only."
    exit 1
}

$deployDir = Join-Path $GameDir "BepInEx\plugins\CasualtiesUnknownOnline"
if (-not (Test-Path -LiteralPath $deployDir -PathType Container)) {
    Write-Error "Deployed plugin folder '$deployDir' does not exist. Run tools/deploy.ps1 first."
    exit 1
}

# Hash set of every bin\<Configuration>\net48 copy in the repo.
$buildHashes = New-Object System.Collections.Generic.HashSet[string]
$buildFiles = 0
$binPattern = "\\bin\\$([regex]::Escape($Configuration))\\net48$"
foreach ($top in @("src", "tests")) {
    $topDir = Join-Path $RepoRoot $top
    if (-not (Test-Path -LiteralPath $topDir)) {
        continue
    }

    Get-ChildItem -LiteralPath $topDir -Recurse -Directory -Filter "net48" -ErrorAction SilentlyContinue |
        Where-Object { $_.FullName -match $binPattern } |
        ForEach-Object {
            Get-ChildItem -LiteralPath $_.FullName -File | ForEach-Object {
                $buildFiles++
                [void]$buildHashes.Add((Get-FileHash -LiteralPath $_.FullName -Algorithm SHA256).Hash)
            }
        }
}

if ($buildFiles -eq 0) {
    Write-Error "No bin\$Configuration\net48 build output found under src/ or tests/. Build the solution (or run tools/deploy.ps1) first."
    exit 1
}

# Legitimately not build output: native payloads deploy.ps1 copies from references/.
$allowedMisses = @("steam_api64.dll")

$deployed = Get-ChildItem -LiteralPath $deployDir -File
$misses = @()
foreach ($file in $deployed) {
    $hash = (Get-FileHash -LiteralPath $file.FullName -Algorithm SHA256).Hash
    if (-not $buildHashes.Contains($hash)) {
        $misses += $file.Name
    }
}

$unexpected = @($misses | Where-Object { $allowedMisses -notcontains $_ })

Write-Host "build output:     $buildFiles file(s), $($buildHashes.Count) unique hash(es)"
Write-Host "deployed:         $($deployed.Count) file(s), $($deployed.Count - $misses.Count) matched this tree"
if ($misses.Count -gt 0) {
    Write-Host "not build output: $($misses -join ', ')"
}

$pluginDll = Join-Path $deployDir "CasualtiesUnknownOnline.dll"
if (-not (Test-Path -LiteralPath $pluginDll)) {
    Write-Error "CasualtiesUnknownOnline.dll is not in the deployed folder."
    exit 1
}

Write-Host "delivered artifact: $((Get-Item -LiteralPath $pluginDll).VersionInfo.ProductVersion)"

if ($unexpected.Count -gt 0) {
    Write-Error "Deployment mismatch: $($unexpected -join ', ') do not match this tree's build output. Commit first, then re-run tools/deploy.ps1."
    exit 1
}

Write-Host "Deployment matches this tree's build output."
exit 0
