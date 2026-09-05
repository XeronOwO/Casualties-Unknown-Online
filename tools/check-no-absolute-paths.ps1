<#
.SYNOPSIS
Red-line gate: fail if any tracked file contains an absolute machine path.

.DESCRIPTION
Scans the git index through `git grep` for drive-letter absolute paths and
common Unix absolute-path roots. URLs are not treated as absolute machine
paths. The intent is to keep the repository portable: machine-specific paths are
only allowed in gitignored `AGENTS.local.md` or as placeholders such as
`<game-dir>` / `<sandbox-root>`.
#>
$ErrorActionPreference = 'Stop'

$repo = Split-Path -Parent $PSScriptRoot
Push-Location $repo
try {
    $patterns = @(
        '[A-Za-z]:\\',
        '[A-Za-z]:/[^/]',
        '(^|[^:/])/(home|Users|tmp|var|opt|mnt|etc|usr|root)/'
    )

    $hits = @()
    foreach ($pattern in $patterns) {
        $output = git grep -n -I -E $pattern -- ':!references/*' ':!*.dll' 2>$null
        if ($LASTEXITCODE -eq 0 -and $output) {
            $hits += $output
        }
    }

    if ($hits.Count -gt 0) {
        Write-Host 'Absolute machine paths found in tracked files:'
        $hits | ForEach-Object { Write-Host $_ }
        exit 1
    }

    Write-Host 'No absolute machine paths in tracked files.'
    exit 0
}
finally {
    Pop-Location
}
