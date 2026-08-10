# check-event-replay.ps1 - hard gate for the entity-event replay completeness
# table (docs/event-replay-matrix.csv). Mirrors check-architecture.ps1:
# run before EVERY commit (alongside dotnet format), exit 1 on violation.
#
# Rules:
#   1. Header is the fixed 8 columns; every kind row is non-empty and unique.
#   2. Every mechanism column (sound-trigger/sound-replay/visual-trigger/
#      visual-replay/state-consumption) is non-empty - an empty cell means the
#      event was never audited (the per-mechanism completeness mandate).
#   3. status is exactly covered | excluded | gap.
#   4. A gap or excluded row MUST carry notes (the why / owning domain); a bare
#      gap is an invisible debt and fails the gate. A covered row may carry
#      notes (recorded sub-gaps, e.g. "0.8s press visual").
#
# When you fix or touch an event mechanism, update the matching row's columns
# in the same commit - the gate checks the table is consistent, the commit
# checklist makes the table update mandatory.

$ErrorActionPreference = "Stop"

$table = Join-Path $PSScriptRoot "..\docs\event-replay-matrix.csv"
$expectedHeader = "kind,sound-trigger,sound-replay,visual-trigger,visual-replay,state-consumption,status,notes"
$allowedStatus = @("covered", "excluded", "gap")

if (-not (Test-Path $table)) {
    Write-Error "event-replay matrix not found: $table"
    exit 1
}

$rows = Import-Csv $table
if ($rows.Count -eq 0) {
    Write-Error "event-replay matrix is empty: $table"
    exit 1
}

$header = (Get-Content $table -TotalCount 1).Trim()
if ($header -ne $expectedHeader) {
    Write-Error "event-replay matrix header mismatch:`n  expected: $expectedHeader`n  actual:   $header"
    exit 1
}

$violations = New-Object System.Collections.Generic.List[string]
$seen = @{}

# Raw-line field count check BEFORE Import-Csv: Import-Csv silently truncates a
# cell containing a bare comma (the split happens at parse time, so a check on
# the parsed cells can never see it - the earlier probe proved exactly that).
$columnCount = @($expectedHeader.Split(",")).Count
$lineNo = 1
foreach ($line in Get-Content $table | Select-Object -Skip 1) {
    $lineNo++
    if ([string]::IsNullOrWhiteSpace($line)) { continue }
    $rawFields = @($line.Split(","))
    if ($rawFields.Count -ne $columnCount) {
        $violations.Add("line $lineNo : $($rawFields.Count) raw fields (expected $columnCount) - a bare comma inside a cell breaks the CSV; reword without commas")
    }
}

foreach ($row in $rows) {
    $kind = $row.kind
    if ([string]::IsNullOrWhiteSpace($kind)) {
        $violations.Add("row with empty kind")
        continue
    }
    if ($seen.ContainsKey($kind)) {
        $violations.Add("duplicate kind: $kind")
    }
    $seen[$kind] = $true

    foreach ($col in @("sound-trigger", "sound-replay", "visual-trigger", "visual-replay", "state-consumption")) {
        if ([string]::IsNullOrWhiteSpace($row.$col)) {
            $violations.Add("$kind : mechanism column '$col' is empty - event not audited")
        }
    }

    # A bare comma inside any cell breaks the CSV for every external viewer
    # (Excel/VS reopen the table misaligned) and Import-Csv silently truncates
    # the column. Describe parameters in words ("Sound.Play sonarouch 2D").
    foreach ($col in @("sound-trigger", "sound-replay", "visual-trigger", "visual-replay", "state-consumption", "notes")) {
        if (-not [string]::IsNullOrWhiteSpace($row.$col) -and $row.$col.Contains(",")) {
            $violations.Add("$kind : bare comma in '$col' - reword without commas (they break the CSV)")
        }
    }

    $statusRaw = $row.status
    if ($null -eq $statusRaw) { $statusRaw = "" }
    $status = $statusRaw.Trim().ToLowerInvariant()
    if ($allowedStatus -notcontains $status) {
        $violations.Add("$kind : invalid status '$($row.status)' (expected covered|excluded|gap)")
        continue
    }

    if ($status -ne "covered" -and [string]::IsNullOrWhiteSpace($row.notes)) {
        $violations.Add("$kind : status '$status' requires a notes entry (why / owning domain)")
    }
}

if ($violations.Count -gt 0) {
    Write-Host "Event-replay gate FAILED:" -ForegroundColor Red
    foreach ($v in $violations) {
        Write-Host "  - $v" -ForegroundColor Red
    }
    exit 1
}

Write-Host "Event-replay gate passed ($($rows.Count) events)." -ForegroundColor Green
exit 0
