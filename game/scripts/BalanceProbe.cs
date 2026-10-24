using System;
using Godot;
using Krntija.Core;
using Krntija.Core.Parts;
using Krntija.Core.Vehicle;

namespace Krntija.Game;

/// <summary>
/// Числовой прогон баланса: полчаса смешанной езды без рендера.
/// Нужен, чтобы настраивать расход, износ и цены по цифрам, а не на глаз.
/// Запуск: `--headless -- --balance`.
/// </summary>
public static class BalanceProbe
{
    public static void Run()
    {
        var session = GameSession.NewGame();
        var vehicle = session.Vehicle;

        double fuelStart = vehicle.Fuel.Liters;
        double maxTemp = 0;

        const double dt = 1.0;
        const int seconds = 30 * 60;

        for (int t = 0; t < seconds; t++)
        {
            var (targetKmh, throttle, brake) = DriveCycle(t % 120);

            vehicle.EngineRunning = true;
            session.Step(dt, new VehicleSample(targetKmh / 3.6), new DriveInput(Throttle: throttle, Brake: brake));

            maxTemp = Math.Max(maxTemp, vehicle.EngineTempC);
        }

        double km = vehicle.OdometerKm;
        double fuelUsed = fuelStart - vehicle.Fuel.Liters;
        double per100 = km > 0.1 ? fuelUsed / km * 100 : 0;

        GD.Print("──────── БАЛАНС: 30 минут смешанной езды ────────");
        GD.Print($"пробег            {km,8:F1} км");
        GD.Print($"топливо           {fuelUsed,8:F2} л   ({per100:F1} л/100 км)");
        GD.Print($"остаток в баке    {vehicle.Fuel.Liters,8:F1} л из {vehicle.Fuel.CapacityLiters:F0}");
        GD.Print($"макс. температура {maxTemp,8:F0} °C");
        GD.Print($"износ двигателя   {(1 - vehicle.Parts[PartSlot.Engine].Condition) * 100,8:F1} %");
        GD.Print($"износ шины        {(1 - vehicle.Parts[PartSlot.WheelFrontLeft].Condition) * 100,8:F1} %");
        GD.Print($"износ тормозов    {(1 - vehicle.Parts[PartSlot.BrakesFront].Condition) * 100,8:F1} %");
        GD.Print($"топливо стоило бы {fuelUsed * 1.35,8:F0} €");
        GD.Print("────────────────────────────────────────────────");
    }

    /// <summary>Простой цикл: разгон, трасса, город, торможение, стоянка.</summary>
    private static (double TargetKmh, double Throttle, double Brake) DriveCycle(int second) => second switch
    {
        < 20 => (60, 1.0, 0),  // разгон
        < 70 => (60, 0.35, 0), // трасса
        < 85 => (45, 0.25, 0), // сброс до города
        < 100 => (40, 0.3, 0), // город
        < 110 => (0, 0, 1.0),  // торможение
        _ => (0, 0, 0),        // стоянка
    };
}
