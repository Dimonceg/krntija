using Krntija.Core.Items;
using Krntija.Core.World;
using Xunit;

namespace Krntija.Core.Tests;

public class LootTests
{
    [Fact]
    public void RollIsDeterministicForSameSeed()
    {
        var first = LootRoll.Roll(LootTables.Village, new Pcg32(123, sequence: 7));
        var second = LootRoll.Roll(LootTables.Village, new Pcg32(123, sequence: 7));

        Assert.Equal(first.Count, second.Count);
        for (int i = 0; i < first.Count; i++)
        {
            Assert.Equal(first[i].DefId, second[i].DefId);
            Assert.Equal(first[i].Count, second[i].Count);
        }
    }

    [Fact]
    public void DifferentSeedsGiveDifferentLootAtLeastSometimes()
    {
        var seen = new HashSet<string>();

        for (ulong seed = 1; seed <= 40; seed++)
        {
            foreach (var stack in LootRoll.Roll(LootTables.Ruins, new Pcg32(seed, sequence: 3)))
                seen.Add(stack.DefId);
        }

        Assert.True(seen.Count > 1, "разные сиды должны давать разный лут");
    }

    [Fact]
    public void RollNeverExceedsTableLimits()
    {
        for (ulong seed = 1; seed <= 50; seed++)
        {
            var loot = LootRoll.Roll(LootTables.GasStation, new Pcg32(seed, sequence: 11));

            Assert.InRange(loot.Count, LootTables.GasStation.MinItems, LootTables.GasStation.MaxItems);
            Assert.All(loot, stack => Assert.True(stack.Count > 0));
        }
    }

    [Fact]
    public void EveryTableItemExistsInCatalog()
    {
        var tables = new[]
        {
            LootTables.GasStation, LootTables.Village, LootTables.Ruins,
            LootTables.Farm, LootTables.House, LootTables.Roadside,
        };

        foreach (var table in tables)
            foreach (var entry in table.Entries)
                Assert.True(ItemCatalog.TryGet(entry.ItemId, out _), $"{table.Id}: нет предмета {entry.ItemId}");
    }

    [Fact]
    public void CatalogItemsHaveValueAndCategory()
    {
        foreach (var item in ItemCatalog.All.Values)
        {
            Assert.True(item.Value >= 0, $"{item.Id}: отрицательная цена");
            Assert.True(item.WeightKg > 0, $"{item.Id}: нулевой вес");
        }
    }

    [Fact]
    public void JunkItemsAreStackableAndCheap()
    {
        foreach (var junk in ItemCatalog.All.Values.Where(item => item.Category == ItemCategory.Junk))
        {
            Assert.True(junk.Stackable, $"{junk.Id}: хлам должен складываться");
            Assert.True(junk.Value <= 20, $"{junk.Id}: хлам не должен стоить как золото");
        }
    }

    [Fact]
    public void InventoryRefusesOverweightLoot()
    {
        var bag = new Inventory(slots: 4, maxWeightKg: 2);
        Assert.True(bag.TryAdd("bread", 2));
        Assert.False(bag.TryAdd("spare_tire"), "запаска не должна влезть в лёгкий инвентарь");
    }

    [Fact]
    public void LootCanBePickedUpIntoInventory()
    {
        var bag = new Inventory(slots: 8, maxWeightKg: 40);
        var loot = LootRoll.Roll(LootTables.House, new Pcg32(2026, sequence: 5));

        foreach (var stack in loot)
            Assert.True(bag.TryAdd(stack), $"не удалось подобрать {stack.DefId}");

        Assert.Equal(loot.Sum(stack => stack.Count), bag.Items.Sum(stack => stack.Count));
        Assert.True(bag.WeightKg > 0);
    }
}
