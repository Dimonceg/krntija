# Модели лута и иконки

Первый набор покрывает 15 базовых предметов: канистра, масло, вода, хлеб,
аптечка, изолента, ключ, свеча, кассета, сигареты, запаска, карта,
радиодеталь, металлолом и пробка. Автомобильные агрегаты пока используют
существующую резервную графику.

Исходники: `art/blender/loot/*.blend`. Генератор: `tools/blender_loot.py`.
Экспорт: `game/assets/models/props/loot_*.glb`; соответствующие прозрачные
иконки 256×256: `game/assets/models/props/icons/*.png`.
Каждая иконка отрендерена с той же модели, которая появляется в мире.
Модели используют один меш и материал, от 122 до 2744 треугольников.
Импорт через `WorldPropImport.gd` сохраняет палитру цветов вершин.

`LootItem` автоматически загружает модели по ID. `ItemIcon` сохраняет
пропорции картинки и сбрасывает кеш при смене ID предмета.

## Воспроизведение

Из корня репозитория (Blender, Godot .NET и dotnet должны быть доступны):

```powershell
blender --background --python tools/blender_loot.py
dotnet build game/Krntija.Game.csproj --ignore-failed-sources
godot --headless --editor --path game --import
godot --headless --fixed-fps 60 --path game --script ../tools/loot_art_check.gd -- --shot-day
godot --path game --resolution 1200x760 --script ../tools/loot_art_review.gd -- --art-view=icons --art-output=../docs/screenshots/loot-art/icons.png
godot --path game --resolution 1440x900 --script ../tools/loot_art_review.gd -- --art-view=backpack --shot-day --shot-backpack --art-output=../docs/screenshots/loot-art/backpack.png
```

Проверка проверяет 15 моделей и текстур, размеры, материалы, прозрачность
и наличие новых моделей у реального лута в Main, включая слой подбора.
При первом прогоне проверено 11 предметов в мире, ошибок нет.
Обзор и рюкзак проверены визуально; снимки и статистика геометрии находятся
в `docs/screenshots/loot-art/`.
