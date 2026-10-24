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

    private readonly double _phase1;
    private readonly double _phase2;
    private readonly double _phase3;

    public RoadCurve(ulong worldSeed)
    {
        ulong hash = Hash.Mix(worldSeed, 17);
        _phase1 = (hash % 1000) / 1000.0 * Math.Tau;
        _phase2 = (hash >> 10) % 1000 / 1000.0 * Math.Tau;
        _phase3 = (hash >> 20) % 1000 / 1000.0 * Math.Tau;
    }

    /// <summary>Смещение вбок, метры. Ограничение производной гарантирует, что дорога не идёт поперёк оси.</summary>
    public double Lateral(double s) =>
        30.0 * Math.Sin(s * 0.0031 + _phase1)
        + 12.0 * Math.Sin(s * 0.0087 + _phase2)
        + 5.0 * Math.Sin(s * 0.0210 + _phase3);

    public double LateralSlope(double s) =>
        30.0 * 0.0031 * Math.Cos(s * 0.0031 + _phase1)
        + 12.0 * 0.0087 * Math.Cos(s * 0.0087 + _phase2)
        + 5.0 * 0.0210 * Math.Cos(s * 0.0210 + _phase3);

    public double LateralCurvature(double s) =>
        -30.0 * 0.0031 * 0.0031 * Math.Sin(s * 0.0031 + _phase1)
        - 12.0 * 0.0087 * 0.0087 * Math.Sin(s * 0.0087 + _phase2)
        - 5.0 * 0.0210 * 0.0210 * Math.Sin(s * 0.0210 + _phase3);

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

    /// <summary>Радиус поворота в метрах на станции s (для проверок).</summary>
    public double TurnRadius(double s)
    {
        double curvature = Math.Abs(LateralCurvature(s));
        return curvature < 1e-9 ? double.PositiveInfinity : 1.0 / curvature;
    }
}
