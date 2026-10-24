using Krntija.Core.Workshop;

namespace Krntija.Core.Items;

/// <summary>
/// Что можно сделать с предметом из сумки. Правила — в ядре, чтобы их можно
/// было проверить тестами и переиспользовать на сервере.
/// </summary>
public static class ItemUseService
{
    public static bool TryUse(GameSession session, string itemId, out string message)
    {
        message = string.Empty;
        var bag = session.Inventory;

        if (bag.Count(itemId) == 0)
            return false;

        switch (itemId)
        {
            case ItemIds.Water:
                bag.Remove(itemId, 1);
                session.Survival.Drink(0.35);
                message = "выпита, жажда -35%";
                return true;

            case ItemIds.Bread:
                bag.Remove(itemId, 1);
                session.Survival.Eat(0.25);
                message = "съеден, голод -25%";
                return true;

            case "medkit":
                bag.Remove(itemId, 1);
                session.Survival.Heal(0.3);
                message = "здоровье +30%";
                return true;

            case "motor_oil":
                bag.Remove(itemId, 1);
                session.Vehicle.OilLevel = Math.Clamp(session.Vehicle.OilLevel + 0.3, 0, 1);
                message = $"долито, масло {session.Vehicle.OilLevel * 100:F0}%";
                return true;

            case ItemIds.JerryCan:
                double poured = VehicleService.RefuelFromCan(session.Vehicle, bag);
                if (poured <= 0.01)
                {
                    message = "канистра пуста";
                    return false;
                }

                message = $"залито {poured:F1} л";
                return true;

            case "cigarettes":
                session.Survival.TakeDamage(0.01);
                message = "подымил, здоровью не легче";
                return true;

            default:
                message = "с этим пока ничего не сделать";
                return false;
        }
    }
}
