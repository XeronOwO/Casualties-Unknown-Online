# check-reading-evidence.ps1 - the gate against the cheat that matters: claiming a row without a reading.
#
# The step gate makes work impossible without the RULES being printed. This one attacks the other cheat: a ticket row marked PASS (or a
# ticket marked Done) whose text carries no measured value. "It works" is not a reading; a number, a quoted log line, a rect, a count
# is. A row cannot be claimed by typing the word.
#
# What it checks, over the tickets this change touches:
#   1. every row whose verdict cell says pass/PASS must carry a digit or a quoted (`...`) value somewhere in that row;
#   2. a ticket whose Status says Done must carry at least one digit in the body - a done ticket with no measurement anywhere was
#      closed by assertion.
#
# It cannot prove the measurement happened. It can prove that a claim with nothing measured is REFUSED, which is the shape every
# fabricated PASS takes.
#
# Usage: tools/check-reading-evidence.ps1 -WorkingTree

param(
    [switch]$WorkingTree,
    [string]$Base = 'HEAD'
)

$ErrorActionPreference = 'Stop'
$repo = Split-Path -Parent $PSScriptRoot

$changed = & git -C $repo status --porcelain
$tickets = @()
foreach ($line in $changed) {
    if ($line.Length -lt 4) { continue }
    $path = $line.Substring(3).Trim()
    if ($path -notmatch '^docs/backlog/(review|done)/.+\.md$') { continue }
    $full = Join-Path $repo $path
    if (Test-Path -LiteralPath $full) { $tickets += $full }
}

if ($tickets.Count -eq 0) {
    Write-Host 'check-reading-evidence: no ticket changed'
    exit 0
}

$problems = @()
foreach ($file in $tickets) {
    $name = Split-Path -Leaf $file
    $lines = Get-Content -LiteralPath $file -Encoding utf8

    foreach ($line in $lines) {
        # A table row whose verdict cell announces a pass. The cell is the last `|` cell of the row.
        if ($line -notmatch '^\|') { continue }
        if ($line -notmatch '(?i)\bpass\b') { continue }
        # The FIRST cell is the row number and the last is the verdict itself: a digit there proves nothing. What must carry the
        # reading is the rest of the row - the acceptance column and whatever the evidence cell says.
        $cells = @($line.Trim().Trim('|') -split '\|')
        $evidence = (@($cells | Select-Object -Skip 1) -join '|')
        if ($evidence -match '\d') { continue }
        if ($evidence -match '`[^`]+`') { continue }
        $trimmed = $line.Trim()
        if ($trimmed.Length -gt 120) { $trimmed = $trimmed.Substring(0, 120) + '...' }
        $problems += "${name}: a row claims a pass with no measured value: $trimmed"
    }

    $status = ($lines | Where-Object { $_ -match '^- Status:' } | Select-Object -First 1)
    if ($status -and $status -match '(?i)\bDone\b') {
        $body = ($lines -join "`n")
        if ($body -notmatch '\d') {
            $problems += "${name}: marked Done with no digit anywhere in the ticket - closed by assertion."
        }
    }
}

if ($problems.Count -gt 0) {
    Write-Host 'check-reading-evidence: REFUSED'
    $problems | ForEach-Object { Write-Host "  $_" }
    Write-Host ''
    Write-Host 'A row needs a reading: a number, a count, a rect, a quoted log line. "It works" is not one.'
    exit 1
}

Write-Host ('check-reading-evidence: every claimed pass in {0} ticket(s) carries a measured value' -f $tickets.Count)
exit 0
