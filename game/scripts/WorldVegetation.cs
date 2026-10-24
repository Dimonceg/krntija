using System;
using System.Collections.Generic;
using Godot;
using Krntija.Core.World;

namespace Krntija.Game;

/// <summary>Blender vegetation rendered in spatial chunks, never a node per tree.</summary>
public static class WorldVegetation
{
    private readonly record struct Spot(Vector3 Position, float Scale, float Yaw, Color Tint);

    public static bool Scatter(List<Vector3> road, List<float> headings, Node3D parent,
        ulong seed, List<RoadMeshBuilder.PointOfInterest>? landmarks)
    {
        string[] ids = { "tree_oak", "tree_poplar", "tree_pine", "shrub" };
        var meshes = new Mesh[ids.Length];
        for (int i = 0; i < ids.Length; i++)
        {
            var root = ModelLibrary.Prop(ids[i]);
            if (root is null) return false;
            var mesh = FindMesh(root);
            root.Free();
            if (mesh is null) return false;
            meshes[i] = mesh;
        }

        var material = new ShaderMaterial
        {
            Shader = new Shader { Code = @"shader_type spatial;
render_mode cull_back;
void vertex() {
    vec3 world = (MODEL_MATRIX * vec4(VERTEX, 1.0)).xyz;
    float foliage = step(COLOR.r * 1.05, COLOR.g);
    float weight = foliage * clamp(VERTEX.y / 6.0, 0.0, 1.0);
    VERTEX.x += sin(TIME * 1.05 + world.x * 0.17 + world.z * 0.08) * 0.075 * weight;
    VERTEX.z += cos(TIME * 0.8 + world.z * 0.13) * 0.035 * weight;
}
void fragment() {
    ALBEDO = COLOR.rgb;
    ROUGHNESS = 0.95;
}" }
        };

        var rng = new Pcg32(seed, sequence: 99);
        int instanceCount = 0;
        for (int start = 4; start < road.Count - 2; start += 48)
        {
            var groups = new List<Spot>[ids.Length];
            for (int i = 0; i < groups.Length; i++) groups[i] = new List<Spot>();
            int end = Math.Min(start + 48, road.Count - 2);
            for (int i = start; i < end; i += 2)
            {
                var right = new Vector3(Mathf.Cos(headings[i]), 0, Mathf.Sin(headings[i]));
                for (int side = -1; side <= 1; side += 2)
                {
                    // Groves and gaps, rather than equally spaced ornaments.
                    double density = 0.55 + 0.25 * Math.Sin(i * 0.035 + side * 2);
                    if (rng.Chance(density))
                    {
                        float offset = (float)rng.Range(13, 47) * side;
                        Vector3 p = road[i] + right * offset;
                        if (!InClearing(p, landmarks))
                        {
                            p.Y = TerrainBuilder.SurfaceHeight(road[i].Y, offset, p) - 0.08f;
                            int kind = rng.Chance(0.56) ? 0 : rng.Chance(0.5) ? 1 : 2;
                            float s = (float)rng.Range(0.75, 1.35);
                            float tint = (float)rng.Range(0.83, 1.12);
                            groups[kind].Add(new Spot(p, s, (float)rng.Range(0, Mathf.Tau),
                                new Color(tint, tint, tint * 0.94f, 1)));
                        }
                    }
                    if (rng.Chance(0.42))
                    {
                        float offset = (float)rng.Range(8, 25) * side;
                        Vector3 p = road[i] + right * offset;
                        if (InClearing(p, landmarks)) continue;
                        p.Y = TerrainBuilder.SurfaceHeight(road[i].Y, offset, p) - 0.05f;
                        groups[3].Add(new Spot(p, (float)rng.Range(0.55, 1.20),
                            (float)rng.Range(0, Mathf.Tau), Colors.White));
                    }
                }
            }

            for (int kind = 0; kind < ids.Length; kind++)
            {
                var spots = groups[kind];
                if (spots.Count == 0) continue;
                var mm = new MultiMesh
                {
                    TransformFormat = MultiMesh.TransformFormatEnum.Transform3D,
                    UseColors = true,
                    Mesh = meshes[kind],
                    InstanceCount = spots.Count,
                };
                Aabb bounds = default;
                for (int i = 0; i < spots.Count; i++)
                {
                    var spot = spots[i];
                    var basis = new Basis(Vector3.Up, spot.Yaw).Scaled(Vector3.One * spot.Scale);
                    var transform = new Transform3D(basis, spot.Position);
                    mm.SetInstanceTransform(i, transform);
                    mm.SetInstanceColor(i, spot.Tint);
                    var aabb = (transform * meshes[kind].GetAabb()).Grow(0.2f);
                    bounds = i == 0 ? aabb : bounds.Merge(aabb);
                }
                parent.AddChild(new MultiMeshInstance3D
                {
                    Name = $"Vegetation_{start}_{ids[kind]}",
                    Multimesh = mm,
                    MaterialOverride = material,
                    CustomAabb = bounds,
                    VisibilityRangeEnd = kind == 3 ? 150 : 420,
                    VisibilityRangeEndMargin = 20,
                });
                instanceCount += spots.Count;
            }
        }
        GD.Print($"[world-art] vegetation: {instanceCount} instances, four shared meshes");
        return true;
    }

    private static Mesh? FindMesh(Node node)
    {
        if (node is MeshInstance3D mesh) return mesh.Mesh;
        foreach (var child in node.GetChildren())
        {
            var found = FindMesh(child);
            if (found is not null) return found;
        }
        return null;
    }

    private static bool InClearing(Vector3 point, List<RoadMeshBuilder.PointOfInterest>? landmarks)
    {
        if (landmarks is null) return false;
        foreach (var poi in landmarks)
        {
            var right = new Vector3(Mathf.Cos(poi.Heading), 0, Mathf.Sin(poi.Heading));
            var forward = new Vector3(Mathf.Sin(poi.Heading), 0, -Mathf.Cos(poi.Heading));
            Vector3 offset = point - poi.Position;
            float lateral = offset.Dot(right);
            if (lateral is > -37 and < -5 && Mathf.Abs(offset.Dot(forward)) < 24) return true;
        }
        return false;
    }
}
