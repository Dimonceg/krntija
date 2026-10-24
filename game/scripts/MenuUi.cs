using System;
using System.Collections.Generic;
using Godot;
using Krntija.Core.Vehicle;

namespace Krntija.Game;

/// <summary>
/// Главное меню и пауза. Живёт поверх игры, работает при остановленном дереве
/// (ProcessMode = Always), поэтому кнопки нажимаются и на паузе.
/// </summary>
public partial class MenuUi : CanvasLayer
{
    public event Action<CarSpec>? NewGameRequested;
    public event Action? ContinueRequested;
    public event Action? ResumeRequested;
    public event Action? SaveRequested;
    public event Action? MainMenuRequested;
    public event Action? QuitRequested;
    public event Action? EscapePressed;

    private Control _root = null!;
    private VBoxContainer _buttons = null!;
    private VBoxContainer _column = null!;
    private Label _title = null!;
    private Label _subtitle = null!;
    private readonly List<Button> _carButtons = new();

    private bool _mainMode = true;
    private bool _hasSave;
    private bool _settingsMode;
    private bool _carSelectMode;

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

        _column = new VBoxContainer { CustomMinimumSize = new Vector2(380, 0) };
        _column.AddThemeConstantOverride("separation", 10);
        center.AddChild(_column);

        _title = new Label
        {
            Text = "KRNTIJA",
            HorizontalAlignment = HorizontalAlignment.Center,
        };
        _title.AddThemeFontSizeOverride("font_size", 56);
        _title.AddThemeColorOverride("font_color", new Color(0.93f, 0.86f, 0.66f));
        _column.AddChild(_title);

        _subtitle = new Label
        {
            Text = "ржавая машина, плохая дорога",
            HorizontalAlignment = HorizontalAlignment.Center,
        };
        _subtitle.AddThemeFontSizeOverride("font_size", 16);
        _subtitle.AddThemeColorOverride("font_color", new Color(0.7f, 0.68f, 0.6f));
        _column.AddChild(_subtitle);

        _column.AddChild(new Control { CustomMinimumSize = new Vector2(0, 18) });

        _buttons = new VBoxContainer();
        _buttons.AddThemeConstantOverride("separation", 8);
        _column.AddChild(_buttons);

        var hint = new Label
        {
            Text = "W/S — газ и тормоз, A/D — руль, E — выйти, J — журнал",
            HorizontalAlignment = HorizontalAlignment.Center,
        };
        hint.AddThemeFontSizeOverride("font_size", 13);
        hint.AddThemeColorOverride("font_color", new Color(0.55f, 0.53f, 0.48f));
        _column.AddChild(new Control { CustomMinimumSize = new Vector2(0, 18) });
        _column.AddChild(hint);
    }

    public override void _UnhandledInput(InputEvent @event)
    {
        if (@event is not InputEventKey { Pressed: true, Echo: false, Keycode: Key.Escape }) return;

        if (_settingsMode || _carSelectMode)
        {
            ShowMainMenu(_hasSave);
            return;
        }

        EscapePressed?.Invoke();
    }

    public void ShowMainMenu(bool hasSave)
    {
        _mainMode = true;
        _settingsMode = false;
        _carSelectMode = false;
        _hasSave = hasSave;
        _title.Text = "KRNTIJA";
        _subtitle.Text = "ржавая машина, плохая дорога";
        _column.CustomMinimumSize = new Vector2(380, 0);

        Clear();

        AddButton("Новая игра", ShowCarSelect);
        AddButton("Продолжить", () => ContinueRequested?.Invoke(), hasSave);
        AddButton("Настройки", ShowSettings);
        AddButton("Выход", () => QuitRequested?.Invoke());

        _root.Visible = true;
    }

    /// <summary>
    /// Экран выбора машины для новой игры. Слева — имя кнопкой, справа —
    /// характеристики: видно, чем «двойка» отличается от «стоеднаестки»,
    /// не залезая в документацию.
    /// </summary>
    public void ShowCarSelect()
    {
        _mainMode = false;
        _settingsMode = false;
        _carSelectMode = true;
        _title.Text = "НА ЧЁМ ПОЕДЕМ";
        _subtitle.Text = "четыре ведра, четыре характера";
        _column.CustomMinimumSize = new Vector2(640, 0);

        Clear();
        _carButtons.Clear();

        foreach (var spec in CarCatalog.All)
        {
            var car = spec;
            var button = AddCarRow(car, () => NewGameRequested?.Invoke(car));
            _carButtons.Add(button);
        }

        AddButton("Назад", () => ShowMainMenu(_hasSave));

        _root.Visible = true;
    }

    /// <summary>Сколько машин предложено — для автотеста и проверок.</summary>
    public int CarChoices => _carButtons.Count;

    /// <summary>Нажать кнопку машины программно: проверки без живой мыши.</summary>
    public void PressCar(int index)
    {
        if (index < 0 || index >= _carButtons.Count) return;
        _carButtons[index].EmitSignal(BaseButton.SignalName.Pressed);
    }

    public void ShowSettings()
    {
        _mainMode = false;
        _settingsMode = true;
        _carSelectMode = false;
        _title.Text = "НАСТРОЙКИ";
        _subtitle.Text = "звук, мышь, экран";
        _column.CustomMinimumSize = new Vector2(380, 0);

        Clear();

        AddSlider("Громкость", 0f, 1f, 0.01f, GameSettings.MasterVolume, value =>
        {
            GameSettings.MasterVolume = value;
            GameSettings.Apply();
            GameSettings.Save();
        });

        AddSlider("Чувствительность мыши", 0.2f, 3f, 0.05f, GameSettings.MouseSensitivity, value =>
        {
            GameSettings.MouseSensitivity = value;
            GameSettings.Save();
        });

        AddToggle("Полный экран", GameSettings.Fullscreen, value =>
        {
            GameSettings.Fullscreen = value;
            GameSettings.Apply();
            GameSettings.Save();
        });

        AddButton("Назад", () => ShowMainMenu(_hasSave));

        _root.Visible = true;
    }

    public void ShowPause()
    {
        _mainMode = false;
        _settingsMode = false;
        _carSelectMode = false;
        _title.Text = "ПАУЗА";
        _subtitle.Text = "дорога никуда не денется";
        _column.CustomMinimumSize = new Vector2(380, 0);

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

    private Button AddButton(string text, Action action, bool enabled = true)
    {
        var button = new Button
        {
            Text = text,
            Disabled = !enabled,
            CustomMinimumSize = new Vector2(0, 46),
        };
        button.AddThemeFontSizeOverride("font_size", 20);
        button.Pressed += () =>
        {
            Sfx.PlayUi();
            action();
        };
        _buttons.AddChild(button);
        return button;
    }

    /// <summary>Строка выбора машины: кнопка с именем и подпись с характеристиками.</summary>
    private Button AddCarRow(CarSpec spec, Action onPressed)
    {
        var row = new HBoxContainer();
        row.AddThemeConstantOverride("separation", 16);

        var button = new Button
        {
            Text = spec.Name,
            CustomMinimumSize = new Vector2(300, 46),
            SizeFlagsHorizontal = Control.SizeFlags.ShrinkBegin,
            ClipText = true,
        };
        button.AddThemeFontSizeOverride("font_size", 19);
        button.Pressed += () =>
        {
            Sfx.PlayUi();
            onPressed();
        };
        row.AddChild(button);

        var info = new Label
        {
            Text = CarSpecLine(spec),
            VerticalAlignment = VerticalAlignment.Center,
        };
        info.AddThemeFontSizeOverride("font_size", 15);
        info.AddThemeColorOverride("font_color", new Color(0.72f, 0.70f, 0.62f));
        row.AddChild(info);

        _buttons.AddChild(row);
        return button;
    }

    /// <summary>Характеристики в человеческих единицах: лошади, километры, литры.</summary>
    private static string CarSpecLine(CarSpec spec)
    {
        int hp = (int)Math.Round(spec.PowerKw * 1.35962);
        int kmh = (int)Math.Round(spec.TopSpeedMps * 3.6);
        return $"{hp} л.с. · {spec.ChassisMassKg:F0} кг · {kmh} км/ч · {GearsText(spec.GearRatios.Count)} · бак {spec.FuelCapacityL:F0} л";
    }

    private static string GearsText(int count)
    {
        int tail = count % 100;
        int last = count % 10;
        string word = tail is >= 11 and <= 14 ? "передач"
            : last == 1 ? "передача"
            : last is >= 2 and <= 4 ? "передачи"
            : "передач";
        return $"{count} {word}";
    }

    private void AddSlider(string label, float min, float max, float step, float value, Action<float> onChanged)
    {
        var row = new HBoxContainer { CustomMinimumSize = new Vector2(0, 40) };
        row.AddThemeConstantOverride("separation", 12);

        var name = new Label { Text = label, CustomMinimumSize = new Vector2(210, 0) };
        name.AddThemeFontSizeOverride("font_size", 16);
        row.AddChild(name);

        var slider = new HSlider
        {
            MinValue = min,
            MaxValue = max,
            Step = step,
            Value = value,
            CustomMinimumSize = new Vector2(150, 0),
            SizeFlagsVertical = Control.SizeFlags.ShrinkCenter,
        };
        slider.ValueChanged += v => onChanged((float)v);
        row.AddChild(slider);

        _buttons.AddChild(row);
    }

    private void AddToggle(string label, bool value, Action<bool> onChanged)
    {
        var toggle = new CheckButton
        {
            Text = label,
            ButtonPressed = value,
            CustomMinimumSize = new Vector2(0, 40),
        };
        toggle.AddThemeFontSizeOverride("font_size", 18);
        toggle.Toggled += pressed => onChanged(pressed);
        _buttons.AddChild(toggle);
    }
}
