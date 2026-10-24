using System;
using System.Collections.Generic;
using Godot;
using Krntija.Core.World;

namespace Krntija.Game;

/// <summary>
/// Жизнь на карте: указатели на развилках, жители, двери в домах, обвал и волки,
/// а также открываемые выезды с куском карты за границей.
/// </summary>
public static class MapLife
{
    private static readonly Color Wood = new(0.42f, 0.31f, 0.2f);
    private static readonly Color Board = new(0.82f, 0.76f, 0.6f);
    private static readonly Color Stone = new(0.5f, 0.48f, 0.46f);

    public static readonly string[] ResidentLines =
    {
        "Дорога дальше плохая, но ты езжай — там наши.",
        "Бензин у заправки есть, а вот с деньгами у всех туго.",
        "В руинах ночью кто-то ходит. Не суйся.",
        "Собака не тронет, если её не дразнить.",
        "Говорят, за шлагбаумом другой край. Когда-нибудь починим.",
    };

    // ------------------------------------------------------------ указатели

    /// <summary>Указатель на развилке: стрелка и названия того, что впереди.</summary>
    public static void BuildForkSigns(Node3D parent, List<Vector3> centerline, List<float> headings,
        List<List<Vector3>> branches, MapResult result)
    {
        for (int i = 0; i < branches.Count; i++)
        {
            var branch = branches[i];
            if (branch.Count < 3) continue;

            Vector3 start = branch[1];
            Vector3 direction = branch[2] - branch[0];
            direction.Y = 0;
            if (direction.LengthSquared() < 0.01f) continue;

            float yaw = Mathf.Atan2(direction.X, direction.Z);
            var root = new Node3D { Name = "ForkSign", Position = start, Rotation = new Vector3(0, yaw, 0) };
            parent.AddChild(root);

            root.AddChild(new MeshInstance3D
            {
                Mesh = new BoxMesh { Size = new Vector3(0.15f, 2.1f, 0.15f) },
                Position = new Vector3(0, 1.05f, 0),
                MaterialOverride = new StandardMaterial3D { AlbedoColor = Wood, Roughness = 0.95f },
            });

            root.AddChild(new MeshInstance3D
            {
                Mesh = new BoxMesh { Size = new Vector3(1.7f, 0.45f, 0.08f) },
                Position = new Vector3(0.2f, 1.85f, 0),
                MaterialOverride = new StandardMaterial3D { AlbedoColor = Board, Roughness = 0.9f },
            });

            var label = new Label3D
            {
                Text = $"→ {NearestName(result, branch[^1])}",
                Position = new Vector3(0.2f, 1.85f, 0.06f),
                FontSize = 48,
                PixelSize = 0.004f,
                Modulate = new Color(0.15f, 0.12f, 0.08f),
                Billboard = BaseMaterial3D.BillboardModeEnum.Disabled,
                NoDepthTest = false,
            };
            root.AddChild(label);

            result.Places.Add(new MapPlace(0, 0, start, "sign", "Указатель"));
        }
    }

    private static string NearestName(MapResult result, Vector3 point)
    {
        string best = "Тупик";
        float distance = float.MaxValue;

        foreach (var place in result.Places)
        {
            if (place.Kind is "dead_end" or "sign") continue;
            float d = place.Position.DistanceTo(point);
            if (d >= distance) continue;

            distance = d;
            best = place.Label;
        }

        return best;
    }

    // ------------------------------------------------------------ жители

    /// <summary>Два жителя: у колодца и у скупщика. С ними можно поговорить.</summary>
    public static void BuildResidents(Node3D parent, MapResult result, Pcg32 rng)
    {
        if (result.Settlement is not { } settlement) return;

        BuildResident(parent, settlement + new Vector3(2.5f, 0, 1.5f), 0.6f, "talk_0");
        result.Uses.Add(new MapUse(settlement + new Vector3(2.5f, 0, 1.5f), "talk", "0"));

        Vector3 dealer = settlement - new Vector3(0, 0, 26f);
        BuildResident(parent, dealer + new Vector3(1.5f, 0, 0.5f), -0.8f, "talk_1");
        result.Uses.Add(new MapUse(dealer + new Vector3(1.5f, 0, 0.5f), "talk", "1"));
    }

    private static void BuildResident(Node3D parent, Vector3 position, float yaw, string name)
    {
        var root = new Node3D { Name = name, Position = position, Rotation = new Vector3(0, yaw, 0) };
        parent.AddChild(root);

        var cloth = new StandardMaterial3D { AlbedoColor = new Color(0.35f, 0.3f, 0.42f), Roughness = 0.95f };
        var skin = new StandardMaterial3D { AlbedoColor = new Color(0.84f, 0.7f, 0.56f), Roughness = 0.8f };

        root.AddChild(new MeshInstance3D
        {
            Mesh = new CapsuleMesh { Radius = 0.28f, Height = 1.1f },
            Position = new Vector3(0, 0.55f, 0),
            MaterialOverride = cloth,
        });
        root.AddChild(new MeshInstance3D
        {
            Mesh = new SphereMesh { Radius = 0.15f, Height = 0.3f },
            Position = new Vector3(0, 1.25f, 0),
            MaterialOverride = skin,
        });

        var label = new Label3D
        {
            Text = "Житель",
            Position = new Vector3(0, 1.65f, 0),
            FontSize = 36,
            PixelSize = 0.004f,
            Modulate = new Color(1f, 0.95f, 0.8f),
            Billboard = BaseMaterial3D.BillboardModeEnum.Enabled,
        };
        root.AddChild(label);
    }

    // ------------------------------------------------------------ выезды

    /// <summary>
    /// Выезды за границу: закрыты шлагбаумом и широкой стеной. Починить можно
    /// за деньги и изоленту — тогда преграда уходит, а за ней открывается карта.
    /// </summary>
    public static List<(Vector3 Position, Door Barrier, Node3D Wall)> BuildClosedExits(
        Node3D parent, List<Vector3> centerline, List<float> headings, MapResult result, Pcg32 rng)
    {
        var exits = new List<(Vector3, Door, Node3D)>();

        for (int i = 0; i < result.ClosedExits.Count; i++)
        {
            int index = result.ClosedExits[i];
            if (index < 4 || index >= centerline.Count - 4) continue;

            float side = i % 2 == 0 ? 1f : -1f;
            Vector3 point = RoadsideMap.PointAt(centerline, headings, index, side, 126f);
            float yaw = -headings[index] + Mathf.Pi / 2f;

            var root = new Node3D { Name = $"Exit_{i}", Position = point, Rotation = new Vector3(0, yaw, 0) };
            parent.AddChild(root);

            // Столбы.
            for (int s = -1; s <= 1; s += 2)
                root.AddChild(new MeshInstance3D
                {
                    Mesh = new BoxMesh { Size = new Vector3(0.3f, 2.4f, 0.3f) },
                    Position = new Vector3(s * 3.2f, 1.2f, 0),
                    MaterialOverride = new StandardMaterial3D { AlbedoColor = Wood, Roughness = 0.9f },
                });

            // Шлагбаум: открывается поворотом вокруг столба.
            var barrier = Door.Create($"ExitBarrier_{i}", Vector3.Zero, 0f, 6.4f, 0.25f,
                new Color(0.75f, 0.2f, 0.15f), garage: false);
            barrier.Position = new Vector3(-3.2f, 1.1f, 0);
            barrier.OpenAngle = 0.9f;
            root.AddChild(barrier);

            // Невидимая стена, которую сносим при починке.
            var wall = new StaticBody3D { Name = "ExitWall" };
            wall.AddChild(new CollisionShape3D
            {
                Shape = new BoxShape3D { Size = new Vector3(8f, 3f, 2f) },
                Position = new Vector3(0, 1.5f, 0),
            });
            root.AddChild(wall);

            exits.Add((point, barrier, wall));
            result.Uses.Add(new MapUse(point + Vector3.Up * 0.2f, "exit", i.ToString()));
        }

        return exits;
    }

    // ------------------------------------------------------------ события

    /// <summary>Обвал на ветке: камни перекрывают дорогу, пока их не разберут.</summary>
    public static (Vector3 Position, Node3D Node, StaticBody3D Body) BuildCollapse(
        Node3D parent, List<Vector3> branch, Pcg32 rng)
    {
        Vector3 point = branch[Mathf.Clamp(branch.Count / 2, 1, branch.Count - 1)];
        var root = new Node3D { Name = "Collapse", Position = point };
        parent.AddChild(root);

        for (int i = 0; i < 6; i++)
        {
            var size = new Vector3((float)rng.Range(0.6, 1.6), (float)rng.Range(0.5, 1.2), (float)rng.Range(0.6, 1.4));
            root.AddChild(new MeshInstance3D
            {
                Mesh = new BoxMesh { Size = size },
                Position = new Vector3((float)rng.Range(-2.4, 2.4), size.Y * 0.5f, (float)rng.Range(-2.5, 2.5)),
                Rotation = new Vector3((float)rng.Range(-0.4, 0.4), (float)rng.Range(0, 3), (float)rng.Range(-0.4, 0.4)),
                MaterialOverride = new StandardMaterial3D { AlbedoColor = Stone, Roughness = 1f },
            });
        }

        var body = new StaticBody3D { Name = "CollapseBody" };
        body.AddChild(new CollisionShape3D
        {
            Shape = new BoxShape3D { Size = new Vector3(6f, 2.2f, 6f) },
            Position = new Vector3(0, 1.1f, 0),
        });
        root.AddChild(body);

        return (point, root, body);
    }

    /// <summary>Волчья стая у сарая: приближаться опасно.</summary>
    public static List<Node3D> BuildWolves(Node3D parent, Vector3 center, Pcg32 rng, int count = 3)
    {
        var wolves = new List<Node3D>();
        var fur = new StandardMaterial3D { AlbedoColor = new Color(0.32f, 0.3f, 0.28f), Roughness = 0.95f };

        for (int i = 0; i < count; i++)
        {
            float angle = i * Mathf.Tau / count + (float)rng.Range(-0.2, 0.2);
            var root = new Node3D
            {
                Name = $"Wolf_{i}",
                Position = center + new Vector3(Mathf.Cos(angle), 0, Mathf.Sin(angle)) * (float)rng.Range(2.5, 5),
            };
            parent.AddChild(root);

            root.AddChild(new MeshInstance3D
            {
                Mesh = new BoxMesh { Size = new Vector3(0.3f, 0.32f, 0.8f) },
                Position = new Vector3(0, 0.5f, 0),
                MaterialOverride = fur,
            });
            root.AddChild(new MeshInstance3D
            {
                Mesh = new BoxMesh { Size = new Vector3(0.26f, 0.26f, 0.3f) },
                Position = new Vector3(0, 0.7f, -0.5f),
                MaterialOverride = fur,
            });

            wolves.Add(root);
        }

        return wolves;
    }
}
