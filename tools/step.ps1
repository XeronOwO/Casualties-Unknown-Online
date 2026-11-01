# Enter a step: print its two rule files IN FULL, then record them for this round.
#
# Why this exists and not a reminder: the rule ("the step you walk is read first") kept being broken because nothing in the loop
# asked for it. This script makes the read the FIRST act of the step and leaves a record a pre-commit hook can check, so the
# failure is mechanical instead of a matter of remembering.
#
# Usage: tools/step.ps1 -Enter 7      (prints 07-implement.md + its .local.md, records both)
#        tools/step.ps1 -Check       (shows this round's record)

param(
    [int]$Enter,
    [switch]$Check,
    [string]$RecordPath = '.acceptance/step-reads.txt'
)

$ErrorActionPreference = 'Stop'
$repo = Split-Path -Parent $PSScriptRoot
$rules = Join-Path $repo 'docs/development/rules'

if ($Check) {
    $path = Join-Path $repo $RecordPath
    if (-not (Test-Path -LiteralPath $path)) { Write-Host 'step: no events recorded this round'; exit 0 }
    Get-Content -LiteralPath $path -Encoding utf8 | ForEach-Object { Write-Host $_ }
    exit 0
}

if ($Enter -le 0) {
    Write-Host 'usage: tools/step.ps1 -Enter <step>   |   tools/step.ps1 -Check'
    exit 2
}

$committed = Get-ChildItem -LiteralPath $rules -Filter ('{0:D2}-*.md' -f $Enter) | Where-Object { $_.Name -notlike '*.local.md' } | Select-Object -First 1
if ($null -eq $committed) {
    Write-Host "step: no rule file for step $Enter under docs/development/rules"
    exit 2
}

$local = Join-Path $rules ($committed.BaseName + '.local.md')

Write-Host "===== step $Enter — $($committed.Name) ($((Get-Content -LiteralPath $committed.FullName).Count) lines) ====="
Get-Content -LiteralPath $committed.FullName -Encoding utf8 | ForEach-Object { Write-Host $_ }
Write-Host "===== step $Enter — $(Split-Path -Leaf $local) ====="
if (Test-Path -LiteralPath $local) {
    Get-Content -LiteralPath $local -Encoding utf8 | ForEach-Object { Write-Host $_ }
    Write-Host "===== end of step $Enter ($((Get-Content -LiteralPath $committed.FullName).Count) + $((Get-Content -LiteralPath $local).Count) lines) ====="
}
else {
    Write-Host '(no local sibling on this checkout)'
}

& (Join-Path $PSScriptRoot 'check-step-read.ps1') -Record "$Enter" | Out-Host
exit 0
