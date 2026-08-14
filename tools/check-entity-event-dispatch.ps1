# check-entity-event-dispatch.ps1 - hard gate for the entity-event dispatch
# completeness across the three Game Adapter dispatch tables that L0 tests
# cannot reach (they reference Unity; the test project deliberately does not
# reference GameAdapter). Mirrors check-architecture.ps1 / check-event-replay.ps1:
# run before EVERY commit, exit 1 on violation.
#
# Rule: every EntityEventKind enum member must be dispatched in EACH of the
# three tables, and no table may reference a kind the enum does not define:
#   1. TrapEntityScan.Rows      - the layout scan (component type -> kinds);
#      a missing kind is never scanned, so the host never publishes its
#      layout entry and the guest never materializes/replays it.
#   2. TrapEffectApplier.ApplyEvent - the host executor; a missing kind falls
#      into `default:` ("no host executor") and is silently dropped.
#   3. TrapVisualReplay.Replay  - the guest replay; a missing kind falls into
#      `default:` ("no replay action") and is silently dropped.
#
# The enum is the single source of truth: a new kind added to EntityEventKind
# but forgotten in any one table fails this gate BEFORE the game launches.
# This is the same defense as DirectionTests' reflection guard and the
# EntityEventArchives completeness guard, applied where reflection cannot go
# (a switch statement's case labels and a Func<Component, EntityEventKind[]>
# closure are not enumerable at runtime).

$ErrorActionPreference = "Stop"

$root = Split-Path -Parent $PSScriptRoot
$enumFile = Join-Path $root "src\CasualtiesUnknownOnline.Runtime\Protocol\EntityEventKind.cs"
$dispatchFiles = @(
    (Join-Path $root "src\CasualtiesUnknownOnline.GameAdapter\World\TrapEntityScan.cs"),
    (Join-Path $root "src\CasualtiesUnknownOnline.GameAdapter\World\TrapEffectApplier.cs"),
    (Join-Path $root "src\CasualtiesUnknownOnline.GameAdapter\World\TrapVisualReplay.cs")
)

if (-not (Test-Path $enumFile)) {
    Write-Error "entity-event enum not found: $enumFile"
    exit 1
}

# 1. The enum member set (the source of truth).
$enumMembers = New-Object System.Collections.Generic.HashSet[string]
foreach ($line in Get-Content $enumFile) {
    $m = [regex]::Match($line, '^\s*([A-Za-z_]\w*)\s*=\s*\d+,')
    if ($m.Success) {
        [void]$enumMembers.Add($m.Groups[1].Value)
    }
}

if ($enumMembers.Count -eq 0) {
    Write-Error "no enum members parsed from $enumFile"
    exit 1
}

$violations = New-Object System.Collections.Generic.List[string]

# 2. Each dispatch table must reference exactly the enum member set.
foreach ($file in $dispatchFiles) {
    if (-not (Test-Path $file)) {
        $violations.Add("dispatch table not found: $file")
        continue
    }

    $relative = $file.Substring($root.Length + 1)
    $referenced = New-Object System.Collections.Generic.HashSet[string]
    foreach ($m in [regex]::Matches((Get-Content $file -Raw), 'EntityEventKind\.(\w+)')) {
        [void]$referenced.Add($m.Groups[1].Value)
    }

    foreach ($member in $enumMembers) {
        if (-not $referenced.Contains($member)) {
            $violations.Add("$relative : EntityEventKind.$member is not dispatched (silent default drop / never scanned)")
        }
    }

    foreach ($ref in $referenced) {
        if (-not $enumMembers.Contains($ref)) {
            $violations.Add("$relative : references EntityEventKind.$ref which is not in the enum (stale/typo)")
        }
    }
}

if ($violations.Count -gt 0) {
    Write-Host "ENTITY-EVENT DISPATCH GATE FAILED:" -ForegroundColor Red
    foreach ($v in $violations) {
        Write-Host "  - $v" -ForegroundColor Red
    }
    exit 1
}

Write-Host "Entity-event dispatch gate passed ($($enumMembers.Count) kinds x $($dispatchFiles.Count) tables)." -ForegroundColor Green
exit 0
