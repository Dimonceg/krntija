# Сделать скриншот геймплея (сохраняется в user://shot.png).
. (Join-Path $PSScriptRoot '_env.ps1')

Push-Location $KrntijaRoot
try {
    & $DotnetExe build game\Krntija.Game.csproj -v q --nologo
    if ($LASTEXITCODE -ne 0) { exit $LASTEXITCODE }

    & $GodotExe --path game -- --shot
    $shot = Join-Path $env:APPDATA 'Godot\app_userdata\Krntija\shot.png'
    if (Test-Path $shot) { "screenshot: $shot" }
}
finally {
    Pop-Location
}
