using Krntija.Core.Parts;
using Krntija.Core.Vehicle;

namespace Krntija.Core;

/// <summary>Имена слотов в файлах и в арте — в одном месте, чтобы не расходились.</summary>
public static class PartSlotIds
{
    private static readonly Dictionary<PartSlot, string> Ids = new()
    {
        [PartSlot.Engine] = "engine",
        [PartSlot.Gearbox] = "gearbox",
        [PartSlot.Radiator] = "radiator",
        [PartSlot.Alternator] = "alternator",
        [PartSlot.Battery] = "battery",
        [PartSlot.BrakesFront] = "brakes_front",
        [PartSlot.BrakesRear] = "brakes_rear",
        [PartSlot.SuspensionFront] = "susp_front",
        [PartSlot.SuspensionRear] = "susp_rear",
        [PartSlot.WheelFrontLeft] = "wheel_fl",
        [PartSlot.WheelFrontRight] = "wheel_fr",
        [PartSlot.WheelRearLeft] = "wheel_rl",
        [PartSlot.WheelRearRight] = "wheel_rr",
        [PartSlot.Body] = "body",
        [PartSlot.DoorFrontLeft] = "door_fl",
        [PartSlot.DoorFrontRight] = "door_fr",
        [PartSlot.DoorRearLeft] = "door_rl",
        [PartSlot.DoorRearRight] = "door_rr",
        [PartSlot.Hood] = "hood",
        [PartSlot.TrunkLid] = "trunk",
    };

    private static readonly Dictionary<string, PartSlot> ById =
        Ids.ToDictionary(pair => pair.Value, pair => pair.Key, StringComparer.OrdinalIgnoreCase);

    /// <summary>Id слота для файлов арта и для <c>Car.SocketGlobal</c>.</summary>
    public static string Of(PartSlot slot) =>
        Ids.TryGetValue(slot, out var id)
            ? id
            : throw new ArgumentOutOfRangeException(nameof(slot), slot, "у слота нет id");

    /// <summary>Слот по id из файла. Неизвестный id — false, а не исключение.</summary>
    public static bool TryParse(string id, out PartSlot slot) => ById.TryGetValue(id, out slot);
}

/// <summary>Гнездо машины: открывается ли, нужен ли доступ под капот.</summary>
public sealed record SlotSpec(PartSlot Slot, bool Opens, bool NeedsAccess);

/// <summary>
/// Раскладка машины: какие слоты у неё физические. Нет раскладки — машина
/// работает как раньше: всё установлено, ничего не снимается.
/// </summary>
public sealed record CarLayout(string SpecId, IReadOnlyList<SlotSpec> Slots)
{
    public static readonly CarLayout Zastava101 = new("zastava_101", new[]
    {
        new SlotSpec(PartSlot.Engine, Opens: false, NeedsAccess: true),
        new SlotSpec(PartSlot.Radiator, Opens: false, NeedsAccess: true),
        new SlotSpec(PartSlot.Battery, Opens: false, NeedsAccess: true),
        new SlotSpec(PartSlot.WheelFrontLeft, Opens: false, NeedsAccess: false),
        new SlotSpec(PartSlot.WheelFrontRight, Opens: false, NeedsAccess: false),
        new SlotSpec(PartSlot.WheelRearLeft, Opens: false, NeedsAccess: false),
        new SlotSpec(PartSlot.WheelRearRight, Opens: false, NeedsAccess: false),
        new SlotSpec(PartSlot.DoorFrontLeft, Opens: true, NeedsAccess: false),
        new SlotSpec(PartSlot.DoorFrontRight, Opens: true, NeedsAccess: false),
        new SlotSpec(PartSlot.DoorRearLeft, Opens: true, NeedsAccess: false),
        new SlotSpec(PartSlot.DoorRearRight, Opens: true, NeedsAccess: false),
        new SlotSpec(PartSlot.Hood, Opens: true, NeedsAccess: false),
        new SlotSpec(PartSlot.TrunkLid, Opens: true, NeedsAccess: false),
    });

    /// <summary>Раскладка по id машины; null — гнёзд нет, машина как раньше.</summary>
    public static CarLayout? For(string specId) =>
        string.Equals(specId, Zastava101.SpecId, StringComparison.OrdinalIgnoreCase) ? Zastava101 : null;

    public SlotSpec? Find(PartSlot slot) => Slots.FirstOrDefault(spec => spec.Slot == slot);
}

public enum AssemblyResult
{
    Ok,
    Occupied,
    Empty,
    WrongPart,
    NeedsAccess,
    NotRemovable,
}

/// <summary>
/// Правила сборки и разборки машины (ADR 0003, п. 2). Сцена спрашивает, что
/// можно сделать, и показывает ответ; сама ничего не решает.
/// </summary>
public static class AssemblyRules
{
    /// <summary>Радиус примагничивания детали к гнезду, метры.</summary>
    public const double SnapDistance = 0.6;

    /// <summary>Скорость с деталью в руках не падает ниже этой доли.</summary>
    public const double MinCarrySpeedFactor = 0.35;

    /// <summary>Масса, при которой скорость с деталью падает вдвое, кг.</summary>
    public const double CarryMassHalfSpeedKg = 200;

    /// <summary>Во сколько раз быстрее греется мотор без радиатора.</summary>
    public const double NoRadiatorOverheatFactor = 4.0;

    public static double CarrySpeedFactor(double massKg) =>
        Math.Clamp(1.0 - massKg / CarryMassHalfSpeedKg, MinCarrySpeedFactor, 1.0);

    /// <summary>Раскладка машины или null, если у неё нет гнёзд.</summary>
    public static CarLayout? Layout(VehicleState vehicle) => CarLayout.For(vehicle.Spec.Id);

    /// <summary>Есть ли у этой машины такое гнездо (снимается ли деталь вообще).</summary>
    public static bool HasSocket(VehicleState vehicle, PartSlot slot) => Layout(vehicle)?.Find(slot) is not null;

    /// <summary>Можно ли поставить деталь в гнездо. Капот закрыт — сначала открой.</summary>
    public static AssemblyResult CanInstall(VehicleState vehicle, PartSlot slot, PartDefinition def)
    {
        var layout = Layout(vehicle);
        if (layout is null) return AssemblyResult.NotRemovable;

        var spec = layout.Find(slot);
        if (spec is null) return AssemblyResult.NotRemovable;

        if (!Fits(vehicle, slot, def)) return AssemblyResult.WrongPart;
        if (spec.NeedsAccess && !HasEngineAccess(vehicle)) return AssemblyResult.NeedsAccess;
        if (vehicle.Parts.ContainsKey(slot)) return AssemblyResult.Occupied;

        return AssemblyResult.Ok;
    }

    /// <summary>Можно ли снять установленную деталь.</summary>
    public static AssemblyResult CanRemove(VehicleState vehicle, PartSlot slot)
    {
        var layout = Layout(vehicle);
        if (layout is null) return AssemblyResult.NotRemovable;

        var spec = layout.Find(slot);
        if (spec is null) return AssemblyResult.NotRemovable;
        if (!vehicle.Parts.ContainsKey(slot)) return AssemblyResult.Empty;
        if (spec.NeedsAccess && !HasEngineAccess(vehicle)) return AssemblyResult.NeedsAccess;

        return AssemblyResult.Ok;
    }

    /// <summary>
    /// Поставить деталь в гнездо. Деталь остаётся собой: состояние сохраняется,
    /// колесо встаёт в любое свободное колесо. Поставленная панель закрыта.
    /// </summary>
    public static AssemblyResult Install(VehicleState vehicle, PartSlot slot, Part part)
    {
        var result = CanInstall(vehicle, slot, part.Definition);
        if (result != AssemblyResult.Ok) return result;

        vehicle.Parts[slot] = MoveToSlot(part, slot);
        vehicle.OpenSlots.Remove(slot);
        return AssemblyResult.Ok;
    }

    /// <summary>То же в «своё» гнездо детали.</summary>
    public static AssemblyResult Install(VehicleState vehicle, Part part) =>
        Install(vehicle, part.Slot, part);

    /// <summary>Снять деталь: она уходит в руки, состояние сохраняется. null — нельзя.</summary>
    public static Part? Remove(VehicleState vehicle, PartSlot slot)
    {
        if (CanRemove(vehicle, slot) != AssemblyResult.Ok) return null;

        var part = vehicle.Parts[slot];
        vehicle.Parts.Remove(slot);
        vehicle.OpenSlots.Remove(slot);
        return part;
    }

    /// <summary>Та же деталь в другом гнезде: колёса взаимозаменяемы, состояние то же.</summary>
    public static Part MoveToSlot(Part part, PartSlot slot)
    {
        if (part.Slot == slot) return part;

        var moved = new Part(part.Definition with { Slot = slot });
        moved.SetCondition(part.Condition);
        return moved;
    }

    /// <summary>Доступ под капот: капот открыт или снят (ADR 0003, п. 1).</summary>
    public static bool HasEngineAccess(VehicleState vehicle) =>
        vehicle.OpenSlots.Contains(PartSlot.Hood) || !vehicle.Parts.ContainsKey(PartSlot.Hood);

    /// <summary>Открыта ли панель. Снятая панель не открыта.</summary>
    public static bool IsOpen(VehicleState vehicle, PartSlot slot) =>
        vehicle.OpenSlots.Contains(slot) && vehicle.Parts.ContainsKey(slot);

    /// <summary>
    /// Открыть или закрыть панель. Открывается только установленная панель, у которой
    /// это есть в раскладке; для остального — false и ничего не меняется.
    /// </summary>
    public static bool SetOpen(VehicleState vehicle, PartSlot slot, bool open)
    {
        var layout = Layout(vehicle);
        if (layout?.Find(slot) is not { Opens: true }) return false;
        if (!vehicle.Parts.ContainsKey(slot)) return false;

        if (open) vehicle.OpenSlots.Add(slot);
        else vehicle.OpenSlots.Remove(slot);
        return true;
    }

    /// <summary>
    /// Можно ли завестись: двигатель и аккумулятор на месте и живые, в баке есть
    /// топливо, батарея заряжена. Без детали зажигание не работает.
    /// </summary>
    public static bool CanStart(VehicleState vehicle) =>
        vehicle.Get(PartSlot.Engine) is { IsBroken: false }
        && vehicle.Get(PartSlot.Battery) is { IsBroken: false }
        && vehicle.Fuel.Liters > 0.05
        && vehicle.BatteryCharge > 0.08;

    /// <summary>Во сколько раз быстрее греется мотор без радиатора (1 — радиатор на месте).</summary>
    public static double OverheatFactor(VehicleState vehicle) =>
        vehicle.Parts.ContainsKey(PartSlot.Radiator) ? 1.0 : NoRadiatorOverheatFactor;

    /// <summary>
    /// Ближайшее подходящее гнездо в радиусе примагничивания. Возвращает null,
    /// если ничего не подходит: тогда сцена кладёт деталь на землю.
    /// </summary>
    public static PartSlot? SnapTarget(
        VehicleState vehicle,
        PartDefinition def,
        IEnumerable<(PartSlot Slot, double Distance)> candidates)
    {
        PartSlot? best = null;
        double bestDistance = SnapDistance;

        foreach (var (slot, distance) in candidates)
        {
            if (distance < 0 || distance > bestDistance) continue;
            if (CanInstall(vehicle, slot, def) != AssemblyResult.Ok) continue;

            best = slot;
            bestDistance = distance;
        }

        return best;
    }

    /// <summary>Деталь подходит гнезду: свой слот (колёса — любое колесо) и своя машина.</summary>
    private static bool Fits(VehicleState vehicle, PartSlot slot, PartDefinition def)
    {
        if (def.Slot != slot && !(IsWheel(slot) && IsWheel(def.Slot))) return false;
        if (def.SpecId is null) return true;

        return string.Equals(def.SpecId, vehicle.Spec.Id, StringComparison.OrdinalIgnoreCase);
    }

    private static bool IsWheel(PartSlot slot) =>
        slot is PartSlot.WheelFrontLeft or PartSlot.WheelFrontRight
            or PartSlot.WheelRearLeft or PartSlot.WheelRearRight;
}
