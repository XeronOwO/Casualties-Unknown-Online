#Requires -Version 5.1
<#
.SYNOPSIS
Starts the acceptance clients (host, guest, alt) and, unless -NoJoin, joins all of them to one lobby and one world.

.DESCRIPTION
The ONE launcher every acceptance run uses. It replaces the per-batch launcher copies that kept being rewritten
with the same mistakes, and it never touches Steam beyond starting it when it is missing:

- A client whose driver endpoint already answers is REUSED. A duplicate launch is harmless in the game (it refuses
  to start twice) but the run must not depend on that: an already-running client is never started again, and a
  process that is still coming up is waited for.
- Steam is only STARTED when a box has none; it is never terminated or restarted. A box's Steam takes up to a
  minute to become usable, so the game is started only after Steam has had that time, and a game that logs
  "Steam not initialized" is restarted once on its own (its box's game process only) while Steam stays up.

EVERY machine value is a parameter: this file carries no drive path, no box name, no port and no account. A value
left out is looked up in the gitignored local facts file (docs/acceptance/AGENTS.local.md by default, the same
convention tools/acceptance/preflight.ps1 and drive-in-process.ps1 follow), and a value found in neither place is
a named error - nothing is guessed. Someone else runs this script with their own parameters or their own facts
file:
  -GameDir <install> -SteamExe <steam> -SandboxieExe <start.exe> -GuestBox <box> -GuestRoot <install>
  -AltBox <box> -AltRoot <install> -HostUrl <ws> -GuestUrl <ws> -AltUrl <ws>

Exit codes: 0 a full session is up, 6 host never answered, 7 guest failed, 8 alt failed, 9 the world never had
all three clients, 64 usage.
#>
param(
	[string]$FactsPath,
	[string]$OutDir,
	[string]$GameDir,
	[string]$SteamExe,
	[string]$SandboxieExe,
	[string]$GuestBox,
	[string]$GuestRoot,
	[string]$AltBox,
	[string]$AltRoot,
	[string]$HostUrl,
	[string]$GuestUrl,
	[string]$AltUrl,
	[switch]$NoJoin,
	[int]$SteamGraceSeconds = 60
)

$ErrorActionPreference = 'Stop'

$repoRoot = (Resolve-Path (Join-Path $PSScriptRoot '..\..')).Path
if ([string]::IsNullOrWhiteSpace($FactsPath)) { $FactsPath = Join-Path $repoRoot 'docs\acceptance\AGENTS.local.md' }
$hasFacts = Test-Path -LiteralPath $FactsPath
if (-not $hasFacts -and -not ($GameDir -and $SteamExe -and $SandboxieExe -and $GuestBox -and $GuestRoot -and $AltBox -and $AltRoot -and $HostUrl -and $GuestUrl -and $AltUrl)) {
	throw "No local facts file at $FactsPath and not every machine value was passed. Pass -GameDir -SteamExe -SandboxieExe -GuestBox -GuestRoot -AltBox -AltRoot -HostUrl -GuestUrl -AltUrl, or point -FactsPath at a facts file with the acceptance-environment keys."
}
if ([string]::IsNullOrWhiteSpace($OutDir)) {
	$OutDir = Join-Path $repoRoot '.acceptance\launcher'
}
New-Item -ItemType Directory -Path $OutDir -Force | Out-Null

function Read-Facts {
	param([string]$Path)
	$facts = @{}
	if (-not (Test-Path -LiteralPath $Path)) { return $facts }
	$inSection = $false
	foreach ($line in (Get-Content -LiteralPath $Path -Encoding UTF8)) {
		if ($line -match '^\s*##\s+(.+?)\s*$') { $inSection = $Matches[1] -match 'acceptance environment'; continue }
		if (-not $inSection) { continue }
		if ($line -match '^\s*-\s*([A-Za-z0-9_.-]+)\s*:\s*(.+?)\s*$') { $facts[$Matches[1]] = $Matches[2] }
	}
	return $facts
}
$facts = Read-Facts $FactsPath
function Resolve-Value([string]$Passed, [string]$Key, [string]$Name) {
	if (-not [string]::IsNullOrWhiteSpace($Passed)) { return $Passed }
	if ($facts.ContainsKey($Key)) { return [string]$facts[$Key] }
	throw "$Name was not passed and the local facts file has no '$Key'."
}

$GameDir = Resolve-Value $GameDir 'game-dir' '-GameDir'
$SteamExe = Resolve-Value $SteamExe 'steam-exe' '-SteamExe'
$Sandboxie = Resolve-Value $SandboxieExe 'sandboxie-exe' '-SandboxieExe'
$GuestRoot = Resolve-Value $GuestRoot 'sandbox-guest-root' '-GuestRoot'
$AltRoot = Resolve-Value $AltRoot 'sandbox-alt-root' '-AltRoot'
$GuestBox = Resolve-Value $GuestBox 'sandbox-guest-box' '-GuestBox'
$AltBox = Resolve-Value $AltBox 'sandbox-alt-box' '-AltBox'
$Urls = @{
	'host' = Resolve-Value $HostUrl 'hotrepl-host-url' '-HostUrl'
	'guest' = Resolve-Value $GuestUrl 'hotrepl-guest-url' '-GuestUrl'
	'alt' = Resolve-Value $AltUrl 'hotrepl-alt-url' '-AltUrl'
}
$Driver = Join-Path $PSScriptRoot 'drive-in-process.ps1'
$script:LaunchLog = Join-Path $OutDir 'launch.log'
Set-Content -LiteralPath $script:LaunchLog -Value '' -Encoding UTF8

function Write-Run([string]$line) {
	$text = "$([DateTime]::Now.ToString('HH:mm:ss')) $line"
	Write-Host $text
	Add-Content -LiteralPath $script:LaunchLog -Value $text -Encoding UTF8
}
function Read-Json([string]$Path) {
	if (-not (Test-Path -LiteralPath $Path)) { return $null }
	return (Get-Content -LiteralPath $Path -Raw | ConvertFrom-Json)
}
function Invoke-Driver([string]$Client, [string]$Action, [hashtable]$Extra = @{}) {
	$out = Join-Path $OutDir "$Client-$Action.json"
	$driverArgs = @('-Url', $Urls[$Client], '-Action', $Action, '-JsonPath', $out)
	foreach ($key in $Extra.Keys) { $driverArgs += "-$key"; $driverArgs += $Extra[$key] }
	& powershell -NoProfile -ExecutionPolicy Bypass -File $Driver @driverArgs *> $null
	return (Read-Json $out)
}
function Test-Endpoint([string]$Client) {
	$answer = Invoke-Driver $Client 'ping'
	if ($null -eq $answer -or $null -eq $answer.result) { return $false }
	return ([string]$answer.result.plugin -eq 'true')
}
function Wait-Endpoint([string]$Client, [int]$Seconds) {
	$deadline = [DateTime]::UtcNow.AddSeconds($Seconds)
	while ([DateTime]::UtcNow -lt $deadline) {
		if (Test-Endpoint $Client) { return $true }
		Start-Sleep -Seconds 3
	}
	return $false
}
function Get-BoxProcesses([string]$Box, [string]$Name) {
	$pids = @(& $Sandboxie "/box:$Box" /listpids 2>&1 | ForEach-Object { $_.ToString().Trim() } | Where-Object { $_ -match '^\d+$' -and $_ -ne '0' })
	return @($pids | Where-Object { $p = Get-Process -Id $_ -ErrorAction SilentlyContinue; $p -and $p.ProcessName -eq $Name })
}
function Test-SteamInitialized([string]$GameRoot) {
	$log = Join-Path $GameRoot 'BepInEx\logs\latest.log'
	if (-not (Test-Path -LiteralPath $log)) { return $false }
	$fs = [System.IO.File]::Open($log, 'Open', 'Read', 'ReadWrite')
	try {
		$sr = New-Object System.IO.StreamReader($fs, [System.Text.UTF8Encoding]::new($false))
		$text = $sr.ReadToEnd()
	}
	finally { $fs.Dispose() }
	return ($text -match 'Steam initialized\.') -and -not ($text -match 'Steam not initialized')
}
function Stop-BoxGame([string]$Box) {
	# This box's game process only; Steam stays untouched.
	foreach ($gamePid in (Get-BoxProcesses $Box 'CasualtiesUnknown')) {
		try { Stop-Process -Id $gamePid -Force } catch { Write-Run "could not stop ${gamePid}: $($_.Exception.Message)" }
	}
}
function Ensure-BoxSteam([string]$Box) {
	if (@(Get-BoxProcesses $Box 'steam').Count -gt 0) { return }
	Write-Run "$Box has no Steam; starting it (never restarting one that runs)"
	Start-Process -FilePath $Sandboxie -ArgumentList @("/box:$Box", $SteamExe) -WindowStyle Hidden
	$deadline = [DateTime]::UtcNow.AddSeconds(180)
	while ([DateTime]::UtcNow -lt $deadline) {
		if (@(Get-BoxProcesses $Box 'steam').Count -gt 0) { break }
		Start-Sleep -Seconds 5
	}
	Write-Run "$Box steam process up; giving it $SteamGraceSeconds s before the game starts"
	Start-Sleep -Seconds $SteamGraceSeconds
}
function Start-BoxClient([string]$Box, [string]$Client, [string]$GameRoot) {
	if (Test-Endpoint $Client) {
		Write-Run "$Client already running (reused)"
		if (Test-SteamInitialized $GameRoot) { return $true }
		Write-Run "$Client runs without Steam; restarting this box's client only"
		Stop-BoxGame $Box
		Start-Sleep -Seconds 10
	}
	elseif (@(Get-BoxProcesses $Box 'CasualtiesUnknown').Count -gt 0) {
		Write-Run "$Client process exists; waiting for its endpoint instead of starting another"
		if (Wait-Endpoint $Client 300) { return $true }
		Write-Run "$Client never answered; restarting this box's client only"
		Stop-BoxGame $Box
		Start-Sleep -Seconds 10
	}

	for ($attempt = 1; $attempt -le 2; $attempt++) {
		Ensure-BoxSteam $Box
		if (@(Get-BoxProcesses $Box 'CasualtiesUnknown').Count -eq 0) {
			Start-Process -FilePath $Sandboxie -ArgumentList @("/box:$Box", (Join-Path $GameRoot 'CasualtiesUnknown.exe'))
		}
		if (-not (Wait-Endpoint $Client 300)) {
			Write-Run "FAIL: $Client endpoint never came up (attempt $attempt)"
			Stop-BoxGame $Box
			Start-Sleep -Seconds 10
			continue
		}
		Start-Sleep -Seconds 5
		if (Test-SteamInitialized $GameRoot) { Write-Run "$Client up with Steam initialized"; return $true }
		Write-Run "$Client is up without Steam (attempt $attempt); waiting and restarting this box's client only"
		Stop-BoxGame $Box
		Start-Sleep -Seconds 30
	}
	Write-Run "FAIL: $Client never came up with Steam initialized"
	return $false
}

# Host: reuse a running one, wait for a starting one, start one only when there is none. Physical-machine Steam is
# never started or stopped by this script.
if (-not (Test-Endpoint 'host')) {
	if (@(Get-Process CasualtiesUnknown -ErrorAction SilentlyContinue).Count -gt 0) {
		Write-Run 'host process exists; waiting for its endpoint instead of starting another'
		if (-not (Wait-Endpoint 'host' 300)) { Write-Run 'FAIL: host process never answered'; exit 6 }
	}
	else {
		Start-Process -FilePath (Join-Path $GameDir 'CasualtiesUnknown.exe') -WorkingDirectory $GameDir
		if (-not (Wait-Endpoint 'host' 300)) { Write-Run 'FAIL: host endpoint never came up'; exit 6 }
	}
}
Write-Run 'host up'
if (-not (Start-BoxClient $GuestBox 'guest' $GuestRoot)) { exit 7 }
if (-not (Start-BoxClient $AltBox 'alt' $AltRoot)) { exit 8 }
Write-Run 'all three up'

if ($NoJoin) { exit 0 }
$lobbyAnswer = Invoke-Driver 'host' 'create-lobby'
$lobby = [string]$lobbyAnswer.result.lobby
if ([string]::IsNullOrWhiteSpace($lobby)) { Write-Run 'FAIL: no lobby id'; exit 6 }
Write-Run "lobby $lobby"
foreach ($client in @('guest', 'alt')) {
	Invoke-Driver $client 'join-lobby' @{ LobbyId = $lobby } | Out-Null
	Write-Run "$client joined"
}
Invoke-Driver 'host' 'start-run' | Out-Null
$deadline = [DateTime]::UtcNow.AddSeconds(240)
$allIn = $false
while ([DateTime]::UtcNow -lt $deadline) {
	$hostIn = [string](Invoke-Driver 'host' 'state').result.inWorld -eq 'true'
	$guestIn = [string](Invoke-Driver 'guest' 'state').result.inWorld -eq 'true'
	$altIn = [string](Invoke-Driver 'alt' 'state').result.inWorld -eq 'true'
	if ($hostIn -and $guestIn -and $altIn) { $allIn = $true; break }
	Start-Sleep -Seconds 3
}
Write-Run "all in world: $allIn"
if (-not $allIn) { exit 9 }
