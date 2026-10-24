using Krntija.Core.World;

namespace Krntija.Core.WorldGen;

/// <summary>
/// Дорога как функция от станции s (метры вдоль оси мира). Положение, высота
/// и поворот вычисляются сразу, без прохода по предыдущим точкам, — поэтому
/// кусок №500 строится так же быстро, как нулевой.
/// Ось +S смотрит в −Z мира (в Godot это «вперёд»).
/// </summary>
public sealed class RoadCurve
{
    public const double MaxLateralSlope = 1.4281;   // tan(55°)
    public const double MinTurnRadius = 60.0;
    public const double MaxGrade = 0.08;

    /// <summary>
    /// Три волны: длинные петли (~3 км), повороты (~900 м) и мелкая извилистость
    /// (~300 м). Амплитуда и длина каждой слегка зависят от сида, поэтому миры
    /// с разным сидом различаются формой, а не только сдвигом.
    /// Худший случай (все волны в фазе): наклон ≤ 1,27 (52°), радиус ≥ 88 м —
    /// в пределах ограничений ниже.
    /// </summary>
    private readonly (double Amplitude, double Frequency, double Phase)[] _waves;

    public RoadCurve(ulong worldSeed)
    {
        var rng = new Pcg32(Hash.Mix(worldSeed, 17), sequence: 5);
        _waves = new[]
        {
            Wave(rng, amplitude: 240, wavelength: 3200),
            Wave(rng, amplitude: 60, wavelength: 900),
            Wave(rng, amplitude: 12, wavelength: 300),
        };
    }

    private static (double, double, double) Wave(Pcg32 rng, double amplitude, double wavelength) =>
        (amplitude * rng.Range(0.85, 1.0), Math.Tau / (wavelength * rng.Range(0.9, 1.15)), rng.Range(0, Math.Tau));

    /// <summary>Смещение вбок, метры. Ограничение производной гарантирует, что дорога не идёт поперёк оси.</summary>
    public double Lateral(double s)
    {
        double sum = 0;
        foreach (var (a, f, p) in _waves) sum += a * Math.Sin(s * f + p);
        return sum;
    }

    public double LateralSlope(double s)
    {
        double sum = 0;
        foreach (var (a, f, p) in _waves) sum += a * f * Math.Cos(s * f + p);
        return sum;
    }

    public double LateralCurvature(double s)
    {
        double sum = 0;
        foreach (var (a, f, p) in _waves) sum -= a * f * f * Math.Sin(s * f + p);
        return sum;
    }

    public double Height(double s) => RoadProfile.HeightAt(s);

    public double Slope(double s) =>
        5.5 * 0.0042 * Math.Cos(s * 0.0042)
        + 2.4 * 0.011 * Math.Cos(s * 0.011 + 1.3)
        + 1.1 * 0.027 * Math.Cos(s * 0.027 + 0.7);

    /// <summary>Направление дороги в радианах: 0 — точно вдоль оси +S.</summary>
    public double Heading(double s) => Math.Atan(LateralSlope(s));

    /// <summary>Точка на осевой линии: (x, y, z) в координатах мира.</summary>
    public (double X, double Y, double Z) Point(double s) => (Lateral(s), Height(s), -s);

    /// <summary>Точка на дороге со смещением вбок: знак — сторона.</summary>
    public (double X, double Y, double Z) Offset(double s, double lateral)
    {
        double heading = Heading(s);
        double rightX = Math.Cos(heading);
        double rightZ = Math.Sin(heading);
        var point = Point(s);
        return (point.X + rightX * lateral, point.Y, point.Z + rightZ * lateral);
    }

    /// <summary>
    /// Обратная к <see cref="Offset"/>: по точке мира (x, z) найти станцию
    /// поперечного сечения дороги, через которое она проходит, и смещение от оси.
    /// Нужна сцене: земля у дороги берёт высоту именно этого сечения, иначе на
    /// уклоне и повороте край полотна уходит под землю.
    /// </summary>
    public (double S, double Lateral) Project(double x, double z)
    {
        // Ось дороги C(s) = (Lateral(s), −s), касательная T = (L′, −1).
        // Точка лежит на нормали к сечению s, когда (P − C(s))·T = 0. Ньютон по s.
        double s = -z;
        for (int i = 0; i < 8; i++)
        {
            double dx = x - Lateral(s);
            double dz = z + s;
            double slope = LateralSlope(s);
            double f = dx * slope - dz;
            double df = -slope * slope + dx * LateralCurvature(s) - 1;
            double step = f / df;
            s -= step;
            if (Math.Abs(step) < 1e-4) break;
        }

        // Смещение — проекция на «вправо» (cos h, sin h) = (1, L′)/√(1 + L′²).
        double l = LateralSlope(s);
        double lateral = ((x - Lateral(s)) + (z + s) * l) / Math.Sqrt(1 + l * l);
        return (s, lateral);
    }

    /// <summary>Радиус поворота в метрах на станции s (для проверок).</summary>
    public double TurnRadius(double s)
    {
        double curvature = Math.Abs(LateralCurvature(s));
        return curvature < 1e-9 ? double.PositiveInfinity : 1.0 / curvature;
    }
}
