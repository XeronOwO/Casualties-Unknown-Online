<#
.SYNOPSIS
Prints the design, architecture and lesson pages that carry a topic, so the work starts from what
the project already recorded instead of from a guess made while reading code.

.DESCRIPTION
The docs tree is large and nothing forces a reader to open it; this command is the forced moment.
Run it before designing or diagnosing an area, with the words a player or a log would use ("bandage",
"pointer", "geyser", "remote backpack"). It searches the design pages, the architecture pages, the
acceptance lesson ledger, the decisions record and the evidence index, and prints the matching lines
with their file and line number - the reader then opens the page it names.

With -Changed it runs the other way: it reads the files this checkout has changed and prints, for
each one, the pages that carry its words, so the reader can update the pages this change made false.

A rule that binds the work belongs in a rule file or an AGENTS page; this command only points at the
pages that already answer "what is this supposed to do, who owns it, and what has it taught us".

.PARAMETER Query
One or more words; a page matching ANY word is printed, ranked by how many of the words it carries.

.PARAMETER Changed
The write-back direction: prints the pages each changed file's words carry.

.PARAMETER Limit
How many matching lines to print in total (default 40).

.EXAMPLE
pwsh -File tools/knowledge.ps1 -Query "bandage minigame pointer"

.EXAMPLE
pwsh -File tools/knowledge.ps1 -Changed
#>
[CmdletBinding()]
param(
	[Parameter(Mandatory = $true, ParameterSetName = 'Query')]
	[string[]] $Query,
	[Parameter(Mandatory = $true, ParameterSetName = 'Changed')]
	[switch] $Changed,
	[int] $Limit = 40
)

$ErrorActionPreference = 'Stop'
$root = Split-Path -Parent $PSScriptRoot

$paths = @(
	Join-Path $root 'docs/design'
	Join-Path $root 'docs/architecture'
	Join-Path $root 'docs/decisions'
	Join-Path $root 'docs/acceptance/lessons.md'
	Join-Path $root 'docs/evidence/verification.md'
)

$files = @()
foreach ($path in $paths) {
	if (Test-Path $path) {
		$files += Get-ChildItem -Path $path -Recurse -File -Filter *.md -ErrorAction SilentlyContinue
	}
}

$pages = @()
foreach ($file in $files) {
	$pages += [pscustomobject]@{
		Name  = $file.FullName.Substring($root.Length + 1).Replace('\', '/')
		Lines = [System.IO.File]::ReadAllLines($file.FullName)
	}
}

function Search-Pages {
	param([string[]] $Terms, [int] $Top)

	$hits = @()
	foreach ($page in $pages) {
		for ($index = 0; $index -lt $page.Lines.Length; $index++) {
			$line = $page.Lines[$index]
			$score = 0
			foreach ($term in $Terms) {
				if ($line.IndexOf($term, [System.StringComparison]::OrdinalIgnoreCase) -ge 0) { $score++ }
			}
			if ($score -gt 0) {
				$hits += [pscustomobject]@{ Score = $score; File = $page.Name; Line = $index + 1; Text = $line.Trim() }
			}
		}
	}
	return $hits | Sort-Object -Property @{ Expression = 'Score'; Descending = $true }, File, Line | Select-Object -First $Top
}

$lineWords = @{}
$lineTotal = 0
foreach ($page in $pages) {
	foreach ($line in $page.Lines) {
		$lineTotal++
		$seen = @{}
		foreach ($piece in ([regex]::Matches($line, '[A-Za-z]{4,}') | ForEach-Object { $_.Value.ToLowerInvariant() })) {
			if ($seen.ContainsKey($piece)) { continue }
			$seen[$piece] = $true
			$lineWords[$piece] = 1 + ($lineWords[$piece] ?? 0)
		}
	}
}
$commonCeiling = [Math]::Max(50, [int]($lineTotal * 0.02))

function Split-Words {
	param([string] $Text)
	$stem = [System.IO.Path]::GetFileNameWithoutExtension($Text)
	$words = @($stem.ToLowerInvariant())
	foreach ($part in ($Text -split '[^A-Za-z0-9]+')) {
		if ($part.Length -lt 4) { continue }
		foreach ($piece in ([regex]::Matches($part, '[A-Z]?[a-z]+|[A-Z]+(?![a-z])|[0-9]+') | ForEach-Object { $_.Value })) {
			if ($piece.Length -lt 5) { continue }
			$word = $piece.ToLowerInvariant()
			$count = $lineWords[$word]
			if ($null -eq $count) { continue }
			if ($count -gt $commonCeiling) { continue }
			$words += $word
		}
	}
	return ($words | Select-Object -Unique)
}

if ($Changed) {
	$touched = @()
	foreach ($line in (git -C $root status --porcelain --untracked-files=all)) {
		if ($line.Length -lt 4) { continue }
		$touched += $line.Substring(3).Trim().Trim('"')
	}
	$touched = $touched | Where-Object { $_ -match '^(src|tests|tools)/' -and $_ -match '\.(cs|ps1|csproj|json|slnx)$' }
	if ($touched.Length -eq 0) {
		Write-Output "knowledge: nothing under src/, tests/ or tools/ has changed - there is no page to write back"
		exit 0
	}

	$reported = 0
	foreach ($file in $touched) {
		$words = Split-Words -Text $file
		$stem = [System.IO.Path]::GetFileNameWithoutExtension($file)
		$all = @(Search-Pages -Terms $words -Top 24)
		$hits = @($all | Where-Object { $_.Text.Length -le 240 } | Where-Object { $_.Score -ge 2 -or $_.Text.IndexOf($stem, [System.StringComparison]::OrdinalIgnoreCase) -ge 0 } | Select-Object -First 3)
		if ($hits.Length -eq 0) { $hits = @($all | Where-Object { $_.Text.Length -le 240 } | Select-Object -First 2) }
		if ($hits.Length -eq 0) { continue }
		Write-Output ""
		Write-Output ("changed: {0}" -f $file)
		foreach ($hit in $hits) {
			$text = $hit.Text
			if ($text.Length -gt 160) { $text = $text.Substring(0, 160) + '...' }
			Write-Output ("  {0}:{1}: {2}" -f $hit.File, $hit.Line, $text)
			$reported++
		}
	}
	if ($reported -eq 0) {
		Write-Output "knowledge: no page names these files' words - if this change invented an area, its design page is missing; write it"
		exit 1
	}
	Write-Output ""
	Write-Output "knowledge: update the pages above whose description this change made false, before the commit"
	exit 0
}

$terms = @()
foreach ($word in $Query) {
	foreach ($part in ($word -split '\s+')) {
		if ($part.Trim().Length -ge 3) { $terms += $part.Trim() }
	}
}
if ($terms.Length -eq 0) {
	Write-Output "knowledge: give at least one word of three letters or more"
	exit 2
}

$found = @(Search-Pages -Terms $terms -Top $Limit)
if ($found.Length -eq 0) {
	Write-Output "knowledge: no page carries '$($terms -join ' ')' - the design page for this area is missing; write it before changing the code"
	exit 1
}

foreach ($hit in $found) {
	$text = $hit.Text
	if ($text.Length -gt 200) { $text = $text.Substring(0, 200) + '...' }
	Write-Output ("{0}:{1}: {2}" -f $hit.File, $hit.Line, $text)
}
Write-Output ("knowledge: {0} matching line(s), {1} shown - open the pages above before reading code" -f $found.Length, $found.Count)
