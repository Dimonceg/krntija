using System;
using System.Collections.Generic;
using Godot;
using Krntija.Core.World;

namespace Krntija.Game;

/// <summary>
/// Карта вокруг трассы: грунтовые съезды, постройки, руины, колодцы и забор
/// по границе. Всё ставится по сиду, поэтому карта воспроизводима.
/// </summary>
public static class WorldMap
{
    public const float BorderHalfWidth = 132f;
    private const float TrackWidth = 4.2f;

    private static readonly Color Wall = new(0.76f, 0.71f, 0.6f);
    private static readonly Color WallOld = new(0.62f, 0.58f, 0.5f);
    private static readonly Color Roof = new(0.45f, 0.24f, 0.19f);
    private static readonly Color RoofOld = new(0.35f, 0.3f, 0.27f);
    private static readonly Color Wood = new(0.42f, 0.31f, 0.2f);
    private static readonly Color Dirt = new(0.5f, 0.44f, 0.33f);
    private static readonly Color Stone = new(0.55f, 0.53f, 0.5f);

    /// <summary>Точка на обочине: индекс центральной линии, сторона и отступ.</summary>
    public readonly record struct Spot(int Index, float Side, float Lateral, Vector3 Position, float Heading);

    public static List<Place> Build(List<Vector3> centerline, List<float> headings, Node3D parent, ulong seed)
    {
        var rng = new Pcg32(seed + 4, sequence: 17);
        var spots = PlanSpots(centerline, headings, rng);

        foreach (var spot in spots)
        {
            BuildTrack(centerline, headings, parent, spot);

            switch (spot.Kind)
            {
                case "house": BuildHouse(parent, spot, rng, abandoned: false); break;
                case "abandoned": BuildHouse(parent, spot, rng, abandoned: true); break;
                case "barn": BuildBarn(parent, spot, rng); break;
                case "ruins": BuildRuins(parent, spot, rng); break;
                case "well": BuildWell(parent, spot, rng); break;
                case "shed": BuildShed(parent, spot, rng); break;
            }
        }

        BuildBorder(centerline, headings, parent, rng);
        GD.Print($"[map] объектов: {spots.Count}, граница ±{BorderHalfWidth:F0} м");

        int middle = centerline.Count / 2;
        Place nearest = spots[0];
        foreach (var spot in spots)
            if (Math.Abs(spot.Index - middle) < Math.Abs(nearest.Index - middle)) nearest = spot;
        GD.Print($"[map] у середины: {nearest.Kind} на индексе {nearest.Index}");

        return spots;
    }

    public readonly record struct Place(int Index, float Side, float Lateral, Vector3 Position, float Heading, string Kind);

    private static List<Place> PlanSpots(List<Vector3> centerline, List<float> headings, Pcg32 rng)
    {
        string[] kinds = { "house", "abandoned", "barn", "ruins", "shed", "well" };
        double[] weights = { 3, 3, 2, 3, 2, 1 };

        var result = new List<Place>();
        int count = Math.Max(10, centerline.Count / 22);   // примерно каждые 88 м

        for (int k = 0; k < count; k++)
        {
            int index = Mathf.Clamp(10 + k * (centerline.Count - 20) / count + rng.NextInt(8) - 4,
                6, centerline.Count - 6);
            float side = rng.Chance(0.5) ? 1f : -1f;
            float lateral = (float)rng.Range(34, 112);

            string kind = PickWeighted(kinds, weights, rng);
            result.Add(new Place(index, side, lateral, PointAt(centerline, headings, index, side, lateral),
                headings[index], kind));
        }

        return result;
    }

    private static string PickWeighted(string[] kinds, double[] weights, Pcg32 rng)
    {
        double total = 0;
        foreach (double weight in weights) total += weight;

        double pick = rng.Range(0, total);
        for (int i = 0; i < kinds.Length; i++)
        {
            pick -= weights[i];
            if (pick <= 0) return kinds[i];
        }

        return kinds[^1];
    }

    /// <summary>Точка сбоку от дороги на нужном отступе, уже посаженная на рельеф.</summary>
    public static Vector3 PointAt(List<Vector3> centerline, List<float> headings, int index, float side, float lateral)
    {
        var right = new Vector3(Mathf.Cos(headings[index]), 0, Mathf.Sin(headings[index]));
        Vector3 point = centerline[index] + right * (side * lateral);
        point.Y = TerrainBuilder.SurfaceHeight(centerline[index].Y, side * lateral, point);
        return point;
    }

    // ------------------------------------------------------------ съезды

    private static void BuildTrack(List<Vector3> centerline, List<float> headings, Node3D parent, Place place)
    {
        Vector3 start = PointAt(centerline, headings, place.Index, place.Side, 5.5f);
        Vector3 end = place.Position;
        Vector3 delta = end - start;
        delta.Y = 0;

        float length = delta.Length();
        if (length < 6f) return;

        Vector3 direction = delta / length;
        float yaw = Mathf.Atan2(direction.X, direction.Z);

        var track = new MeshInstance3D
        {
            Mesh = new BoxMesh { Size = new Vector3(TrackWidth, 0.12f, length) },
            Position = start + direction * (length / 2) + Vector3.Up * 0.04f,
            Rotation = new Vector3(0, yaw, 0),
            MaterialOverride = new StandardMaterial3D { AlbedoColor = Dirt, Roughness = 0.98f },
        };
        parent.AddChild(track);
    }

    // ------------------------------------------------------------ постройки

    private static void BuildHouse(Node3D parent, Place place, Pcg32 rng, bool abandoned)
    {
        var root = Group(parent, place, abandoned ? "Abandoned" : "House");
        float width = (float)rng.Range(7.5, 9.5);
        float depth = (float)rng.Range(5.5, 7.0);
        float height = 3.1f;
        Color wall = abandoned ? WallOld : Wall;

        Box(root, new Vector3(width, height, 0.3f), new Vector3(0, height / 2, -depth / 2), wall);
        Box(root, new Vector3(width, height, 0.3f), new Vector3(0, height / 2, depth / 2), wall);

        // Проём вместо двери: стена собирается из двух кусков.
        float door = 1.2f;
        float half = width / 2;
        Box(root, new Vector3(half - door / 2, height, 0.3f), new Vector3(-(door / 2 + (half - door / 2) / 2), height / 2, 0), wall);
        Box(root, new Vector3(half - door / 2, height, 0.3f), new Vector3(door / 2 + (half - door / 2) / 2, height / 2, 0), wall);

        Box(root, new Vector3(0.3f, height, depth), new Vector3(-width / 2, height / 2, 0), wall);
        Box(root, new Vector3(0.3f, height, depth), new Vector3(width / 2, height / 2, 0), wall);

        Roof2(root, width + 0.7f, depth + 0.7f, height, abandoned ? RoofOld : Roof);

        if (abandoned)
        {
            // Обвал: кусок стены лежит рядом.
            Box(root, new Vector3(width * 0.35f, 0.35f, 0.4f),
                new Vector3(width * 0.2f, 0.2f, depth * 0.75f), wall, rotateY: (float)rng.Range(-0.4, 0.4));
        }
    }

    private static void BuildBarn(Node3D parent, Place place, Pcg32 rng)
    {
        var root = Group(parent, place, "Barn");
        float width = (float)rng.Range(9f, 12f);
        float depth = (float)rng.Range(6f, 8f);
        float height = 4.2f;

        Box(root, new Vector3(width, height, 0.3f), new Vector3(0, height / 2, -depth / 2), Wood);
        Box(root, new Vector3(width, height, 0.3f), new Vector3(0, height / 2, depth / 2), Wood);
        Box(root, new Vector3(0.3f, height, depth), new Vector3(-width / 2, height / 2, 0), Wood);
        Box(root, new Vector3(0.3f, height, depth), new Vector3(width / 2, height / 2, 0), Wood);
        Roof2(root, width + 0.8f, depth + 0.8f, height, RoofOld);
    }

    private static void BuildRuins(Node3D parent, Place place, Pcg32 rng)
    {
        var root = Group(parent, place, "Ruins");
        int walls = 2 + rng.NextInt(3);

        for (int i = 0; i < walls; i++)
        {
            float length = (float)rng.Range(2.5f, 6.5f);
            float height = (float)rng.Range(0.8f, 2.4f);
            float yaw = (float)rng.Range(-0.5, 0.5) + i * 0.9f;
            Box(root, new Vector3(length, height, 0.4f),
                new Vector3((float)rng.Range(-2.5, 2.5), height / 2, (float)rng.Range(-2.5, 2.5)), Stone, rotateY: yaw);
        }
    }

    private static void BuildShed(Node3D parent, Place place, Pcg32 rng)
    {
        var root = Group(parent, place, "Shed");
        float width = (float)rng.Range(2.8f, 3.8f);
        float depth = (float)rng.Range(2.4f, 3.2f);
        float height = 2.2f;

        Box(root, new Vector3(width, height, 0.25f), new Vector3(0, height / 2, -depth / 2), Wood);
        Box(root, new Vector3(0.25f, height, depth), new Vector3(-width / 2, height / 2, 0), Wood);
        Box(root, new Vector3(0.25f, height, depth), new Vector3(width / 2, height / 2, 0), Wood);
        Box(root, new Vector3(width + 0.5f, 0.2f, depth + 0.5f), new Vector3(0, height + 0.1f, 0), RoofOld);
    }

    private static void BuildWell(Node3D parent, Place place, Pcg32 rng)
    {
        var root = Group(parent, place, "Well");
        float radius = 1.1f;

        var ring = new MeshInstance3D
        {
            Mesh = new CylinderMesh { Height = 0.9f, TopRadius = radius, BottomRadius = radius },
            Position = new Vector3(0, 0.45f, 0),
            MaterialOverride = new StandardMaterial3D { AlbedoColor = Stone, Roughness = 0.95f },
        };
        root.AddChild(ring);
        Solid(root, new Vector3(radius * 2, 0.9f, radius * 2), new Vector3(0, 0.45f, 0));

        // Крыша-навес на двух столбах.
        for (int i = -1; i <= 1; i += 2)
            Box(root, new Vector3(0.15f, 2.1f, 0.15f), new Vector3(i * (radius + 0.1f), 1.05f, 0), Wood);
        Box(root, new Vector3(radius * 2 + 0.6f, 0.15f, radius * 2 + 0.6f),
            new Vector3(0, 2.2f, 0), RoofOld, rotateY: (float)rng.Range(-0.1, 0.1));
    }

    // ------------------------------------------------------------ граница

    private static void BuildBorder(List<Vector3> centerline, List<float> headings, Node3D parent, Pcg32 rng)
    {
        var root = new Node3D { Name = "MapBorder" };
        parent.AddChild(root);

        // Выезды за карту: закрыты, но видно, что они есть.
        int[] gateIndices = { 14, centerline.Count / 3, centerline.Count / 2, centerline.Count - 16 };

        for (int side = -1; side <= 1; side += 2)
        {
            for (int i = 6; i < centerline.Count - 6; i += 6)
            {
                Vector3 post = PointAt(centerline, headings, i, side, BorderHalfWidth);

                bool gate = System.Array.Exists(gateIndices, index => Math.Abs(index - i) < 6);

                if (gate)
                {
                    if (Math.Abs(Array.IndexOf(gateIndices, i) % 2) == 0)
                        BuildGate(root, post, headings[i], side);
                    continue;
                }

                root.AddChild(new MeshInstance3D
                {
                    Mesh = new BoxMesh { Size = new Vector3(0.18f, 1.5f, 0.18f) },
                    Position = post + Vector3.Up * 0.75f,
                    MaterialOverride = new StandardMaterial3D { AlbedoColor = Wood, Roughness = 0.9f },
                });

                Vector3 next = PointAt(centerline, headings, Mathf.Min(i + 6, centerline.Count - 1), side, BorderHalfWidth);
                Vector3 mid = (post + next) / 2;
                float length = (next - post).Length();
                if (length < 0.5f) continue;

                root.AddChild(new MeshInstance3D
                {
                    Mesh = new BoxMesh { Size = new Vector3(0.1f, 0.1f, length) },
                    Position = mid + Vector3.Up * 1.2f,
                    Rotation = new Vector3(0, Mathf.Atan2(next.X - post.X, next.Z - post.Z), 0),
                    MaterialOverride = new StandardMaterial3D { AlbedoColor = Wood, Roughness = 0.9f },
                });
            }
        }
    }

    private static void BuildGate(Node3D parent, Vector3 position, float heading, float side)
    {
        var root = new Node3D
        {
            Name = "ClosedExit",
            Position = position,
            Rotation = new Vector3(0, -heading + Mathf.Pi / 2, 0),
        };
        parent.AddChild(root);

        var metal = new StandardMaterial3D { AlbedoColor = new Color(0.55f, 0.2f, 0.18f), Metallic = 0.4f, Roughness = 0.6f };
        var wood = new StandardMaterial3D { AlbedoColor = Wood, Roughness = 0.9f };

        // Два столба и шлагбаум поперёк выезда — проезд закрыт до будущих времён.
        for (int i = -1; i <= 1; i += 2)
            root.AddChild(new MeshInstance3D
            {
                Mesh = new BoxMesh { Size = new Vector3(0.25f, 2.2f, 0.25f) },
                Position = new Vector3(i * 2.6f, 1.1f, 0),
                MaterialOverride = wood,
            });

        root.AddChild(new MeshInstance3D
        {
            Mesh = new BoxMesh { Size = new Vector3(5.4f, 0.18f, 0.18f) },
            Position = new Vector3(0, 1.9f, 0),
            MaterialOverride = metal,
        });

        Solid(root, new Vector3(5.6f, 2.6f, 0.5f), new Vector3(0, 1.3f, 0));
    }

    // ------------------------------------------------------------ утилиты

    private static Node3D Group(Node3D parent, Place place, string name)
    {
        var root = new Node3D
        {
            Name = name,
            Position = place.Position,
            Rotation = new Vector3(0, -place.Heading + (float)(place.Index % 3) * 0.3f, 0),
        };
        parent.AddChild(root);
        return root;
    }

    private static void Roof2(Node3D parent, float width, float depth, float height, Color color)
    {
        var material = new StandardMaterial3D { AlbedoColor = color, Roughness = 0.9f };
        float rise = 1.1f;
        float half = depth / 2;

        for (int side = -1; side <= 1; side += 2)
        {
            float slope = Mathf.Sqrt(half * half + (rise * 0.5f) * (rise * 0.5f));
            var slab = new MeshInstance3D
            {
                Mesh = new BoxMesh { Size = new Vector3(width, 0.16f, slope * 2f) },
                Position = new Vector3(0, height + rise * 0.25f, side * half * 0.5f),
                MaterialOverride = material,
            };
            slab.Rotation = new Vector3(side * Mathf.Atan2(rise * 0.5f, half), 0, 0);
            parent.AddChild(slab);
        }
    }

    private static void Box(Node3D parent, Vector3 size, Vector3 position, Color color, float rotateY = 0f)
    {
        var mesh = new MeshInstance3D
        {
            Mesh = new BoxMesh { Size = size },
            Position = position,
            Rotation = new Vector3(0, rotateY, 0),
            MaterialOverride = new StandardMaterial3D { AlbedoColor = color, Roughness = 0.94f },
        };
        parent.AddChild(mesh);
        Solid(parent, size, position, rotateY);
    }

    private static void Solid(Node3D parent, Vector3 size, Vector3 position, float rotateY = 0f)
    {
        var body = new StaticBody3D
        {
            Position = position,
            Rotation = new Vector3(0, rotateY, 0),
        };
        body.AddChild(new CollisionShape3D { Shape = new BoxShape3D { Size = size } });
        parent.AddChild(body);
    }
}
