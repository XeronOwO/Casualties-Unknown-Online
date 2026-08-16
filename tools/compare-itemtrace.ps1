# compare-itemtrace.ps1 - the real-log vs replay SimTrace diff automation
# (the OperationTrace->replay loop's last link). The replay theory writes one
# SimTraces/<file>.trace per replay file (raw OperationTrace-format lines, see
# SimTrace.cs); a real BepInEx latest.log carries the game's [ItemTrace] lines
# for the same gesture sequence. This script normalizes both sides with the
# SAME fidelity surface extract-itemtrace.ps1 establishes (origin/item/op ids
# are dropped; begin-event, result and the event chain are compared) and then
# matches the expected replay sequence inside the real log.
#
# Usage:
#   powershell -File tools/compare-itemtrace.ps1 -RealLog <path-to-latest.log-or-.log.gz> -Replay drop-repick-cycle
#   powershell -File tools/compare-itemtrace.ps1 -RealLog <latest.log> -Replay <file>.replay -Refresh
#   powershell -File tools/compare-itemtrace.ps1 -RealLog <latest.log> -Replay <file>.trace -NoBegins
#
# Matching modes:
#   default       the replay token sequence must appear as a SUBSEQUENCE of the
#                 real log's tokens (a whole-session log may contain unrelated
#                 operations before/between/after the gesture battery).
#   -Contiguous   the replay token sequence must appear as one consecutive run
#                 (no unrelated ItemTrace lines interleaved).
#   -Strict       the two token sequences must be exactly equal (the real log
#                 was already windowed to the gesture battery).
#   -NoBegins     compare end lines only (the extract-itemtrace.ps1 -NoBegins
#                 surface: "every cross-frame op resolved" result-sequence diff).
#                 With -NoBegins, begin-without-end leak detection is disabled.
#
# Leaks: an unresolved begin in the EXPECTED replay trace is always a failure
# (the same contract ReplayTests asserts). An unresolved begin in the REAL log
# is a warning by default (the session may have ended mid-operation) and a
# failure with -FailOnLeak.
param(
    [Parameter(Mandatory = $true)][string]$RealLog,
    [Parameter(Mandatory = $true)][string]$Replay,
    [string]$SimTrace,
    [string]$Configuration = "Debug",
    [switch]$NoBegins,
    [switch]$Contiguous,
    [switch]$Strict,
    [switch]$FailOnLeak,
    [switch]$Show,
    [switch]$Refresh,
    [string]$TestFilter = "FullyQualifiedName~ReplayTests.Replay"
)

$ErrorActionPreference = "Stop"
$root = Split-Path -Parent $PSScriptRoot

function Fail([string]$message) {
    Write-Host "SIMTRACE DIFF FAILED: $message" -ForegroundColor Red
    exit 1
}

function Read-TraceLines {
    param([string]$Path)

    if ($Path -notmatch '\.gz$') {
        return [System.IO.File]::ReadAllLines($Path)
    }

    $file = [System.IO.File]::OpenRead($Path)
    try {
        $gzip = New-Object System.IO.Compression.GZipStream($file, [System.IO.Compression.CompressionMode]::Decompress)
        try {
            $reader = New-Object System.IO.StreamReader($gzip, [System.Text.Encoding]::UTF8)
            try {
                return $reader.ReadToEnd() -split "\r?\n"
            }
            finally {
                $reader.Dispose()
            }
        }
        finally {
            $gzip.Dispose()
        }
    }
    finally {
        $file.Dispose()
    }
}

function ConvertTo-ItemTraceTokens {
    param([string]$Path, [bool]$IncludeBegins)

    if (-not (Test-Path -LiteralPath $Path -PathType Leaf)) {
        Fail "trace file not found: $Path"
    }

    $tokens = New-Object System.Collections.Generic.List[object]
    $pending = @{}
    $beginPattern = [regex]'\[ItemTrace\] op=(\d+) begin .*event=(\S+)'
    $endPattern = [regex]'\[ItemTrace\] op=(\d+) .*result=(\S+).*events=\[([^\]]*)\]'

    $lineNo = 0
    foreach ($line in @(Read-TraceLines -Path $Path)) {
        $lineNo++
        if ($line -notmatch '\[ItemTrace\]') { continue }

        $begin = $beginPattern.Match($line)
        if ($begin.Success) {
            $op = [long]$begin.Groups[1].Value
            $event = $begin.Groups[2].Value
            $pending[$op] = $event
            if ($IncludeBegins) {
                $tokens.Add([pscustomobject]@{ Text = "begin:$event"; Line = $lineNo })
            }
            continue
        }

        $end = $endPattern.Match($line)
        if ($end.Success) {
            $op = [long]$end.Groups[1].Value
            if ($pending.ContainsKey($op)) {
                $pending.Remove($op)
            }

            $text = "end:$($end.Groups[2].Value):$($end.Groups[3].Value)"
            $tokens.Add([pscustomobject]@{ Text = $text; Line = $lineNo })
        }
    }

    return [pscustomobject]@{
        Tokens = $tokens.ToArray()
        PendingOps = $pending
    }
}

function Find-ContiguousMatch {
    param([object[]]$Expected, [object[]]$Actual)

    $maxStart = $Actual.Count - $Expected.Count
    for ($start = 0; $start -le $maxStart; $start++) {
        $match = $true
        for ($i = 0; $i -lt $Expected.Count; $i++) {
            if ($Actual[$start + $i].Text -cne $Expected[$i].Text) {
                $match = $false
                break
            }
        }

        if ($match) {
            return [pscustomobject]@{
                IsMatch = $true
                Start = $start
                End = $start + $Expected.Count - 1
            }
        }
    }

    return [pscustomobject]@{ IsMatch = $false; Start = -1; End = -1 }
}

function Find-SubsequenceMatch {
    param([object[]]$Expected, [object[]]$Actual)

    $expectedIndex = 0
    $start = -1
    for ($actualIndex = 0; $actualIndex -lt $Actual.Count; $actualIndex++) {
        if ($Actual[$actualIndex].Text -cne $Expected[$expectedIndex].Text) { continue }

        if ($expectedIndex -eq 0) { $start = $actualIndex }
        $expectedIndex++
        if ($expectedIndex -eq $Expected.Count) {
            return [pscustomobject]@{
                IsMatch = $true
                Start = $start
                End = $actualIndex
            }
        }
    }

    return [pscustomobject]@{ IsMatch = $false; Start = -1; End = -1 }
}

function Write-TokenList {
    param([object[]]$Tokens, [int]$MaxLines = 40)

    if ($Tokens.Count -le $MaxLines) {
        for ($i = 0; $i -lt $Tokens.Count; $i++) {
            Write-Host ("    {0,4}: line {1}: {2}" -f ($i + 1), $Tokens[$i].Line, $Tokens[$i].Text)
        }
        return
    }

    $half = [int]($MaxLines / 2)
    for ($i = 0; $i -lt $half; $i++) {
        Write-Host ("    {0,4}: line {1}: {2}" -f ($i + 1), $Tokens[$i].Line, $Tokens[$i].Text)
    }

    Write-Host "    ... ($($Tokens.Count - $MaxLines) more tokens omitted; pass -Show for the full list)"
    for ($i = $Tokens.Count - $half; $i -lt $Tokens.Count; $i++) {
        Write-Host ("    {0,4}: line {1}: {2}" -f ($i + 1), $Tokens[$i].Line, $Tokens[$i].Text)
    }
}

if (-not (Test-Path -LiteralPath $RealLog -PathType Leaf)) {
    Fail "real log not found: $RealLog"
}

if ($Contiguous -and $Strict) {
    Fail "-Contiguous and -Strict are mutually exclusive (strict already requires exact equality)"
}

# Resolve the SimTrace file. -Replay may be a .replay path/name (the trace is
# <name>.trace) or a generated .trace path directly. Only the former can be
# auto-generated with -Refresh; an explicit trace path must already exist.
$replayName = ""
$tracePath = ""
$canGenerate = $false
if (-not [string]::IsNullOrWhiteSpace($SimTrace)) {
    $tracePath = $SimTrace
    $replayName = [System.IO.Path]::GetFileName($SimTrace)
}
elseif ($Replay -match '\.trace$') {
    $tracePath = $Replay
    $replayName = [System.IO.Path]::GetFileName($Replay)
}
else {
    $replayName = [System.IO.Path]::GetFileName($Replay)
    if ($replayName -notmatch '\.replay$') {
        $replayName = "$replayName.replay"
    }

    $canGenerate = $true
    $traceName = "$replayName.trace"
    $preferred = Join-Path $root "tests\CasualtiesUnknownOnline.Tests\bin\$Configuration\net48\SimTraces\$traceName"
    if (Test-Path -LiteralPath $preferred -PathType Leaf) {
        $tracePath = $preferred
    }
    else {
        $candidates = @(Get-ChildItem -Path (Join-Path $root "tests") -Recurse -Filter $traceName -File -ErrorAction SilentlyContinue |
            Where-Object { $_.DirectoryName -match '\\bin\\[^\\]+\\[^\\]+\\SimTraces$' } |
            Sort-Object LastWriteTime -Descending)
        if ($candidates.Count -gt 0) {
            $tracePath = $candidates[0].FullName
        }
    }
}

if ($canGenerate -and $Refresh) {
    Write-Host "Generating SimTrace(s): dotnet test $root\CasualtiesUnknownOnline.slnx -c $Configuration --filter $TestFilter"
    $sln = Join-Path $root "CasualtiesUnknownOnline.slnx"
    & dotnet test $sln -c $Configuration --filter $TestFilter
    if ($LASTEXITCODE -ne 0) {
        Fail "dotnet test replay theory failed (exit code $LASTEXITCODE) - fix the replay tests before diffing"
    }

    $traceName = "$replayName.trace"
    $preferred = Join-Path $root "tests\CasualtiesUnknownOnline.Tests\bin\$Configuration\net48\SimTraces\$traceName"
    if (Test-Path -LiteralPath $preferred -PathType Leaf) {
        $tracePath = $preferred
    }
    else {
        $candidates = @(Get-ChildItem -Path (Join-Path $root "tests") -Recurse -Filter $traceName -File -ErrorAction SilentlyContinue |
            Where-Object { $_.DirectoryName -match '\\bin\\[^\\]+\\[^\\]+\\SimTraces$' } |
            Sort-Object LastWriteTime -Descending)
        if ($candidates.Count -gt 0) {
            $tracePath = $candidates[0].FullName
        }
    }
}

if ([string]::IsNullOrWhiteSpace($tracePath) -or -not (Test-Path -LiteralPath $tracePath -PathType Leaf)) {
    if ($canGenerate -and -not $Refresh) {
        Fail "SimTrace not found for '$replayName'. Run the replay theory first (dotnet test CasualtiesUnknownOnline.slnx --filter '$TestFilter') or re-run with -Refresh."
    }

    Fail "SimTrace not found: $(if ($tracePath) { $tracePath } else { $Replay })"
}

$includeBegins = -not $NoBegins
$real = ConvertTo-ItemTraceTokens -Path $RealLog -IncludeBegins $includeBegins
$expected = ConvertTo-ItemTraceTokens -Path $tracePath -IncludeBegins $includeBegins

if ($expected.Tokens.Count -eq 0) {
    Fail "expected SimTrace has no [ItemTrace] lines: $tracePath"
}

if ($real.Tokens.Count -eq 0) {
    Fail "real log has no [ItemTrace] lines: $RealLog"
}

if ($includeBegins -and $expected.PendingOps.Count -gt 0) {
    $ops = ($expected.PendingOps.Keys | Sort-Object | ForEach-Object { "op=$_ ($($expected.PendingOps[$_]))" }) -join ", "
    Fail "expected SimTrace has begin-without-end leak(s): $ops"
}

if ($includeBegins -and $real.PendingOps.Count -gt 0) {
    $ops = ($real.PendingOps.Keys | Sort-Object | ForEach-Object { "op=$_ ($($real.PendingOps[$_]))" }) -join ", "
    if ($FailOnLeak) {
        Fail "real log has begin-without-end leak(s): $ops"
    }

    Write-Host "WARNING: real log has $($real.PendingOps.Count) begin-without-end leak(s): $ops" -ForegroundColor Yellow
}

$mode = "subsequence"
if ($Strict) { $mode = "strict" }
elseif ($Contiguous) { $mode = "contiguous" }
if ($NoBegins) { $mode = "$mode (begins ignored)" }

if ($Show) {
    Write-Host "Expected $($expected.Tokens.Count) tokens ($tracePath):" -ForegroundColor Cyan
    Write-TokenList -Tokens $expected.Tokens
    Write-Host "Real $($real.Tokens.Count) tokens ($RealLog):" -ForegroundColor Cyan
    Write-TokenList -Tokens $real.Tokens
}

$match = $null
if ($Strict) {
    $equal = $expected.Tokens.Count -eq $real.Tokens.Count
    if ($equal) {
        for ($i = 0; $i -lt $expected.Tokens.Count; $i++) {
            if ($expected.Tokens[$i].Text -cne $real.Tokens[$i].Text) {
                $equal = $false
                break
            }
        }
    }

    if ($equal) {
        $match = [pscustomobject]@{ IsMatch = $true; Start = 0; End = $real.Tokens.Count - 1 }
    }
}
elseif ($Contiguous) {
    $match = Find-ContiguousMatch -Expected $expected.Tokens -Actual $real.Tokens
}
else {
    $match = Find-SubsequenceMatch -Expected $expected.Tokens -Actual $real.Tokens
}

if ($null -eq $match) {
    $match = [pscustomobject]@{ IsMatch = $false; Start = -1; End = -1 }
}

if ($match.IsMatch) {
    Write-Host ("SIMTRACE DIFF PASSED: {0} matches {1} ({2} mode; {3} expected tokens at real-trace tokens {4}-{5}, original log lines {6}-{7})." -f `
        $replayName, (Split-Path -Leaf $RealLog), $mode, $expected.Tokens.Count,
        ($match.Start + 1), ($match.End + 1),
        $real.Tokens[$match.Start].Line, $real.Tokens[$match.End].Line) -ForegroundColor Green
    exit 0
}

Write-Host "SIMTRACE DIFF FAILED: $replayName does not match $(Split-Path -Leaf $RealLog) ($mode mode)." -ForegroundColor Red
Write-Host "Expected sequence ($($expected.Tokens.Count) tokens):" -ForegroundColor Red
Write-TokenList -Tokens $expected.Tokens
Write-Host "Real sequence ($($real.Tokens.Count) tokens):" -ForegroundColor Red
Write-TokenList -Tokens $real.Tokens
Write-Host "Use -NoBegins for the result-only surface, or -Strict/-Contiguous for stronger matching." -ForegroundColor Yellow
exit 1
