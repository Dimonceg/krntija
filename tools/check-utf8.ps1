# Проверка, что все исходники — валидный UTF-8.
# Ловит классическую проблему: файл перезаписали консолью в CP1251,
# а Godot потом не может его загрузить.
$ErrorActionPreference = 'Stop'

$root = Split-Path $PSScriptRoot -Parent
$strict = New-Object System.Text.UTF8Encoding($false, $true)
$broken = @()

$files = Get-ChildItem $root -Recurse -Include *.cs, *.py, *.md, *.tscn, *.godot -File |
    Where-Object {
        $relative = $_.FullName.Substring($root.Length)
        # build\ — чужие worktree и сторонние библиотеки, их не проверяем.
        $relative -notmatch '\\(obj|bin|\.godot)\\' -and $relative -notmatch '^\\build\\'
    }

foreach ($file in $files) {
    try {
        [void]$strict.GetString([IO.File]::ReadAllBytes($file.FullName))
    }
    catch {
        $broken += $file.FullName.Replace("$root\", '')
    }
}

# Windows PowerShell 5.1 читает .ps1 без BOM как ANSI: кириллица ломает разбор,
# и check.ps1 падает раньше любого шага. BOM обязателен у .ps1 с не-ASCII.
$noBom = @()
$scripts = Get-ChildItem (Join-Path $root 'tools') -Recurse -Filter *.ps1 -File
foreach ($script in $scripts) {
    $bytes = [IO.File]::ReadAllBytes($script.FullName)
    $hasBom = $bytes.Length -ge 3 -and $bytes[0] -eq 0xEF -and $bytes[1] -eq 0xBB -and $bytes[2] -eq 0xBF
    $ascii = -not ($bytes | Where-Object { $_ -gt 127 } | Select-Object -First 1)
    if (-not $hasBom -and -not $ascii) { $noBom += $script.FullName.Replace("$root\", '') }
}

if ($broken.Count -gt 0 -or $noBom.Count -gt 0) {
    if ($broken.Count -gt 0) {
        Write-Host "НЕ UTF-8:" -ForegroundColor Red
        $broken | ForEach-Object { Write-Host "  $_" }
    }
    if ($noBom.Count -gt 0) {
        Write-Host ".ps1 с кириллицей без BOM (PowerShell 5.1 их не разберёт):" -ForegroundColor Red
        $noBom | ForEach-Object { Write-Host "  $_" }
    }
    exit 1
}

Write-Host "UTF-8 в порядке: $($files.Count) файлов, BOM у $($scripts.Count) .ps1"
exit 0
