using Krntija.Core.Items;
using Krntija.Core.Parts;
using Krntija.Core.Vehicle;

namespace Krntija.Core.Workshop;

/// <summary>
/// Всё, что делает игрок с машиной: снять/поставить деталь, залепить изолентой,
/// залить топливо из канистры. Живёт в ядре, потому что это правила мира,
/// а не анимации.
/// </summary>
public static class VehicleService
{
    /// <summary>Ставит новую деталь из инвентаря. Старая уезжает обратно в сумку.</summary>
    public static bool TryReplacePart(VehicleState car, Inventory bag, string partDefId, out Part? replaced) =>
        TryReplacePart(car, bag, partDefId, null, out replaced);

    /// <summary>
    /// То же, но с явным слотом: нужно для колёс, у которых один каталог на все
    /// четыре позиции. Без слота деталь встанет в «свой» слот по каталогу.
    /// </summary>
    public static bool TryReplacePart(
        VehicleState car,
        Inventory bag,
        string partDefId,
        PartSlot? targetSlot,
        out Part? replaced)
    {
        replaced = null;

        if (!PartCatalog.All.TryGetValue(partDefId, out var def))
            return false;

        var slot = targetSlot ?? def.Slot;

        // Кривая деталь в чужой слот не встанет: только колёса взаимозаменяемы.
        bool compatible = def.Slot == slot || (IsWheel(def.Slot) && IsWheel(slot));
        if (!compatible)
            return false;

        // Деталь должна физически лежать в инвентаре.
        if (!bag.Remove(partDefId, 1))
            return false;

        replaced = car.Get(slot);
        car.Install(PartCatalog.Create(partDefId, slot));

        // Старую деталь забираем, если влезает по весу; иначе она остаётся на дороге.
        if (replaced is not null)
            bag.TryAdd(ItemIds.Part(replaced.Definition.Id), 1, replaced.Condition);

        return true;
    }

    /// <summary>Первая подходящая деталь в сумке — для журнала и «поставить».</summary>
    public static string? FindPartInBag(Inventory bag, PartSlot slot) =>
        bag.Items
            .Select(i => i.DefId)
            .FirstOrDefault(id => PartCatalog.All.TryGetValue(id, out var def) && (def.Slot == slot || (IsWheel(def.Slot) && IsWheel(slot))));

    private static bool IsWheel(PartSlot slot) =>
        slot is PartSlot.WheelFrontLeft or PartSlot.WheelFrontRight
             or PartSlot.WheelRearLeft or PartSlot.WheelRearRight;

    /// <summary>Подмотнуть изолентой: +20% состояния за моток.</summary>
    public static bool TryRepair(VehicleState car, Inventory bag, PartSlot slot, double amount = 0.2)
    {
        var part = car.Get(slot);
        if (part is null || part.Condition >= 0.999)
            return false;

        if (!bag.Remove(ItemIds.DuctTape, 1))
            return false;

        part.Repair(amount);
        return true;
    }

    /// <summary>Залить из канистры. Возвращает, сколько литров реально ушло.</summary>
    public static double RefuelFromCan(VehicleState car, Inventory bag)
    {
        var can = bag.Items.FirstOrDefault(i => i.DefId == ItemIds.JerryCan && i.Liters > 0.01);
        if (can is null)
            return 0;

        double moved = car.Fuel.Refuel(can.Liters);
        can.Liters -= moved;
        return moved;
    }

    /// <summary>Самая убитая деталь — на неё смотрит журнал.</summary>
    public static Part? WorstPart(VehicleState car) =>
        car.Parts.Values.OrderBy(p => p.Condition).FirstOrDefault();
}
