using Krntija.Core.Economy;
using Krntija.Core.Items;
using Krntija.Core.Parts;
using Krntija.Core.World;
using Krntija.Core.WorldGen;

namespace Krntija.Core;

/// <summary>Чем закончилось платное действие: игра по этому выбирает подсказку.</summary>
public enum ActionResult
{
    Done,
    NothingToDo,
    NotEnoughMoney,
    NoDuctTape,
    NoRoom,
}

/// <summary>
/// Правила действий игрока в мире: шлагбаум, ночлег, колодец, верстак, выезды.
/// Цены и эффекты живут здесь, а не в сцене: их можно проверить тестом,
/// а в коопе их применит хост.
/// </summary>
public static class WorldActions
{
    public const double GateToll = 15;
    public const double MotelPrice = 15;
    public const double WorkbenchPrice = 25;
    public const double ExitRepairPrice = 60;
    public const double PlaceReward = 40;
    public const double ObjectiveReward = 80;

    public const double SleepHours = 8;
    public const double WakeUpHour = 7;
    public const double HomeSleepHeal = 0.2;
    public const double WellHydration = 0.35;

    /// <summary>Верстак берётся только за детали, изношенные сильнее этого.</summary>
    public const double WorkbenchThreshold = 0.95;

    public static bool TryPayToll(Wallet wallet) => wallet.TrySpend(GateToll);

    /// <summary>Первое посещение места — награда. Повторно (и после загрузки) не платим.</summary>
    public static bool TryRewardPlace(GameSession session, int poiIndex)
    {
        if (!session.WorldState.MarkDone(WorldState.Place(poiIndex)))
            return false;

        session.Wallet.Earn(PlaceReward);
        return true;
    }

    /// <summary>Первое посещение места из плана (мир кусками). Платим только за заметные места.</summary>
    public static bool TryRewardPlace(GameSession session, PlacePlan place)
    {
        if (!RewardsVisit(place.Kind) || !session.WorldState.MarkDone(WorldState.Place(place.Id)))
            return false;

        session.Wallet.Earn(PlaceReward);
        return true;
    }

    /// <summary>
    /// За какие места платим при первом посещении. Сараи, колодцы и брошенные дома
    /// стоят в каждом куске: награда за них раздула бы экономику.
    /// </summary>
    public static bool RewardsVisit(PlaceKind kind) => kind is PlaceKind.GasStation or PlaceKind.Village
        or PlaceKind.Checkpoint or PlaceKind.Ruins or PlaceKind.Farm;

    /// <summary>Мотель: плата, восемь часов сна и подъём в 7 утра.</summary>
    public static bool TrySleepAtMotel(GameSession session)
    {
        if (!session.Wallet.TrySpend(MotelPrice))
            return false;

        session.Survival.Sleep(SleepHours);

        double now = session.Clock.TotalMinutes;
        double dayStart = Math.Floor(now / GameClock.MinutesPerDay) * GameClock.MinutesPerDay;
        double morning = dayStart + WakeUpHour * 60;
        if (morning <= now) morning += GameClock.MinutesPerDay;
        session.Clock.SetTotalMinutes(morning);
        return true;
    }

    /// <summary>Сон дома: бесплатно, восемь игровых часов, силы и немного здоровья.</summary>
    public static void SleepAtHome(GameSession session)
    {
        session.Clock.SetTotalMinutes(session.Clock.TotalMinutes + SleepHours * 60);
        session.Survival.Sleep(SleepHours);
        session.Survival.Fatigue = 1.0;
        session.Survival.Heal(HomeSleepHeal);
    }

    public static void DrinkFromWell(GameSession session) => session.Survival.Drink(WellHydration);

    /// <summary>Верстак: самая изношенная деталь становится как новая за плату и изоленту.</summary>
    public static ActionResult TryRepairAtWorkbench(GameSession session, out Part? repaired)
    {
        repaired = session.Vehicle.Parts.Values
            .Where(part => part.Condition < WorkbenchThreshold)
            .OrderBy(part => part.Condition)
            .FirstOrDefault();

        if (repaired is null)
            return ActionResult.NothingToDo;

        var result = PayWithTape(session.Wallet, session.Inventory, WorkbenchPrice);
        if (result == ActionResult.Done)
            repaired.SetCondition(1);

        return result;
    }

    /// <summary>Рецепты верстака: из хлама собираем нужное.</summary>
    public static readonly (string Result, string[] Ingredients)[] Recipes =
    {
        ("spare_tire", new[] { "scrap_metal", "scrap_metal", "scrap_metal", ItemIds.DuctTape, ItemIds.DuctTape }),
        ("spark_plug", new[] { "radio_part", "scrap_metal" }),
        ("medkit", new[] { ItemIds.DuctTape, "water" }),
    };

    /// <summary>Собрать, что получается из того, что лежит в кармане.</summary>
    public static ActionResult TryCraft(GameSession session, out string crafted)
    {
        crafted = string.Empty;

        foreach (var recipe in Recipes)
        {
            var need = recipe.Ingredients
                .GroupBy(id => id)
                .Select(group => (Id: group.Key, Count: group.Count()))
                .ToList();

            if (need.Any(item => session.Inventory.Count(item.Id) < item.Count))
                continue;

            foreach (var item in need)
                session.Inventory.Remove(item.Id, item.Count);

            // Результат может не влезть по весу (запаска — 12 кг): тогда возвращаем хлам.
            if (!session.Inventory.TryAdd(recipe.Result, 1))
            {
                foreach (var item in need)
                    session.Inventory.TryAdd(item.Id, item.Count);
                return ActionResult.NoRoom;
            }

            crafted = ItemCatalog.Get(recipe.Result).Name;
            return ActionResult.Done;
        }

        return ActionResult.NothingToDo;
    }

    /// <summary>Что лежит в ящике: детерминированно от его id, чтобы в коопе совпадало.</summary>
    public static (string ItemId, int Count) CrateLoot(string crateStateId)
    {
        var rng = new Pcg32(WorldGen.Hash.MixString(0x5EED, crateStateId), sequence: 31);
        var loot = LootRoll.Roll(LootTables.Roadside, rng);
        var first = loot[0];
        return (first.DefId, first.Count);
    }

    /// <summary>Починка закрытого выезда: плата и моток изоленты.</summary>
    public static ActionResult TryRepairExit(Wallet wallet, Inventory bag) =>
        PayWithTape(wallet, bag, ExitRepairPrice);

    private static ActionResult PayWithTape(Wallet wallet, Inventory bag, double price)
    {
        if (!wallet.CanAfford(price))
            return ActionResult.NotEnoughMoney;

        if (bag.Count(ItemIds.DuctTape) < 1)
            return ActionResult.NoDuctTape;

        wallet.TrySpend(price);
        bag.Remove(ItemIds.DuctTape, 1);
        return ActionResult.Done;
    }
}
