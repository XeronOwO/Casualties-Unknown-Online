# Committed content is English: fails on non-English WORDING in the STAGED diff, in every language.
#
# The rule is "English prose", not "no Chinese": German, French, Spanish, Russian, Japanese and Chinese are the same
# defect. The mechanical check is therefore "no non-ASCII LETTER" — an umlaut, an accent, a CJK ideograph and a Cyrillic
# letter are all Unicode letters, while the typography English prose legitimately carries (em dash, ellipsis, curly
# quotes, arrows, check marks, the minus sign) is punctuation or a symbol and is left alone. That keeps the check
# language-agnostic and precise: a Chinese sentence carries letters even when its punctuation is full width.
#
# Why a pre-commit check and not a gate: the rule's reach is "whatever this change ADDS", and a repository-wide scan
# cannot express that — `docs/zh/` is a legitimate Chinese block, `zh.json` is the Chinese half of every player-facing
# string, the pinyin dictionary is CJK by definition, and a fixture that tests non-English handling must contain it.
# A gate over paths would either miss a new ticket or fail on those, so the check reads what is about to be
# committed, exactly like RepositoryGateTests does for machine paths.
#
# Usage:  powershell -ExecutionPolicy Bypass -File tools/check-language.ps1
#         powershell -ExecutionPolicy Bypass -File tools/check-language.ps1 -WorkingTree   # uncommitted files instead
# Exit:   0 = clean, 1 = non-English wording outside the exemptions (file:line printed).
param(
	[switch]$WorkingTree
)

$ErrorActionPreference = 'Stop'
$repo = Split-Path -Parent $PSScriptRoot
Push-Location $repo
try {
	# The exemptions, by path: the Chinese human block, the Chinese catalogue, the pinyin dictionary, and the fixtures
	# whose whole purpose is non-English text.
	$exempt = @(
		'^docs/zh/'
		, '^README\.zh\.md$'
		, '/zh\.json$'
		, '^src/CasualtiesUnknownOnline\.PinyinSearch\.Core/Search/pinyin_data\.txt$'
		, '^docs/standard/terminology\.txt$'
		, '^tests/CasualtiesUnknownOnline\.Tests/PinyinSearch/'
		, '^tests/CasualtiesUnknownOnline\.Tests/Session/LocalFavouriteMarksTests\.cs$'
		, '^tests/CasualtiesUnknownOnline\.Tests/Localization/'
		, '^tests/CasualtiesUnknownOnline\.Tests/Content/'
		, '^tests/CasualtiesUnknownOnline\.Tests/Mods/'
		, '^tests/CasualtiesUnknownOnline\.Tests/Persistence/'
		, '^tests/CasualtiesUnknownOnline\.NormativeGates\.Tests/LocalizationCatalogueGateTests\.cs$'
		, '^tests/CasualtiesUnknownOnline\.NormativeGates\.Tests/TextEncodingGateTests\.cs$'
		, '^src/CasualtiesUnknownOnline\.Plugin/OnlineUiPreferencesDrawer\.cs$'
	)

	# A character is a violation when it is a non-ASCII LETTER: that is what every written language is made of, and it is
	# what no English prose needs. Symbols, arrows, dashes, quotes and check marks pass.
	function Test-NonEnglishLetter([char]$c) {
		return ([int]$c -gt 0x7F) -and [char]::IsLetter($c)
	}

	if (-not $WorkingTree) {
		$files = git diff --cached --name-only --diff-filter=ACM
	} else {
		$files = git status --porcelain | ForEach-Object { if ($_.Length -gt 3) { $_.Substring(3).Trim('"') } }
	}

	$bad = @()
	foreach ($file in $files) {
		if (-not $file) { continue }
		# A working-tree listing names a new DIRECTORY as one entry; only files carry text.
		if (-not (Test-Path -LiteralPath $file -PathType Leaf)) { continue }
		$skip = $false
		foreach ($pattern in $exempt) {
			if ($file -match $pattern) { $skip = $true; break }
		}

		if ($skip) { continue }
		if ($file -match '\.(png|jpg|jpeg|gif|ico|dll|pdb|zip|7z)$') { continue }

		$lines = [System.IO.File]::ReadAllLines((Resolve-Path -LiteralPath $file), [System.Text.Encoding]::UTF8)
		for ($i = 0; $i -lt $lines.Length; $i++) {
			$offending = $false
			foreach ($c in $lines[$i].ToCharArray()) {
				if (Test-NonEnglishLetter $c) { $offending = $true; break }
			}

			if ($offending) {
				$bad += "${file}:$($i + 1): $($lines[$i].Trim())"
			}
		}
	}

	if ($bad.Count -gt 0) {
		Write-Host "Committed content is English: $($bad.Count) line(s) carry non-English wording outside the exemptions."
		Write-Host 'Quote a report in translation (marked as a translation), or move the text into docs/zh/.'
		$bad | Select-Object -First 40 | ForEach-Object { Write-Host "  $_" }
		exit 1
	}

	Write-Host 'check-language: clean (English wording outside the documented exemptions).'
	exit 0
}
finally {
	Pop-Location
}
