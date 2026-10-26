using System;
using Godot;
using Krntija.Core;
using Krntija.Core.Economy;
using Krntija.Core.Items;
using Krntija.Core.World;

namespace Krntija.Game;

/// <summary>
/// Дом и места: сундук, сон, колодец, ящик, разговор, верстак, починка выезда и
/// разбор завала. Решения — в ядре (<see cref="WorldActions"/>), здесь показ и ввод.
/// </summary>
public partial class Main
{
    /// <summary>Сундук дома: рюкзак с третьей колонкой — складом.</summary>
    private void OpenHomeChest()
    {
        if (_backpack.IsOpen) return;

        _backpack.Toggle(_car.Session.Inventory, _car.Session.Trunk, _car.Session.Home);
        SetCursorFree(true);
        _hud.Toast("Дом: склад открыт");
    }

    /// <summary>Сон дома: бесплатно, но время идёт.</summary>
    private void SleepAtHome()
    {
        WorldActions.SleepAtHome(_car.Session);
        SaveGame();
        _hud.Toast("Выспался дома: 8 часов, игра сохранена");
    }

    /// <summary>Вода из колодца: немного восстанавливает жажду.</summary>
    private void DrinkFromWell()
    {
        WorldActions.DrinkFromWell(_car.Session);
        _hud.Toast("Вода холодная, жажда утолена");
        Sfx.PlayUi();
    }

    /// <summary>Ящик: детерминированный лут по id, открывается один раз.</summary>
    private void OpenCrate(InteractionPoint point)
    {
        if (point.StateId is null) return;

        if (_car.Session.WorldState.IsDone(point.StateId))
        {
            _hud.Toast("Ящик уже пуст");
            return;
        }

        var (itemId, count) = WorldActions.CrateLoot(point.StateId);
        if (!_car.Session.Inventory.TryAdd(itemId, count))
        {
            _hud.Toast("В кармане нет места");
            return;
        }

        MarkWorld(point.StateId);
        _hud.Toast($"В ящике: {ItemCatalog.Get(itemId).Name} ×{count}");
        Sfx.PlayUi();
    }

    /// <summary>Житель говорит одну из своих реплик.</summary>
    private void TalkToResident(InteractionPoint point)
    {
        int index = int.TryParse(point.LootId, out int parsed) ? parsed : 0;
        string line = ResidentLines.All[Math.Abs(index) % ResidentLines.All.Length];
        _hud.Toast(line);
        Sfx.PlayUi();
    }

    /// <summary>
    /// Верстак: сначала поставить улучшение (если есть и машина рядом), потом
    /// починить самую изношенную деталь. A9: установка не ждёт починки.
    /// </summary>
    private void RepairAtWorkbench()
    {
        var session = _car.Session;

        if (UpgradeInstall.Pending(session) is { } pending
            && UpgradeInstall.CanInstall(session, pending, PlayerToCar()) == UpgradeInstallResult.Ok)
        {
            UpgradeInstall.Install(session, pending, PlayerToCar());
            InstalledUpgrade(pending);
            return;
        }

        switch (WorldActions.TryRepairAtWorkbench(session, out var part))
        {
            case ActionResult.NothingToDo:
                InstallUpgradeFromBag();
                break;
            case ActionResult.NotEnoughMoney:
                _hud.Toast($"Нужно {Money.Format(WorldActions.WorkbenchPrice)}");
                break;
            case ActionResult.NoDuctTape:
                _hud.Toast("Нужна изолента");
                break;
            default:
                _hud.Toast($"Починено: {part!.Name}");
                Sfx.PlayDoor(_car.GlobalPosition);
                break;
        }
    }

    /// <summary>Чинить нечего — ставим улучшение, если есть и машина рядом (A9).</summary>
    private void InstallUpgradeFromBag()
    {
        var session = _car.Session;

        if (UpgradeInstall.Pending(session) is { } id)
        {
            if (UpgradeInstall.CanInstall(session, id, PlayerToCar()) != UpgradeInstallResult.Ok)
            {
                _hud.Toast("Улучшение ставится у машины: подгони её к дому");
                return;
            }

            UpgradeInstall.Install(session, id, PlayerToCar());
            InstalledUpgrade(id);
            return;
        }

        switch (WorldActions.TryCraft(session, out string crafted))
        {
            case ActionResult.Done:
                _hud.Toast($"Собрано: {crafted}");
                Sfx.PlayDoor(_car.GlobalPosition);
                return;
            case ActionResult.NoRoom:
                _hud.Toast("Собрать можно, но некуда положить: освободи карман");
                return;
        }

        _hud.Toast("Чинить нечего, улучшений нет");
    }

    /// <summary>Починка выезда: плата и изолента. Открывает шлагбаум и убирает стену.</summary>
    private void TryRepairExit(InteractionPoint point)
    {
        // Мир кусками: заграждение выезда — узел точки, id — из плана.
        if (point.Chunk >= 0)
        {
            if (!TryPayForExit()) return;

            RemoveChunkProp(point);
            _hud.Toast("Выезд открыт");
            Sfx.PlayUi();
            return;
        }

        if (int.TryParse(point.LootId, out int index) && index >= 0 && index < _exitGates.Count)
        {
            var gate = _exitGates[index];
            if (gate.Wall is null || !IsInstanceValid(gate.Wall))
            {
                _hud.Toast("Выезд уже открыт");
                return;
            }

            if (!TryPayForExit()) return;

            if (!gate.Barrier.IsOpen) gate.Barrier.Toggle();
            gate.Wall.QueueFree();
            MarkWorld(point.StateId);
            _hud.Toast("Выезд открыт: дальше новый край");
            Sfx.PlayUi();
        }
    }

    /// <summary>Плата за выезд ядру; false — не хватило денег или изоленты (уже сказано).</summary>
    private bool TryPayForExit()
    {
        var session = _car.Session;
        switch (WorldActions.TryRepairExit(session.Wallet, session.Inventory))
        {
            case ActionResult.NotEnoughMoney:
                _hud.Toast($"Нужно {Money.Format(WorldActions.ExitRepairPrice)}");
                return false;
            case ActionResult.NoDuctTape:
                _hud.Toast("Нужна изолента");
                return false;
            default:
                return true;
        }
    }

    /// <summary>Разобрать завал на ветке: убирает камни и освобождает дорогу.</summary>
    private void ClearCollapse(InteractionPoint point)
    {
        if (point.Chunk >= 0)
        {
            RemoveChunkProp(point);
            _hud.Toast("Завал разобран, дорога свободна");
            Sfx.PlayUi();
            return;
        }

        if (_collapseNode is null || !IsInstanceValid(_collapseNode)) return;

        _collapseNode.QueueFree();
        _collapseNode = null;
        MarkWorld(WorldState.Collapse(0));
        _collapseBody = null;
        _hud.Toast("Завал разобран, дорога свободна");
        Sfx.PlayUi();
    }

    /// <summary>Действие в куске сделано: убрать предмет, записать в мир, снять точку.</summary>
    private void RemoveChunkProp(InteractionPoint point)
    {
        point.Node?.QueueFree();
        MarkWorld(point.StateId);
        _interactions.Remove(point);
    }
}
