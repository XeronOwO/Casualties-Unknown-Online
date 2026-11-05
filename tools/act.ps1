# act.ps1 - the ONLY way to enter a step, and the thing that makes work impossible without it.
#
# It prints the step's two rule files IN FULL, then writes the marker that `require-active-step.ps1` demands: the build
# (Directory.Build.targets), the deploy and the acceptance driver all refuse while no fresh marker names their step. So the rules are
# not a document to remember - they are the key the work needs.
#
# Usage: tools/act.ps1 -Step 7                 (print the rules, arm the step)
#        tools/act.ps1 -Step 13 -Then "..."    (print the rules, arm, then run one command)

param(
    [Parameter(Mandatory = $true)]
    [int]$Step,
    [string]$Then,
    [string]$MarkerPath = '.acceptance/active-step.txt'
)

$ErrorActionPreference = 'Stop'
$repo = Split-Path -Parent $PSScriptRoot
$rules = Join-Path $repo 'docs/development/rules'
$recordPath = Join-Path $repo '.acceptance/step-reads.txt'

# THE ROUND BOUNDARY. A round starts when step 0 is printed: the record is truncated first, so a line left by an earlier
# round can never open a gate (measured 2026-11-05: a commit moved backlog files on a record whose own header still said
# two days earlier). Every other step refuses until the round is open - "read at the moment you enter" needs a "now".
$roundHeader = '^# step-reads for the current round'
$roundOpen = $false
if (Test-Path -LiteralPath $recordPath) {
    $roundOpen = [bool](@(Get-Content -LiteralPath $recordPath -Encoding utf8 | Where-Object { $_ -match $roundHeader }).Count)
}

if ($Step -eq 0) {
    & powershell -NoProfile -ExecutionPolicy Bypass -File (Join-Path $PSScriptRoot 'check-step-read.ps1') -NewRound
    if ($LASTEXITCODE -ne 0) { exit $LASTEXITCODE }
    Write-Host 'act: a NEW ROUND starts here - every earlier step record is void. Re-enter each step you will touch.'
}
elseif (-not $roundOpen) {
    Write-Host 'act: REFUSED - no round is open, so this step would be recorded on a record that outlives its round.'
    Write-Host '  A round starts when step 0 is printed:'
    Write-Host '    powershell -ExecutionPolicy Bypass -File tools/act.ps1 -Step 0'
    exit 2
}

$committed = Get-ChildItem -LiteralPath $rules -Filter ('{0:D2}-*.md' -f $Step) | Where-Object { $_.Name -notlike '*.local.md' } | Select-Object -First 1
if ($null -eq $committed) {
    Write-Host "act: no rule file for step $Step"
    exit 2
}

$local = Join-Path $rules ($committed.BaseName + '.local.md')
$content = Get-Content -LiteralPath $committed.FullName -Encoding utf8

Write-Host "===== step $Step - $($committed.Name) ($($content.Count) lines) ====="
$content | ForEach-Object { Write-Host $_ }
Write-Host "===== step $Step - $(Split-Path -Leaf $local) ====="
if (Test-Path -LiteralPath $local) {
    Get-Content -LiteralPath $local -Encoding utf8 | ForEach-Object { Write-Host $_ }
    Write-Host "===== end of step $Step ($($content.Count) + $((Get-Content -LiteralPath $local).Count) lines) ====="
}
else {
    Write-Host '(no local sibling on this checkout)'
}

$sha = (Get-FileHash -LiteralPath $committed.FullName -Algorithm SHA256).Hash.Substring(0, 12)
$marker = Join-Path $repo $MarkerPath
$dir = Split-Path -Parent $marker
if (-not (Test-Path -LiteralPath $dir)) { New-Item -ItemType Directory -Path $dir | Out-Null }
# The marker accumulates: a round walks several steps, and each one stays armed for the window. One entry per line, newest last.
$entries = @()
if (Test-Path -LiteralPath $marker) {
    $entries = @(Get-Content -LiteralPath $marker -Encoding utf8 | Where-Object { $_ -and $_ -notmatch '^#' -and $_ -match '^\d+\|' })
}
$entries = @($entries | Where-Object { $_ -notmatch ('^{0}\|' -f $Step) })
$entries += ('{0}|{1}|{2}' -f $Step, $sha, [datetime]::UtcNow.ToString('o', [System.Globalization.CultureInfo]::InvariantCulture))
Set-Content -LiteralPath $marker -Encoding utf8 -Value (@('# written by tools/act.ps1 - the marker require-active-step.ps1 demands') + $entries)
Write-Host "act: step $Step is armed ($sha); build, deploy and the acceptance driver accept it for 45 minutes."

# The commit hook's record, written here because this is the command that printed the files: one entry point, two gates armed.
$lines = @()
if (Test-Path -LiteralPath $recordPath) { $lines = @(Get-Content -LiteralPath $recordPath -Encoding utf8) }
$entry = '{0:D2}  {1}  {2}' -f $Step, $sha, $committed.Name
$lines = @($lines | Where-Object { $_ -notmatch ('^{0:D2}\s' -f $Step) }) + $entry
Set-Content -LiteralPath $recordPath -Encoding utf8 -Value $lines
Write-Host "act: step $Step recorded for the commit hook as well."

if ($Then) {
    Write-Host "act: running: $Then"
    & powershell -NoProfile -ExecutionPolicy Bypass -Command $Then
    exit $LASTEXITCODE
}

exit 0
