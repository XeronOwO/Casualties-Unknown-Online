# check-item-authority.ps1 - Phase B write-path architecture gate.
# Ensures the legacy item projection tables are only mutated by the projection
# classes, not by adapters/services, so item facts have one authoritative write
# path (the ItemKernelAuthority) and the old tables are rebuildable projections.
$ErrorActionPreference = "Stop"

$root = Split-Path -Parent $PSScriptRoot
$itemDir = Join-Path $root "src\CasualtiesUnknownOnline.Runtime\Session\Items"
$allowed = @(
    "ItemProjection.cs",
    "ItemArbitration.cs",   # transfer-table projection/cache owner (its methods call the kernel first)
    "KernelBatchItemProjection.cs", # Phase C guest batch projection (confirmed batches -> world cache)
    "WorldItemTable.cs"     # table definition
)

$failures = [System.Collections.Generic.List[string]]::new()

Get-ChildItem -Path $itemDir -Filter *.cs -File | ForEach-Object {
    $name = $_.Name
    if ($allowed -contains $name) {
        return
    }

    $text = Get-Content -Path $_.FullName -Raw
    if ($text -match '_worldTable\.(Set|Remove|Clear|RegisterIfAbsent)') {
        $failures.Add("$name mutates WorldItemTable directly; route through ItemProjection")
    }

    if ($text -match '_transferred\s*[\[]|_transferred\.') {
        $failures.Add("$name mutates the transfer table directly; route through ItemArbitration (projection owner)")
    }
}

if ($failures.Count -gt 0) {
    Write-Host "Item authority gate FAILED:" -ForegroundColor Red
    foreach ($failure in $failures) {
        Write-Host "  - $failure" -ForegroundColor Red
    }
    exit 1
}

Write-Host "Item authority gate passed."
exit 0
