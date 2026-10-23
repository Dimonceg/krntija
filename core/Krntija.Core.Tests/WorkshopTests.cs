using Krntija.Core.Items;
using Krntija.Core.Parts;
using Krntija.Core.Vehicle;
using Krntija.Core.Workshop;
using Xunit;

namespace Krntija.Core.Tests;

public class WorkshopTests
{
    private static Inventory BigBag()
    {
        // Багажник, в который влезает даже двигатель.
        return new Inventory(slots: 24, maxWeightKg: 300);
    }

    [Fact]
    public void ReplacePart_NeedsThePartInTheBag()
    {
        var car = VehicleState.CreateDefault();
        var bag = BigBag();

        bool ok = VehicleService.TryReplacePart(car, bag, "radiator_std", out _);

        Assert.False(ok);
        Assert.Equal(PartCatalog.All["radiator_std"].Slot, car.Get(PartSlot.Radiator)!.Slot);
    }

    [Fact]
    public void ReplacePart_SwapsAndReturnsTheOldOne()
    {
        var car = VehicleState.CreateDefault();
        car.Get(PartSlot.Radiator)!.SetCondition(0.2);

        var bag = BigBag();
        bag.TryAdd("radiator_std");

        bool ok = VehicleService.TryReplacePart(car, bag, "radiator_std", out var replaced);

        Assert.True(ok);
        Assert.NotNull(replaced);
        Assert.Equal(0.2, replaced!.Condition, 3);
        Assert.Equal(1.0, car.Get(PartSlot.Radiator)!.Condition, 3);

        // Новая ушла в машину, старая вернулась в сумку — с тем же износом.
        var back = bag.Items.Single(i => i.DefId == ItemIds.Part("radiator_std"));
        Assert.Equal(1, back.Count);
        Assert.Equal(0.2, back.Condition, 3);
    }

    [Fact]
    public void Repair_ConsumesTape()
    {
        var car = VehicleState.CreateDefault();
        car.Get(PartSlot.Engine)!.SetCondition(0.5);

        var bag = BigBag();
        bag.TryAdd(ItemIds.DuctTape, 1);

        bool ok = VehicleService.TryRepair(car, bag, PartSlot.Engine);

        Assert.True(ok);
        Assert.Equal(0.7, car.Get(PartSlot.Engine)!.Condition, 3);
        Assert.Equal(0, bag.Count(ItemIds.DuctTape));
    }

    [Fact]
    public void Repair_RefusesAtFullHealth()
    {
        var car = VehicleState.CreateDefault();
        var bag = BigBag();
        bag.TryAdd(ItemIds.DuctTape, 1);

        Assert.False(VehicleService.TryRepair(car, bag, PartSlot.Engine));
        Assert.Equal(1, bag.Count(ItemIds.DuctTape));
    }

    [Fact]
    public void Refuel_PoursCanIntoTheTank()
    {
        var car = VehicleState.CreateDefault();
        car.Fuel.SetLiters(2);

        var bag = BigBag();
        bag.TryAdd(ItemIds.JerryCan, 1, liters: 15);

        double poured = VehicleService.RefuelFromCan(car, bag);

        Assert.Equal(15, poured, 3);
        Assert.Equal(17, car.Fuel.Liters, 3);
        Assert.Equal(0, bag.Items.Single(i => i.DefId == ItemIds.JerryCan).Liters, 3);
    }

    [Fact]
    public void WorstPart_PointsAtTheMostBrokenOne()
    {
        var car = VehicleState.CreateDefault();
        car.Get(PartSlot.BrakesRear)!.SetCondition(0.05);
        car.Get(PartSlot.Engine)!.SetCondition(0.6);

        Assert.Equal(PartSlot.BrakesRear, VehicleService.WorstPart(car)!.Slot);
    }

    [Fact]
    public void Wheel_GoesIntoTheRequestedSlot()
    {
        var car = VehicleState.CreateDefault();
        car.Get(PartSlot.WheelRearRight)!.SetCondition(0.1);

        var bag = BigBag();
        bag.TryAdd("wheel_steel");

        bool ok = VehicleService.TryReplacePart(car, bag, "wheel_steel", PartSlot.WheelRearRight, out _);

        Assert.True(ok);
        Assert.Equal(1.0, car.Get(PartSlot.WheelRearRight)!.Condition, 3);
        Assert.Equal(0.1, bag.Items.Single(i => i.DefId == "wheel_steel").Condition, 3);
    }

    [Fact]
    public void WrongPartForSlot_IsRejected()
    {
        var car = VehicleState.CreateDefault();
        var bag = BigBag();
        bag.TryAdd("radiator_std");

        bool ok = VehicleService.TryReplacePart(car, bag, "radiator_std", PartSlot.Engine, out _);

        Assert.False(ok);
        Assert.Equal(1, bag.Count("radiator_std")); // деталь осталась в сумке
    }

    [Fact]
    public void FindPartInBag_MatchesSlot_AndWheelsAreInterchangeable()
    {
        var bag = BigBag();
        bag.TryAdd("wheel_steel");
        bag.TryAdd("radiator_std");

        Assert.Equal("wheel_steel", VehicleService.FindPartInBag(bag, PartSlot.WheelRearLeft));
        Assert.Equal("radiator_std", VehicleService.FindPartInBag(bag, PartSlot.Radiator));
        Assert.Null(VehicleService.FindPartInBag(bag, PartSlot.Engine));
    }
}
