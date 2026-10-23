using System;
using System.Linq;
using Godot;
using Krntija.Core;
using Krntija.Core.Parts;

namespace Krntija.Game;

/// <summary>
/// HUD: строка состояния всегда на экране, журнал (J) — поверх,
/// с деталями, инвентарём и подсказками.
/// </summary>
public partial class Hud : CanvasLayer
{
    public Car? Car { get; set; }

    public bool JournalVisible
    {
        get => _journalPanel.Visible;
        set => _journalPanel.Visible = value;
    }

    private Label _status = null!;
    private Panel _journalPanel = null!;
    private RichTextLabel _journal = null!;
    private Label _toast = null!;
    private float _toastLeft;

    public override void _Ready()
    {
        _status = new Label { Position = new Vector2(20, 16) };
        _status.AddThemeFontSizeOverride("font_size", 17);
        _status.AddThemeColorOverride("font_color", new Color(0.96f, 0.94f, 0.86f));
        _status.AddThemeColorOverride("font_outline_color", new Color(0, 0, 0, 0.85f));
        _status.AddThemeConstantOverride("outline_size", 5);
        AddChild(_status);

        _journalPanel = new Panel
        {
            Visible = false,
            Position = new Vector2(20, 120),
            Size = new Vector2(560, 560),
        };
        var style = new StyleBoxFlat
        {
            BgColor = new Color(0.06f, 0.06f, 0.055f, 0.92f),
            BorderColor = new Color(0.45f, 0.36f, 0.2f),
            BorderWidthBottom = 2,
            BorderWidthTop = 2,
            BorderWidthLeft = 2,
            BorderWidthRight = 2,
            ContentMarginLeft = 16,
            ContentMarginTop = 12,
            ContentMarginRight = 16,
            ContentMarginBottom = 12,
        };
        _journalPanel.AddThemeStyleboxOverride("panel", style);
        AddChild(_journalPanel);

        _journal = new RichTextLabel
        {
            BbcodeEnabled = true,
            FitContent = false,
            ScrollActive = true,
            Position = new Vector2(16, 12),
            Size = new Vector2(528, 536),
        };
        _journal.AddThemeFontSizeOverride("normal_font_size", 15);
        _journal.AddThemeFontSizeOverride("bold_font_size", 15);
        _journalPanel.AddChild(_journal);

        _toast = new Label { Position = new Vector2(20, 90) };
        _toast.AddThemeFontSizeOverride("font_size", 16);
        _toast.AddThemeColorOverride("font_color", new Color(0.95f, 0.82f, 0.45f));
        _toast.AddThemeColorOverride("font_outline_color", new Color(0, 0, 0, 0.9f));
        _toast.AddThemeConstantOverride("outline_size", 5);
        AddChild(_toast);
    }

    public void Toast(string message)
    {
        _toast.Text = message;
        _toastLeft = 2.5f;
    }

    public override void _Process(double delta)
    {
        if (Car is null) return;

        if (_toastLeft > 0)
        {
            _toastLeft -= (float)delta;
            _toast.Modulate = new Color(1, 1, 1, Mathf.Clamp(_toastLeft, 0, 1));
            if (_toastLeft <= 0) _toast.Text = string.Empty;
        }

        var session = Car.Session;
        var vehicle = session.Vehicle;
        var survival = session.Survival;
        var train = session.DriveTrain;

        string ignition = vehicle.EngineRunning ? "ON" : "OFF";
        string heat = session.Systems.Overheating ? "  !! ПЕРЕГРЕВ !!" : string.Empty;

        _status.Text =
            $"{Car.Kmh,5:F0} км/ч   {train.Gear} передача   {train.Rpm,4:F0} об/мин   двигатель {ignition}{heat}\n" +
            $"Топливо {vehicle.Fuel.Liters,5:F1} / {vehicle.Fuel.CapacityLiters:F0} л   " +
            $"температура {vehicle.EngineTempC,4:F0} °C   {vehicle.Spec.Name}\n" +
            $"Здоровье {survival.Health * 100,3:F0}%   голод {survival.Hunger * 100,3:F0}%   жажда {survival.Thirst * 100,3:F0}%\n" +
            $"{session.Clock}   [W/S/A/D] [V] зажигание [J] журнал [F5] сохранить [F9] загрузить";

        if (_journalPanel.Visible)
            _journal.Text = BuildJournal(session);
    }

    private static string BuildJournal(GameSession session)
    {
        var sb = new System.Text.StringBuilder();
        var vehicle = session.Vehicle;
        var survival = session.Survival;

        sb.AppendLine($"[b]ЖУРНАЛ — {vehicle.Spec.Name}[/b]");
        sb.AppendLine($"{session.Clock}    пробег {vehicle.OdometerKm:F1} км    топливо {vehicle.Fuel.Fraction * 100:F0}%");
        sb.AppendLine();

        sb.AppendLine("[b]ДЕТАЛИ[/b]");
        foreach (var slot in Enum.GetValues<PartSlot>())
        {
            if (!vehicle.Parts.TryGetValue(slot, out var part)) continue;

            string color = part.Condition switch
            {
                < 0.25 => "#d05a4a",
                < 0.55 => "#d6a34a",
                _ => "#9ac46a",
            };

            sb.AppendLine($"{part.Name,-20} [color={color}]{Bar(part.Condition)}[/color] {part.Condition * 100,3:F0}%");
        }

        sb.AppendLine();
        sb.AppendLine($"[b]ИНВЕНТАРЬ[/b]  {session.Inventory.Items.Count()}/{session.Inventory.SlotCount} слотов, " +
                      $"{session.Inventory.WeightKg:F1} / {session.Inventory.MaxWeightKg:F0} кг");

        foreach (var item in session.Inventory.Items)
        {
            string extra = item.Liters > 0.01 ? $"  ({item.Liters:F1} л)" : string.Empty;
            string wear = item.Condition < 0.999 ? $"  износ {(1 - item.Condition) * 100:F0}%" : string.Empty;
            sb.AppendLine($"{item.Definition.Name} ×{item.Count}{extra}{wear}");
        }

        sb.AppendLine();
        sb.AppendLine($"[b]СОСТОЯНИЕ[/b]  здоровье {survival.Health * 100:F0}%, голод {survival.Hunger * 100:F0}%, " +
                      $"жажда {survival.Thirst * 100:F0}%, сон {survival.Fatigue * 100:F0}%");
        sb.AppendLine();
        sb.AppendLine("[color=#8a8578][R] подмотать худшую деталь изолентой   [F] залить из канистры   [J] закрыть[/color]");

        return sb.ToString();
    }

    private static string Bar(double value)
    {
        int filled = Mathf.RoundToInt((float)Mathf.Clamp(value, 0, 1) * 10);
        return new string('|', filled) + new string('.', 10 - filled);
    }
}
