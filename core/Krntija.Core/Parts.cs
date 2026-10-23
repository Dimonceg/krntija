namespace Krntija.Core.Parts;

/// <summary>Слот, в который ставится деталь. Один слот — одна деталь.</summary>
public enum PartSlot
{
    Engine,
    Gearbox,
    Radiator,
    Alternator,
    Battery,
    BrakesFront,
    BrakesRear,
    SuspensionFront,
    SuspensionRear,
    WheelFrontLeft,
    WheelFrontRight,
    WheelRearLeft,
    WheelRearRight,
    Body,
}

/// <summary>Описание типа детали: то, что не меняется от экземпляра к экземпляру.</summary>
public sealed record PartDefinition(
    string Id,
    PartSlot Slot,
    string Name,
    double MassKg,
    double Price,
    double WearPerHour);

/// <summary>Установленная деталь с её состоянием.</summary>
public sealed class Part
{
    public Part(PartDefinition definition)
    {
        Definition = definition;
        Condition = 1.0;
    }

    public PartDefinition Definition { get; }
    public PartSlot Slot => Definition.Slot;
    public string Name => Definition.Name;

    /// <summary>1 — новая, 0 — убитая. Ниже 0.02 деталь считается нерабочей.</summary>
    public double Condition { get; private set; }

    public bool IsBroken => Condition <= 0.02;

    /// <summary>Множитель эффективности детали: 0.2 у убитой, 1.0 у новой.</summary>
    public double HealthFactor => 0.2 + 0.8 * Condition;

    public double MassKg => Definition.MassKg;
    public double Price => Definition.Price;

    public void Wear(double amount)
    {
        if (amount <= 0) return;
        Condition = Math.Clamp(Condition - amount, 0, 1);
    }

    public void Repair(double amount)
    {
        if (amount <= 0) return;
        Condition = Math.Clamp(Condition + amount, 0, 1);
    }

    public void Restore() => Condition = 1.0;

    public void SetCondition(double value) => Condition = Math.Clamp(value, 0, 1);
}

public static class PartCatalog
{
    public static readonly IReadOnlyDictionary<string, PartDefinition> All =
        new Dictionary<string, PartDefinition>(StringComparer.OrdinalIgnoreCase)
        {
            ["engine_1_6"] = new("engine_1_6", PartSlot.Engine, "Двигатель 1.6", 140, 900, 0.10),
            ["gearbox_4sp"] = new("gearbox_4sp", PartSlot.Gearbox, "КПП 4-ст.", 45, 400, 0.04),
            ["radiator_std"] = new("radiator_std", PartSlot.Radiator, "Радиатор", 8, 120, 0.06),
            ["alternator_std"] = new("alternator_std", PartSlot.Alternator, "Генератор", 6, 90, 0.03),
            ["battery_std"] = new("battery_std", PartSlot.Battery, "Аккумулятор", 15, 80, 0.02),
            ["brakes_front"] = new("brakes_front", PartSlot.BrakesFront, "Тормоза (перед)", 12, 70, 0.08),
            ["brakes_rear"] = new("brakes_rear", PartSlot.BrakesRear, "Тормоза (зад)", 8, 50, 0.06),
            ["susp_front"] = new("susp_front", PartSlot.SuspensionFront, "Подвеска (перед)", 30, 150, 0.05),
            ["susp_rear"] = new("susp_rear", PartSlot.SuspensionRear, "Подвеска (зад)", 25, 130, 0.05),
            ["wheel_steel"] = new("wheel_steel", PartSlot.WheelFrontLeft, "Колесо (сталь)", 12, 40, 0.03),
            ["body_panel"] = new("body_panel", PartSlot.Body, "Кузов", 60, 200, 0.01),
        };

    /// <summary>Базовый набор деталей «как сошла с завода»: слот задаётся явно.</summary>
    public static readonly IReadOnlyList<(string Id, PartSlot Slot)> StandardBuild = new[]
    {
        ("engine_1_6", PartSlot.Engine),
        ("gearbox_4sp", PartSlot.Gearbox),
        ("radiator_std", PartSlot.Radiator),
        ("alternator_std", PartSlot.Alternator),
        ("battery_std", PartSlot.Battery),
        ("brakes_front", PartSlot.BrakesFront),
        ("brakes_rear", PartSlot.BrakesRear),
        ("susp_front", PartSlot.SuspensionFront),
        ("susp_rear", PartSlot.SuspensionRear),
        ("body_panel", PartSlot.Body),
        ("wheel_steel", PartSlot.WheelFrontLeft),
        ("wheel_steel", PartSlot.WheelFrontRight),
        ("wheel_steel", PartSlot.WheelRearLeft),
        ("wheel_steel", PartSlot.WheelRearRight),
    };

    public static readonly IReadOnlyList<PartSlot> Wheels = new[]
    {
        PartSlot.WheelFrontLeft, PartSlot.WheelFrontRight,
        PartSlot.WheelRearLeft, PartSlot.WheelRearRight,
    };

    public static Part Create(string id) => Create(id, null);

    public static Part Create(string id, PartSlot? slot)
    {
        if (!All.TryGetValue(id, out var def))
            throw new ArgumentException($"Неизвестная деталь: {id}", nameof(id));

        if (slot is { } s && s != def.Slot)
            def = def with { Slot = s };

        return new Part(def);
    }
}
