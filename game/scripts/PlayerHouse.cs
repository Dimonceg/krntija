using System.Collections.Generic;
using Godot;

namespace Krntija.Game;

/// <summary>
/// Дом игрока у старта: зал, гостиная, кухня, гараж и чердак.
/// Строится кодом из стен с коллизией, лестницы и дверей на петлях.
/// Локальные координаты: X — вдоль фасада, Z — вглубь, перед дома смотрит в −Z.
/// </summary>
public static partial class PlayerHouse
{
    private const float Width = 11f;
    private const float Depth = 8f;
    private const float WallHeight = 2.7f;
    private const float Thickness = 0.24f;
    private const float EavesHeight = 4.6f;
    private const float RidgeHeight = 6.2f;

    // Проём двери: 2.1 м при росте игрока 1.78 — запас 32 см.
    private const float DoorHeight = 2.1f;
    private const float DoorWidth = 1.3f;
    private const float InnerDoorWidth = 1.1f;

    private static readonly Color Wall = new(0.78f, 0.74f, 0.64f);
    private static readonly Color WallInner = new(0.82f, 0.79f, 0.71f);
    private static readonly Color Floor = new(0.42f, 0.34f, 0.25f);
    private static readonly Color DoorColor = new(0.36f, 0.23f, 0.15f);
    private static readonly Color GarageDoorColor = new(0.55f, 0.55f, 0.52f);
    private static readonly Color Furniture = new(0.48f, 0.36f, 0.24f);
    private static readonly Color Glass = new(0.55f, 0.68f, 0.75f);

    /// <summary>Собрать дом. Возвращает двери вместе с мировыми точками для подсказок.</summary>
    public static List<(Vector3 Position, Door Door)> Build(Node3D parent, Vector3 origin, float heading)
    {
        var root = new Node3D { Name = "PlayerHouse", Position = origin, Rotation = new Vector3(0, heading, 0) };
        parent.AddChild(root);

        var doors = new List<(Vector3, Door)>();

        // --- фундамент и полы ---
        Slab(root, new Vector3(Width, 0.6f, Depth), new Vector3(0, -0.4f, 0), new Color(0.5f, 0.48f, 0.44f));
        Slab(root, new Vector3(Width, 0.2f, Depth), new Vector3(0, -0.1f, 0), Floor);
        // Перекрытие чердака с проёмом над лестницей (x 3.2…5.5, z 0.6…3.6).
        // Твёрдое: на чердаке кровать, без коллизии игрок проваливался на первый этаж.
        Slab(root, new Vector3(8.7f, 0.2f, Depth), new Vector3(-1.15f, WallHeight, 0), Floor);
        Slab(root, new Vector3(2.3f, 0.2f, 4.0f), new Vector3(4.35f, WallHeight, -2.0f), Floor);
        Slab(root, new Vector3(2.3f, 0.2f, 0.4f), new Vector3(4.35f, WallHeight, 3.8f), Floor);

        // --- внешние стены с проёмами ---
        // Фасад (z = -Depth/2): вход в зал и ворота гаража.
        WallX(root, -Depth / 2, -Width / 2, -1.9f);
        WallX(root, -Depth / 2, -0.6f, 2.5f);
        LintelAt(root, -Depth / 2, -1.9f + DoorWidth / 2, DoorWidth, DoorHeight, horizontal: true);
        LintelAt(root, -Depth / 2, 2.5f + 1.5f, 3.0f, 2.2f, horizontal: true);

        // Задняя стена (z = +Depth/2): чёрный ход из кухни.
        WallX(root, Depth / 2, -Width / 2, -4.6f);
        WallX(root, Depth / 2, -4.6f + DoorWidth, Width / 2);
        LintelAt(root, Depth / 2, -4.6f + DoorWidth / 2, DoorWidth, DoorHeight, horizontal: true);

        // Боковые стены.
        WallZ(root, -Width / 2, -Depth / 2, Depth / 2);
        WallZ(root, Width / 2, -Depth / 2, Depth / 2);

        // --- внутренние перегородки ---
        // Гараж: отделён от зала и гостиной, с дверью в зал.
        WallZ(root, 2.2f, -Depth / 2, -2.0f, WallInner);
        WallZ(root, 2.2f, -2.0f + InnerDoorWidth, 0f, WallInner);
        WallX(root, 0f, 2.2f, Width / 2, WallInner);
        LintelAt(root, 2.2f, -2.0f + InnerDoorWidth / 2, InnerDoorWidth, DoorHeight, horizontal: false);

        // Кухня: угол в глубине, вход из гостиной (проём 2.35…3.45).
        WallX(root, 1.6f, -Width / 2, -2.2f, WallInner);
        WallZ(root, -2.2f, 1.6f, 2.35f, WallInner);
        WallZ(root, -2.2f, 3.45f, Depth / 2, WallInner);
        LintelAt(root, -2.2f, 2.9f, InnerDoorWidth, DoorHeight, horizontal: false);

        // --- окна ---
        AddWindowX(root, -Depth / 2, -3.7f, 1.55f, 1.1f, 1.0f);
        AddWindowX(root, -Depth / 2, 0.8f, 1.55f, 1.1f, 1.0f);
        AddWindowX(root, Depth / 2, 1.2f, 1.55f, 1.1f, 1.0f);
        AddWindowZ(root, -Width / 2, 0.6f, 1.55f, 1.1f, 1.0f);
        AddWindowZ(root, Width / 2, -2.6f, 1.6f, 1.0f, 0.9f);

        // --- крыша и чердак ---
        BuildRoof(root);
        AddHouseModel(root, "home_facade");

        // Лестница на чердак: низкие ступени по 12 см — шаг вверх берёт их надёжно.
        // Пандус убран: капсула на нём застревала, а на ступенях с шагом идёт.
        for (int i = 0; i < 22; i++)
        {
            float y = 0.06f + i * 0.12f;
            Slab(root, new Vector3(2.6f, 0.12f, 0.34f), new Vector3(3.6f, y, 3.7f - i * 0.16f), Floor);
        }

        // Перила вокруг проёма: с чердака не свалиться вниз.
        for (int i = 0; i <= 8; i++)
        {
            float z = 0.4f + i * 0.4f;
            Slab(root, new Vector3(0.12f, 1.0f, 0.12f), new Vector3(3.15f, WallHeight + 0.5f, z), Furniture);
        }

        Slab(root, new Vector3(0.1f, 0.12f, 3.4f), new Vector3(3.15f, WallHeight + 1.0f, 2.0f), Furniture, solid: false);

        AddWindowZ(root, -Width / 2, 0f, 3.8f, 1.2f, 1f);
        AddWindowZ(root, Width / 2, 0f, 3.8f, 1.2f, 1f);
        AddWindowX(root, -4f, -3.7f, 3.8f, 1.1f, .8f);
        AddWindowX(root, -4f, .8f, 3.8f, 1.1f, .8f);

        BuildCellar(root);

        // --- мебель ---
        BuildFurniture(root);

        // --- свет: интерьер без ламп был бы чёрным ---
        AddLight(root, new Vector3(0, 2.35f, -2.0f), 1.7f);
        AddLight(root, new Vector3(-3.4f, 2.35f, 1.4f), 1.5f);
        AddLight(root, new Vector3(4.0f, 2.35f, -2.0f), 1.2f);
        AddLight(root, new Vector3(0, 5.25f, 0.4f), 1.4f);

        // --- крыльцо и дорожка к дороге ---
        // Крыльцо вровень с полом и заходит под стену: никакого порожка в проёме.
        Slab(root, new Vector3(3.4f, 0.18f, 1.8f), new Vector3(-1.25f, -0.09f, -Depth / 2 - 0.7f), new Color(0.55f, 0.52f, 0.46f));
        Slab(root, new Vector3(3.4f, 0.12f, 0.5f), new Vector3(-1.25f, -0.18f, -Depth / 2 - 1.75f), new Color(0.55f, 0.52f, 0.46f), solid: false);
        Slab(root, new Vector3(1.5f, 0.06f, 6.5f), new Vector3(-1.25f, -0.14f, -Depth / 2 - 4.6f), new Color(0.62f, 0.58f, 0.5f), solid: false);

        // Заднее крыльцо и съезд к нему: порог 12 см капсула не берёт.
        Slab(root, new Vector3(3.0f, 0.18f, 1.6f), new Vector3(-4.05f, -0.09f, Depth / 2 + 0.7f), new Color(0.55f, 0.52f, 0.46f));
        var backRamp = Slab(root, new Vector3(3.0f, 0.14f, 1.8f), new Vector3(-4.05f, -0.12f, Depth / 2 + 2.2f),
            new Color(0.55f, 0.52f, 0.46f));
        backRamp.Rotation = new Vector3(0.1f, 0, 0);

        // --- двери ---
        // Уличные открываются НАРУЖУ: полотно не должно стоять в проёме.
        var frontDoor = AddDoor(root, "FrontDoor", new Vector3(-1.9f, 0f, -Depth / 2), 0f, DoorWidth, DoorHeight, DoorColor);
        frontDoor.OpenAngle = -1.75f;
        doors.Add((frontDoor.GlobalPosition, frontDoor));

        var backDoor = AddDoor(root, "BackDoor", new Vector3(-4.6f, 0f, Depth / 2), 0f, DoorWidth, DoorHeight, DoorColor);
        backDoor.OpenAngle = 1.75f;
        doors.Add((backDoor.GlobalPosition, backDoor));

        doors.Add((ToWorld(root, new Vector3(2.5f, 0, -Depth / 2)), AddDoor(
            root, "GarageDoor", new Vector3(2.5f, 2.25f, -Depth / 2), 0f, 3.0f, 2.2f, GarageDoorColor, garage: true)));

        doors.Add((ToWorld(root, new Vector3(2.2f, 0, -2.0f)), AddDoor(
            root, "GarageInnerDoor", new Vector3(2.2f, 0f, -2.0f), -Mathf.Pi / 2, InnerDoorWidth, DoorHeight, DoorColor)));

        doors.Add((ToWorld(root, new Vector3(-2.2f, 0, 3.4f)), AddDoor(
            root, "KitchenDoor", new Vector3(-2.2f, 0f, 3.4f), Mathf.Pi / 2, InnerDoorWidth, DoorHeight, DoorColor)));

        return doors;
    }

    /// <summary>Перемычка над проёмом: от верха двери до потолка, а не навстречу голове.</summary>
    private static void LintelAt(Node3D parent, float fixedCoord, float centerAlong, float width, float openingHeight,
        bool horizontal)
    {
        float height = WallHeight - openingHeight;
        var size = horizontal
            ? new Vector3(width, height, Thickness)
            : new Vector3(Thickness, height, width);
        var center = horizontal
            ? new Vector3(centerAlong, openingHeight + height / 2, fixedCoord)
            : new Vector3(fixedCoord, openingHeight + height / 2, centerAlong);

        Slab(parent, size, center, Wall);
    }

    private static Door AddDoor(Node3D root, string name, Vector3 position, float yaw, float width, float height,
        Color color, bool garage = false)
    {
        var door = Door.Create(name, Vector3.Zero, yaw, width, height, color, garage);
        door.Position = position;
        root.AddChild(door);
        DressDoor(door);
        return door;
    }

    private static Vector3 ToWorld(Node3D root, Vector3 local) =>
        root.ToGlobal(local);

    // ---------------------------------------------------------------- стены

    private static void WallX(Node3D parent, float z, float fromX, float toX, Color? color = null)
    {
        float[] windows = z == -4f ? new[] { -3.7f, .8f } : z == 4f ? new[] { 1.2f } : System.Array.Empty<float>();
        WallWithWindows(parent, z, fromX, toX, true, windows, 1.55f, 1.1f, 1f, color ?? Wall);
    }
    private static void WallZ(Node3D parent, float x, float fromZ, float toZ, Color? color = null)
    {
        float[] windows = x == -5.5f ? new[] { .6f } : x == 5.5f ? new[] { -2.6f } : System.Array.Empty<float>();
        WallWithWindows(parent, x, fromZ, toZ, false, windows,
            x == 5.5f ? 1.6f : 1.55f, x == 5.5f ? 1f : 1.1f, x == 5.5f ? .9f : 1f, color ?? Wall);
    }
    private static void AddWindowX(Node3D parent, float z, float x, float y, float width, float height) =>
        WindowGlass(parent, new Vector3(x,y,z), new Vector3(width,height,.035f));
    private static void AddWindowZ(Node3D parent, float x, float z, float y, float width, float height) =>
        WindowGlass(parent, new Vector3(x,y,z), new Vector3(.035f,height,width));

    private static void BuildRoof(Node3D parent)
    {
        AddHouseModel(parent, "home_roof");
        float halfDepth = Depth / 2 + .5f;
        float rise = RidgeHeight - EavesHeight;
        float slope = Mathf.Sqrt(halfDepth * halfDepth + rise * rise);
        float angle = Mathf.Atan2(rise, halfDepth);
        for (int side = -1; side <= 1; side += 2)
        {
            var roof = Blocker(parent, new Vector3(Width+.8f, .12f, slope),
                new Vector3(0, (EavesHeight+RidgeHeight)/2, side*halfDepth/2));
            roof.Rotation = new Vector3(side*angle, 0, 0);
            Blocker(parent, new Vector3(Width, 1.9f, .2f), new Vector3(0, 3.75f, side*3.4f));
            Blocker(parent, new Vector3(.2f, 3.4f, Depth), new Vector3(side*5.3f, 4.5f, 0));
        }
    }

    private static StaticBody3D Blocker(Node3D parent, Vector3 size, Vector3 position)
    {
        var body = new StaticBody3D { Name = "Blocker", Position = position };
        body.AddChild(new CollisionShape3D { Shape = new BoxShape3D { Size = size } });
        parent.AddChild(body);
        return body;
    }

    /// <summary>Погреб за домом: пристройка с полками, вход со двора.</summary>
    private static void BuildCellar(Node3D root)
    {
        const float width = 4.4f;
        const float depth = 3.2f;
        const float height = 2.1f;
        float z = Depth / 2 + depth / 2 + 1.2f;

        Slab(root, new Vector3(width, 0.2f, depth), new Vector3(0, -0.1f, z), Floor);
        Blocker(root, new Vector3(width, 0.12f, depth), new Vector3(0, height + .18f, z));

        Slab(root, new Vector3(width, height, 0.25f), new Vector3(0, height / 2, z + depth / 2), Wall);
        Slab(root, new Vector3(0.25f, height, depth), new Vector3(-width / 2, height / 2, z), Wall);
        Slab(root, new Vector3(0.25f, height, depth), new Vector3(width / 2, height / 2, z), Wall);

        // Вход со стороны дома: две стены с проёмом 1.2 м.
        float door = 1.2f;
        float half = width / 2;
        Slab(root, new Vector3(half - door / 2, height, 0.25f),
            new Vector3(-(door / 2 + (half - door / 2) / 2), height / 2, z - depth / 2), Wall);
        Slab(root, new Vector3(half - door / 2, height, 0.25f),
            new Vector3(door / 2 + (half - door / 2) / 2, height / 2, z - depth / 2), Wall);

        // Полки и банки входят в home_furniture, свет остаётся в сцене.
        AddLight(root, new Vector3(0, 1.8f, z), 1.2f);
    }

    private static void BuildFurniture(Node3D parent)
    {
        AddHouseModel(parent, "home_furniture");
        AddFurniture(parent, "home_chest", "chest", new Vector3(-5.12f,0,2.9f), new Vector3(.65f,.85f,2.2f));
        AddFurniture(parent, "home_workbench", "workbench", new Vector3(4.3f,0,-.55f), new Vector3(2.2f,.85f,.7f));
        AddFurniture(parent, "home_bed", "bed", new Vector3(-3.6f,2.8f,-1.8f), new Vector3(1.9f,.35f,.9f), false);
        Blocker(parent, new Vector3(2.4f,.75f,.9f), new Vector3(-4,.4f,.5f));
        Blocker(parent, new Vector3(1.6f,.82f,1f), new Vector3(-1.4f,.41f,1.2f));
        Blocker(parent, new Vector3(.7f,.9f,.7f), new Vector3(-5.1f,.45f,3.7f));
    }

    public static Node3D? UseFurniture(Node3D house, string use) => house.FindChild("Use_" + use, true, false) as Node3D;

    private static void AddLight(Node3D parent, Vector3 position, float energy)
    {
        parent.AddChild(new OmniLight3D
        {
            Position = position,
            LightEnergy = energy,
            OmniRange = 8f,
            LightColor = new Color(1f, 0.9f, 0.72f),
            ShadowEnabled = false,
        });
    }

    /// <summary>Блок с коллизией: стена, пол, мебель.</summary>
    private static MeshInstance3D Slab(Node3D parent, Vector3 size, Vector3 position, Color color, bool solid = true)
    {
        var mesh = new MeshInstance3D
        {
            Mesh = new BoxMesh { Size = size },
            Position = position,
            MaterialOverride = HouseMaterial(color),
        };
        parent.AddChild(mesh);

        if (!solid) return mesh;

        var body = new StaticBody3D { Name = "Solid" };
        body.AddChild(new CollisionShape3D { Shape = new BoxShape3D { Size = size } });
        mesh.AddChild(body);

        return mesh;
    }
}
