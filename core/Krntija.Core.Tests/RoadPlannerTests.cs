using Krntija.Core.World;
using Xunit;

namespace Krntija.Core.Tests;

public class RoadPlannerTests
{
    [Fact]
    public void SameSeed_SameRoad()
    {
        var a = RoadPlanner.Generate(seed: 42, totalMeters: 2000);
        var b = RoadPlanner.Generate(seed: 42, totalMeters: 2000);

        Assert.Equal(a.Segments.Count, b.Segments.Count);
        for (int i = 0; i < a.Segments.Count; i++)
        {
            Assert.Equal(a.Segments[i].StartM, b.Segments[i].StartM, 3);
            Assert.Equal(a.Segments[i].LengthM, b.Segments[i].LengthM, 3);
            Assert.Equal(a.Segments[i].Curvature, b.Segments[i].Curvature, 3);
            Assert.Equal(a.Segments[i].Feature, b.Segments[i].Feature);
        }
    }

    [Fact]
    public void DifferentSeeds_DifferentRoads()
    {
        var a = RoadPlanner.Generate(seed: 1, totalMeters: 2000);
        var b = RoadPlanner.Generate(seed: 2, totalMeters: 2000);

        Assert.NotEqual(
            a.Segments.Select(s => s.Curvature).ToArray(),
            b.Segments.Select(s => s.Curvature).ToArray());
    }

    [Fact]
    public void Segments_FollowEachOther_WithoutGaps()
    {
        var plan = RoadPlanner.Generate(seed: 7, totalMeters: 3000);

        for (int i = 1; i < plan.Segments.Count; i++)
            Assert.Equal(plan.Segments[i - 1].EndM, plan.Segments[i].StartM, 3);

        Assert.True(plan.TotalMeters >= 3000);
        Assert.True(plan.TotalMeters < 3200);
    }

    [Fact]
    public void Road_AlwaysHasAGasStation_AndOtherPlaces()
    {
        for (ulong seed = 1; seed <= 5; seed++)
        {
            var plan = RoadPlanner.Generate(seed, totalMeters: 4000);

            Assert.NotNull(plan.FirstWithFeature(RoadFeature.GasStation));
            Assert.True(plan.Features.Count() >= 3, $"seed {seed}: мало точек интереса");
        }
    }

    [Fact]
    public void Features_AreFarApart()
    {
        var plan = RoadPlanner.Generate(seed: 11, totalMeters: 6000);
        var starts = plan.Features.Select(s => s.StartM).ToList();

        for (int i = 1; i < starts.Count; i++)
            Assert.True(starts[i] - starts[i - 1] >= 400, $"точки интереса слишком близко: {starts[i - 1]} и {starts[i]}");
    }

    [Fact]
    public void Curvature_IsBounded_AndSomeSegmentsAreStraight()
    {
        var plan = RoadPlanner.Generate(seed: 3, totalMeters: 5000);

        Assert.All(plan.Segments, s => Assert.InRange(s.Curvature, -0.95, 0.95));
        Assert.Contains(plan.Segments, s => !s.IsCurve);
        Assert.Contains(plan.Segments, s => s.IsCurve);
    }
}
