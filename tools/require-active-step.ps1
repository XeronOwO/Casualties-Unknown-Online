# require-active-step.ps1 - the gate that makes work IMPOSSIBLE without its step rules.
#
# It reads the marker `tools/act.ps1` writes when it printed a step's rules, and refuses unless that marker names one of the accepted
# steps, is fresh, and still matches the rule file's SHA. It is called by the build (Directory.Build.targets), by deploy, and by the
# acceptance driver - so build, test, deploy and every driven row fail until the rules were printed for the step being worked.
#
# This is outcome enforcement, not a token: there is no value to grep. The only way to satisfy it is to have the printer run and
# write the marker, and the printer is the thing that prints the files.
#
# Usage: tools/require-active-step.ps1 -Steps 7,8      (build/test)
#        tools/require-active-step.ps1 -Steps 13       (deploy, acceptance driver)

param(
    [Parameter(Mandatory = $true)]
    [string]$Steps,
    [string]$MarkerPath = '.acceptance/active-step.txt',
    [int]$MaxAgeMinutes = 45
)

$ErrorActionPreference = 'Stop'
$repo = Split-Path -Parent $PSScriptRoot
$marker = Join-Path $repo $MarkerPath
$accepted = @($Steps -split ',' | ForEach-Object { $_.Trim() } | Where-Object { $_ -match '^\d+$' })

if (-not (Test-Path -LiteralPath $marker)) {
    Write-Host "require-active-step: REFUSED - no step is active."
    Write-Host "  Print the rules of the step you are working in FIRST:"
    Write-Host "    powershell -ExecutionPolicy Bypass -File tools/act.ps1 -Step <n>"
    exit 1
}

$entries = @(Get-Content -LiteralPath $marker -Encoding utf8 | Where-Object { $_ -and $_ -notmatch '^#' -and $_ -match '^\d+\|' })
if ($entries.Count -eq 0) {
    Write-Host "require-active-step: REFUSED - the active-step marker carries no step entry."
    exit 1
}

# A round walks several steps, so the marker accumulates one entry per step: accept the first entry that names an accepted step, is
# fresh, and still matches its rule file.
$matched = $false
$reason = ''
foreach ($line in $entries) {
    $parts = $line -split '\|'
    if ($parts.Count -lt 3) { $reason = "malformed entry ($line)"; continue }

    $step = $parts[0]
    $sha = $parts[1]
    $stamp = [datetime]::Parse($parts[2], [System.Globalization.CultureInfo]::InvariantCulture, [System.Globalization.DateTimeStyles]::RoundtripKind)
    $age = ([datetime]::UtcNow - $stamp.ToUniversalTime()).TotalMinutes

    if ($accepted -notcontains $step) { $reason = "step $step is armed, this action belongs to step(s) $Steps"; continue }
    if ($age -gt $MaxAgeMinutes) { $reason = ("step {0} was printed {1:N0} minutes ago (limit {2})" -f $step, $age, $MaxAgeMinutes); continue }

    $rule = Get-ChildItem -LiteralPath (Join-Path $repo 'docs/development/rules') -Filter ('{0:D2}-*.md' -f [int]$step) |
        Where-Object { $_.Name -notlike '*.local.md' } | Select-Object -First 1
    if ($null -eq $rule) { $reason = "step $step has no rule file"; continue }

    # .NET hashing, not Get-FileHash: under MSBuild's `powershell` the cmdlet is absent and the gate failed for the wrong reason
    # (measured 2026-11-03: the build reported MSB3073 with "Get-FileHash is not recognized").
    $bytes = [System.IO.File]::ReadAllBytes($rule.FullName)
    $sha256 = [System.Security.Cryptography.SHA256]::Create().ComputeHash($bytes)
    $current = ([System.BitConverter]::ToString($sha256) -replace '-', '').Substring(0, 12)
    if ($current -ne $sha) { $reason = "step $step rules changed after they were printed (printed $sha, now $current)"; continue }

    $matched = $true
    break
}

if (-not $matched) {
    Write-Host "require-active-step: REFUSED - $reason"
    Write-Host "    powershell -ExecutionPolicy Bypass -File tools/act.ps1 -Step $($accepted[0])"
    exit 1
}

exit 0