using System.Linq;
using Godot;
using Krntija.Core.Economy;
using Krntija.Core.Items;
using Krntija.Core.Parts;
using Krntija.Core.Workshop;

namespace Krntija.Game;

/// <summary>
/// Лавка, скупщик, заправка и журнал: деньги, покупки, канистра, предметы из сумки.
/// Правила и цены — в ядре (<see cref="TradeService"/>), здесь только показ и ввод.
/// </summary>
public partial class Main
{
    private void RefuelAtPump()
    {
        var session = _car.Session;
        double need = session.Vehicle.Fuel.CapacityLiters - session.Vehicle.Fuel.Liters;
        var (liters, cost) = TradeService.RefuelAtPump(session.Vehicle, session.Wallet, need);

        if (liters > 0.01)
            Sfx.PlayPour(_car.GlobalPosition);

        _hud.Toast(liters > 0.01
            ? $"Залито {liters:F1} л за {Money.Format(cost)}"
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
            ? $"Сдано: {name} за {Money.Format(price)}"
            : "Не получилось сдать");
    }

    /// <summary>Скупщик берёт всё, кроме инструментов, по 70 % цены.</summary>
    private void SellEverythingToDealer()
    {
        var (pocketCount, pocketMoney) =
            TradeService.SellEverything(_car.Session.Inventory, _car.Session.Wallet);
        var (trunkCount, trunkMoney) =
            TradeService.SellEverything(_car.Session.Trunk, _car.Session.Wallet);

        int total = pocketCount + trunkCount;
        _hud.Toast(total > 0
            ? $"Скупщик забрал {total} шт. за {Money.Format(pocketMoney + trunkMoney)}"
            : "Скупщику нечего предложить");

        _backpack.Refresh(_car.Session.Inventory, _car.Session.Trunk);
    }

    /// <summary>Скупка всего хлама — и из кармана, и из багажника.</summary>
    private void SellJunk()
    {
        var (pocketCount, pocketMoney) =
            TradeService.SellJunk(_car.Session.Inventory, _car.Session.Wallet);
        var (trunkCount, trunkMoney) =
            TradeService.SellJunk(_car.Session.Trunk, _car.Session.Wallet);

        int total = pocketCount + trunkCount;
        _hud.Toast(total > 0
            ? $"Продано {total} шт. за {Money.Format(pocketMoney + trunkMoney)}"
            : "Нечего продавать");

        _backpack.Refresh(_car.Session.Inventory, _car.Session.Trunk);
    }

    /// <summary>Залить из канистры, которая лежит в сумке.</summary>
    private void RefuelFromCan()
    {
        double poured = VehicleService.RefuelFromCan(_car.Session.Vehicle, _car.Session.Inventory);
        _hud.Toast(poured > 0.01 ? $"Залито {poured:F1} л" : "Канистра пуста");
    }

    /// <summary>Журнал: подмотать изолентой выбранную деталь.</summary>
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

    /// <summary>Журнал: поставить выбранную деталь из сумки (старый путь, кроме рук).</summary>
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
}
