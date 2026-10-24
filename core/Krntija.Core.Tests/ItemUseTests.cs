using Krntija.Core.Items;
using Xunit;

namespace Krntija.Core.Tests;

public class ItemUseTests
{
    private static GameSession NewSession()
    {
        var session = GameSession.NewGame();
        session.Survival.Thirst = 0.3;
        session.Survival.Hunger = 0.3;
        session.Survival.Health = 0.5;
        return session;
    }

    [Fact]
    public void Water_QuenchesThirst_AndLeavesTheBag()
    {
        var session = NewSession();

        bool ok = ItemUseService.TryUse(session, ItemIds.Water, out string message);

        Assert.True(ok);
        Assert.InRange(session.Survival.Thirst, 0.64, 0.66);
        Assert.Equal(1, session.Inventory.Count(ItemIds.Water));
        Assert.Contains("жажда", message);
    }

    [Fact]
    public void Bread_SatisfiesHunger()
    {
        var session = NewSession();

        Assert.True(ItemUseService.TryUse(session, ItemIds.Bread, out _));
        Assert.InRange(session.Survival.Hunger, 0.54, 0.56);
    }

    [Fact]
    public void Medkit_Heals()
    {
        var session = NewSession();
        session.Inventory.TryAdd("medkit");

        Assert.True(ItemUseService.TryUse(session, "medkit", out _));
        Assert.InRange(session.Survival.Health, 0.79, 0.81);
    }

    [Fact]
    public void MotorOil_TopsUpOilLevel()
    {
        var session = NewSession();
        session.Vehicle.OilLevel = 0.4;
        session.Inventory.TryAdd("motor_oil");

        Assert.True(ItemUseService.TryUse(session, "motor_oil", out _));
        Assert.InRange(session.Vehicle.OilLevel, 0.69, 0.71);
    }

    [Fact]
    public void JerryCan_PoursFuelIntoTheCar()
    {
        var session = NewSession();
        session.Vehicle.Fuel.SetLiters(5);

        bool ok = ItemUseService.TryUse(session, ItemIds.JerryCan, out string message);

        Assert.True(ok);
        Assert.True(session.Vehicle.Fuel.Liters > 5);
        Assert.Contains("залито", message);
    }

    [Fact]
    public void UnknownOrUselessItem_IsRejected()
    {
        var session = NewSession();

        Assert.False(ItemUseService.TryUse(session, "wrench", out string message));
        Assert.Contains("ничего не сделать", message);
    }

    [Fact]
    public void MissingItem_Fails()
    {
        var session = NewSession();
        session.Inventory.Remove(ItemIds.Water, session.Inventory.Count(ItemIds.Water));

        Assert.False(ItemUseService.TryUse(session, ItemIds.Water, out _));
    }
}
