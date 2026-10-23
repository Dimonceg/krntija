using Krntija.Core.Economy;
using Krntija.Core.Items;
using Krntija.Core.Vehicle;
using Xunit;

namespace Krntija.Core.Tests;

public class EconomyTests
{
    [Fact]
    public void Refuel_ChargesForEveryLiter()
    {
        var car = VehicleState.CreateDefault();
        car.Fuel.SetLiters(10);
        var wallet = new Wallet(100);

        var (liters, cost) = TradeService.RefuelAtPump(car, wallet, litersRequested: 20);

        Assert.Equal(20, liters, 3);
        Assert.Equal(27, cost, 2);
        Assert.Equal(73, wallet.Money, 2);
        Assert.Equal(30, car.Fuel.Liters, 3);
    }

    [Fact]
    public void Refuel_TakesOnlyWhatMoneyAllows()
    {
        var car = VehicleState.CreateDefault();
        car.Fuel.SetLiters(10);
        var wallet = new Wallet(5);

        var (liters, cost) = TradeService.RefuelAtPump(car, wallet, 20);

        Assert.InRange(liters, 3.6, 3.8);   // 5 € / 1.35 ≈ 3.7 л
        Assert.InRange(cost, 4.9, 5.0);
        Assert.True(wallet.Money < 0.01);
    }

    [Fact]
    public void Refuel_StopsAtFullTank()
    {
        var car = VehicleState.CreateDefault();
        car.Fuel.SetLiters(30); // бак 36
        var wallet = new Wallet(1000);

        var (liters, _) = TradeService.RefuelAtPump(car, wallet, 50);

        Assert.Equal(6, liters, 3);
        Assert.Equal(car.Fuel.CapacityLiters, car.Fuel.Liters, 3);
    }

    [Fact]
    public void Buy_SpendsMoneyAndAddsItem()
    {
        var bag = new Inventory();
        var wallet = new Wallet(50);

        bool ok = TradeService.TryBuy(bag, wallet, "duct_tape");

        Assert.True(ok);
        Assert.Equal(1, bag.Count("duct_tape"));
        Assert.Equal(45, wallet.Money, 2);
    }

    [Fact]
    public void Buy_WithoutMoney_Fails()
    {
        var bag = new Inventory();
        var wallet = new Wallet(3);

        Assert.False(TradeService.TryBuy(bag, wallet, "motor_oil"));
        Assert.Equal(0, bag.Count("motor_oil"));
        Assert.Equal(3, wallet.Money, 2);
    }

    [Fact]
    public void Buy_RespectsWeight()
    {
        var bag = new Inventory(slots: 24, maxWeightKg: 6);
        var wallet = new Wallet(100);

        // Канистра 5 кг влезает, масло 4.5 + канистра уже нет.
        Assert.True(TradeService.TryBuy(bag, wallet, "jerry_can"));
        Assert.False(TradeService.TryBuy(bag, wallet, "motor_oil"));
    }

    [Fact]
    public void SellPart_PaysHalfPrice()
    {
        var bag = new Inventory(slots: 24, maxWeightKg: 300);
        bag.TryAdd("radiator_std");
        var wallet = new Wallet(0);

        bool ok = TradeService.TrySellPart(bag, wallet, "radiator_std");

        Assert.True(ok);
        Assert.Equal(0, bag.Count("radiator_std"));
        Assert.Equal(60, wallet.Money, 2); // радиатор 120 €, половина
    }
}
