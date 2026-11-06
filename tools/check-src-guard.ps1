# check-src-guard.ps1 - the mechanical half of "a source change closes the game first".
#
# The rule (docs/development/rules/00-work-mode.local.md) is: before the first edit under `src/`, stop the game and
# confirm zero processes; then edit, build, gates, commit, deploy, verify, relaunch. Printed rules alone did not hold:
# on 2026-11-06 a radiation fix was edited with three clients running, and the deploy that followed could not be trusted
# against the loaded assemblies.
#
# This script makes the violation fail where it is reached:
#   -Build   the build refuses when `src/` is dirty and a game process is running;
#   -Commit  the pre-commit hook refuses when the staged change touches `src/` and a game process is running.
#
# Exit 0 means the guard does not apply (clean tree, or no game process). It never blocks a docs/tests/tools-only change.

param(
    [switch]$Build,
    [switch]$Commit
)

$ErrorActionPreference = 'Stop'
$repo = Split-Path -Parent $PSScriptRoot
Push-Location -LiteralPath $repo
try {
    $running = @(Get-Process -Name CasualtiesUnknown -ErrorAction SilentlyContinue).Count
    if ($running -eq 0) { exit 0 }

    $paths = if ($Commit) {
        @(git diff --cached --name-only -- src 2>$null)
    }
    else {
        @(git status --porcelain -- src 2>$null | ForEach-Object { $_.Substring(3).Trim('"') })
    }
    $paths = @($paths | Where-Object { $_ })
    if ($paths.Count -eq 0) { exit 0 }

    $where = if ($Commit) { 'staged' } else { 'in the working tree' }
    Write-Host "src-guard: REFUSED - src/ changes are $where while $running game process(es) run."
    Write-Host "  A source change closes the game first: stop the clients, confirm zero processes, then edit, build,"
    Write-Host "  gates, commit, deploy, verify, relaunch (docs/development/rules/00-work-mode.local.md)."
    Write-Host "  Changed:"
    $paths | Select-Object -First 12 | ForEach-Object { Write-Host "    $_" }
    exit 1
}
finally {
    Pop-Location
}
