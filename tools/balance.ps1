# Прогон баланса: 30 минут смешанной езды без рендера.
. (Join-Path $PSScriptRoot '_env.ps1')

Push-Location $KrntijaRoot
try {
    & $DotnetExe build game\Krntija.Game.csproj -v q --nologo
    if ($LASTEXITCODE -ne 0) { exit $LASTEXITCODE }

    $out = Join-Path $KrntijaRoot 'balance-out.txt'
    Remove-Item $out -ErrorAction SilentlyContinue

    $process = Start-Process -FilePath $GodotExe -ArgumentList '--headless','--path','game','--','--balance' `
        -RedirectStandardOutput $out -RedirectStandardError (Join-Path $KrntijaRoot 'balance-err.txt') -PassThru -NoNewWindow
    $process.WaitForExit(60000) | Out-Null

    Get-Content $out -Encoding UTF8 | Where-Object { $_ -match '\S' }
    Remove-Item $out, (Join-Path $KrntijaRoot 'balance-err.txt') -ErrorAction SilentlyContinue
}
finally {
    Pop-Location
}
