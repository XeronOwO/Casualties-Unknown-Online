#Requires -Version 5.1
<#
.SYNOPSIS
Builds the acceptance suite first, launches a clean client pair, then starts the suite immediately.

.DESCRIPTION
The suite used to be started after the game pair was already sitting at the main menu, and `dotnet run` then spent
its build time before the first driver connection. The screen showed a ready game and no connection for tens of
seconds. This command removes that gap:

1. build the suite before any client starts;
2. launch the pair and wait only for the driver endpoints;
3. start the prebuilt suite with `--no-build`.

The printed timings separate the suite build, the launch, and the suite wall time, so a future regression cannot hide
behind a single number.

.PARAMETER GameDir
The game install to deploy/launch. Required; this file carries no machine path.

.PARAMETER OutDir
Acceptance output directory (report, client logs).

.PARAMETER Suite
Suite name passed to the acceptance runner (`all` by default).

.PARAMETER Fresh
Ask the suite to create a fresh pair instead of reusing an existing world.

.PARAMETER ParallelSetup
Start the host's run and the member's join concurrently.

.PARAMETER Reuse
Do not restart the named clients; reuse the ones already running.

.PARAMETER NoLaunch
Run the suite against clients that are already up (implies -Reuse).
#>
param(
	[Parameter(Mandatory = $true)]
	[string]$GameDir,
	[string]$OutDir = ".acceptance\suite",
	[string]$Suite = "all",
	[switch]$Fresh,
	[switch]$ParallelSetup,
	[switch]$Reuse,
	[switch]$NoLaunch
)

$ErrorActionPreference = "Stop"

$repoRoot = Split-Path -Parent (Split-Path -Parent $PSScriptRoot)
$project = Join-Path $repoRoot "tests\CasualtiesUnknownOnline.AcceptanceSuite"
$launcher = Join-Path $PSScriptRoot "launch-clients.ps1"
$outPath = Join-Path $repoRoot $OutDir
$reportPath = Join-Path $outPath "report"

New-Item -ItemType Directory -Force -Path $outPath | Out-Null

$buildWatch = [System.Diagnostics.Stopwatch]::StartNew()
dotnet build $project --nologo
if ($LASTEXITCODE -ne 0)
{
	throw "the acceptance suite did not build"
}
$buildWatch.Stop()

$launchWatch = [System.Diagnostics.Stopwatch]::StartNew()
if (-not $NoLaunch)
{
	$launchArgs = @{
		OutDir = $outPath
		GameDir = $GameDir
		AllowOfflineSteam = $true
	}
	if (-not $Reuse)
	{
		$launchArgs.Restart = $true
	}
	& $launcher @launchArgs
	if ($LASTEXITCODE -ne 0)
	{
		throw "the acceptance clients did not start"
	}
}
$launchWatch.Stop()

$suiteArgs = @(
	"--suite", $Suite,
	"--out", $reportPath,
	"--game-dir", $GameDir
)
if ($Fresh)
{
	$suiteArgs += "--fresh"
}
if ($ParallelSetup)
{
	$suiteArgs += "--parallel-setup"
}

$suiteWatch = [System.Diagnostics.Stopwatch]::StartNew()
dotnet run --no-build --project $project -- @suiteArgs
$suiteExit = $LASTEXITCODE
$suiteWatch.Stop()

Write-Host ("suite build: {0:N1}s; launch to endpoints: {1:N1}s; suite wall: {2:N1}s; exit: {3}" -f `
	$buildWatch.Elapsed.TotalSeconds, $launchWatch.Elapsed.TotalSeconds, $suiteWatch.Elapsed.TotalSeconds, $suiteExit)
exit $suiteExit
