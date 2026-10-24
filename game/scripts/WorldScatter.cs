using System;
using System.Collections.Generic;
using Godot;
using Krntija.Core.World;

namespace Krntija.Game;

/// <summary>
/// Одевает обочины: деревья и столбы ЛЭП. Всё через MultiMesh, чтобы
/// сотни объектов не превращались в сотни узлов.
/// </summary>
public static class WorldScatter
{
    /// <summary>
    /// Деревья кусками вдоль дороги: у каждого куска свой AABB, поэтому
    /// движок отсекает то, что за спиной или далеко впереди.
    /// </summary>
    public static void ScatterTrees(List<Vector3> centerline, List<float> headings, Node3D parent, ulong seed,
        List<RoadMeshBuilder.PointOfInterest>? landmarks = null)
    {
        if (WorldVegetation.Scatter(centerline, headings, parent, seed, landmarks)) return;

        const int chunkPoints = 48;

        var rng = new Pcg32(seed, sequence: 99);
        var trunkMesh = new CylinderMesh { TopRadius = 0.13f, BottomRadius = 0.22f, Height = 3.0f, RadialSegments = 6 };
        var pineCrown = BuildPineCrown();
        var roundCrown = new SphereMesh { Radius = 1.6f, Height = 2.7f, RadialSegments = 8, Rings = 5 };

        for (int start = 4; start < centerline.Count - 2; start += chunkPoints)
        {
            int end = Math.Min(start + chunkPoints, centerline.Count);
            var spots = new List<(Vector3 Position, float Scale, float Yaw, bool Pine)>();
            var bounds = new Aabb();

            for (int i = start; i < end; i += 2)
            {
                for (int side = -1; side <= 1; side += 2)
                {
                    if (!rng.Chance(0.62)) continue;

                    float offset = (float)rng.Range(14, 42);
                    var right = new Vector3(Mathf.Cos(headings[i]), 0, Mathf.Sin(headings[i]));
                    Vector3 tree = centerline[i] + right * offset * side;
                    tree.Y = TerrainBuilder.SurfaceHeight(centerline[i].Y, offset * side, tree);

                    spots.Add((tree, (float)rng.Range(0.65, 1.15), (float)rng.Range(0, Mathf.Tau), rng.Chance(0.62)));

                    var padded = new Aabb(tree - new Vector3(9, 2, 9), new Vector3(18, 16, 18));
                    bounds = bounds.Size == Vector3.Zero ? padded : bounds.Merge(padded);
                }
            }

            if (spots.Count == 0) continue;

            int pineCount = 0;
            foreach (var spot in spots)
                if (spot.Pine) pineCount++;

            var trunks = new MultiMesh { TransformFormat = MultiMesh.TransformFormatEnum.Transform3D, Mesh = trunkMesh, InstanceCount = spots.Count };
            var pines = new MultiMesh { TransformFormat = MultiMesh.TransformFormatEnum.Transform3D, Mesh = pineCrown, InstanceCount = pineCount };
            var rounds = new MultiMesh { TransformFormat = MultiMesh.TransformFormatEnum.Transform3D, Mesh = roundCrown, InstanceCount = spots.Count - pineCount };

            int pineIndex = 0;
            int roundIndex = 0;

            for (int i = 0; i < spots.Count; i++)
            {
                var (position, scale, yaw, pine) = spots[i];
                var basis = new Basis(Vector3.Up, yaw).Scaled(new Vector3(scale, scale, scale));

                trunks.SetInstanceTransform(i, new Transform3D(basis, position + Vector3.Up * (1.5f * scale)));

                if (pine)
                {
                    pines.SetInstanceTransform(pineIndex++, new Transform3D(basis, position + Vector3.Up * (1.4f * scale)));
                }
                else
                {
                    rounds.SetInstanceTransform(roundIndex++, new Transform3D(basis, position + Vector3.Up * (3.1f * scale)));
                }
            }

            AddChunk(parent, trunks, new StandardMaterial3D { AlbedoColor = new Color(0.29f, 0.22f, 0.15f), Roughness = 1f }, bounds);

            if (pineCount > 0)
                AddChunk(parent, pines, SwayMaterial(new Color(0.17f, 0.25f, 0.15f)), bounds);

            if (roundIndex > 0)
                AddChunk(parent, rounds, SwayMaterial(new Color(0.27f, 0.34f, 0.17f)), bounds);
        }
    }

    private static void AddChunk(Node3D parent, MultiMesh mesh, Material material, Aabb bounds)
    {
        parent.AddChild(new MultiMeshInstance3D
        {
            Multimesh = mesh,
            MaterialOverride = material,
            CustomAabb = bounds,
        });
    }

    /// <summary>Материал кроны с лёгким покачиванием на ветру (вершинный шейдер).</summary>
    private static ShaderMaterial SwayMaterial(Color color)
    {
        var shader = new Shader
        {
            Code = @"shader_type spatial;
render_mode cull_disabled;
uniform vec4 tint : source_color = vec4(0.3, 0.4, 0.2, 1.0);
uniform float amount = 0.035;

void vertex() {
    vec3 world = (MODEL_MATRIX * vec4(VERTEX, 1.0)).xyz;
    float h = max(VERTEX.y, 0.0);
    float sway = sin(TIME * 1.1 + world.x * 0.3 + world.z * 0.21);
    VERTEX.x += sway * amount * h;
    VERTEX.z += cos(TIME * 0.85 + world.x * 0.25) * amount * 0.55 * h;
}

void fragment() {
    ALBEDO = tint.rgb;
    ROUGHNESS = 1.0;
}",
        };

        var material = new ShaderMaterial { Shader = shader };
        material.SetShaderParameter("tint", color);
        return material;
    }

    /// <summary>Ёлка из трёх конусов — читается как хвойное дерево, а не как конус.</summary>
    private static ArrayMesh BuildPineCrown()
    {
        var tool = new SurfaceTool();
        tool.Begin(Mesh.PrimitiveType.Triangles);

        AppendCone(tool, radius: 1.75f, height: 2.6f, y: 3.2f);
        AppendCone(tool, radius: 1.35f, height: 2.2f, y: 4.6f);
        AppendCone(tool, radius: 0.95f, height: 1.9f, y: 6.0f);

        return tool.Commit();
    }

    private static void AppendCone(SurfaceTool tool, float radius, float height, float y)
    {
        var cone = new CylinderMesh
        {
            TopRadius = 0f,
            BottomRadius = radius,
            Height = height,
            RadialSegments = 7,
        };

        tool.AppendFrom(cone, 0, new Transform3D(Basis.Identity, new Vector3(0, y, 0)));
    }

    public static void ScatterPowerLine(List<Vector3> centerline, List<float> headings, Node3D parent, ulong seed)
    {
        var rng = new Pcg32(seed, sequence: 123);
        int side = rng.Chance(0.5) ? 1 : -1;

        var poles = new List<Vector3>();
        for (int i = 12; i < centerline.Count; i += 14)
        {
            var right = new Vector3(Mathf.Cos(headings[i]), 0, Mathf.Sin(headings[i]));
            Vector3 pole = centerline[i] + right * (13f * side);
            pole.Y = TerrainBuilder.SurfaceHeight(centerline[i].Y, 13f * side, pole);
            poles.Add(pole);
        }

        if (poles.Count == 0) return;

        var poleMesh = new CylinderMesh { TopRadius = 0.12f, BottomRadius = 0.2f, Height = 7f, RadialSegments = 5 };
        var mm = new MultiMesh { TransformFormat = MultiMesh.TransformFormatEnum.Transform3D, Mesh = poleMesh, InstanceCount = poles.Count };

        for (int i = 0; i < poles.Count; i++)
            mm.SetInstanceTransform(i, new Transform3D(Basis.Identity, poles[i] + Vector3.Up * 3.5f));

        parent.AddChild(new MultiMeshInstance3D
        {
            Multimesh = mm,
            MaterialOverride = new StandardMaterial3D { AlbedoColor = new Color(0.36f, 0.31f, 0.26f), Roughness = 1f },
        });

        // Провода: тонкие цилиндры между соседними столбами.
        var wireMaterial = new StandardMaterial3D { AlbedoColor = new Color(0.08f, 0.08f, 0.08f), Roughness = 1f };
        for (int i = 0; i < poles.Count - 1; i++)
        {
            for (int wire = -1; wire <= 1; wire += 2)
            {
                Vector3 from = poles[i] + Vector3.Up * 6.6f + Vector3.Right * (0.45f * wire);
                Vector3 to = poles[i + 1] + Vector3.Up * 6.6f + Vector3.Right * (0.45f * wire);
                AddWire(parent, from, to, wireMaterial);
            }
        }
    }

    /// <summary>Километровые столбики с цифрами: по ним видно, сколько проехал.</summary>
    public static void AddKilometerPosts(List<Vector3> centerline, List<float> headings, Node3D parent)
    {
        const int everyPoints = 50; // 50 * 4 м = 200 м
        var material = new StandardMaterial3D { AlbedoColor = new Color(0.88f, 0.87f, 0.82f), Roughness = 0.9f };

        for (int i = everyPoints; i < centerline.Count; i += everyPoints)
        {
            Vector3 right = new Vector3(Mathf.Cos(headings[i]), 0, Mathf.Sin(headings[i]));
            Vector3 position = centerline[i] + right * (RoadMeshBuilder.Width * 0.5f + 1.1f);

            parent.AddChild(new MeshInstance3D
            {
                Mesh = new BoxMesh { Size = new Vector3(0.18f, 1.1f, 0.18f) },
                Position = position + Vector3.Up * 0.55f,
                MaterialOverride = material,
            });

            parent.AddChild(new Label3D
            {
                Text = $"{i * 4 / 1000.0:F1}",
                FontSize = 72,
                PixelSize = 0.0032f,
                Position = position + Vector3.Up * 1.25f,
                Billboard = BaseMaterial3D.BillboardModeEnum.Enabled,
                Modulate = new Color(0.95f, 0.94f, 0.88f),
                OutlineSize = 6,
            });
        }
    }

    /// <summary>Дальние холмы за пределами полосы террейна: горизонт не плоский.</summary>
    public static void ScatterHills(List<Vector3> centerline, List<float> headings, Node3D parent, ulong seed)
    {
        var rng = new Pcg32(seed, sequence: 77);
        var spots = new List<(Vector3 Position, Vector3 Scale, float Yaw)>();

        for (int i = 8; i < centerline.Count; i += 14)
        {
            for (int side = -1; side <= 1; side += 2)
            {
                if (!rng.Chance(0.55)) continue;

                float offset = (float)rng.Range(150, 330);
                var right = new Vector3(Mathf.Cos(headings[i]), 0, Mathf.Sin(headings[i]));
                Vector3 hill = centerline[i] + right * offset * side;

                float edge = TerrainBuilder.SurfaceHeight(centerline[i].Y, TerrainBuilder.HalfWidth, hill);
                hill.Y = edge - 14f;

                spots.Add((
                    hill,
                    new Vector3((float)rng.Range(70, 160), (float)rng.Range(16, 40), (float)rng.Range(70, 160)),
                    (float)rng.Range(0, Mathf.Tau)));
            }
        }

        if (spots.Count == 0) return;

        var sphere = new SphereMesh { Radius = 1f, Height = 2f, RadialSegments = 10, Rings = 6 };
        var mm = new MultiMesh
        {
            TransformFormat = MultiMesh.TransformFormatEnum.Transform3D,
            Mesh = sphere,
            InstanceCount = spots.Count,
        };

        for (int i = 0; i < spots.Count; i++)
        {
            var (position, scale, yaw) = spots[i];
            var basis = new Basis(Vector3.Up, yaw).Scaled(scale);
            mm.SetInstanceTransform(i, new Transform3D(basis, position));
        }

        parent.AddChild(new MultiMeshInstance3D
        {
            Multimesh = mm,
            MaterialOverride = new StandardMaterial3D { AlbedoColor = new Color(0.22f, 0.28f, 0.20f), Roughness = 1f },
        });
    }

    /// <summary>Дорожные знаки: столбик и табличка, иногда круглый ограничитель.</summary>
    public static void ScatterSigns(List<Vector3> centerline, List<float> headings, Node3D parent, ulong seed)
    {
        var rng = new Pcg32(seed, sequence: 91);
        var postMesh = new CylinderMesh { TopRadius = 0.05f, BottomRadius = 0.06f, Height = 2.2f, RadialSegments = 6 };
        var plateMesh = new BoxMesh { Size = new Vector3(0.9f, 0.62f, 0.05f) };
        var roundMesh = new CylinderMesh { TopRadius = 0.42f, BottomRadius = 0.42f, Height = 0.06f, RadialSegments = 16 };

        var postMaterial = new StandardMaterial3D { AlbedoColor = new Color(0.55f, 0.55f, 0.55f), Roughness = 0.7f, Metallic = 0.4f };
        var plateMaterial = new StandardMaterial3D { AlbedoColor = new Color(0.86f, 0.83f, 0.7f), Roughness = 0.6f };
        var redMaterial = new StandardMaterial3D { AlbedoColor = new Color(0.72f, 0.16f, 0.13f), Roughness = 0.5f };

        for (int i = 20; i < centerline.Count - 4; i += 34)
        {
            if (!rng.Chance(0.7)) continue;

            int side = rng.Chance(0.5) ? 1 : -1;
            var right = new Vector3(Mathf.Cos(headings[i]), 0, Mathf.Sin(headings[i]));
            Vector3 spot = centerline[i] + right * (RoadMeshBuilder.Width * 0.5f + 1.4f) * side;
            spot.Y = TerrainBuilder.SurfaceHeight(centerline[i].Y, 6f * side, spot);

            var root = new Node3D { Position = spot, Rotation = new Vector3(0, -headings[i], 0) };
            parent.AddChild(root);

            root.AddChild(new MeshInstance3D
            {
                Mesh = postMesh,
                Position = new Vector3(0, 1.1f, 0),
                MaterialOverride = postMaterial,
            });

            bool round = rng.Chance(0.4);
            root.AddChild(new MeshInstance3D
            {
                Mesh = round ? roundMesh : plateMesh,
                Position = new Vector3(0, 2.1f, 0),
                Rotation = round ? new Vector3(Mathf.Pi / 2, 0, 0) : Vector3.Zero,
                MaterialOverride = round ? redMaterial : plateMaterial,
            });
        }
    }

    private static void AddWire(Node3D parent, Vector3 from, Vector3 to, Material material)
    {
        float length = from.DistanceTo(to);
        if (length < 0.1f) return;

        var wire = new MeshInstance3D
        {
            Mesh = new CylinderMesh { TopRadius = 0.03f, BottomRadius = 0.03f, Height = length, RadialSegments = 4 },
            MaterialOverride = material,
        };

        parent.AddChild(wire);

        Vector3 direction = (to - from).Normalized();
        Vector3 middle = (from + to) * 0.5f;

        // Цилиндр растёт вдоль +Y, поэтому поворачиваем его на направление провода.
        var basis = Basis.Identity;
        if (Math.Abs(direction.Dot(Vector3.Up)) < 0.999f)
        {
            Vector3 axis = Vector3.Up.Cross(direction).Normalized();
            float angle = Vector3.Up.AngleTo(direction);
            basis = new Basis(axis, angle);
        }

        wire.Transform = new Transform3D(basis, middle);
    }
}
