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
	# One client only, by name. Every row that is the CLIENT's own (an input patch, a UI frame, a console line) needs one client and no lobby, so
	# a single-client launch is the cheap shape for those — and it never touches the other boxes.
	[ValidateSet('host', 'guest', 'alt')]
	[string]$Only,
	[int]$SteamGraceSeconds = 60,
	[int]$ReadyTimeoutSeconds = 300
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
function Get-EndpointAddress([string]$Client) {
	# A driver endpoint is a ws:// URL; readiness is a plain TCP connect to its port, which costs nothing.
	$uri = [Uri]$Urls[$Client]
	return @{ Host = $uri.Host; Port = $uri.Port }
}
function Test-Port([string]$Client) {
	$address = Get-EndpointAddress $Client
	# NOT $client: PowerShell parameter names are case-insensitive, so assigning a TcpClient to $client coerces it to the [string] parameter and
	# Dispose() then fails with "String does not contain a method named Dispose" (hit 2026-10-26 on the first run of this rewrite).
	$socket = New-Object System.Net.Sockets.TcpClient
	try {
		$task = $socket.ConnectAsync($address.Host, $address.Port)
		return ($task.Wait(400) -and $socket.Connected)
	}
	catch { return $false }
	finally { $socket.Dispose() }
}
function Test-Endpoint([string]$Client) {
	# ONE driver ping, used only to confirm the answering socket is really our plug-in. Polling readiness with this would spawn a
	# powershell per poll (the old launcher did, every three seconds per client, which is most of why a launch felt slow).
	if (-not (Test-Port $Client)) { return $false }
	$answer = Invoke-Driver $Client 'ping'
	if ($null -eq $answer -or $null -eq $answer.result) { return $false }
	return ([string]$answer.result.plugin -eq 'true')
}
function Wait-Endpoints([string[]]$Clients, [int]$Seconds) {
	# One loop for ALL clients instead of a sequential wait per client: the slowest box decides the launch, never the sum of the three.
	# The poll interval is a poll interval, not a grace: a client that is ready is seen within it.
	$deadline = [DateTime]::UtcNow.AddSeconds($Seconds)
	$pending = [System.Collections.Generic.List[string]]::new()
	foreach ($client in $Clients) { $pending.Add($client) }

	while ($pending.Count -gt 0 -and [DateTime]::UtcNow -lt $deadline) {
		foreach ($client in @($pending)) {
			if (Test-Port $client) {
				Write-Run "$client endpoint up"
				$pending.Remove($client) | Out-Null
			}
		}

		if ($pending.Count -gt 0) { Start-Sleep -Milliseconds 250 }
	}

	return ($pending.Count -eq 0)
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

	# Poll for the process actually being gone instead of a fixed wait: the next start fights a half-dead process, and a fixed sleep
	# either wastes the whole interval or starts too early. Ten seconds is the ceiling, the exit is usually under one.
	$deadline = [DateTime]::UtcNow.AddSeconds(10)
	while ([DateTime]::UtcNow -lt $deadline) {
		if (@(Get-BoxProcesses $Box 'CasualtiesUnknown').Count -eq 0) { return }
		Start-Sleep -Milliseconds 250
	}
}
function Wait-SteamSettled([string]$Box) {
	# Poll for the box's Steam being genuinely up — the launcher process plus its web helper — instead of a fixed grace. The old
	# $SteamGraceSeconds wait was 60 s of dead time per launch even when Steam was ready in three.
	$deadline = [DateTime]::UtcNow.AddSeconds($SteamGraceSeconds)
	while ([DateTime]::UtcNow -lt $deadline) {
		if (@(Get-BoxProcesses $Box 'steamwebhelper').Count -gt 0) { return $true }
		Start-Sleep -Milliseconds 500
	}

	return $false
}
function Ensure-SandboxService() {
	# A box can only start when Sandboxie's service is up. After a machine restart SbieSvc is started on demand, and an unelevated
	# Start.exe cannot start it: the sandbox initialisation fails and the processes it spawns error out, which is exactly what "no UAC
	# prompt appeared, the sandbox errored, the processes inside spat errors" looked like (2026-10-26). One elevated `sc start` here is
	# the whole fix; it is never repeated while the service runs, and Steam/the game are started unelevated afterwards.
	$service = Get-Service -Name SbieSvc -ErrorAction SilentlyContinue
	if ($null -eq $service) {
		Write-Run 'WARN: no SbieSvc on this machine; a sandboxed box cannot start at all'
		return $false
	}

	if ($service.Status -eq 'Running') { return $true }

	Write-Run 'SbieSvc is not running; starting it elevated (a UAC prompt is expected)'
	try {
		Start-Process -FilePath 'sc.exe' -ArgumentList @('start', 'SbieSvc') -Verb RunAs -WindowStyle Hidden -ErrorAction Stop
	}
	catch {
		Write-Run "FAIL: elevation was refused or failed, so no box can start: $($_.Exception.Message)"
		return $false
	}

	$deadline = [DateTime]::UtcNow.AddSeconds(60)
	while ([DateTime]::UtcNow -lt $deadline) {
		$service = Get-Service -Name SbieSvc -ErrorAction SilentlyContinue
		if ($service -and $service.Status -eq 'Running') {
			Write-Run 'SbieSvc is up'
			return $true
		}

		Start-Sleep -Milliseconds 500
	}

	Write-Run 'FAIL: SbieSvc did not reach Running within 60 s'
	return $false
}
function Ensure-BoxSteam([string]$Box) {
	if (@(Get-BoxProcesses $Box 'steam').Count -gt 0) { return }
	Write-Run "$Box has no Steam; starting it (never restarting one that runs)"
	Start-Process -FilePath $Sandboxie -ArgumentList @("/box:$Box", $SteamExe) -WindowStyle Hidden
	$deadline = [DateTime]::UtcNow.AddSeconds(180)
	while ([DateTime]::UtcNow -lt $deadline) {
		if (@(Get-BoxProcesses $Box 'steam').Count -gt 0) { break }
		Start-Sleep -Milliseconds 500
	}
	if (Wait-SteamSettled $Box) { Write-Run "$Box steam settled" } else { Write-Run "$Box steam did not settle within $SteamGraceSeconds s; starting the game anyway" }
}
function Start-ClientProcess([string]$Client) {
	# Start the process for one client and return immediately: this function never waits. Waiting is one loop for all clients, below.
	switch ($Client) {
		'host' {
			if (Test-Endpoint 'host') { Write-Run 'host already running (reused)'; return }
			Start-Process -FilePath (Join-Path $GameDir 'CasualtiesUnknown.exe') -WorkingDirectory $GameDir
			Write-Run 'host started'
		}
		'guest' { Start-BoxGame $GuestBox 'guest' $GuestRoot }
		alt { Start-BoxGame $AltBox 'alt' $AltRoot }
	}
}
function Start-BoxGame([string]$Box, [string]$Client, [string]$GameRoot) {
	if (Test-Endpoint $Client) { Write-Run "$Client already running (reused)"; return }

	# A box whose game already runs but has not answered yet is NOT started again: the game refuses a second instance, and the wait loop
	# below will see it the moment it answers.
	if (@(Get-BoxProcesses $Box 'CasualtiesUnknown').Count -gt 0) { Write-Run "$Client process exists; letting it come up"; return }

	Ensure-BoxSteam $Box
	Start-Process -FilePath $Sandboxie -ArgumentList @("/box:$Box", (Join-Path $GameRoot 'CasualtiesUnknown.exe'))
	Write-Run "$Client started"
}
function Restart-BoxGame([string]$Box, [string]$Client, [string]$GameRoot) {
	# One restart per client, only for a box whose game DIED before answering (a game that exits on a not-ready Steam). Steam stays up.
	Stop-BoxGame $Box
	Ensure-BoxSteam $Box
	Start-Process -FilePath $Sandboxie -ArgumentList @("/box:$Box", (Join-Path $GameRoot 'CasualtiesUnknown.exe'))
	Write-Run "$Client restarted once (its earlier process exited before answering)"
}

# ALL clients start together, then ONE wait loop watches all of them. The old shape started host, waited up to 300 s, then guest, waited,
# then alt: the launch cost the SUM of the three, and every readiness poll spawned a powershell. A slow alt box must not make the run look
# failed — it only has to be waited for.
#
# -Only narrows the run to ONE client: a row that is the client's own (an input patch, a UI frame, a console line) needs no lobby, and a single
# client touches neither the other boxes nor the network. The sandbox service is only ensured when a BOXED client is in the run.
$clients = if ($Only) { @($Only) } else { @('host', 'guest', 'alt') }
$needsSandbox = @($clients | Where-Object { $_ -ne 'host' }).Count -gt 0
if ($needsSandbox -and -not (Ensure-SandboxService)) { Write-Run 'FAIL: the sandbox cannot start, so no boxed client can come up'; exit 7 }

foreach ($client in $clients) { Start-ClientProcess $client }

$boxes = @{ 'guest' = $GuestBox; 'alt' = $AltBox }
$roots = @{ 'guest' = $GuestRoot; 'alt' = $AltRoot }
$restarted = @{}

$ready = Wait-Endpoints $clients $ReadyTimeoutSeconds
foreach ($client in @($clients)) {
	if ($ready -and -not (Test-Endpoint $client)) {
		# The port answered but this is not our plug-in (another listener): treat as not ready and keep waiting.
		$ready = $false
	}
}

if (-not $ready) {
	# Give each box whose game died before answering exactly one restart, then wait again inside what is left of the deadline.
	foreach ($client in @('guest', 'alt')) {
		if (Test-Port $client) { continue }
		if (@(Get-BoxProcesses $boxes[$client] 'CasualtiesUnknown').Count -eq 0) {
			$restarted[$client] = $true
			Restart-BoxGame $boxes[$client] $client $roots[$client]
		}
	}

	$ready = Wait-Endpoints $clients $ReadyTimeoutSeconds
}

$missing = @($clients | Where-Object { -not (Test-Port $_) })
if ($missing.Count -gt 0) {
	foreach ($client in $missing) {
		if ($client -eq 'host') { Write-Run 'FAIL: host endpoint never came up'; exit 6 }
	}

	if ($missing -contains 'guest') { exit 7 }
	exit 8
}

foreach ($client in @($clients | Where-Object { $_ -ne 'host' })) {
	$root = $roots[$client]
	if (-not (Test-SteamInitialized $root)) {
		Write-Run "FAIL: $client answered without Steam initialized (its log says so); this box needs its Steam up before a session"
		if ($client -eq 'guest') { exit 7 }
		exit 8
	}
}

if ($Only) { Write-Run "$Only up" } else { Write-Run 'all three up' }

# A single-client run has no lobby to join and no world to enter: the endpoint that answered IS the deliverable.
if ($Only -or $NoJoin) { exit 0 }
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
