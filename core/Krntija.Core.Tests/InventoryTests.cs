using Krntija.Core.Items;
using Xunit;

namespace Krntija.Core.Tests;

public class InventoryTests
{
    [Fact]
    public void StackableItems_MergeIntoOneSlot()
    {
        var inv = new Inventory();

        Assert.True(inv.TryAdd("bread", 3));
        Assert.True(inv.TryAdd("bread", 2));

        Assert.Equal(5, inv.Count("bread"));
        Assert.Single(inv.Items);
    }

    [Fact]
    public void StackSize_IsRespected()
    {
        var inv = new Inventory();

        Assert.True(inv.TryAdd("bread", 7)); // MaxStack = 5

        Assert.Equal(7, inv.Count("bread"));
        Assert.Equal(2, inv.Items.Count());
    }

    [Fact]
    public void WeightLimit_BlocksOverload()
    {
        var inv = new Inventory(slots: 24, maxWeightKg: 20);

        Assert.True(inv.TryAdd("spare_tire"));       // 12 кг
        Assert.False(inv.TryAdd("spare_tire"));      // 24 кг > 20

        Assert.Equal(1, inv.Count("spare_tire"));
    }

    [Fact]
    public void SlotLimit_BlocksWhenFull()
    {
        var inv = new Inventory(slots: 2, maxWeightKg: 1000);

        Assert.True(inv.TryAdd("wrench"));
        Assert.True(inv.TryAdd("map"));
        Assert.False(inv.TryAdd("spare_tire"));

        Assert.Equal(2, inv.Items.Count());
    }

    [Fact]
    public void Remove_TakesExactlyWhatAsked()
    {
        var inv = new Inventory();
        inv.TryAdd("water", 4);

        Assert.True(inv.Remove("water", 3));
        Assert.Equal(1, inv.Count("water"));

        Assert.False(inv.Remove("water", 5));
        Assert.Equal(1, inv.Count("water"));
    }

    [Fact]
    public void FuelCan_KeepsItsLiters()
    {
        var inv = new Inventory();
        inv.TryAdd("jerry_can", 1, condition: 0.6, liters: 12.5);

        var can = inv.Items.Single();

        Assert.Equal(12.5, can.Liters);
        Assert.Equal(0.6, can.Condition);
        Assert.Equal(5.0, can.TotalWeightKg);
    }

    [Fact]
    public void UnknownItem_IsRejected()
    {
        var inv = new Inventory();
        Assert.False(inv.TryAdd("nonsense"));
    }

    [Fact]
    public void TryAdd_StackThatDoesNotFitInSlots_AddsNothing()
    {
        // Один слот, стопка металлолома до 10: 8 + 5 не помещаются.
        var bag = new Inventory(slots: 1, maxWeightKg: 1000);
        bag.TryAdd("scrap_metal", 8);

        Assert.False(bag.TryAdd("scrap_metal", 5));
        Assert.Equal(8, bag.Count("scrap_metal"));
    }

    [Fact]
    public void TryAdd_NonStackableBeyondFreeSlots_AddsNothing()
    {
        var bag = new Inventory(slots: 2, maxWeightKg: 1000);

        Assert.False(bag.TryAdd("spare_tire", 3));
        Assert.Equal(0, bag.Count("spare_tire"));
        Assert.True(bag.TryAdd("spare_tire", 2));
    }
}
