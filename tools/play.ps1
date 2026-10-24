# Единая команда: пересобрать и запустить игру.
#
#   .\tools\play.ps1                 — обычный запуск
#   .\tools\play.ps1 --car=golf_2    — запуск на выбранной машине
#   .\tools\play.ps1 --smoke         — прогон-проверка (игра сама закроется)
#   .\tools\play.ps1 -NoBuild        — запустить без пересборки
#   .\tools\play.ps1 -RebuildAssets  — заодно переимпортировать ассеты
#
# Всё, что передано после имени скрипта, уходит в игру как аргументы (после `--`).

param(
    [switch]$NoBuild,
    [switch]$RebuildAssets
)

. (Join-Path $PSScriptRoot '_env.ps1')

Push-Location $KrntijaRoot
try {
    $gameArgs = @($args | Where-Object { $_ -ne $null })

    if (-not $NoBuild) {
        Write-Host 'Сборка…' -ForegroundColor DarkGray
        & $DotnetExe build game\Krntija.Game.csproj -v q --nologo
        if ($LASTEXITCODE -ne 0) {
            Write-Host 'Сборка не прошла. Если игра запущена — закройте её и повторите.' -ForegroundColor Red
            exit $LASTEXITCODE
        }
    }

    # Первый запуск на новой машине: без кэша импорта GLB не загрузятся.
    $importCache = Join-Path $KrntijaRoot 'game\.godot\imported'
    if ($RebuildAssets -or -not (Test-Path $importCache)) {
        Write-Host 'Импорт ассетов…' -ForegroundColor DarkGray
        & $GodotConsoleExe --headless --path game --import | Out-Null
    }

    if ($gameArgs.Count -gt 0) {
        & $GodotConsoleExe --path game -- @gameArgs
    }
    else {
        & $GodotConsoleExe --path game
    }
}
finally {
    Pop-Location
}
