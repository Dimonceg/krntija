using System;
using System.Collections.Generic;
using Godot;
using Krntija.Core.World;

namespace Krntija.Game;

/// <summary>
/// Глубинка за трассой: сеть грунтовых дорог, поселение, интерьеры и события.
/// Всё по сиду, поэтому карта одинаковая у всех и в тестах.
/// </summary>
public static class BackcountryMap
{
    private static readonly Color Wood = new(0.42f, 0.31f, 0.2f);
    private static readonly Color Stone = new(0.55f, 0.53f, 0.5f);
    private static readonly Color Dirt = new(0.5f, 0.44f, 0.33f);
    private static readonly Color Rust = new(0.45f, 0.26f, 0.18f);
    private static readonly Color Paint = new(0.35f, 0.42f, 0.3f);

    /// <summary>Построить всё и вернуть данные для игры и миникарты.</summary>
    public static MapResult Build(List<Vector3> centerline, List<float> headings, Node3D parent, ulong seed)
    {
        var rng = new Pcg32(seed + 77, sequence: 23);
        var result = new MapResult();

        BuildBranchNetwork(centerline, headings, parent, rng, result);
        BuildSettlement(centerline, headings, parent, rng, result);
        BuildEvents(centerline, headings, parent, rng, result);

        GD.Print($"[map] ветки: {result.Branches.Count}, мест: {result.Places.Count}, "
                 + $"взаимодействий: {result.Uses.Count}, выездов: {result.ClosedExits.Count}");

        return result;
    }

    // ------------------------------------------------------------ сеть дорог

    private static void BuildBranchNetwork(List<Vector3> centerline, List<float> headings, Node3D parent,
        Pcg32 rng, MapResult result)
    {
        // Три развилки: каждая уходит в сторону и вьётся до тупика.
        int[] starts = { centerline.Count / 5, centerline.Count / 2, centerline.Count - centerline.Count / 5 };

        for (int b = 0; b < starts.Length; b++)
        {
            int index = Mathf.Clamp(starts[b], 8, centerline.Count - 8);
            float side = b % 2 == 0 ? 1f : -1f;

            var path = BuildBranch(centerline, headings, parent, rng, index, side, out float length);
            if (path.Count < 3) continue;
            result.Branches.Add(path);

            // Два объекта вдоль ветки и тупик в конце.
            for (int k = 1; k <= 2; k++)
            {
                Vector3 point = path[Mathf.Clamp(path.Count * k / 3, 1, path.Count - 1)];
                float yaw = headings[index] + (float)rng.Range(-1, 1);
                string kind = k == 1 ? "house" : (rng.Chance(0.5) ? "barn" : "ruins");
                BuildStructure(parent, point, yaw, kind, rng, result: result);
                result.Places.Add(new MapPlace(index, side, point, kind, LabelFor(kind)));
                result.Uses.Add(new MapUse(point + new Vector3(0, 0.2f, 2.2f), "loot", LootFor(kind), 1));
            }

            Vector3 deadEnd = path[^1];
            BuildDeadEnd(parent, deadEnd, rng);
            result.Places.Add(new MapPlace(index, side, deadEnd, "dead_end", "Тупик"));
        }
    }

    /// <summary>Вьющийся грунтовый съезд длиной 260–420 м; возвращает осевую линию.</summary>
    private static List<Vector3> BuildBranch(List<Vector3> centerline, List<float> headings, Node3D parent,
        Pcg32 rng, int index, float side, out float length)
    {
        var path = new List<Vector3>();
        Vector3 start = RoadsideMap.PointAt(centerline, headings, index, side, 6f);
        float heading = headings[index] + side * Mathf.Pi / 2f;

        Vector3 position = start;
        float target = (float)rng.Range(260, 420);
        length = target;

        while (path.Count * 8f < target)
        {
            path.Add(position);
            heading += (float)rng.Range(-0.13, 0.13);

            var direction = new Vector3(Mathf.Sin(heading), 0, -Mathf.Cos(heading));
            position += direction * 8f;
            position.Y = TerrainBuilder.SurfaceHeight(centerline[index].Y, 80f, position);
        }

        for (int i = 0; i < path.Count - 1; i++)
        {
            Vector3 a = path[i];
            Vector3 b = path[i + 1];
            Vector3 delta = b - a;
            float segment = delta.Length();
            if (segment < 0.5f) continue;

            parent.AddChild(new MeshInstance3D
            {
                Mesh = new BoxMesh { Size = new Vector3(5.6f, 0.12f, segment + 0.4f) },
                Position = (a + b) / 2 + Vector3.Up * 0.04f,
                Rotation = new Vector3(0, Mathf.Atan2(delta.X, delta.Z), 0),
                MaterialOverride = new StandardMaterial3D { AlbedoColor = Dirt, Roughness = 0.98f },
            });
        }

        return path;
    }

    private static void BuildDeadEnd(Node3D parent, Vector3 position, Pcg32 rng)
    {
        var root = new Node3D { Name = "DeadEnd", Position = position };
        parent.AddChild(root);

        // Насыпь и пара столбов: видно, что дальше не проехать.
        root.AddChild(new MeshInstance3D
        {
            Mesh = new BoxMesh { Size = new Vector3(7f, 0.7f, 2.2f) },
            Position = new Vector3(0, 0.35f, 0),
            MaterialOverride = new StandardMaterial3D { AlbedoColor = Dirt, Roughness = 1f },
        });

        for (int i = -1; i <= 1; i += 2)
            root.AddChild(new MeshInstance3D
            {
                Mesh = new BoxMesh { Size = new Vector3(0.18f, 1.4f, 0.18f) },
                Position = new Vector3(i * 2.6f, 0.7f, 0),
                MaterialOverride = new StandardMaterial3D { AlbedoColor = Wood, Roughness = 0.9f },
            });

        var body = new StaticBody3D { Name = "DeadEndBody" };
        body.AddChild(new CollisionShape3D
        {
            Shape = new BoxShape3D { Size = new Vector3(7f, 1.6f, 2.2f) },
            Position = new Vector3(0, 0.8f, 0),
        });
        root.AddChild(body);
    }

    // ------------------------------------------------------------ поселение

    private static void BuildSettlement(List<Vector3> centerline, List<float> headings, Node3D parent,
        Pcg32 rng, MapResult result)
    {
        int index = centerline.Count * 2 / 5;
        float side = -1f;
        Vector3 center = RoadsideMap.PointAt(centerline, headings, index, side, 58f);
        result.Settlement = center;

        // Площадь и колодец в центре.
        parent.AddChild(new MeshInstance3D
        {
            Mesh = new CylinderMesh { Height = 0.1f, TopRadius = 16f, BottomRadius = 16f },
            Position = center + Vector3.Up * 0.03f,
            MaterialOverride = new StandardMaterial3D { AlbedoColor = new Color(0.48f, 0.44f, 0.36f), Roughness = 1f },
        });

        BuildWell(parent, center + Vector3.Up * 0.05f, rng);
        result.Uses.Add(new MapUse(center + Vector3.Up * 1.2f, "drink"));
        result.Places.Add(new MapPlace(index, side, center, "well", "Колодец"));

        // Пять домов по кругу.
        for (int k = 0; k < 5; k++)
        {
            float angle = k * Mathf.Tau / 5f + 0.35f;
            Vector3 spot = center + new Vector3(Mathf.Cos(angle), 0, Mathf.Sin(angle)) * (float)rng.Range(18, 24);
            spot.Y = TerrainSurface(centerline[index], spot);
            float yaw = -angle + Mathf.Pi / 2f;

            BuildStructure(parent, spot, yaw, "house", rng, interior: true, result: result);
            result.Places.Add(new MapPlace(index, side, spot, "house", "Дом"));
            result.Uses.Add(new MapUse(spot + new Vector3(0, 0.2f, 2.4f), "loot", LootFor("house"), 2));
            result.Uses.Add(new MapUse(spot + new Vector3(0, 0.2f, -2.4f), "loot", "cassette", 1));
        }

        // Забор по кругу с проёмом на дорогу.
        for (int k = 0; k < 40; k++)
        {
            float angle = k * Mathf.Tau / 40f;
            if (Math.Abs(angle - Mathf.Pi / 2f) < 0.2f) continue;   // проём к дороге

            Vector3 spot = center + new Vector3(Mathf.Cos(angle), 0, Mathf.Sin(angle)) * 30f;
            spot.Y = TerrainSurface(centerline[index], spot);
            parent.AddChild(new MeshInstance3D
            {
                Mesh = new BoxMesh { Size = new Vector3(0.15f, 1.2f, 0.15f) },
                Position = spot + Vector3.Up * 0.6f,
                MaterialOverride = new StandardMaterial3D { AlbedoColor = Wood, Roughness = 0.95f },
            });
        }

        // Скупщик у дороги: село торгует.
        Vector3 dealer = center - new Vector3(0, 0, 26f);
        dealer.Y = TerrainSurface(centerline[index], dealer);
        result.Uses.Add(new MapUse(dealer + Vector3.Up * 0.2f, "sell"));
        result.Places.Add(new MapPlace(index, side, dealer, "dealer", "Скупщик"));
    }

    // ------------------------------------------------------------ события

    private static void BuildEvents(List<Vector3> centerline, List<float> headings, Node3D parent,
        Pcg32 rng, MapResult result)
    {
        // Брошенная машина: кузов на земле и запчасти рядом.
        int carIndex = centerline.Count / 3;
        Vector3 carSpot = RoadsideMap.PointAt(centerline, headings, carIndex, 1f, 12f);
        BuildWreck(parent, carSpot, headings[carIndex] + 0.6f);
        result.Places.Add(new MapPlace(carIndex, 1f, carSpot, "wreck", "Брошенная машина"));
        result.Uses.Add(new MapUse(carSpot + Vector3.Up * 0.2f, "loot", "spare_tire", 1));

        // Сломанный трактор у сарая.
        int tractorIndex = centerline.Count / 7;
        Vector3 tractorSpot = RoadsideMap.PointAt(centerline, headings, tractorIndex, -1f, 34f);
        BuildTractor(parent, tractorSpot, headings[tractorIndex] - 0.4f);
        result.Places.Add(new MapPlace(tractorIndex, -1f, tractorSpot, "tractor", "Трактор"));
        result.Uses.Add(new MapUse(tractorSpot + Vector3.Up * 0.2f, "loot", "spark_plug", 2));

        // Собака у сарая: можно погладить.
        int dogIndex = centerline.Count * 3 / 4;
        Vector3 barnSpot = RoadsideMap.PointAt(centerline, headings, dogIndex, 1f, 40f);
        BuildStructure(parent, barnSpot, headings[dogIndex] + 0.4f, "barn", rng, result: result);
        Vector3 dog = barnSpot + new Vector3(2.4f, 0, 2.6f);
        BuildDog(parent, dog);
        result.Places.Add(new MapPlace(dogIndex, 1f, barnSpot, "dog", "Собака у сарая"));
        result.Uses.Add(new MapUse(dog, "dog"));

        // Закрытые выезды помечаем для миникарты.
        result.ClosedExits.Add(14);
        result.ClosedExits.Add(centerline.Count / 3);
        result.ClosedExits.Add(centerline.Count / 2);
        result.ClosedExits.Add(centerline.Count - 16);
    }

    // ------------------------------------------------------------ постройки

    private static void BuildStructure(Node3D parent, Vector3 position, float yaw, string kind, Pcg32 rng,
        bool interior = false, MapResult? result = null)
    {
        var root = new Node3D { Name = kind, Position = position, Rotation = new Vector3(0, yaw, 0) };
        parent.AddChild(root);

        switch (kind)
        {
            case "ruins": BuildRuins(root, rng); return;
            case "barn": BuildBarn(root, rng); return;
            default: BuildHouse(root, rng, kind == "abandoned", interior, result); return;
        }
    }

    private static void BuildHouse(Node3D root, Pcg32 rng, bool abandoned, bool interior, MapResult? result = null)
    {
        float width = (float)rng.Range(7.5, 9.5);
        float depth = (float)rng.Range(5.5, 7f);
        float height = 3.1f;
        Color wall = abandoned ? new Color(0.62f, 0.58f, 0.5f) : new Color(0.76f, 0.71f, 0.6f);

        // Пол и потолок: дом полый, внутрь можно войти.
        Box(root, new Vector3(width, 0.2f, depth), new Vector3(0, -0.1f, 0), new Color(0.42f, 0.34f, 0.25f));

        float door = 1.25f;
        float half = width / 2f;
        Box(root, new Vector3(half - door / 2f, height, 0.3f),
            new Vector3(-(door / 2f + (half - door / 2f) / 2f), height / 2f, -depth / 2f), wall);
        Box(root, new Vector3(half - door / 2f, height, 0.3f),
            new Vector3(door / 2f + (half - door / 2f) / 2f, height / 2f, -depth / 2f), wall);

        Box(root, new Vector3(width, height, 0.3f), new Vector3(0, height / 2f, depth / 2f), wall);
        Box(root, new Vector3(0.3f, height, depth), new Vector3(-half, height / 2f, 0), wall);
        Box(root, new Vector3(0.3f, height, depth), new Vector3(half, height / 2f, 0), wall);

        if (interior)
        {
            // Перегородка делит дом на две комнаты.
            Box(root, new Vector3(0.25f, height, depth * 0.55f),
                new Vector3(width * 0.15f, height / 2f, -depth * 0.2f), wall);
        }

        Roof(root, width + 0.8f, depth + 0.8f, height, abandoned ? new Color(0.35f, 0.3f, 0.27f) : new Color(0.45f, 0.24f, 0.19f));

        // Настоящая дверь на петлях: открывается по E, как в доме игрока.
        var swing = Door.Create("HouseDoor", Vector3.Zero, 0f, door * 0.95f, 2.05f,
            new Color(0.36f, 0.23f, 0.15f));
        swing.Position = new Vector3(-door / 2f, 0f, -depth / 2f);
        root.AddChild(swing);
        result?.Doors.Add(swing);
    }

    private static void BuildBarn(Node3D root, Pcg32 rng)
    {
        float width = (float)rng.Range(9f, 12f);
        float depth = (float)rng.Range(6f, 8f);
        float height = 4.2f;

        Box(root, new Vector3(width, 0.2f, depth), new Vector3(0, -0.1f, 0), Dirt);
        Box(root, new Vector3(width, height, 0.3f), new Vector3(0, height / 2f, -depth / 2f), Wood);
        Box(root, new Vector3(width, height, 0.3f), new Vector3(0, height / 2f, depth / 2f), Wood);
        Box(root, new Vector3(0.3f, height, depth), new Vector3(-width / 2f, height / 2f, 0), Wood);
        Box(root, new Vector3(0.3f, height, depth), new Vector3(width / 2f, height / 2f, 0), Wood);
        Roof(root, width + 0.8f, depth + 0.8f, height, new Color(0.35f, 0.3f, 0.27f));
    }

    private static void BuildRuins(Node3D root, Pcg32 rng)
    {
        int walls = 2 + rng.NextInt(3);

        for (int i = 0; i < walls; i++)
        {
            float length = (float)rng.Range(2.5f, 6.5f);
            float height = (float)rng.Range(0.8f, 2.4f);
            Box(root, new Vector3(length, height, 0.4f),
                new Vector3((float)rng.Range(-2.5, 2.5), height / 2f, (float)rng.Range(-2.5, 2.5)),
                Stone, (float)rng.Range(-0.5, 0.5) + i * 0.9f);
        }
    }

    private static void BuildWell(Node3D parent, Vector3 position, Pcg32 rng)
    {
        var root = new Node3D { Name = "Well", Position = position };
        parent.AddChild(root);

        float radius = 1.15f;
        root.AddChild(new MeshInstance3D
        {
            Mesh = new CylinderMesh { Height = 0.9f, TopRadius = radius, BottomRadius = radius },
            Position = new Vector3(0, 0.45f, 0),
            MaterialOverride = new StandardMaterial3D { AlbedoColor = Stone, Roughness = 0.95f },
        });
        Solid(root, new Vector3(radius * 2, 0.9f, radius * 2), new Vector3(0, 0.45f, 0));

        for (int i = -1; i <= 1; i += 2)
            Box(root, new Vector3(0.15f, 2.1f, 0.15f), new Vector3(i * (radius + 0.1f), 1.05f, 0), Wood);

        Box(root, new Vector3(radius * 2 + 0.7f, 0.15f, radius * 2 + 0.7f),
            new Vector3(0, 2.2f, 0), new Color(0.35f, 0.3f, 0.27f), (float)rng.Range(-0.1, 0.1));
    }

    private static void BuildWreck(Node3D parent, Vector3 position, float yaw)
    {
        var root = new Node3D { Name = "Wreck", Position = position, Rotation = new Vector3(0, yaw, 0) };
        parent.AddChild(root);

        Box(root, new Vector3(1.7f, 0.7f, 3.9f), new Vector3(0, 0.55f, 0), Rust);
        Box(root, new Vector3(1.5f, 0.5f, 1.9f), new Vector3(0, 1.1f, -0.2f), Rust);
    }

    private static void BuildTractor(Node3D parent, Vector3 position, float yaw)
    {
        var root = new Node3D { Name = "Tractor", Position = position, Rotation = new Vector3(0, yaw, 0) };
        parent.AddChild(root);

        Box(root, new Vector3(1.1f, 0.9f, 2.1f), new Vector3(0, 0.75f, 0), Paint);
        Box(root, new Vector3(1.0f, 0.8f, 0.9f), new Vector3(0, 1.6f, -0.5f), Paint);
        root.AddChild(new MeshInstance3D
        {
            Mesh = new CylinderMesh { Height = 0.4f, TopRadius = 0.75f, BottomRadius = 0.75f },
            Position = new Vector3(0.55f, 0.75f, 0.4f),
            RotationDegrees = new Vector3(0, 0, 90),
            MaterialOverride = new StandardMaterial3D { AlbedoColor = new Color(0.12f, 0.12f, 0.12f), Roughness = 0.95f },
        });
    }

    private static void BuildDog(Node3D parent, Vector3 position)
    {
        var root = new Node3D { Name = "Dog", Position = position };
        parent.AddChild(root);

        var fur = new StandardMaterial3D { AlbedoColor = new Color(0.42f, 0.32f, 0.22f), Roughness = 0.95f };
        root.AddChild(new MeshInstance3D
        {
            Mesh = new BoxMesh { Size = new Vector3(0.35f, 0.35f, 0.9f) },
            Position = new Vector3(0, 0.55f, 0),
            MaterialOverride = fur,
        });
        root.AddChild(new MeshInstance3D
        {
            Mesh = new BoxMesh { Size = new Vector3(0.3f, 0.3f, 0.35f) },
            Position = new Vector3(0, 0.78f, -0.55f),
            MaterialOverride = fur,
        });
        for (int i = -1; i <= 1; i += 2)
            for (int j = -1; j <= 1; j += 2)
                root.AddChild(new MeshInstance3D
                {
                    Mesh = new BoxMesh { Size = new Vector3(0.09f, 0.42f, 0.09f) },
                    Position = new Vector3(i * 0.12f, 0.21f, j * 0.28f),
                    MaterialOverride = fur,
                });
    }

    // ------------------------------------------------------------ утилиты

    private static float TerrainSurface(Vector3 roadPoint, Vector3 world)
    {
        float lateral = new Vector2(world.X - roadPoint.X, world.Z - roadPoint.Z).Length();
        return TerrainBuilder.SurfaceHeight(roadPoint.Y, lateral, world);
    }

    private static string LootFor(string kind) => kind switch
    {
        "ruins" => "scrap_metal",
        "barn" => "spark_plug",
        "well" => "bottle_cap",
        _ => "cassette",
    };

    private static string LabelFor(string kind) => kind switch
    {
        "ruins" => "Руины",
        "barn" => "Сарай",
        "house" => "Дом",
        "dead_end" => "Тупик",
        _ => "Место",
    };

    private static void Roof(Node3D parent, float width, float depth, float height, Color color)
    {
        var material = new StandardMaterial3D { AlbedoColor = color, Roughness = 0.9f };
        float half = depth / 2f;

        for (int side = -1; side <= 1; side += 2)
        {
            float slope = Mathf.Sqrt(half * half + 0.36f);
            var slab = new MeshInstance3D
            {
                Mesh = new BoxMesh { Size = new Vector3(width, 0.16f, slope * 1.1f) },
                Position = new Vector3(0, height + 0.55f, side * half * 0.45f),
                MaterialOverride = material,
            };
            slab.Rotation = new Vector3(side * Mathf.Atan2(0.6f, half), 0, 0);
            parent.AddChild(slab);
        }
    }

    private static void Box(Node3D parent, Vector3 size, Vector3 position, Color color, float rotateY = 0f)
    {
        parent.AddChild(new MeshInstance3D
        {
            Mesh = new BoxMesh { Size = size },
            Position = position,
            Rotation = new Vector3(0, rotateY, 0),
            MaterialOverride = new StandardMaterial3D { AlbedoColor = color, Roughness = 0.94f },
        });
        Solid(parent, size, position, rotateY);
    }

    private static void Solid(Node3D parent, Vector3 size, Vector3 position, float rotateY = 0f)
    {
        var body = new StaticBody3D { Position = position, Rotation = new Vector3(0, rotateY, 0) };
        body.AddChild(new CollisionShape3D { Shape = new BoxShape3D { Size = size } });
        parent.AddChild(body);
    }
}
