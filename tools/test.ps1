# Run the core test suite.
. (Join-Path $PSScriptRoot '_env.ps1')

Push-Location $KrntijaRoot
try {
    & $DotnetExe run --project core\Krntija.Core.Tests -c Debug
    exit $LASTEXITCODE
}
finally {
    Pop-Location
}
