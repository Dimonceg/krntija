using System;
using System.Collections.Generic;
using Godot;
using Krntija.Core.World;
using Krntija.Core.WorldGen;

namespace Krntija.Game;

/// <summary>
/// Строит места куска по плану (ADR 0002, фаза 2б): постройку, лут и точки
/// взаимодействия. Сцену не трогает: всё кладёт под корень куска, а точки
/// возвращает списком — их регистрирует <see cref="Main"/> и снимает при выгрузке.
/// </summary>
/// <remarks>
/// Позиции точек — в координатах корня куска: корень двигается при сдвиге начала,
/// поэтому переводить их в сцену надо в момент регистрации. Id для
/// <see cref="WorldState"/> берутся из плана и от порядка постройки не зависят.
/// </remarks>
public static class PlaceBuilder
{
    /// <summary>Точка взаимодействия места. <paramref name="Kind"/> — вид из <see cref="UsePlan.Kind"/>
    /// или служебный: <c>workbench</c>, <c>sleep_home</c>.</summary>
    /// <param name="Prop">Что убрать, когда действие сделано: завал, заграждение выезда.</param>
    public sealed record Use(string Kind, Vector3 Local, string? StateId = null,
        CheckpointGate? Gate = null, Door? Door = null, Node3D? Prop = null);

    public sealed record Loot(string StateId, string ItemId, int Count, Vector3 Local);

    public sealed record Built(PlacePlan Plan, Vector3 Local, List<Use> Uses, List<Loot> Loot);

    /// <summary>Радиус, внутри которого у места нет деревьев и травы: габарит и запас на крону.</summary>
    public static float ClearRadius(PlacePlan place) => (float)place.Footprint.Radius + 4f;

    /// <param name="local">Мировые (x, y, z) → координаты корня куска.</param>
    /// <param name="ground">Высота земли куска в точке мира (x, z): с ровными площадками мест.</param>
    public static Built Build(RoadCurve curve, PlacePlan place, Node3D chunkRoot,
        Func<double, double, double, Vector3> local, Func<double, double, float> ground)
    {
        var (cx, cz) = PlaceGeometry.Center(place, curve);
        float cy = place.OnRoad ? (float)curve.Height(place.S) : ground(cx, cz);
        var built = new Built(place, local(cx, cy, cz), new List<Use>(), new List<Loot>());

        var root = new Node3D
        {
            Name = $"Place_{place.Id.Replace('/', '_')}",
            Position = built.Local,
            Rotation = new Vector3(0, -(float)place.Yaw, 0),
        };
        chunkRoot.AddChild(root);

        bool ownLayout = place.Kind switch
        {
            PlaceKind.House => BuildHouse(root, built),
            PlaceKind.Checkpoint => BuildCheckpoint(curve, place, root, chunkRoot, built, local, ground),
            _ => BuildModel(place, root),
        };

        // Точки из плана; дом и шлагбаум блокпоста раскладываются сами.
        if (!ownLayout)
            foreach (var use in place.Uses)
                AddPlanUse(curve, use, root, chunkRoot, built, local, ground);

        foreach (var loot in place.Loot)
        {
            var offset = new Vector3((float)loot.OffsetX, 0, (float)loot.OffsetZ);
            Vector3 point = OnGround(root, chunkRoot, offset, local, ground) + Vector3.Up * (float)loot.OffsetY;
            built.Loot.Add(new Loot(WorldState.Loot(loot.Id), loot.ItemId, loot.Count, point));
        }

        // Дом собран из сотен ящиков — сливаем, как статику старого мира. Модели GLB
        // не сливаются (текстуры), а их обход стоит до 8 мс: посреди поездки это рывок.
        if (place.Kind == PlaceKind.House) StaticBatcher.Merge(root, cellSize: 64f);
        return built;
    }

    /// <summary>Точка плана: предмет-заглушка, якорь из модели или точка на земле, id состояния.</summary>
    private static void AddPlanUse(RoadCurve curve, UsePlan use, Node3D root, Node3D chunkRoot, Built built,
        Func<double, double, double, Vector3> local, Func<double, double, float> ground)
    {
        var offset = new Vector3((float)use.OffsetX, (float)use.OffsetY, (float)use.OffsetZ);
        var anchor = root.FindChild(AnchorName(use.Kind), true, false) as Node3D;
        Vector3 point = anchor is not null
            ? chunkRoot.ToLocal(anchor.GlobalPosition)
            : OnGround(root, chunkRoot, offset, local, ground);

        Node3D? prop = BuildUseProp(use.Kind, root, offset);

        // Состояние в мире помнит только то, что игрок может изменить.
        string? stateId = use.Kind switch
        {
            "exit" => WorldState.Use("exit", use.Id),
            "collapse" => WorldState.Use("collapse", use.Id),
            "crate" => WorldState.Use("crate", use.Id),
            _ => null,
        };

        // Убираются завал и заграждение; пустой ящик остаётся стоять.
        built.Uses.Add(new Use(use.Kind, point + Vector3.Up * 0.8f, stateId,
            Prop: use.Kind is "exit" or "collapse" ? prop : null));
    }

    /// <summary>Точка в модели, к которой художник привязал действие: <c>PumpAnchor</c>, <c>SleepAnchor</c>.</summary>
    private static string AnchorName(string kind) => char.ToUpperInvariant(kind[0]) + kind[1..] + "Anchor";

    /// <summary>Смещение от центра места (в его повороте) → точка на земле в координатах куска.</summary>
    private static Vector3 OnGround(Node3D placeRoot, Node3D chunkRoot, Vector3 offset,
        Func<double, double, double, Vector3> local, Func<double, double, float> ground)
    {
        Vector3 inChunk = placeRoot.Position + placeRoot.Basis * offset;
        Vector3 origin = local(0, 0, 0);
        double x = inChunk.X - origin.X;
        double z = inChunk.Z - origin.Z;
        return local(x, ground(x, z), z);
    }

    // ------------------------------------------------------------ постройки

    private static readonly string[] ModelIds =
    {
        "gasstation", "village", "ruins", "farm", "checkpoint", "well", "shed", "barn", "abandoned",
    };

    private static string[] Variants(string id) => new[] { id + "_a", id + "_b", id + "_c", id };

    /// <summary>
    /// Загрузить модели мест заранее: первая загрузка GLB — десятки миллисекунд,
    /// посреди поездки это рывок. Вызывается один раз при создании мира.
    /// </summary>
    public static void Preload()
    {
        foreach (string id in ModelIds)
        {
            foreach (string variant in Variants(id))
            {
                var model = ModelLibrary.Instantiate(PropFactory.Folder + variant + ".glb");
                if (model is null) continue;
                PropFactory.Measure(model); // габариты кешируются по имени модели
                model.Free();
            }
        }
    }

    /// <summary>Модель места из assets/models/props, если художник её положил.</summary>
    private static bool BuildModel(PlacePlan place, Node3D root)
    {
        string? id = place.Kind switch
        {
            PlaceKind.GasStation => "gasstation",
            PlaceKind.Village => "village",
            PlaceKind.Ruins => "ruins",
            PlaceKind.Farm => "farm",
            PlaceKind.Well => "well",
            PlaceKind.Shed => "shed",
            PlaceKind.Barn => "barn",
            PlaceKind.Abandoned => "abandoned",
            _ => null,
        };

        string? variant = id is null ? null : PropFactory.PickVariant(Variants(id), place.VariantSeed);

        if (variant is null || PropFactory.Spawn(variant, Vector3.Zero, 0, solid: true, root) is null)
            BuildStub(place.Kind, root);

        return false;
    }

    /// <summary>Заглушка, пока у места нет модели: силуэт видно издалека, на карте точка.</summary>
    private static void BuildStub(PlaceKind kind, Node3D root)
    {
        var (size, color) = kind switch
        {
            PlaceKind.Well => (new Vector3(1.4f, 0.9f, 1.4f), new Color(0.55f, 0.55f, 0.52f)),
            PlaceKind.Shed => (new Vector3(3.5f, 2.4f, 3f), new Color(0.45f, 0.36f, 0.26f)),
            PlaceKind.Barn => (new Vector3(9f, 5f, 7f), new Color(0.5f, 0.27f, 0.2f)),
            PlaceKind.Abandoned => (new Vector3(7f, 3.4f, 6f), new Color(0.6f, 0.57f, 0.5f)),
            _ => (new Vector3(8f, 3f, 6f), new Color(0.58f, 0.55f, 0.5f)),
        };

        var material = new StandardMaterial3D { AlbedoColor = color, Roughness = 0.92f };
        root.AddChild(new MeshInstance3D
        {
            Mesh = new BoxMesh { Size = size },
            Position = new Vector3(0, size.Y * 0.5f - 0.2f, 0),
            MaterialOverride = material,
        });

        var body = new StaticBody3D();
        body.AddChild(new CollisionShape3D
        {
            Shape = new BoxShape3D { Size = size },
            Position = new Vector3(0, size.Y * 0.5f - 0.2f, 0),
        });
        root.AddChild(body);
    }

    /// <summary>Дом игрока: свои двери, сундук, верстак и кровать на чердаке.</summary>
    private static bool BuildHouse(Node3D root, Built built)
    {
        // Фундамент приподнят над газоном, чтобы рельеф не пробивал пол.
        root.Position += Vector3.Up * 0.12f;
        var chunkRoot = (Node3D)root.GetParent();

        var doors = PlayerHouse.Build(root, Vector3.Zero, Mathf.Pi * 0.5f);
        foreach (var (position, door) in doors)
        {
            // Подсказка ищется от центра полотна, а не от петли: у петли радиус мал.
            Vector3 center = position + door.GlobalBasis.X.Normalized() * (door.Width * 0.5f);
            built.Uses.Add(new Use("door", chunkRoot.ToLocal(center), Door: door));
        }

        var house = root.GetNode<Node3D>("PlayerHouse");
        Vector3 At(float x, float y, float z) => chunkRoot.ToLocal(house.ToGlobal(new Vector3(x, y, z)));
        built.Uses.Add(new Use("chest", At(-4.3f, 0.7f, 2.0f)));
        built.Uses.Add(new Use("workbench", At(4.3f, 0.7f, -0.6f)));
        built.Uses.Add(new Use("sleep_home", At(-3.6f, 3.0f, -1.8f)));
        return true;
    }

    // ------------------------------------------------------------ точки-предметы

    private static readonly Color WoodColor = new(0.42f, 0.31f, 0.2f);
    private static readonly Color RockColor = new(0.5f, 0.48f, 0.46f);
    private static readonly Color ClothColor = new(0.35f, 0.3f, 0.42f);
    private static readonly Color FurColor = new(0.32f, 0.3f, 0.28f);

    /// <summary>
    /// Простая геометрия для точек плана: жители, собака, волки, завал, выезд, ящик.
    /// Всё под одним узлом — его убирают целиком (разобранный завал, открытый выезд).
    /// </summary>
    private static Node3D? BuildUseProp(string kind, Node3D placeRoot, Vector3 offset)
    {
        var root = new Node3D { Name = $"Use_{kind}" };
        placeRoot.AddChild(root);

        switch (kind)
        {
            case "talk":
                Prop(root, offset + new Vector3(0, 1.0f, 0), new Vector3(0.55f, 1.6f, 0.4f), ClothColor);
                Prop(root, offset + new Vector3(0, 1.95f, 0), new Vector3(0.32f, 0.34f, 0.32f),
                    new Color(0.84f, 0.7f, 0.56f));
                break;

            case "dog":
                Prop(root, offset + new Vector3(0, 0.55f, 0), new Vector3(0.35f, 0.4f, 0.9f), FurColor);
                break;

            case "wolves":
                for (int i = 0; i < 3; i++)
                {
                    var spread = new Vector3((i - 1) * 1.8f, 0, Math.Abs(i - 1) * 1.2f);
                    Prop(root, offset + spread + new Vector3(0, 0.5f, 0), new Vector3(0.32f, 0.36f, 0.8f), FurColor);
                }
                break;

            case "collapse":
                for (int i = 0; i < 4; i++)
                {
                    var spread = new Vector3((i - 1.5f) * 1.1f, 0.5f, (i % 2) * 1.2f);
                    Prop(root, offset + spread, new Vector3(1.2f, 1.0f, 1.0f), RockColor, solid: true);
                }
                break;

            case "exit":
                Prop(root, offset + new Vector3(-3, 1.2f, 0), new Vector3(0.3f, 2.4f, 0.3f), WoodColor, solid: true);
                Prop(root, offset + new Vector3(3, 1.2f, 0), new Vector3(0.3f, 2.4f, 0.3f), WoodColor, solid: true);
                Prop(root, offset + new Vector3(0, 1.1f, 0), new Vector3(6.4f, 0.25f, 0.25f),
                    new Color(0.75f, 0.2f, 0.15f));
                break;

            case "crate":
                Prop(root, offset + new Vector3(0, 0.35f, 0), new Vector3(0.9f, 0.7f, 0.7f), WoodColor, solid: true);
                break;
        }

        if (root.GetChildCount() > 0) return root;
        root.QueueFree();
        return null;
    }

    /// <summary>Коробка-заглушка: цвет по смыслу, коллизия по желанию.</summary>
    private static MeshInstance3D Prop(Node3D root, Vector3 position, Vector3 size, Color color, bool solid = false)
    {
        var mesh = new MeshInstance3D
        {
            Mesh = new BoxMesh { Size = size },
            Position = position,
            MaterialOverride = new StandardMaterial3D { AlbedoColor = color, Roughness = 0.95f },
        };
        root.AddChild(mesh);

        if (!solid) return mesh;

        var body = new StaticBody3D { Name = "PropBody", Position = position };
        body.AddChild(new CollisionShape3D { Shape = new BoxShape3D { Size = size } });
        root.AddChild(body);
        return mesh;
    }

    /// <summary>Блокпост: шлагбаум поперёк дороги, будка на обочине.</summary>
    private static bool BuildCheckpoint(RoadCurve curve, PlacePlan place, Node3D root, Node3D chunkRoot,
        Built built, Func<double, double, double, Vector3> local, Func<double, double, float> ground)
    {
        float heading = (float)curve.Heading(place.S);
        root.Rotation = new Vector3(0, -heading, 0);

        var gate = CheckpointGate.Create(Vector3.Zero, 0);
        gate.Toll = ThreatRules.TollPrice(WorldGenerator.Tier(place.S));
        root.AddChild(gate);

        var (bx, _, bz) = curve.Offset(place.S, -(RoadMeshBuilder.Width * 0.5 + 6));
        float by = ground(bx, bz);
        var booth = new Node3D { Position = root.ToLocal(chunkRoot.ToGlobal(local(bx, by, bz))) };
        root.AddChild(booth);

        string? variant = PropFactory.PickVariant(Variants("checkpoint"), place.VariantSeed);
        if (variant is null || PropFactory.Spawn(variant, Vector3.Zero, 0, solid: true, booth) is null)
            BuildStub(PlaceKind.Checkpoint, booth);

        foreach (var use in place.Uses)
        {
            if (use.Kind == "gate")
                built.Uses.Add(new Use("gate", built.Local + Vector3.Up * 1.2f, WorldState.Use("gate", use.Id), Gate: gate));
            else
                AddPlanUse(curve, use, root, chunkRoot, built, local, ground); // закрытый выезд сбоку
        }

        return true;
    }
}
