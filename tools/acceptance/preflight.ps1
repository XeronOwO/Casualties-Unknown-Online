#Requires -Version 5.1
<#
.SYNOPSIS
Read-only dependency preflight for an agent acceptance run.

.DESCRIPTION
Checks every dependency in docs/acceptance/dependencies.md against this machine and prints one row
per dependency: present, missing, unknown or pending. Machine values are resolved from the
"acceptance environment" section of docs/acceptance/AGENTS.local.md (gitignored, never committed); a
key that is absent is reported as FACT-MISSING and is a question for the user, not something to guess.

The script changes nothing on the machine. Exit codes: 0 = a full two-client run is possible,
2 = at least one required dependency is not present, 1 = the preflight itself failed.

.PARAMETER FactsPath
Path to the local facts file. Defaults to docs/acceptance/AGENTS.local.md under the repository root.

.PARAMETER JsonPath
Optional path for a machine-readable copy of the report.

.PARAMETER TimeoutMs
Connect timeout for the probe endpoints, in milliseconds.

.EXAMPLE
powershell -ExecutionPolicy Bypass -File tools/acceptance/preflight.ps1
#>
[CmdletBinding()]
param(
	[string]$FactsPath,
	[string]$JsonPath,
	[int]$TimeoutMs = 750
)

$ErrorActionPreference = 'Stop'

# Required for a full two-client run; the other ids are reported but never fail the exit code.
$RequiredIds = @('steam', 'game', 'sandboxie', 'dotnet', 'capture', 'logs', 'artifacts')

function New-Result {
	param([string]$Id, [string]$State, [string]$Detail)
	[pscustomobject]@{ id = $Id; state = $State; detail = $Detail }
}

function Read-Facts {
	param([string]$Path)

	$facts = @{}
	if ([string]::IsNullOrWhiteSpace($Path) -or -not (Test-Path -LiteralPath $Path)) {
		return $facts
	}

	$inSection = $false
	# The section heading is bilingual ("验收环境 / acceptance environment"); this script stays
	# ASCII-only so Windows PowerShell 5.1 reads it the same way on any code page.
	foreach ($line in (Get-Content -LiteralPath $Path -Encoding UTF8)) {
		if ($line -match '^\s*##\s+(.+?)\s*$') {
			$inSection = $Matches[1] -match 'acceptance environment'
			continue
		}
		if (-not $inSection) { continue }
		if ($line -match '^\s*-\s*([A-Za-z0-9_.-]+)\s*:\s*(.+?)\s*$') {
			$facts[$Matches[1]] = $Matches[2]
		}
	}
	return $facts
}

function Get-Fact {
	param([hashtable]$Facts, [string]$Key)
	if ($Facts.ContainsKey($Key)) { return [string]$Facts[$Key] }
	return $null
}

function Test-Endpoint {
	param([string]$Url, [int]$TimeoutMs)

	if ([string]::IsNullOrWhiteSpace($Url)) { return $false }
	$uri = $null
	if (-not [System.Uri]::TryCreate($Url, [System.UriKind]::Absolute, [ref]$uri)) { return $false }

	$client = New-Object System.Net.Sockets.TcpClient
	try {
		$async = $client.BeginConnect($uri.Host, $uri.Port, $null, $null)
		if (-not $async.AsyncWaitHandle.WaitOne($TimeoutMs)) { return $false }
		$client.EndConnect($async)
		return $true
	}
	catch {
		return $false
	}
	finally {
		$client.Close()
	}
}

function Get-SandboxieExeFromService {
	# The program may be installed anywhere; when the local fact is unset, derive it from the service
	# that is running rather than reporting a dependency missing that is plainly present.
	try {
		$service = Get-CimInstance -ClassName Win32_Service -Filter "Name='SbieSvc'" -ErrorAction Stop
		if ($null -eq $service -or [string]::IsNullOrWhiteSpace($service.PathName)) { return $null }
		$directory = Split-Path -Parent ($service.PathName.Trim('"'))
		foreach ($candidate in @('Start.exe', 'SandMan.exe', 'SbieCtrl.exe')) {
			$path = Join-Path $directory $candidate
			if (Test-Path -LiteralPath $path) { return $path }
		}
		return $null
	}
	catch {
		return $null
	}
}

function Get-HeadSha {
	param([string]$RepoRoot)

	try {
		# Collect first, then read the exit code: piping into `Select-Object -First 1` closes the pipe
		# early and leaves git with a non-zero status even on success.
		$output = & git -C $RepoRoot rev-parse HEAD 2>$null
		if ($LASTEXITCODE -ne 0 -or $null -eq $output) { return $null }
		return ([string]($output | Select-Object -First 1)).Trim()
	}
	catch {
		return $null
	}
}

function Get-InstallAppId {
	param([string]$GameDir)

	$file = Join-Path $GameDir 'steam_appid.txt'
	if (-not (Test-Path -LiteralPath $file)) { return $null }
	$line = @(Get-Content -LiteralPath $file -Encoding UTF8 -TotalCount 5) | Where-Object { -not [string]::IsNullOrWhiteSpace($_) } | Select-Object -First 1
	if ($null -eq $line) { return $null }
	return ([string]$line).Trim()
}

function Get-AppManifestPath {
	param([string]$GameDir, [string]$AppId)

	# The manifest sits at <library>\steamapps\appmanifest_<id>.acf, beside the install's common folder.
	$trimmed = $GameDir.TrimEnd('\', '/')
	$common = Split-Path -Parent $trimmed
	if ([string]::IsNullOrWhiteSpace($common) -or (Split-Path -Leaf $common) -ne 'common') { return $null }
	$steamApps = Split-Path -Parent $common
	if ([string]::IsNullOrWhiteSpace($steamApps)) { return $null }
	$manifest = Join-Path $steamApps ("appmanifest_$AppId.acf")
	if (Test-Path -LiteralPath $manifest) { return $manifest }
	return $null
}

function Get-ManifestInstallDir {
	param([string]$Path)

	try {
		foreach ($line in (Get-Content -LiteralPath $Path -Encoding UTF8)) {
			if ($line -match '^\s*"installdir"\s*"(.*)"\s*$') { return $Matches[1] }
		}
	}
	catch {
		return $null
	}
	return $null
}

function Get-AppIdMapping {
	param([string]$GameDir, [string]$AppId)

	if ([string]::IsNullOrWhiteSpace($AppId)) {
		return [pscustomobject]@{ State = 'unknown'; Detail = 'game-app-id is not set, so the launch id cannot be cross-checked against the install' }
	}
	$appId = $AppId.Trim()
	if ([string]::IsNullOrWhiteSpace($GameDir) -or -not (Test-Path -LiteralPath $GameDir)) {
		return [pscustomobject]@{ State = 'unknown'; Detail = "game-dir is unresolved, so app id $appId cannot be cross-checked against the install" }
	}

	# Every source that exists must confirm the id: one that contradicts it is a wrong fact or a wrong
	# install, and the launch line must not run on an unproven or contradicted id.
	$expected = Split-Path -Leaf $GameDir.TrimEnd('\', '/')
	$installId = Get-InstallAppId -GameDir $GameDir
	$manifest = Get-AppManifestPath -GameDir $GameDir -AppId $appId
	$manifestDir = $null
	if (-not [string]::IsNullOrWhiteSpace($manifest)) { $manifestDir = Get-ManifestInstallDir -Path $manifest }

	$conflicts = @()
	$confirmations = @()
	if (-not [string]::IsNullOrWhiteSpace($installId)) {
		if ($installId -ieq $appId) { $confirmations += "the install's steam_appid.txt" }
		else { $conflicts += "the install's steam_appid.txt reads $installId" }
	}
	if (-not [string]::IsNullOrWhiteSpace($manifestDir)) {
		if ($manifestDir -ieq $expected) { $confirmations += "the app manifest (installdir $manifestDir)" }
		else { $conflicts += "$(Split-Path -Leaf $manifest) names installdir '$manifestDir', not this install ('$expected')" }
	}
	if ($conflicts.Count -gt 0) {
		return [pscustomobject]@{ State = 'missing'; Detail = "game-app-id $appId is not this install: " + ($conflicts -join '; ') + "; fix the fact or the install before launching" }
	}
	if ($confirmations.Count -gt 0) {
		return [pscustomobject]@{ State = 'present'; Detail = "app id $appId matches " + ($confirmations -join ' and ') }
	}
	return [pscustomobject]@{ State = 'unknown'; Detail = "app id $appId cannot be cross-checked against the install: no steam_appid.txt under game-dir and no appmanifest_$appId.acf with an installdir beside it; a manifest is only read when the install sits under a steamapps\common folder; do not launch on an unproven id" }
}

function Test-Capture {
	try {
		Add-Type -AssemblyName System.Drawing -ErrorAction Stop
		Add-Type -AssemblyName System.Windows.Forms -ErrorAction Stop
		$bounds = [System.Windows.Forms.SystemInformation]::VirtualScreen
		return ($bounds.Width -gt 0) -and ($bounds.Height -gt 0) -and [Environment]::UserInteractive
	}
	catch {
		return $false
	}
}

function Invoke-Preflight {
	$repoRoot = Split-Path -Parent (Split-Path -Parent $PSScriptRoot)
	if ([string]::IsNullOrWhiteSpace($FactsPath)) {
		$FactsPath = Join-Path $repoRoot 'docs\acceptance\AGENTS.local.md'
	}

	$results = New-Object System.Collections.ArrayList
	$facts = Read-Facts -Path $FactsPath
	if ($facts.Count -eq 0) {
		[void]$results.Add((New-Result 'facts' 'missing' "no 'acceptance environment' section resolved from $FactsPath; every key below is a question for the user"))
	}

	$gameDir = Get-Fact $facts 'game-dir'
	$sandboxRoot = Get-Fact $facts 'sandbox-guest-root'
	$artifactDir = Get-Fact $facts 'acceptance-artifacts-dir'

	foreach ($key in @('game-dir', 'game-app-id', 'steam-exe', 'sandboxie-exe', 'sandbox-guest-root', 'acceptance-artifacts-dir')) {
		if ([string]::IsNullOrWhiteSpace((Get-Fact $facts $key))) {
			[void]$results.Add((New-Result 'fact' 'missing' "FACT-MISSING: $key is not set in the acceptance environment section"))
		}
	}

	# dotnet
	$dotnet = Get-Command dotnet -ErrorAction SilentlyContinue
	if ($null -eq $dotnet) {
		[void]$results.Add((New-Result 'dotnet' 'missing' 'dotnet is not on PATH; the commit under acceptance cannot be built'))
	}
	else {
		$version = (& dotnet --version 2>&1 | Select-Object -First 1)
		[void]$results.Add((New-Result 'dotnet' 'present' "dotnet $version"))
	}

	# game install
	if ([string]::IsNullOrWhiteSpace($gameDir)) {
		[void]$results.Add((New-Result 'game' 'missing' 'game-dir is not set'))
	}
	elseif (-not (Test-Path -LiteralPath $gameDir)) {
		[void]$results.Add((New-Result 'game' 'missing' 'game-dir does not resolve'))
	}
	else {
		$gameExe = Join-Path $gameDir 'CasualtiesUnknown.exe'
		$doorstop = Join-Path $gameDir 'winhttp.dll'
		$bepinex = Join-Path $gameDir 'BepInEx'
		$missing = @()
		if (-not (Test-Path -LiteralPath $gameExe)) { $missing += 'the game executable' }
		if (-not (Test-Path -LiteralPath $doorstop)) { $missing += 'the doorstop DLL' }
		if (-not (Test-Path -LiteralPath $bepinex)) { $missing += 'BepInEx' }
		if ($missing.Count -gt 0) {
			[void]$results.Add((New-Result 'game' 'missing' ('game-dir is missing ' + ($missing -join ', '))))
		}
		else {
			[void]$results.Add((New-Result 'game' 'present' 'game install, doorstop and BepInEx resolve'))
		}
	}

	# deploy (advisory: the workflow's build/deploy step fixes it; the identity is reported, and the
	# comparison against the repository's HEAD is the workflow's, because a documentation-only commit
	# legitimately moves HEAD without changing the artifact)
	if (-not [string]::IsNullOrWhiteSpace($gameDir)) {
		$deployed = Join-Path $gameDir 'BepInEx\plugins\CasualtiesUnknownOnline\CasualtiesUnknownOnline.dll'
		if (Test-Path -LiteralPath $deployed) {
			$info = [System.Diagnostics.FileVersionInfo]::GetVersionInfo($deployed)
			$head = Get-HeadSha -RepoRoot $repoRoot
			$headNote = if ([string]::IsNullOrWhiteSpace($head)) { 'repository HEAD is unknown' } else { "repository HEAD is $head" }
			[void]$results.Add((New-Result 'deploy' 'present' "deployed plugin ProductVersion $($info.ProductVersion), written $((Get-Item -LiteralPath $deployed).LastWriteTime.ToString('u')); $headNote"))
		}
		else {
			[void]$results.Add((New-Result 'deploy' 'missing' 'no deployed plugin DLL under the game install; run the workflow build/deploy step'))
		}
	}
	else {
		[void]$results.Add((New-Result 'deploy' 'missing' 'game-dir is unresolved, so the deployed artifact cannot be checked'))
	}

	# steam (a not-yet-started client is not a missing dependency: the run starts it; a set app id is
	# not a proven one -- it is cross-checked against the install before the launch line is used)
	$steamExe = Get-Fact $facts 'steam-exe'
	$steamAppId = Get-Fact $facts 'game-app-id'
	$steamProcess = @(Get-Process -Name steam -ErrorAction SilentlyContinue)
	if ([string]::IsNullOrWhiteSpace($steamExe) -or -not (Test-Path -LiteralPath $steamExe)) {
		[void]$results.Add((New-Result 'steam' 'missing' 'steam-exe does not resolve'))
	}
	else {
		$running = if ($steamProcess.Count -gt 0) { 'Steam is running' } else { 'Steam is not running yet; the run starts it' }
		$mapping = Get-AppIdMapping -GameDir $gameDir -AppId $steamAppId
		$detail = "steam.exe resolves; $running; $($mapping.Detail)"
		if ($mapping.State -eq 'present') { $detail += '; the account state is settled at launch' }
		[void]$results.Add((New-Result 'steam' $mapping.State $detail))
	}

	# sandboxie (the program may be derived from the service when the local fact is unset)
	$sandboxExe = Get-Fact $facts 'sandboxie-exe'
	$sandboxExeSource = 'sandboxie-exe'
	if ([string]::IsNullOrWhiteSpace($sandboxExe) -or -not (Test-Path -LiteralPath $sandboxExe)) {
		$derived = Get-SandboxieExeFromService
		if (-not [string]::IsNullOrWhiteSpace($derived)) {
			$sandboxExe = $derived
			$sandboxExeSource = 'derived from the SbieSvc image path'
		}
	}

	$sandboxProblems = @()
	if ([string]::IsNullOrWhiteSpace($sandboxExe) -or -not (Test-Path -LiteralPath $sandboxExe)) { $sandboxProblems += 'sandboxie-exe does not resolve and cannot be derived from SbieSvc' }
	if ([string]::IsNullOrWhiteSpace($sandboxRoot) -or -not (Test-Path -LiteralPath $sandboxRoot)) { $sandboxProblems += 'sandbox-guest-root does not resolve' }
	$sbieServices = @(Get-Service -Name 'SbieSvc', 'SbieDrv' -ErrorAction SilentlyContinue)
	if ($sbieServices.Count -lt 2) { $sandboxProblems += "SbieSvc and SbieDrv: found $($sbieServices.Count) of the 2 services" }
	$altRoot = Get-Fact $facts 'sandbox-alt-root'
	if (-not [string]::IsNullOrWhiteSpace($altRoot) -and -not (Test-Path -LiteralPath $altRoot)) { $sandboxProblems += 'sandbox-alt-root is set but does not resolve' }
	if ($sandboxProblems.Count -gt 0) {
		[void]$results.Add((New-Result 'sandboxie' 'missing' ($sandboxProblems -join '; ')))
	}
	else {
		[void]$results.Add((New-Result 'sandboxie' 'present' "program ($sandboxExeSource), guest sandbox root, SbieSvc and SbieDrv resolve"))
	}

	# hotrepl (advisory: probes are optional; log evidence still runs)
	if (-not [string]::IsNullOrWhiteSpace($gameDir)) {
		$hotreplDir = Join-Path $gameDir 'BepInEx\plugins\HotRepl'
		if (-not (Test-Path -LiteralPath $hotreplDir)) {
			[void]$results.Add((New-Result 'hotrepl' 'missing' 'the HotRepl plugin directory is not deployed under the game install'))
		}
		else {
			$hostUp = Test-Endpoint (Get-Fact $facts 'hotrepl-host-url') $TimeoutMs
			$guestUp = Test-Endpoint (Get-Fact $facts 'hotrepl-guest-url') $TimeoutMs
			$detail = 'plugin deployed; host endpoint ' + $(if ($hostUp) { 'up' } else { 'idle (no client running)' }) + ', guest endpoint ' + $(if ($guestUp) { 'up' } else { 'idle (no client running)' })
			[void]$results.Add((New-Result 'hotrepl' 'present' $detail))
		}
	}
	else {
		[void]$results.Add((New-Result 'hotrepl' 'missing' 'game-dir is unresolved, so the evaluator plugin cannot be checked'))
	}

	# capture
	if (Test-Capture) {
		[void]$results.Add((New-Result 'capture' 'present' 'an interactive desktop with the drawing stack is available'))
	}
	else {
		[void]$results.Add((New-Result 'capture' 'missing' 'no interactive desktop or drawing stack; visual rows cannot be captured'))
	}

	# input (the committed in-process driver: the evaluator channel, never OS-level keyboard or mouse)
	$inputHelper = Join-Path $repoRoot 'tools\acceptance\drive-in-process.ps1'
	$inputTemplate = Join-Path $repoRoot 'tools\acceptance\driver\InProcessDriver.cs'
	$inputRecipes = Join-Path $repoRoot 'tools\acceptance\recipes'
	if ((Test-Path -LiteralPath $inputHelper) -and (Test-Path -LiteralPath $inputTemplate) -and (Test-Path -LiteralPath $inputRecipes)) {
		[void]$results.Add((New-Result 'input' 'present' 'the in-process driver helper, its template and the scenario recipes resolve; setups run through the evaluator channel the hotrepl row reports'))
	}
	else {
		[void]$results.Add((New-Result 'input' 'missing' 'the in-process driver helper, its template or the scenario recipes do not resolve; a scenario that needs a driven setup stays blocked, and OS-level input is never a substitute'))
	}

	# logs (both clients write here; the guest's root lives under the sandbox)
	if (-not [string]::IsNullOrWhiteSpace($gameDir) -and (Test-Path -LiteralPath $gameDir)) {
		$logRoots = @(Join-Path $gameDir 'BepInEx')
		if (-not [string]::IsNullOrWhiteSpace($sandboxRoot)) { $logRoots += (Join-Path $sandboxRoot 'BepInEx') }
		$missingRoots = @($logRoots | Where-Object { -not (Test-Path -LiteralPath $_) })
		if ($missingRoots.Count -eq 0) {
			[void]$results.Add((New-Result 'logs' 'present' "$($logRoots.Count) BepInEx log root(s) resolve; per-run files appear after a client starts"))
		}
		else {
			[void]$results.Add((New-Result 'logs' 'missing' "$($missingRoots.Count) of $($logRoots.Count) BepInEx log root(s) missing (the guest's root comes from the sandbox install)"))
		}
	}
	else {
		[void]$results.Add((New-Result 'logs' 'missing' 'game-dir is unresolved, so the log root cannot be checked'))
	}

	# artifacts
	if ([string]::IsNullOrWhiteSpace($artifactDir)) {
		[void]$results.Add((New-Result 'artifacts' 'missing' 'acceptance-artifacts-dir is not set'))
	}
	elseif (Test-Path -LiteralPath $artifactDir) {
		[void]$results.Add((New-Result 'artifacts' 'present' 'the artifact directory resolves'))
	}
	else {
		[void]$results.Add((New-Result 'artifacts' 'missing' 'the artifact directory does not exist yet; create it or point acceptance-artifacts-dir elsewhere'))
	}

	return $results
}

try {
	$report = Invoke-Preflight
	$required = @($report | Where-Object { $RequiredIds -contains $_.id })
	$blocking = @($required | Where-Object { $_.state -ne 'present' })

	foreach ($row in $report) {
		'{0,-7} {1,-11} {2,-8} {3}' -f "[$($row.state)]", $row.id, '', $row.detail
	}

	$counts = @{}
	foreach ($row in $report) {
		if (-not $counts.ContainsKey($row.state)) { $counts[$row.state] = 0 }
		$counts[$row.state] = $counts[$row.state] + 1
	}
	$summary = ($counts.GetEnumerator() | Sort-Object Name | ForEach-Object { "$($_.Value) $($_.Name)" }) -join ', '
	Write-Output "preflight: $summary"

	if ($null -ne $JsonPath -and -not [string]::IsNullOrWhiteSpace($JsonPath)) {
		try {
			# BOM-less UTF-8: Windows PowerShell 5.1's `Set-Content -Encoding UTF8` writes a BOM.
			$json = $report | ConvertTo-Json -Depth 4
			[System.IO.File]::WriteAllText($JsonPath, $json, (New-Object System.Text.UTF8Encoding($false)))
			Write-Output "json: $JsonPath"
		}
		catch {
			# A failed copy must not destroy the dependency verdict above.
			Write-Output "json: FAILED - $($_.Exception.Message) (the report above stands)"
		}
	}

	if ($blocking.Count -eq 0) {
		Write-Output 'RESULT: OK - a full two-client run is possible'
		exit 0
	}

	$names = ($blocking | ForEach-Object { $_.id }) -join ', '
	Write-Output "RESULT: MISSING - $names; ask the user per docs/acceptance/dependencies.md (Asking the user)"
	exit 2
}
catch {
	Write-Output "RESULT: ERROR - $($_.Exception.Message)"
	exit 1
}
