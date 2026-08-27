# check-gamestate-isolation.ps1 - hard gate for the GameState kernel project.
# Exit 1 when the kernel references a forbidden CUO/Unity/BepInEx/Steam/network
# assembly or uses a forbidden ambient namespace. Run before every commit,
# normally reached through tools/check-architecture.ps1.
$ErrorActionPreference = "Stop"

$root = Split-Path -Parent $PSScriptRoot
$projectPath = Join-Path $root "src\CasualtiesUnknownOnline.GameState\CasualtiesUnknownOnline.GameState.csproj"
$failures = [System.Collections.Generic.List[string]]::new()

if (-not (Test-Path $projectPath)) {
    Write-Error "GameState project not found: $projectPath"
    exit 1
}

[xml]$csproj = Get-Content -Path $projectPath -Raw

# 1. Project references: the kernel must not reference another CUO project.
foreach ($ref in $csproj.SelectNodes("//*[local-name()='ProjectReference']")) {
    $include = $ref.GetAttribute("Include")
    $failures.Add("GameState.csproj has forbidden ProjectReference: $include")
}

# 2. Package references: only the net48 reference-assemblies build package is
#    allowed; runtime dependencies are forbidden by design.
$allowedPackages = @("Microsoft.NETFramework.ReferenceAssemblies")
foreach ($pkg in $csproj.SelectNodes("//*[local-name()='PackageReference']")) {
    $include = $pkg.GetAttribute("Include")
    if ($allowedPackages -notcontains $include) {
        $failures.Add("GameState.csproj has forbidden PackageReference: $include")
    }
}

# 3. Raw assembly references (Steamworks, game DLLs, etc.) are forbidden.
foreach ($ref in $csproj.SelectNodes("//*[local-name()='Reference']")) {
    $include = $ref.GetAttribute("Include")
    $failures.Add("GameState.csproj has forbidden Reference: $include")
}

# 4. Source-level isolation: no Unity, BepInEx, Steam, CUO Runtime/Protocol,
#    network/persistence namespaces, and no ambient randomness / wall-clock.
$forbidden = @(
    "UnityEngine",
    "BepInEx",
    "Steamworks",
    "CasualtiesUnknownOnline.Runtime",
    "CasualtiesUnknownOnline.Protocol",
    "CasualtiesUnknownOnline.GameAdapter",
    "CasualtiesUnknownOnline.Plugin",
    "CasualtiesUnknownOnline.Abstractions",
    "CasualtiesUnknownOnline.Application",
    "Microsoft.Extensions",
    "System.Net",
    "System.IO",
    "System.Threading",
    "System.Random",
    # Phase B wire-free item domain guard: no Protocol DTO names, protobuf
    # attributes, or network vector shapes may leak into the kernel surface.
    "ProtoContract",
    "CharacterItemMsg",
    "ComponentStateMsg",
    "LiquidStackMsg",
    "NetVector",
    "protobuf"
)

$sourceRoot = Join-Path $root "src\CasualtiesUnknownOnline.GameState"
Get-ChildItem -Path $sourceRoot -Filter *.cs -Recurse | ForEach-Object {
    $relative = $_.FullName.Substring($root.Length + 1)
    $text = [System.IO.File]::ReadAllText($_.FullName)
    foreach ($token in $forbidden) {
        if ($text.Contains($token)) {
            $failures.Add("$relative contains forbidden token '$token'")
        }
    }
}

if ($failures.Count -gt 0) {
    Write-Host "GameState isolation gate FAILED:" -ForegroundColor Red
    foreach ($failure in $failures) {
        Write-Host "  - $failure" -ForegroundColor Red
    }
    exit 1
}

Write-Host "GameState isolation gate passed." -ForegroundColor Green
exit 0
