using System.Collections.Generic;
using Godot;

namespace Krntija.Game;

/// <summary>Small graded plots keep buildings and forecourts on the same ground as physics.</summary>
public static class WorldArtGround
{
    private readonly record struct Plot(Vector3 Center, Vector3 RoadCenter, Basis Basis, Vector2 Extent);
    private static readonly List<Plot> Plots = new();

    public static void Configure(List<RoadMeshBuilder.PointOfInterest> points)
    {
        Plots.Clear();
        foreach (var poi in points)
        {
            var basis = new Basis(Vector3.Up, -poi.Heading);
            // The rightmost hard edge remains at least 6 m from the road centre.
            bool village = poi.Segment.Feature == Krntija.Core.World.RoadFeature.Village;
            Vector3 offset = village ? new Vector3(-20, 0, 1) : new Vector3(-16, 0, -2);
            Vector2 extent = village ? new Vector2(14, 18) : new Vector2(10, 16);
            Plots.Add(new Plot(poi.Position + basis * offset, poi.Position, basis, extent));
        }
    }

    public static float HeightAt(float naturalHeight, Vector3 world)
    {
        foreach (var plot in Plots)
        {
            Vector3 local = plot.Basis.Inverse() * (world - plot.Center);
            float lateral = (plot.Basis.Inverse() * (world - plot.RoadCenter)).X;
            if (lateral >= -5.5f) continue;
            float outside = Mathf.Max(Mathf.Abs(local.X) - plot.Extent.X,
                Mathf.Abs(local.Z) - plot.Extent.Y);
            if (outside >= 3) continue;
            // Do not grade the asphalt or its immediate shoulder.
            float weight = (1 - Mathf.SmoothStep(0, 3, Mathf.Max(outside, 0)))
                * Mathf.SmoothStep(0, 2, -lateral - 5.5f);
            naturalHeight = Mathf.Lerp(naturalHeight, plot.Center.Y, weight);
        }
        return naturalHeight;
    }

    public static bool IsPlot(Vector3 world, float margin = 0)
    {
        foreach (var plot in Plots)
        {
            var p = plot.Basis.Inverse() * (world - plot.Center);
            if (Mathf.Abs(p.X) < plot.Extent.X + margin && Mathf.Abs(p.Z) < plot.Extent.Y + margin) return true;
        }
        return false;
    }
}
