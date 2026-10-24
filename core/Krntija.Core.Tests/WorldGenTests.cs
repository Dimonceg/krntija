using System.Text.RegularExpressions;
using Krntija.Core.WorldGen;
using Xunit;

namespace Krntija.Core.Tests;

public class WorldGenTests
{
    private const ulong Seed = 20261025;

    [Fact]
    public void SameSeedAndChunkGiveSamePlan()
    {
        var first = WorldGenerator.Plan(Seed, 12);
        var second = WorldGenerator.Plan(Seed, 12);

        AssertPlansEqual(first, second);
    }

    [Fact]
    public void PlanDoesNotDependOnCallOrderOrEarlierChunks()
    {
        var direct = WorldGenerator.Plan(Seed, 7);

        for (int i = 0; i < 7; i++) WorldGenerator.Plan(Seed, i);
        var afterOthers = WorldGenerator.Plan(Seed, 7);

        AssertPlansEqual(direct, afterOthers);
    }

    [Fact]
    public void DifferentSeedsGiveDifferentWorlds()
    {
        var a = WorldGenerator.Plan(Seed, 9);
        var b = WorldGenerator.Plan(Seed + 1, 9);

        Assert.NotEqual(Serialize(a), Serialize(b));
    }

    [Fact]
    public void IdsAreUniqueAndHaveExpectedFormat()
    {
        var placeIds = new HashSet<string>();
        var lootIds = new HashSet<string>();
        var useIds = new HashSet<string>();

        for (int chunk = 0; chunk < 25; chunk++)
        {
            foreach (var place in WorldGenerator.Plan(Seed, chunk).Places)
            {
                Assert.Matches(new Regex(@"^c\d+/p\d+$"), place.Id);
                Assert.True(placeIds.Add(place.Id), $"повтор id места: {place.Id}");

                foreach (var loot in place.Loot)
                {
                    Assert.Matches(new Regex(@"^c\d+/p\d+/l\d+$"), loot.Id);
                    Assert.True(lootIds.Add(loot.Id), $"повтор id лута: {loot.Id}");
                }

                foreach (var use in place.Uses)
                {
                    Assert.Matches(new Regex(@"^c\d+/p\d+/u\d+$"), use.Id);
                    Assert.True(useIds.Add(use.Id), $"повтор id точки: {use.Id}");
                }
            }
        }
    }

    [Fact]
    public void RoadStaysWithinLimitsForHundredKilometers()
    {
        var curve = WorldGenerator.Curve(Seed);

        for (double s = 0; s <= 100_000; s += 5)
        {
            Assert.True(Math.Abs(curve.LateralSlope(s)) <= RoadCurve.MaxLateralSlope,
                $"дорога идёт поперёк оси на {s} м");

            if (curve.TurnRadius(s) < RoadCurve.MinTurnRadius)
                Assert.Fail($"слишком крутой поворот на {s} м: радиус {curve.TurnRadius(s):F0} м");

            Assert.True(Math.Abs(curve.Slope(s)) <= RoadCurve.MaxGrade,
                $"уклон больше 8 % на {s} м");
        }
    }

    [Fact]
    public void RoadActuallyTurns()
    {
        // Защита от «бесконечной прямой»: за 20 км дорога должна заметно петлять.
        var curve = WorldGenerator.Curve(Seed);
        double maxHeading = 0, minLateral = double.MaxValue, maxLateral = double.MinValue;

        for (double s = 0; s <= 20_000; s += 10)
        {
            maxHeading = Math.Max(maxHeading, Math.Abs(curve.Heading(s)));
            minLateral = Math.Min(minLateral, curve.Lateral(s));
            maxLateral = Math.Max(maxLateral, curve.Lateral(s));
        }

        Assert.True(maxHeading > 25 * Math.PI / 180, $"самый крутой поворот всего {maxHeading * 180 / Math.PI:F0}°");
        Assert.True(maxLateral - minLateral > 200, $"дорога гуляет вбок всего на {maxLateral - minLateral:F0} м");
    }

    [Fact]
    public void RoadShapeDependsOnSeed()
    {
        var a = WorldGenerator.Curve(1);
        var b = WorldGenerator.Curve(2);

        // Не просто сдвиг фазы: на одной и той же станции дороги расходятся.
        Assert.True(Math.Abs(a.Lateral(5_000) - b.Lateral(5_000)) > 1);
        Assert.NotEqual(a.TurnRadius(1_234).ToString("F3"), b.TurnRadius(1_234).ToString("F3"));
    }

    [Fact]
    public void RandomSeedsOfPlacesAndChunksDoNotCollide()
    {
        // Сид ГПСЧ каждого места (и уровня куска: место −1) уникален на первых 300 кусках.
        var seen = new HashSet<ulong>();
        for (int chunk = 0; chunk < 300; chunk++)
        for (int place = -1; place < 8; place++)
            Assert.True(seen.Add(Hash.Mix(Seed, chunk, place)), $"совпадение сидов: кусок {chunk}, место {place}");
    }

    [Fact]
    public void PlacesStayOffRoadAndDoNotOverlap()
    {
        for (int chunk = 0; chunk < 30; chunk++)
        {
            var places = WorldGenerator.Plan(Seed, chunk).Places;

            foreach (var place in places)
            {
                Assert.True(place.S >= chunk * WorldGenerator.ChunkLength - 0.001,
                    $"{place.Id} стоит до своего куска");
                Assert.True(place.S < (chunk + 1) * WorldGenerator.ChunkLength + 0.001,
                    $"{place.Id} вылез за кусок");

                if (!place.OnRoad)
                    Assert.True(Math.Abs(place.Lateral) >= 12, $"{place.Id} стоит на полотне");

                foreach (var other in places)
                {
                    if (ReferenceEquals(other, place) || string.CompareOrdinal(other.Id, place.Id) <= 0) continue;

                    double along = Math.Abs(other.S - place.S);
                    Assert.True(along >= 25, $"{place.Id} и {other.Id} слишком близко: {along:F0} м");
                }
            }
        }
    }

    [Fact]
    public void TierNeverDecreases()
    {
        int previous = 0;

        for (double s = 0; s <= 100_000; s += 100)
        {
            int tier = WorldGenerator.Tier(s);
            Assert.True(tier >= previous, $"тир упал на {s} м");
            Assert.InRange(tier, 0, WorldGenerator.MaxTier);
            previous = tier;
        }
    }

    [Fact]
    public void ChunkZeroHasHouseAndEarlyChunksGiveEconomyAndGate()
    {
        var start = WorldGenerator.Plan(Seed, 0);
        Assert.Contains(start.Places, place => place.Kind == PlaceKind.House);

        var kinds = new List<PlaceKind>();
        for (int chunk = 0; chunk < 4; chunk++)
            kinds.AddRange(WorldGenerator.Plan(Seed, chunk).Places.Select(place => place.Kind));

        Assert.Contains(PlaceKind.GasStation, kinds);
        Assert.Contains(PlaceKind.Checkpoint, kinds);
    }

    [Fact]
    public void PlaceIsIndependentOfNeighbours()
    {
        // Место, которое точно попадает в план, обязано совпасть с прямым вызовом.
        var direct = WorldGenerator.Place(Seed, 5, 0);
        Assert.NotNull(direct);

        var plan = WorldGenerator.Plan(Seed, 5);
        var fromPlan = plan.Places.FirstOrDefault(place => place.Id == direct!.Id);

        Assert.NotNull(fromPlan);
        Assert.Equal(Serialize(direct!), Serialize(fromPlan!));

        // Лут места не зависит от того, какие места есть в куске рядом.
        var again = WorldGenerator.Place(Seed, 5, 0);
        Assert.Equal(direct!.Loot.Count, again!.Loot.Count);
        Assert.All(direct.Loot, loot => Assert.Single(again.Loot, other => other.Id == loot.Id));
    }

    [Fact]
    public void NegativeChunksDoNotExist()
    {
        Assert.Throws<ArgumentOutOfRangeException>(() => WorldGenerator.Plan(Seed, -1));
    }

    private static void AssertPlansEqual(ChunkPlan a, ChunkPlan b)
    {
        Assert.Equal(a.Index, b.Index);
        Assert.Equal(a.Tier, b.Tier);
        Assert.Equal(Serialize(a), Serialize(b));
    }

    private static string Serialize(ChunkPlan plan) => string.Join("|",
        plan.Places.Select(place => Serialize(place)));

    private static string Serialize(PlacePlan place) =>
        $"{place.Id}:{place.Kind}:{place.S:F3}:{place.Lateral:F3}:{place.Yaw:F4}:{place.VariantSeed}:"
        + string.Join(",", place.Loot.Select(loot => $"{loot.Id}={loot.ItemId}x{loot.Count}@{loot.OffsetX:F2}"))
        + ";" + string.Join(",", place.Uses.Select(use => $"{use.Id}={use.Kind}"));
}
