using System.Collections.Generic;
using Godot;

namespace Krntija.Game;

/// <summary>
/// Дом игрока у старта: зал, гостиная, кухня, гараж и чердак.
/// Строится кодом из стен с коллизией, лестницы и дверей на петлях.
/// Локальные координаты: X — вдоль фасада, Z — вглубь, перед дома смотрит в −Z.
/// </summary>
public static class PlayerHouse
{
    private const float Width = 11f;
    private const float Depth = 8f;
    private const float WallHeight = 2.7f;
    private const float Thickness = 0.24f;
    private const float RidgeHeight = 4.6f;

    // Проём двери: 2.1 м при росте игрока 1.78 — запас 32 см.
    private const float DoorHeight = 2.1f;
    private const float DoorWidth = 1.3f;
    private const float InnerDoorWidth = 1.1f;

    private static readonly Color Wall = new(0.78f, 0.74f, 0.64f);
    private static readonly Color WallInner = new(0.82f, 0.79f, 0.71f);
    private static readonly Color Floor = new(0.42f, 0.34f, 0.25f);
    private static readonly Color Roof = new(0.47f, 0.24f, 0.18f);
    private static readonly Color DoorColor = new(0.36f, 0.23f, 0.15f);
    private static readonly Color GarageDoorColor = new(0.55f, 0.55f, 0.52f);
    private static readonly Color Furniture = new(0.48f, 0.36f, 0.24f);
    private static readonly Color Glass = new(0.55f, 0.68f, 0.75f);
    private static readonly Color Frame = new(0.88f, 0.86f, 0.8f);

    /// <summary>Собрать дом. Возвращает двери вместе с мировыми точками для подсказок.</summary>
    public static List<(Vector3 Position, Door Door)> Build(Node3D parent, Vector3 origin, float heading)
    {
        var root = new Node3D { Name = "PlayerHouse", Position = origin, Rotation = new Vector3(0, heading, 0) };
        parent.AddChild(root);

        var doors = new List<(Vector3, Door)>();

        // --- фундамент и полы ---
        Slab(root, new Vector3(Width + 0.3f, 0.6f, Depth + 0.3f), new Vector3(0, -0.4f, 0), new Color(0.5f, 0.48f, 0.44f));
        Slab(root, new Vector3(Width, 0.2f, Depth), new Vector3(0, -0.1f, 0), Floor);
        Slab(root, new Vector3(Width, 0.2f, Depth), new Vector3(0, WallHeight, 0), Floor, solid: false);

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

        // Лестница на чердак: вдоль правой стены, поднимается к передней части.
        for (int i = 0; i < 12; i++)
        {
            float y = 0.11f + i * 0.225f;
            Slab(root, new Vector3(1.9f, 0.22f, 0.28f), new Vector3(4.2f, y, 3.4f - i * 0.26f), Floor);
        }

        // --- мебель ---
        BuildFurniture(root);

        // --- свет: интерьер без ламп был бы чёрным ---
        AddLight(root, new Vector3(0, 2.35f, -2.0f), 1.7f);
        AddLight(root, new Vector3(-3.4f, 2.35f, 1.4f), 1.5f);
        AddLight(root, new Vector3(4.0f, 2.35f, -2.0f), 1.2f);
        AddLight(root, new Vector3(0, 3.7f, 0.4f), 1.4f);

        // Пандус с газона на крыльцо: высота 12 см, чтобы игрок заходил пешком.
        var ramp = Slab(root, new Vector3(3.4f, 0.12f, 1.3f), new Vector3(-1.25f, -0.06f, -Depth / 2 - 2.1f),
            new Color(0.55f, 0.52f, 0.46f));
        ramp.Rotation = new Vector3(0.1f, 0, 0);

        // --- крыльцо и дорожка к дороге ---
        Slab(root, new Vector3(3.4f, 0.18f, 1.6f), new Vector3(-1.25f, -0.09f, -Depth / 2 - 0.9f), new Color(0.55f, 0.52f, 0.46f));
        Slab(root, new Vector3(3.4f, 0.12f, 0.5f), new Vector3(-1.25f, -0.13f, -Depth / 2 - 1.9f), new Color(0.55f, 0.52f, 0.46f), solid: false);
        Slab(root, new Vector3(1.5f, 0.06f, 6.5f), new Vector3(-1.25f, 0.0f, -Depth / 2 - 5.4f), new Color(0.62f, 0.58f, 0.5f), solid: false);

        // --- двери ---
        doors.Add((ToWorld(root, new Vector3(-1.9f, 0, -Depth / 2)), AddDoor(
            root, "FrontDoor", new Vector3(-1.9f, 0f, -Depth / 2), 0f, DoorWidth, DoorHeight, DoorColor)));

        doors.Add((ToWorld(root, new Vector3(-4.6f, 0, Depth / 2)), AddDoor(
            root, "BackDoor", new Vector3(-4.6f, 0f, Depth / 2), 0f, DoorWidth, DoorHeight, DoorColor)));

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
        return door;
    }

    private static Vector3 ToWorld(Node3D root, Vector3 local) =>
        root.ToGlobal(local);

    // ---------------------------------------------------------------- стены

    private static void WallX(Node3D parent, float z, float fromX, float toX, Color? color = null)
    {
        if (toX - fromX <= 0.01f) return;
        Slab(parent, new Vector3(toX - fromX, WallHeight, Thickness),
            new Vector3((fromX + toX) / 2, WallHeight / 2, z), color ?? Wall);
    }

    private static void WallZ(Node3D parent, float x, float fromZ, float toZ, Color? color = null)
    {
        if (toZ - fromZ <= 0.01f) return;
        Slab(parent, new Vector3(Thickness, WallHeight, toZ - fromZ),
            new Vector3(x, WallHeight / 2, (fromZ + toZ) / 2), color ?? Wall);
    }

    /// <summary>Окно на стене вдоль X: рама и стекло без коллизии.</summary>
    private static void AddWindowX(Node3D parent, float z, float x, float y, float width, float height)
    {
        Slab(parent, new Vector3(width, height, 0.06f), new Vector3(x, y, z), Glass, solid: false);
        Slab(parent, new Vector3(width + 0.14f, 0.1f, 0.1f), new Vector3(x, y + height / 2 + 0.05f, z), Frame, solid: false);
        Slab(parent, new Vector3(width + 0.14f, 0.1f, 0.1f), new Vector3(x, y - height / 2 - 0.05f, z), Frame, solid: false);
        Slab(parent, new Vector3(0.1f, height + 0.2f, 0.1f), new Vector3(x - width / 2 - 0.05f, y, z), Frame, solid: false);
        Slab(parent, new Vector3(0.1f, height + 0.2f, 0.1f), new Vector3(x + width / 2 + 0.05f, y, z), Frame, solid: false);
    }

    /// <summary>Окно на стене вдоль Z.</summary>
    private static void AddWindowZ(Node3D parent, float x, float z, float y, float width, float height)
    {
        Slab(parent, new Vector3(0.06f, height, width), new Vector3(x, y, z), Glass, solid: false);
        Slab(parent, new Vector3(0.1f, 0.1f, width + 0.14f), new Vector3(x, y + height / 2 + 0.05f, z), Frame, solid: false);
        Slab(parent, new Vector3(0.1f, 0.1f, width + 0.14f), new Vector3(x, y - height / 2 - 0.05f, z), Frame, solid: false);
        Slab(parent, new Vector3(0.1f, height + 0.2f, 0.1f), new Vector3(x, y, z - width / 2 - 0.05f), Frame, solid: false);
        Slab(parent, new Vector3(0.1f, height + 0.2f, 0.1f), new Vector3(x, y, z + width / 2 + 0.05f), Frame, solid: false);
    }

    private static void BuildRoof(Node3D parent)
    {
        float halfDepth = Depth / 2 + 0.4f;
        float slope = Mathf.Sqrt(halfDepth * halfDepth + (RidgeHeight - WallHeight) * (RidgeHeight - WallHeight));
        float angle = Mathf.Atan2(RidgeHeight - WallHeight, halfDepth);

        for (int side = -1; side <= 1; side += 2)
        {
            var slab = Slab(parent, new Vector3(Width + 0.8f, 0.18f, slope),
                new Vector3(0, (WallHeight + RidgeHeight) / 2, side * halfDepth / 2), Roof, solid: false);
            slab.Rotation = new Vector3(side * angle, 0, 0);
        }

        // Фронтоны: ступенчатый треугольник из трёх блоков вместо одного ящика.
        float h = RidgeHeight - WallHeight;
        for (int side = -1; side <= 1; side += 2)
        {
            for (int step = 0; step < 3; step++)
            {
                float stepHeight = h / 3f;
                float stepDepth = Depth * 0.5f * (1f - step * 0.3f);
                Slab(parent, new Vector3(Thickness, stepHeight, stepDepth),
                    new Vector3(side * Width / 2, WallHeight + stepHeight / 2 + step * stepHeight, 0), Wall, solid: false);
            }
        }
    }

    private static void BuildFurniture(Node3D parent)
    {
        // Кухня: столешница, плита, мойка.
        Slab(parent, new Vector3(2.6f, 0.85f, 0.65f), new Vector3(-4.0f, 0.42f, 2.5f), Furniture);
        Slab(parent, new Vector3(0.65f, 0.9f, 0.65f), new Vector3(-4.9f, 0.45f, 2.5f), new Color(0.25f, 0.25f, 0.26f));
        Slab(parent, new Vector3(0.6f, 0.06f, 0.5f), new Vector3(-4.9f, 0.93f, 2.5f), new Color(0.65f, 0.66f, 0.66f));
        Slab(parent, new Vector3(2.6f, 0.6f, 0.06f), new Vector3(-4.0f, 1.9f, 2.84f), Furniture, solid: false);

        // Гостиная: стол, четыре стула, диван.
        Slab(parent, new Vector3(1.6f, 0.08f, 1.0f), new Vector3(-1.4f, 0.78f, 1.2f), Furniture);
        for (int sx = -1; sx <= 1; sx += 2)
            for (int sz = -1; sz <= 1; sz += 2)
                Slab(parent, new Vector3(0.12f, 0.74f, 0.12f), new Vector3(-1.4f + sx * 0.65f, 0.37f, 1.2f + sz * 0.4f), Furniture);

        Slab(parent, new Vector3(2.4f, 0.5f, 0.9f), new Vector3(-4.0f, 0.3f, 0.5f), new Color(0.36f, 0.28f, 0.22f));
        Slab(parent, new Vector3(2.4f, 0.6f, 0.25f), new Vector3(-4.0f, 0.55f, 0.1f), new Color(0.36f, 0.28f, 0.22f));

        // Гараж: верстак и пара ящиков.
        Slab(parent, new Vector3(2.2f, 0.85f, 0.7f), new Vector3(4.3f, 0.42f, -0.55f), new Color(0.4f, 0.32f, 0.24f));
        Slab(parent, new Vector3(0.6f, 0.6f, 0.6f), new Vector3(5.0f, 0.3f, -3.4f), new Color(0.45f, 0.36f, 0.2f));

        // Чердак: кровать и сундук.
        Slab(parent, new Vector3(1.9f, 0.35f, 0.9f), new Vector3(-3.6f, 2.95f, -1.8f), new Color(0.5f, 0.42f, 0.34f), solid: false);
        Slab(parent, new Vector3(0.9f, 0.5f, 0.6f), new Vector3(1.6f, 3.0f, -2.6f), Furniture, solid: false);
    }

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
            MaterialOverride = new StandardMaterial3D { AlbedoColor = color, Roughness = 0.92f },
        };
        parent.AddChild(mesh);

        if (!solid) return mesh;

        var body = new StaticBody3D { Name = "Solid" };
        body.AddChild(new CollisionShape3D { Shape = new BoxShape3D { Size = size } });
        mesh.AddChild(body);

        return mesh;
    }
}
