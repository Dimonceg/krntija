using Krntija.Core.WorldGen;
using Xunit;

namespace Krntija.Core.Tests;

public class DecorTests
{
    private const ulong Seed = 20261025;

    private static IEnumerable<(PlacePlan Place, IReadOnlyList<DecorPlan> Decor)> Sample(RoadCurve curve)
    {
        for (int chunk = 0; chunk < 120; chunk++)
            foreach (var place in WorldGenerator.Plan(Seed, chunk).Places)
                yield return (place, WorldGenerator.Decor(Seed, place, curve));
    }

    [Fact]
    public void Decor_IsDeterministicAndUsesOnlyTheYardKit()
    {
        var curve = WorldGenerator.Curve(Seed);
        int total = 0;
        foreach (var (place, decor) in Sample(curve))
        {
            var again = WorldGenerator.Decor(Seed, place, curve);
            Assert.Equal(decor, again);
            Assert.All(decor, item => Assert.Contains(item.ModelId, WorldGenerator.DecorModels));
            total += decor.Count;
        }

        Assert.True(total > 100, $"декора слишком мало: {total}");
    }

    [Fact]
    public void Decor_KeepsEntranceUsesLootAndRoadFree()
    {
        var curve = WorldGenerator.Curve(Seed);
        foreach (var (place, decor) in Sample(curve))
        {
            foreach (var item in decor)
            {
                bool inEntrance = Math.Abs(item.OffsetX) < WorldGenerator.EntranceHalfWidth
                                  && item.OffsetZ < -place.Footprint.HalfDepth
                                  && item.OffsetZ > -place.Footprint.HalfDepth - WorldGenerator.EntranceDepth;
                Assert.False(inEntrance, $"{place.Id}: {item.ModelId} перед входом");

                foreach (var use in place.Uses)
                    Assert.True(Distance(use.OffsetX, use.OffsetZ, item) >= WorldGenerator.DecorUseClearance,
                        $"{place.Id}: {item.ModelId} у точки {use.Kind}");
                foreach (var loot in place.Loot)
                    Assert.True(Distance(loot.OffsetX, loot.OffsetZ, item) >= WorldGenerator.DecorUseClearance,
                        $"{place.Id}: {item.ModelId} на луте");

                var (x, z) = WorldGenerator.PlaceToWorld(place, curve, item.OffsetX, item.OffsetZ);
                Assert.True(Math.Abs(curve.Project(x, z).Lateral) >= WorldGenerator.DecorRoadClearance,
                    $"{place.Id}: {item.ModelId} у дороги");
            }
        }
    }

    [Fact]
    public void PlaceToWorld_MatchesPlaceCenterAndRoadSide()
    {
        var curve = WorldGenerator.Curve(Seed);
        var place = WorldGenerator.Plan(Seed, 1).Places[0];
        var center = PlaceGeometry.Center(place, curve);
        var (x, z) = WorldGenerator.PlaceToWorld(place, curve, 0, 0);
        Assert.Equal(center.X, x, 9);
        Assert.Equal(center.Z, z, 9);

        // Ось X места смотрит от дороги вправо: +X у места слева от дороги — ближе к полотну.
        var (rx, rz) = WorldGenerator.PlaceToWorld(place, curve, 5, 0);
        double lateralCenter = curve.Project(x, z).Lateral;
        double lateralRight = curve.Project(rx, rz).Lateral;
        Assert.True(lateralRight > lateralCenter, $"{lateralCenter:F2} → {lateralRight:F2}");
    }

    private static double Distance(double x, double z, DecorPlan item) =>
        Math.Sqrt((x - item.OffsetX) * (x - item.OffsetX) + (z - item.OffsetZ) * (z - item.OffsetZ));
}
