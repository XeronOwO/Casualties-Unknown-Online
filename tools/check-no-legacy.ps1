# check-no-legacy.ps1 - Phase E guard for remaining dual-architecture markers.
# Fails when production source still contains a legacy/shadow/compat type marker
# or a known old direct-result/removed NetMsg reference. This is the automated
# part of architecture-guards.md item 10.
$ErrorActionPreference = "Stop"

$root = Split-Path -Parent $PSScriptRoot
$src = Join-Path $root "src"
$failures = [System.Collections.Generic.List[string]]::new()

# Uppercase type-style markers. Lowercase uses such as "legacy log path" and
# "compatibility" are not markers of dual architecture; they are ordinary words.
$typeMarkers = @("Shadow", "Legacy", "Compat", "Dual")

# Known removed direct-result/high-frequency/legacy wire references. If a file
# contains one of these strings it is either stale documentation or resurrected
# dead wire, both of which Phase E rejects.
$removedWireMarkers = @(
    "NetMsg.PlayerState",
    "NetMsg.PlayerStateReport",
    "NetMsg.EnemyState",
    "NetMsg.PlayerCarryState",
    "NetMsg.PlayerInventoryTransfer",
    "NetMsg.PlayerHealResult",
    "NetMsg.PlayerItemUseResult",
    "NetMsg.EnemyBite",
    "NetMsg.EnemyLunge",
    "NetMsg.EnemyEffect",
    "NetMsg.EnemyRemoved",
    "NetMsg.WorldStartParams",
    "NetMsg.TrapStateSnapshot",
    "NetMsg.OpenedEntitiesSnapshot",
    "NetMsg.BuildingEntityHealthSnapshot",
    "ItemCheckpointStore",
    "KernelShadow",
    "KernelForDiagnostics",
    "ItemDiagnosticsProjection",
    "NetMsg.ItemReject"
)

# ItemReject is the single documented legacy item-frame survivor, used only for
# block-break drop refusal. It may appear only in these two files.
$itemRejectAllowedFiles = @("ItemMessageFlowService.cs", "ItemRejectHandler.cs")

Get-ChildItem -Path $src -Filter *.cs -Recurse | ForEach-Object {
    $relative = $_.FullName.Substring($root.Length + 1)
    $text = [System.IO.File]::ReadAllText($_.FullName)

    foreach ($marker in $typeMarkers) {
        $pattern = "(?im)^\s*(public\s+|internal\s+|private\s+|protected\s+|static\s+|sealed\s+|abstract\s+|partial\s+)*(class|record|struct|interface|enum)\s+$marker[A-Za-z0-9_]*"
        if ($text -match $pattern) {
            $failures.Add("$relative contains dual-architecture type declaration '$marker'")
        }
    }

    foreach ($marker in $removedWireMarkers) {
        if ($marker -eq "NetMsg.ItemReject" -and $itemRejectAllowedFiles -contains $_.Name) {
            continue
        }

        if ($text.Contains($marker)) {
            $failures.Add("$relative contains removed wire marker '$marker'")
        }
    }
}

if ($failures.Count -gt 0) {
    Write-Host "No-legacy gate FAILED:" -ForegroundColor Red
    foreach ($failure in $failures) {
        Write-Host "  - $failure" -ForegroundColor Red
    }
    exit 1
}

Write-Host "No-legacy gate passed."
exit 0
