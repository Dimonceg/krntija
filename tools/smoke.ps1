# Headless smoke test: the car must accelerate on its own and exit with code 0.
. (Join-Path $PSScriptRoot '_env.ps1')

Push-Location $KrntijaRoot
try {
    & $DotnetExe build game\Krntija.Game.csproj -v q --nologo
    if ($LASTEXITCODE -ne 0) { exit $LASTEXITCODE }

    & $GodotExe --headless --path game -- --smoke
    exit $LASTEXITCODE
}
finally {
    Pop-Location
}
