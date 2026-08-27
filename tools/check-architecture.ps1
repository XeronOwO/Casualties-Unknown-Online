# Architecture gate — run before every commit (same tier as `dotnet format`).
# Scans src/ for the three structural rules the codebase must not regress into:
#   1. one top-level type per file (user rule)
#   2. a LOGICAL top-level type over MaxLines is a domain that must be split first
#      (line counts are aggregated across partial files, so physical partial
#      splitting can no longer hide a logical god class)
#   3. a logical top-level type with more than MaxBoolFlags expression-state
#      booleans must be modeled as a state machine instead of piling flags
#
# Existing over-limit logical types are tracked in docs/architecture-debt.json.
# They are allowed to remain ONLY as recorded debt: a type that is not in the
# ledger, or that grows beyond its recorded size, fails the gate. Run with
# -Strict to fail on every recorded debt too (once the mountain is flattened).
param(
    [int]$MaxLines = 600,
    [int]$MaxBoolFlags = 5,
    [switch]$Strict
)

$ErrorActionPreference = "Stop"
$root = Split-Path -Parent $PSScriptRoot
$src = Join-Path $root "src"
$debtPath = Join-Path $root "docs\architecture-debt.json"
$failures = [System.Collections.Generic.List[string]]::new()
$debtWarnings = [System.Collections.Generic.List[string]]::new()

# Recorded debt: { TypeName = { Lines, BoolFlags } }.
$debt = @{}
if (Test-Path $debtPath) {
    $json = Get-Content -Path $debtPath -Raw -Encoding UTF8 | ConvertFrom-Json
    foreach ($prop in $json.PSObject.Properties) {
        $debt[$prop.Name] = $prop.Value
    }
}

# Aggregate per logical top-level type across all files (including partials).
$types = @{}

Get-ChildItem -Path $src -Filter *.cs -Recurse | ForEach-Object {
    $path = $_.FullName
    $relative = $_.FullName.Substring($root.Length + 1)
    $lines = Get-Content -Path $path
    $text = [System.IO.File]::ReadAllText($path)

    # Namespace (file-scoped or classic).
    $namespace = ""
    if ($text -match '(?m)^namespace\s+([A-Za-z0-9_.]+)\s*(\{|;)') {
        $namespace = $Matches[1]
    }

    # 1. Top-level type count: declarations at brace-depth 0 outside any
    #    namespace body. Track braces per line (naive, string literals with
    #    braces are rare in C#; this is a gate, not a parser).
    $depth = 0
    $topLevelTypes = [System.Collections.Generic.List[string]]::new()
    foreach ($line in $lines) {
        $trimmed = $line.Trim()
        $braceDelta = ($trimmed.ToCharArray() | Where-Object { $_ -eq '{' }).Count - ($trimmed.ToCharArray() | Where-Object { $_ -eq '}' }).Count
        if ($depth -eq 0 -and $trimmed -match '^(public\s+|internal\s+|sealed\s+|static\s+|abstract\s+|partial\s+)*(class|struct|interface|enum|record)\s+(\w+)') {
            $topLevelTypes.Add($Matches[3])
        }
        $depth += $braceDelta
        if ($depth -lt 0) { $depth = 0 }
    }

    if ($topLevelTypes.Count -gt 1) {
        $failures.Add("$relative : $($topLevelTypes.Count) top-level types (rule: one per file)")
    }

    # Only aggregate when the file follows the one-top-level-type rule.
    if ($topLevelTypes.Count -eq 1) {
        $typeName = $topLevelTypes[0]
        $fullName = if ($namespace) { "$namespace.$typeName" } else { $typeName }

        # 2. Logical type line count (aggregated across partial files).
        # 3. Expression-state boolean fields across the whole logical type.
        $boolFlags = 0
        foreach ($line in $lines) {
            if ($line -match '^\s*(private|internal|public|protected)?\s*(static\s+)?bool\s+_\w+\s*;') {
                $boolFlags++
            }
        }

        if (-not $types.ContainsKey($fullName)) {
            $types[$fullName] = [pscustomobject]@{
                TypeName = $typeName
                FullName = $fullName
                Lines = 0
                BoolFlags = 0
            }
        }

        $types[$fullName].Lines += $lines.Count
        $types[$fullName].BoolFlags += $boolFlags
    }
}

foreach ($type in $types.Values) {
    # Line-count debt / failure.
    if ($type.Lines -gt $MaxLines) {
        $recorded = $debt[$type.FullName]
        if ($null -eq $recorded) {
            $failures.Add("$($type.FullName) : $($type.Lines) aggregate lines (max $MaxLines — split the logical type; add to docs/architecture-debt.json only after a deliberate plan)")
        }
        elseif ($type.Lines -gt $recorded.Lines) {
            $failures.Add("$($type.FullName) : $($type.Lines) aggregate lines grew beyond recorded debt $($recorded.Lines) — no new debt allowed")
        }
        elseif ($Strict) {
            $failures.Add("$($type.FullName) : $($type.Lines) recorded aggregate lines (max $MaxLines — strict mode refuses recorded debt)")
        }
        else {
            $debtWarnings.Add("DEBT $($type.FullName) : $($type.Lines) aggregate lines (recorded $($recorded.Lines))")
        }
    }

    # Bool-flag debt / failure.
    if ($type.BoolFlags -gt $MaxBoolFlags) {
        $recorded = $debt[$type.FullName]
        if ($null -eq $recorded) {
            $failures.Add("$($type.FullName) : $($type.BoolFlags) boolean state fields (max $MaxBoolFlags — model a state machine instead)")
        }
        elseif ($type.BoolFlags -gt $recorded.BoolFlags) {
            $failures.Add("$($type.FullName) : $($type.BoolFlags) boolean state fields grew beyond recorded debt $($recorded.BoolFlags) — no new debt allowed")
        }
        elseif ($Strict) {
            $failures.Add("$($type.FullName) : $($type.BoolFlags) recorded boolean state fields (max $MaxBoolFlags — strict mode refuses recorded debt)")
        }
        else {
            $debtWarnings.Add("DEBT $($type.FullName) : $($type.BoolFlags) boolean state fields (recorded $($recorded.BoolFlags))")
        }
    }
}

# Print recorded debts as a visible, non-blocking report so the debt is not
# invisible while it is being worked down.
if ($debtWarnings.Count -gt 0) {
    Write-Host "Recorded architecture debt (must not grow; -Strict refuses these):" -ForegroundColor Yellow
    $debtWarnings | ForEach-Object { Write-Host "  $_" -ForegroundColor Yellow }
}

if ($failures.Count -gt 0) {
    Write-Host "ARCHITECTURE GATE FAILED:" -ForegroundColor Red
    $failures | ForEach-Object { Write-Host "  $_" -ForegroundColor Red }
    exit 1
}

# The GameState kernel is a dependency-free deterministic project; its
# isolation boundary is part of the architecture gate.
& (Join-Path $PSScriptRoot "check-gamestate-isolation.ps1")
if ($LASTEXITCODE -ne 0) {
    exit $LASTEXITCODE
}

Write-Host "Architecture gate passed." -ForegroundColor Green
exit 0
