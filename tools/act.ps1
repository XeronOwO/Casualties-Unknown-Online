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
    [string]$Confirm,
    # THE INTENT. A step is entered with a one-line statement of what is about to be done and which of the step's rules
    # binds it; the step refuses to arm without one. The failure this exists for (2026-11-07): the rules were printed,
    # then the agent acted on a hypothesis whose chain it had never read (user, 2026-11-07: "you never read the code to
    # lay out the full chain; you started from a hunch, so the edit was bound to be wrong", translated).
    # Stating the intent before the rules print is not a promise; it is the plan the rules are read against.
    [string]$Intent,
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

# THE INTENT GATE (2026-11-07). A step is entered with the plan it is about to execute and the rule clause that binds
# it; without that line the step neither prints nor arms. The failure it answers: the rules were printed, then the edit
# was made from a hypothesis whose chain had never been read (user, 2026-11-07: "you never read the code to lay out the
# full chain; you started from a hunch, so the edit was bound to be wrong", translated).
if (-not $Confirm -and $Step -gt 0) {
    if ([string]::IsNullOrWhiteSpace($Intent) -or $Intent.Trim().Length -lt 40) {
        Write-Host 'act: REFUSED - state the intent first: -Intent "<what this step will do, and the rule that binds it>".'
        Write-Host '  One line, at least 40 characters; the step will not print or arm without it.'
        exit 5
    }
}

$committed = Get-ChildItem -LiteralPath $rules -Filter ('{0:D2}-*.md' -f $Step) | Where-Object { $_.Name -notlike '*.local.md' } | Select-Object -First 1
if ($null -eq $committed) {
    Write-Host "act: no rule file for step $Step"
    exit 2
}

$local = Join-Path $rules ($committed.BaseName + '.local.md')
$content = Get-Content -LiteralPath $committed.FullName -Encoding utf8
$sha = (Get-FileHash -LiteralPath $committed.FullName -Algorithm SHA256).Hash.Substring(0, 12)
$tokenPath = Join-Path $repo '.acceptance/step-token.json'

if ($Confirm) {
    # Confirming a read: the token comes from the FIRST line of the last full print. Nothing is printed here on purpose -
    # the whole text already went out in the print command, and a confirm must not become a second, smaller read.
    if (-not (Test-Path -LiteralPath $tokenPath)) {
        Write-Host "act: REFUSED - no read-token was stamped; print the step first."
        exit 3
    }
    $saved = Get-Content -LiteralPath $tokenPath -Encoding utf8 -Raw | ConvertFrom-Json
    if ($saved.step -ne $Step -or $saved.sha -ne $sha -or $saved.token -ne $Confirm) {
        Write-Host "act: REFUSED - the token does not match the last full print of step $Step. Print it again and read it whole."
        exit 3
    }
    $lines = @()
    if (Test-Path -LiteralPath $recordPath) { $lines = @(Get-Content -LiteralPath $recordPath -Encoding utf8) }
    $entry = '{0:D2}  {1}  {2}' -f $Step, $sha, $committed.Name
    $lines = @($lines | Where-Object { $_ -notmatch ('^{0:D2}\s' -f $Step) }) + $entry
    Set-Content -LiteralPath $recordPath -Encoding utf8 -Value $lines
    Write-Host "act: step $Step recorded for the commit hook (read-token confirmed)."
    if ($Then) {
        Write-Host "act: running: $Then"
        & powershell -NoProfile -ExecutionPolicy Bypass -Command $Then
        exit $LASTEXITCODE
    }
    exit 0
}

# THE READ TOKEN. The first line of the print carries a value that exists nowhere else; the record is written only when
# that value is passed back with -Confirm. A piped or truncated print (`| Select-Object -Last 8`) never carries it, so
# the shortcut cannot produce a record - the commit hook then reports the step missing, instead of trusting a print.
$token = -join (1..4 | ForEach-Object { '{0:x2}' -f (Get-Random -Minimum 0 -Maximum 256) })
$tokenDir = Split-Path -Parent $tokenPath
if (-not (Test-Path -LiteralPath $tokenDir)) { New-Item -ItemType Directory -Path $tokenDir | Out-Null }
[pscustomobject]@{ step = $Step; sha = $sha; token = $token; intent = $Intent.Trim(); at = [datetime]::UtcNow.ToString('o', [System.Globalization.CultureInfo]::InvariantCulture) } |
    ConvertTo-Json | Set-Content -LiteralPath $tokenPath -Encoding utf8

Write-Host "===== step $Step [read-token $token] - $($committed.Name) ($($content.Count) lines) ====="
$content | ForEach-Object { Write-Host $_ }
Write-Host "===== step $Step - $(Split-Path -Leaf $local) ====="
if (Test-Path -LiteralPath $local) {
    Get-Content -LiteralPath $local -Encoding utf8 | ForEach-Object { Write-Host $_ }
    Write-Host "===== end of step $Step ($($content.Count) + $((Get-Content -LiteralPath $local).Count) lines) ====="
}
else {
    Write-Host '(no local sibling on this checkout)'
}

# The intent is recorded beside the rules it was written against, and printed after them so the transcript pairs the plan
# with the rules that bind it. A gated build/deploy/driver can read .acceptance/step-intent.txt to echo what was planned.
$intentPath = Join-Path $repo '.acceptance/step-intent.txt'
$intentLines = @()
if (Test-Path -LiteralPath $intentPath) {
    $intentLines = @(Get-Content -LiteralPath $intentPath -Encoding utf8 | Where-Object { $_ -and $_ -notmatch '^#' -and $_ -notmatch ('^{0}\|' -f $Step) })
}
$intentLines += ('{0}|{1}|{2}|{3}' -f $Step, $sha, [datetime]::UtcNow.ToString('o', [System.Globalization.CultureInfo]::InvariantCulture), $Intent.Trim())
Set-Content -LiteralPath $intentPath -Encoding utf8 -Value (@('# written by tools/act.ps1 - the plan each step was entered with') + $intentLines)
Write-Host "act: INTENT for step ${Step}: $($Intent.Trim())"

# THE BEHAVIOUR DIGEST. The rules above are long; what the work will be held to is short. Printed last, after the rules
# and the intent, so the first thing in view when the work starts is the action-level form (2026-11-07: the user asked
# for the rules to carry weight during the work, not to be a dead letter).
$behaviourPath = Join-Path $rules 'behaviour.md'
if (Test-Path -LiteralPath $behaviourPath) {
    Write-Host '===== the behaviours this step is judged by - behaviour.md ====='
    Get-Content -LiteralPath $behaviourPath -Encoding utf8 | ForEach-Object { Write-Host $_ }
}

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

# The record follows the LAST print of the step: an unconfirmed print removes any earlier entry, so a truncated read can
# never ride on an honest one from earlier in the round.
$lines = @()
if (Test-Path -LiteralPath $recordPath) { $lines = @(Get-Content -LiteralPath $recordPath -Encoding utf8) }
$lines = @($lines | Where-Object { $_ -notmatch ('^{0:D2}\s' -f $Step) })
Set-Content -LiteralPath $recordPath -Encoding utf8 -Value $lines
Write-Host "act: step $Step is ARMED but NOT recorded - pass the read-token from the FIRST line of this print:"
Write-Host "act:   powershell -ExecutionPolicy Bypass -File tools/act.ps1 -Step $Step -Confirm <token from the first line>"
Write-Host 'act: a piped or truncated print loses that line, so it cannot make a record.'

if ($Then) {
    Write-Host "act: running: $Then"
    & powershell -NoProfile -ExecutionPolicy Bypass -Command $Then
    exit $LASTEXITCODE
}

exit 0
