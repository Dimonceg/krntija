using Krntija.Core.Items;
using Krntija.Core.Vehicle;

namespace Krntija.Core.Economy;

/// <summary>Кошелёк. Деньги — единственная валюта, всё остальное считается в них.</summary>
public sealed class Wallet
{
    public Wallet(double money = 120) => Money = Math.Max(0, money);

    public double Money { get; private set; }

    public bool CanAfford(double amount) => Money + 1e-9 >= amount;

    public bool TrySpend(double amount)
    {
        if (amount < 0 || !CanAfford(amount)) return false;
        Money -= amount;
        return true;
    }

    public void Earn(double amount)
    {
        if (amount > 0) Money += amount;
    }

    public void Set(double amount) => Money = Math.Max(0, amount);
}

public sealed record ShopEntry(string ItemId, double Price);

/// <summary>Цены и правила торговли: заправка, лавка, продажа.</summary>
public static class TradeService
{
    public const double FuelPricePerLiter = 1.35;

    public static readonly IReadOnlyList<ShopEntry> Shop = new[]
    {
        new ShopEntry("bread", 3),
        new ShopEntry("water", 2),
        new ShopEntry("duct_tape", 5),
        new ShopEntry("spark_plug", 8),
        new ShopEntry("motor_oil", 25),
        new ShopEntry("jerry_can", 30),
    };

    /// <summary>
    /// Заливает столько, сколько просят, влезает и позволяет кошелёк.
    /// Возвращает литры и списанную сумму.
    /// </summary>
    public static (double Liters, double Cost) RefuelAtPump(VehicleState car, Wallet wallet, double litersRequested)
    {
        double room = car.Fuel.CapacityLiters - car.Fuel.Liters;
        double affordable = Math.Floor(wallet.Money / FuelPricePerLiter * 100) / 100;
        double liters = Math.Max(0, Math.Min(Math.Min(litersRequested, room), affordable));

        if (liters <= 0)
            return (0, 0);

        double cost = Math.Round(liters * FuelPricePerLiter, 2);
        if (!wallet.TrySpend(cost))
            return (0, 0);

        car.Fuel.Refuel(liters);
        return (liters, cost);
    }

    public static double PriceOf(string itemId) =>
        Shop.FirstOrDefault(e => e.ItemId == itemId)?.Price ?? 0;

    public static bool TryBuy(Inventory bag, Wallet wallet, string itemId, int count = 1)
    {
        double price = PriceOf(itemId);
        if (price <= 0 || count <= 0) return false;
        if (!wallet.CanAfford(price * count)) return false;

        // Сначала проверяем, что вещь влезает, и только потом снимаем деньги.
        double before = bag.WeightKg;
        if (bag.WeightKg + ItemCatalog.Get(itemId).WeightKg * count > bag.MaxWeightKg + 1e-9)
            return false;

        if (!bag.TryAdd(itemId, count)) return false;
        wallet.TrySpend(price * count);
        return bag.WeightKg > before;
    }

    /// <summary>Скупка хлама: всё из категории «хлам» уходит за половину цены.</summary>
    public static (int Count, double Money) SellJunk(Inventory bag, Wallet wallet)
    {
        int count = 0;
        double money = 0;

        foreach (var stack in bag.Items.Where(i => i.Definition.Category == ItemCategory.Junk).ToList())
        {
            int amount = stack.Count;
            if (!bag.Remove(stack.DefId, amount)) continue;

            double earned = Math.Round(stack.Definition.Value * 0.5 * amount, 2);
            wallet.Earn(earned);
            count += amount;
            money += earned;
        }

        return (count, Math.Round(money, 2));
    }

    /// <summary>
    /// Скупщик берёт всё, кроме инструментов: те нужны самому.
    /// Платит хуже лавки — зато не привередничает.
    /// </summary>
    public static (int Count, double Money) SellEverything(Inventory bag, Wallet wallet, double factor = 0.7)
    {
        int count = 0;
        double money = 0;

        foreach (var stack in bag.Items.Where(i => i.Definition.Category != ItemCategory.Tool).ToList())
        {
            int amount = stack.Count;
            if (!bag.Remove(stack.DefId, amount)) continue;

            double earned = Math.Round(stack.Definition.Value * factor * amount, 2);
            wallet.Earn(earned);
            count += amount;
            money += earned;
        }

        return (count, Math.Round(money, 2));
    }

    /// <summary>Скупка: деталь можно сдать за половину цены.</summary>
    public static bool TrySellPart(Inventory bag, Wallet wallet, string partDefId)
    {
        if (!Parts.PartCatalog.All.TryGetValue(partDefId, out var def)) return false;
        if (!bag.Remove(partDefId, 1)) return false;

        wallet.Earn(Math.Round(def.Price * 0.5, 2));
        return true;
    }
}
