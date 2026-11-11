# Runs the solution's build and tests with a private temp directory.
#
# Why: MSBuild writes every <Exec> through a `.exec.cmd` under %TEMP%\MSBuildTemp*, and this
# machine's antivirus flags those files on every build. Pointing TEMP/TMP at the repository's
# own `.build-temp/` (gitignored) keeps the compiler's temporary executables out of %TEMP%.
#
#   pwsh -File tools/build.ps1              # build
#   pwsh -File tools/build.ps1 -Test        # build + the full test suite
#   pwsh -File tools/build.ps1 -Gates       # build + the normative gates
#   pwsh -File tools/build.ps1 -Format      # format only
[CmdletBinding()]
param(
	[switch]$Test,
	[switch]$Gates,
	[switch]$Format
)

$ErrorActionPreference = 'Stop'
$root = Split-Path -Parent $PSScriptRoot
$temp = Join-Path $root '.build-temp'
New-Item -ItemType Directory -Force -Path $temp | Out-Null
$env:TEMP = $temp
$env:TMP = $temp
# A reused MSBuild node keeps the TEMP it started with, so its temporary `.exec.cmd` files
# would still land in %TEMP%; every invocation gets its own node instead.
$env:MSBUILDDISABLENODEREUSE = '1'

Push-Location $root
try {
	if ($Format) {
		& dotnet format CasualtiesUnknownOnline.slnx
		exit $LASTEXITCODE
	}

	& dotnet build CasualtiesUnknownOnline.slnx -nodeReuse:false
	if ($LASTEXITCODE -ne 0) { exit $LASTEXITCODE }

	if ($Test) {
		& dotnet test tests/CasualtiesUnknownOnline.Tests/CasualtiesUnknownOnline.Tests.csproj -nodeReuse:false
		exit $LASTEXITCODE
	}

	if ($Gates) {
		& dotnet test tests/CasualtiesUnknownOnline.NormativeGates.Tests/CasualtiesUnknownOnline.NormativeGates.Tests.csproj -nodeReuse:false
		exit $LASTEXITCODE
	}

	exit 0
}
finally {
	Pop-Location
}

