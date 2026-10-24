# Проверка, что все исходники — валидный UTF-8.
# Ловит классическую проблему: файл перезаписали консолью в CP1251,
# а Godot потом не может его загрузить.
$ErrorActionPreference = 'Stop'

$root = Split-Path $PSScriptRoot -Parent
$strict = New-Object System.Text.UTF8Encoding($false, $true)
$broken = @()

$files = Get-ChildItem $root -Recurse -Include *.cs, *.py, *.md, *.tscn, *.godot -File |
    Where-Object { $_.FullName -notmatch '\\(obj|bin|\.godot)\\' }

foreach ($file in $files) {
    try {
        [void]$strict.GetString([IO.File]::ReadAllBytes($file.FullName))
    }
    catch {
        $broken += $file.FullName.Replace("$root\", '')
    }
}

if ($broken.Count -gt 0) {
    Write-Host "НЕ UTF-8:" -ForegroundColor Red
    $broken | ForEach-Object { Write-Host "  $_" }
    exit 1
}

Write-Host "UTF-8 в порядке: $($files.Count) файлов"
exit 0
