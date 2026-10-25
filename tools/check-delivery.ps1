# The delivery checklist as a SCRIPT, run before a commit - not a test.
#
# Why a script: a checked box is the cycle's process record, so the moment the record is validated it must stop being
# green for the next cycle. A test cannot do that (it runs inside `dotnet test`, cannot reset the file, and a stale
# fully-ticked file certifies every later commit - the defect the user found on 2026-10-25: a whole session of commits
# ran against a previous cycle's eleven checked boxes). So: fill the boxes while working, run this before the commit.
# It validates, ARCHIVES the filled record under docs/evidence/checklists/, resets every box to `[ ]`, and the commit
# carries both the archive and the clean checklist the next cycle must re-tick.
#
# Usage:
#   powershell -ExecutionPolicy Bypass -File tools/check-delivery.ps1            # validate + archive + reset
#   powershell -ExecutionPolicy Bypass -File tools/check-delivery.ps1 -CheckOnly # validate only, no reset
#
# Fast loop (the user's 2026-10-25 note: a 3-minute format+test per step is pure waste; the full run happens ONCE,
# right before the commit):
#   dotnet build CasualtiesUnknownOnline.slnx                                      # once, after edits
#   dotnet format CasualtiesUnknownOnline.slnx --include <changed files> --no-restore
#   dotnet test tests/.../NormativeGates.Tests.csproj --no-build --filter "FullyQualifiedName~<Gate>"
#   dotnet test tests/.../CasualtiesUnknownOnline.Tests.csproj --no-build --filter "FullyQualifiedName~<Area>"
#   dotnet test CasualtiesUnknownOnline.slnx --no-build                            # the ONE full run before the commit
param(
	[switch]$CheckOnly
)

$ErrorActionPreference = 'Stop'
$repo = Split-Path -Parent $PSScriptRoot
$checklistPath = Join-Path $repo 'docs/evidence/delivery-checklist.md'
$archiveDir = Join-Path $repo 'docs/evidence/checklists'

# The separator between a box's item and its evidence, built from its code point so no editor can change the character.
$emdash = [string][char]0x2014
# A box may continue its name with a parenthesis ("Entry mapping (a scope that …)"); built from its code point for the
# same reason as the dash.
$openParen = [string][char]0x28

$required = @(
	'Design comprehension'
	, 'Native parity'
	, 'Native parity probe'
	, 'Entry mapping'
	, 'Mechanism inventory'
	, 'Whole-family audit'
	, 'Self-check table'
	, 'Verification design'
	, 'Plan approved by the user'
	, 'Structure review done'
	, 'Build + dotnet format + dotnet test normative gates pass'
	, 'Release-cycle deployment/acceptance'
)
$openByDesign = @('Release-cycle deployment/acceptance', 'FORBIDDEN')

$lines = [System.IO.File]::ReadAllLines($checklistPath)
$text = [System.IO.File]::ReadAllText($checklistPath)

# Parse the boxes with their continuation lines: a box's evidence may sit on the following indented lines, so an entry
# is the box line plus everything indented under it.
$entries = @()
$current = $null
foreach ($line in $lines) {
	if ($line -match '^- \[( |x)\] (.+)$') {
		if ($null -ne $current) { $entries += $current }
		$current = [pscustomobject]@{ State = $Matches[1]; Body = $Matches[2]; Text = $Matches[2] }
		continue
	}

	if ($null -ne $current -and $line -match '^ {2,}\S') {
		$current.Text = $current.Text + ' ' + $line.Trim()
		continue
	}

	if ($null -ne $current) { $entries += $current; $current = $null }
}

if ($null -ne $current) { $entries += $current }

# A required item matches a box whose body IS the name or continues it after a separator - never a longer name that
# merely begins with it ("Native parity probe" must not stand in for "Native parity").
$failures = @()
foreach ($item in $required) {
	$box = $null
	foreach ($entry in $entries) {
		if ($entry.Body -eq $item) { $box = $entry; break }
		if ($entry.Body -like ($item + ' ' + $emdash + '*')) { $box = $entry; break }
		if ($entry.Body -like ($item + ' ' + $openParen + '*')) { $box = $entry; break }
		if ($entry.Body -like ($item + ': *')) { $box = $entry; break }
	}

	if ($null -eq $box) {
		$failures += "missing box: " + $item
		continue
	}

	if ($openByDesign -contains $item) { continue }
	if ($box.State -ne 'x') {
		$failures += "unchecked: " + $item
		continue
	}

	if ($box.Text -notmatch (' ' + $emdash + ' evidence: \S')) {
		$failures += "no evidence: " + $item
	}
}

if ($failures.Count -gt 0) {
	Write-Host "check-delivery: the cycle's checklist is incomplete ($($failures.Count) item(s))."
	$failures | ForEach-Object { Write-Host "  $_" }
	Write-Host 'Fill each box with one line of evidence from THIS cycle, then run this script again.'
	exit 1
}

$checked = 0
foreach ($entry in $entries) { if ($entry.State -eq 'x') { $checked++ } }

# The language rule runs HERE, not in a separate step somebody can forget: "committed content is English" is a property of the DIFF, so it
# cannot be a code gate (a test sees the tree, and the tree legitimately holds translated quotes in its historical records). Anchoring it to
# the one entry point that must pass before a commit is the hard version of that rule.
$languageScript = Join-Path $PSScriptRoot 'check-language.ps1'
if (Test-Path -LiteralPath $languageScript) {
	& $languageScript
	if ($LASTEXITCODE -ne 0) {
		Write-Host 'check-delivery: the language check failed - fix the staged wording before committing.'
		exit 1
	}
}

Write-Host "check-delivery: green - $checked box(es) carry this cycle's evidence."

if ($CheckOnly) { exit 0 }

# Archive the validated record so the commit keeps it, then reset the checklist for the next cycle.
if (-not (Test-Path -LiteralPath $archiveDir)) {
	New-Item -ItemType Directory -Path $archiveDir | Out-Null
}

$stamp = Get-Date -Format 'yyyyMMdd-HHmm'
$archive = Join-Path $archiveDir ("cycle-" + $stamp + ".md")
[System.IO.File]::WriteAllText($archive, $text, (New-Object System.Text.UTF8Encoding($false)))
Write-Host ("archived this cycle's record: docs/evidence/checklists/cycle-" + $stamp + ".md")

# The reset drops the EVIDENCE with the checkmark, and it has to drop it from the box's whole BLOCK: an evidence suffix sits on its own
# continuation line whenever the statement is longer than one line, and a reset that only stripped the first line left that suffix behind — which
# is how a stale checklist certified the next cycle's commits (measured 2026-10-25: after a reset, five boxes still carried the previous cycle's
# evidence on their continuation lines and the script answered green).
$reset = [System.Collections.Generic.List[string]]::new()
$inBox = $false
foreach ($line in $lines) {
	if ($line -match '^- \[([ x])\] (.+)$') {
		$inBox = $true
		$name = $Matches[2]
		$cut = $name.IndexOf(' ' + $emdash + ' evidence:', [System.StringComparison]::Ordinal)
		if ($cut -ge 0) { $name = $name.Substring(0, $cut) }
		$reset.Add("- [ ] " + $name)
		continue
	}

	if ($inBox -and $line.TrimStart().StartsWith($emdash + ' evidence:', [System.StringComparison]::Ordinal)) {
		continue
	}

	# A box's block ends at a blank line or a new heading — NOT at a continuation line, which starts with spaces. Clearing the flag on the first
	# continuation line left every multi-line box's evidence behind (measured 2026-10-25: a reset kept five boxes' evidence that way).
	if ($line.Trim().Length -eq 0 -or $line -match '^#') {
		$inBox = $false
	}

	$reset.Add($line)
}

[System.IO.File]::WriteAllText($checklistPath, (($reset -join "`n") + "`n"), (New-Object System.Text.UTF8Encoding($false)))
Write-Host 'reset every box: the next cycle must tick them again.'
exit 0
