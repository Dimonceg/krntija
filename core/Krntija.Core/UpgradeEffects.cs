namespace Krntija.Core;

/// <summary>
/// Числа эффектов улучшений. Сцена читает их для фар и вида, ядро — для
/// вместимости, мощности и урона; дублировать числа в сцене нельзя.
/// </summary>
public static class UpgradeEffects
{
    /// <summary>Форсированный двигатель: множитель мощности.</summary>
    public const double EnginePower = 1.15;

    /// <summary>Кенгурятник: во сколько раз меньше урона машине от удара.</summary>
    public const double CrashDamage = 0.6;

    /// <summary>Багажник на крышу: прибавка к багажнику, кг.</summary>
    public const double RoofRackKg = 25;

    /// <summary>Фары: дальность света в метрах (обычные — 30).</summary>
    public static double LightsRange(bool upgraded) => upgraded ? 48 : 30;
}
