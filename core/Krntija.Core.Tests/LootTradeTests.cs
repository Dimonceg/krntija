using Krntija.Core;
using Krntija.Core.Economy;
using Krntija.Core.Items;
using Xunit;

namespace Krntija.Core.Tests;

public class LootTradeTests
{
    [Fact]
    public void SellingJunkRemovesItAndPaysHalfValue()
    {
        var bag = new Inventory();
        var wallet = new Wallet(0);
        bag.TryAdd("cassette", 4);   // 5 € за штуку
        bag.TryAdd("bread", 2);      // еда не хлам

        var (count, money) = TradeService.SellJunk(bag, wallet);

        Assert.Equal(4, count);
        Assert.Equal(10, money, 2);
        Assert.Equal(0, bag.Count("cassette"));
        Assert.Equal(2, bag.Count("bread"));
        Assert.Equal(10, wallet.Money, 2);
    }

    [Fact]
    public void NothingToSellWhenNoJunk()
    {
        var bag = new Inventory();
        var wallet = new Wallet(0);
        bag.TryAdd("bread", 1);

        var (count, money) = TradeService.SellJunk(bag, wallet);

        Assert.Equal(0, count);
        Assert.Equal(0, money, 2);
        Assert.Equal(1, bag.Count("bread"));
    }

    [Fact]
    public void TrunkSurvivesSaveRoundTrip()
    {
        var session = GameSession.NewGame();
        session.Trunk.TryAdd("spare_tire");
        session.Trunk.TryAdd("cassette", 3);

        var restored = GameSession.FromSave(session.ToSave());

        Assert.True(restored.Trunk.Count("spare_tire") == 1);
        Assert.True(restored.Trunk.Count("cassette") == 3);
        Assert.True(restored.Inventory.Count("bread") == 2);
    }

    [Fact]
    public void DealerBuysEverythingExceptTools()
    {
        var bag = new Inventory();
        var wallet = new Wallet(0);
        bag.TryAdd("wrench");     // инструмент остаётся
        bag.TryAdd("bread", 2);
        bag.TryAdd("spark_plug", 1);
        bag.TryAdd("cassette", 2);

        var (count, money) = TradeService.SellEverything(bag, wallet);

        Assert.Equal(5, count);
        Assert.Equal(1, bag.Count("wrench"));
        Assert.Equal(0, bag.Count("bread"));
        Assert.True(money > 0);
    }

    [Fact]
    public void EncumbranceSlowsOnlyOverEightyPercent()
    {
        Assert.Equal(1f, Encumbrance.SpeedFactor(10, 45), 3);
        Assert.Equal(1f, Encumbrance.SpeedFactor(36, 45), 3);
        Assert.True(Encumbrance.SpeedFactor(45, 45) < 0.7f);
        Assert.True(Encumbrance.SpeedFactor(60, 45) >= 0.55f, "ниже 55 % скорость не падает");
    }

    [Fact]
    public void UpgradesChangeStatsAndPersist()
    {
        var session = GameSession.NewGame();
        double fuel = session.Vehicle.Fuel.CapacityLiters;
        double trunk = session.Trunk.MaxWeightKg;
        double pocket = session.Inventory.MaxWeightKg;

        Assert.Contains("установлено", session.ApplyUpgrade(UpgradeCatalog.FuelTank));
        Assert.Contains("установлено", session.ApplyUpgrade(UpgradeCatalog.Springs));
        Assert.Contains("установлено", session.ApplyUpgrade(UpgradeCatalog.Toolbox));

        Assert.Equal(fuel + 6, session.Vehicle.Fuel.CapacityLiters, 2);
        Assert.Equal(trunk + 15, session.Trunk.MaxWeightKg, 2);
        Assert.Equal(pocket + 10, session.Inventory.MaxWeightKg, 2);

        var restored = GameSession.FromSave(session.ToSave());
        Assert.Equal(fuel + 6, restored.Vehicle.Fuel.CapacityLiters, 2);
        Assert.Contains(UpgradeCatalog.Springs, restored.Upgrades);
        Assert.Contains("уже установлено", restored.ApplyUpgrade(UpgradeCatalog.FuelTank));
    }

    [Fact]
    public void HomeStorageSurvivesSaveRoundTrip()
    {
        var session = GameSession.NewGame();
        session.Home.TryAdd("scrap_metal", 4);
        session.Home.TryAdd("wrench");

        var restored = GameSession.FromSave(session.ToSave());

        Assert.Equal(4, restored.Home.Count("scrap_metal"));
        Assert.Equal(1, restored.Home.Count("wrench"));
        Assert.Equal(0, restored.Home.Count("bread"));
    }
}
