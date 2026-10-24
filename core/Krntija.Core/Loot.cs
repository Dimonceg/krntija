using Krntija.Core.World;

namespace Krntija.Core.Items;

/// <summary>Переносимый вес и его влияние на скорость пешехода.</summary>
public static class Encumbrance
{
    /// <summary>До 80 % веса идёшь как обычно, дальше — всё медленнее, до 55 %.</summary>
    public static float SpeedFactor(double weightKg, double maxWeightKg)
    {
        if (maxWeightKg <= 0) return 1f;

        double load = weightKg / maxWeightKg;
        if (load <= 0.8) return 1f;

        double over = (load - 0.8) / 0.2;          // 0…1 при перегрузе
        return (float)Math.Clamp(1.0 - over * 0.45, 0.55, 1.0);
    }
}

/// <summary>Что это за вещь: от категории зависит иконка, цвет и кому продавать.</summary>
public enum ItemCategory
{
    Consumable,
    Part,
    Junk,
    Tool,
    Money,
}

/// <summary>Одна строка таблицы добычи: что, сколько и с каким весом выпадает.</summary>
public sealed record LootEntry(string ItemId, int MinCount, int MaxCount, double Weight = 1);

/// <summary>
/// Набор предметов, который лежит в конкретном месте: на заправке, в селе,
/// в руинах, на ферме или в доме игрока.
/// </summary>
public sealed class LootTable
{
    public LootTable(string id, int minItems, int maxItems, params LootEntry[] entries)
    {
        if (entries.Length == 0) throw new ArgumentException("В таблице нет предметов", nameof(entries));

        Id = id;
        MinItems = Math.Max(0, minItems);
        MaxItems = Math.Max(MinItems, maxItems);
        Entries = entries;
    }

    public string Id { get; }
    public int MinItems { get; }
    public int MaxItems { get; }
    public IReadOnlyList<LootEntry> Entries { get; }
}

/// <summary>Каталог мест: каждая точка интереса ссылается на свою таблицу.</summary>
public static class LootTables
{
    public static readonly LootTable GasStation = new(
        "gas_station", 1, 3,
        new LootEntry("jerry_can", 1, 1, 2),
        new LootEntry("motor_oil", 1, 1, 1.5),
        new LootEntry("cigarettes", 1, 2, 2),
        new LootEntry("duct_tape", 1, 1, 1),
        new LootEntry("cassette", 1, 2, 2),
        new LootEntry("radio_part", 1, 1, 1.5));

    public static readonly LootTable Village = new(
        "village", 2, 4,
        new LootEntry("bread", 1, 2, 2),
        new LootEntry("water", 1, 2, 2),
        new LootEntry("medkit", 1, 1, 0.6),
        new LootEntry("wrench", 1, 1, 0.5),
        new LootEntry("cigarettes", 1, 3, 1.5),
        new LootEntry("cassette", 1, 3, 2),
        new LootEntry("radio_part", 1, 2, 1.2));

    public static readonly LootTable Ruins = new(
        "ruins", 1, 3,
        new LootEntry("scrap_metal", 2, 5, 3),
        new LootEntry("radio_part", 1, 2, 2),
        new LootEntry("cassette", 1, 2, 1.5),
        new LootEntry("bottle_cap", 2, 5, 2),
        new LootEntry("upgrade_tank", 1, 1, 0.5));

    public static readonly LootTable Farm = new(
        "farm", 1, 3,
        new LootEntry("spark_plug", 1, 2, 2),
        new LootEntry("motor_oil", 1, 1, 1.5),
        new LootEntry("bread", 1, 2, 1.5),
        new LootEntry("duct_tape", 1, 1, 1),
        new LootEntry("scrap_metal", 1, 3, 1.5),
        new LootEntry("upgrade_springs", 1, 1, 0.4),
        new LootEntry("upgrade_toolbox", 1, 1, 0.4));

    public static readonly LootTable House = new(
        "house", 3, 5,
        new LootEntry("bread", 1, 2, 2),
        new LootEntry("water", 1, 2, 2),
        new LootEntry("medkit", 1, 1, 1),
        new LootEntry("jerry_can", 1, 1, 1.2),
        new LootEntry("wrench", 1, 1, 0.8),
        new LootEntry("cassette", 1, 2, 1.5),
        new LootEntry("radio_part", 1, 1, 1));

    public static readonly LootTable Roadside = new(
        "roadside", 1, 2,
        new LootEntry("bottle_cap", 1, 3, 3),
        new LootEntry("scrap_metal", 1, 2, 2),
        new LootEntry("bread", 1, 1, 1),
        new LootEntry("duct_tape", 1, 1, 1));
}

/// <summary>
/// Детерминированный выбор добычи: один и тот же сид даёт один и тот же набор.
/// Нужно, чтобы в коопе у всех падало одинаково и чтобы тесты были стабильны.
/// </summary>
public static class LootRoll
{
    public static List<ItemStack> Roll(LootTable table, Pcg32 rng)
    {
        var result = new List<ItemStack>();
        int count = table.MinItems + rng.NextInt(table.MaxItems - table.MinItems + 1);

        double totalWeight = 0;
        foreach (var entry in table.Entries) totalWeight += entry.Weight;

        for (int i = 0; i < count; i++)
        {
            double pick = rng.Range(0, totalWeight);
            LootEntry chosen = table.Entries[^1];

            foreach (var entry in table.Entries)
            {
                pick -= entry.Weight;
                if (pick <= 0)
                {
                    chosen = entry;
                    break;
                }
            }

            int amount = chosen.MinCount + rng.NextInt(chosen.MaxCount - chosen.MinCount + 1);
            if (amount > 0) result.Add(new ItemStack(chosen.ItemId, amount));
        }

        return result;
    }
}
