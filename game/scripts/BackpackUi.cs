using System;
using System.Linq;
using Godot;
using Krntija.Core.Items;

namespace Krntija.Game;

/// <summary>
/// Рюкзак и багажник: две колонки плиток с иконками. Клик по предмету
/// в кармане — использовать или переложить в багажник; клик в багажнике —
/// забрать обратно. Открывается по B, мышь при этом освобождается.
/// </summary>
public partial class BackpackUi : Control
{
    private GridContainer _pocket = null!;
    private GridContainer _trunk = null!;
    private GridContainer _home = null!;
    private VBoxContainer _homeBox = null!;
    private Label _title = null!;

    /// <summary>Клик по предмету: id и откуда — "pocket", "trunk", "home" или "to_home".</summary>
    public event Action<string, string>? Clicked;

    public bool IsOpen => Visible;

    public override void _Ready()
    {
        SetAnchorsAndOffsetsPreset(LayoutPreset.FullRect);
        Visible = false;
        MouseFilter = MouseFilterEnum.Ignore;

        var dim = new ColorRect { Color = new Color(0, 0, 0, 0.55f) };
        dim.SetAnchorsAndOffsetsPreset(LayoutPreset.FullRect);
        AddChild(dim);

        var center = new CenterContainer();
        center.SetAnchorsAndOffsetsPreset(LayoutPreset.FullRect);
        AddChild(center);

        var panel = new PanelContainer();
        panel.AddThemeStyleboxOverride("panel", new StyleBoxFlat
        {
            BgColor = new Color(0.08f, 0.08f, 0.09f, 0.96f),
            BorderColor = new Color(0.45f, 0.4f, 0.3f),
            BorderWidthLeft = 2, BorderWidthRight = 2, BorderWidthTop = 2, BorderWidthBottom = 2,
            CornerRadiusTopLeft = 6, CornerRadiusTopRight = 6, CornerRadiusBottomLeft = 6, CornerRadiusBottomRight = 6,
            ContentMarginLeft = 18, ContentMarginRight = 18, ContentMarginTop = 14, ContentMarginBottom = 14,
        });
        center.AddChild(panel);

        var column = new VBoxContainer { CustomMinimumSize = new Vector2(820, 0) };
        panel.AddChild(column);

        _title = new Label { Text = "Рюкзак" };
        _title.AddThemeFontSizeOverride("font_size", 22);
        column.AddChild(_title);

        var columns = new HBoxContainer();
        columns.AddThemeConstantOverride("separation", 24);
        column.AddChild(columns);

        var pocketBox = new VBoxContainer();
        pocketBox.AddChild(Title("Карман"));
        _pocket = new GridContainer { Columns = 6 };
        _pocket.AddThemeConstantOverride("h_separation", 8);
        pocketBox.AddChild(_pocket);
        columns.AddChild(pocketBox);

        var trunkBox = new VBoxContainer();
        trunkBox.AddChild(Title("Багажник"));
        _trunk = new GridContainer { Columns = 6 };
        _trunk.AddThemeConstantOverride("h_separation", 8);
        trunkBox.AddChild(_trunk);
        columns.AddChild(trunkBox);

        _homeBox = new VBoxContainer { Visible = false };
        _homeBox.AddChild(Title("Дом"));
        _home = new GridContainer { Columns = 6 };
        _home.AddThemeConstantOverride("h_separation", 8);
        _homeBox.AddChild(_home);
        columns.AddChild(_homeBox);

        var hint = new Label
        {
            Text = "Клик в кармане — использовать или переложить в багажник. "
                   + "Клик в багажнике — забрать, ПКМ — отправить домой. B или Esc — закрыть.",
            AutowrapMode = TextServer.AutowrapMode.WordSmart,
        };
        hint.AddThemeFontSizeOverride("font_size", 13);
        hint.Modulate = new Color(1, 1, 1, 0.65f);
        column.AddChild(hint);
    }

    private static Label Title(string text)
    {
        var label = new Label { Text = text };
        label.AddThemeFontSizeOverride("font_size", 16);
        label.Modulate = new Color(1, 1, 1, 0.8f);
        return label;
    }

    public void Toggle(Inventory pocket, Inventory trunk, Inventory? home = null)
    {
        Visible = !Visible;
        if (Visible) Refresh(pocket, trunk, home);
    }

    public void Close() => Visible = false;

    public void Refresh(Inventory pocket, Inventory trunk, Inventory? home = null)
    {
        string text = $"Рюкзак — {pocket.WeightKg:F1} / {pocket.MaxWeightKg:F0} кг"
                      + $"      Багажник — {trunk.WeightKg:F1} / {trunk.MaxWeightKg:F0} кг";

        if (home is not null) text += $"      Дом — {home.WeightKg:F1} / {home.MaxWeightKg:F0} кг";
        _title.Text = text;

        _homeBox.Visible = home is not null;

        Fill(_pocket, pocket, "pocket");
        Fill(_trunk, trunk, "trunk");
        if (home is not null) Fill(_home, home, "home");
    }

    private void Fill(GridContainer grid, Inventory bag, string source)
    {
        foreach (Node child in grid.GetChildren()) child.QueueFree();

        bool any = false;
        foreach (ItemStack stack in bag.Items)
        {
            grid.AddChild(Tile(stack, source));
            any = true;
        }

        if (!any)
        {
            var empty = new Label { Text = "пусто" };
            empty.Modulate = new Color(1, 1, 1, 0.45f);
            grid.AddChild(empty);
        }
    }

    private Control Tile(ItemStack stack, string source)
    {
        var button = new Button
        {
            CustomMinimumSize = new Vector2(98, 98),
            Flat = true,
            TooltipText = $"{stack.Definition.Name} — {stack.Definition.Value} €",
            ClipContents = true,
        };

        var column = new VBoxContainer { MouseFilter = MouseFilterEnum.Ignore };
        column.SetAnchorsAndOffsetsPreset(LayoutPreset.FullRect);

        var icon = new ItemIcon { Def = stack.Definition, CustomMinimumSize = new Vector2(54, 54) };
        column.AddChild(icon);

        string name = stack.Definition.Name;
        if (name.Length > 10) name = name[..9] + ".";

        column.AddChild(new Label
        {
            Text = stack.Count > 1 ? $"{name} ×{stack.Count}" : name,
            HorizontalAlignment = HorizontalAlignment.Center,
        });

        button.AddChild(column);
        button.Pressed += () => Clicked?.Invoke(stack.DefId, source);
        button.GuiInput += @event =>
        {
            if (@event is InputEventMouseButton { Pressed: true, ButtonIndex: MouseButton.Right })
                Clicked?.Invoke(stack.DefId, "to_home");
        };
        return button;
    }
}
