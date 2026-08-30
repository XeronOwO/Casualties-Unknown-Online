# check-kernel-shape.ps1 - Phase E guard for generic/string-keyed kernel state.
# The kernel must use typed domain models, not string/object dictionaries.
# This is the automated part of architecture-guards.md item 9.
$ErrorActionPreference = "Stop"

$root = Split-Path -Parent $PSScriptRoot
$stateDir = Join-Path $root "src\CasualtiesUnknownOnline.GameState"
$failures = [System.Collections.Generic.List[string]]::new()

Get-ChildItem -Path $stateDir -Filter *.cs -Recurse | ForEach-Object {
    $relative = $_.FullName.Substring($root.Length + 1)
    $text = [System.IO.File]::ReadAllText($_.FullName)

    if ($text -match 'Dictionary\s*<\s*string\s*,') {
        $failures.Add("$relative uses a string-keyed dictionary; kernel state must be typed")
    }

    if ($text -match 'Hashtable') {
        $failures.Add("$relative uses Hashtable; kernel state must be typed")
    }
}

if ($failures.Count -gt 0) {
    Write-Host "Kernel shape gate FAILED:" -ForegroundColor Red
    foreach ($failure in $failures) {
        Write-Host "  - $failure" -ForegroundColor Red
    }
    exit 1
}

Write-Host "Kernel shape gate passed."
exit 0
