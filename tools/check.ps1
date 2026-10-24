# Проверка перед слиянием: всё, что должно быть зелёным, одной командой.
#
#   .\tools\check.ps1          — полный прогон (около 2–3 минут)
#   .\tools\check.ps1 -Quick   — только UTF-8, тесты ядра и сборка игры
#
# Код возврата 0 — можно сливать. Любой другой — нет, причина в выводе.

param(
    [switch]$Quick
)

. (Join-Path $PSScriptRoot '_env.ps1')
$ErrorActionPreference = 'Continue'

# Godot иногда падает уже при выходе, после того как проверка прошла
# (см. HANDOFF.md). Такой код — не 0 и не 1; прогон повторяем один раз.
function Invoke-GodotCheck([string]$flag) {
    for ($attempt = 1; $attempt -le 2; $attempt++) {
        $output = & $GodotConsoleExe --headless --path game -- $flag 2>&1 | Out-String
        $code = $LASTEXITCODE
        if ($code -eq 0 -or $code -eq 1) { break }
        Write-Host "    Godot упал при выходе (код $code), повтор…" -ForegroundColor DarkYellow
    }

    # -cmatch: маркеры провала пишутся заглавными, а «не провалился» — это успех.
    $failures = $output -split "`n" | Where-Object { $_ -cmatch 'FAIL|ЗАБЛОКИРОВАНО|НЕ ЗАШЁЛ|ПРОВАЛИЛСЯ|SCRIPT ERROR' }
    return @{ Code = $code; Failures = $failures }
}

$results = [ordered]@{}

function Step([string]$name, [scriptblock]$body) {
    Write-Host "• $name" -ForegroundColor Cyan
    $ok = & $body
    $results[$name] = [bool]$ok
    if ($ok) { Write-Host "  ок" -ForegroundColor Green }
    else { Write-Host "  ОШИБКА" -ForegroundColor Red }
}

Push-Location $KrntijaRoot
try {
    Step 'UTF-8 исходников' {
        & powershell -NoProfile -File (Join-Path $PSScriptRoot 'check-utf8.ps1') | Out-Host
        $LASTEXITCODE -eq 0
    }

    Step 'Тесты ядра' {
        $out = & $DotnetExe run --project core\Krntija.Core.Tests -c Debug 2>&1 | Out-String
        $code = $LASTEXITCODE
        $summary = $out -split "`n" | Where-Object { $_ -match 'Total:|\[FAIL\]|: (error|warning) ' } | Select-Object -Unique
        $summary | ForEach-Object { Write-Host "  $($_.Trim())" }
        # Предупреждения в тестах тоже не пропускаем, как и в сборке игры.
        $code -eq 0 -and -not ($summary -match ': warning ')
    }

    Step 'Сборка игры' {
        $out = & $DotnetExe build game\Krntija.Game.csproj -v q --nologo 2>&1 | Out-String
        $issues = $out -split "`n" | Where-Object { $_ -match ': (error|warning) ' } | Select-Object -Unique
        $issues | ForEach-Object { Write-Host "  $($_.Trim())" }
        # Предупреждения тоже считаем проблемой: сейчас их ноль, пусть так и остаётся.
        $LASTEXITCODE -eq 0 -and $issues.Count -eq 0
    }

    if (-not $Quick) {
        if (-not (Test-Path (Join-Path $KrntijaRoot 'game\.godot\imported'))) {
            Write-Host '• Импорт ассетов (первый запуск в этой папке)' -ForegroundColor DarkGray
            & $GodotConsoleExe --headless --path game --import 2>&1 | Out-Null
        }

        $checks = [ordered]@{
            '--smoke'         = 'Машина едет сама'
            '--selftest'      = 'Сценарии: ремонт, сейв, пешеход, ручник'
            '--selftest-cars' = 'Выбор машины и новая игра'
            '--check-house'   = 'Дом: двери, лестница, чердак'
            '--check-props'   = 'Модели мест: габариты и центровка'
        }

        foreach ($flag in $checks.Keys) {
            Step "$flag — $($checks[$flag])" {
                $r = Invoke-GodotCheck $flag
                $r.Failures | ForEach-Object { Write-Host "  $($_.Trim())" -ForegroundColor Red }
                $r.Code -eq 0 -and $r.Failures.Count -eq 0
            }
        }
    }
}
finally {
    Pop-Location
}

$failed = @($results.Keys | Where-Object { -not $results[$_] })
Write-Host ''
if ($failed.Count -eq 0) {
    Write-Host "ВСЁ ЗЕЛЁНОЕ ($($results.Count) шагов) — можно сливать." -ForegroundColor Green
    exit 0
}

Write-Host "НЕ СЛИВАТЬ: $($failed.Count) из $($results.Count) шагов красные:" -ForegroundColor Red
$failed | ForEach-Object { Write-Host "  - $_" -ForegroundColor Red }
exit 1
