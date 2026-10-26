using Krntija.Core.Parts;
using Krntija.Core.Vehicle;

namespace Krntija.Core;

/// <summary>
/// Деталь, которую на старте кладут рядом с машиной (ADR 0003, п. 5).
/// <paramref name="Spot"/> — номер места <c>PartSpot_N</c> в боксе; смещение
/// (X, Y, Z, Yaw) в координатах машины нужно, когда якорей в модели нет:
/// X вправо, Y вверх, −Z вперёд.
/// </summary>
public readonly record struct PartPlacement(
    PartSlot Slot,
    string DefId,
    int Spot,
    double X,
    double Y,
    double Z,
    double Yaw);

/// <summary>
/// Поза якоря из модели бокса в мировых координатах: сцена находит узел
/// <c>PartSpot_N</c> и отдаёт ядру; какая деталь на каком месте — решает ядро.
/// </summary>
public readonly record struct SpotPose(int Spot, double X, double Y, double Z, double Yaw);

/// <summary>
/// План старта: какие гнёзда пусты и какие детали лежат рядом. Считается в ядре,
/// сцена ставит тела на места или поворачивает смещения на угол машины
/// (ADR 0003, п. 5).
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
/// разобранная машина, детали лежат рядом. Какие слоты пусты и на каком месте
/// лежит деталь, решает ядро; сцена только ставит машину и тела по якорям дома.
/// </summary>
public static class StartScenario
{
    /// <summary>
    /// Zastava 101 в сборочном боксе: сняты двигатель, аккумулятор, радиатор,
    /// капот, крышка багажника и левая передняя дверь. Колёса на месте — машину
    /// можно толкать и домкратить, но не завести. Тяжёлое — на полу, мелкое
    /// (аккумулятор) — на столе: места 1 и 6 стоят на верстаке бокса.
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
            new PartPlacement(PartSlot.Engine, "engine_1_6", 2, 2.0, 0.45, -7.6, 0.35),
            new PartPlacement(PartSlot.Battery, "battery_std", 1, 6.0, 0.25, -7.4, -0.6),
            new PartPlacement(PartSlot.Radiator, "radiator_std", 3, 2.2, 0.20, -5.2, 1.1),
            new PartPlacement(PartSlot.Hood, "hood_101", 4, 5.2, 0.15, -8.7, 0.15),
            new PartPlacement(PartSlot.TrunkLid, "trunk_101", 5, 6.3, 0.15, -5.4, -1.25),
            new PartPlacement(PartSlot.DoorFrontLeft, "door_fl_101", 7, 1.5, 0.15, -6.6, 1.4),
        });

    /// <summary>План старта для машины; null — раскладки нет, старт как раньше.</summary>
    public static StartPlan? For(string specId) =>
        string.Equals(specId, ZastavaGarage.SpecId, StringComparison.OrdinalIgnoreCase)
            ? ZastavaGarage
            : null;

    /// <summary>
    /// Собрать стартовое состояние: снять детали с машины и положить их в мир.
    /// <paramref name="spots"/> — позы якорей <c>PartSpot_N</c> из модели дома;
    /// места нет в списке — деталь ложится по запасному смещению от точки
    /// <paramref name="x"/>, <paramref name="y"/>, <paramref name="z"/>
    /// (мировые координаты, <paramref name="yaw"/> — угол машины).
    /// Возвращает план или null, если старт обычный.
    /// </summary>
    public static StartPlan? Apply(GameSession session, double x, double y, double z, double yaw,
        IReadOnlyList<SpotPose>? spots = null)
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
            if (SpotAt(spots, placement.Spot) is { } pose)
            {
                session.LooseParts.Add(
                    placement.DefId, 1.0, pose.X, pose.Y, pose.Z, pose.Yaw, LooseParts.Garage);
                continue;
            }

            // Смещение в координатах машины → мировые: тот же поворот, что у Godot вокруг Y.
            double worldX = x + placement.X * cos + placement.Z * sin;
            double worldZ = z - placement.X * sin + placement.Z * cos;

            session.LooseParts.Add(
                placement.DefId, 1.0, worldX, y + placement.Y, worldZ, yaw + placement.Yaw, LooseParts.Garage);
        }

        return plan;
    }

    private static SpotPose? SpotAt(IReadOnlyList<SpotPose>? spots, int spot)
    {
        if (spots is null) return null;

        foreach (var pose in spots)
            if (pose.Spot == spot)
                return pose;

        return null;
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
