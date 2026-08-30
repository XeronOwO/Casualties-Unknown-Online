# check-command-authority.ps1 - Phase E guard for authority-policy completeness.
# Every GameCommand subclass in the kernel must carry an Authority field/policy
# in its definition. This is the automated part of architecture-guards.md item 6.
$ErrorActionPreference = "Stop"

$root = Split-Path -Parent $PSScriptRoot
$stateDir = Join-Path $root "src\CasualtiesUnknownOnline.GameState"
$failures = [System.Collections.Generic.List[string]]::new()

Get-ChildItem -Path $stateDir -Filter *.cs -Recurse | ForEach-Object {
    $relative = $_.FullName.Substring($root.Length + 1)
    $text = [System.IO.File]::ReadAllText($_.FullName)

    # Files that define a command subclass (not merely mention GameCommand).
    if ($text -match '(?m):\s*GameCommand(?:\s|\()') {
        if ($text -notmatch 'AuthorityKind|Authority') {
            $failures.Add("$relative defines a GameCommand without an Authority policy")
        }
    }
}

if ($failures.Count -gt 0) {
    Write-Host "Command authority gate FAILED:" -ForegroundColor Red
    foreach ($failure in $failures) {
        Write-Host "  - $failure" -ForegroundColor Red
    }
    exit 1
}

Write-Host "Command authority gate passed."
exit 0
