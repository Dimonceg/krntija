using System;
using System.Linq;
using Godot;
using Krntija.Core;
using Krntija.Core.Economy;
using Krntija.Core.Items;
using Krntija.Core.Parts;

namespace Krntija.Game;

public enum HudScreen
{
    None,
    Journal,
    Shop,
}

/// <summary>
/// HUD: строка состояния, подсказка взаимодействия, журнал (J) и лавка.
/// </summary>
public partial class Hud : CanvasLayer
{
    public Car? Car { get; set; }

    public HudScreen Screen { get; set; } = HudScreen.None;

    public string Objective { get; set; } = string.Empty;

    public string Prompt
    {
        get => _prompt.Text;
        set => _prompt.Text = value;
    }

    private Label _status = null!;
    private Label _prompt = null!;
    private Panel _panel = null!;
    private RichTextLabel _panelText = null!;
    private Label _toast = null!;
    private float _toastLeft;

    private static readonly PartSlot[] SlotOrder = Enum.GetValues<PartSlot>();
    private int _selected;

    public PartSlot SelectedSlot => SlotOrder[Mathf.Clamp(_selected, 0, SlotOrder.Length - 1)];

    public void MoveSelection(int delta) =>
        _selected = (_selected + delta + SlotOrder.Length) % SlotOrder.Length;

    public override void _Ready()
    {
        _status = new Label { Position = new Vector2(20, 16) };
        _status.AddThemeFontSizeOverride("font_size", 17);
        _status.AddThemeColorOverride("font_color", new Color(0.96f, 0.94f, 0.86f));
        _status.AddThemeColorOverride("font_outline_color", new Color(0, 0, 0, 0.85f));
        _status.AddThemeConstantOverride("outline_size", 5);
        AddChild(_status);

        _panel = new Panel
        {
            Visible = false,
            Position = new Vector2(20, 112),
            Size = new Vector2(640, 520),
        };
        _panel.AddThemeStyleboxOverride("panel", new StyleBoxFlat
        {
            BgColor = new Color(0.06f, 0.06f, 0.055f, 0.92f),
            BorderColor = new Color(0.45f, 0.36f, 0.2f),
            BorderWidthBottom = 2,
            BorderWidthTop = 2,
            BorderWidthLeft = 2,
            BorderWidthRight = 2,
        });
        AddChild(_panel);

        _panelText = new RichTextLabel
        {
            BbcodeEnabled = true,
            ScrollActive = true,
            Position = new Vector2(16, 12),
            Size = new Vector2(608, 496),
        };
        _panelText.AddThemeFontSizeOverride("normal_font_size", 13);
        _panelText.AddThemeFontSizeOverride("bold_font_size", 13);
        _panel.AddChild(_panelText);

        _prompt = new Label { HorizontalAlignment = HorizontalAlignment.Center };
        _prompt.AddThemeFontSizeOverride("font_size", 19);
        _prompt.AddThemeColorOverride("font_color", new Color(0.98f, 0.9f, 0.65f));
        _prompt.AddThemeColorOverride("font_outline_color", new Color(0, 0, 0, 0.9f));
        _prompt.AddThemeConstantOverride("outline_size", 5);
        AddChild(_prompt);

        _toast = new Label { Position = new Vector2(20, 104) };
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

        Vector2 viewport = GetViewport().GetVisibleRect().Size;
        _prompt.Size = new Vector2(viewport.X, 30);
        _prompt.Position = new Vector2(0, viewport.Y - 74);

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
            $"{Car.Kmh,5:F0} км/ч   {train.GearText} передача   {train.Rpm,4:F0} об/мин   двигатель {ignition}{heat}\n" +
            $"{vehicle.Spec.Name}   топливо {vehicle.Fuel.Liters,5:F1} / {vehicle.Fuel.CapacityLiters:F0} л   " +
            $"температура {vehicle.EngineTempC,4:F0} °C\n" +
            $"Здоровье {survival.Health * 100,3:F0}%   голод {survival.Hunger * 100,3:F0}%   " +
            $"жажда {survival.Thirst * 100,3:F0}%   {session.Wallet.Money:F0} €\n" +
            $"{session.Clock}   [W/S/A/D] [V] зажигание [E] выйти/сесть [J] журнал [C] вид [F5/F9] сейв";

        _panel.Visible = Screen != HudScreen.None;
        if (Screen != HudScreen.None)
            _panelText.Text = Screen == HudScreen.Journal ? BuildJournal(session) : BuildShop(session);
    }

    private string BuildJournal(GameSession session)
    {
        var sb = new System.Text.StringBuilder();
        var vehicle = session.Vehicle;
        var survival = session.Survival;

        sb.AppendLine($"[b]ЖУРНАЛ — {vehicle.Spec.Name}[/b]");
        sb.AppendLine($"{session.Clock}   пробег {vehicle.OdometerKm:F1} км   денег {session.Wallet.Money:F0} €");
        if (!string.IsNullOrEmpty(Objective))
            sb.AppendLine($"[color=#e8d9a8]Задача: {Objective}[/color]");
        sb.AppendLine();

        sb.AppendLine("[b]ДЕТАЛИ[/b]");
        int index = 0;
        foreach (var slot in SlotOrder)
        {
            if (!vehicle.Parts.TryGetValue(slot, out var part)) continue;

            string color = part.Condition switch
            {
                < 0.25 => "#d05a4a",
                < 0.55 => "#d6a34a",
                _ => "#9ac46a",
            };

            bool selected = index == _selected;
            string marker = selected ? "[color=#e8d9a8]>[/color] " : "  ";
            string name = selected ? $"[b]{part.Name}[/b]" : part.Name;

            sb.AppendLine($"{marker}{name,-22}[color={color}]{Bar(part.Condition)}[/color] {part.Condition * 100,3:F0}%");
            index++;
        }

        sb.AppendLine();
        sb.AppendLine($"[b]ИНВЕНТАРЬ[/b]  {session.Inventory.Items.Count()}/{session.Inventory.SlotCount} слотов, " +
                      $"{session.Inventory.WeightKg:F1} / {session.Inventory.MaxWeightKg:F0} кг");

        int slotNumber = 1;
        foreach (var item in session.Inventory.Items)
        {
            string extra = item.Liters > 0.01 ? $"  ({item.Liters:F1} л)" : string.Empty;
            string wear = item.Condition < 0.999 ? $"  износ {(1 - item.Condition) * 100:F0}%" : string.Empty;
            sb.AppendLine($"[b]{slotNumber}[/b]  {item.Definition.Name} ×{item.Count}{extra}{wear}");
            slotNumber++;
        }

        sb.AppendLine();
        sb.AppendLine($"[b]СОСТОЯНИЕ[/b]  здоровье {survival.Health * 100:F0}%, голод {survival.Hunger * 100:F0}%, " +
                      $"жажда {survival.Thirst * 100:F0}%, сон {survival.Fatigue * 100:F0}%");
        sb.AppendLine();
        sb.AppendLine("[color=#8a8578][↑/↓] выбрать деталь   [T] поставить   [R] изолента   [F] канистра[/color]");
        sb.AppendLine("[color=#8a8578][1..9] использовать предмет из списка   [J] закрыть[/color]");

        return sb.ToString();
    }

    private static string BuildShop(GameSession session)
    {
        var sb = new System.Text.StringBuilder();

        sb.AppendLine("[b]ЛАВКА У ДОРОГИ[/b]");
        sb.AppendLine($"Денег в кармане: [b]{session.Wallet.Money:F0} €[/b]");
        sb.AppendLine();

        int index = 1;
        foreach (var entry in TradeService.Shop)
        {
            var def = ItemCatalog.Get(entry.ItemId);
            sb.AppendLine($"[b]{index}[/b]   {def.Name,-18} {entry.Price,4:F0} €   ({def.WeightKg:F1} кг)");
            index++;
        }

        sb.AppendLine();
        sb.AppendLine("[b]У ТЕБЯ[/b]");
        foreach (var item in session.Inventory.Items)
            sb.AppendLine($"    {item.Definition.Name} ×{item.Count}");

        sb.AppendLine();
        sb.AppendLine("[b]СДАТЬ ДЕТАЛИ[/b]");
        var carried = session.Inventory.Items.Where(i => PartCatalog.All.ContainsKey(i.DefId)).ToList();
        if (carried.Count == 0)
        {
            sb.AppendLine("    нечего сдавать");
        }
        else
        {
            foreach (var item in carried)
                sb.AppendLine($"    {item.Definition.Name} — {PartCatalog.All[item.DefId].Price * 0.5:F0} €");
        }

        sb.AppendLine();
        sb.AppendLine("[color=#8a8578][1..6] купить   [S] сдать деталь   [J] закрыть[/color]");
        return sb.ToString();
    }

    private static string Bar(double value)
    {
        int filled = Mathf.RoundToInt((float)Mathf.Clamp(value, 0, 1) * 10);
        return new string('|', filled) + new string('.', 10 - filled);
    }
}
