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
    if (-not (Test-Path -LiteralPath $path)) {
        $dir = Split-Path -Parent $path
        if (-not (Test-Path -LiteralPath $dir)) { New-Item -ItemType Directory -Path $dir | Out-Null }
        $lines = @("# step-reads for the current round")
    }

    foreach ($step in $recordSteps) {
        $file = Get-RuleFile $step
        if ($null -eq $file) {
            Write-Error "step-reads: step $step has no rule file under docs/development/rules"
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

# Which steps does this change TOUCH? A staged diff decides; an unstaged one (a plain `git status`) counts too, so the check
# runs before anything is staged.
$changed = & git -C $repo status --porcelain | ForEach-Object { $_.Substring(3).Trim() }
$touched = [System.Collections.Generic.SortedSet[int]]::new()
$touched.Add(11) | Out-Null   # every commit is step 11
foreach ($file in $changed) {
    $f = $file -replace '\\', '/'
    if ($f -like 'src/*') { $touched.Add(7) | Out-Null }
    if ($f -like 'tests/*') { $touched.Add(7) | Out-Null; $touched.Add(8) | Out-Null }
    if ($f -like 'docs/design/*') { $touched.Add(2) | Out-Null }
    if ($f -like 'tools/*') { $touched.Add(13) | Out-Null }
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
    Write-Host 'Read the file(s), then run: tools/check-step-read.ps1 -Record <steps>'
    exit 1
}

Write-Host ('check-step-read: every touched step is recorded ({0})' -f (($touched | ForEach-Object { '{0:D2}' -f $_ }) -join ', '))
exit 0
