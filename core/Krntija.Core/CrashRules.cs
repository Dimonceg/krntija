namespace Krntija.Core;

/// <summary>
/// Удар: потеря скорости за кадр превращается в износ кузова. Числа здесь,
/// чтобы кенгурятник и качество деталей правились без запуска игры.
/// </summary>
public static class CrashRules
{
    /// <summary>Потеря скорости ниже этого порога — это торможение, а не удар.</summary>
    public const double ThresholdMps = 3.0;

    /// <summary>Износ кузова за удар: растёт с потерей скорости, но не убивает машину сразу.</summary>
    public static double BodyWear(double speedDropMps)
    {
        if (speedDropMps < ThresholdMps) return 0;

        double excess = speedDropMps - ThresholdMps;
        return Math.Clamp(0.004 + excess * 0.010, 0, 0.35);
    }

    /// <summary>Тот же удар с учётом защиты: кенгурятник уменьшает износ.</summary>
    public static double BodyWear(double speedDropMps, double damageFactor) =>
        BodyWear(speedDropMps) * Math.Clamp(damageFactor, 0.2, 1.0);
}
