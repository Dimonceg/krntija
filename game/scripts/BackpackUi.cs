using Godot;
using Krntija.Core.Items;

namespace Krntija.Game;

/// <summary>
/// Рюкзак: плитки всех предметов с иконками, вес и подсказка по управлению.
/// Открывается по B, мышь при этом освобождается.
/// </summary>
public partial class BackpackUi : Control
{
    private PanelContainer _panel = null!;
    private GridContainer _grid = null!;
    private Label _title = null!;
    private Label _hint = null!;

    public override void _Ready()
    {
        SetAnchorsAndOffsetsPreset(LayoutPreset.FullRect);
        Visible = false;
        MouseFilter = MouseFilterEnum.Ignore;

        // Затемнение и центрирование: размер панели задаёт её содержимое.
        var dim = new ColorRect { Color = new Color(0, 0, 0, 0.55f) };
        dim.SetAnchorsAndOffsetsPreset(LayoutPreset.FullRect);
        AddChild(dim);

        var center = new CenterContainer();
        center.SetAnchorsAndOffsetsPreset(LayoutPreset.FullRect);
        AddChild(center);

        _panel = new PanelContainer();

        var style = new StyleBoxFlat
        {
            BgColor = new Color(0.08f, 0.08f, 0.09f, 0.96f),
            BorderColor = new Color(0.45f, 0.4f, 0.3f),
            BorderWidthLeft = 2, BorderWidthRight = 2, BorderWidthTop = 2, BorderWidthBottom = 2,
            CornerRadiusTopLeft = 6, CornerRadiusTopRight = 6, CornerRadiusBottomLeft = 6, CornerRadiusBottomRight = 6,
            ContentMarginLeft = 18, ContentMarginRight = 18, ContentMarginTop = 14, ContentMarginBottom = 14,
        };
        _panel.AddThemeStyleboxOverride("panel", style);
        center.AddChild(_panel);

        var column = new VBoxContainer { CustomMinimumSize = new Vector2(520, 0) };
        _panel.AddChild(column);

        _title = new Label { Text = "Рюкзак" };
        _title.AddThemeFontSizeOverride("font_size", 22);
        column.AddChild(_title);

        _grid = new GridContainer { Columns = 6 };
        _grid.AddThemeConstantOverride("h_separation", 10);
        _grid.AddThemeConstantOverride("v_separation", 10);
        column.AddChild(_grid);

        _hint = new Label
        {
            Text = "B или Esc — закрыть. Предметы на земле: наведи мышь и щёлкни левой кнопкой.",
            AutowrapMode = TextServer.AutowrapMode.WordSmart,
        };
        _hint.AddThemeFontSizeOverride("font_size", 13);
        _hint.Modulate = new Color(1, 1, 1, 0.65f);
        column.AddChild(_hint);
    }

    public bool IsOpen => Visible;

    public void Toggle(Inventory inventory)
    {
        Visible = !Visible;
        if (Visible) Fill(inventory);
    }

    public void Close() => Visible = false;

    private void Fill(Inventory inventory)
    {
        foreach (Node child in _grid.GetChildren()) child.QueueFree();

        _title.Text = $"Рюкзак — {inventory.WeightKg:F1} / {inventory.MaxWeightKg:F0} кг";

        bool any = false;
        foreach (ItemStack stack in inventory.Items)
        {
            _grid.AddChild(Tile(stack));
            any = true;
        }

        if (!any)
        {
            var empty = new Label { Text = "Пусто. Поищи на заправке, в селе или дома." };
            empty.Modulate = new Color(1, 1, 1, 0.6f);
            _grid.AddChild(empty);
        }
    }

    private static Control Tile(ItemStack stack)
    {
        var box = new VBoxContainer { CustomMinimumSize = new Vector2(80, 0) };

        var icon = new ItemIcon { Def = stack.Definition, CustomMinimumSize = new Vector2(64, 64) };
        box.AddChild(icon);

        string name = stack.Definition.Name;
        if (name.Length > 11) name = name[..10] + ".";

        box.AddChild(new Label
        {
            Text = stack.Count > 1 ? $"{name} ×{stack.Count}" : name,
            HorizontalAlignment = HorizontalAlignment.Center,
        });

        return box;
    }
}
