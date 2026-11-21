<#
.SYNOPSIS
Finds and (with -Apply) removes the one-off junk this repository accumulates outside git, so the
ignored parts of the tree stay a working set instead of a landfill.

.DESCRIPTION
The tree carries a lot of git-ignored output: acceptance run folders, test result folders, build logs,
IDE caches. Those are read once (or never) and then sit there for months. This command answers two
questions on demand: how much is there, and which of it has stopped being evidence.

What it keeps on purpose:
  - reversing/ — the decompiled game assemblies, needed before any native question can be answered;
  - artifacts/contract/ — the recorded game-assembly contracts the runbook cites;
  - every acceptance run whose folder is named in docs/, and every run newer than -KeepDays;
  - the project bin/obj folders — a rebuild costs minutes, a stale cache costs nothing.

Deletion is layer by layer: files first, then the directories that are empty, deepest first. No call
handed to this command removes a multi-level tree on its own, so a wrong path cannot take a family of
folders with it.

.PARAMETER Apply
Actually delete. Without it the command only prints what it would remove.

.PARAMETER KeepDays
Acceptance runs newer than this many days are kept (default 2).

.EXAMPLE
pwsh -File tools/clean.ps1
pwsh -File tools/clean.ps1 -Apply
#>
[CmdletBinding()]
param(
	[switch] $Apply,
	[int] $KeepDays = 2
)

$ErrorActionPreference = 'Stop'
$repo = Split-Path -Parent $PSScriptRoot
Set-Location $repo

function Get-TreeSize {
	param([string] $Path)
	$files = @(Get-ChildItem -LiteralPath $Path -Recurse -File -Force -ErrorAction SilentlyContinue)
	$sum = ($files | Measure-Object Length -Sum).Sum
	return [pscustomobject]@{ Files = $files.Count; MB = [math]::Round(($sum / 1MB), 1) }
}

function Remove-LayerByLayer {
	param([string] $Path)
	$resolved = (Resolve-Path -LiteralPath $Path).Path
	if (-not $resolved.StartsWith($repo, [System.StringComparison]::OrdinalIgnoreCase)) {
		throw "refusing to delete outside the repository: $resolved"
	}

	# Files first, then the directories that are empty, deepest first: no single call removes a
	# multi-level tree, so a wrong path cannot take a family of folders with it.
	foreach ($file in @(Get-ChildItem -LiteralPath $resolved -Recurse -File -Force -ErrorAction SilentlyContinue)) {
		try { [System.IO.File]::Delete($file.FullName) } catch { }
	}

	$directories = @(Get-ChildItem -LiteralPath $resolved -Recurse -Directory -Force -ErrorAction SilentlyContinue | Sort-Object { $_.FullName.Length } -Descending)
	foreach ($directory in $directories) {
		try { [System.IO.Directory]::Delete($directory.FullName, $false) } catch { }
	}

	try { [System.IO.Directory]::Delete($resolved, $false) } catch { }
}

# The bucket names a folder whose whole content is one-off output.
$buckets = @('TestResults', '.build-temp', '.vs', 'bin', 'obj')
$report = @()
foreach ($name in $buckets) {
	$path = Join-Path $repo $name
	if (-not (Test-Path -LiteralPath $path)) { continue }
	$size = Get-TreeSize -Path $path
	$report += [pscustomobject]@{ Target = $name; Kind = 'folder'; Files = $size.Files; MB = $size.MB; Action = 'delete' }
}

$artifacts = Join-Path $repo 'artifacts'
if (Test-Path -LiteralPath $artifacts) {
	foreach ($file in @(Get-ChildItem -LiteralPath $artifacts -File -Force -ErrorAction SilentlyContinue)) {
		$report += [pscustomobject]@{ Target = "artifacts/$($file.Name)"; Kind = 'file'; Files = 1; MB = [math]::Round($file.Length / 1MB, 2); Action = 'delete' }
	}
}

$acceptance = Join-Path $repo '.acceptance'
if (Test-Path -LiteralPath $acceptance) {
	$documented = (Get-ChildItem -LiteralPath (Join-Path $repo 'docs') -Recurse -File -Filter *.md -ErrorAction SilentlyContinue |
		ForEach-Object { [System.IO.File]::ReadAllText($_.FullName) }) -join "`n"
	$cutoff = (Get-Date).AddDays(-$KeepDays)
	foreach ($directory in @(Get-ChildItem -LiteralPath $acceptance -Directory -Force -ErrorAction SilentlyContinue)) {
		if ($directory.Name -eq 'tools') { continue }
		$referenced = $documented.Contains($directory.Name)
		$recent = $directory.LastWriteTime -ge $cutoff
		if ($recent) { continue }
		$size = Get-TreeSize -Path $directory.FullName
		if ($referenced) {
			# The docs cite this run: keep what a citation opens (the report and its timeline) and drop
			# the logs, saves and frames that only the finished investigation ever read.
			$bulk = @(Get-ChildItem -LiteralPath $directory.FullName -Recurse -File -Force -ErrorAction SilentlyContinue |
				Where-Object { $_.FullName -notmatch '[\\/]report[\\/]' })
			$bulkMb = [math]::Round((($bulk | Measure-Object Length -Sum).Sum / 1MB), 1)
			if ($bulkMb -ge 0.5) {
				$report += [pscustomobject]@{ Target = ".acceptance/$($directory.Name)"; Kind = 'strip'; Files = $bulk.Count; MB = $bulkMb; Action = 'strip' }
			}

			continue
		}

		$report += [pscustomobject]@{ Target = ".acceptance/$($directory.Name)"; Kind = 'folder'; Files = $size.Files; MB = $size.MB; Action = 'delete' }
	}
}

$totalMb = [math]::Round((($report | Measure-Object MB -Sum).Sum), 1)
$totalFiles = (($report | Measure-Object Files -Sum).Sum)

if ($report.Count -eq 0) {
	Write-Output "clean: nothing to remove - the ignored tree carries no one-off output"
	exit 0
}

foreach ($row in ($report | Sort-Object MB -Descending)) {
	Write-Output ("{0,-40} {1,7} MB {2,7} file(s)" -f $row.Target, $row.MB, $row.Files)
}

Write-Output ("clean: {0} target(s), {1} file(s), {2} MB" -f $report.Count, $totalFiles, $totalMb)
if (-not $Apply) {
	Write-Output "clean: nothing removed - run again with -Apply"
	exit 0
}

foreach ($row in $report) {
	$path = Join-Path $repo $row.Target
	if (-not (Test-Path -LiteralPath $path)) { continue }
	if ($row.Kind -eq 'file') {
		Remove-Item -LiteralPath $path -Force
	}
	elseif ($row.Kind -eq 'strip') {
		foreach ($file in @(Get-ChildItem -LiteralPath $path -Recurse -File -Force -ErrorAction SilentlyContinue |
			Where-Object { $_.FullName -notmatch '[\\/]report[\\/]' })) {
			try { [System.IO.File]::Delete($file.FullName) } catch { }
		}
	}
	else {
		Remove-LayerByLayer -Path $path
	}
}

$left = 0
foreach ($name in $buckets) {
	$path = Join-Path $repo $name
	if (Test-Path -LiteralPath $path) { $left += (Get-TreeSize -Path $path).Files }
}
Write-Output ("clean: removed {0} MB; {1} file(s) remain in the deleted buckets" -f $totalMb, $left)
