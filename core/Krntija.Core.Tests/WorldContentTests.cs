using Krntija.Core.WorldGen;
using Xunit;

namespace Krntija.Core.Tests;

public class WorldContentTests
{
    private const ulong Seed = 20261025;

    [Fact]
    public void VillageCarriesDealerSleepAndTwoResidents()
    {
        for (int chunk = 0; chunk < 40; chunk++)
        {
            var village = WorldGenerator.Plan(Seed, chunk).Places
                .FirstOrDefault(place => place.Kind == PlaceKind.Village);
            if (village is null) continue;

            Assert.Contains(village.Uses, use => use.Kind == "sell");
            Assert.Contains(village.Uses, use => use.Kind == "sleep");
            Assert.Equal(2, village.Uses.Count(use => use.Kind == "talk"));
            return;
        }

        Assert.Fail("в первых 40 кусках не нашлось села");
    }

    [Fact]
    public void CheckpointCarriesGateAndClosedExit()
    {
        var checkpoint = WorldGenerator.Plan(Seed, 2).Places
            .First(place => place.Kind == PlaceKind.Checkpoint);

        Assert.Contains(checkpoint.Uses, use => use.Kind == "gate");
        Assert.Contains(checkpoint.Uses, use => use.Kind == "exit");
    }

    [Fact]
    public void ThreatGrowsWithTierButStartIsCalm()
    {
        Assert.Equal(0, ThreatRules.WolvesAt(0));
        Assert.True(ThreatRules.WolvesAt(3) >= ThreatRules.WolvesAt(1));
        Assert.False(ThreatRules.HasCollapse(1));
        Assert.True(ThreatRules.HasCollapse(5));
        Assert.True(ThreatRules.TollPrice(5) > ThreatRules.TollPrice(0));
        Assert.InRange(ThreatRules.DangerChance(0), 0.1, 0.85);
        Assert.InRange(ThreatRules.DangerChance(10), 0.1, 0.85);
    }

    [Fact]
    public void WolvesAndCollapseAppearOnlyAtDangerousChunks()
    {
        var startChunk = WorldGenerator.Plan(Seed, 0);
        var early = WorldGenerator.Plan(Seed, 1);

        Assert.DoesNotContain(startChunk.Places.SelectMany(place => place.Uses), use => use.Kind == "wolves");
        Assert.DoesNotContain(early.Places.SelectMany(place => place.Uses), use => use.Kind == "collapse");

        var lateUses = new List<UsePlan>();
        for (int chunk = 40; chunk < 60; chunk++)
            lateUses.AddRange(WorldGenerator.Plan(Seed, chunk).Places.SelectMany(place => place.Uses));

        Assert.Contains(lateUses, use => use.Kind == "collapse");
    }
}
