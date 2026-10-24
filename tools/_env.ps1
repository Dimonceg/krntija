# Shared paths for all scripts.
# Godot finds .NET through DOTNET_ROOT. The machine-wide .NET 6 is too old,
# so we point at the per-user install. Nothing here changes system state.

$ErrorActionPreference = 'Stop'

$KrntijaRoot = Split-Path -Parent $PSScriptRoot
$DotnetRoot = 'C:\Tools\dotnet'
$GodotDir = 'C:\Tools\Godot4\Godot_v4.7.2-stable_mono_win64'
$GodotExe = Join-Path $GodotDir 'Godot_v4.7.2-stable_mono_win64.exe'
# Консольная сборка: печатает логи в терминал, в отличие от оконной.
$GodotConsoleExe = Join-Path $GodotDir 'Godot_v4.7.2-stable_mono_win64_console.exe'

if (-not (Test-Path (Join-Path $DotnetRoot 'dotnet.exe'))) {
    throw "dotnet not found in $DotnetRoot (see README)"
}
if (-not (Test-Path $GodotExe)) {
    throw "Godot not found in $GodotDir (see README)"
}

$env:DOTNET_ROOT = $DotnetRoot
$env:PATH = "$DotnetRoot;$GodotDir;$env:PATH"
$env:DOTNET_CLI_TELEMETRY_OPTOUT = '1'
$env:DOTNET_NOLOGO = '1'

$DotnetExe = Join-Path $DotnetRoot 'dotnet.exe'
