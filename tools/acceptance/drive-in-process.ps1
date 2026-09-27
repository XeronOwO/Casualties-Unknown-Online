#Requires -Version 5.1
<#
.SYNOPSIS
Drives a running CUO client's Online UI from inside its own process, through the HotRepl evaluator.

.DESCRIPTION
The acceptance run may reproduce a scenario without a person at the keyboard, but never through
OS-level keyboard or mouse. This helper is that door: it connects to a client's HotRepl endpoint
(the machine values come from docs/acceptance/AGENTS.local.md, never from this file), sends one eval
snippet per step, and returns the client's answer as JSON.

The in-process half (driver/InProcessDriver.cs) enters the Online UI at the frame's registered
action table: the real control ids (home.create_lobby, home.lobby_id, home.join, tab.*) receive the
same intent payloads the native surface view emits, so LobbySwitchActions and the native run-start
gate decide. A composite action sequences several evals; HotRepl evaluates at most one snippet per
frame on the main thread, so the retry loops wait out a page or frame switch instead of racing it.

Exit codes: 0 ok, 1 the client refused or the setup was not reached, 2 transport failure, 3 timeout,
4 driver or protocol failure (a malformed, truncated or unexpected answer), 64 usage.

.PARAMETER Url
The HotRepl websocket endpoint of the client to drive (ws:// or wss://). Read it from
docs/acceptance/AGENTS.local.md.

.PARAMETER Action
The action to perform. List them with -ListActions.

.PARAMETER ControlId
The control id for click / set-text (use the ids -Action state reports as offered).

.PARAMETER Text
The value for set-text.

.PARAMETER LobbyId
The lobby id for join-lobby (digits only).

.PARAMETER Page
The page for goto-page: home, players, network, admin, worlds or preferences.

.PARAMETER TimeoutMs
The budget for the whole action, in milliseconds, checked between steps. Each eval also carries an eval
timeout derived from it, and one in-flight eval can overrun the budget by that timeout (at most 10
seconds) plus the receive grace, because the evaluator cannot be interrupted mid-step.

.PARAMETER RetryDelayMs
How long to wait between retries while a control is not offered yet.

.PARAMETER ListActions
Print the action vocabulary and exit without a client.

.PARAMETER JsonPath
Optional path to also write the JSON result to (the parent directory is created).

.EXAMPLE
powershell -ExecutionPolicy Bypass -File tools/acceptance/drive-in-process.ps1 -Url ws://127.0.0.1:18590 -Action state
.EXAMPLE
powershell -ExecutionPolicy Bypass -File tools/acceptance/drive-in-process.ps1 -Url ws://127.0.0.1:18590 -Action create-lobby
#>
[CmdletBinding()]
param(
	[string]$Url,
	[ValidateSet('ping', 'state', 'open-window', 'goto-page', 'click', 'set-text', 'create-lobby', 'join-lobby', 'start-run', 'quit')]
	[string]$Action,
	[string]$ControlId,
	[string]$Text,
	[string]$LobbyId,
	[ValidateSet('home', 'players', 'network', 'admin', 'worlds', 'preferences')]
	[string]$Page,
	[int]$TimeoutMs = 30000,
	[int]$RetryDelayMs = 100,
	[switch]$ListActions,
	[string]$JsonPath
)

$ErrorActionPreference = 'Stop'

$ActionHelp = [ordered]@{
	'ping' = 'connect and report the plugin, the overlay and the offered control count'
	'state' = 'read the session/world/window facts and the offered control ids'
	'open-window' = 'show the Online UI window (idempotent; the launcher click''s own action)'
	'goto-page' = 'switch the window to a page: home, players, network, admin, worlds, preferences'
	'click' = 'apply ControlInvoked for a real control id; retries while not offered'
	'set-text' = 'apply ControlEdited for a real text-field control id; retries while not offered'
	'create-lobby' = 'open the window and click home.create_lobby; wait for the lobby id'
	'join-lobby' = 'set home.lobby_id and click home.join; wait for the joined lobby'
	'start-run' = 'call the game''s own PreRunScript.StartRun; wait for the world to start'
	'quit' = 'ask the client to quit (only a client this run started)'
}

$PageIds = @{
	'home' = 'tab.home'
	'players' = 'tab.players'
	'network' = 'tab.network'
	'admin' = 'tab.admin'
	'worlds' = 'tab.worlds'
	'preferences' = 'tab.preferences'
}

$PageTitles = @{
	'home' = 'Home'
	'players' = 'Players'
	'network' = 'Network'
	'admin' = 'Admin'
	'worlds' = 'Worlds'
	'preferences' = 'Preferences'
}

function Write-Usage {
	param([string]$Problem)
	if (-not [string]::IsNullOrWhiteSpace($Problem)) {
		[Console]::Error.WriteLine("drive-in-process: $Problem")
	}
	[Console]::Error.WriteLine('usage: drive-in-process.ps1 -Url <ws-url> -Action <action> [parameters]')
	[Console]::Error.WriteLine('       drive-in-process.ps1 -ListActions')
}

function Write-Actions {
	Write-Output 'drive-in-process.ps1 actions (in-process only; never OS-level keyboard or mouse):'
	foreach ($entry in $ActionHelp.GetEnumerator()) {
		Write-Output ('  {0,-13} {1}' -f $entry.Key, $entry.Value)
	}
	Write-Output 'parameters: -Url <ws-url> -ControlId <id> -Text <value> -LobbyId <digits> -Page <page> -TimeoutMs <ms> -RetryDelayMs <ms> -JsonPath <file>'
}

function ConvertTo-CSharpLiteral {
	param([string]$Value)
	$builder = New-Object System.Text.StringBuilder
	[void]$builder.Append('"')
	foreach ($c in $Value.ToCharArray()) {
		if ($c -eq '"') { [void]$builder.Append('\"') }
		elseif ($c -eq '\') { [void]$builder.Append('\\') }
		elseif ($c -eq [char]13) { [void]$builder.Append('\r') }
		elseif ($c -eq [char]10) { [void]$builder.Append('\n') }
		elseif ($c -eq [char]9) { [void]$builder.Append('\t') }
		else { [void]$builder.Append($c) }
	}
	[void]$builder.Append('"')
	return $builder.ToString()
}

function Expand-DriverCode {
	param([string]$Template, [string]$CommandName, [string]$Argument, [string]$TextValue)
	# One pass over the template's own placeholders: the values are inserted as they are built, so a value
	# that happens to spell a placeholder is never substituted again and stays a legal text value.
	$replacements = [ordered]@{
		'"{{COMMAND}}"' = (ConvertTo-CSharpLiteral -Value $CommandName)
		'"{{ARGUMENT}}"' = (ConvertTo-CSharpLiteral -Value $Argument)
		'"{{TEXT}}"' = (ConvertTo-CSharpLiteral -Value $TextValue)
	}
	foreach ($token in $replacements.Keys) {
		$occurrences = ([regex]::Matches($Template, [regex]::Escape($token))).Count
		if ($occurrences -ne 1) {
			throw [System.InvalidOperationException]::new("the in-process template must carry $token exactly once; found $occurrences")
		}
	}
	$pattern = (($replacements.Keys | ForEach-Object { [regex]::Escape($_) }) -join '|')
	return [regex]::Replace($Template, $pattern, { param($match) $replacements[$match.Value] }.GetNewClosure())
}

function Connect-DriverSocket {
	param([string]$Address, [int]$Timeout)
	$uri = $null
	if (-not [System.Uri]::TryCreate($Address, [System.UriKind]::Absolute, [ref]$uri)) {
		throw [System.ArgumentException]::new("not a websocket url: $Address")
	}
	if ($uri.Scheme -ne 'ws' -and $uri.Scheme -ne 'wss') {
		throw [System.ArgumentException]::new("not a websocket url: $Address")
	}
	$socket = New-Object System.Net.WebSockets.ClientWebSocket
	$cancellation = New-Object System.Threading.CancellationTokenSource
	try {
		$cancellation.CancelAfter($Timeout)
		try {
			$socket.ConnectAsync($uri, $cancellation.Token).GetAwaiter().GetResult() | Out-Null
		}
		catch {
			if ($cancellation.IsCancellationRequested) {
				throw [System.TimeoutException]::new("the client did not accept the connection within $Timeout ms")
			}
			throw
		}
	}
	catch {
		$socket.Dispose()
		throw
	}
	finally {
		$cancellation.Dispose()
	}
	return $socket
}

function Send-DriverFrame {
	param([System.Net.WebSockets.ClientWebSocket]$Socket, [string]$Frame, [int]$Timeout)
	$bytes = [System.Text.Encoding]::UTF8.GetBytes($Frame)
	$segment = [System.ArraySegment[byte]]::new($bytes)
	$cancellation = New-Object System.Threading.CancellationTokenSource
	try {
		$cancellation.CancelAfter($Timeout)
		try {
			$Socket.SendAsync($segment, [System.Net.WebSockets.WebSocketMessageType]::Text, $true, $cancellation.Token).GetAwaiter().GetResult() | Out-Null
		}
		catch {
			if ($cancellation.IsCancellationRequested) {
				throw [System.TimeoutException]::new('the client did not accept the frame in time')
			}
			throw
		}
	}
	finally {
		$cancellation.Dispose()
	}
}

function Receive-DriverFrame {
	param([System.Net.WebSockets.ClientWebSocket]$Socket, [int]$Timeout)
	$buffer = New-Object byte[] 65536
	$stream = New-Object System.IO.MemoryStream
	$deadline = [System.Diagnostics.Stopwatch]::StartNew()
	try {
		while ($true) {
			$remaining = $Timeout - [int]$deadline.ElapsedMilliseconds
			if ($remaining -le 0) {
				throw [System.TimeoutException]::new('the client did not answer in time')
			}
			$cancellation = New-Object System.Threading.CancellationTokenSource
			try {
				$cancellation.CancelAfter($remaining)
				$segment = [System.ArraySegment[byte]]::new($buffer)
				try {
					$result = $Socket.ReceiveAsync($segment, $cancellation.Token).GetAwaiter().GetResult()
				}
				catch {
					if ($cancellation.IsCancellationRequested) {
						throw [System.TimeoutException]::new('the client did not answer in time')
					}
					throw
				}
			}
			finally {
				$cancellation.Dispose()
			}
			if ($result.MessageType -eq [System.Net.WebSockets.WebSocketMessageType]::Close) {
				throw [System.IO.EndOfStreamException]::new('the client closed the connection before answering')
			}
			$stream.Write($buffer, 0, $result.Count)
			if ($result.EndOfMessage) {
				break
			}
		}
		return [System.Text.Encoding]::UTF8.GetString($stream.ToArray())
	}
	finally {
		$stream.Dispose()
	}
}

function Invoke-DriverEval {
	param(
		[System.Net.WebSockets.ClientWebSocket]$Socket,
		[string]$Template,
		[string]$CommandName,
		[string]$Argument,
		[string]$TextValue,
		[int]$EvalTimeoutMs
	)
	$code = Expand-DriverCode -Template $Template -CommandName $CommandName -Argument $Argument -TextValue $TextValue
	$id = [guid]::NewGuid().ToString('N')
	$frame = @{ type = 'eval'; id = $id; code = $code; timeoutMs = $EvalTimeoutMs } | ConvertTo-Json -Compress
	Send-DriverFrame -Socket $Socket -Frame $frame -Timeout $EvalTimeoutMs

	$waitMs = $EvalTimeoutMs + 2000
	$deadline = [System.Diagnostics.Stopwatch]::StartNew()
	while ($true) {
		$remaining = $waitMs - [int]$deadline.ElapsedMilliseconds
		if ($remaining -le 0) {
			throw [System.TimeoutException]::new("no eval_result for '$CommandName' within $waitMs ms")
		}
		$message = (Receive-DriverFrame -Socket $Socket -Timeout $remaining) | ConvertFrom-Json
		if ($message.id -ne $id) {
			continue
		}
		if ($message.type -eq 'eval_result') {
			if ($message.truncated -eq $true) {
				throw [System.InvalidOperationException]::new("eval '$CommandName' answered with a truncated value; the answer is not usable")
			}
			if ([string]::IsNullOrEmpty($message.value)) {
				throw [System.InvalidOperationException]::new("eval '$CommandName' returned no value")
			}
			return ($message.value | ConvertFrom-Json)
		}
		if ($message.type -eq 'eval_error') {
			$errorCode = if ($message.error -and $message.error.code) { [string]$message.error.code } else { '' }
			$detail = if ($message.error -and $message.error.message) { $message.error.message } else { ($message.error | Out-String).Trim() }
			if ($errorCode -eq 'evalTimeout') {
				throw [System.TimeoutException]::new("the client aborted eval '$CommandName' at its own timeout: $detail")
			}
			$exception = New-Object System.Exception("eval '$CommandName' failed: $detail")
			$exception.Data['driverKind'] = 'eval-error'
			throw $exception
		}
		throw [System.InvalidOperationException]::new("unexpected frame type '$($message.type)'")
	}
}

function Get-DriverState {
	param([System.Net.WebSockets.ClientWebSocket]$Socket, [string]$Template, [int]$EvalTimeoutMs)
	return Invoke-DriverEval -Socket $Socket -Template $Template -CommandName 'state' -Argument '' -TextValue '' -EvalTimeoutMs $EvalTimeoutMs
}

function Get-RemainingBudget {
	param([System.Diagnostics.Stopwatch]$Clock, [int]$BudgetMs)
	return [Math]::Max(0, $BudgetMs - [int]$Clock.ElapsedMilliseconds)
}

function Wait-ForWindow {
	param([System.Net.WebSockets.ClientWebSocket]$Socket, [string]$Template, [int]$EvalTimeoutMs, [int]$BudgetMs, [int]$RetryDelayMs)
	$stopwatch = [System.Diagnostics.Stopwatch]::StartNew()
	$last = $null
	while ($stopwatch.ElapsedMilliseconds -lt $BudgetMs) {
		$last = Get-DriverState -Socket $Socket -Template $Template -EvalTimeoutMs $EvalTimeoutMs
		if ($last.ok -and $last.windowVisible) { return $last }
		Start-Sleep -Milliseconds $RetryDelayMs
	}
	return $last
}

function Wait-ForControl {
	param(
		[System.Net.WebSockets.ClientWebSocket]$Socket,
		[string]$Template,
		[int]$EvalTimeoutMs,
		[int]$BudgetMs,
		[int]$RetryDelayMs,
		[string]$CommandName,
		[string]$ControlName,
		[string]$Value
	)
	$stopwatch = [System.Diagnostics.Stopwatch]::StartNew()
	$last = $null
	while ($stopwatch.ElapsedMilliseconds -lt $BudgetMs) {
		$last = Invoke-DriverEval -Socket $Socket -Template $Template -CommandName $CommandName -Argument $ControlName -TextValue $Value -EvalTimeoutMs $EvalTimeoutMs
		if ($last.ok -and $last.applied) { return $last }
		Start-Sleep -Milliseconds $RetryDelayMs
	}
	return $last
}

function New-DriverSuccess {
	param([string]$ActionName, $Step)
	$result = @{ ok = $true; action = $ActionName }
	if ($null -ne $Step) { $result.result = $Step }
	return $result
}

function New-DriverFailure {
	param([string]$ActionName, [string]$Code, [string]$Detail, $Last)
	$result = @{ ok = $false; action = $ActionName; error = $Code; detail = $Detail }
	if ($null -ne $Last) { $result.last = $Last }
	return $result
}

function Write-DriverOutput {
	param($Result, [string]$JsonPath)
	$json = $Result | ConvertTo-Json -Depth 8
	Write-Output $json
	if (-not [string]::IsNullOrWhiteSpace($JsonPath)) {
		$directory = Split-Path -Parent $JsonPath
		if (-not [string]::IsNullOrWhiteSpace($directory) -and -not (Test-Path -LiteralPath $directory)) {
			[void](New-Item -ItemType Directory -Path $directory -Force)
		}
		[System.IO.File]::WriteAllText($JsonPath, $json, (New-Object System.Text.UTF8Encoding($false)))
	}
}

function Invoke-DriverAction {
	param(
		[System.Net.WebSockets.ClientWebSocket]$Socket,
		[string]$Template,
		[string]$ActionName,
		[int]$BudgetMs,
		[int]$EvalTimeoutMs,
		[string]$RetryDelayMs
	)

	$clock = [System.Diagnostics.Stopwatch]::StartNew()

	switch ($ActionName) {
		'ping' {
			$step = Invoke-DriverEval -Socket $Socket -Template $Template -CommandName 'ping' -Argument '' -TextValue '' -EvalTimeoutMs $EvalTimeoutMs
			if (-not $step.ok) { return New-DriverFailure -ActionName $ActionName -Code $step.error -Detail $step.detail -Last $step }
			return New-DriverSuccess -ActionName $ActionName -Step $step
		}
		'state' {
			$step = Get-DriverState -Socket $Socket -Template $Template -EvalTimeoutMs $EvalTimeoutMs
			if (-not $step.ok) { return New-DriverFailure -ActionName $ActionName -Code $step.error -Detail $step.detail -Last $step }
			return New-DriverSuccess -ActionName $ActionName -Step $step
		}
		'open-window' {
			$step = Invoke-DriverEval -Socket $Socket -Template $Template -CommandName 'open-window' -Argument '' -TextValue '' -EvalTimeoutMs $EvalTimeoutMs
			if (-not $step.ok) { return New-DriverFailure -ActionName $ActionName -Code $step.error -Detail $step.detail -Last $step }
			if ($step.visible) { return New-DriverSuccess -ActionName $ActionName -Step $step }
			$state = Wait-ForWindow -Socket $Socket -Template $Template -EvalTimeoutMs $EvalTimeoutMs -BudgetMs (Get-RemainingBudget -Clock $clock -BudgetMs $BudgetMs) -RetryDelayMs $RetryDelayMs
			if ($null -eq $state -or -not $state.windowVisible) {
				return New-DriverFailure -ActionName $ActionName -Code 'window-not-shown' -Detail 'the Online UI window did not become visible' -Last $state
			}
			return New-DriverSuccess -ActionName $ActionName -Step $state
		}
		'goto-page' {
			$target = $PageTitles[$Page]
			$state = Get-DriverState -Socket $Socket -Template $Template -EvalTimeoutMs $EvalTimeoutMs
			if (-not ($state.ok -and $state.windowVisible)) {
				$opened = Invoke-DriverEval -Socket $Socket -Template $Template -CommandName 'open-window' -Argument '' -TextValue '' -EvalTimeoutMs $EvalTimeoutMs
				if (-not $opened.ok) { return New-DriverFailure -ActionName $ActionName -Code $opened.error -Detail $opened.detail -Last $opened }
				$state = Get-DriverState -Socket $Socket -Template $Template -EvalTimeoutMs $EvalTimeoutMs
			}
			if ($state.page -eq $target) { return New-DriverSuccess -ActionName $ActionName -Step $state }
			$clicked = Wait-ForControl -Socket $Socket -Template $Template -EvalTimeoutMs $EvalTimeoutMs -BudgetMs (Get-RemainingBudget -Clock $clock -BudgetMs $BudgetMs) -RetryDelayMs $RetryDelayMs -CommandName 'click' -ControlName $PageIds[$Page] -Value ''
			if ($null -eq $clicked -or -not $clicked.applied) {
				return New-DriverFailure -ActionName $ActionName -Code 'control-not-offered' -Detail "the '$($PageIds[$Page])' tab was never offered" -Last $clicked
			}
			$state = Get-DriverState -Socket $Socket -Template $Template -EvalTimeoutMs $EvalTimeoutMs
			if ($state.page -ne $target) {
				return New-DriverFailure -ActionName $ActionName -Code 'page-not-switched' -Detail "the window still shows page '$($state.page)' instead of '$target'" -Last $state
			}
			return New-DriverSuccess -ActionName $ActionName -Step $state
		}
		'click' {
			$step = Wait-ForControl -Socket $Socket -Template $Template -EvalTimeoutMs $EvalTimeoutMs -BudgetMs (Get-RemainingBudget -Clock $clock -BudgetMs $BudgetMs) -RetryDelayMs $RetryDelayMs -CommandName 'click' -ControlName $ControlId -Value ''
			if ($step.ok -and $step.applied) { return New-DriverSuccess -ActionName $ActionName -Step $step }
			return New-DriverFailure -ActionName $ActionName -Code 'control-not-offered' -Detail "the control '$ControlId' was never offered" -Last $step
		}
		'set-text' {
			$step = Wait-ForControl -Socket $Socket -Template $Template -EvalTimeoutMs $EvalTimeoutMs -BudgetMs (Get-RemainingBudget -Clock $clock -BudgetMs $BudgetMs) -RetryDelayMs $RetryDelayMs -CommandName 'set-text' -ControlName $ControlId -Value $Text
			if (-not ($step.ok -and $step.applied)) {
				return New-DriverFailure -ActionName $ActionName -Code 'control-not-offered' -Detail "the field '$ControlId' was never offered" -Last $step
			}
			$state = Get-DriverState -Socket $Socket -Template $Template -EvalTimeoutMs $EvalTimeoutMs
			return New-DriverSuccess -ActionName $ActionName -Step $state
		}
		'create-lobby' {
			$state = Get-DriverState -Socket $Socket -Template $Template -EvalTimeoutMs $EvalTimeoutMs
			if (-not $state.ok) { return New-DriverFailure -ActionName $ActionName -Code $state.error -Detail $state.detail -Last $state }
			if ($state.lobby -ne '0') {
				if ($state.role -eq 'Host') { return New-DriverSuccess -ActionName $ActionName -Step $state }
				return New-DriverFailure -ActionName $ActionName -Code 'already-in-lobby' -Detail "the client is in lobby $($state.lobby) as '$($state.role)', not as host" -Last $state
			}
			$opened = Invoke-DriverEval -Socket $Socket -Template $Template -CommandName 'open-window' -Argument '' -TextValue '' -EvalTimeoutMs $EvalTimeoutMs
			if (-not $opened.ok) { return New-DriverFailure -ActionName $ActionName -Code $opened.error -Detail $opened.detail -Last $opened }
			$clicked = Wait-ForControl -Socket $Socket -Template $Template -EvalTimeoutMs $EvalTimeoutMs -BudgetMs (Get-RemainingBudget -Clock $clock -BudgetMs $BudgetMs) -RetryDelayMs $RetryDelayMs -CommandName 'click' -ControlName 'home.create_lobby' -Value ''
			if (-not ($clicked.ok -and $clicked.applied)) {
				return New-DriverFailure -ActionName $ActionName -Code 'control-not-offered' -Detail 'the Home page''s create control was never offered' -Last $clicked
			}
			$stopwatch = [System.Diagnostics.Stopwatch]::StartNew()
			$last = $clicked
			while ($stopwatch.ElapsedMilliseconds -lt (Get-RemainingBudget -Clock $clock -BudgetMs $BudgetMs)) {
				$state = Get-DriverState -Socket $Socket -Template $Template -EvalTimeoutMs $EvalTimeoutMs
				$last = $state
				if ($state.lobby -ne '0' -and $state.role -eq 'Host') { return New-DriverSuccess -ActionName $ActionName -Step $state }
				Start-Sleep -Milliseconds $RetryDelayMs
			}
			return New-DriverFailure -ActionName $ActionName -Code 'lobby-not-created' -Detail 'the create control was applied but no lobby id appeared' -Last $last
		}
		'join-lobby' {
			$target = $LobbyId
			$state = Get-DriverState -Socket $Socket -Template $Template -EvalTimeoutMs $EvalTimeoutMs
			if (-not $state.ok) { return New-DriverFailure -ActionName $ActionName -Code $state.error -Detail $state.detail -Last $state }
			if ($state.lobby -eq $target) { return New-DriverSuccess -ActionName $ActionName -Step $state }
			if ($state.lobby -ne '0') {
				return New-DriverFailure -ActionName $ActionName -Code 'already-in-lobby' -Detail "the client is in lobby $($state.lobby), not the requested $target" -Last $state
			}
			$opened = Invoke-DriverEval -Socket $Socket -Template $Template -CommandName 'open-window' -Argument '' -TextValue '' -EvalTimeoutMs $EvalTimeoutMs
			if (-not $opened.ok) { return New-DriverFailure -ActionName $ActionName -Code $opened.error -Detail $opened.detail -Last $opened }
			$state = Get-DriverState -Socket $Socket -Template $Template -EvalTimeoutMs $EvalTimeoutMs
			if ($state.page -ne 'Home') {
				$home = Wait-ForControl -Socket $Socket -Template $Template -EvalTimeoutMs $EvalTimeoutMs -BudgetMs (Get-RemainingBudget -Clock $clock -BudgetMs $BudgetMs) -RetryDelayMs $RetryDelayMs -CommandName 'click' -ControlName 'tab.home' -Value ''
				if (-not ($home.ok -and $home.applied)) {
					return New-DriverFailure -ActionName $ActionName -Code 'control-not-offered' -Detail 'the Home tab was never offered' -Last $home
				}
				$state = Get-DriverState -Socket $Socket -Template $Template -EvalTimeoutMs $EvalTimeoutMs
				if ($state.page -ne 'Home') {
					return New-DriverFailure -ActionName $ActionName -Code 'page-not-switched' -Detail "the window did not switch to Home (page '$($state.page)')" -Last $state
				}
			}
			$typed = Wait-ForControl -Socket $Socket -Template $Template -EvalTimeoutMs $EvalTimeoutMs -BudgetMs (Get-RemainingBudget -Clock $clock -BudgetMs $BudgetMs) -RetryDelayMs $RetryDelayMs -CommandName 'set-text' -ControlName 'home.lobby_id' -Value $target
			if (-not ($typed.ok -and $typed.applied)) {
				return New-DriverFailure -ActionName $ActionName -Code 'control-not-offered' -Detail 'the lobby id field was never offered' -Last $typed
			}
			$state = Get-DriverState -Socket $Socket -Template $Template -EvalTimeoutMs $EvalTimeoutMs
			if ($state.lobbyIdInput -ne $target) {
				return New-DriverFailure -ActionName $ActionName -Code 'text-not-applied' -Detail "the lobby id field holds '$($state.lobbyIdInput)' instead of the requested id" -Last $state
			}
			$joined = Wait-ForControl -Socket $Socket -Template $Template -EvalTimeoutMs $EvalTimeoutMs -BudgetMs (Get-RemainingBudget -Clock $clock -BudgetMs $BudgetMs) -RetryDelayMs $RetryDelayMs -CommandName 'click' -ControlName 'home.join' -Value ''
			if (-not ($joined.ok -and $joined.applied)) {
				return New-DriverFailure -ActionName $ActionName -Code 'control-not-offered' -Detail 'the join control was never offered' -Last $joined
			}
			$stopwatch = [System.Diagnostics.Stopwatch]::StartNew()
			$last = $joined
			while ($stopwatch.ElapsedMilliseconds -lt (Get-RemainingBudget -Clock $clock -BudgetMs $BudgetMs)) {
				$state = Get-DriverState -Socket $Socket -Template $Template -EvalTimeoutMs $EvalTimeoutMs
				$last = $state
				if ($state.lobby -eq $target) { return New-DriverSuccess -ActionName $ActionName -Step $state }
				Start-Sleep -Milliseconds $RetryDelayMs
			}
			return New-DriverFailure -ActionName $ActionName -Code 'join-not-entered' -Detail "the join was applied but the client did not settle into lobby $target" -Last $last
		}
		'start-run' {
			$state = Get-DriverState -Socket $Socket -Template $Template -EvalTimeoutMs $EvalTimeoutMs
			if (-not $state.ok) { return New-DriverFailure -ActionName $ActionName -Code $state.error -Detail $state.detail -Last $state }
			if ($state.inWorld) { return New-DriverSuccess -ActionName $ActionName -Step $state }
			$started = Invoke-DriverEval -Socket $Socket -Template $Template -CommandName 'start-run' -Argument '' -TextValue '' -EvalTimeoutMs $EvalTimeoutMs
			if (-not $started.ok) { return New-DriverFailure -ActionName $ActionName -Code $started.error -Detail $started.detail -Last $started }
			$stopwatch = [System.Diagnostics.Stopwatch]::StartNew()
			$last = $started
			while ($stopwatch.ElapsedMilliseconds -lt (Get-RemainingBudget -Clock $clock -BudgetMs $BudgetMs)) {
				$state = Get-DriverState -Socket $Socket -Template $Template -EvalTimeoutMs $EvalTimeoutMs
				$last = $state
				if ($state.inWorld -or $state.gateWaiting) { return New-DriverSuccess -ActionName $ActionName -Step $state }
				Start-Sleep -Milliseconds $RetryDelayMs
			}
			return New-DriverFailure -ActionName $ActionName -Code 'run-not-started' -Detail 'StartRun was called but the world never started generating' -Last $last
		}
		'quit' {
			$note = 'the client was asked to quit'
			try {
				[void](Invoke-DriverEval -Socket $Socket -Template $Template -CommandName 'quit' -Argument '' -TextValue '' -EvalTimeoutMs ([Math]::Min(2500, $EvalTimeoutMs)))
			}
			catch {
				$kind = if ($_.Exception.Data.Contains('driverKind')) { $_.Exception.Data['driverKind'] } else { '' }
				if ($kind -eq 'eval-error') { throw }
				$note = 'the client closed or stopped answering while it was being asked to quit'
			}
			return New-DriverSuccess -ActionName $ActionName -Step @{ quitting = $true; note = $note }
		}
		default {
			return New-DriverFailure -ActionName $ActionName -Code 'unknown-action' -Detail "unknown action '$ActionName'" -Last $null
		}
	}
}

# ---- entry ---------------------------------------------------------------------------------
if ($ListActions) {
	Write-Actions
	exit 0
}

if ([string]::IsNullOrWhiteSpace($Url)) {
	Write-Usage -Problem '-Url is required'
	exit 64
}
$urlProbe = $null
if (-not [System.Uri]::TryCreate($Url, [System.UriKind]::Absolute, [ref]$urlProbe) -or ($urlProbe.Scheme -ne 'ws' -and $urlProbe.Scheme -ne 'wss')) {
	Write-Usage -Problem '-Url must be a ws:// or wss:// websocket url'
	exit 64
}
if ([string]::IsNullOrWhiteSpace($Action)) {
	Write-Usage -Problem '-Action is required'
	exit 64
}
if ($TimeoutMs -lt 100) {
	Write-Usage -Problem '-TimeoutMs must be at least 100'
	exit 64
}
if ($RetryDelayMs -lt 1) {
	Write-Usage -Problem '-RetryDelayMs must be at least 1'
	exit 64
}

$missing = $null
switch ($Action) {
	'goto-page' {
		if ([string]::IsNullOrWhiteSpace($Page)) { $missing = '-Page' }
	}
	'click' {
		if ([string]::IsNullOrWhiteSpace($ControlId)) { $missing = '-ControlId' }
	}
	'set-text' {
		if ([string]::IsNullOrWhiteSpace($ControlId)) { $missing = '-ControlId' }
		elseif (-not $PSBoundParameters.ContainsKey('Text')) { $missing = '-Text' }
	}
	'join-lobby' {
		if ($LobbyId -notmatch '^[0-9]+$') { $missing = '-LobbyId (digits only)' }
	}
	default { }
}
if ($missing) {
	Write-Usage -Problem "$Action needs $missing"
	exit 64
}

$templatePath = Join-Path $PSScriptRoot 'driver\InProcessDriver.cs'
if (-not (Test-Path -LiteralPath $templatePath)) {
	Write-Usage -Problem "the in-process template does not resolve: $templatePath"
	exit 64
}
$template = Get-Content -LiteralPath $templatePath -Raw -Encoding UTF8

$evalTimeoutMs = [Math]::Min(10000, [Math]::Max(1000, $TimeoutMs))
$connectTimeoutMs = [Math]::Min(5000, [Math]::Max(500, $TimeoutMs + 500))

$result = $null
$exitCode = 0
$socket = $null
try {
	$socket = Connect-DriverSocket -Address $Url -Timeout $connectTimeoutMs
	$result = Invoke-DriverAction -Socket $socket -Template $template -ActionName $Action -BudgetMs $TimeoutMs -EvalTimeoutMs $evalTimeoutMs -RetryDelayMs $RetryDelayMs
}
catch {
	$thrown = $_.Exception
	$kind = if ($thrown.Data.Contains('driverKind')) { $thrown.Data['driverKind'] } else { '' }
	$exception = $thrown
	while ($null -ne $exception.InnerException -and ($exception -is [System.Management.Automation.MethodInvocationException] -or $exception -is [System.AggregateException])) {
		$exception = $exception.InnerException
		if ($exception.Data.Contains('driverKind') -and $exception.Data['driverKind'] -eq 'eval-error') { $kind = 'eval-error' }
	}
	$exceptionName = if ($null -eq $exception) { '' } else { $exception.GetType().FullName }
	if ($kind -eq 'eval-error') {
		$result = New-DriverFailure -ActionName $Action -Code 'eval-error' -Detail $thrown.Message -Last $null
		$exitCode = 1
	}
	elseif ($exceptionName -in @('System.TimeoutException', 'System.OperationCanceledException', 'System.Threading.Tasks.TaskCanceledException')) {
		$result = New-DriverFailure -ActionName $Action -Code 'timeout' -Detail $exception.Message -Last $null
		$exitCode = 3
	}
	elseif ($exceptionName -in @('System.Net.WebSockets.WebSocketException', 'System.Net.Sockets.SocketException', 'System.Net.WebException', 'System.Net.Http.HttpRequestException', 'System.IO.IOException', 'System.IO.EndOfStreamException', 'System.ObjectDisposedException')) {
		$result = New-DriverFailure -ActionName $Action -Code 'transport' -Detail $exception.Message -Last $null
		$exitCode = 2
	}
	else {
		$result = New-DriverFailure -ActionName $Action -Code 'driver-error' -Detail $exception.Message -Last $null
		$exitCode = 4
	}
}
finally {
	if ($null -ne $socket) { $socket.Dispose() }
}

if ($null -eq $result) {
	$result = New-DriverFailure -ActionName $Action -Code 'driver-error' -Detail 'the driver produced no result' -Last $null
	$exitCode = 2
}
if ($exitCode -eq 0 -and -not $result.ok) {
	$exitCode = 1
}

Write-DriverOutput -Result $result -JsonPath $JsonPath
exit $exitCode
