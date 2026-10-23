#Requires -Version 5.1
<#
.SYNOPSIS
    Packs the CUO core into ONE managed plugin assembly for the single-file package
    (docs/backlog/done/single-file-plugin-package.md).

.DESCRIPTION
    Copies the built plugin DLL and adds every other managed dependency from the build output as an embedded
    `Packed.<name>.dll` manifest resource, which `PackedAssemblyResolver` serves at runtime. The plugin DLL keeps
    its identity (name, version, plugin guid) so BepInEx loads it exactly as the normal deployment does.

    Two files stay loose on purpose and the design records why: `steam_api64.dll` is NATIVE (it cannot live in a
    managed assembly; Steamworks.NET DllImports it from disk) and BepInEx's own assemblies belong to the install.

    This is the probe/design step's artifact, not the deploy path: `deploy.ps1` still ships the loose set. The
    packed output is for the real-client feasibility test until the design lands.

.PARAMETER GameDir
    The game directory (its BepInEx core supplies Mono.Cecil and defines the BepInEx-owned names).

.PARAMETER OutputDir
    Where the packed assembly is written. Defaults to a `packed` folder beside the build output.

.PARAMETER PluginOut
    The plugin build output directory. Defaults to the plugin project's Debug net48 output.
#>
param(
	[Parameter(Mandatory = $true)][string]$GameDir,
	[string]$OutputDir,
	[string]$PluginOut
)

$ErrorActionPreference = 'Stop'
$RepoRoot = Split-Path -Parent $PSScriptRoot
if (-not $PluginOut) {
	$PluginOut = Join-Path $RepoRoot 'src\CasualtiesUnknownOnline.Plugin\bin\Debug\net48'
}
if (-not $OutputDir) {
	$OutputDir = Join-Path $PluginOut 'packed'
}

$pluginDll = Join-Path $PluginOut 'CasualtiesUnknownOnline.dll'
if (-not (Test-Path -LiteralPath $pluginDll)) {
	Write-Error "The built plugin is missing: $pluginDll. Build first (dotnet build CasualtiesUnknownOnline.slnx)."
	exit 1
}

# Assemblies that must stay loose: BepInEx owns its own copies; the plugin itself is the carrier.
$loose = @(
	'BepInEx.dll', 'BepInEx.Harmony.dll',
	'Mono.Cecil.dll', 'Mono.Cecil.Mdb.dll', 'Mono.Cecil.Pdb.dll', 'Mono.Cecil.Rocks.dll',
	'0Harmony.dll', '0Harmony20.dll',
	'MonoMod.RuntimeDetour.dll', 'MonoMod.Utils.dll',
	'CasualtiesUnknownOnline.dll'
)

$cecil = Join-Path $GameDir 'BepInEx\core\Mono.Cecil.dll'
if (-not (Test-Path -LiteralPath $cecil)) {
	Write-Error "Mono.Cecil is missing from the game's BepInEx core: $cecil"
	exit 1
}
Add-Type -Path $cecil

New-Item -ItemType Directory -Force -Path $OutputDir | Out-Null
$target = Join-Path $OutputDir 'CasualtiesUnknownOnline.dll'

$resolver = New-Object Mono.Cecil.DefaultAssemblyResolver
$resolver.AddSearchDirectory($PluginOut)
$readerParameters = New-Object Mono.Cecil.ReaderParameters
$readerParameters.AssemblyResolver = $resolver
$assembly = [Mono.Cecil.AssemblyDefinition]::ReadAssembly($pluginDll, $readerParameters)
try {
	$module = $assembly.MainModule
	$embedded = Get-ChildItem -LiteralPath $PluginOut -Filter '*.dll' |
		Where-Object { $loose -notcontains $_.Name }
	# Steamworks.NET is a managed dependency from references/ (deploy copies it separately, so it is not in the
	# build output); its native steam_api64.dll stays loose.
	$steamworks = Join-Path $RepoRoot 'references\Steamworks.NET.dll'
	if (Test-Path -LiteralPath $steamworks) {
		$embedded += Get-Item -LiteralPath $steamworks
	}
	foreach ($dll in $embedded) {
		$resourceName = 'Packed.' + $dll.BaseName + '.dll'
		if ($module.Resources | Where-Object { $_.Name -eq $resourceName }) {
			continue
		}
		$bytes = [System.IO.File]::ReadAllBytes($dll.FullName)
		$resource = New-Object Mono.Cecil.EmbeddedResource(
			$resourceName,
			[Mono.Cecil.ManifestResourceAttributes]::Private,
			$bytes)
		$module.Resources.Add($resource)
		Write-Host "  embedded $($dll.Name) as $resourceName ($($bytes.Length) bytes)"
	}
	$assembly.Write($target)
} finally {
	$assembly.Dispose()
}

$packed = Get-Item -LiteralPath $target
Write-Host "packed artifact: $($packed.FullName) ($($packed.Length) bytes, $($embedded.Count) embedded assemblies)"
Write-Host "loose files still required by the design: steam_api64.dll (native, DllImported by Steamworks.NET)"
