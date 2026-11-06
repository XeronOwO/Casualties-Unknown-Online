# The goal-completion gate: refuses to let a goal be marked complete while any backlog ticket is still actionable.
#
# A goal may only be completed when the work left in the backlog is work NO agent can do alone -- each remaining ticket must say so
# in its own "- Status:" line, with one of the recorded "parked" markers (blocked on the user, on a human motion, on a build
# capability, or on a fixture only a player can make). Everything else is actionable: an ordinary status, a missing status, or a
# status that still carries a residual row means the agent has work left and must not report completion.
#
# Why a script and not a sentence in the rules: a rule that lives in prose is a rule an agent can skip; a rule that exits 1 is not.
# The check is the last ACTION before `update_goal complete`, and its output is the reading that claim is required to paste.
#
# Markers accepted (case-insensitive, anywhere in the "- Status:" line):
#     blocked on user / blocked by user      the user's decision, look or own pass is required
#     blocked on a human motion              a real hand at the keyboard is required
#     blocked on build capability            this game build does not carry the thing the row needs
#     blocked on a fixture                   the state the row needs cannot be produced from a batch
#     blocked on hardware / on the machine   the machine cannot stage it
# A status that carries a marker AND a residual row (owed / unproven / residual / TODO / pending / still needs) still fails: a
# parked ticket parks the whole ticket, not one row of it.
#
# Usage: pwsh tools/check-goal-completion.ps1 [-Quiet]
# Exit codes: 0 = every remaining ticket names why it is not actionable; 1 = at least one is actionable; 2 = the backlog is missing.
[CmdletBinding()]
param(
    [string] $BacklogRoot,
    [switch] $Quiet
)

$ErrorActionPreference = 'Stop'

$repoRoot = Split-Path -Parent $PSScriptRoot
if ([string]::IsNullOrWhiteSpace($BacklogRoot)) {
    $BacklogRoot = Join-Path $repoRoot 'docs\backlog'
}

if (-not (Test-Path -LiteralPath $BacklogRoot)) {
    Write-Error "backlog root not found: $BacklogRoot"
    exit 2
}

$parkedPattern = '(?i)blocked\s+(on|by)\s+(a\s+|an\s+|the\s+)?(user|human|build|capability|fixture|hardware|machine)'
$residualPattern = '(?i)\b(owed|unproven|residual|TODO|pending|not yet|still needs)\b'
$statusPattern = '^\s*-\s*Status\s*:\s*(?<text>.+)$'

function Read-TicketStatus {
    param([string] $Path)

    foreach ($line in Get-Content -LiteralPath $Path -Encoding UTF8) {
        $match = [regex]::Match($line, $statusPattern)
        if ($match.Success) {
            return $match.Groups['text'].Value.Trim()
        }

        if ($line -match '^\s*##\s') { break }
    }

    return $null
}

$tickets = @()
foreach ($queue in @('todo', 'review')) {
    $directory = Join-Path $BacklogRoot $queue
    if (-not (Test-Path -LiteralPath $directory)) { continue }
    foreach ($file in Get-ChildItem -LiteralPath $directory -Filter '*.md' -File | Sort-Object Name) {
        $status = Read-TicketStatus -Path $file.FullName
        $parked = ($null -ne $status) -and ($status -match $parkedPattern)
        $residual = ($null -ne $status) -and ($status -match $residualPattern)
        $tickets += [pscustomobject]@{
            Queue    = $queue
            Name     = $file.BaseName
            Status   = $status
            Parked   = $parked
            Residual = $residual
        }
    }
}

if ($tickets.Count -eq 0) {
    Write-Output 'goal-completion gate: no ticket remains in todo/ or review/ - the queue is empty, completion is allowed.'
    exit 0
}

$todoCount = @($tickets | Where-Object { $_.Queue -eq 'todo' }).Count
$reviewCount = @($tickets | Where-Object { $_.Queue -eq 'review' }).Count
$offenders = @($tickets | Where-Object { (-not $_.Parked) -or $_.Residual })

if (-not $Quiet) {
    Write-Output ("goal-completion gate: {0} ticket(s) remain ({1} todo, {2} review)" -f $tickets.Count, $todoCount, $reviewCount)
    Write-Output ''
    foreach ($ticket in $tickets) {
        if ($ticket.Residual) {
            $verdict = 'FAIL (residual row)'
        } elseif ($ticket.Parked) {
            $verdict = 'parked'
        } else {
            $verdict = 'FAIL (actionable)'
        }

        $shown = $ticket.Status
        if ($null -eq $shown) { $shown = '(no Status line)' }
        if ($shown.Length -gt 110) { $shown = $shown.Substring(0, 110) + '...' }
        Write-Output ("  [{0}] {1}/{2} - {3}" -f $verdict, $ticket.Queue, $ticket.Name, $shown)
    }

    Write-Output ''
}

if ($offenders.Count -gt 0) {
    Write-Output ("goal-completion gate: REFUSED - {0} ticket(s) are still actionable. Marking the goal complete now would be a false claim." -f $offenders.Count)
    Write-Output 'Give each one a status that says why nobody can do it alone (blocked on user / on a human motion / on build capability / on a fixture), with the reading that proves it, or keep working.'
    foreach ($offender in $offenders) {
        Write-Output ("  - {0}/{1}" -f $offender.Queue, $offender.Name)
    }

    exit 1
}

Write-Output ("goal-completion gate: PASSED - all {0} remaining ticket(s) name why they are not actionable." -f $tickets.Count)
exit 0
