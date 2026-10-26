namespace Krntija.Core;

/// <summary>
/// Путь: расход, износ, рекорд и «сколько осталось до следующего тира».
/// Все числа цикла «добыть → починить → уехать дальше» живут здесь.
/// </summary>
public static class Journey
{
    /// <summary>Тир начинается каждые 3 км после первого; совпадает с WorldGen.Tier.</summary>
    public const double TierStepMeters = 3000;
    public const double FirstTierMeters = 1000;

    /// <summary>Расход на километр: с тиром дорога тяжелее, в дождь — ещё больше.</summary>
    public static double LitersPerKm(int tier, double rainIntensity = 0)
    {
        double baseRate = 0.085 + 0.012 * Math.Clamp(tier, 0, 10);
        return baseRate * (1 + 0.25 * Math.Clamp(rainIntensity, 0, 1));
    }

    /// <summary>Сколько литров уйдёт на участок пути.</summary>
    public static double FuelFor(double meters, int tier, double rainIntensity = 0) =>
        Math.Max(0, meters) / 1000.0 * LitersPerKm(tier, rainIntensity);

    /// <summary>Износ деталей на километр: дорога дальше — злее, дождь — скользко.</summary>
    public static double WearPerKm(int tier, double rainIntensity = 0)
    {
        double baseRate = 0.004 + 0.002 * Math.Clamp(tier, 0, 10);
        return baseRate * (1 + 0.4 * Math.Clamp(rainIntensity, 0, 1));
    }

    /// <summary>Множитель к закону расхода машины: на тире 0 в сухую ровно 1.</summary>
    public static double FuelFactor(int tier, double rainIntensity = 0) =>
        LitersPerKm(tier, rainIntensity) / LitersPerKm(0, 0);

    /// <summary>Множитель к износу: на тире 0 в сухую ровно 1.</summary>
    public static double WearFactor(int tier, double rainIntensity = 0) =>
        WearPerKm(tier, rainIntensity) / WearPerKm(0, 0);

    /// <summary>Сколько километров ещё можно проехать на остатке бака.</summary>
    public static double RangeKm(double liters, int tier, double rainIntensity = 0)
    {
        double rate = LitersPerKm(tier, rainIntensity);
        return rate <= 0 ? 0 : Math.Max(0, liters) / rate;
    }

    /// <summary>Сколько метров осталось до следующего тира (0 — уже на максимуме).</summary>
    public static double MetersToNextTier(double s)
    {
        int tier = WorldGen.WorldGenerator.Tier(s);
        if (tier >= WorldGen.WorldGenerator.MaxTier) return 0;

        double nextStart = tier == 0
            ? FirstTierMeters
            : FirstTierMeters + tier * TierStepMeters;

        return Math.Max(0, nextStart - s);
    }
}
