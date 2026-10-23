# Run the game with a window.
. (Join-Path $PSScriptRoot '_env.ps1')

Push-Location $KrntijaRoot
try {
    & $DotnetExe build game\Krntija.Game.csproj -v q --nologo
    if ($LASTEXITCODE -ne 0) { exit $LASTEXITCODE }

    & $GodotExe --path game
}
finally {
    Pop-Location
}
