# Extracts the [ItemTrace] operation lines from a CUO log — the regression
# diff gate for the item-sync refactor: after each step, run the same gesture
# battery on both sides and diff the traces (host vs guest, or new vs baseline).
# The same script normalizes the SIMULATION's trace: ReplayRunner emits
# SimTraces/{file}.trace with the same [ItemTrace] prefix (test output
# directory), so running this on the simulation output and diffing it against
# the game's real trace of the same gesture sequence is the simulation-fidelity
# check (the ps1 drops origin/item — the simulation has no hook chain; the
# result/events sequences line up).
#
# Usage:
#   powershell -File tools/extract-itemtrace.ps1 -Log <path-to-latest.log>
#     -Raw: print every [ItemTrace] line as-is (default: op, result and events only)
#     -NoBegins: drop begin lines (assert "every cross-frame op resolved" by
#       diffing with this flag: a baseline with no unmatched begins)
param(
    [Parameter(Mandatory = $true)][string]$Log,
    [switch]$Raw,
    [switch]$NoBegins
)

$lines = Get-Content $Log | Where-Object { $_ -match '\[ItemTrace\]' }
if (-not $lines) { Write-Output "(no [ItemTrace] lines)"; exit 0 }

foreach ($line in $lines) {
    if ($Raw) { Write-Output $line; continue }
    if ($NoBegins -and $line -match 'begin ') { continue }
    # Normalize: keep op, result and events, drop timestamps/thread/category noise.
    if ($line -match 'op=(\d+) .*result=([^ ]+).*events=\[([^\]]*)\]') {
        Write-Output ("op={0} result={1} events=[{2}]" -f $matches[1], $matches[2], $matches[3])
    }
    elseif ($line -match 'op=(\d+) begin ') {
        Write-Output ("op={0} begin" -f $matches[1])
    }
}
