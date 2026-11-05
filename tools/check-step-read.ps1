# The step-read gate: a step the round TOUCHES must be recorded as read before the commit goes through.
#
# Why a script and not a promise: the rules say each step walked is read first, and every round that broke it broke it the same
# way - the work pulled straight from "read step 0" into implementation, and no moment ever said "you are now in step 7". A gate
# turns the transition into something that FAILS LOUDLY instead of something that has to be remembered.
#
# What it proves and what it does not: the record is written with the file's SHA-256 as it stood when it was read, so a stale
# record is detectable and a round cannot silently reuse last round's line. It cannot prove the reading happened; it proves the
# round acknowledged the step and froze the text it read.
#
# Usage:
#   tools/check-step-read.ps1 -Record 07,11,13      mark those steps read in THIS round's record
#   tools/check-step-read.ps1 -WorkingTree          refuse when a touched step has no record (run before a commit)
#   tools/check-step-read.ps1 -NewRound             start a fresh record (called at the round's first step-0 read)

param(
    [string]$Record,
    [string]$Token,
    [switch]$WorkingTree,
    [switch]$NewRound,
    [string]$RecordPath = '.acceptance/step-reads.txt'
)

$ErrorActionPreference = 'Stop'
$repo = Split-Path -Parent $PSScriptRoot

function Get-RuleFile([int]$Step) {
    $rules = Join-Path $repo 'docs/development/rules'
    # The committed file, not its `.local.md` sibling: both match the pattern, and the record names the one the rules live in.
    $match = Get-ChildItem -LiteralPath $rules -Filter ('{0:D2}-*.md' -f $Step) | Where-Object { $_.Name -notlike '*.local.md' } | Select-Object -First 1
    if ($null -eq $match) { return $null }
    return $match.FullName
}

# The token `tools/step.ps1` prints in the MIDDLE of a step's output. Recording without it is refused, which is what makes the
# reading portable to a command instead of a memory: a piped tail never carries the middle line, so a piped print cannot be recorded.
function Get-StepToken([int]$Step, [string]$File) {
    $source = [System.IO.File]::ReadAllText($File) + "|" + $Step
    $sha = [System.Security.Cryptography.SHA256]::Create().ComputeHash([System.Text.Encoding]::UTF8.GetBytes($source))
    return ([System.BitConverter]::ToString($sha) -replace '-', '').Substring(0, 10).ToLowerInvariant()
}

# The round boundary. A record is only this round's if the header a round start writes is present; a file the printer kept
# appending to across rounds used to satisfy the hook with lines from days earlier (measured 2026-11-05).
$RoundHeader = '^# step-reads for the current round'
function Test-CurrentRound($recordLines) {
    return (@($recordLines | Where-Object { $_ -match $RoundHeader }).Count -gt 0)
}

if ($NewRound) {
    $dir = Split-Path -Parent (Join-Path $repo $RecordPath)
    if (-not (Test-Path -LiteralPath $dir)) { New-Item -ItemType Directory -Path $dir | Out-Null }
    Set-Content -LiteralPath (Join-Path $repo $RecordPath) -Value "# step-reads for the current round ($(Get-Date -Format 'yyyy-MM-dd HH:mm'))" -Encoding utf8
    Write-Host "step-reads: a new round record was opened"
    exit 0
}

$path = Join-Path $repo $RecordPath
$lines = @()
if (Test-Path -LiteralPath $path) { $lines = Get-Content -LiteralPath $path -Encoding utf8 }

$recordSteps = @()
if ($Record) { $recordSteps = @($Record -split ',' | Where-Object { $_ -match '^\d+$' } | ForEach-Object { [int]$_ }) }
if ($recordSteps.Count -gt 0) {
    if ($recordSteps -contains 0) {
        # Printing step 0 IS the round boundary, so the record starts over here: earlier rounds' lines are void.
        $dir = Split-Path -Parent $path
        if (-not (Test-Path -LiteralPath $dir)) { New-Item -ItemType Directory -Path $dir | Out-Null }
        $lines = @("# step-reads for the current round ($(Get-Date -Format 'yyyy-MM-dd HH:mm'))")
        Write-Host 'step-reads: step 0 opened a new round; earlier entries are void'
    }
    elseif (-not (Test-CurrentRound $lines)) {
        Write-Host 'step-reads: REFUSED to record - no round is open, so this line would outlive the round it belongs to.'
        Write-Host '  A round starts when step 0 is printed: powershell -ExecutionPolicy Bypass -File tools/act.ps1 -Step 0'
        exit 2
    }

    foreach ($step in $recordSteps) {
        $file = Get-RuleFile $step
        if ($null -eq $file) {
            Write-Error "step-reads: step $step has no rule file under docs/development/rules"
            exit 1
        }

        # The token is the proof the print was SEEN, not merely run: it lives in the middle of the output.
        $expectedToken = Get-StepToken $step $file
        if ($Token -ne $expectedToken) {
            Write-Host "step-reads: REFUSED to record step $step - the token does not match."
            Write-Host "  Run the printer BARE and copy the STEP-TOKEN line it prints in the middle:"
            Write-Host "    powershell -ExecutionPolicy Bypass -File tools/step.ps1 -Enter $step"
            exit 1
        }

        $hash = (Get-FileHash -LiteralPath $file -Algorithm SHA256).Hash.Substring(0, 12)
        $name = Split-Path -Leaf $file
        $entry = '{0:D2}  {1}  {2}' -f $step, $hash, $name
        $kept = @($lines | Where-Object { $_ -notmatch ('^{0:D2}\s' -f $step) })
        $kept += $entry
        $lines = $kept
        Write-Host "step-reads: recorded step $step ($hash)"
    }

    Set-Content -LiteralPath $path -Value $lines -Encoding utf8
    exit 0
}

if (-not $WorkingTree) {
    Write-Host 'check-step-read: nothing to do (pass -Record, -WorkingTree or -NewRound)'
    exit 0
}

# A record without a round header belongs to no round: it cannot satisfy this one, whatever it lists.
if (-not (Test-CurrentRound $lines)) { $lines = @() }

# Which steps does this change TOUCH? A staged diff decides; an unstaged one (a plain `git status`) counts too, so the check
# runs before anything is staged.
$changed = & git -C $repo status --porcelain | ForEach-Object { $_.Substring(3).Trim() }
$touched = [System.Collections.Generic.SortedSet[int]]::new()
$touched.Add(11) | Out-Null   # every commit is step 11
foreach ($file in $changed) {
    $f = $file -replace '\\', '/'
    # A source change is implemented AND verified on the real thing, so it walks both steps. Measured 2026-11-03: rounds that changed
    # `src/` read step 7 and never step 8, while making deploy-and-run claims - the mapping, not the diligence, was the hole.
    if ($f -like 'src/*') { $touched.Add(7) | Out-Null; $touched.Add(8) | Out-Null }
    if ($f -like 'tests/*') { $touched.Add(7) | Out-Null; $touched.Add(8) | Out-Null }
    if ($f -like 'docs/design/*') { $touched.Add(2) | Out-Null }
    if ($f -like 'tools/*') { $touched.Add(13) | Out-Null; $touched.Add(8) | Out-Null }
    if ($f -like 'docs/backlog/*') { $touched.Add(12) | Out-Null }
}

$missing = @()
foreach ($step in $touched) {
    $file = Get-RuleFile $step
    if ($null -eq $file) { continue }
    $hash = (Get-FileHash -LiteralPath $file -Algorithm SHA256).Hash.Substring(0, 12)
    $recorded = $lines | Where-Object { $_ -match ('^{0:D2}\s+{1}\s' -f $step, $hash) }
    if (-not $recorded) { $missing += ('{0:D2} {1}' -f $step, (Split-Path -Leaf $file)) }
}

if ($missing.Count -gt 0) {
    Write-Host 'check-step-read: this change touches a step the round has not recorded as read:'
    $missing | ForEach-Object { Write-Host "  missing: $_" }

    # The instruction the user asked for: an interception is not a retry prompt, it is a STOP. The violation is logged so a
    # pattern becomes visible across rounds instead of being felt as bad luck.
    $log = Join-Path $repo '.acceptance/step-read-violations.txt'
    $logDir = Split-Path -Parent $log
    if (-not (Test-Path -LiteralPath $logDir)) { New-Item -ItemType Directory -Path $logDir | Out-Null }
    Add-Content -LiteralPath $log -Value ('{0}  missing: {1}  touched: {2}' -f (Get-Date -Format 'yyyy-MM-dd HH:mm:ss'), ($missing -join '; '), (($touched | ForEach-Object { '{0:D2}' -f $_ }) -join ',')) -Encoding utf8

    Write-Host ''
    Write-Host '================================================================================'
    Write-Host 'STOP. Do not retry this commit.'
    Write-Host ''
    Write-Host 'A step was walked without its rules being read. That is the round failing, not a'
    Write-Host 'gate being fussy. Before anything else:'
    Write-Host ''
    Write-Host '  1. Stop the work in hand. Do not "just record it and move on".'
    Write-Host '  2. Ask WHY this step was entered unread - was it not planned, was the reading'
    Write-Host '     skipped to save a call, or was the step reached by accident mid-item?'
    Write-Host '  3. Strengthen the rule so the same entry cannot happen again: name the steps in'
    Write-Host '     the plan before the first act, and enter each one with tools/step.ps1 -Enter'
    Write-Host '     BEFORE its first action - not before its commit.'
    Write-Host '  4. Only then read the file(s) above and re-run the commit.'
    Write-Host ''
    Write-Host 'This interception is appended to .acceptance/step-read-violations.txt so a pattern'
    Write-Host 'across rounds stays visible.'
    Write-Host '================================================================================'
    exit 1
}

Write-Host ('check-step-read: every touched step is recorded ({0})' -f (($touched | ForEach-Object { '{0:D2}' -f $_ }) -join ', '))
exit 0
