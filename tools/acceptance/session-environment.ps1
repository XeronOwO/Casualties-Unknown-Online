#Requires -Version 5.1
<#
.SYNOPSIS
Says whether the game install is free for an acceptance run, and swaps its BepInEx trees only when it is.

.DESCRIPTION
An install can carry more than one BepInEx tree: CUO's own, and whatever the machine's owner keeps for
their own play. Which tree is ACTIVE is always the install's `BepInEx` folder; which tree a folder HOLDS is
decided by a marker DLL inside it, never by a folder name. This script is the run's gate before any build,
deploy or launch:

  -Mode status      reports the machine, read-only.
  -Mode ensure-cuo  makes CUO's tree active, but ONLY when no game process is running. A running game
                    means its owner is playing (or a client is already up): the script refuses, and it
                    never kills a process and never moves a folder in that case.

The marker names, the process name and the parking name are machine facts (docs/acceptance/AGENTS.local.md);
this script carries only CUO's own marker as its default. Nothing outside -GameDir is read or written.

Exit codes: 0 ready (or nothing to do / a report), 2 refused (the machine is not free), 64 usage.

.PARAMETER GameDir
The game install to inspect (the `game-dir` value in docs/acceptance/AGENTS.local.md).

.PARAMETER Mode
status (read-only report) or ensure-cuo (swap the trees when the machine is free).

.PARAMETER GameProcessName
The game's process name without the extension (default CasualtiesUnknown).

.PARAMETER CuoMarkerName
The DLL that marks CUO's own tree (default CasualtiesUnknownOnline.dll).

.PARAMETER PlayMarkerName
The DLL that marks the machine owner's play tree, as recorded in the local facts. Empty means the report
cannot name that tree: it reads `play` with the marker, `other` without it, and a swap works either way.

.PARAMETER ParkedTreeName
The name an inactive tree is parked under when a swap runs (the machine's own convention).

.EXAMPLE
powershell -ExecutionPolicy Bypass -File tools/acceptance/session-environment.ps1 -GameDir "<game-dir>" -Mode status -PlayMarkerName "<play-marker>.dll"

.EXAMPLE
powershell -ExecutionPolicy Bypass -File tools/acceptance/session-environment.ps1 -GameDir "<game-dir>" -Mode ensure-cuo -PlayMarkerName "<play-marker>.dll"
#>
[CmdletBinding()]
param(
	[Parameter(Mandatory = $true)][string]$GameDir,
	[ValidateSet('status', 'ensure-cuo')][string]$Mode = 'status',
	[string]$GameProcessName = 'CasualtiesUnknown',
	[string]$CuoMarkerName = 'CasualtiesUnknownOnline.dll',
	[string]$PlayMarkerName = '',
	[string]$ParkedTreeName = 'BepInEx-play'
)

$ErrorActionPreference = 'Stop'

$ActiveName = 'BepInEx'

function Find-Marker([string]$Tree, [string]$Marker) {
	if ([string]::IsNullOrWhiteSpace($Marker)) { return '' }
	if (-not (Test-Path -LiteralPath $Tree -PathType Container)) { return '' }
	$hit = Get-ChildItem -LiteralPath $Tree -Recurse -Depth 6 -Filter $Marker -File -ErrorAction SilentlyContinue |
		Select-Object -First 1
	if ($null -eq $hit) { return '' }
	return $hit.FullName
}

function Get-TreeKind([string]$Tree) {
	if ((Find-Marker $Tree $CuoMarkerName) -ne '') { return 'cuo' }
	if ((Find-Marker $Tree $PlayMarkerName) -ne '') { return 'play' }
	return 'other'
}

if (-not (Test-Path -LiteralPath $GameDir -PathType Container)) {
	Write-Output "usage: -GameDir does not resolve: $GameDir"
	exit 64
}

$active = Join-Path $GameDir $ActiveName
$trees = @(Get-ChildItem -LiteralPath $GameDir -Directory -Filter 'BepInEx*' -ErrorAction SilentlyContinue)
$running = @(Get-Process -Name $GameProcessName -ErrorAction SilentlyContinue).Count -gt 0

if (-not (Test-Path -LiteralPath $active -PathType Container)) {
	Write-Output 'active=none'
	Write-Output "detail=no $ActiveName tree under $GameDir"
	Write-Output "game-running=$($running.ToString().ToLowerInvariant())"
	Write-Output 'launch=blocked'
	exit 2
}

$activeKind = Get-TreeKind $active
$cuoTrees = @($trees |
	Where-Object { $_.FullName -ne $active -and (Get-TreeKind $_.FullName) -eq 'cuo' } |
	ForEach-Object { $_.Name })
$playTrees = @($trees |
	Where-Object { $_.FullName -ne $active -and (Get-TreeKind $_.FullName) -eq 'play' } |
	ForEach-Object { $_.Name })

$playTree = if ($activeKind -eq 'play') { $ActiveName } elseif ($playTrees.Count -ge 1) { $playTrees[0] } else { '-' }
$cuoTree = if ($activeKind -eq 'cuo') { $ActiveName } elseif ($cuoTrees.Count -ge 1) { $cuoTrees[0] } else { '-' }
$swapNeeded = $activeKind -ne 'cuo'
$parkedFree = -not (Test-Path -LiteralPath (Join-Path $GameDir $ParkedTreeName))
$swapPossible = $swapNeeded -and $cuoTrees.Count -eq 1 -and $parkedFree
$launch = if ($running -or ($swapNeeded -and -not $swapPossible)) { 'blocked' } else { 'ok' }

Write-Output "active=$activeKind"
Write-Output "active-path=$active"
Write-Output "play-tree=$playTree"
Write-Output "cuo-tree=$cuoTree"
Write-Output "game-running=$($running.ToString().ToLowerInvariant())"
Write-Output "swap-needed=$($swapNeeded.ToString().ToLowerInvariant())"
Write-Output "launch=$launch"

if ($Mode -eq 'status') {
	if ($running) { Write-Output 'reason=game-running' }
	elseif ($swapNeeded -and $cuoTrees.Count -ne 1) { Write-Output "reason=cuo-tree-count=$($cuoTrees.Count)" }
	elseif ($swapNeeded -and -not $parkedFree) { Write-Output "reason=parked-name-taken=$ParkedTreeName" }
	exit 0
}

if ($running) {
	Write-Output 'action=refused'
	Write-Output 'reason=game-running'
	Write-Output 'detail=a running game means its owner is playing; no process is killed and no folder is moved'
	exit 2
}

if ($activeKind -eq 'cuo') {
	Write-Output 'action=already-cuo'
	exit 0
}

if ($cuoTrees.Count -ne 1) {
	Write-Output 'action=refused'
	Write-Output "reason=cuo-tree-count=$($cuoTrees.Count)"
	Write-Output 'detail=exactly one sibling tree must carry the CUO marker'
	exit 2
}

$parked = Join-Path $GameDir $ParkedTreeName
if (Test-Path -LiteralPath $parked) {
	Write-Output 'action=refused'
	Write-Output "reason=parked-name-taken=$ParkedTreeName"
	Write-Output "detail=$parked already exists; move it aside first"
	exit 2
}

Rename-Item -LiteralPath $active -NewName $ParkedTreeName
Rename-Item -LiteralPath (Join-Path $GameDir $cuoTrees[0]) -NewName $ActiveName

Write-Output 'action=swapped'
Write-Output "parked-tree=$ParkedTreeName"
Write-Output "cuo-tree=$ActiveName"
Write-Output 'launch=ok'
exit 0
