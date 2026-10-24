using Krntija.Core.Economy;
using Krntija.Core.Items;
using Krntija.Core.Parts;
using Xunit;

namespace Krntija.Core.Tests;

public class WorldActionsTests
{
    [Fact]
    public void SleepAtHome_TakesEightGameHours()
    {
        var session = GameSession.NewGame();
        session.Clock.SetTotalMinutes(22 * 60);
        session.Survival.Fatigue = 0.1;

        WorldActions.SleepAtHome(session);

        // Раньше часы двигались как реальные секунды: 8 часов превращались в 20 суток.
        Assert.Equal(30 * 60, session.Clock.TotalMinutes, precision: 3);
        Assert.Equal(1.0, session.Survival.Fatigue);
    }

    [Fact]
    public void SleepAtHome_HealsAndCostsFoodAndWater()
    {
        var session = GameSession.NewGame();
        session.Survival.Health = 0.5;
        double hunger = session.Survival.Hunger;

        WorldActions.SleepAtHome(session);

        Assert.Equal(0.5 + WorldActions.HomeSleepHeal, session.Survival.Health, precision: 6);
        Assert.True(session.Survival.Hunger < hunger);
    }

    [Fact]
    public void Motel_WakesAtSevenNextMorning()
    {
        var session = GameSession.NewGame();
        session.Clock.SetTotalMinutes(21 * 60);
        double money = session.Wallet.Money;

        Assert.True(WorldActions.TrySleepAtMotel(session));

        Assert.Equal(2, session.Clock.Day);
        Assert.Equal(7.0, session.Clock.HourOfDay, precision: 6);
        Assert.Equal(money - WorldActions.MotelPrice, session.Wallet.Money, precision: 6);
    }

    [Fact]
    public void Motel_WithoutMoney_KeepsTimeAndFatigue()
    {
        var session = GameSession.NewGame();
        session.Wallet.Set(5);
        session.Clock.SetTotalMinutes(21 * 60);
        double fatigue = session.Survival.Fatigue;

        Assert.False(WorldActions.TrySleepAtMotel(session));

        Assert.Equal(21 * 60, session.Clock.TotalMinutes);
        Assert.Equal(fatigue, session.Survival.Fatigue);
    }

    [Fact]
    public void Workbench_RestoresWorstPartForMoneyAndTape()
    {
        var session = GameSession.NewGame();
        var engine = session.Vehicle.Get(PartSlot.Engine)!;
        engine.SetCondition(0.3);
        int tape = session.Inventory.Count(ItemIds.DuctTape);
        double money = session.Wallet.Money;

        var result = WorldActions.TryRepairAtWorkbench(session, out var repaired);

        Assert.Equal(ActionResult.Done, result);
        Assert.Same(engine, repaired);
        Assert.Equal(1.0, engine.Condition);
        Assert.Equal(tape - 1, session.Inventory.Count(ItemIds.DuctTape));
        Assert.Equal(money - WorldActions.WorkbenchPrice, session.Wallet.Money, precision: 6);
    }

    [Fact]
    public void Workbench_WithoutTape_ChargesNothing()
    {
        var session = GameSession.NewGame();
        session.Vehicle.Get(PartSlot.Engine)!.SetCondition(0.3);
        session.Inventory.Remove(ItemIds.DuctTape, session.Inventory.Count(ItemIds.DuctTape));
        double money = session.Wallet.Money;

        var result = WorldActions.TryRepairAtWorkbench(session, out _);

        Assert.Equal(ActionResult.NoDuctTape, result);
        Assert.Equal(money, session.Wallet.Money);
        Assert.Equal(0.3, session.Vehicle.Get(PartSlot.Engine)!.Condition, precision: 6);
    }

    [Fact]
    public void Workbench_HealthyCar_HasNothingToDo()
    {
        var session = GameSession.NewGame();
        foreach (var part in session.Vehicle.Parts.Values) part.SetCondition(1);

        Assert.Equal(ActionResult.NothingToDo, WorldActions.TryRepairAtWorkbench(session, out _));
    }

    [Fact]
    public void ExitRepair_ChecksMoneyBeforeTape()
    {
        var wallet = new Wallet(10);
        var bag = new Inventory();

        Assert.Equal(ActionResult.NotEnoughMoney, WorldActions.TryRepairExit(wallet, bag));

        wallet.Set(100);
        Assert.Equal(ActionResult.NoDuctTape, WorldActions.TryRepairExit(wallet, bag));

        bag.TryAdd(ItemIds.DuctTape);
        Assert.Equal(ActionResult.Done, WorldActions.TryRepairExit(wallet, bag));
        Assert.Equal(40, wallet.Money, precision: 6);
        Assert.Equal(0, bag.Count(ItemIds.DuctTape));
    }

    [Fact]
    public void Well_QuenchesThirstUpToFull()
    {
        var session = GameSession.NewGame();
        session.Survival.Thirst = 0.9;

        WorldActions.DrinkFromWell(session);

        Assert.Equal(1.0, session.Survival.Thirst);
    }
}
