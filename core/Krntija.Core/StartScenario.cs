using Krntija.Core.Parts;
using Krntija.Core.Vehicle;

namespace Krntija.Core;

/// <summary>
/// Деталь, которую на старте кладут рядом с машиной (ADR 0003, п. 5).
/// Смещение — в координатах машины: X вправо, Y вверх, −Z вперёд.
/// </summary>
public readonly record struct PartPlacement(
    PartSlot Slot,
    string DefId,
    double X,
    double Y,
    double Z,
    double Yaw);

/// <summary>
/// План старта: какие гнёзда пусты и какие детали лежат рядом. Считается в ядре,
/// сцена поворачивает смещения на угол машины и ставит тела (ADR 0003, п. 5).
/// </summary>
public sealed record StartPlan(
    string SpecId,
    IReadOnlyList<PartSlot> EmptySlots,
    IReadOnlyList<PartPlacement> LooseParts)
{
    public bool IsEmpty => EmptySlots.Count == 0 && LooseParts.Count == 0;
}

/// <summary>
/// Старт в гараже дома (ADR 0003, п. 5): новая игра на машине с раскладкой —
/// разобранная машина, детали лежат рядом. Какие слоты пусты и где лежат
/// детали, решает ядро; сцена только ставит машину и тела по якорям дома.
/// </summary>
public static class StartScenario
{
    /// <summary>
    /// Zastava 101 в гараже: сняты двигатель, аккумулятор, радиатор, капот,
    /// крышка багажника и левая передняя дверь. Колёса на месте — машину можно
    /// толкать и домкратить, но не завести. Детали лежат вокруг машины на полу.
    /// </summary>
    public static readonly StartPlan ZastavaGarage = new(
        "zastava_101",
        new[]
        {
            PartSlot.Engine, PartSlot.Battery, PartSlot.Radiator,
            PartSlot.Hood, PartSlot.TrunkLid, PartSlot.DoorFrontLeft,
        },
        new[]
        {
            // Спереди и по бокам от машины, во дворе: к дверям дома детали не кладём,
            // иначе подсказка у них спорит с дверью дома (проверено --selftest-assembly).
            new PartPlacement(PartSlot.Engine, "engine_1_6", 2.0, 0.45, -7.6, 0.35),
            new PartPlacement(PartSlot.Battery, "battery_std", 6.0, 0.25, -7.4, -0.6),
            new PartPlacement(PartSlot.Radiator, "radiator_std", 2.2, 0.20, -5.2, 1.1),
            new PartPlacement(PartSlot.Hood, "hood_101", 5.2, 0.15, -8.7, 0.15),
            new PartPlacement(PartSlot.TrunkLid, "trunk_101", 6.3, 0.15, -5.4, -1.25),
            new PartPlacement(PartSlot.DoorFrontLeft, "door_fl_101", 1.5, 0.15, -6.6, 1.4),
        });

    /// <summary>План старта для машины; null — раскладки нет, старт как раньше.</summary>
    public static StartPlan? For(string specId) =>
        string.Equals(specId, ZastavaGarage.SpecId, StringComparison.OrdinalIgnoreCase)
            ? ZastavaGarage
            : null;

    /// <summary>
    /// Собрать стартовое состояние: снять детали с машины и положить их в мир
    /// вокруг точки <paramref name="x"/>, <paramref name="y"/>, <paramref name="z"/>
    /// (мировые координаты, <paramref name="yaw"/> — угол машины). Возвращает план
    /// или null, если старт обычный.
    /// </summary>
    public static StartPlan? Apply(GameSession session, double x, double y, double z, double yaw)
    {
        var plan = For(session.Vehicle.Spec.Id);
        if (plan is null) return null;

        foreach (var slot in plan.EmptySlots)
        {
            session.Vehicle.Parts.Remove(slot);
            session.Vehicle.OpenSlots.Remove(slot);
        }

        double cos = Math.Cos(yaw);
        double sin = Math.Sin(yaw);

        foreach (var placement in plan.LooseParts)
        {
            // Смещение в координатах машины → мировые: тот же поворот, что у Godot вокруг Y.
            double worldX = x + placement.X * cos + placement.Z * sin;
            double worldZ = z - placement.X * sin + placement.Z * cos;

            session.LooseParts.Add(
                placement.DefId, 1.0, worldX, y + placement.Y, worldZ, yaw + placement.Yaw, LooseParts.Garage);
        }

        return plan;
    }

    /// <summary>Машину ещё надо собирать: есть пустое гнездо раскладки.</summary>
    public static bool NeedsAssembly(VehicleState vehicle)
    {
        var layout = CarLayout.For(vehicle.Spec.Id);
        if (layout is null) return false;

        foreach (var spec in layout.Slots)
            if (!vehicle.Parts.ContainsKey(spec.Slot))
                return true;

        return false;
    }

    /// <summary>Сколько гнёзд пусто — для подсказки «осталось поставить».</summary>
    public static int MissingCount(VehicleState vehicle)
    {
        var layout = CarLayout.For(vehicle.Spec.Id);
        if (layout is null) return 0;

        return layout.Slots.Count(spec => !vehicle.Parts.ContainsKey(spec.Slot));
    }
}
