using Krntija.Core.Parts;

namespace Krntija.Core.Vehicle;

/// <summary>Бак. Литры — единственный источник правды о топливе.</summary>
public sealed class FuelTank
{
    public FuelTank(double capacityLiters, double liters = 0)
    {
        CapacityLiters = Math.Max(1, capacityLiters);
        Liters = Math.Clamp(liters, 0, CapacityLiters);
    }

    public double CapacityLiters { get; private set; }

    /// <summary>Улучшение бака: вместимость растёт, топливо не теряется.</summary>
    public void RaiseCapacity(double extraLiters)
    {
        if (extraLiters <= 0) return;
        CapacityLiters += extraLiters;
    }
    public double Liters { get; private set; }
    public double Fraction => Liters / CapacityLiters;
    public bool IsEmpty => Liters <= 0.05;

    /// <summary>Списывает топливо, возвращает сколько реально ушло.</summary>
    public double Consume(double liters)
    {
        if (liters <= 0) return 0;
        double used = Math.Min(liters, Liters);
        Liters -= used;
        return used;
    }

    /// <summary>Заливает топливо, возвращает сколько влезло.</summary>
    public double Refuel(double liters)
    {
        if (liters <= 0) return 0;
        double added = Math.Min(liters, CapacityLiters - Liters);
        Liters += added;
        return added;
    }

    public void SetLiters(double value) => Liters = Math.Clamp(value, 0, CapacityLiters);
}

/// <summary>Ввод игрока за один тик. Углы и педали — 0..1 (руль -1..1).</summary>
public readonly record struct DriveInput(
    bool Ignition = false,
    double Throttle = 0,
    double Brake = 0,
    double Steer = 0,
    bool Handbrake = false);

/// <summary>То, что сообщает физика движка: скорость, температура воздуха и т.п.</summary>
public readonly record struct VehicleSample(
    double SpeedMps,
    double AmbientC = 20,
    double Rpm = 0);

/// <summary>Крутилки баланса. Всё в одном месте, чтобы правки не расползались.</summary>
public sealed class VehicleTuning
{
    public double IdleLitersPerHour { get; init; } = 0.9;
    public double FullThrottleLitersPerHour { get; init; } = 14.0;

    /// <summary>Тепло, которое двигатель выделяет на холостых, °C/мин.</summary>
    public double IdleHeatPerMinute { get; init; } = 60;

    /// <summary>Дополнительное тепло на полном газу, °C/мин.</summary>
    public double LoadHeatPerMinute { get; init; } = 120;

    /// <summary>Порог перегрева.</summary>
    public double OverheatC { get; init; } = 110;
}

public sealed class VehicleState
{
    /// <summary>Множитель расхода от дороги и погоды: 1 — обычная езда у дома в сухую.</summary>
    public double FuelMultiplier { get; set; } = 1;

    /// <summary>Множитель износа от дороги и погоды.</summary>
    public double WearMultiplier { get; set; } = 1;

    /// <summary>Модель: масса, мощность, бак, передаточные числа.</summary>
    public CarSpec Spec { get; init; } = CarCatalog.Zastava101;

    public Dictionary<PartSlot, Part> Parts { get; } = new();
    public FuelTank Fuel { get; init; } = new(36, 24);
    public double BatteryCharge { get; set; } = 0.9;
    public bool EngineRunning { get; set; }
    public double EngineTempC { get; set; } = 20;
    public double OdometerKm { get; set; }
    public double OilLevel { get; set; } = 1.0;

    public static VehicleState CreateDefault(CarSpec? spec = null)
    {
        spec ??= CarCatalog.Zastava101;
        var state = new VehicleState
        {
            Spec = spec,
            Fuel = new FuelTank(spec.FuelCapacityL, spec.FuelStartL),
        };

        foreach (var (id, slot) in PartCatalog.StandardBuild)
            state.Install(PartCatalog.Create(id, slot));

        return state;
    }

    public void Install(Part part) => Parts[part.Slot] = part;

    public Part? Get(PartSlot slot) => Parts.TryGetValue(slot, out var part) ? part : null;

    public double Condition(PartSlot slot) => Get(slot)?.Condition ?? 0;

    public double TotalMassKg => Spec.ChassisMassKg + Fuel.Liters * 0.75 + Parts.Values.Sum(p => p.MassKg);

    /// <summary>Средний износ колёс — от него зависит сцепление.</summary>
    public double WheelCondition
    {
        get
        {
            double sum = 0;
            foreach (var slot in PartCatalog.Wheels)
                sum += Condition(slot);
            return sum / PartCatalog.Wheels.Count;
        }
    }
}

/// <summary>
/// Системы автомобиля: зажигание, расход, нагрев, износ.
/// Позицию и повороты считает физика движка — здесь только «что происходит с железом».
/// </summary>
public sealed class VehicleSystems
{
    private readonly VehicleTuning _tuning;

    public VehicleSystems(VehicleState state, VehicleTuning? tuning = null)
    {
        State = state;
        _tuning = tuning ?? new VehicleTuning();
    }

    public VehicleState State { get; }

    /// <summary>0..1 — насколько хорошо двигатель тянет прямо сейчас.</summary>
    public double PowerFactor { get; private set; }

    /// <summary>0..1 — насколько живы тормоза.</summary>
    public double BrakeFactor { get; private set; }

    /// <summary>0..1 — сцепление шин.</summary>
    public double GripFactor { get; private set; }

    public double FuelBurnLitersPerHour { get; private set; }

    /// <summary>Множитель расхода от дороги и погоды: 1 — обычная езда у дома в сухую.</summary>
    public double FuelMultiplier { get; set; } = 1;

    /// <summary>Множитель износа от дороги и погоды.</summary>
    public double WearMultiplier { get; set; } = 1;
    public bool Overheating => State.EngineTempC >= _tuning.OverheatC;

    public void Step(double dt, in VehicleSample sample, in DriveInput input)
    {
        if (dt <= 0) return;

        var st = State;
        double throttle = Math.Clamp(input.Throttle, 0, 1);
        double brake = Math.Clamp(input.Brake, 0, 1);

        double engineCondition = st.Condition(PartSlot.Engine);
        double radiatorCondition = st.Condition(PartSlot.Radiator);

        // --- зажигание ---
        bool canStart = st.Fuel.Liters > 0.05
                        && st.BatteryCharge > 0.08
                        && engineCondition > 0.02
                        && st.Condition(PartSlot.Alternator) > 0.02;

        if (input.Ignition && !st.EngineRunning && canStart)
            st.EngineRunning = true;

        if (st.Fuel.IsEmpty || engineCondition <= 0.02)
            st.EngineRunning = false;

        // --- расход ---
        if (st.EngineRunning)
        {
            double load = 0.25 + 0.75 * throttle;
            FuelBurnLitersPerHour = (_tuning.IdleLitersPerHour + _tuning.FullThrottleLitersPerHour * throttle * load)
                * Math.Clamp(st.FuelMultiplier, 0.2, 4.0);
            st.Fuel.Consume(FuelBurnLitersPerHour * dt / 3600.0);

            // Генератор потихоньку заряжает батарею.
            double alternator = st.Condition(PartSlot.Alternator);
            st.BatteryCharge = Math.Min(1.0, st.BatteryCharge + 0.0015 * alternator * dt);
        }
        else
        {
            FuelBurnLitersPerHour = 0;
            st.BatteryCharge = Math.Max(0, st.BatteryCharge - 0.00002 * dt);
        }

        // --- температура ---
        double speedKmh = Math.Abs(sample.SpeedMps) * 3.6;
        double delta = st.EngineTempC - sample.AmbientC;
        double radiatorFactor = 0.25 + 0.75 * Math.Clamp(radiatorCondition, 0, 1);

        // Вентилятор и термостат: чем горячее мотор, тем сильнее охлаждение.
        double thermostat = Math.Max(0, st.EngineTempC - 85) * 0.03;
        double coolingPerMinute = radiatorFactor * (0.7 + 0.010 * speedKmh + thermostat) * Math.Max(0, delta);
        double heatPerMinute = st.EngineRunning
            ? _tuning.IdleHeatPerMinute + _tuning.LoadHeatPerMinute * throttle * (0.25 + 0.75 * throttle)
            : 0;

        st.EngineTempC = Math.Clamp(
            st.EngineTempC + (heatPerMinute - coolingPerMinute) * (dt / 60.0),
            sample.AmbientC,
            160);

        // --- износ, всё в час ---
        double hours = dt / 3600.0;
        double overheat = Math.Max(0, st.EngineTempC - _tuning.OverheatC);

        Wear(PartSlot.Engine, (0.03 + 0.15 * throttle + 0.03 * overheat) * hours);
        Wear(PartSlot.Radiator, (0.01 + 0.02 * throttle) * hours);
        Wear(PartSlot.Gearbox, (0.01 + 0.03 * throttle) * hours);
        Wear(PartSlot.BrakesFront, 0.15 * brake * hours);
        Wear(PartSlot.BrakesRear, 0.10 * brake * hours);
        Wear(PartSlot.SuspensionFront, 0.02 * speedKmh / 100.0 * hours);
        Wear(PartSlot.SuspensionRear, 0.02 * speedKmh / 100.0 * hours);
        Wear(PartSlot.Body, 0.002 * hours);

        // Шины стираются по километрам: комплект живёт примерно 2.5 тысячи км.
        double km = Math.Abs(sample.SpeedMps) * dt / 1000.0;
        foreach (var wheel in PartCatalog.Wheels)
            Wear(wheel, 0.0006 * km);

        st.OdometerKm += km;

        // --- характеристики для представления ---
        double heatPenalty = st.EngineTempC > _tuning.OverheatC + 4
            ? Math.Clamp(1.0 - (st.EngineTempC - _tuning.OverheatC - 4) / 40.0, 0.3, 1.0)
            : 1.0;

        PowerFactor = st.EngineRunning
            ? Math.Clamp(0.35 + 0.65 * engineCondition, 0, 1) * heatPenalty * (0.9 + 0.1 * Math.Clamp(st.OilLevel, 0, 1))
            : 0;

        BrakeFactor = Math.Clamp(0.3 + 0.7 * (st.Condition(PartSlot.BrakesFront) * 0.6 + st.Condition(PartSlot.BrakesRear) * 0.4), 0, 1);
        GripFactor = Math.Clamp(0.35 + 0.65 * st.WheelCondition, 0, 1);
    }

    public void Repair(PartSlot slot, double amount) => State.Get(slot)?.Repair(amount);

    private void Wear(PartSlot slot, double amount)
    {
        if (amount <= 0) return;
        State.Get(slot)?.Wear(amount * Math.Clamp(State.WearMultiplier, 0.2, 4.0));
    }
}
