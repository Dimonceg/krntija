using Godot;
using Krntija.Core.Items;

namespace Krntija.Game;

/// <summary>
/// Иконка предмета: цвет и форма зависят от категории. Пока это кодовая
/// заглушка — когда арт-модель принесёт PNG, достаточно подставить текстуру.
/// </summary>
public partial class ItemIcon : Control
{
    private ItemDef? _def;

    public ItemDef? Def
    {
        get => _def;
        set
        {
            _def = value;
            QueueRedraw();
        }
    }

    public override void _Draw()
    {
        var rect = new Rect2(Vector2.Zero, Size);

        // Фон плитки.
        DrawRect(rect, new Color(0.12f, 0.12f, 0.13f, 0.9f));

        if (_def is null) return;

        Color tint = CategoryColor(_def.Category);
        DrawRect(new Rect2(2, 2, Size.X - 4, Size.Y - 4), new Color(tint, 0.18f));

        var center = Size / 2;
        float s = Mathf.Min(Size.X, Size.Y);

        switch (_def.Category)
        {
            case ItemCategory.Consumable:
                DrawRect(new Rect2(center + new Vector2(-s * 0.16f, -s * 0.28f), new Vector2(s * 0.32f, s * 0.56f)), tint);
                DrawRect(new Rect2(center + new Vector2(-s * 0.08f, -s * 0.36f), new Vector2(s * 0.16f, s * 0.1f)), tint);
                break;

            case ItemCategory.Part:
                DrawCircle(center, s * 0.24f, tint);
                DrawCircle(center, s * 0.1f, new Color(0.12f, 0.12f, 0.13f));
                break;

            case ItemCategory.Tool:
                DrawLine(center + new Vector2(-s * 0.22f, s * 0.22f), center + new Vector2(s * 0.2f, -s * 0.2f), tint, s * 0.12f);
                DrawCircle(center + new Vector2(s * 0.22f, -s * 0.22f), s * 0.12f, tint);
                break;

            case ItemCategory.Money:
                DrawCircle(center, s * 0.24f, tint);
                DrawCircle(center, s * 0.16f, new Color(0.12f, 0.12f, 0.13f));
                break;

            default:
                var box = new Rect2(center + new Vector2(-s * 0.22f, -s * 0.18f), new Vector2(s * 0.44f, s * 0.36f));
                DrawRect(box, tint);
                DrawLine(box.Position with { X = center.X }, box.End with { X = center.X }, new Color(0.12f, 0.12f, 0.13f), 2f);
                break;
        }
    }

    public static Color CategoryColor(ItemCategory category) => category switch
    {
        ItemCategory.Consumable => new Color(0.4f, 0.78f, 0.45f),
        ItemCategory.Part => new Color(0.65f, 0.68f, 0.72f),
        ItemCategory.Tool => new Color(0.45f, 0.6f, 0.9f),
        ItemCategory.Money => new Color(0.95f, 0.82f, 0.35f),
        _ => new Color(0.72f, 0.56f, 0.36f),
    };
}
