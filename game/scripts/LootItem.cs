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
        Color color = def.Category switch
        {
            ItemCategory.Consumable => new Color(0.35f, 0.75f, 0.4f),
            ItemCategory.Part => new Color(0.55f, 0.58f, 0.62f),
            ItemCategory.Tool => new Color(0.35f, 0.5f, 0.8f),
            ItemCategory.Money => new Color(0.9f, 0.8f, 0.3f),
            _ => new Color(0.6f, 0.45f, 0.3f),
        };

        var mesh = new MeshInstance3D
        {
            Mesh = new BoxMesh { Size = new Vector3(0.28f, 0.28f, 0.28f) },
            Position = new Vector3(0, 0.18f, 0),
            MaterialOverride = new StandardMaterial3D { AlbedoColor = color, Roughness = 0.7f },
        };
        AddChild(mesh);

        // Коллизия в отдельном слое: нужна только для мыши, игрока не держит.
        var body = new StaticBody3D { CollisionLayer = 2, CollisionMask = 0 };
        body.AddChild(new CollisionShape3D
        {
            Shape = new BoxShape3D { Size = new Vector3(0.55f, 0.55f, 0.55f) },
            Position = new Vector3(0, 0.26f, 0),
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
