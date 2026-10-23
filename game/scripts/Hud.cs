using Godot;

namespace Krntija.Game;

/// <summary>Отладочный HUD M0: скорость, топливо, температура, потребности, часы.</summary>
public partial class Hud : CanvasLayer
{
    public Car? Car { get; set; }

    private Label _label = null!;

    public override void _Ready()
    {
        _label = new Label
        {
            Position = new Vector2(20, 16),
            Text = "…",
        };
        _label.AddThemeFontSizeOverride("font_size", 18);
        _label.AddThemeColorOverride("font_color", new Color(0.96f, 0.94f, 0.86f));
        _label.AddThemeColorOverride("font_outline_color", new Color(0, 0, 0, 0.8f));
        _label.AddThemeConstantOverride("outline_size", 4);
        AddChild(_label);
    }

    public override void _Process(double delta)
    {
        if (Car is null) return;

        var vehicle = Car.Session.Vehicle;
        var survival = Car.Session.Survival;
        var systems = Car.Session.Systems;

        string ignition = vehicle.EngineRunning ? "ON" : "OFF";
        string heat = systems.Overheating ? "  !! ПЕРЕГРЕВ !!" : string.Empty;

        _label.Text =
            $"{Car.Kmh,5:F0} км/ч   передача {Car.GearLabel}   двигатель {ignition}{heat}\n" +
            $"Топливо {vehicle.Fuel.Liters,5:F1} / {vehicle.Fuel.CapacityLiters:F0} л ({vehicle.Fuel.Fraction * 100:F0}%)\n" +
            $"Температура {vehicle.EngineTempC,5:F0} °C   масло {vehicle.OilLevel * 100:F0}%\n" +
            $"Здоровье {survival.Health * 100,3:F0}%   голод {survival.Hunger * 100,3:F0}%   жажда {survival.Thirst * 100,3:F0}%\n" +
            $"{Car.Session.Clock}   [W/S/A/D] [V] зажигание [Space] ручник";
    }
}
