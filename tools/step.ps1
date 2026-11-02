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

if (-not $PSBoundParameters.ContainsKey('Enter') -or $Enter -lt 0) {
    Write-Host 'usage: tools/step.ps1 -Enter <step>   |   tools/step.ps1 -Check'
    exit 2
}

$committed = Get-ChildItem -LiteralPath $rules -Filter ('{0:D2}-*.md' -f $Enter) | Where-Object { $_.Name -notlike '*.local.md' } | Select-Object -First 1
if ($null -eq $committed) {
    Write-Host "step: no rule file for step $Enter under docs/development/rules"
    exit 2
}

$local = Join-Path $rules ($committed.BaseName + '.local.md')

# THE TOKEN. It is printed in the MIDDLE of the file, and the recorder refuses a step without it: a caller who pipes this output
# through `Select-Object -Last 2` sees the end marker and the record line, but never the token, so the step cannot be recorded and
# the pre-commit hook refuses the change. That is the difference between a rule in a file nobody reads and a rule the command
# enforces. Measured 2026-11-03, by the user: every round for more than a day piped this printer into a tail, so the step files were
# printed and never seen.
$content = Get-Content -LiteralPath $committed.FullName -Encoding utf8
$tokenSource = [System.IO.File]::ReadAllText($committed.FullName) + "|" + $Enter
$sha = [System.Security.Cryptography.SHA256]::Create().ComputeHash([System.Text.Encoding]::UTF8.GetBytes($tokenSource))
$token = ([System.BitConverter]::ToString($sha) -replace '-', '').Substring(0, 10).ToLowerInvariant()
$tokenLine = [int][Math]::Min([Math]::Max(3, [Math]::Floor($content.Count / 2)), $content.Count)

Write-Host "===== step $Enter — $($committed.Name) ($($content.Count) lines) ====="
for ($i = 0; $i -lt $content.Count; $i++) {
    Write-Host $content[$i]
    if ($i -eq ($tokenLine - 1)) {
        Write-Host ""
        Write-Host "STEP-TOKEN: $token   (required to record this step; it sits here, in the middle, so a tail cannot carry it)"
        Write-Host ""
    }
}

Write-Host "===== step $Enter — $(Split-Path -Leaf $local) ====="
if (Test-Path -LiteralPath $local) {
    Get-Content -LiteralPath $local -Encoding utf8 | ForEach-Object { Write-Host $_ }
    Write-Host "===== end of step $Enter ($($content.Count) + $((Get-Content -LiteralPath $local).Count) lines) ====="
}
else {
    Write-Host '(no local sibling on this checkout)'
}

# NOT recorded here on purpose. The record is a SECOND command, typed with the token the print put in the middle of its output:
#   tools/check-step-read.ps1 -Record '<step>' -Token '<the STEP-TOKEN line above>'
# A piped print never shows that line, so a piped print cannot be recorded, and the pre-commit hook refuses the change until it is.
Write-Host ""
Write-Host "Next, and only with the token above: tools/check-step-read.ps1 -Record '$Enter' -Token '<STEP-TOKEN>'"
