using System;
using Godot;

namespace Krntija.Game;

/// <summary>
/// Главное меню и пауза. Живёт поверх игры, работает при остановленном дереве
/// (ProcessMode = Always), поэтому кнопки нажимаются и на паузе.
/// </summary>
public partial class MenuUi : CanvasLayer
{
    public event Action? NewGameRequested;
    public event Action? ContinueRequested;
    public event Action? ResumeRequested;
    public event Action? SaveRequested;
    public event Action? MainMenuRequested;
    public event Action? QuitRequested;
    public event Action? EscapePressed;

    private Control _root = null!;
    private VBoxContainer _buttons = null!;
    private Label _title = null!;
    private Label _subtitle = null!;

    private bool _mainMode = true;

    public bool IsOpen => _root.Visible;

    public override void _Ready()
    {
        ProcessMode = ProcessModeEnum.Always;
        Layer = 100;

        _root = new Control { Visible = false };
        _root.SetAnchorsPreset(Control.LayoutPreset.FullRect);
        AddChild(_root);

        var dim = new ColorRect { Color = new Color(0.03f, 0.03f, 0.045f, 0.93f) };
        dim.SetAnchorsPreset(Control.LayoutPreset.FullRect);
        _root.AddChild(dim);

        var center = new CenterContainer();
        center.SetAnchorsPreset(Control.LayoutPreset.FullRect);
        _root.AddChild(center);

        var column = new VBoxContainer { CustomMinimumSize = new Vector2(380, 0) };
        column.AddThemeConstantOverride("separation", 10);
        center.AddChild(column);

        _title = new Label
        {
            Text = "KRNTIJA",
            HorizontalAlignment = HorizontalAlignment.Center,
        };
        _title.AddThemeFontSizeOverride("font_size", 56);
        _title.AddThemeColorOverride("font_color", new Color(0.93f, 0.86f, 0.66f));
        column.AddChild(_title);

        _subtitle = new Label
        {
            Text = "ржавая машина, плохая дорога",
            HorizontalAlignment = HorizontalAlignment.Center,
        };
        _subtitle.AddThemeFontSizeOverride("font_size", 16);
        _subtitle.AddThemeColorOverride("font_color", new Color(0.7f, 0.68f, 0.6f));
        column.AddChild(_subtitle);

        column.AddChild(new Control { CustomMinimumSize = new Vector2(0, 18) });

        _buttons = new VBoxContainer();
        _buttons.AddThemeConstantOverride("separation", 8);
        column.AddChild(_buttons);

        var hint = new Label
        {
            Text = "W/S — газ и тормоз, A/D — руль, E — выйти, J — журнал",
            HorizontalAlignment = HorizontalAlignment.Center,
        };
        hint.AddThemeFontSizeOverride("font_size", 13);
        hint.AddThemeColorOverride("font_color", new Color(0.55f, 0.53f, 0.48f));
        column.AddChild(new Control { CustomMinimumSize = new Vector2(0, 18) });
        column.AddChild(hint);
    }

    public override void _UnhandledInput(InputEvent @event)
    {
        if (@event is InputEventKey { Pressed: true, Echo: false, Keycode: Key.Escape })
            EscapePressed?.Invoke();
    }

    public void ShowMainMenu(bool hasSave)
    {
        _mainMode = true;
        _title.Text = "KRNTIJA";
        _subtitle.Text = "ржавая машина, плохая дорога";

        Clear();

        AddButton("Новая игра", () => NewGameRequested?.Invoke());
        AddButton("Продолжить", () => ContinueRequested?.Invoke(), hasSave);
        AddButton("Выход", () => QuitRequested?.Invoke());

        _root.Visible = true;
    }

    public void ShowPause()
    {
        _mainMode = false;
        _title.Text = "ПАУЗА";
        _subtitle.Text = "дорога никуда не денется";

        Clear();

        AddButton("Продолжить", () => ResumeRequested?.Invoke());
        AddButton("Сохранить", () => SaveRequested?.Invoke());
        AddButton("В главное меню", () => MainMenuRequested?.Invoke());
        AddButton("Выход", () => QuitRequested?.Invoke());

        _root.Visible = true;
    }

    public void Close() => _root.Visible = false;

    public bool InMainMode => _mainMode;

    private void Clear()
    {
        foreach (var child in _buttons.GetChildren())
            child.QueueFree();
    }

    private void AddButton(string text, Action action, bool enabled = true)
    {
        var button = new Button
        {
            Text = text,
            Disabled = !enabled,
            CustomMinimumSize = new Vector2(0, 46),
        };
        button.AddThemeFontSizeOverride("font_size", 20);
        button.Pressed += action;
        _buttons.AddChild(button);
    }
}
