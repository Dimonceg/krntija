namespace Krntija.Core.WorldGen;

/// <summary>
/// Опасность и цена по тиру: чем дальше от дома, тем больше угроз и дороже
/// проезд. Все числа здесь, чтобы баланс правился в одном месте.
/// </summary>
public static class ThreatRules
{
    /// <summary>Проезд через блокпост: 15 $ на первом километре, дальше дороже.</summary>
    public static double TollPrice(int tier) => 15 + 5 * Math.Clamp(tier, 0, WorldGenerator.MaxTier);

    /// <summary>Сколько волков у сарая. До тира 1 их нет: старт должен быть спокойным.</summary>
    public static int WolvesAt(int tier) => tier < 1 ? 0 : Math.Clamp(1 + tier / 2, 1, 4);

    /// <summary>Завал на руинах: появляется со второго тира, дальше — всегда.</summary>
    public static bool HasCollapse(int tier) => tier >= 2;

    /// <summary>Урон одного укуса стаи: с тиром больнее, но есть предел.</summary>
    public static double WolfDamage(int tier) =>
        Math.Clamp(0.03 + 0.01 * Math.Clamp(tier, 0, WorldGenerator.MaxTier), 0.03, 0.12);

    /// <summary>Шанс, что место с лутом окажется с «сюрпризом» — растёт с тиром.</summary>
    public static double DangerChance(int tier) => Math.Clamp(0.1 + tier * 0.08, 0.1, 0.85);
}
