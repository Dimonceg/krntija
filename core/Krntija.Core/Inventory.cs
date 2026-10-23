using Krntija.Core.Parts;

namespace Krntija.Core.Items;

public static class ItemIds
{
    public const string Wrench = "wrench";
    public const string DuctTape = "duct_tape";
    public const string JerryCan = "jerry_can";
    public const string Bread = "bread";
    public const string Water = "water";

    /// <summary>Деталь лежит в инвентаре под тем же id, что и в каталоге деталей.</summary>
    public static string Part(string partDefId) => partDefId;
}

public sealed record ItemDef(
    string Id,
    string Name,
    double WeightKg,
    bool Stackable = true,
    int MaxStack = 10);

public static class ItemCatalog
{
    private static readonly Dictionary<string, ItemDef> Items =
        new(StringComparer.OrdinalIgnoreCase)
        {
            ["wrench"] = new("wrench", "Ключ", 1.2, false),
            ["duct_tape"] = new("duct_tape", "Изолента", 0.3, true, 3),
            ["spark_plug"] = new("spark_plug", "Свеча", 0.1, true, 4),
            ["jerry_can"] = new("jerry_can", "Канистра", 5.0, false),
            ["motor_oil"] = new("motor_oil", "Масло", 4.5, true, 2),
            ["bread"] = new("bread", "Хлеб", 0.4, true, 5),
            ["water"] = new("water", "Вода", 0.6, true, 5),
            ["medkit"] = new("medkit", "Аптечка", 1.0, true, 2),
            ["spare_tire"] = new("spare_tire", "Запаска", 12.0, false),
            ["cigarettes"] = new("cigarettes", "Сигареты", 0.05, true, 20),
            ["map"] = new("map", "Карта", 0.2, false),
        };

    static ItemCatalog()
    {
        // Любую деталь можно держать в руках и нести — она становится предметом.
        foreach (var part in Parts.PartCatalog.All.Values)
            Items[part.Id] = new ItemDef(part.Id, part.Name, part.MassKg, Stackable: false);
    }

    public static IReadOnlyDictionary<string, ItemDef> All => Items;

    public static ItemDef Get(string id) =>
        Items.TryGetValue(id, out var def)
            ? def
            : throw new ArgumentException($"Неизвестный предмет: {id}", nameof(id));

    public static bool TryGet(string id, out ItemDef def) => Items.TryGetValue(id, out def!);
}

public sealed class ItemStack
{
    public ItemStack(string defId, int count = 1, double condition = 1, double liters = 0)
    {
        DefId = defId;
        Count = count;
        Condition = condition;
        Liters = liters;
    }

    public string DefId { get; }
    public int Count { get; set; }
    public double Condition { get; set; }
    public double Liters { get; set; }

    public ItemDef Definition => ItemCatalog.Get(DefId);
    public double TotalWeightKg => Definition.WeightKg * Count;
}

/// <summary>Инвентарь на фиксированное число слотов с ограничением по весу.</summary>
public sealed class Inventory
{
    private readonly ItemStack?[] _slots;

    public Inventory(int slots = 24, double maxWeightKg = 45)
    {
        if (slots <= 0) throw new ArgumentOutOfRangeException(nameof(slots));
        _slots = new ItemStack?[slots];
        MaxWeightKg = maxWeightKg;
    }

    public int SlotCount => _slots.Length;
    public double MaxWeightKg { get; }
    public IReadOnlyList<ItemStack?> Slots => _slots;

    public double WeightKg
    {
        get
        {
            double total = 0;
            foreach (var slot in _slots)
                if (slot is not null)
                    total += slot.TotalWeightKg;
            return total;
        }
    }

    public IEnumerable<ItemStack> Items
    {
        get
        {
            foreach (var slot in _slots)
                if (slot is not null)
                    yield return slot;
        }
    }

    public int Count(string defId)
    {
        int total = 0;
        foreach (var slot in _slots)
            if (slot is not null && string.Equals(slot.DefId, defId, StringComparison.OrdinalIgnoreCase))
                total += slot.Count;
        return total;
    }

    public bool TryAdd(string defId, int count = 1, double condition = 1, double liters = 0)
    {
        if (count <= 0 || !ItemCatalog.TryGet(defId, out var def))
            return false;

        double addedWeight = def.WeightKg * count;
        if (WeightKg + addedWeight > MaxWeightKg + 1e-9)
            return false;

        int remaining = count;

        if (def.Stackable)
        {
            for (int i = 0; i < _slots.Length && remaining > 0; i++)
            {
                var slot = _slots[i];
                if (slot is null || !string.Equals(slot.DefId, defId, StringComparison.OrdinalIgnoreCase))
                    continue;

                int space = def.MaxStack - slot.Count;
                if (space <= 0) continue;

                int moved = Math.Min(space, remaining);
                slot.Count += moved;
                remaining -= moved;
            }
        }

        for (int i = 0; i < _slots.Length && remaining > 0; i++)
        {
            if (_slots[i] is not null) continue;

            int moved = def.Stackable ? Math.Min(def.MaxStack, remaining) : 1;
            _slots[i] = new ItemStack(defId, moved, condition, liters);
            remaining -= moved;
        }

        return remaining == 0;
    }

    public bool TryAdd(ItemStack stack) => TryAdd(stack.DefId, stack.Count, stack.Condition, stack.Liters);

    public bool Remove(string defId, int count = 1)
    {
        if (count <= 0 || Count(defId) < count) return false;

        int remaining = count;
        for (int i = 0; i < _slots.Length && remaining > 0; i++)
        {
            var slot = _slots[i];
            if (slot is null || !string.Equals(slot.DefId, defId, StringComparison.OrdinalIgnoreCase))
                continue;

            int taken = Math.Min(slot.Count, remaining);
            slot.Count -= taken;
            remaining -= taken;
            if (slot.Count <= 0) _slots[i] = null;
        }

        return true;
    }

    public void Clear()
    {
        for (int i = 0; i < _slots.Length; i++)
            _slots[i] = null;
    }
}
