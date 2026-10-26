using System;
using System.Collections.Generic;
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
/// HUD по углам экрана: скорость слева сверху, время и деньги справа,
/// состояние внизу слева, подсказка и сообщения внизу по центру.
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

    private Label _speed = null!;
    private Label _info = null!;
    private Label _vitals = null!;
    private Label _prompt = null!;
    private Label _hint = null!;
    private Label _objective = null!;
    private Label _toast = null!;
    private float _toastLeft;

    private Panel _panel = null!;
    private RichTextLabel _panelText = null!;

    private static readonly PartSlot[] SlotOrder = Enum.GetValues<PartSlot>();
    private int _selected;

    /// <summary>Какие детали сейчас на машине: снятых нет в списке (ADR 0003).</summary>
    private List<PartSlot> InstalledSlots()
    {
        var parts = Car?.Session.Vehicle.Parts;
        if (parts is null) return new List<PartSlot>();

        var installed = new List<PartSlot>(parts.Count);
        foreach (var slot in SlotOrder)
            if (parts.ContainsKey(slot))
                installed.Add(slot);

        return installed;
    }

    /// <summary>Выбранная в журнале деталь — только из установленных.</summary>
    public PartSlot SelectedSlot
    {
        get
        {
            var installed = InstalledSlots();
            if (installed.Count == 0) return PartSlot.Engine;

            return installed[Mathf.Clamp(_selected, 0, installed.Count - 1)];
        }
    }

    public void MoveSelection(int delta)
    {
        int count = Mathf.Max(1, InstalledSlots().Count);
        _selected = (_selected + delta + count) % count;
    }

    public override void _Ready()
    {
        _speed = CornerLabel(0, 0, 20, 14, 20);
        _info = CornerLabel(0, 0, 20, 78, 16);
        _vitals = CornerLabel(0, 1, 20, -16, 15);
        _hint = CornerLabel(1, 1, -20, -16, 13);
        _hint.OffsetLeft = -560;
        _hint.HorizontalAlignment = HorizontalAlignment.Right;

        _objective = new Label
        {
            HorizontalAlignment = HorizontalAlignment.Center,
            AnchorsPreset = (int)Control.LayoutPreset.CenterTop,
            AnchorLeft = 0.5f,
            AnchorRight = 0.5f,
            OffsetLeft = -320,
            OffsetRight = 320,
            OffsetTop = 12,
            OffsetBottom = 40,
        };
        Style(_objective, 16, new Color(0.9f, 0.86f, 0.7f));
        AddChild(_objective);

        _prompt = new Label
        {
            HorizontalAlignment = HorizontalAlignment.Center,
            AnchorsPreset = (int)Control.LayoutPreset.CenterBottom,
            AnchorLeft = 0.5f,
            AnchorRight = 0.5f,
            AnchorTop = 1f,
            AnchorBottom = 1f,
            OffsetLeft = -320,
            OffsetRight = 320,
            // Две строки: действие и, если есть, что поставит U (A9).
            OffsetTop = -108,
            OffsetBottom = -46,
            VerticalAlignment = VerticalAlignment.Bottom,
        };
        Style(_prompt, 19, new Color(0.98f, 0.9f, 0.65f));
        AddChild(_prompt);

        _toast = new Label
        {
            HorizontalAlignment = HorizontalAlignment.Center,
            AnchorLeft = 0.5f,
            AnchorRight = 0.5f,
            AnchorTop = 1f,
            AnchorBottom = 1f,
            OffsetLeft = -360,
            OffsetRight = 360,
            OffsetTop = -112,
            OffsetBottom = -80,
        };
        Style(_toast, 16, new Color(0.95f, 0.82f, 0.45f));
        AddChild(_toast);

        BuildPanel();

        _hint.Text = "[E] действие   [J] журнал   [B] рюкзак   [M] карта   [C] вид   [F5/F9] сейв";
    }

    private Label CornerLabel(int right, int bottom, float dx, float dy, int size)
    {
        var label = new Label
        {
            AnchorLeft = right,
            AnchorRight = right,
            AnchorTop = bottom,
            AnchorBottom = bottom,
            OffsetLeft = right == 0 ? dx : dx - 420,
            OffsetTop = bottom == 0 ? dy : dy - 90,
            OffsetRight = right == 0 ? dx + 420 : dx,
            OffsetBottom = bottom == 0 ? dy + 40 : dy,
        };

        Style(label, size, new Color(0.96f, 0.94f, 0.86f));
        if (right == 1) label.HorizontalAlignment = HorizontalAlignment.Right;
        AddChild(label);
        return label;
    }

    private static void Style(Label label, int size, Color color)
    {
        label.AddThemeFontSizeOverride("font_size", size);
        label.AddThemeColorOverride("font_color", color);
        label.AddThemeColorOverride("font_outline_color", new Color(0, 0, 0, 0.9f));
        label.AddThemeConstantOverride("outline_size", 5);
    }

    private void BuildPanel()
    {
        _panel = new Panel
        {
            Visible = false,
            Position = new Vector2(20, 100),
            Size = new Vector2(640, 500),
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
            Size = new Vector2(608, 476),
        };
        _panelText.AddThemeFontSizeOverride("normal_font_size", 13);
        _panelText.AddThemeFontSizeOverride("bold_font_size", 13);
        _panel.AddChild(_panelText);
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

        string heat = session.Systems.Overheating ? "  ПЕРЕГРЕВ" : string.Empty;
        string ignition = vehicle.EngineRunning ? string.Empty : "  · заглушен";

        _speed.Text = $"{Car.Kmh:F0} км/ч\n{train.GearText} · {train.Rpm:F0} об/мин{ignition}{heat}";
        // Путь — по станции дороги из ядра, не по одометру: езда туда-обратно у дома
        // не поднимает ни тир, ни рекорд. HUD только читает.
        double toNext = Core.Journey.MetersToNextTier(session.RoadS);
        string nextTier = toNext <= 0 ? "тир максимум" : $"до тира {session.Tier + 1}: {toNext / 1000:F1} км";

        _info.Text = $"{session.Clock}\n{Money.Format(session.Wallet.Money)}   топливо {vehicle.Fuel.Liters:F1}/{vehicle.Fuel.CapacityLiters:F0} л"
            + $"\nДальше всего: {session.BestDistanceMeters / 1000:F1} км   {nextTier}";
        _vitals.Text = $"Здоровье {survival.Health * 100:F0}%   голод {survival.Hunger * 100:F0}%   "
                       + $"жажда {survival.Thirst * 100:F0}%   сон {survival.Fatigue * 100:F0}%";
        _objective.Text = Objective;

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
        sb.AppendLine($"{session.Clock}   пробег {vehicle.OdometerKm:F1} км   денег {Money.Format(session.Wallet.Money)}");
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
        sb.AppendLine($"Денег в кармане: [b]{Money.Format(session.Wallet.Money)}[/b]");
        sb.AppendLine();

        int index = 1;
        foreach (var entry in TradeService.Shop)
        {
            var def = ItemCatalog.Get(entry.ItemId);
            sb.AppendLine($"[b]{index}[/b]   {def.Name,-18} {Money.Format(entry.Price),6}   ({def.WeightKg:F1} кг)");
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
                sb.AppendLine($"    {item.Definition.Name} — {Money.Format(PartCatalog.All[item.DefId].Price * 0.5)}");
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
