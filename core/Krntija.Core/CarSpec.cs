namespace Krntija.Core.Vehicle;

/// <summary>
/// Модель автомобиля как данные, а не как код. Один и тот же симулятор
/// крутит «стоеднаестку», «двойку» и «шестёрку» — отличаются только числа.
/// </summary>
public sealed record CarSpec(
    string Id,
    string Name,
    double ChassisMassKg,
    double PowerKw,
    double TopSpeedMps,
    double FuelCapacityL,
    double FuelStartL,
    double WheelRadiusM,
    double FinalDrive,
    IReadOnlyList<double> GearRatios,
    double IdleRpm = 800,
    double MaxRpm = 6200,
    double ReverseRatio = 3.4)
{
    /// <summary>Тяга на ведущей оси при нулевой скорости, Н (упрощённо).</summary>
    public double MaxTractionN => PowerKw * 1000 / 4.0;
}

public static class CarCatalog
{
    public static readonly CarSpec Zastava101 = new(
        Id: "zastava_101",
        Name: "Zastava 101",
        ChassisMassKg: 900,
        PowerKw: 40,
        TopSpeedMps: 33,
        FuelCapacityL: 36,
        FuelStartL: 24,
        WheelRadiusM: 0.29,
        FinalDrive: 3.9,
        GearRatios: new[] { 3.53, 2.04, 1.35, 1.00 });

    public static readonly CarSpec Golf2 = new(
        Id: "golf_2",
        Name: "Golf Mk2",
        ChassisMassKg: 970,
        PowerKw: 51,
        TopSpeedMps: 39,
        FuelCapacityL: 55,
        FuelStartL: 30,
        WheelRadiusM: 0.30,
        FinalDrive: 3.94,
        GearRatios: new[] { 3.45, 1.94, 1.29, 0.97, 0.76 });

    public static readonly CarSpec Vaz2106 = new(
        Id: "vaz_2106",
        Name: "ВАЗ-2106",
        ChassisMassKg: 1050,
        PowerKw: 55,
        TopSpeedMps: 37,
        FuelCapacityL: 40,
        FuelStartL: 25,
        WheelRadiusM: 0.31,
        FinalDrive: 3.9,
        GearRatios: new[] { 3.67, 2.10, 1.36, 1.00 });

    /// <summary>Заднемоторный, лёгкий и злой. Идеальная машина для плохой дороги.</summary>
    public static readonly CarSpec Zaz968 = new(
        Id: "zaz_968",
        Name: "ЗАЗ-968 «Запорожец»",
        ChassisMassKg: 840,
        PowerKw: 30,
        TopSpeedMps: 31,
        FuelCapacityL: 40,
        FuelStartL: 22,
        WheelRadiusM: 0.28,
        FinalDrive: 4.1,
        GearRatios: new[] { 3.80, 2.10, 1.40, 1.00 },
        MaxRpm: 5600);

    public static readonly IReadOnlyList<CarSpec> All = new[] { Zastava101, Golf2, Vaz2106, Zaz968 };

    public static CarSpec Get(string id) =>
        All.FirstOrDefault(s => string.Equals(s.Id, id, StringComparison.OrdinalIgnoreCase))
        ?? Zastava101;
}

/// <summary>
/// Коробка передач и обороты. Пока автомат: в M2 дадим игроку ручное
/// переключение и сцепление, но интерфейс останется тем же.
/// </summary>
public sealed class DriveTrain
{
    private const double ShiftUpRpm = 5600;
    private const double ShiftDownRpm = 1400;

    public DriveTrain(CarSpec spec)
    {
        Spec = spec;
        Rpm = spec.IdleRpm;
    }

    public CarSpec Spec { get; }
    public int Gear { get; private set; } = 1;
    public double Rpm { get; private set; }

    public bool IsRedline => Rpm >= Spec.MaxRpm * 0.95;

    public bool IsReverse => Gear == -1;

    public string GearText => Gear == -1 ? "R" : Gear.ToString();

    public void Update(double speedMps, double throttle, double dt, bool reverse = false)
    {
        if (dt <= 0) return;

        double wheelRpm = Math.Abs(speedMps) / (2 * Math.PI * Spec.WheelRadiusM) * 60;

        double k = Math.Clamp(dt * 8.0, 0, 1);

        if (reverse)
        {
            Gear = -1;
            double reverseTarget = Math.Clamp(
                wheelRpm * Spec.ReverseRatio * Spec.FinalDrive,
                Spec.IdleRpm,
                Spec.MaxRpm * 0.6);

            Rpm += (reverseTarget - Rpm) * k;
            return;
        }

        if (Gear == -1) Gear = 1;

        double RpmInGear(int gear) => wheelRpm * Spec.GearRatios[gear - 1] * Spec.FinalDrive;

        if (RpmInGear(Gear) > ShiftUpRpm && Gear < Spec.GearRatios.Count)
            Gear++;
        else if (Gear > 1 && RpmInGear(Gear) < ShiftDownRpm)
            Gear--;

        double target = Math.Clamp(RpmInGear(Gear), Spec.IdleRpm, Spec.MaxRpm);
        if (throttle < 0.05) target = Math.Max(Spec.IdleRpm, target * 0.9);

        // Сглаживание, чтобы стрелка не дёргалась.
        Rpm += (target - Rpm) * k;
    }

    public void Reset()
    {
        Gear = 1;
        Rpm = Spec.IdleRpm;
    }
}
