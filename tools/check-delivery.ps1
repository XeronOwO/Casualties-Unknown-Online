# check-delivery.ps1 - the delivery-gate checklist (docs/evidence/delivery-checklist.md).
# Every delivery cycle: work through the checklist, then commit ONLY with all
# boxes checked (this script exits 1 otherwise). When the cycle completes
# (deployed + runtime-verified) run with -Reset to start the next cycle clean.
#
# Usage:
#   powershell -File tools/check-delivery.ps1          # check: exit 1 on any unchecked box
#   powershell -File tools/check-delivery.ps1 -Reset   # uncheck every box (new cycle)

param([switch]$Reset)

$ErrorActionPreference = "Stop"

$checklist = Join-Path $PSScriptRoot "..\docs\evidence\delivery-checklist.md"
if (-not (Test-Path $checklist)) {
    Write-Error "delivery checklist not found: $checklist"
    exit 1
}

if ($Reset) {
    $lines = Get-Content $checklist -Encoding UTF8
    $changed = 0
    for ($i = 0; $i -lt $lines.Count; $i++) {
        if ($lines[$i] -match '^\s*- \[[x ]\]') {
            $lines[$i] = $lines[$i] -replace '\[[x ]\]', '[ ]'
            $changed++
        }
    }
    [System.IO.File]::WriteAllLines($checklist, [string[]]$lines,
        (New-Object System.Text.UTF8Encoding($false)))
    Write-Host "Delivery checklist reset ($changed boxes)." -ForegroundColor Yellow
    exit 0
}

$unchecked = @()
$checked = 0
$forbiddenChecked = @()
foreach ($line in Get-Content $checklist -Encoding UTF8) {
    if ($line -match '^\s*- \[ \]') {
        if ($line -match 'FORBIDDEN') { continue } # honey-pot: never a task, only a trap — an unchecked forbidden box is not an incomplete step
        if ($line -match 'Release-cycle deployment/acceptance') { continue } # user release action, not a development commit gate
        $unchecked += $line.Trim()
    }
    elseif ($line -match '^\s*- \[x\]') {
        $checked++
        if ($line -match 'FORBIDDEN') {
            $forbiddenChecked += $line.Trim()
        }
    }
}

if ($forbiddenChecked.Count -gt 0) {
    Write-Host "Delivery gate FAILED - FORBIDDEN box(es) checked:" -ForegroundColor Red
    foreach ($item in $forbiddenChecked) {
        Write-Host "  - $item" -ForegroundColor Red
    }
    exit 1
}

if ($unchecked.Count -gt 0) {
    Write-Host "Delivery gate FAILED - $($unchecked.Count) unchecked box(es):" -ForegroundColor Red
    foreach ($item in $unchecked) {
        Write-Host "  - $item" -ForegroundColor Red
    }
    Write-Host "Check the boxes in docs/delivery-checklist.md as you complete each step." -ForegroundColor Yellow
    exit 1
}

Write-Host "Delivery gate passed ($checked boxes checked)." -ForegroundColor Green
exit 0
