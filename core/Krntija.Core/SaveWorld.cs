using System.Text.RegularExpressions;

namespace Krntija.Core;

/// <summary>
/// Сейв и мир, в который его загружают (ADR 0002, фаза 2в). Мир строится по
/// сиду, сейв хранит только позицию машины и то, что игрок в мире изменил.
/// Если мир другой, позиция и состояние мира из сейва ничего не значат.
/// </summary>
public static class SaveWorld
{
    /// <summary>Бесконечный мир кусками по плану из ядра.</summary>
    public const string Chunks = "chunks";

    /// <summary>Старый мир на 2,5 км (до конца фазы 3, флаг --world=legacy).</summary>
    public const string Legacy = "legacy";

    /// <summary>Id из плана: <c>loot:c12/p3/l1</c>, <c>gate:c2/p0/u0</c>, <c>place:c4/p1</c>.</summary>
    private static readonly Regex PlanId = new(@"^[a-z_]+:c\d+/p\d+(/[a-z]\d+)?$", RegexOptions.CultureInvariant);

    /// <summary>Id в мире кусками — из плана, а не номер точки старой сцены.</summary>
    public static bool IsPlanId(string id) => PlanId.IsMatch(id);

    /// <summary>
    /// Согласовать сейв с миром, в который его загружают. Машина, деньги,
    /// инвентарь и время остаются всегда. Позиция — только если мир тот же
    /// (тип и сид). Состояние мира — только id, которые в этом мире существуют.
    /// </summary>
    /// <returns>True — позиция машины из сейва годится; иначе ставить на старт.</returns>
    public static bool Prepare(SaveData data, string world, ulong seed)
    {
        // До версии 3 мир кусками не сохранялся: старые сейвы — из старого мира.
        if (data.Version < 3)
        {
            data.World = Legacy;
            data.WorldSeed = 0;
        }

        bool sameWorld = data.World == world && (world == Legacy || data.WorldSeed == seed);
        if (sameWorld) return true;

        // Другой мир: id старой сцены (loot:poi:3) или чужого сида тут не значат ничего.
        data.WorldDone = world == Chunks && data.World == Chunks
            ? new List<string>()
            : data.WorldDone.Where(id => world == Chunks ? IsPlanId(id) : !IsPlanId(id)).ToList();
        return false;
    }
}
