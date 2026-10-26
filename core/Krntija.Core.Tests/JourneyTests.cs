using Krntija.Core;
using Krntija.Core.Items;
using Krntija.Core.WorldGen;
using Xunit;

namespace Krntija.Core.Tests;

public class JourneyTests
{
    [Fact]
    public void FuelAndWearGrowWithTierAndRain()
    {
        Assert.True(Journey.LitersPerKm(5) > Journey.LitersPerKm(0));
        Assert.True(Journey.LitersPerKm(0, 1) > Journey.LitersPerKm(0));
        Assert.True(Journey.WearPerKm(5) > Journey.WearPerKm(0));

        double range = Journey.RangeKm(40, 0);
        Assert.True(range > 300, $"на 40 л должно быть больше 300 км, а не {range:F0}");

        // «Трудно уехать 5–10 км без заправки»: на старте бака хватает далеко,
        // но с тиром запас падает.
        Assert.True(Journey.RangeKm(40, 5) < range);
    }

    [Fact]
    public void MetersToNextTierMatchesTierFunction()
    {
        Assert.Equal(1000, Journey.MetersToNextTier(0), 0);
        Assert.Equal(7000, Journey.MetersToNextTier(0) + 6000, 0);

        for (double s = 0; s < 20_000; s += 250)
        {
            double toNext = Journey.MetersToNextTier(s);
            if (WorldGenerator.Tier(s) >= WorldGenerator.MaxTier) continue;

            Assert.True(toNext > 0);
            int current = WorldGenerator.Tier(s);
            int next = WorldGenerator.Tier(s + toNext + 0.5);
            Assert.True(next > current, $"на {s} м «до тира» не доводит до следующего");
        }
    }

    [Fact]
    public void RecordOnlyGrows()
    {
        var session = GameSession.NewGame();

        session.StepRoad(500);
        Assert.Equal(500, session.BestDistanceMeters);

        session.StepRoad(300);
        Assert.Equal(500, session.BestDistanceMeters);

        session.StepRoad(7400);
        Assert.Equal(7400, session.BestDistanceMeters);
    }

    [Fact]
    public void RecordSurvivesSaveAndLoad()
    {
        var session = GameSession.NewGame();
        session.StepRoad(7400);

        var data = session.ToSave();
        Assert.Equal(7400, data.BestDistanceMeters);

        var loaded = GameSession.FromSave(data);
        Assert.Equal(7400, loaded.BestDistanceMeters);
    }

    [Fact]
    public void RareUpgradesExistInCatalog()
    {
        string[] rare =
        {
            "upgrade_headlights", "upgrade_bullbar", "upgrade_engine", "upgrade_roofrack",
        };

        foreach (string id in rare)
        {
            Assert.True(UpgradeCatalog.TryGet(id, out var def), $"нет улучшения {id}");
            Assert.True(ItemCatalog.TryGet(id, out _), $"нет предмета {id}");
            Assert.True(def!.Cost > 0);
        }
    }

    [Fact]
    public void RareLootAppearsOnlyFarFromHome()
    {
        string[] rare =
        {
            "upgrade_headlights", "upgrade_bullbar", "upgrade_engine", "upgrade_roofrack",
        };

        int near = 0;
        int far = 0;

        for (int chunk = 0; chunk < 4; chunk++)
            near += Count(WorldGenerator.Plan(20261025, chunk));

        for (int chunk = 40; chunk < 80; chunk++)
            far += Count(WorldGenerator.Plan(20261025, chunk));

        Assert.True(far > 0, "далеко от дома не нашлось редкого лута");
        Assert.True(near == 0, $"у дома выпало редкое: {near}");

        int Count(ChunkPlan plan) => plan.Places
            .SelectMany(place => place.Loot)
            .Count(loot => rare.Contains(loot.ItemId));
    }
}
