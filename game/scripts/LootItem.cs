using Godot;
using Krntija.Core.Items;

namespace Krntija.Game;

/// <summary>
/// Предмет в мире: заглушка-кубик цвета категории. Когда арт-модель принесёт
/// GLB, достаточно заменить меш внутри, логика подбора не изменится.
/// </summary>
public partial class LootItem : Node3D
{
    private float _spin;

    public string ItemId { get; private set; } = string.Empty;
    public int Count { get; private set; } = 1;

    public static LootItem Create(string itemId, int count, Vector3 position)
    {
        var item = new LootItem { Name = $"Loot_{itemId}", ItemId = itemId, Count = count };
        item.BuildVisual();
        item.Position = position;
        return item;
    }

    private void BuildVisual()
    {
        var def = ItemCatalog.Get(ItemId);

        // Если арт-модель положила GLB — берём её, иначе собираем заглушку.
        Node3D? authored = ModelLibrary.Instantiate($"res://assets/models/props/loot_{ItemId}.glb");
        if (authored is not null)
        {
            authored.Position = new Vector3(0, 0.18f, 0);
            AddChild(authored);
            BuildStand();
            return;
        }

        Color color = def.Category switch
        {
            ItemCategory.Consumable => new Color(0.35f, 0.75f, 0.4f),
            ItemCategory.Part => new Color(0.55f, 0.58f, 0.62f),
            ItemCategory.Tool => new Color(0.35f, 0.5f, 0.8f),
            ItemCategory.Money => new Color(0.9f, 0.8f, 0.3f),
            _ => new Color(0.6f, 0.45f, 0.3f),
        };

        var material = new StandardMaterial3D { AlbedoColor = color, Roughness = 0.7f, Metallic = 0.15f };
        float s = def.Category == ItemCategory.Tool ? 0.34f : 0.26f;

        switch (def.Category)
        {
            case ItemCategory.Consumable:
                AddPart(new CylinderMesh
                {
                    Height = s * 1.4f, TopRadius = s * 0.34f, BottomRadius = s * 0.4f,
                }, new Vector3(0, 0.22f, 0), material);
                AddPart(new CylinderMesh
                {
                    Height = s * 0.3f, TopRadius = s * 0.12f, BottomRadius = s * 0.14f,
                }, new Vector3(0, 0.22f + s * 0.8f, 0), material);
                break;

            case ItemCategory.Part:
                AddPart(new TorusMesh
                {
                    InnerRadius = s * 0.24f, OuterRadius = s * 0.48f, Rings = 12, RingSegments = 8,
                }, new Vector3(0, 0.26f, 0), material);
                AddPart(new CylinderMesh
                {
                    Height = s * 0.5f, TopRadius = s * 0.14f, BottomRadius = s * 0.14f,
                }, new Vector3(0, 0.26f, 0), material);
                break;

            case ItemCategory.Tool:
                AddPart(new BoxMesh { Size = new Vector3(s * 0.18f, s * 0.18f, s * 1.5f) },
                    new Vector3(0, 0.24f, 0), material);
                AddPart(new CylinderMesh
                {
                    Height = s * 0.16f, TopRadius = s * 0.26f, BottomRadius = s * 0.26f,
                }, new Vector3(0, 0.24f, s * 0.75f), material, new Vector3(90, 0, 0));
                break;

            case ItemCategory.Money:
                AddPart(new CylinderMesh
                {
                    Height = s * 0.1f, TopRadius = s * 0.42f, BottomRadius = s * 0.42f,
                }, new Vector3(0, 0.2f, 0), material);
                break;

            default:
                AddPart(new BoxMesh { Size = new Vector3(s * 1.2f, s * 0.9f, s * 1.2f) },
                    new Vector3(0, 0.2f + s * 0.45f, 0), material);
                AddPart(new BoxMesh { Size = new Vector3(s * 1.25f, s * 0.12f, s * 1.25f) },
                    new Vector3(0, 0.2f + s * 0.95f, 0), material);
                break;
        }

        BuildStand();
    }

    private void AddPart(Mesh mesh, Vector3 position, Material material, Vector3? rotationDegrees = null)
    {
        var instance = new MeshInstance3D
        {
            Mesh = mesh,
            Position = position,
            MaterialOverride = material,
        };

        if (rotationDegrees is { } degrees)
            instance.RotationDegrees = degrees;

        AddChild(instance);
    }

    private void BuildStand()
    {
        // Коллизия в отдельном слое: нужна только для мыши, игрока не держит.
        var body = new StaticBody3D { CollisionLayer = 2, CollisionMask = 0 };
        body.AddChild(new CollisionShape3D
        {
            Shape = new BoxShape3D { Size = new Vector3(0.6f, 0.6f, 0.6f) },
            Position = new Vector3(0, 0.28f, 0),
        });
        AddChild(body);

        // Тонкая подложка, чтобы предмет читался в траве.
        AddChild(new MeshInstance3D
        {
            Mesh = new CylinderMesh { Height = 0.02f, TopRadius = 0.24f, BottomRadius = 0.24f },
            Position = new Vector3(0, 0.01f, 0),
            MaterialOverride = new StandardMaterial3D { AlbedoColor = new Color(0.15f, 0.15f, 0.15f, 0.5f) },
        });
    }

    public override void _Process(double delta)
    {
        _spin += (float)delta * 1.6f;
        Rotation = new Vector3(0, _spin, 0);
    }
}
