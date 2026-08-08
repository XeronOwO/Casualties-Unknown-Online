# Architecture gate — run before every commit (same tier as `dotnet format`).
# Scans src/ for the three structural rules the codebase must not regress into:
#   1. one top-level type per file (user rule)
#   2. a class file over MaxLines is a domain that must be split first
#   3. a class with more than MaxBoolFlags expression-state booleans must be
#      modeled as a state machine instead of piling flags
# Exit code 1 (with the offenders listed) when any rule is violated.
param(
    [int]$MaxLines = 600,
    [int]$MaxBoolFlags = 5
)

$ErrorActionPreference = "Stop"
$root = Split-Path -Parent $PSScriptRoot
$src = Join-Path $root "src"
$failures = [System.Collections.Generic.List[string]]::new()

Get-ChildItem -Path $src -Filter *.cs -Recurse | ForEach-Object {
    $path = $_.FullName
    $relative = $_.FullName.Substring($root.Length + 1)
    $lines = Get-Content -Path $path

    # 1. Top-level type count: declarations at brace-depth 0 outside any
    #    namespace body. Track braces per line (naive, string literals with
    #    braces are rare in C#; this is a gate, not a parser).
    $depth = 0
    $topLevelTypes = 0
    foreach ($line in $lines) {
        $trimmed = $line.Trim()
        $braceDelta = ($trimmed.ToCharArray() | Where-Object { $_ -eq '{' }).Count - ($trimmed.ToCharArray() | Where-Object { $_ -eq '}' }).Count
        if ($depth -eq 0 -and $trimmed -match '^(public\s+|internal\s+|sealed\s+|static\s+|abstract\s+|partial\s+)*(class|struct|interface|enum|record)\s+\w+') {
            $topLevelTypes++
        }
        $depth += $braceDelta
        if ($depth -lt 0) { $depth = 0 }
    }
    if ($topLevelTypes -gt 1) {
        $failures.Add("$relative : $topLevelTypes top-level types (rule: one per file)")
    }

    # 2. File size — with one top-level type per file, the file length is the
    #    type's length.
    if ($lines.Count -gt $MaxLines) {
        $failures.Add("$relative : $($lines.Count) lines (max $MaxLines — split the domain)")
    }

    # 3. Expression-state boolean fields: `private bool _name;` fields that are
    #    not simple config (constants are excluded by not matching).
    $boolFlags = 0
    foreach ($line in $lines) {
        if ($line -match '^\s*(private|internal|public|protected)?\s*(static\s+)?bool\s+_\w+\s*;') {
            $boolFlags++
        }
    }
    if ($boolFlags -gt $MaxBoolFlags) {
        $failures.Add("$relative : $boolFlags boolean state fields (max $MaxBoolFlags — model a state machine instead)")
    }
}

if ($failures.Count -gt 0) {
    Write-Host "ARCHITECTURE GATE FAILED:" -ForegroundColor Red
    $failures | ForEach-Object { Write-Host "  $_" -ForegroundColor Red }
    exit 1
}

Write-Host "Architecture gate passed." -ForegroundColor Green
exit 0
