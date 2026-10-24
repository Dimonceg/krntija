using System;
using System.Collections.Generic;
using System.Linq;
using Godot;
using Krntija.Core;
using Krntija.Core.Economy;
using Krntija.Core.Items;
using Krntija.Core.Parts;
using Krntija.Core.Vehicle;
using Krntija.Core.Workshop;
using Krntija.Core.World;

namespace Krntija.Game;

/// <summary>Точки взаимодействия: подсказки, клавиша E, лавка, журнал, скупщик, дом.</summary>
public partial class Main
{
    private enum InteractionKind
    {
        Pump,
        Shop,
        Gate,
        Sleep,
        Door,
        Loot,
        SellAll,
        Drink,
        Pet,
        Talk,
        RepairExit,
        ClearCollapse,
        Chest,
        Workbench,
        SleepHome,
    }

    /// <param name="StateId">Id в <see cref="WorldState"/>, если действие меняет мир и сохраняется.</param>
    private sealed record InteractionPoint(Vector3 Position, InteractionKind Kind, float Radius,
        CheckpointGate? Gate = null, Door? Door = null, string? LootId = null, Node3D? Node = null,
        string? StateId = null);

    private readonly List<InteractionPoint> _interactions = new();

    private string BuildPrompt()
    {
        if (_hud.Screen != HudScreen.None) return string.Empty;

        var point = NearestInteraction();

        if (_onFoot && _player is not null)
        {
            if (point is not null && point.Position.DistanceTo(_player.GlobalPosition) < DistanceToCar() + 0.5f)
                return PromptFor(point);
            if (DistanceToCar() < 3.5f) return "[E] сесть в машину";
            return "мышь — обзор, W/S — идти, A/D — шаг вбок, Shift — бежать";
        }

        if (point is not null && _car.SpeedMps < 2f) return PromptFor(point);

        return _car.SpeedMps < 2f ? "[E] выйти из машины" : string.Empty;
    }

    private static string PromptFor(InteractionPoint point) => point.Kind switch
    {
        InteractionKind.Shop => "[E] зайти в лавку",
        InteractionKind.SellAll => "[E] сдать всё скупщику",
        InteractionKind.Drink => "[E] попить из колодца",
        InteractionKind.Pet => "[E] погладить собаку",
        InteractionKind.Talk => "[E] поговорить",
        InteractionKind.RepairExit => $"[E] починить выезд ({Krntija.Core.Economy.Money.Format(WorldActions.ExitRepairPrice)} + изолента)",
        InteractionKind.ClearCollapse => "[E] разобрать завал",
        InteractionKind.Chest => "[E] открыть сундук",
        InteractionKind.Workbench => $"[E] верстак: починить деталь ({Krntija.Core.Economy.Money.Format(WorldActions.WorkbenchPrice)} + изолента)",
        InteractionKind.SleepHome => "[E] поспать дома",
        InteractionKind.Gate => point.Gate is { IsOpen: true } ? "[E] шлагбаум открыт" : $"[E] заплатить {Krntija.Core.Economy.Money.Format(WorldActions.GateToll)} за проезд",
        InteractionKind.Sleep => $"[E] переночевать ({Krntija.Core.Economy.Money.Format(WorldActions.MotelPrice)})",
        InteractionKind.Door => point.Door is { IsOpen: true } ? "[E] закрыть дверь" : "[E] открыть дверь",
        InteractionKind.Loot => point.LootId is not null
            ? $"[E] поднять {Core.Items.ItemCatalog.Get(point.LootId).Name}"
            : "[E] поднять",
        _ => "[E] заправить",
    };

    private void Interact()
    {
        if (_hud.Screen != HudScreen.None)
        {
            _hud.Screen = HudScreen.None;
            return;
        }

        if (_onFoot)
        {
            // Сначала ближайшее действие: у дома E не должно сажать обратно за руль.
            var point = NearestInteraction();
            if (point is not null && point.Position.DistanceTo(_player!.GlobalPosition) < DistanceToCar() + 0.5f)
            {
                Use(point);
                return;
            }

            if (DistanceToCar() < 3.5f)
            {
                EnterCar();
                return;
            }

            if (point is not null) Use(point);

            return;
        }

        var inCar = NearestInteraction();
        if (inCar is not null && _car.SpeedMps < 2f && inCar.Kind != InteractionKind.Shop)
        {
            Use(inCar);
            return;
        }

        if (_car.SpeedMps >= 2f)
        {
            _hud.Toast("Сначала остановись");
            return;
        }

        ExitCar();
    }

    private void Use(InteractionPoint point)
    {
        switch (point.Kind)
        {
            case InteractionKind.Shop:
                _hud.Screen = HudScreen.Shop;
                break;

            case InteractionKind.Gate:
                PayForGate(point);
                break;

            case InteractionKind.SellAll:
                SellEverythingToDealer();
                break;

            case InteractionKind.Sleep:
                TrySleep();
                break;

            case InteractionKind.Loot:
                PickUpLoot(point);
                break;

            case InteractionKind.Door:
                point.Door?.Toggle();
                Sfx.PlayUi();
                break;

            case InteractionKind.Drink:
                DrinkFromWell();
                break;

            case InteractionKind.Pet:
                _hud.Toast("Собака виляет хвостом");
                Sfx.PlayUi();
                break;

            case InteractionKind.Talk:
                TalkToResident(point);
                break;

            case InteractionKind.RepairExit:
                TryRepairExit(point);
                break;

            case InteractionKind.ClearCollapse:
                ClearCollapse();
                break;

            case InteractionKind.Chest:
                OpenHomeChest();
                break;

            case InteractionKind.Workbench:
                RepairAtWorkbench();
                break;

            case InteractionKind.SleepHome:
                SleepAtHome();
                break;

            default:
                RefuelAtPump();
                break;
        }
    }

    private void PayForGate(InteractionPoint point)
    {
        if (point.Gate is not { } gate) return;

        if (gate.IsOpen)
        {
            _hud.Toast("Проезд уже оплачен");
            return;
        }

        if (WorldActions.TryPayToll(_car.Session.Wallet))
        {
            gate.Open();
            MarkWorld(point.StateId);
            _hud.Toast($"Проезд оплачен: {Krntija.Core.Economy.Money.Format(WorldActions.GateToll)}");
        }
        else
        {
            _hud.Toast($"Нет {Krntija.Core.Economy.Money.Format(WorldActions.GateToll)} на проезд");
        }
    }

    /// <summary>За первое посещение каждого места — деньги: есть смысл ехать дальше.</summary>
    private void RewardNewPlaces()
    {
        if (_road is null) return;

        for (int i = 0; i < _road.Points.Count; i++)
        {
            var point = _road.Points[i];
            if (_car.GlobalPosition.DistanceTo(point.Position) > 25f) continue;

            if (!WorldActions.TryRewardPlace(_car.Session, i)) continue;
            _hud.Toast($"{point.Segment.Title}: +{Krntija.Core.Economy.Money.Format(WorldActions.PlaceReward)}");
        }
    }

    private void TrySleep()
    {
        _hud.Toast(WorldActions.TrySleepAtMotel(_car.Session)
            ? "Выспались. Утро, силы восстановлены"
            : $"Ночлег стоит {Krntija.Core.Economy.Money.Format(WorldActions.MotelPrice)}");
    }

    private float DistanceToCar() =>
        _player is null ? float.MaxValue : _player.GlobalPosition.DistanceTo(_car.GlobalPosition);

    private InteractionPoint? NearestInteraction()
    {
        Vector3 from = _onFoot && _player is not null ? _player.GlobalPosition : _car.GlobalPosition;

        InteractionPoint? best = null;
        float bestDistance = float.MaxValue;

        foreach (var point in _interactions)
        {
            float distance = from.DistanceTo(point.Position);
            if (distance > point.Radius || distance >= bestDistance) continue;

            best = point;
            bestDistance = distance;
        }

        return best;
    }

    private void RefuelAtPump()
    {
        var session = _car.Session;
        double need = session.Vehicle.Fuel.CapacityLiters - session.Vehicle.Fuel.Liters;
        var (liters, cost) = TradeService.RefuelAtPump(session.Vehicle, session.Wallet, need);

        if (liters > 0.01)
            Sfx.PlayPour(_car.GlobalPosition);

        _hud.Toast(liters > 0.01
            ? $"Залито {liters:F1} л за {Krntija.Core.Economy.Money.Format(cost)}"
            : session.Wallet.Money < TradeService.FuelPricePerLiter * need
                ? "Не хватает денег"
                : "Бак полный");
    }

    private void BuyShopItem(int index)
    {
        if (index < 0 || index >= TradeService.Shop.Count) return;

        var entry = TradeService.Shop[index];
        bool ok = TradeService.TryBuy(_car.Session.Inventory, _car.Session.Wallet, entry.ItemId);

        _hud.Toast(ok
            ? $"Куплено: {ItemCatalog.Get(entry.ItemId).Name}"
            : "Не хватает денег или места");
    }

    /// <summary>Использовать предмет из сумки по номеру в журнале.</summary>
    private void UseInventoryItem(int index)
    {
        var session = _car.Session;
        var item = session.Inventory.Items.ElementAtOrDefault(index);

        if (item is null)
        {
            _hud.Toast("В сумке нет такого предмета");
            return;
        }

        string name = item.Definition.Name;
        ItemUseService.TryUse(session, item.DefId, out string message);
        _hud.Toast($"{name}: {message}");
    }

    /// <summary>Сдать первую попавшуюся деталь из сумки за половину цены.</summary>
    private void SellCarriedPart()
    {
        var session = _car.Session;
        var item = session.Inventory.Items.FirstOrDefault(i => PartCatalog.All.ContainsKey(i.DefId));

        if (item is null)
        {
            _hud.Toast("Нечего сдавать");
            return;
        }

        string name = item.Definition.Name;
        double price = PartCatalog.All[item.DefId].Price * 0.5;

        _hud.Toast(TradeService.TrySellPart(session.Inventory, session.Wallet, item.DefId)
            ? $"Сдано: {name} за {Krntija.Core.Economy.Money.Format(price)}"
            : "Не получилось сдать");
    }

    private void RepairSelectedPart()
    {
        var session = _car.Session;
        var slot = _hud.SelectedSlot;
        var part = session.Vehicle.Get(slot);
        if (part is null) return;

        if (session.Inventory.Count(ItemIds.DuctTape) == 0)
        {
            _hud.Toast("Нет изоленты");
            return;
        }

        _hud.Toast(VehicleService.TryRepair(session.Vehicle, session.Inventory, slot)
            ? $"{part.Name}: подмотано до {part.Condition * 100:F0}%"
            : $"{part.Name} в порядке");
    }

    private void InstallSelectedPart()
    {
        var session = _car.Session;
        var slot = _hud.SelectedSlot;

        string? defId = VehicleService.FindPartInBag(session.Inventory, slot);
        if (defId is null)
        {
            _hud.Toast("Нет подходящей детали в сумке");
            return;
        }

        if (!VehicleService.TryReplacePart(session.Vehicle, session.Inventory, defId, slot, out _))
        {
            _hud.Toast("Не удалось поставить деталь");
            return;
        }

        _hud.Toast($"{session.Vehicle.Get(slot)!.Name}: установлена");
    }

    private void RefuelFromCan()
    {
        double poured = VehicleService.RefuelFromCan(_car.Session.Vehicle, _car.Session.Inventory);
        _hud.Toast(poured > 0.01 ? $"Залито {poured:F1} л" : "Канистра пуста");
    }

    /// <summary>Вода из колодца: немного восстанавливает жажду.</summary>
    private void DrinkFromWell()
    {
        WorldActions.DrinkFromWell(_car.Session);
        _hud.Toast("Вода холодная, жажда утолена");
        Sfx.PlayUi();
    }

    /// <summary>Житель говорит одну из своих реплик.</summary>
    private void TalkToResident(InteractionPoint point)
    {
        int index = int.TryParse(point.LootId, out int parsed) ? parsed : 0;
        string line = MapLife.ResidentLines[Math.Abs(index) % MapLife.ResidentLines.Length];
        _hud.Toast(line);
        Sfx.PlayUi();
    }

    /// <summary>Починка выезда: плата и изолента. Открывает шлагбаум и убирает стену.</summary>
    private void TryRepairExit(InteractionPoint point)
    {
        if (int.TryParse(point.LootId, out int index) && index >= 0 && index < _exitGates.Count)
        {
            var gate = _exitGates[index];
            if (gate.Wall is null || !IsInstanceValid(gate.Wall))
            {
                _hud.Toast("Выезд уже открыт");
                return;
            }

            var session = _car.Session;
            switch (WorldActions.TryRepairExit(session.Wallet, session.Inventory))
            {
                case ActionResult.NotEnoughMoney:
                    _hud.Toast($"Нужно {Krntija.Core.Economy.Money.Format(WorldActions.ExitRepairPrice)}");
                    return;
                case ActionResult.NoDuctTape:
                    _hud.Toast("Нужна изолента");
                    return;
            }

            if (!gate.Barrier.IsOpen) gate.Barrier.Toggle();
            gate.Wall.QueueFree();
            MarkWorld(point.StateId);
            _hud.Toast("Выезд открыт: дальше новый край");
            Sfx.PlayUi();
        }
    }

    /// <summary>Разобрать завал на ветке: убирает камни и освобождает дорогу.</summary>
    private void ClearCollapse()
    {
        if (_collapseNode is null || !IsInstanceValid(_collapseNode)) return;

        _collapseNode.QueueFree();
        _collapseNode = null;
        MarkWorld(WorldState.Collapse(0));
        _collapseBody = null;
        _hud.Toast("Завал разобран, дорога свободна");
        Sfx.PlayUi();
    }

    /// <summary>Сундук дома: рюкзак с третьей колонкой — складом.</summary>
    private void OpenHomeChest()
    {
        if (_backpack.IsOpen) return;

        _backpack.Toggle(_car.Session.Inventory, _car.Session.Trunk, _car.Session.Home);
        SetCursorFree(true);
        _hud.Toast("Дом: склад открыт");
    }

    /// <summary>Верстак: починить самую изношенную деталь за плату и изоленту.</summary>
    private void RepairAtWorkbench()
    {
        switch (WorldActions.TryRepairAtWorkbench(_car.Session, out var part))
        {
            case ActionResult.NothingToDo:
                InstallUpgradeFromBag();
                break;
            case ActionResult.NotEnoughMoney:
                _hud.Toast($"Нужно {Krntija.Core.Economy.Money.Format(WorldActions.WorkbenchPrice)}");
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

    /// <summary>Чинить нечего — ставим улучшение, если оно есть в кармане.</summary>
    private void InstallUpgradeFromBag()
    {
        var session = _car.Session;

        foreach (string id in Core.Items.UpgradeCatalog.All.Keys)
        {
            if (session.Upgrades.Contains(id) || session.Inventory.Count(id) < 1) continue;

            session.Inventory.Remove(id, 1);
            _hud.Toast(session.ApplyUpgrade(id) ?? "Установлено");
            Sfx.PlayDoor(_car.GlobalPosition);
            return;
        }

        switch (Core.WorldActions.TryCraft(session, out string crafted))
        {
            case Core.ActionResult.Done:
                _hud.Toast($"Собрано: {crafted}");
                Sfx.PlayDoor(_car.GlobalPosition);
                return;
            case Core.ActionResult.NoRoom:
                _hud.Toast("Собрать можно, но некуда положить: освободи карман");
                return;
        }

        _hud.Toast("Чинить нечего, улучшений нет");
    }

    /// <summary>Сон дома: бесплатно, но время идёт.</summary>
    private void SleepAtHome()
    {
        WorldActions.SleepAtHome(_car.Session);
        SaveGame();
        _hud.Toast("Выспался дома: 8 часов, игра сохранена");
    }

    /// <summary>Скупщик берёт всё, кроме инструментов, по 70 % цены.</summary>
    private void SellEverythingToDealer()
    {
        var (pocketCount, pocketMoney) =
            Core.Economy.TradeService.SellEverything(_car.Session.Inventory, _car.Session.Wallet);
        var (trunkCount, trunkMoney) =
            Core.Economy.TradeService.SellEverything(_car.Session.Trunk, _car.Session.Wallet);

        int total = pocketCount + trunkCount;
        _hud.Toast(total > 0
            ? $"Скупщик забрал {total} шт. за {Krntija.Core.Economy.Money.Format(pocketMoney + trunkMoney)}"
            : "Скупщику нечего предложить");

        _backpack.Refresh(_car.Session.Inventory, _car.Session.Trunk);
    }

    /// <summary>Скупка всего хлама — и из кармана, и из багажника.</summary>
    private void SellJunk()
    {
        var (pocketCount, pocketMoney) =
            Core.Economy.TradeService.SellJunk(_car.Session.Inventory, _car.Session.Wallet);
        var (trunkCount, trunkMoney) =
            Core.Economy.TradeService.SellJunk(_car.Session.Trunk, _car.Session.Wallet);

        int total = pocketCount + trunkCount;
        _hud.Toast(total > 0
            ? $"Продано {total} шт. за {Krntija.Core.Economy.Money.Format(pocketMoney + trunkMoney)}"
            : "Нечего продавать");

        _backpack.Refresh(_car.Session.Inventory, _car.Session.Trunk);
    }
}
