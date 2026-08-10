<#
.SYNOPSIS
    Maintains docs/entity-features-matrix.csv — the world entity x feature matrix.

.DESCRIPTION
    The CSV is the single source for which world entity has which feature and
    how its sync is covered — the lookup table for "did we miss a mechanism?"
    (the entity-domain twin of tools/item-features.ps1). Every read validates
    the table first; every write validates after — a misaligned row aborts
    with exit 1, never silently. The CSV is UTF-8 without BOM (git-clean);
    cells with commas must be quoted (Import-Csv / ConvertTo-Csv round-trip
    quotes automatically); use '/' to separate values inside a cell instead.

    Commands:
      validate                        column alignment + entity uniqueness + column names
      list                            all data rows, tab-separated
      get <entity> [feature]          one entity's row / one cell
      set <entity> <feature> <value>  edit one cell (adds the row if missing)
      add-entity <entity> [feature=value...]  append a row (blank cells unless given)
      remove-entity <entity>          delete a row
      add-feature <feature>           add a column (every row gains a blank cell)

    -Doc <docs/entity-features.md>    advisory: warn (not fail) when a feature
                                     column has no matching "###" section.

    The sync column is the completeness gate: every entity row must end up
    covered (with a path), excluded (with a reason), or missing (with a
    priority) — a missing row is an open TODO.

.EXAMPLE
    tools/entity-features.ps1 validate
    tools/entity-features.ps1 list
    tools/entity-features.ps1 get MineScript replay
    tools/entity-features.ps1 set MineScript sync covered
    tools/entity-features.ps1 add-entity NewEntity type=trap sync=missing
    tools/entity-features.ps1 add-feature newfeature
#>
param(
    [Parameter(Position = 0, Mandatory = $true)]
    [ValidateSet('validate', 'list', 'get', 'set', 'add-entity', 'remove-entity', 'add-feature')]
    [string]$Command,
    [Parameter(Position = 1)]
    [string]$Arg1,
    [Parameter(Position = 2)]
    [string]$Arg2,
    [Parameter(Position = 3)]
    [string]$Arg3,
    [Parameter(ValueFromRemainingArguments = $true)]
    [string[]]$Rest,
    [string]$Doc = $null
)

Set-StrictMode -Version 2.0

$ErrorActionPreference = 'Stop'
$scriptDir = Split-Path -Parent $MyInvocation.MyCommand.Path
$matrixPath = Join-Path $scriptDir '..\docs\entity-features-matrix.csv'
$entityColumn = 'entity'

# Splits one CSV line into cells, honoring quoted cells ("a,b" stays one cell).
function Split-CsvLine {
    param([string]$Line)
    $cells = [System.Collections.Generic.List[string]]::new()
    $sb = [System.Text.StringBuilder]::new()
    $inQuotes = $false
    for ($i = 0; $i -lt $Line.Length; $i++) {
        $c = $Line[$i]
        if ($inQuotes) {
            if ($c -eq '"') {
                if ($i + 1 -lt $Line.Length -and $Line[$i + 1] -eq '"') { [void]$sb.Append('"'); $i++ }
                else { $inQuotes = $false }
            }
            else { [void]$sb.Append($c) }
        }
        else {
            if ($c -eq '"') { $inQuotes = $true }
            elseif ($c -eq ',') { $cells.Add($sb.ToString()); [void]$sb.Clear() }
            else { [void]$sb.Append($c) }
        }
    }
    $cells.Add($sb.ToString())
    return ,$cells
}

# Reads the raw lines (column alignment can only be proven on the raw text —
# Import-Csv swallows a wrong cell count per its header).
function Read-RawLines {
    Get-Content -Path $matrixPath -Encoding UTF8
}

# Validates the table. Throws with a precise message on the first defect.
function Assert-Valid {
    $lines = Read-RawLines
    if ($lines.Count -lt 2) { throw "Matrix '$matrixPath' needs a header row and at least one data row." }

    $header = Split-CsvLine $lines[0]
    $headerCount = $header.Count
    if ($headerCount -lt 2) { throw "Header row needs at least the '$entityColumn' column plus one feature column." }
    if ($header[0] -ne $entityColumn) { throw "First header cell must be '$entityColumn' (found '$($header[0])')." }

    $seen = @{}
    for ($i = 1; $i -lt $lines.Count; $i++) {
        if ([string]::IsNullOrWhiteSpace($lines[$i])) { continue }
        $cells = Split-CsvLine $lines[$i]
        if ($cells.Count -ne $headerCount) {
            throw "Row $($i + 1) has $($cells.Count) cells, header has $headerCount — misaligned. Fix with the tool, not by hand."
        }
        $entity = $cells[0]
        if ($seen.ContainsKey($entity)) { throw "Duplicate entity '$entity' on row $($i + 1)." }
        $seen[$entity] = $true
    }

    return ,$header
}

# Reads the matrix as objects (call after Assert-Valid).
function Read-Matrix {
    Import-Csv -Path $matrixPath -Encoding UTF8
}

# Writes the matrix UTF-8 without BOM (Export-Csv on PS 5.1 would add one).
function Write-Matrix {
    param([System.Collections.IEnumerable]$Rows)
    $lines = $Rows | ConvertTo-Csv -NoTypeInformation
    [System.IO.File]::WriteAllLines($matrixPath, [string[]]$lines,
        (New-Object System.Text.UTF8Encoding($false)))
}

# Returns the header cells.
function Get-Header {
    return ,(Split-CsvLine (Read-RawLines)[0])
}

# Feature columns = every column except the entity column.
function Get-Features {
    $header = Get-Header
    return ,($header | Select-Object -Skip 1)
}

# Advisory: every feature column should have a "### <feature>" section in -Doc.
function Invoke-Advisory {
    if ([string]::IsNullOrEmpty($Doc)) { return }
    if (-not (Test-Path $Doc)) { Write-Warning "Advisory: doc '$Doc' not found — skipped."; return }

    $sections = @{}
    Get-Content $Doc -Encoding UTF8 | ForEach-Object {
        # Section headers are "### <feature> — description": the first word is the feature.
        if ($_ -match '^### ([A-Za-z0-9]+)') { $sections[$matches[1]] = $true }
    }
    foreach ($feature in (Get-Features)) {
        if (-not $sections.ContainsKey($feature)) {
            Write-Warning "Feature column '$feature' has no '### $feature' section in $Doc."
        }
    }
}

# ---- command implementations ----

switch ($Command) {
    'validate' {
        try { $header = Assert-Valid }
        catch { Write-Error $_.Exception.Message; exit 1 }
        Write-Host "OK: $($header.Count) columns, matrix aligned."
        Invoke-Advisory
        exit 0
    }

    'list' {
        try { Assert-Valid | Out-Null }
        catch { Write-Error $_.Exception.Message; exit 1 }
        Read-Matrix | ForEach-Object {
            $_.PSObject.Properties.Value -join "`t"
        }
        exit 0
    }

    'get' {
        if ([string]::IsNullOrEmpty($Arg1)) { Write-Error "get needs <entity> [feature]."; exit 1 }
        try { Assert-Valid | Out-Null }
        catch { Write-Error $_.Exception.Message; exit 1 }
        $row = Read-Matrix | Where-Object { $_.$entityColumn -eq $Arg1 }
        if (-not $row) { Write-Error "Entity '$Arg1' not in matrix."; exit 1 }
        if (-not [string]::IsNullOrEmpty($Arg2)) {
            $value = $row.PSObject.Properties[$Arg2]
            if (-not $value) { Write-Error "Feature '$Arg2' not a matrix column."; exit 1 }
            Write-Output $value.Value
        }
        else {
            $row.PSObject.Properties.Value -join "`t"
        }
        exit 0
    }

    'set' {
        if ([string]::IsNullOrEmpty($Arg1) -or [string]::IsNullOrEmpty($Arg2)) { Write-Error "set needs <entity> <feature> <value>."; exit 1 }
        try { $header = Assert-Valid }
        catch { Write-Error $_.Exception.Message; exit 1 }
        if ($header -notcontains $Arg2) { Write-Error "Feature '$Arg2' not a matrix column."; exit 1 }
        $rows = Read-Matrix
        $row = $rows | Where-Object { $_.$entityColumn -eq $Arg1 }
        if (-not $row) {
            $new = [PSCustomObject]@{}
            foreach ($h in $header) { $new | Add-Member -NotePropertyName $h -NotePropertyValue '' }
            $new.$entityColumn = $Arg1
            $rows += $new
            $row = $new
        }
        $row.$Arg2 = $Arg3
        Write-Matrix $rows
        try { Assert-Valid | Out-Null }
        catch { Write-Error "Write left the matrix invalid: $($_.Exception.Message)"; exit 1 }
        Write-Host "set: $Arg1/$Arg2 = $Arg3"
        exit 0
    }

    'add-entity' {
        if ([string]::IsNullOrEmpty($Arg1)) { Write-Error "add-entity needs <entity> [feature=value ...]."; exit 1 }
        try { $header = Assert-Valid }
        catch { Write-Error $_.Exception.Message; exit 1 }
        $rows = Read-Matrix
        if ($rows | Where-Object { $_.$entityColumn -eq $Arg1 }) { Write-Error "Entity '$Arg1' already in matrix."; exit 1 }

        $new = [PSCustomObject]@{}
        foreach ($h in $header) { $new | Add-Member -NotePropertyName $h -NotePropertyValue '' }
        $new.$entityColumn = $Arg1
        # feature=value pairs arrive as the positional args after the entity
        # name (Arg2, Arg3, then whatever remains) — none is optional.
        foreach ($pair in @($Arg2, $Arg3) + @($Rest)) {
            if ([string]::IsNullOrEmpty($pair)) { continue }
            if ($pair -notmatch '^([^=]+)=(.+)$') { Write-Error "Feature value '$pair' must look like 'feature=value'."; exit 1 }
            $feature = $matches[1]
            if ($header -notcontains $feature) { Write-Error "Feature '$feature' not a matrix column."; exit 1 }
            $new.$feature = $matches[2]
        }
        $rows += $new
        Write-Matrix $rows
        try { Assert-Valid | Out-Null }
        catch { Write-Error "Write left the matrix invalid: $($_.Exception.Message)"; exit 1 }
        Write-Host "add-entity: $Arg1"
        exit 0
    }

    'remove-entity' {
        if ([string]::IsNullOrEmpty($Arg1)) { Write-Error "remove-entity needs <entity>."; exit 1 }
        try { Assert-Valid | Out-Null }
        catch { Write-Error $_.Exception.Message; exit 1 }
        $rows = Read-Matrix
        $kept = @($rows | Where-Object { $_.$entityColumn -ne $Arg1 })
        if ($kept.Count -eq $rows.Count) { Write-Error "Entity '$Arg1' not in matrix."; exit 1 }
        Write-Matrix $kept
        try { Assert-Valid | Out-Null }
        catch { Write-Error "Write left the matrix invalid: $($_.Exception.Message)"; exit 1 }
        Write-Host "remove-entity: $Arg1"
        exit 0
    }

    'add-feature' {
        if ([string]::IsNullOrEmpty($Arg1)) { Write-Error "add-feature needs <feature>."; exit 1 }
        try { $header = Assert-Valid }
        catch { Write-Error $_.Exception.Message; exit 1 }
        if ($header -contains $Arg1) { Write-Error "Feature '$Arg1' already a matrix column."; exit 1 }
        $rows = Read-Matrix
        foreach ($row in $rows) {
            $row | Add-Member -NotePropertyName $Arg1 -NotePropertyValue '' -Force
        }
        Write-Matrix $rows
        try { Assert-Valid | Out-Null }
        catch { Write-Error "Write left the matrix invalid: $($_.Exception.Message)"; exit 1 }
        Write-Host "add-feature: $Arg1 (every row gained a blank cell)"
        Invoke-Advisory
        exit 0
    }
}
