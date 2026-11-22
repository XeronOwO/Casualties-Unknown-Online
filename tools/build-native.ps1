# Builds the native helpers the acceptance host loads. Nothing here is shipped to players; the client-side
# `window-quiet.dll` is only copied next to the launcher (see the ticket on the game window never taking the front).
#
# The Visual Studio installation is found through vswhere first and through the two usual roots second, so the script
# works on a machine where only Build Tools are installed.

[CmdletBinding()]
param(
    [string] $Configuration = 'Release'
)

$ErrorActionPreference = 'Stop'

$root = Split-Path -Parent $PSScriptRoot
$output = Join-Path $root 'artifacts\native'
$source = Join-Path $root 'tools\native\window-quiet\window-quiet.c'
$object = Join-Path $output 'window-quiet.obj'

function Find-VcVars {
    $vswhere = Join-Path ${env:ProgramFiles(x86)} 'Microsoft Visual Studio\Installer\vswhere.exe'
    if (Test-Path $vswhere) {
        $install = & $vswhere -latest -products * -requires Microsoft.VisualStudio.Component.VC.Tools.x86.x64 -property installationPath 2>$null
        if ($install) {
            $candidate = Join-Path $install 'VC\Auxiliary\Build\vcvars64.bat'
            if (Test-Path $candidate) { return $candidate }
        }
    }

    foreach ($root in @($env:ProgramFiles, ${env:ProgramFiles(x86)})) {
        if (-not $root) { continue }
        $candidate = Get-ChildItem -Path (Join-Path $root 'Microsoft Visual Studio') -Recurse -Depth 4 -Filter 'vcvars64.bat' -ErrorAction SilentlyContinue | Select-Object -First 1
        if ($candidate) { return $candidate.FullName }
    }

    return $null
}

$vcvars = Find-VcVars
if (-not $vcvars) {
    throw 'no Visual C++ toolchain found: install the C++ workload or point the script at vcvars64.bat'
}

New-Item -ItemType Directory -Force -Path $output | Out-Null

$command = '"{0}" >nul && cl /nologo /LD /O2 /W4 /Fo:"{1}" /Fe:"{2}" "{3}" user32.lib' -f $vcvars, $object, (Join-Path $output 'window-quiet.dll'), $source
& cmd.exe /c $command
if ($LASTEXITCODE -ne 0) {
    throw "cl.exe failed with exit code $LASTEXITCODE"
}

Write-Host "built $output\window-quiet.dll"
